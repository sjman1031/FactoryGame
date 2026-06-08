using System;
using System.Collections.Generic;

/// <summary>
/// 아이템 보유량 관리. 순수 C# — MonoBehaviour 아님.
/// 플레이어, 창고, 제작기 입력버퍼 등 모두 이 클래스를 사용한다.
/// </summary>
public class Inventory
{
    private readonly Dictionary<ItemData, int> _items = new();
    private readonly int _maxSlots;

    public event Action OnChanged;

    public Inventory(int maxSlots = 20) => _maxSlots = maxSlots;

    public int  GetAmount(ItemData item) => _items.TryGetValue(item, out int n) ? n : 0;
    public bool HasItem(ItemData item, int amount = 1) => GetAmount(item) >= amount;
    public IReadOnlyDictionary<ItemData, int> GetAll() => _items;

    public bool TryAdd(ItemData item, int amount)
    {
        if (amount <= 0) return false;
        if (!_items.ContainsKey(item) && _items.Count >= _maxSlots) return false;

        int newTotal = GetAmount(item) + amount;
        if (newTotal > item.stackSize) return false;

        _items[item] = newTotal;
        OnChanged?.Invoke();
        return true;
    }

    public bool TryRemove(ItemData item, int amount)
    {
        if (!HasItem(item, amount)) return false;
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
}
