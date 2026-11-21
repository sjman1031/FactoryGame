using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// buildingId(문자열) -> BuildingBase 프리팹 조회
/// WorldStreamManager가 RestoreChunk 시 사용
/// Inspector에서 프리팹 등록
/// </summary>
public class BuildingCategory : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string       buildingId;
        public BuildingBase prefab;
    }

    [SerializeField] private List<Entry> _entries = new();
    private Dictionary<string, BuildingBase> _map;

    private void Awake()
    {
        _map = new ();
        foreach (var e in _entries)
            _map[e.buildingId] = e.prefab;
    }

    public BuildingBase Get(string buildingId)
        => _map.TryGetValue(buildingId, out var prefab) ? prefab : null;

}