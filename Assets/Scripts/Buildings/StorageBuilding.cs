using System.Collections.Generic;
using UnityEngine;

/// <summary>아이템을 보관하는 창고. 패시브 건물.</summary>
public class StorageBuilding : BuildingBase, IItemInput, IItemOutput
{
    [SerializeField] private int _slotCount = 20;
    public Inventory Storage { get; private set; }

    // Deserialize(Instantiate 직후)와 이웃 벨트의 첫 배달(Start보다 먼저 올 수 있음)보다
    // 앞서 있어야 하므로 Awake에서 만든다.
    protected override void Awake()
    {
        base.Awake();
        Storage = new Inventory(_slotCount);
    }

    public override void OnBuildingPlaced()
    {
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

    public override BuildingSaveData Serialize()
    {
        var data = base.Serialize();
        InventorySaveUtil.Write(Storage, _unresolved, data.items);
        return data;
    }

    // 불러올 때 id를 찾지 못했거나 자리가 없어 못 넣은 항목. 버리지 않고 다음 저장에 그대로 다시 쓴다.
    private List<InventoryEntry> _unresolved;

    public override void Deserialize(BuildingSaveData data)
    {
        base.Deserialize(data);
        _unresolved = InventorySaveUtil.Restore(Storage, data.items, this);
    }
}
