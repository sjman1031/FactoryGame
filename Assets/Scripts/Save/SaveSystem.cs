using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class InventoryEntry
{
    public string itemId;
    public int amount;
}

[Serializable]
public class PlayerLiveData
{
    public float px, py, pz;   // position
    public float rotY;
    public List<InventoryEntry> inventory = new();
}

// LiveData - live.json에 저장되는 가변 데이터 구조
[Serializable]
public class LiveData
{
    public PlayerLiveData player = new();
    public List<BuildingSaveData> buildings = new();
}

// SaveSystem - world.bin (Binary) + live.json (JSON)
// world.bin -> 지형 + 노드 (새 게임에 1회만 생성, 이후 읽기 전용)
// live.json -> 플레이어 + 건물 (플레이 중 수시 갱신)
// 저장 시 live.json만 통째로 덮어씀 -> world.bin은 최초 1회 생성 이후 갱신 X
public class SaveSystem
{
    // Monobehaviour가 필요없는 클래스이기 때문에 SingletonManager를 상속하지 않음
    // 상속시키면 씬에 오브젝트가 생기고 DontDestoryOnLoad 처리도 해야함
    public static SaveSystem Instance { get; private set; }

    // 경로
    private static string SlotDir(int slot)
        => Path.Combine(Application.persistentDataPath, "saves", $"slot_{slot}");

    private static string WorldBinPath(int slot)
        => Path.Combine(SlotDir(slot), "world.bin");

    private static string LiveJsonPath(int slot)
        => Path.Combine(SlotDir(slot), "live.json");

    public static bool Exists(int slot) => File.Exists(WorldBinPath(slot));

    // 런타임 캐시
    private int _slot;
    private Dictionary<Vector2Int, (long offset, int lenght)> _chunkIndex;
    private List<NodeEntry> _allNodes;
    private BinaryReader _reader;

    // 초기화
    public static void Initialize(int slot)
    {
        Instance?._reader?.Close();
        Instance = null;

        var sys = new SaveSystem { _slot = slot };
        try
        {
            sys.OpenWorldForRead();
            Instance = sys;
        }
        catch (Exception e)
        {
            // OpenWorldForRead 실패 시 Instance를 null로 유지
            // WorldStreamingManager가 null 체크 후 경고 출력
            Debug.LogWarning($"[SaveSystem] 초기화 실패: {e.Message}");
        }
    }

    private void OpenWorldForRead()
    {
        try
        {
            var stream = new FileStream(WorldBinPath(_slot), FileMode.Open, FileAccess.Read);
            _reader = new BinaryReader(stream);

            int count = _reader.ReadInt32();
            _chunkIndex = new Dictionary<Vector2Int, (long offset, int lenght)>(count);

            for (int i = 0; i < count; i++)
            {
                int cx      = _reader.ReadInt32();
                int cz      = _reader.ReadInt32();
                long offset = _reader.ReadInt64();
                int lenght  = _reader.ReadInt32();
                _chunkIndex[new Vector2Int(cx, cz)] = (offset, lenght);
            }

            int nodeCount = _reader.ReadInt32();
            _allNodes = new List<NodeEntry>(nodeCount);

            for (int i = 0; i < nodeCount; i++)
            {
                _allNodes.Add(new NodeEntry
                {
                    localX = _reader.ReadInt32(),
                    localZ = _reader.ReadInt32(),
                    nodeDataid = _reader.ReadString(),
                    purity = (NodePurity)_reader.ReadInt32(),
                    chunkCoordX = _reader.ReadInt32(),
                    chunkCoordZ = _reader.ReadInt32(),
                });
            }
        }
        catch (Exception e)
        {
            _reader?.Close();
            _reader = null;
            Debug.LogError($"[SaveSystem] world.bin 읽기 실패 (slot_{_slot}): {e.Message}");
            throw; // Initialize 에서 이대로 넘어가면 정상 처리 된것으로 간주하고 넘어갈 수 있으므로, throw를 통해 Initialize의 catch 구문을 발생시킴
        }
    }

