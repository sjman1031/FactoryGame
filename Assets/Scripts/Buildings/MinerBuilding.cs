using UnityEngine;

/// <summary>
/// 자원 노드 위에 설치해 아이템을 주기적으로 채굴하는 채굴기
/// 생산한 아이템은 _pendingItem에 대기 - 컨베이어가 TryOutputItem()로 가져감
/// </summary>
public class MinerBuilding : BuildingBase, IItemOutput
{
    [Header("채굴기 설정")]
    public ResourceNodeData targetNode;
    [Range(0.5f, 2f)]
    public float efficiency = 1f;

    private float    _timer;
    private float    _interval;    // 아이템 1개 생산 간격 (초)
    private ItemData _pendingItem; // 출력 대기중인 아이템

    public override void OnBuildingPlaced()
    {
        if (targetNode == null)
        {
            Debug.LogWarning($"[Miner] {name} - targetNode가 없습니다.");
            return;
        }

        // 분당 산출량 -> 초 단위 생산 간격
        _interval = 60f / targetNode.GetActualOutput(efficiency);
    }

    /// <summary>
    /// 배치 후 노드를 연결할 때 호출. OnBuildingPlaced()를 중복 호출하지 않고
    /// interval만 재계산한다.
    /// </summary>
    public void ConnetNode(ResourceNodeData node)
    {
        targetNode = node;
        _interval = 60f / targetNode.GetActualOutput(efficiency);
    }

    public override void Tick(float deltaTime)
    {
        if (targetNode == null) return;

        // 대기중인 아이템이 있으면 컨베이어가 가져갈 때까지 생산 중단
        if (_pendingItem != null) return;

       _timer += deltaTime;
       if (_timer < _interval) return;

       _timer -= _interval;
        _pendingItem = targetNode.item;
    }

    // IItemOutput - 컨베이어가 아이템을 가져갈 때 호출
    public bool TryOutputItem(out ItemData item)
    {
        if (_pendingItem == null)  { item = null; return false; }

        item         = _pendingItem;
        _pendingItem = null;
        return true;
    }
}