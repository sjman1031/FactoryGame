using System.Collections.Generic;
using UnityEngine;

/// <summary>buildingId(클래스 이름) → 건물 프리팹 매핑. 저장된 건물을 복원할 때 쓴다.</summary>
public class BuildingCatalog : MonoBehaviour
{
    [System.Serializable]
    public class Entry { public string buildingId; public BuildingBase prefab; }

    [SerializeField] private List<Entry> _entries = new();
    private Dictionary<string, BuildingBase> _map;

    private void Awake()
    {
        _map = new();
        foreach (var e in _entries)
        {
            if (e == null || e.prefab == null) continue;
            // 저장 데이터의 buildingId는 GetType().Name이다. 비워 두면 프리팹의 클래스 이름을 쓴다.
            // (이전에는 빈 칸 하나가 Awake에서 ArgumentNullException을 냈음)
            string id = string.IsNullOrEmpty(e.buildingId) ? e.prefab.GetType().Name : e.buildingId;
            if (_map.ContainsKey(id))
                Debug.LogWarning($"[BuildingCatalog] buildingId '{id}' 중복 — 나중 항목을 씁니다.", this);
            _map[id] = e.prefab;
        }
    }

    public BuildingBase Get(string id)
        => !string.IsNullOrEmpty(id) && _map != null && _map.TryGetValue(id, out var p) ? p : null;
}
