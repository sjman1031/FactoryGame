using UnityEngine;

/// <summary>
/// 아이템을 보관하는 창고. 패시브 건물 - Tick()에 처리 없음
/// 컨베이어가 넣고 꺼낼 수 있고, 플레이어가 직접 열람도 가능
/// </summary>
public class StorageBuilding : BuildingBase, IItemInput, IItemOutput
{
    [SerializeField] private int _slotCount = 20;

    public Inventory Storage { get; private set; }

    public override void OnBuildingPlaced()
    {
        Storage = new Inventory(_slotCount);
        // TODO: Storage.OnChanged += () => { /* UI 갱신 등 */ };
    }

    // 패시브 건물 - 주기 처리 없음
    public override void Tick(float deltaTime) { }

    // IItemInput - 컨베이어가 아이템을 넣을 때
    public bool TryInputItem(ItemData item) => Storage.TryAdd(item, 1);

    // IItemOutput - 컨베이어가 아이템을 꺼낼 때
    public bool TryOutputItem(out ItemData item)
    {
        foreach (var kv in Storage.GetAll())
        {
            if (kv.Value <= 0) continue; // 빈 슬롯
            item = kv.Key;
            Storage.TryRemove(item, 1);
            return true;
        }

        item = null;
        return false; // 보관함이 비어있음
    }

}