using System;
using UnityEngine;

// ── ItemData ──────────────────────────────────────────────────
public enum ItemCategory { RAW, MATERIAL, COMPONENT, FUEL }

[CreateAssetMenu(fileName = "New Item", menuName = "Game Data/Item")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string       itemId;
    public string       displayName;
    public ItemCategory category;
    public int          stackSize = 50;

    [Header("비주얼")]
    public Sprite       icon;
}

// ── RecipeData ────────────────────────────────────────────────
public enum MachineType { HAND, SMELTER, CONSTRUCTOR, MANUFACTURER, ASSEMBLER }

[Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int      amount;
}

[CreateAssetMenu(fileName = "New Recipe", menuName = "Game Data/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("기본 정보")]
    public string      recipeId;
    public string      displayName;
    public MachineType machineType;
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

// ── ResourceNodeData ──────────────────────────────────────────
public enum NodePurity { IMPURE, NORMAL, PURE }

[CreateAssetMenu(fileName = "New ResourceNode", menuName = "Game Data/ResourceNode")]
public class ResourceNodeData : ScriptableObject
{
    [Header("기본 정보")]
    public string     nodeId;
    public ItemData   item;
    public NodePurity purity;

    [Header("산출량")]
    public float baseOutputPerMinute = 60f;

    public float GetActualOutput(float minerEfficiency = 1f)
    {
        float purityMult = purity switch
        {
            NodePurity.PURE   => 2.0f,
            NodePurity.NORMAL => 1.0f,
            NodePurity.IMPURE => 0.5f,
            _                 => 1.0f
        };
        return baseOutputPerMinute * purityMult * minerEfficiency;
    }
}