    // 청크 읽기 (Seek 으로 해당 청크만)
    public ChunkData LoadChunk(Vector2Int coord)
    {
        if (!_chunkIndex.TryGetValue(coord, out var entry))
        {
            Debug.LogWarning($"[SaveSystem] 청크 {coord} 인덱스 없음");
            return null;
        }

        try
        {
            _reader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);

            int size = ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE;
            float[] heights = new float[size];

            for (int i = 0; i < size; i++) heights[i] = _reader.ReadSingle();

            return new ChunkData(coord) { heights = heights };
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 청크 {coord} 읽기 실패: {e.Message}");
            return null;
        }
    }

    public List<BuildingSaveData> LoadBuildingsForChunk(Vector2Int coord)
    {
        LoadLive(); // 캐시/인덱스 보장
        return _buildingIndex.TryGetValue(coord, out var list)
            ? list
            : new List<BuildingSaveData>();
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

    // live.json 읽기 (캐시)
    // LoadBuildingsForChunk + LoadChunk가 같은 프레임에 함께 호출 되므로
    // live.json을 두 번 파싱하지 않도록 캐싱한다.
    private LiveData _liveCache;

    // 건물 목록을 청크 좌표로 인덱싱 - 청크 로드마다 전체 순회를 방지
    private Dictionary<Vector2Int, List<BuildingSaveData>> _buildingIndex;

    private LiveData LoadLive()
    {
        if (_liveCache != null) return _liveCache;

        string path = LiveJsonPath(_slot);
        if (!File.Exists(path)) return _liveCache = new LiveData();

        try
        {
            _liveCache = JsonUtility.FromJson<LiveData>(File.ReadAllText(path)) ?? new LiveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] live.json 파싱 실패: {e.Message}. 빈 데이터로 대체");
            _liveCache = new LiveData();
        }

        BuildBuildingIndex(_liveCache);
        return _liveCache;  
    }

    private void BuildBuildingIndex(LiveData live)
    {
        _buildingIndex = new Dictionary<Vector2Int, List<BuildingSaveData>>();

        foreach (var b in live.buildings)
        {
            var coord = ChunkUtils.WorldToChunk(b.position);
            if (!_buildingIndex.TryGetValue(coord, out var list))
                _buildingIndex[coord] = list = new List<BuildingSaveData>();
            list.Add(b);
        }
    }

    /// <summary> 저장 후 캐시 무효화 - 다음 로드 시 파일에서 재 읽기. </summary>
    private void InvaildLiveCache()
    {
        _liveCache     = null;
        _buildingIndex = null;
    }

    // live.json 저장 (world.bin은 건들지 않음)
    /// <summary>
    /// 플레이 중 저장 시 호출
    /// live.json만 통째로 덮어씀 - world.bin은 변경 없음
    /// </summary>
    public void Save(List<BuildingSaveData> buildings, PlayerController player)
    {
        var live = new LiveData
        {
            buildings = buildings,
            player    = new PlayerLiveData
            {
                px   = player.transform.position.x,
                py   = player.transform.position.y,
                pz   = player.transform.position.z,
                rotY = player.transform.rotation.y,
            }
        };

        foreach (var kv in player.PlayerInventory.GetAll())
            live.player.inventory.Add(new InventoryEntry
            { itemId = kv.Key.itemId, amount = kv.Value });

        // 임시 파일에 먼저 쓴 뒤 교체 - 쓰기 중 실패해도 기존 파일 보존
        string finalPath = LiveJsonPath(_slot);
        string tempPath  = finalPath + ".tmp"; 

        try
        {
            File.WriteAllText(tempPath, JsonUtility.ToJson(live, prettyPrint: false));
            // 임시 파일 쓰기 성공 후 원본 교체
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            InvaildLiveCache();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] live.json 저장 실패: {e.Message}");
            // 임시 파일 정리
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } 
                catch { }
            }
        }
    }

    public void Close() => _reader?.Close();

    // WorldWriter - world.bin 스트리밍 쓰기 전담 내부 클래스
    // static 필드 대신 인스턴스로 관리해 상태 오염 방지
    // BeginWorldWrite -> WriteChunk(xN) -> FinalizeWorldWrite 순서로 호출
    private class WorldWriter : IDisposable
    {
        private readonly BinaryWriter                                    _w;
        private readonly long                                            _indexTableStart;
        private readonly List<(int cx, int cz, long offset, int lenght)> _index;

        public WorldWriter(string path, int totalChunks)
        {
            try
            {
                var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                _w = new BinaryWriter(stream);
                _index = new List<(int cx, int cz, long offset, int lenght)>(totalChunks);
            }
            catch (Exception e)
            {
                Dispose();
                throw new IOException($"[SaveSystem] world.bin 생성 실패: {e.Message}", e);
            }
        }

        public bool WriteChunk(Vector2Int coord, ChunkData data)
        {
            try
            {
                long offset = _w.BaseStream.Position;
                foreach (float h in data.heights) _w.Write(h);
                _index.Add((coord.x, coord.y, offset, (int)(_w.BaseStream.Position - offset)));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 청크 {coord} 쓰기 실패: {e.Message}");
                return false;
            }
        }
        
        public bool Finalize(List<NodeEntry> nodes, Vector3 playerStart, string liveJsonPath)
        {
            try
            {
                long endPos = _w.BaseStream.Position;

                // WriteChunk에서 실패하는 경우가 생길경우 기록되지 않은 파일의 슬롯의 offset이 0이 됨
                // 그래서 Seek(0)으로 이동하면 파일의 맨 앞인 헤더의 위치를 청크데이터로 오독할 가능성이 있음
                // Seek(0) -> Write(_index.Count)를 먼저 실행해 헤더를 실제로 쓴 개수로 바꿔놓아 실제 데이터가 있는 수 만큼 루프를 돌고
                // 예약만 해둔 나머지 슬롯에 접근을 막음
                _w.BaseStream.Seek(0, SeekOrigin.Begin);
                _w.Write(_index.Count);

                _w.BaseStream.Seek(_indexTableStart, SeekOrigin.Begin);
                foreach (var (cx, cz, off, len) in _index)
                {
                    _w.Write(cx);
                    _w.Write(cz);
                    _w.Write(off);
                    _w.Write(len);
                }

                _w.BaseStream.Seek(endPos, SeekOrigin.Begin);
                _w.Write(nodes.Count);
                foreach (var n in nodes)
                {
                    _w.Write(n.localX);
                    _w.Write(n.localZ);
                    _w.Write(n.nodeDataid ?? "");
                    _w.Write((int)n.purity);
                    _w.Write(n.chunkCoordX);
                    _w.Write(n.chunkCoordZ);
                }

                var live = new LiveData
                {
                    player = new PlayerLiveData
                    {
                        px = playerStart.x,
                        py = playerStart.y,
                        pz = playerStart.z
                    }
                };

                File.WriteAllText(liveJsonPath, JsonUtility.ToJson(live, prettyPrint: false));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] world.bin Finalize 실패: {e.Message}");
                return false;
            }
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
    private static int         _currentWriterSlot;

    public static void BeginWorldWrite(int slot, int totalChunks)
    {
        _currentWriter?.Dispose();
        Directory.CreateDirectory(SlotDir(slot));
        _currentWriter = new WorldWriter(WorldBinPath(slot), totalChunks);
        _currentWriterSlot = slot;
    }

    public static void WriteChunk(Vector2Int coord, ChunkData data)
        => _currentWriter?.WriteChunk(coord, data);

    public static void FinalizeWorldWrite(int slot, List<NodeEntry> nodes, Vector3 playerStart)
    {
        if (_currentWriter == null) return;
        _currentWriter.Finalize(nodes, playerStart, LiveJsonPath(slot));
        _currentWriter.Dispose();
        _currentWriter = null;
    }
} 