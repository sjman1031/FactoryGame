using UnityEngine;

public enum ItemCategory { Raw, Material, Component, Fuel }

/// <summary>아이템 정의. DataImporter가 items.csv로 만든다.</summary>
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
