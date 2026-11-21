using System;
using System.Collections.Generic;

/// <summary>
/// 아이템 보유량 관리. 순수 C# — MonoBehaviour 아님.
/// 플레이어, 창고, 제작기 입력버퍼 등 모두 이 클래스를 사용한다.
///
/// 슬롯 모델: 아이템 하나가 stackSize개마다 슬롯 한 칸을 차지한다.
/// 예) 슬롯 20칸, 철광석 stackSize 100 → 철광석만 넣으면 2000개까지 들어간다.
/// (이전에는 stackSize를 아이템 종류별 총량 한도로 써서 창고가 종류마다 한 스택에서 멈췄음)
/// 보유량은 종류별 총량으로 들고 있으므로 저장 형식(InventoryEntry{itemId, amount})은 그대로다.
/// </summary>
public class Inventory
{
    private readonly Dictionary<ItemData, int> _items = new();
    private readonly int _maxSlots;

    public event Action OnChanged;

    public Inventory(int maxSlots = 20) => _maxSlots = maxSlots;

    public int  MaxSlots => _maxSlots;
    public int  GetAmount(ItemData item) => item != null && _items.TryGetValue(item, out int n) ? n : 0;
    public bool HasItem(ItemData item, int amount = 1) => GetAmount(item) >= amount;
    public IReadOnlyDictionary<ItemData, int> GetAll() => _items;

    /// <summary>현재 사용 중인 슬롯 수.</summary>
    public int UsedSlots
    {
        get
        {
            int used = 0;
            foreach (var kv in _items) used += SlotsFor(kv.Key, kv.Value);
            return used;
        }
    }

    /// <summary>이 아이템을 더 넣을 수 있는 최대 개수 (빈 슬롯 + 이 아이템 마지막 스택의 남은 칸).</summary>
    public int GetAddableAmount(ItemData item)
    {
        if (item == null) return 0;
        int stack       = Math.Max(1, item.stackSize);
        int current     = GetAmount(item);
        int othersSlots = UsedSlots - SlotsFor(item, current);
        int freeSlots   = _maxSlots - othersSlots;
        return Math.Max(0, freeSlots * stack - current);
    }

    public bool TryAdd(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;
        if (amount > GetAddableAmount(item)) return false;

        _items[item] = GetAmount(item) + amount;
        OnChanged?.Invoke();
        return true;
    }

    public bool TryRemove(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || !HasItem(item, amount)) return false;
        _items[item] -= amount;
        if (_items[item] <= 0) _items.Remove(item);
        OnChanged?.Invoke();
        return true;
    }

    public bool TryConsumeRecipe(RecipeData recipe)
    {
        if (!recipe.CanCraft(this)) return false;
        foreach (var ing in recipe.inputs)
            TryRemove(ing.item, ing.amount);
        return true;
    }

    private static int SlotsFor(ItemData item, int amount)
    {
        if (amount <= 0) return 0;
        int stack = Math.Max(1, item.stackSize);
        return (amount + stack - 1) / stack;   // 올림 나눗셈
    }
}
