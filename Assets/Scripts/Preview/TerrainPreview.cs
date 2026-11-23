using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 지형이 생성되는 과정을 눈으로 보는 미리보기 도구. 세이브(world.bin)를 거치지 않는다.
/// </summary>
/// <remarks>
/// 게임과 같은 코드를 그대로 쓴다.
///  - 높이: <see cref="ChunkGenerator.Generate"/>
///  - 메시: <see cref="ChunkInstance.BuildMesh"/>
///  - 시드 · 평지 중심 · 생성 순서(가운데부터): <see cref="WorldPreGenerator.SpawnPoint"/>, <see cref="WorldPreGenerator.BuildCoordList"/>
///  - 광맥: <see cref="ChunkGenerator.GetOreDistribution"/>
/// 그래서 같은 시드·맵 크기면 실제 새 게임과 같은 지형이 나온다.
/// 바꾸는 것은 표시뿐이다: 청크마다 64×64 텍스처를 칠해 높이·광맥을 색으로 보여 준다(MaterialPropertyBlock, 머티리얼 복제 없음).
/// </remarks>
public class TerrainPreview : MonoBehaviour
{
    public enum ViewMode { Height, Ore, Plain }

    [Serializable]
    public class OreColor
    {
        public string oreId;
        public Color  color = Color.white;
    }

    [Header("월드 — WorldPreGenerator와 같은 의미")]
    [SerializeField]          private float _seed;
    [SerializeField]          private bool  _randomSeedOnStart;
    [SerializeField, Min(1)]  private int   _mapWidth  = 8;
    [SerializeField, Min(1)]  private int   _mapHeight = 8;

    [Header("진행 속도 — 천천히 보고 싶으면 간격을 늘린다")]
    [SerializeField, Min(1)]  private int   _chunksPerStep = 1;
    [SerializeField, Min(0f)] private float _stepInterval  = 0.05f;
    [Tooltip("청크가 바닥에서 솟아오르는 연출 시간(초). 0이면 바로 나타난다.")]
    [SerializeField, Min(0f)] private float _riseDuration  = 0.4f;

    [Header("표시")]
    [SerializeField] private ViewMode      _viewMode = ViewMode.Height;
    [Tooltip("게임의 Chunk 프리팹. 비워 두면 ChunkInstance를 코드로 붙여 만든다.")]
    [SerializeField] private ChunkInstance _chunkPrefab;
    [Tooltip("비워 두면 Standard 셰이더로 만든다. 프리팹을 쓰면 프리팹의 머티리얼을 쓴다.")]
    [SerializeField] private Material      _material;
    [SerializeField] private Gradient      _heightGradient = DefaultGradient();
    [Tooltip("그라디언트 끝(1)에 해당하는 높이. SampleTerrain의 최대치는 약 26.")]
    [SerializeField, Min(0.01f)] private float _maxHeight = 26f;
    [SerializeField] private List<OreColor> _oreColors = DefaultOreColors();
    [SerializeField] private bool _autoFrameCamera = true;
    [SerializeField] private bool _showGui         = true;

    private class Built
    {
        public ChunkInstance chunk;
        public Texture2D     texture;
        public float         bornTime;
    }

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex"); // Built-in (Standard)
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"); // URP로 바꿔도 동작하게

    private readonly List<Vector2Int>                  _pending = new();
    private readonly Dictionary<Vector2Int, Built>     _built   = new();
    private readonly List<Built>                       _rising  = new();
    private readonly List<(string oreId, float probability)> _oreBuffer = new();
    private readonly Dictionary<string, Color>         _oreColorMap = new();
    private readonly Stopwatch                         _sw = new();

    private Color32[]             _pixels;
    private Material              _runtimeMaterial;   // 직접 만든 것만 파괴한다
    private MaterialPropertyBlock _mpb;
    private ViewMode              _paintedMode;
    private Vector3               _spawn;
    private float                 _nextStepTime;
    private bool                  _paused;
    private int                   _total;
    private double                _generateMs, _meshMs, _paintMs;

    // ── 수명 ─────────────────────────────────────────────────

