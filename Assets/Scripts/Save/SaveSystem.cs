using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ══════════════════════════════════════════════════════════════
// SaveSystem — world.bin (Binary) + live.json (JSON)
//
// world.bin: 헤더(형식·시드) + 지형 (새 게임 1회 생성, 이후 읽기 전용)
// live.json: 플레이어 + 건물 (플레이 중 수시 갱신)
//
// 저장 시 live.json만 통째로 덮어씀 → world.bin 건드리지 않음
// 광맥은 저장하지 않는다 — 시드로 위치마다 다시 계산 (ChunkGenerator.GetOreDistribution)
//
// 실패 처리 원칙 — "기본값으로 대체"하지 않고 "알리고 보존"한다
//  1. 경계(IO)에서 실패하면 결과를 돌려준다 (Initialize → bool, LoadFailed 플래그)
//  2. 실패한 상태에서는 쓰지 않는다 (LoadFailed면 자동 저장 차단)
//  3. 모르는 데이터는 버리지 않는다 (손상 파일 백업, 못 찾은 id 보관)
// ══════════════════════════════════════════════════════════════
/// <summary>world.bin·live.json 읽기와 쓰기. MonoBehaviour가 아니며 SaveSystem.Instance로 접근한다.</summary>
public class SaveSystem
{
    public static SaveSystem Instance { get; private set; }

    // world.bin 헤더. 형식이 바뀌면 WORLD_VERSION을 올린다 — 이전 형식 파일은 열지 않고 오류로 알린다.
    private const int WORLD_MAGIC   = 0x31574746; // "FGW1"
    private const int WORLD_VERSION = 2;          // 2: 노드 목록 제거, 시드 추가

    /// <summary>이 월드를 만든 시드. 광맥 확률을 생성 때와 같게 다시 계산하는 데 쓴다.</summary>
    public float WorldSeed { get; private set; }

    /// <summary>
    /// live.json이 있었는데 읽지 못했다. 이 상태에서 저장하면 빈 데이터로 원본을 덮어쓰므로
    /// SaveController가 종료 시 자동 저장을 막는다. 손상 파일은 CorruptBackupPath로 복사돼 있다.
    /// </summary>
    public bool   LoadFailed        { get; private set; }
    public string CorruptBackupPath { get; private set; }

    /// <summary>Initialize가 실패한 이유. 로딩 UI가 보여 준다.</summary>
    public static string LastError { get; private set; }

    // ── 경로 ──────────────────────────────────────────────────
    private static string SlotDir(int slot)
        => Path.Combine(Application.persistentDataPath, "saves", $"slot_{slot}");

    private static string WorldBinPath(int slot)
        => Path.Combine(SlotDir(slot), "world.bin");

    private static string LiveJsonPath(int slot)
        => Path.Combine(SlotDir(slot), "live.json");

    // world.bin은 생성이 끝까지 성공해야 .tmp에서 이 이름으로 바뀐다 — 파일이 있으면 완성된 월드다
    public static bool Exists(int slot) => File.Exists(WorldBinPath(slot));

    // ── 런타임 캐시 ───────────────────────────────────────────
    private int                                               _slot;
    private Dictionary<Vector2Int, (long offset, int length)> _chunkIndex;
    private BinaryReader                                      _reader;
    private byte[]                                            _chunkBytes;   // LoadChunk 읽기 버퍼 재사용

    // ── 초기화 ────────────────────────────────────────────────
    /// <summary>슬롯의 world.bin을 연다. 실패하면 false를 돌려주고 이유를 LastError에 남긴다.</summary>
    public static bool Initialize(int slot)
    {
        Instance?.Close();
        Instance  = null;
        LastError = null;

        var sys = new SaveSystem { _slot = slot };
        try
        {
            sys.OpenWorldForRead();
            Instance = sys;
            // 게임 중 광맥 확률 조회가 생성 때와 같은 시드를 쓰도록
            ChunkGenerator.SetSeed(sys.WorldSeed);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[SaveSystem] 초기화 실패 (slot {slot}): {e.Message}");
            return false;
        }
    }

