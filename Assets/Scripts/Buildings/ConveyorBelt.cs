using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뒤 칸 건물(IItemOutput)에서 꺼내 앞 칸 건물(IItemInput)로 아이템을 이송.
/// 벨트끼리도 이어진다 — 벨트는 IItemInput이라 앞 벨트가 뒤 벨트로 밀어 넣는다.
/// </summary>
public class ConveyorBelt : BuildingBase, IItemInput
{
    [Header("컨베이어 설정")]
    public float beltSpeed = 1f;

    public IItemOutput inputSource;
    public IItemInput  outputTarget;

    private readonly Queue<(ItemData item, float progress)> _belt = new();
    private float _fetchTimer;
    private const float FETCH_INTERVAL = 0.5f;
    // 벨트 한 칸에 올라갈 수 있는 최대 개수. 출구가 막혀 가득 차면 더 받지 않아서 막힘이 소스까지 전해진다.
    private const int   CAPACITY       = 4;

    public override void OnBuildingPlaced() { }

    public override void Tick(float deltaTime)
    {
        FetchFromSource(deltaTime);
        AdvanceBelt(deltaTime);
    }

    public void Connect(IItemOutput source, IItemInput target)
    {
        inputSource = source; outputTarget = target;
    }

    /// <summary>
    /// 뒤 칸 건물을 소스로, 앞 칸 건물을 타깃으로 연결한다. 이미 연결된 쪽은 그대로 둔다.
    /// 벨트를 놓을 때, 이웃에 건물이 새로 생길 때, 저장에서 복원할 때 호출된다.
    /// </summary>
    public void ResolveConnections()
    {
        inputSource  ??= FindNeighbor<IItemOutput>(transform.position - transform.forward);
        outputTarget ??= FindNeighbor<IItemInput>(transform.position + transform.forward);

        // 앞 칸 벨트가 나를 향하고 있으면 둘이 서로에게 밀어 넣어 아이템이 영원히 오간다 — 연결하지 않는다
        if (outputTarget is ConveyorBelt front &&
            Vector3.Dot(front.transform.forward, transform.forward) < -0.5f)
            outputTarget = null;
    }

    // 이웃 칸 가운데(건물 높이 중간)에 작은 구를 놓아 그 칸의 건물을 찾는다. 대각선 칸에는 닿지 않는 크기.
    private T FindNeighbor<T>(Vector3 cell) where T : class
    {
        foreach (var col in Physics.OverlapSphere(cell + Vector3.up * 0.5f, 0.3f))
            if (col.GetComponentInParent<BuildingBase>() is T found && !ReferenceEquals(found, this))
                return found;
        return null;
    }

    /// <summary>앞 벨트가 밀어 넣을 때. 자리가 있으면 맨 뒤에 올린다.</summary>
    public bool TryInputItem(ItemData item)
    {
        if (_belt.Count >= CAPACITY) return false;
        _belt.Enqueue((item, 0f));
        return true;
    }

    private void FetchFromSource(float deltaTime)
    {
        if (inputSource == null || _belt.Count >= CAPACITY) return;
        _fetchTimer += deltaTime;
        if (_fetchTimer < FETCH_INTERVAL) return;
        _fetchTimer = 0f;
        if (inputSource.TryOutputItem(out ItemData item))
            _belt.Enqueue((item, 0f));
    }

    private void AdvanceBelt(float deltaTime)
    {
        if (_belt.Count == 0) return;

        // 벨트 위 아이템 전체를 한 틱에 전진시킴.
        // 이전 코드는 TryDequeue()를 한 번만 호출해 첫 아이템만 처리했음 — 버그.
        // 현재 큐 크기만큼만 반복해 이번 틱에 새로 추가된 아이템은 건드리지 않음.
        int count = _belt.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_belt.TryDequeue(out var entry)) break;

            float newProgress = entry.progress + deltaTime * beltSpeed;

            // 끝에 닿았으면 넘기고, 받는 쪽이 없거나 거부하면 끝에서 기다린다.
            // (이전에는 결과를 무시해서 거부된 아이템이 사라졌음)
            if (newProgress >= 1f && outputTarget != null && outputTarget.TryInputItem(entry.item))
                continue;

            _belt.Enqueue((entry.item, Mathf.Min(newProgress, 1f)));
        }
    }
}
