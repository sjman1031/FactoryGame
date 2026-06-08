using System.Collections.Generic;
using UnityEngine;

// ── StorageBuilding ───────────────────────────────────────────
/// <summary>아이템을 보관하는 창고. 패시브 건물.</summary>
public class StorageBuilding : BuildingBase, IItemInput, IItemOutput
{
    [SerializeField] private int _slotCount = 20;
    public Inventory Storage { get; private set; }

    public override void OnBuildingPlaced()
    {
        Storage = new Inventory(_slotCount);
        // TODO: Storage.OnChanged += UI 갱신
    }

    public override void Tick(float deltaTime) { }

    public bool TryInputItem(ItemData item) => Storage.TryAdd(item, 1);

    public bool TryOutputItem(out ItemData item)
    {
        foreach (var kv in Storage.GetAll())
        {
            if (kv.Value <= 0) continue;
            item = kv.Key;
            Storage.TryRemove(item, 1);
            return true;
        }
        item = null; return false;
    }
}

// ── ConveyorBelt ──────────────────────────────────────────────
/// <summary>IItemOutput → IItemInput 으로 아이템을 자동 이송.</summary>
public class ConveyorBelt : BuildingBase
{
    [Header("컨베이어 설정")]
    public float beltSpeed = 1f;

    public IItemOutput inputSource;
    public IItemInput  outputTarget;

    private readonly Queue<(ItemData item, float progress)> _belt = new();
    private float _fetchTimer;
    private const float FETCH_INTERVAL = 0.5f;

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

    private void FetchFromSource(float deltaTime)
    {
        if (inputSource == null) return;
        _fetchTimer += deltaTime;
        if (_fetchTimer < FETCH_INTERVAL) return;
        _fetchTimer = 0f;
        if (inputSource.TryOutputItem(out ItemData item))
            _belt.Enqueue((item, 0f));
    }

    private void AdvanceBelt(float deltaTime)
    {
        if (_belt.Count == 0) return;

        // [최적화] 이전 코드는 Peek()으로 값을 읽고 Dequeue()로 제거하는
        // 두 번의 Queue 접근이 있었음. TryDequeue()로 한 번에 처리.
        if (!_belt.TryDequeue(out var entry)) return;

        float newProgress = entry.progress + deltaTime * beltSpeed;

        if (newProgress >= 1f)
            outputTarget?.TryInputItem(entry.item);
        else
            _belt.Enqueue((entry.item, newProgress));
    }
}