    private void OpenWorldForRead()
    {
        try
        {
            // FileShare.Read — 같은 슬롯을 다른 곳에서 읽는 것은 허용
            var stream  = new FileStream(WorldBinPath(_slot), FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader     = new BinaryReader(stream);

            int magic   = _reader.ReadInt32();
            int version = _reader.ReadInt32();
            if (magic != WORLD_MAGIC || version != WORLD_VERSION)
                throw new InvalidDataException(
                    "지원하지 않는 world.bin 형식입니다. 이전 버전에서 만든 세이브이므로 새 게임을 만들어 주세요.");
            WorldSeed = _reader.ReadSingle();

            int count   = _reader.ReadInt32();
            _chunkIndex = new Dictionary<Vector2Int, (long, int)>(count);

            for (int i = 0; i < count; i++)
            {
                int  cx     = _reader.ReadInt32();
                int  cz     = _reader.ReadInt32();
                long offset = _reader.ReadInt64();
                int  length = _reader.ReadInt32();
                // 길이 0 엔트리는 쓰다 만 인덱스 — 진짜 청크 엔트리를 덮어쓰지 않게 건너뛴다
                if (length <= 0) continue;
                _chunkIndex[new Vector2Int(cx, cz)] = (offset, length);
            }
            if (_chunkIndex.Count != count)
                throw new InvalidDataException($"world.bin 인덱스가 불완전합니다 ({_chunkIndex.Count}/{count}).");
        }
        catch
        {
            Close();
            throw; // 호출자(Initialize)가 Instance 세팅을 막도록 재throw
        }
    }

    // ── 청크 읽기 (Seek로 해당 청크만) ────────────────────────
    /// <summary>맵 안의 청크인지. 맵 밖 좌표는 경고 없이 건너뛰는 데 쓴다.</summary>
    public bool HasChunk(Vector2Int coord) => _chunkIndex != null && _chunkIndex.ContainsKey(coord);

    public ChunkData LoadChunk(Vector2Int coord)
    {
        if (_reader == null || !_chunkIndex.TryGetValue(coord, out var entry))
        {
            Debug.LogWarning($"[SaveSystem] 청크 {coord} 인덱스 없음");
            return null;
        }

        try
        {
            int size  = ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE;
            int bytes = size * sizeof(float);
            if (entry.length != bytes)
                throw new InvalidDataException($"청크 길이 {entry.length} ≠ {bytes}");

            _reader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);
            _chunkBytes ??= new byte[bytes];
            int read = 0;
            while (read < bytes)
            {
                int n = _reader.BaseStream.Read(_chunkBytes, read, bytes - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
            // ReadSingle 4096회 대신 한 번에 읽어 복사 — 디스크의 float 배열과 메모리 배치가 같다
            var heights = new float[size];
            Buffer.BlockCopy(_chunkBytes, 0, heights, 0, bytes);
            return new ChunkData(coord, heights);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 청크 {coord} 읽기 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 저장된 건물 전체. 건물은 청크와 함께 내리지 않고 항상 살아 있으므로
    /// 게임 시작 때 한 번에 복원한다 (BuildingRegistry.RestoreAll).
    /// </summary>
    public List<BuildingSaveData> LoadAllBuildings()
        => LoadLive().buildings;

    public PlayerLiveData LoadPlayer()
        => LoadLive().player;

    // ── live.json 읽기 (캐시) ─────────────────────────────────
    // LoadPlayer + LoadAllBuildings가 게임 시작 때 함께 호출되므로
    // live.json을 두 번 파싱하지 않도록 캐싱한다.
    private LiveData _liveCache;

    private LiveData LoadLive()
    {
        if (_liveCache != null) return _liveCache;

        string main = LiveJsonPath(_slot);
        bool anyFileFound = false;

        // live.json → (교체 도중 죽었을 때) .tmp → (직전 저장) .bak 순서로 찾는다
        foreach (string path in new[] { main, main + ".tmp", main + ".bak" })
        {
            if (!File.Exists(path)) continue;
            anyFileFound = true;

            if (TryParseLive(path, out var live))
            {
                if (path != main)
                    Debug.LogWarning($"[SaveSystem] live.json을 쓸 수 없어 {Path.GetFileName(path)}에서 복구했습니다.");
                return _liveCache = live;
            }

            // 읽지 못한 파일은 지우지 않고 백업해 둔다 — 손으로 고칠 수 있게
            if (path == main) BackupCorrupt(main);
        }

        if (anyFileFound)
        {
            LoadFailed = true;
            Debug.LogError("[SaveSystem] 저장 파일을 하나도 읽지 못했습니다. 종료 시 자동 저장을 막습니다." +
                           (CorruptBackupPath != null ? $" 원본 백업: {CorruptBackupPath}" : ""));
        }

        // 파일이 없거나 전부 실패 — 빈 데이터로 시작하되 플레이어는 평지 존(맵 중앙)에 둔다
        _liveCache = new LiveData();
        var spawn = FallbackSpawn();
        _liveCache.player.px = spawn.x; _liveCache.player.py = spawn.y; _liveCache.player.pz = spawn.z;
        return _liveCache;
    }

    private static bool TryParseLive(string path, out LiveData live)
    {
        live = null;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            live = JsonUtility.FromJson<LiveData>(json);
            if (live == null) return false;
            live.player    ??= new PlayerLiveData();
            live.buildings ??= new List<BuildingSaveData>();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] {Path.GetFileName(path)} 파싱 실패: {e.Message}");
            return false;
        }
    }

    private void BackupCorrupt(string path)
    {
        try
        {
            string backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(path, backup, overwrite: false);
            CorruptBackupPath = backup;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 손상 파일 백업 실패: {e.Message}");
        }
    }

    // 새 월드는 맵 중앙 청크의 한가운데를 평지 존으로 만든다(WorldPreGenerator).
    // live.json이 없을 때 (0,0,0) 구석이 아니라 그 자리에 스폰하도록 인덱스 범위로 계산한다.
    private Vector3 FallbackSpawn()
    {
        if (_chunkIndex == null || _chunkIndex.Count == 0) return Vector3.zero;
        int maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var c in _chunkIndex.Keys) { maxX = Mathf.Max(maxX, c.x); maxZ = Mathf.Max(maxZ, c.y); }
        var center = new Vector2Int((maxX + 1) / 2, (maxZ + 1) / 2);
        return ChunkUtils.ChunkToWorld(center)
             + new Vector3(ChunkUtils.CHUNK_SIZE / 2, 0f, ChunkUtils.CHUNK_SIZE / 2);
    }

    /// <summary>저장 후 캐시 무효화 — 다음 로드 시 파일에서 재읽기.</summary>
    private void InvalidateLiveCache() => _liveCache = null;

    // ── live.json 저장 (world.bin 건드리지 않음) ──────────────
    /// <summary>
    /// 플레이 중 저장 시 호출.
    /// live.json만 통째로 덮어씀 — world.bin은 변경 없음.
    /// unresolvedPlayerItems: 불러올 때 복원하지 못한 플레이어 아이템 — 버리지 않고 다시 쓴다.
    /// </summary>
    public bool Save(List<BuildingSaveData> buildings, PlayerController player,
                     List<InventoryEntry> unresolvedPlayerItems = null)
    {
        var live = new LiveData
        {
            buildings = buildings,
            player    = new PlayerLiveData
            {
                px   = player.transform.position.x,
                py   = player.transform.position.y,
                pz   = player.transform.position.z,
                rotY = player.transform.eulerAngles.y,
            }
        };
        InventorySaveUtil.Write(player.PlayerInventory, unresolvedPlayerItems, live.player.inventory);

        if (!WriteJsonAtomic(LiveJsonPath(_slot), JsonUtility.ToJson(live, prettyPrint: false)))
            return false;

        InvalidateLiveCache();
        LoadFailed = false;   // 정상 저장이 끝났으니 더는 빈 데이터로 덮어쓸 위험이 없다
        return true;
    }

    /// <summary>
    /// 임시 파일에 쓴 뒤 교체한다. 기존 파일은 .bak으로 남는다.
    /// (이전에는 Delete → Move 사이에 앱이 죽으면 live.json이 사라졌음)
    /// </summary>
    private static bool WriteJsonAtomic(string finalPath, string json)
    {
        string tempPath = finalPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, finalPath + ".bak");
            else                        File.Move(tempPath, finalPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] {Path.GetFileName(finalPath)} 저장 실패: {e.Message}");
            TryDelete(tempPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 정리 실패는 무시 — 다음 생성 때 다시 덮어쓴다 */ }
    }

