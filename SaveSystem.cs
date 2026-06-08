using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ══════════════════════════════════════════════════════════════
// LiveData — live.json에 저장되는 가변 데이터 구조
// ══════════════════════════════════════════════════════════════
[System.Serializable]
public class LiveData
{
    public PlayerLiveData         player    = new();
    public List<BuildingSaveData> buildings = new();
}

[System.Serializable]
public class PlayerLiveData
{
    public float px, py, pz;   // position
    public float rotY;
    public List<InventoryEntry> inventory = new();
}

[System.Serializable]
public class InventoryEntry
{
    public string itemId;
    public int    amount;
}

// ══════════════════════════════════════════════════════════════
// SaveSystem — world.bin (Binary) + live.json (JSON)
//
// world.bin: 지형 + 노드 (새 게임 1회 생성, 이후 읽기 전용)
// live.json: 플레이어 + 건물 (플레이 중 수시 갱신)
//
// 저장 시 live.json만 통째로 덮어씀 → world.bin 건드리지 않음
// ══════════════════════════════════════════════════════════════
public class SaveSystem
{
    public static SaveSystem Instance { get; private set; }

    // ── 경로 ──────────────────────────────────────────────────
    private static string SlotDir(int slot)
        => Path.Combine(Application.persistentDataPath, "saves", $"slot_{slot}");

    private static string WorldBinPath(int slot)
        => Path.Combine(SlotDir(slot), "world.bin");

    private static string LiveJsonPath(int slot)
        => Path.Combine(SlotDir(slot), "live.json");

    public static bool Exists(int slot) => File.Exists(WorldBinPath(slot));

    // ── 런타임 캐시 ───────────────────────────────────────────
    private int                                               _slot;
    private Dictionary<Vector2Int, (long offset, int length)> _chunkIndex;
    private List<NodeEntry>                                   _allNodes;
    private BinaryReader                                      _reader;

    // ── 초기화 ────────────────────────────────────────────────
    public static void Initialize(int slot)
    {
        Instance?._reader?.Close();
        var sys = new SaveSystem { _slot = slot };
        sys.OpenWorldForRead();
        Instance = sys;
    }

