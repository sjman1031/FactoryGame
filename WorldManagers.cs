using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// ── WorldPreGenerator ─────────────────────────────────────────
/// <summary>새 게임 시작 시 전체 맵을 생성해서 SaveSystem으로 저장.</summary>
public class WorldPreGenerator : MonoBehaviour
{
    [SerializeField] private int _mapWidth       = 16;
    [SerializeField] private int _mapHeight      = 16;
    [SerializeField] private int _chunksPerFrame = 4;

    public float  Progress    { get; private set; }
    public bool   IsCompleted { get; private set; }
    public string StatusText  { get; private set; }
    public event Action OnCompleted;

    public async Task GenerateAll(int slot, float seed)
    {
        ChunkGenerator.SetSeed(seed);

        int  total  = _mapWidth * _mapHeight;
        int  done   = 0;
        var  coords = BuildCoordList(_mapWidth, _mapHeight);
        var  nodes  = new List<NodeEntry>();

        StatusText = "지형 생성 중...";

        // [최적화] 이전에는 모든 청크를 메모리에 올린 뒤 한 번에 저장했으나
        // 맵이 커지면 메모리 부족 문제가 생길 수 있음.
        // 청크를 생성하는 즉시 world.bin에 스트리밍 방식으로 저장하도록 변경.
        SaveSystem.BeginWorldWrite(slot, total);

        foreach (var coord in coords)
        {
            ChunkData data = ChunkGenerator.Generate(coord);

            foreach (var n in data.nodes)
            {
                n.chunkCoordX = coord.x; n.chunkCoordZ = coord.y;
                nodes.Add(n);
            }

            // 생성 즉시 파일에 기록 — 메모리에 누적하지 않음
            SaveSystem.WriteChunk(coord, data);

            done++;
            Progress   = (float)done / total;
            StatusText = $"지형 생성 중... ({done}/{total})";
            if (done % _chunksPerFrame == 0) await Task.Yield();
        }

        var center      = new Vector2Int(_mapWidth / 2, _mapHeight / 2);
        var playerStart = ChunkUtils.ChunkToWorld(center);

        StatusText = "파일 저장 중...";
        SaveSystem.FinalizeWorldWrite(slot, nodes, playerStart);

        StatusText = "완료!"; Progress = 1f; IsCompleted = true;
        OnCompleted?.Invoke();
    }

    private List<Vector2Int> BuildCoordList(int w, int h)
    {
        var center = new Vector2Int(w / 2, h / 2);
        var list   = new List<Vector2Int>(w * h);
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
            list.Add(new Vector2Int(x, z));
        list.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
        return list;
    }
}

// ── WorldStreamingManager ─────────────────────────────────────
/// <summary>플레이어 위치 기준 청크 로드/언로드.</summary>
public class WorldStreamingManager : SingletonManager<WorldStreamingManager>
{
    [SerializeField] private int              _viewDistance = 3;
    [SerializeField] private PlayerController _player;
    [SerializeField] private BuildingCatalog  _catalog;
    [SerializeField] private ChunkInstance    _chunkPrefab;

    private readonly Dictionary<Vector2Int, ChunkInstance> _loaded     = new();
    private readonly Queue<ChunkInstance>                  _pool       = new();

    // [최적화] 이전에는 UpdateChunks() 매 호출마다 new HashSet<>(_loaded.Keys)로
    // 새 HashSet을 생성했음 — 매 청크 이동마다 GC 압박 발생.
    // 재사용 가능한 캐시 HashSet으로 교체.
    private readonly HashSet<Vector2Int> _loadedKeyCache = new();
    private readonly HashSet<Vector2Int> _neededCache    = new();
    // Unload 대상을 별도 리스트에 수집 후 순회 — 순회 중 _loaded 수정 방지
    private readonly List<Vector2Int>    _toUnload       = new();

    private Vector2Int _lastChunk = new(int.MaxValue, int.MaxValue);

    private void Update()
    {
        if (_player == null) return;
        var current = ChunkUtils.WorldToChunk(_player.transform.position);
        if (current == _lastChunk) return;
        _lastChunk = current;
        UpdateChunks(current);
    }

    private void UpdateChunks(Vector2Int center)
    {
        // 두 HashSet 모두 재사용
        ChunkUtils.GetCoordsInRange(center, _viewDistance, _neededCache);

        _loadedKeyCache.Clear();
        foreach (var key in _loaded.Keys) _loadedKeyCache.Add(key);

        foreach (var coord in _neededCache)
            if (!_loadedKeyCache.Contains(coord)) _ = LoadAsync(coord);

        // Unload 대상 수집 후 별도 순회 — _loaded 수정과 분리
        _toUnload.Clear();
        foreach (var coord in _loadedKeyCache)
            if (!_neededCache.Contains(coord)) _toUnload.Add(coord);

        foreach (var coord in _toUnload) Unload(coord);
    }

    private async Task LoadAsync(Vector2Int coord)
    {
        ChunkData data = SaveSystem.Instance.LoadChunk(coord);
        if (data == null) { Debug.LogWarning($"[Streaming] {coord} 없음"); return; }

        ChunkInstance chunk = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_chunkPrefab);
        chunk.transform.position = ChunkUtils.ChunkToWorld(coord);
        chunk.gameObject.SetActive(true);

        await Task.Yield();
        chunk.BuildMesh(data);

        BuildingRegistry.RestoreChunk(coord, _catalog,
            SaveSystem.Instance.LoadBuildingsForChunk(coord),
            SaveSystem.Instance.LoadNodesForChunk(coord));

        _loaded[coord] = chunk;
    }

    private void Unload(Vector2Int coord)
    {
        if (!_loaded.TryGetValue(coord, out var chunk)) return;
        BuildingRegistry.SerializeChunk(coord);
        chunk.gameObject.SetActive(false);
        _pool.Enqueue(chunk);
        _loaded.Remove(coord);
    }

    private void OnDestroy() => SaveSystem.Instance?.Close();
}
