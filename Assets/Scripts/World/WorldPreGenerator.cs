using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>새 게임 시작 시 전체 맵을 생성해서 SaveSystem으로 저장.</summary>
public class WorldPreGenerator : MonoBehaviour
{
    [SerializeField] private int _mapWidth       = 16;
    [SerializeField] private int _mapHeight      = 16;
    [SerializeField] private int _chunksPerFrame = 4;

    public float  Progress    { get; private set; }
    public bool   IsCompleted { get; private set; }
    public bool   IsFailed    { get; private set; }
    public string StatusText  { get; private set; }
    public event Action         OnCompleted;
    /// <summary>생성 실패. 인자는 사용자에게 보여 줄 이유.</summary>
    public event Action<string> OnFailed;

    public async Task GenerateAll(int slot, float seed)
    {
        IsCompleted = false; IsFailed = false; Progress = 0f;

        var spawn = SpawnPoint(_mapWidth, _mapHeight);

        ChunkGenerator.SetSeed(seed);
        ChunkGenerator.SetFlatCenter(new Vector2(spawn.x, spawn.z));

        int  total  = _mapWidth * _mapHeight;
        int  done   = 0;
        var  coords = BuildCoordList(_mapWidth, _mapHeight);
        // 플레이 모드를 끄거나 씬이 바뀌어 이 오브젝트가 파괴되면 생성을 멈춘다
        var  token  = destroyCancellationToken;

        StatusText = "지형 생성 중...";

        try
        {
            SaveSystem.BeginWorldWrite(slot, total, seed);

            foreach (var coord in coords)
            {
                ChunkData data = ChunkGenerator.Generate(coord);
                SaveSystem.WriteChunk(coord, data);   // 실패하면 예외 → catch에서 Abort

                done++;
                Progress   = (float)done / total;
                StatusText = $"지형 생성 중... ({done}/{total})";
                if (done % _chunksPerFrame == 0)
                {
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();
                }
            }

            StatusText = "파일 저장 중...";
            // 여기서 world.bin.tmp → world.bin으로 커밋된다. 그 전에는 "저장 있음"이 되지 않는다.
            // spawn.y는 평지 존의 지면 높이(0). 불러올 때 SaveController가 살짝 띄워서 배치한다.
            SaveSystem.FinalizeWorldWrite(slot, spawn);

            StatusText = "완료!"; Progress = 1f; IsCompleted = true;
            OnCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // 생성 도중 종료 — 쓰다 만 world.bin.tmp를 지운다. 기존 세이브는 그대로다.
            SaveSystem.AbortWorldWrite();
        }
        catch (Exception e)
        {
            SaveSystem.AbortWorldWrite();
            IsFailed   = true;
            StatusText = "맵 생성에 실패했습니다.";
            Debug.LogException(e, this);
            OnFailed?.Invoke(e.Message);
        }
    }

    private void OnDestroy()
    {
        // await 사이에 파괴돼 continuation이 다시 돌지 못하는 경우에도 파일 핸들을 닫는다
        if (!IsCompleted) SaveSystem.AbortWorldWrite();
    }

    /// <summary>
    /// 맵 중앙 청크의 한가운데 — 평지 존의 중심이자 플레이어 시작점.
    /// 청크 모서리가 아니라 가운데라서 청크 경계에 걸치지 않는다.
    /// (TerrainPreview도 이 값을 써서 게임과 같은 지형을 만든다)
    /// </summary>
    public static Vector3 SpawnPoint(int mapWidth, int mapHeight)
    {
        var center = new Vector2Int(mapWidth / 2, mapHeight / 2);
        return ChunkUtils.ChunkToWorld(center)
             + new Vector3(ChunkUtils.CHUNK_SIZE / 2, 0f, ChunkUtils.CHUNK_SIZE / 2);
    }

    /// <summary>맵의 모든 청크 좌표를 중앙에서 가까운 순서로 돌려준다 (생성 순서).</summary>
    public static List<Vector2Int> BuildCoordList(int w, int h)
    {
        var center = new Vector2Int(w / 2, h / 2);
        var list   = new List<Vector2Int>(w * h);
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
            list.Add(new Vector2Int(x, z));
        list.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
        return list;
    }
}