    private void Awake()
    {
        _mpb    = new MaterialPropertyBlock();
        _pixels = new Color32[ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE];
        RebuildOreColorMap();

        if (_chunkPrefab == null && _material == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) Debug.LogWarning("[TerrainPreview] Standard 셰이더가 없습니다 — _material을 지정하세요.", this);
            else _runtimeMaterial = new Material(shader) { name = "TerrainPreview (runtime)" };
        }
    }

    private void Start()
    {
        if (_randomSeedOnStart) _seed = NewSeed();
        Restart();
    }

    private void OnDestroy()
    {
        ClearAll();
        if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
    }

    private void OnValidate()
    {
        // 인스펙터에서 색을 바꾸면 다음 프레임에 다시 칠한다
        if (!Application.isPlaying || _mpb == null) return;
        RebuildOreColorMap();
        _paintedMode = (ViewMode)(-1);
    }

    private void Update()
    {
        HandleInput();
        if (_viewMode != _paintedMode) RepaintAll();
        AnimateRise();

        if (_pending.Count == 0 || _paused || Time.time < _nextStepTime) return;
        _nextStepTime = Time.time + _stepInterval;
        Step();
    }

    // ── 생성 ─────────────────────────────────────────────────

    /// <summary>지금까지 만든 청크를 지우고 현재 시드로 처음부터 다시 생성한다.</summary>
    public void Restart()
    {
        ClearAll();

        // WorldPreGenerator.GenerateAll과 같은 준비 — 그래서 같은 지형이 나온다
        _spawn = WorldPreGenerator.SpawnPoint(_mapWidth, _mapHeight);
        ChunkGenerator.SetSeed(_seed);
        ChunkGenerator.SetFlatCenter(new Vector2(_spawn.x, _spawn.z));

        _pending.AddRange(WorldPreGenerator.BuildCoordList(_mapWidth, _mapHeight));
        _total        = _pending.Count;
        _generateMs   = _meshMs = _paintMs = 0;
        _nextStepTime = 0f;

        if (_autoFrameCamera) FrameCamera();
    }

    /// <summary>대기 중인 청크를 _chunksPerStep개 만든다. 일시정지 중에도 N키로 한 단계씩 진행할 수 있다.</summary>
    private void Step()
    {
        for (int i = 0; i < _chunksPerStep && _pending.Count > 0; i++)
        {
            var coord = _pending[0];
            _pending.RemoveAt(0);
            BuildChunk(coord);
        }
    }

    private void BuildChunk(Vector2Int coord)
    {
        _sw.Restart();
        ChunkData data = ChunkGenerator.Generate(coord);
        _generateMs += _sw.Elapsed.TotalMilliseconds;

        var chunk = CreateChunkObject(coord);
        // WorldStreamingManager.Load와 같은 순서: 위치 → 메시 → 활성화
        chunk.transform.position = ChunkUtils.ChunkToWorld(coord);
        _sw.Restart();
        chunk.BuildMesh(data);
        _meshMs += _sw.Elapsed.TotalMilliseconds;

        var tex = new Texture2D(ChunkUtils.CHUNK_SIZE, ChunkUtils.CHUNK_SIZE, TextureFormat.RGBA32, false)
        {
            name       = $"ChunkPreview_{coord}",
            filterMode = FilterMode.Point,       // 칸 경계가 또렷하게 보이도록
            wrapMode   = TextureWrapMode.Clamp,
        };
        Paint(tex, data);

        var renderer = chunk.GetComponent<MeshRenderer>();
        renderer.GetPropertyBlock(_mpb);
        _mpb.SetTexture(MainTexId, tex);
        _mpb.SetTexture(BaseMapId, tex);
        renderer.SetPropertyBlock(_mpb);

        var built = new Built { chunk = chunk, texture = tex, bornTime = Time.time };
        _built[coord] = built;

        if (_riseDuration > 0f)
        {
            chunk.transform.localScale = new Vector3(1f, 0.01f, 1f);
            _rising.Add(built);
        }
        chunk.gameObject.SetActive(true);
    }

    private ChunkInstance CreateChunkObject(Vector2Int coord)
    {
        ChunkInstance chunk;
        if (_chunkPrefab != null)
        {
            chunk = Instantiate(_chunkPrefab, transform);
        }
        else
        {
            // RequireComponent가 MeshFilter·MeshRenderer·MeshCollider를 먼저 붙인 뒤 ChunkInstance.Awake가 돈다
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            chunk = go.AddComponent<ChunkInstance>();
            var material = _material != null ? _material : _runtimeMaterial;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        chunk.gameObject.name = $"Chunk {coord}";
        chunk.gameObject.SetActive(false);
        return chunk;
    }

    private void AnimateRise()
    {
        for (int i = _rising.Count - 1; i >= 0; i--)
        {
            var b = _rising[i];
            float t = Mathf.Clamp01((Time.time - b.bornTime) / _riseDuration);
            b.chunk.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, Mathf.SmoothStep(0f, 1f, t)), 1f);
            if (t >= 1f) _rising.RemoveAt(i);
        }
    }

    private void ClearAll()
    {
        foreach (var b in _built.Values)
        {
            if (b.chunk != null)   Destroy(b.chunk.gameObject);   // 메시는 ChunkInstance.OnDestroy가 지운다
            if (b.texture != null) Destroy(b.texture);
        }
        _built.Clear();
        _rising.Clear();
        _pending.Clear();
    }

    // ── 색칠 ─────────────────────────────────────────────────

    private void RepaintAll()
    {
        _paintedMode = _viewMode;
        _paintMs     = 0;
        foreach (var b in _built.Values)
            if (b.chunk != null && b.chunk.Data != null) Paint(b.texture, b.chunk.Data);
    }

    /// <summary>
    /// 텍스처 픽셀 (x, z) = 정점 (x, z)에서 시작하는 칸. BuildMesh의 UV가 (x/size, z/size)라서 그대로 맞는다.
    /// </summary>
    private void Paint(Texture2D tex, ChunkData data)
    {
        _sw.Restart();
        int size = ChunkUtils.CHUNK_SIZE;
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
        {
            float h = data.GetHeight(x, z);
            _pixels[z * size + x] = _viewMode switch
            {
                ViewMode.Height => HeightColor(h),
                ViewMode.Ore    => OreCellColor(data.coord.x * size + x, data.coord.y * size + z, h),
                _               => new Color(0.75f, 0.75f, 0.75f),
            };
        }
        tex.SetPixels32(_pixels);
        tex.Apply(false);
        _paintMs += _sw.Elapsed.TotalMilliseconds;
    }

    private Color HeightColor(float h) => _heightGradient.Evaluate(Mathf.Clamp01(h / _maxHeight));

    /// <summary>가장 확률이 높은 광물의 색. 진할수록 확률이 높다. 광물이 없으면 높이색을 어둡게.</summary>
    private Color OreCellColor(int gx, int gz, float h)
    {
        var baseColor = Color.Lerp(HeightColor(h), Color.gray, 0.6f) * 0.55f;
        ChunkGenerator.GetOreDistribution(gx, gz, h, _oreBuffer);
        if (_oreBuffer.Count == 0) return baseColor;

        var best = _oreBuffer[0];
        for (int i = 1; i < _oreBuffer.Count; i++)
            if (_oreBuffer[i].probability > best.probability) best = _oreBuffer[i];

        return Color.Lerp(baseColor, GetOreColor(best.oreId), 0.35f + 0.65f * Mathf.Clamp01(best.probability));
    }

    private Color GetOreColor(string oreId)
    {
        if (_oreColorMap.TryGetValue(oreId, out var c)) return c;
        // 목록에 없는 광물(테이블에 새로 추가됨)도 보이도록 id에서 색을 정한다 — 실행마다 같은 색
        uint hash = 2166136261;
        foreach (char ch in oreId) hash = (hash ^ ch) * 16777619;
        c = Color.HSVToRGB((hash % 360) / 360f, 0.8f, 1f);
        _oreColorMap[oreId] = c;
        return c;
    }

    private void RebuildOreColorMap()
    {
        _oreColorMap.Clear();
        if (_oreColors == null) return;
        foreach (var o in _oreColors)
            if (o != null && !string.IsNullOrEmpty(o.oreId)) _oreColorMap[o.oreId] = o.color;
    }

    // ── 입력 · 카메라 ────────────────────────────────────────

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space)) Restart();
        if (Input.GetKeyDown(KeyCode.R))     { _seed = NewSeed(); Restart(); }
        if (Input.GetKeyDown(KeyCode.V))     _viewMode = (ViewMode)(((int)_viewMode + 1) % 3);
        if (Input.GetKeyDown(KeyCode.P))     _paused = !_paused;
        if (Input.GetKeyDown(KeyCode.N) && _paused) Step();
        if (Input.GetKeyDown(KeyCode.F))     while (_pending.Count > 0) Step();   // 남은 청크를 한 번에
        if (Input.GetKeyDown(KeyCode.G))     _showGui = !_showGui;
    }

    private static float NewSeed() => UnityEngine.Random.Range(0, 100000);

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        float extent = Mathf.Max(_mapWidth, _mapHeight) * ChunkUtils.CHUNK_SIZE;
        var   target = _spawn + Vector3.up * 5f;
        cam.transform.position = target + new Vector3(0f, extent * 0.55f, -extent * 0.7f);
        cam.transform.LookAt(target);
        if (cam.farClipPlane < extent * 3f) cam.farClipPlane = extent * 3f;
    }

    // ── 표시 ─────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_showGui) return;
        var white = Texture2D.whiteTexture;   // 내장 텍스처 — 파괴하지 않는다

        int built = _total - _pending.Count;
        GUILayout.BeginArea(new Rect(10, 10, 380, 330), GUI.skin.box);
        GUILayout.Label($"<b>지형 미리보기</b>   시드 {_seed}   맵 {_mapWidth}×{_mapHeight} 청크", Rich);
        GUILayout.Label($"진행 {built}/{_total} ({(_total == 0 ? 0 : 100f * built / _total):0}%)" +
                        (_paused ? "   ⏸ 일시정지" : _pending.Count == 0 ? "   ✔ 완료" : ""));

        var bar = GUILayoutUtility.GetRect(360, 8);
        GUI.color = new Color(1f, 1f, 1f, 0.2f); GUI.DrawTexture(bar, white);
        GUI.color = new Color(0.4f, 0.8f, 1f);   GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (_total == 0 ? 0 : (float)built / _total), bar.height), white);
        GUI.color = Color.white;

        if (built > 0)
            GUILayout.Label($"청크당 평균  높이 {_generateMs / built:0.00}ms · 메시 {_meshMs / built:0.00}ms · 색칠 {_paintMs / Mathf.Max(1, _built.Count):0.00}ms");
        GUILayout.Label($"표시: {ModeName(_viewMode)}");

        if (_viewMode == ViewMode.Ore)
            foreach (var ore in _oreColors)
            {
                GUI.color = ore.color;
                GUILayout.Label($"■ {ore.oreId}");
            }
        GUI.color = Color.white;

        GUILayout.Label("Space 다시 생성 · R 새 시드 · V 표시 전환 · P 일시정지 · N 한 단계 · F 전부 · G 숨김\n" +
                        "카메라: 우클릭 회전 · WASD 이동 · Q/E 아래/위 · Shift 빠르게 · 휠 앞뒤");
        GUILayout.EndArea();
    }

    private static GUIStyle s_rich;
    private static GUIStyle Rich => s_rich ??= new GUIStyle(GUI.skin.label) { richText = true };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_rich = null;   // 이전 플레이의 GUI.skin을 붙잡지 않게

    private static string ModeName(ViewMode m) => m switch
    {
        ViewMode.Height => "높이 (낮음 → 높음)",
        ViewMode.Ore    => "광맥 (칸마다 확률이 가장 높은 광물, 진할수록 확률 높음)",
        _               => "단색 (조명만)",
    };

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 맵 경계
        float w = _mapWidth * ChunkUtils.CHUNK_SIZE, h = _mapHeight * ChunkUtils.CHUNK_SIZE;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(new Vector3(w / 2f, 0f, h / 2f), new Vector3(w, 0f, h));

        // 평지 존(높이 0)과 전이 구간
        DrawCircle(_spawn, ChunkGenerator.FLAT_RADIUS,       Color.green);
        DrawCircle(_spawn, ChunkGenerator.TRANSITION_RADIUS, Color.yellow);

        // 다음에 만들 청크
        if (_pending.Count > 0)
        {
            var next = ChunkUtils.ChunkToWorld(_pending[0]);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(next + new Vector3(ChunkUtils.CHUNK_SIZE / 2f, 0f, ChunkUtils.CHUNK_SIZE / 2f),
                                new Vector3(ChunkUtils.CHUNK_SIZE, 1f, ChunkUtils.CHUNK_SIZE));
        }
    }

    private static void DrawCircle(Vector3 center, float radius, Color color)
    {
        const int segments = 48;
        Gizmos.color = color;
        var prev = center + new Vector3(radius, 0.2f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a    = i * Mathf.PI * 2f / segments;
            var   next = center + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    // ── 기본값 ───────────────────────────────────────────────

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.80f, 0.60f), 0.00f), // 평지(모래)
                new GradientColorKey(new Color(0.35f, 0.65f, 0.30f), 0.20f), // 풀
                new GradientColorKey(new Color(0.20f, 0.45f, 0.20f), 0.45f), // 숲
                new GradientColorKey(new Color(0.50f, 0.40f, 0.30f), 0.70f), // 흙
                new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0.85f), // 바위
                new GradientColorKey(Color.white,                    1.00f), // 꼭대기
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    /// <summary>HardcodedOreTable의 id와 맞춘다. 없는 id는 GetOreColor가 자동으로 색을 정한다.</summary>
    private static List<OreColor> DefaultOreColors() => new()
    {
        new OreColor { oreId = "iron_ore",   color = new Color(0.80f, 0.35f, 0.25f) },
        new OreColor { oreId = "copper_ore", color = new Color(1.00f, 0.60f, 0.15f) },
        new OreColor { oreId = "coal",       color = new Color(0.10f, 0.10f, 0.10f) },
        new OreColor { oreId = "quartz",     color = new Color(0.95f, 0.75f, 0.95f) },
        new OreColor { oreId = "uranium",    color = new Color(0.30f, 1.00f, 0.20f) },
    };
}
