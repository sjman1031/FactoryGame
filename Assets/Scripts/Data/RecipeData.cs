using UnityEngine;

public enum MachineType { HAND, SMELTER, CONSTRUCTOR, MANUFACTURER, ASSEMBLER }

/// <summary>
/// 재료 + 수량 
/// RecipeData 안에서만 사용
/// </summary>
[System.Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int      amount;
}

/// <summary>
/// 재료 -> 결과
/// CanCraft()로 인벤토리 충족 여부를 직접 확인
/// </summary>
[CreateAssetMenu(fileName = "New Recipe", menuName = "Game Data/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("기본 정보")]
    public string      recipeId;
    public string      displayName; 
    public MachineType machineType;
    public float       craftingTime = 4f; // 초 단위

    [Header("재료 (최대 4개)")]
    public RecipeIngredient[] inputs = new RecipeIngredient[0];

    [Header("결과물")]
    public ItemData output;
    public int      outputAmount = 1;

    /// <summary>
    /// 인벤토리에 재료가 충분한지 확인
    /// </summary>
    public bool CanCraft(Inventory inventory)
    {
        foreach (var ingredient in inputs)
        {
            if (inventory.GetAmount(ingredient.item) < ingredient.amount)
                return false;
        }

        return true;
    }
}