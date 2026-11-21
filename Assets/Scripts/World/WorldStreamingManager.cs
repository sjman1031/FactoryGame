using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 위치 기준 청크 로드/언로드 판정
/// </summary>
public class WorldStreamingManager : SingletonManager<WorldStreamingManager>
{
    [Header("스트리밍 설정")]
    [SerializeField] private int _viewDistance = 3; // 청크 로드 반경 (청크 단위)

    [Header("의존 컴포넌트")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private BuildingCategory _category;
    [SerializeField] private ChunkInstance    _chunkPrefab; // 지형 메시 오브젝트 프리팹

    private readonly Dictionary<Vector2Int, ChunkInstance> _loaded = new();
    private readonly Queue<ChunkInstance>                  _pool   = new();

    // 재사용 가능한 캐시 HashSet
    private readonly HashSet<Vector2Int> _loadedKeyCache = new();
    private readonly HashSet<Vector2Int> _neededCache    = new();

    // 순회중 _loaded 수정 방지를 위해 Unload 대상을 별도 리스트에 수집 후 순회
    private readonly List<Vector2Int> _toUnload          = new();

    // LoadAsync가 await Task.Yield() 사이에 같은 좌표로 중복 진입하는 것을 방지
    // _loaded에 등록되기 전 구간을 커버함
    private readonly HashSet<Vector2> _loading           = new();

    private Vector2Int _lastPlayerChunk = new(int.MaxValue, int.MaxValue);

    public bool IsChunkLoaded(Vector2Int coord) => _loaded.ContainsKey(coord);

    // 매 프레임
    private void Update()
    {
        if (_player == null) return;

        var current = ChunkUtils.WorldToChunk(_player.transform.position);
        if (current == _lastPlayerChunk) return;
        
        _lastPlayerChunk = current;
        UpdateChunk(current);
    }

    // 청크 로드/언로드 판정
    private void UpdateChunk(Vector2Int center)
    {
        ChunkUtils.GetCoordsInRange(center, _viewDistance, _neededCache);

        _loadedKeyCache.Clear();
        foreach (var key in _loaded.Keys) _loadedKeyCache.Add(key);

        foreach (var coord in _neededCache)
            if (!_loadedKeyCache.Contains(coord) && !_loading.Contains(coord))
                _ = LoadChunkAsync(coord);
    }

    // 비동기 로드
    private async Task LoadChunkAsync(Vector2Int coord)
    {
        _loading.Add(coord);

        try
        {
            ChunkData data = SaveSystem.Instance.LoadChunk(coord);
            if (data == null) { Debug.LogWarning($"[Streaming] {coord} 없음"); return; }

            ChunkInstance chunk = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_chunkPrefab);
            chunk.transform.position = ChunkUtils.ChunkToWorld(coord);
            chunk.gameObject.SetActive(true);

            await Task.Yield();
            chunk.BuildMesh(data);

            //BuildingRegistry.RestoreChunk(coord, _category,
            //    SaveSystem.Instance.LoadBuildingsForChunk(coord),
            //    SaveSystem.Instance.LoadNodesForChunk(coord));

            _loaded[coord] = chunk;

        }
        finally
        {
            _loading.Remove(coord);
        }
    }

    // 언로드
    private void UnloadChunk(Vector2Int coord)
    {
        if (!_loaded.TryGetValue(coord, out var chunk)) return;

        // 건물 직렬화 + 파일 저장
        BuildingRegistry.SerializeChunk(coord);

        // 오브젝트 파괴 대신 풀 반환
        chunk.gameObject.SetActive(false);
        _pool.Enqueue(chunk);

        _loaded.Remove(coord);
    }

    protected override void OnDestroy()
    { 
        SaveSystem.Instance?.Close();
        base.OnDestroy();
    }
}