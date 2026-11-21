using UnityEngine;

public enum ItemCategory { NONE, MATERIAL, COMPONENT, FUEL }

/// <summary>
/// 게임 내 모든 아이템의 원형 데이터
/// Inventory의 키로 사용되므로 ScriptableObject로 구현
/// </summary>
[CreateAssetMenu(fileName ="New Item", menuName = "Game Data/Item")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string       itemId;
    public string       displayName;
    public ItemCategory category;
    public int          stackSize = 50;

    [Header("비주얼")]
    public Sprite       icon;
    public GameObject   worldPrefab;
}