    private void OpenWorldForRead()
    {
        var stream  = new FileStream(WorldBinPath(_slot), FileMode.Open, FileAccess.Read);
        _reader     = new BinaryReader(stream);

        // 헤더: 청크 수
        int count   = _reader.ReadInt32();
        _chunkIndex = new Dictionary<Vector2Int, (long, int)>(count);

        // 인덱스 테이블 읽기
        for (int i = 0; i < count; i++)
        {
            int  cx     = _reader.ReadInt32();
            int  cz     = _reader.ReadInt32();
            long offset = _reader.ReadInt64();
            int  length = _reader.ReadInt32();
            _chunkIndex[new Vector2Int(cx, cz)] = (offset, length);
        }

        // 노드 섹션 읽기
        int nodeCount = _reader.ReadInt32();
        _allNodes     = new List<NodeEntry>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            _allNodes.Add(new NodeEntry
            {
                localX      = _reader.ReadInt32(),
                localZ      = _reader.ReadInt32(),
                nodeDataId  = _reader.ReadString(),
                purity      = (NodePurity)_reader.ReadInt32(),
                chunkCoordX = _reader.ReadInt32(),
                chunkCoordZ = _reader.ReadInt32(),
            });
        }
    }

    // ── 청크 읽기 (Seek로 해당 청크만) ────────────────────────
    public ChunkData LoadChunk(Vector2Int coord)
    {
        if (!_chunkIndex.TryGetValue(coord, out var entry))
        {
            Debug.LogWarning($"[SaveSystem] 청크 {coord} 인덱스 없음");
            return null;
        }

        _reader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);
        int     size    = ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE;
        float[] heights = new float[size];
        for (int i = 0; i < size; i++) heights[i] = _reader.ReadSingle();

        return new ChunkData(coord) { heights = heights };
    }

    public List<BuildingSaveData> LoadBuildingsForChunk(Vector2Int coord)
    {
        // live.json에서 해당 청크 건물만 필터링
        var live   = LoadLive();
        var result = new List<BuildingSaveData>();
        foreach (var b in live.buildings)
            if (ChunkUtils.WorldToChunk(b.position) == coord) result.Add(b);
        return result;
    }

    public List<NodeEntry> LoadNodesForChunk(Vector2Int coord)
    {
        var result = new List<NodeEntry>();
        foreach (var n in _allNodes)
            if (n.chunkCoordX == coord.x && n.chunkCoordZ == coord.y) result.Add(n);
        return result;
    }

    public PlayerLiveData LoadPlayer()
        => LoadLive().player;

    // ── live.json 읽기 (캐시) ─────────────────────────────────
    // LoadBuildingsForChunk + LoadNodesForChunk가 같은 프레임에 함께 호출되므로
    // live.json을 두 번 파싱하지 않도록 캐싱한다.
    private LiveData _liveCache;

    private LiveData LoadLive()
    {
        if (_liveCache != null) return _liveCache;
        string path = LiveJsonPath(_slot);
        if (!File.Exists(path)) return _liveCache = new LiveData();
        _liveCache = JsonUtility.FromJson<LiveData>(File.ReadAllText(path)) ?? new LiveData();
        return _liveCache;
    }

    /// <summary>저장 후 캐시 무효화 — 다음 로드 시 파일에서 재읽기.</summary>
    private void InvalidateLiveCache() => _liveCache = null;

    // ── live.json 저장 (world.bin 건드리지 않음) ───────────────
    /// <summary>
    /// 플레이 중 저장 시 호출.
    /// live.json만 통째로 덮어씀 — world.bin은 변경 없음.
    /// live.json이 수 KB 수준이라 통째로 씌워도 IO 부담 없음.
    /// </summary>
    public void Save(List<BuildingSaveData> buildings, PlayerController player)
    {
        var live = new LiveData
        {
            buildings = buildings,
            player    = new PlayerLiveData
            {
                px    = player.transform.position.x,
                py    = player.transform.position.y,
                pz    = player.transform.position.z,
                rotY  = player.transform.eulerAngles.y,
            }
        };

        // 인벤토리 직렬화
        foreach (var kv in player.PlayerInventory.GetAll())
            live.player.inventory.Add(new InventoryEntry
                { itemId = kv.Key.itemId, amount = kv.Value });

        File.WriteAllText(LiveJsonPath(_slot),
                          JsonUtility.ToJson(live, prettyPrint: false));
        InvalidateLiveCache();
    }

    public void Close() => _reader?.Close();

    // ══════════════════════════════════════════════════════════
    // WorldWriter — world.bin 스트리밍 쓰기 전담 내부 클래스
    // static 필드 대신 인스턴스로 관리해 상태 오염 방지.
    // BeginWorldWrite → WriteChunk(×N) → FinalizeWorldWrite 순서로 호출.
    // ══════════════════════════════════════════════════════════
    private class WorldWriter : IDisposable
    {
        private readonly BinaryWriter                                       _w;
        private readonly long                                               _indexTableStart;
        private readonly List<(int cx, int cz, long offset, int length)>   _index;

        public WorldWriter(string path, int totalChunks)
        {
            var stream       = new FileStream(path, FileMode.Create, FileAccess.Write);
            _w               = new BinaryWriter(stream);
            _index           = new List<(int, int, long, int)>(totalChunks);

            _w.Write(totalChunks);

            _indexTableStart = _w.BaseStream.Position;
            for (int i = 0; i < totalChunks; i++)
            { _w.Write(0); _w.Write(0); _w.Write(0L); _w.Write(0); }
        }

        public void WriteChunk(Vector2Int coord, ChunkData data)
        {
            long offset = _w.BaseStream.Position;
            foreach (float h in data.heights) _w.Write(h);
            _index.Add((coord.x, coord.y, offset, (int)(_w.BaseStream.Position - offset)));
        }

        public void Finalize(List<NodeEntry> nodes, Vector3 playerStart, string liveJsonPath)
        {
            long endPos = _w.BaseStream.Position;
            _w.BaseStream.Seek(_indexTableStart, SeekOrigin.Begin);
            foreach (var (cx, cz, off, len) in _index)
            { _w.Write(cx); _w.Write(cz); _w.Write(off); _w.Write(len); }

            _w.BaseStream.Seek(endPos, SeekOrigin.Begin);
            _w.Write(nodes.Count);
            foreach (var n in nodes)
            { _w.Write(n.localX); _w.Write(n.localZ); _w.Write(n.nodeDataId ?? ""); _w.Write((int)n.purity); _w.Write(n.chunkCoordX); _w.Write(n.chunkCoordZ); }

            // Close는 Dispose()에서 처리 — 여기서 중복 호출 제거

            var live = new LiveData
            {
                player = new PlayerLiveData { px = playerStart.x, py = playerStart.y, pz = playerStart.z },
            };
            File.WriteAllText(liveJsonPath, JsonUtility.ToJson(live, prettyPrint: false));
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _w?.Close();
        }
    }

    private static WorldWriter _currentWriter;

    public static void BeginWorldWrite(int slot, int totalChunks)
    {
        _currentWriter?.Dispose();
        Directory.CreateDirectory(SlotDir(slot));
        _currentWriter = new WorldWriter(WorldBinPath(slot), totalChunks);
        _currentWriterSlot = slot;
    }
    private static int _currentWriterSlot;

    public static void WriteChunk(Vector2Int coord, ChunkData data)
        => _currentWriter?.WriteChunk(coord, data);

    public static void FinalizeWorldWrite(int slot, List<NodeEntry> nodes, Vector3 playerStart)
    {
        _currentWriter?.Finalize(nodes, playerStart, LiveJsonPath(slot));
        _currentWriter?.Dispose();
        _currentWriter = null;
    }
