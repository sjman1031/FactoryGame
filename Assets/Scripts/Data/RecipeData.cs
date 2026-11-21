using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum MachineType { Hand, Smelter, Constructor, Manufacturer, Assembler }

/// <summary>레시피 정의. DataImporter가 recipes.csv로 만든다.</summary>
[CreateAssetMenu(fileName = "New Recipe", menuName = "Game Data/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("기본 정보")]
    public string      recipeId;
    public string      displayName;
    public MachineType machineType;
    // 이전 이름 craftingTime으로 저장된 에셋도 그대로 읽는다
    [FormerlySerializedAs("craftingTime")]
    public float       craftTime = 4f;

    [Header("재료 (최대 4개)")]
    public RecipeIngredient[] inputs = Array.Empty<RecipeIngredient>();

    [Header("결과물")]
    public ItemData output;
    public int      outputAmount = 1;

    public bool CanCraft(Inventory inventory)
    {
        foreach (var ing in inputs)
            if (inventory.GetAmount(ing.item) < ing.amount) return false;
        return true;
    }
}

// ── RecipeIngredient (RecipeData.inputs 원소) ─────────────────
/// <summary>레시피 재료 하나 (아이템 + 수량).</summary>
[Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int      amount;
}