    public void Close()
    {
        _reader?.Close();
        _reader = null;
    }

    // ══════════════════════════════════════════════════════════
    // WorldWriter — world.bin 스트리밍 쓰기 전담 내부 클래스
    // static 필드 대신 인스턴스로 관리해 상태 오염 방지.
    // BeginWorldWrite → WriteChunk(×N) → FinalizeWorldWrite 순서로 호출. 실패하면 AbortWorldWrite.
    //
    // world.bin.tmp에 쓰고, 끝까지 성공해야 world.bin으로 이름을 바꾼다 — 이 rename이 커밋이다.
    // (이전에는 world.bin에 바로 써서, 중간에 실패하거나 앱이 꺼지면 반쯤 쓴 파일이 "저장 있음"이 됐음)
    // ══════════════════════════════════════════════════════════
    private class WorldWriter : IDisposable
    {
        private readonly BinaryWriter                                       _w;
        private readonly long                                               _indexTableStart;
        private readonly List<(int cx, int cz, long offset, int length)>   _index;
        private readonly int                                                _totalChunks;

        public WorldWriter(string path, int totalChunks, float seed)
        {
            try
            {
                var stream       = new FileStream(path, FileMode.Create, FileAccess.Write);
                _w               = new BinaryWriter(stream);
                _index           = new List<(int, int, long, int)>(totalChunks);
                _totalChunks     = totalChunks;

                _w.Write(WORLD_MAGIC);
                _w.Write(WORLD_VERSION);
                _w.Write(seed);
                _w.Write(totalChunks);
                _indexTableStart = _w.BaseStream.Position;
                for (int i = 0; i < totalChunks; i++)
                { _w.Write(0); _w.Write(0); _w.Write(0L); _w.Write(0); }
            }
            catch (Exception e)
            {
                Dispose();
                throw new IOException($"world.bin 생성 실패: {e.Message}", e);
            }
        }

