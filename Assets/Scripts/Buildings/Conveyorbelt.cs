using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IItemOutput -> IItemInput 으로 아이템을 자동 이송
/// 아이템은 Queue에서 진행도 (0 ~ 1) 로 이동 상태를 관리함
/// </summary>
public class Conveyorbelt : BuildingBase
{
    [Header("컨베이어 설정")]
    public float beltSpeed = 1f; // 초당 이동 속도 

    // 연결은 오브젝트 레퍼런스로 보관
    // TODO: 청크 경계 대비 -> ID 문자열로 교체
    public IItemOutput inputSource;
    public IItemInput  outputTarget;

    private readonly Queue<(ItemData item, float progress)> _belt = new();
    private float _fetchTimer;
    private const float FETCH_INTERVAL = 0.5f; // 소스에서 아이템 픽업 주기

    public override void OnBuildingPlaced() { }

    public override void Tick(float deltaTime)
    {
        FetchFromSource(deltaTime);
        AdvanceBelt(deltaTime);
    }

    /// <summary> BuildingPlacer가 배치 시 호출해 얄 끝을 연결 </summary>
    public void Connect(IItemOutput source, IItemInput target)
    {
        inputSource  = source;
        outputTarget = target;
    }

    // 소스에서 아이템 픽업
    public void FetchFromSource(float deltaTime)
    {
        if (inputSource == null) return;
        _fetchTimer += deltaTime;

        if (_fetchTimer < FETCH_INTERVAL) return;
        _fetchTimer = 0;

        if(inputSource.TryOutputItem(out var item))
            _belt.Enqueue((item, 0f)); 
    }

    // 벨트 위 아이템 전진
    public void AdvanceBelt(float deltaTime)
    {
        if (_belt.Count == 0) return;

        // 벨트 위 아이템 전체를 한 틱에 전진시킴.
        int count = _belt.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_belt.TryDequeue(out var entry)) break;

            float newProgress = entry.progress + deltaTime * beltSpeed;

            if (newProgress >= 1f) 
                outputTarget?.TryInputItem(entry.item); // 끝에 도달하면 출력
            else
                
                _belt.Enqueue((entry.item, newProgress)); // 아직 진행중이면 다시 큐에 넣음
        }
    }
}
