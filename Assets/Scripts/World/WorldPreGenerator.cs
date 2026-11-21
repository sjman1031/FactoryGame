using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///  새 게임 시작 시 전체 맵 청크를 일괄 생성해서 파일로 저장
///  async/await로 프레임 분산 => 로딩 중 UI가 멈추지 않음
///  </summary>
public class WorldPreGenerator : MonoBehaviour
{
    [Header("생성 설정")]
    [SerializeField] private int _mapWidth      = 16;  // 청크 단위 맵 가로 크기
    [SerializeField] private int _mapHeight     = 16;  // 청크 단위 맵 세로 크기
    [SerializeField] private int _chunkPerFrame = 4; // 프레임당 생성할 청크 수 (성능 조절)

    // 진행률: 0.0 ~ 1.0
    public float  Progress   { get; private set; }
    public bool   IsCompleted { get; private set; }
    public string StatusText { get; private set; }

    // 완료 시 발행
    public event Action OnComplete;

    // 외부 진입점
    /// <summary> LoadingSceneController가 새 게임 시작 시 호출 </summary>
    public async Task GenerateAll(int slot, float seed)
    {
        ChunkGenerator.SetSeed(seed);

        int total  = _mapWidth * _mapHeight;
        int done   = 0;
        var coords = BuildCoordList(_mapWidth, _mapHeight);
        var nodes  = new List<NodeEntry>();

        StatusText = "지형 생성 중...";

        try
        {
            SaveSystem.BeginWorldWrite(slot, total);

            foreach (var coord in coords)
            {
                ChunkData data = ChunkGenerator.Generate(coord);

                foreach (var n in data.nodes)
                {
                    n.chunkCoordX = coord.x; n.chunkCoordZ = coord.y;
                    nodes.Add(n);
                }

                SaveSystem.WriteChunk(coord, data);

                done++;
                Progress   = (float)done / total;
                StatusText = $"지형 생성 중... ({done}/{total}) [{coord.x}, {coord.y}]";
                if (done % _chunkPerFrame == 0) await Task.Yield();
            }

            var center = new Vector2Int(_mapWidth / 2 , _mapWidth / 2);
            var playerStart = ChunkUtils.ChunkToWorld(center);

            StatusText = "파일 저장 중...";
            SaveSystem.FinalizeWorldWrite(slot, nodes, playerStart);

            StatusText = "완료!"; Progress = 1f; IsCompleted = true;
            OnComplete?.Invoke();
        }
        catch (Exception e)
        {
            // 예외 시 로딩 화면이 무한 대기하지 않도록 실패 표시
            StatusText = "오류 발생 - 다시 시도해주세요.";
            Debug.LogWarning($"[WorldPreGenerator] 맵 생성 실패: {e.Message}");
        }
    }

    // 내부 유틸
    /// <summary> 
    /// 플레이어 시작 지점(맵 중앙)에서 가까운 청크부터 우선 생성
    /// 로딩 직후 플레이어가 빈 맵에 놓이는 상황을 방지
    /// </summary> 
    private List<Vector2Int> BuildCoordList(int width, int height)
    {
        var center = new Vector2Int(width / 2, height / 2);
        var list   = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                list.Add(new Vector2Int(x, z));

        // 중앙에서 가까운 순으로 정렬
        list.Sort((a, b) =>
        {
            int sqrDa = (a - center).sqrMagnitude;
            int sqrDb = (b - center).sqrMagnitude;  

            return sqrDa.CompareTo(sqrDb);  
        });

        return list;
    }
}