        // 실패하면 예외를 그대로 올린다 — 호출자가 무시하고 "완료"로 넘어가지 못하게
        public void WriteChunk(Vector2Int coord, ChunkData data)
        {
            long offset = _w.BaseStream.Position;
            foreach (float h in data.heights) _w.Write(h);
            _index.Add((coord.x, coord.y, offset, (int)(_w.BaseStream.Position - offset)));
        }

        public void WriteIndex()
        {
            if (_index.Count != _totalChunks)
                throw new InvalidOperationException($"청크 {_index.Count}/{_totalChunks}개만 기록됨");

            long endPos = _w.BaseStream.Position;
            _w.BaseStream.Seek(_indexTableStart, SeekOrigin.Begin);
            foreach (var (cx, cz, off, len) in _index)
            { _w.Write(cx); _w.Write(cz); _w.Write(off); _w.Write(len); }
            _w.BaseStream.Seek(endPos, SeekOrigin.Begin);
            _w.Flush();
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

    private static string WorldTmpPath(int slot) => WorldBinPath(slot) + ".tmp";

    public static void BeginWorldWrite(int slot, int totalChunks, float seed)
    {
        AbortWorldWrite();
        // 같은 슬롯을 읽는 중이면 먼저 닫는다 — 열린 파일 위로 rename할 수 없다
        if (Instance != null && Instance._slot == slot) { Instance.Close(); Instance = null; }

        Directory.CreateDirectory(SlotDir(slot));
        _currentWriter     = new WorldWriter(WorldTmpPath(slot), totalChunks, seed);
        _currentWriterSlot = slot;
    }

    public static void WriteChunk(Vector2Int coord, ChunkData data)
    {
        if (_currentWriter == null) throw new InvalidOperationException("BeginWorldWrite 없이 WriteChunk 호출");
        _currentWriter.WriteChunk(coord, data);
    }

    /// <summary>
    /// 인덱스를 채우고 world.bin으로 커밋한 뒤, 시작 위치만 담은 live.json을 쓴다.
    /// 실패하면 예외를 던지고 tmp를 지운다 — 기존 world.bin은 그대로 남는다.
    /// </summary>
    public static void FinalizeWorldWrite(int slot, Vector3 playerStart)
    {
        if (_currentWriter == null) throw new InvalidOperationException("BeginWorldWrite 없이 FinalizeWorldWrite 호출");
        if (slot != _currentWriterSlot) throw new InvalidOperationException($"슬롯 불일치 ({slot} ≠ {_currentWriterSlot})");

        try
        {
            _currentWriter.WriteIndex();
            _currentWriter.Dispose();
            _currentWriter = null;

            // 새 월드의 live.json을 먼저 .tmp로 준비 — 이전 게임의 건물이 새 지형에 섞이지 않게
            string live = LiveJsonPath(slot);
            var start = new LiveData
            {
                player = new PlayerLiveData { px = playerStart.x, py = playerStart.y, pz = playerStart.z },
            };
            File.WriteAllText(live + ".tmp", JsonUtility.ToJson(start, prettyPrint: false));

            // 이전 게임의 live.json·.bak을 먼저 치운다. 여기서부터 끝까지 사이에 앱이 꺼져도
            // LoadLive가 .tmp(새 시작 위치)를 찾으므로, 이전 게임의 건물이 새 지형에 섞이지 않는다.
            TryDelete(live + ".bak");
            if (File.Exists(live)) File.Delete(live);

            // 커밋: world.bin.tmp → world.bin
            string final = WorldBinPath(slot);
            if (File.Exists(final)) File.Delete(final);
            File.Move(WorldTmpPath(slot), final);

            File.Move(live + ".tmp", live);
        }
        catch
        {
            AbortWorldWrite();
            throw;
        }
    }

    /// <summary>생성 실패·중단 시 호출. 열린 파일을 닫고 쓰다 만 tmp를 지운다.</summary>
    public static void AbortWorldWrite()
    {
        if (_currentWriter == null) return;
        _currentWriter.Dispose();
        _currentWriter = null;
        TryDelete(WorldTmpPath(_currentWriterSlot));
    }

    // Enter Play Mode에서 Domain Reload를 꺼도 이전 플레이의 닫힌 리더가 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance?.Close();
        Instance       = null;
        LastError      = null;
        _currentWriter = null;
    }
}
