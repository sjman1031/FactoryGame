using System;
using System.Collections.Generic;

/// <summary>
/// 아이템 보유량 관리. 순수 C# 클래스
/// 플레이어, 창고, 제작기 입/출력 버퍼 모두 이 클래스 사용
/// </summary>  
public class Inventory
{
    private readonly Dictionary<ItemData, int> _items = new();
    private readonly int _maxSlots;

    /// <summary> 아이템 추가/제거 시 발행. UI 갱신 연동용 </summary>
    public event Action OnChanged;

    public Inventory(int maxSlots = 20) => _maxSlots = maxSlots;

    // 읽기
    public int GetAmount(ItemData item)
        => _items.TryGetValue(item, out int amount) ? amount : 0;

    public bool HasItem(ItemData item, int amount = 1)
        => GetAmount(item) >= amount;           
    public IReadOnlyDictionary<ItemData, int> GetAll() => _items;

    // 쓰기
    /// <summary> 
    /// 아이템 추가 시도. 
    /// 슬롯 초과 또는 스택 한도 초과 시 false 반환
    /// </summary>
    public bool TryAdd(ItemData item, int amount)
    {
        if (amount <= 0) return false;
        if (!_items.ContainsKey(item) && _items.Count >= _maxSlots) return false;

        int newAmount = GetAmount(item) + amount;

        // 스택 한도 초과
        if (newAmount > item.stackSize) return false;

        _items[item] = newAmount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 아이템 제거 시도.
    /// 보유량 부족 시 false 반환 - 부분 차감 없음
    /// </summary>
    public bool TryRemove(ItemData item, int amount)
    {
        if (!HasItem(item, amount)) return false;

        _items[item] -= amount;
        if(_items[item] <= 0) _items.Remove(item);

        OnChanged?.Invoke();   
        return true;    
    }

    /// <summary>
    /// 레시피 재료를 한 번에 소모.
    /// CanCraft 확인 후 전체 차감
    /// </summary>
    public bool TryConsumeRecipe(RecipeData recipe)
    {
        if (!recipe.CanCraft(this)) return false;

        foreach(var ingredient in recipe.inputs)
            TryRemove(ingredient.item, ingredient.amount);

        return true;
    }
}