using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 위치 기준 청크(지형) 로드/언로드.
/// 건물은 내리지 않는다 — 청크가 내려가면 그 위 건물의 렌더러만 끈다.
///
/// 로드는 프레임 예산 큐로 처리한다: 필요한 청크를 가까운 순서로 줄 세우고 프레임마다 몇 개씩만 만든다.
/// (이전에는 async + Task.Yield로 시야 안 청크 전부를 한 프레임에 메시까지 구워 스파이크가 났고,
///  예외는 버려진 Task 안에 묻혔고, 재사용 청크가 이전 메시로 한 프레임 보였음)
/// </summary>
public class WorldStreamingManager : SingletonManager<WorldStreamingManager>
{
    [SerializeField] private int              _viewDistance   = 3;
    [Tooltip("프레임마다 만들 최대 청크 수. 메시·콜라이더 굽기가 무거우므로 작게 둔다.")]
    [SerializeField] private int              _chunksPerFrame = 2;
    [SerializeField] private PlayerController _player;
    [SerializeField] private ChunkInstance    _chunkPrefab;

    private readonly Dictionary<Vector2Int, ChunkInstance> _loaded = new();
    private readonly Queue<ChunkInstance>                  _pool   = new();

    // 재사용 버퍼 — 청크 경계를 넘을 때마다 할당하지 않는다
    private readonly HashSet<Vector2Int> _neededCache = new();
    private readonly List<Vector2Int>    _toUnload    = new();
    // 아직 만들지 않은 청크. 가까운 것이 앞에 오도록 정렬해 둔다.
    private readonly List<Vector2Int>    _pending     = new();

    private Vector2Int _lastChunk = new(int.MaxValue, int.MaxValue);

    public bool IsChunkLoaded(Vector2Int coord) => _loaded.ContainsKey(coord);

    private void Update()
    {
        // 로딩 씬을 거치지 않고 게임 씬을 바로 실행하면 SaveSystem이 없다 — 매 청크마다 NRE 대신 대기
        if (_player == null || SaveSystem.Instance == null) return;

        var current = ChunkUtils.WorldToChunk(_player.transform.position);
        if (current != _lastChunk)
        {
            _lastChunk = current;
            UpdateChunks(current);
        }
        ProcessPending();
    }

    private void UpdateChunks(Vector2Int center)
    {
        var save = SaveSystem.Instance;
        ChunkUtils.GetCoordsInRange(center, _viewDistance, _neededCache);
        // 맵 밖 좌표는 처음부터 빼 둔다 — 경계를 넘을 때마다 없는 청크를 읽으려다 경고가 쌓이지 않게
        _neededCache.RemoveWhere(c => !save.HasChunk(c));

        _pending.Clear();
        foreach (var coord in _neededCache)
            if (!_loaded.ContainsKey(coord)) _pending.Add(coord);
        _pending.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));

        // Unload 대상 수집 후 별도 순회 — 순회 중 _loaded 수정 방지
        _toUnload.Clear();
        foreach (var coord in _loaded.Keys)
            if (!_neededCache.Contains(coord)) _toUnload.Add(coord);
        foreach (var coord in _toUnload) Unload(coord);
    }

    private void ProcessPending()
    {
        int built = 0;
        while (built < _chunksPerFrame && _pending.Count > 0)
        {
            var coord = _pending[0];
            _pending.RemoveAt(0);   // 최대 49개라 앞에서 빼도 비용이 작다

            // 큐에 넣은 뒤 플레이어가 움직였다면 이미 필요 없는 좌표일 수 있다
            if (!_neededCache.Contains(coord) || _loaded.ContainsKey(coord)) continue;

            Load(coord);
            built++;
        }
    }

    private void Load(Vector2Int coord)
    {
        ChunkData data = SaveSystem.Instance.LoadChunk(coord);
        if (data == null) { Debug.LogWarning($"[WorldStreamingManager] {coord} 없음"); return; }

        ChunkInstance chunk = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_chunkPrefab);
        // 순서: 위치 → 메시 → 활성화. 재사용 청크가 이전 좌표의 메시·콜라이더로 보이는 프레임이 없게 한다.
        chunk.transform.position = ChunkUtils.ChunkToWorld(coord);
        chunk.BuildMesh(data);
        chunk.gameObject.SetActive(true);

        _loaded[coord] = chunk;
        BuildingRegistry.SetChunkVisible(coord, true);
    }

    private void Unload(Vector2Int coord)
    {
        if (!_loaded.TryGetValue(coord, out var chunk)) return;
        BuildingRegistry.SetChunkVisible(coord, false);   // 건물은 계속 틱 — 지형 없는 곳에 떠 보이지 않게만
        chunk.gameObject.SetActive(false);
        _pool.Enqueue(chunk);
        _loaded.Remove(coord);
    }

    protected override void OnDestroy()
    {
        // 중복으로 생겼다가 파괴되는 사본이 공유 중인 SaveSystem 리더를 닫지 않도록 원본일 때만 닫는다
        if (Instance == this) SaveSystem.Instance?.Close();
        base.OnDestroy();
    }
}
