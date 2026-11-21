using System;
using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════
// live.json 스키마 — 저장 포맷 전체가 이 파일 하나
// LiveData ─┬─ PlayerLiveData ── InventoryEntry
//           └─ BuildingSaveData ─ InventoryEntry (창고 내용물)
// ══════════════════════════════════════════════════════════════
/// <summary>live.json의 루트 — 플레이어와 건물 전체.</summary>
[Serializable]
public class LiveData
{
    public PlayerLiveData         player    = new();
    public List<BuildingSaveData> buildings = new();
}

/// <summary>플레이어 위치·회전·인벤토리.</summary>
[Serializable]
public class PlayerLiveData
{
    public float px, py, pz;   // position
    public float rotY;
    public List<InventoryEntry> inventory = new();
}

/// <summary>인벤토리 한 칸. 아이템은 itemId로 저장하고 GameDatabase로 되살린다.</summary>
[Serializable]
public class InventoryEntry
{
    public string itemId;
    public int    amount;
}

/// <summary>건물 하나의 저장 데이터. 파생 건물이 필요한 필드만 채운다.</summary>
[Serializable]
public class BuildingSaveData
{
    public string     buildingId, instanceId, recipeId;
    public Vector3    position;
    public Quaternion rotation;
    public List<InventoryEntry> items = new();   // 창고 내용물
}

// ── 인벤토리 ↔ InventoryEntry 변환 (플레이어·창고 공용) ─────────
/// <summary>
/// 인벤토리 저장·복원. 복원하지 못한 항목은 버리지 않고 돌려준다.
/// 호출자가 그 목록을 들고 있다가 저장할 때 Write에 넘기면, 데이터를 고친 뒤 다시 살릴 수 있다.
/// (이전에는 GameDatabase에서 id를 못 찾으면 조용히 건너뛰어, 다음 저장에서 영구히 사라졌음)
/// </summary>
public static class InventorySaveUtil
{
    /// <summary>entries를 inventory에 넣는다. 넣지 못한 항목(모르는 id, 자리 부족)을 반환한다.</summary>
    public static List<InventoryEntry> Restore(Inventory inventory, List<InventoryEntry> entries, UnityEngine.Object context)
    {
        var unresolved = new List<InventoryEntry>();
        if (entries == null) return unresolved;

        foreach (var entry in entries)
        {
            if (entry == null || entry.amount <= 0) continue;
            var item = GameDatabase.GetItem(entry.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[InventorySaveUtil] 아이템 '{entry.itemId}' ×{entry.amount}을 찾지 못했습니다 — 보관해 두고 다음 저장에 그대로 씁니다.", context);
                unresolved.Add(entry);
                continue;
            }

            int fit = Mathf.Min(entry.amount, inventory.GetAddableAmount(item));
            if (fit > 0) inventory.TryAdd(item, fit);
            if (fit < entry.amount)
            {
                Debug.LogWarning($"[InventorySaveUtil] '{entry.itemId}' {entry.amount - fit}개가 자리가 없어 들어가지 않았습니다 — 보관합니다.", context);
                unresolved.Add(new InventoryEntry { itemId = entry.itemId, amount = entry.amount - fit });
            }
        }
        return unresolved;
    }

    /// <summary>inventory 내용과 unresolved 항목을 target에 쓴다.</summary>
    public static void Write(Inventory inventory, List<InventoryEntry> unresolved, List<InventoryEntry> target)
    {
        foreach (var kv in inventory.GetAll())
            target.Add(new InventoryEntry { itemId = kv.Key.itemId, amount = kv.Value });
        if (unresolved != null)
            foreach (var e in unresolved)
                target.Add(new InventoryEntry { itemId = e.itemId, amount = e.amount });
    }
}
