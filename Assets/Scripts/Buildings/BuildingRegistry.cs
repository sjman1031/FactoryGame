using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아 있는 건물 전체의 목록.
/// 건물은 청크 스트리밍과 무관하게 항상 살아서 틱을 받는다 — 멀리 있는 공장도 계속 돌아간다.
/// 청크가 내려가면 그 위 건물의 렌더러만 끈다.
/// </summary>
public static class BuildingRegistry
{
    private static readonly Dictionary<string, BuildingBase> _live = new();

    // 복원하지 못한 건물(카탈로그에 프리팹 없음). 버리지 않고 다음 저장에 원본 그대로 다시 쓴다.
    private static readonly List<BuildingSaveData> _unresolved = new();

    public static void Register(string id, BuildingBase b)
    {
        if (_live.TryGetValue(id, out var existing) && existing != null && existing != b)
            Debug.LogError($"[BuildingRegistry] instanceId 중복: {id} — '{existing.name}'과 '{b.name}'. 나중 건물로 덮어씁니다.", b);
        _live[id] = b;
    }

    /// <summary>b가 그 id로 등록된 본인일 때만 지운다 — id가 겹친 다른 건물의 등록을 지우지 않게.</summary>
    public static void Unregister(string id, BuildingBase b)
    {
        if (id != null && _live.TryGetValue(id, out var current) && current == b)
            _live.Remove(id);
    }

    public static BuildingBase FindById(string id) => _live.TryGetValue(id, out var b) ? b : null;

    /// <summary>저장용 — 살아 있는 모든 건물을 직렬화. 복원하지 못한 건물도 원본 그대로 포함한다.</summary>
    public static List<BuildingSaveData> SerializeAll()
    {
        var list = new List<BuildingSaveData>(_live.Count + _unresolved.Count);
        foreach (var b in _live.Values)
            if (b != null) list.Add(b.Serialize());
        list.AddRange(_unresolved);
        return list;
    }

    /// <summary>게임 시작 때 한 번 — 저장된 건물을 전부 만든다.</summary>
    public static void RestoreAll(BuildingCatalog catalog, List<BuildingSaveData> saves)
    {
        _unresolved.Clear();
        if (saves == null || saves.Count == 0) return;
        if (catalog == null)
        {
            Debug.LogError("[BuildingRegistry] BuildingCatalog가 연결되지 않았습니다 — 건물을 복원하지 않고 저장 데이터를 보관합니다.");
            _unresolved.AddRange(saves);
            return;
        }

        var belts = new List<ConveyorBelt>();
        foreach (var save in saves)
        {
            var prefab = catalog.Get(save.buildingId);
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildingRegistry] 카탈로그에 '{save.buildingId}' 없음 — 보관해 두고 다음 저장에 그대로 씁니다.");
                _unresolved.Add(save);
                continue;
            }
            var building = Object.Instantiate(prefab, save.position, save.rotation);
            building.Deserialize(save);
            if (building is ConveyorBelt belt) belts.Add(belt);
        }

        // 벨트는 저장된 위치·방향 그대로 앞뒤 칸을 다시 찾아 연결한다.
        // 방금 만든 건물의 콜라이더를 물리 쿼리가 보도록 먼저 동기화.
        Physics.SyncTransforms();
        foreach (var belt in belts) belt.ResolveConnections();
    }

    /// <summary>청크가 올라오거나 내려갈 때 그 청크 위 건물의 렌더러를 켜고 끈다.</summary>
    public static void SetChunkVisible(Vector2Int coord, bool visible)
    {
        foreach (var b in _live.Values)
            if (b != null && ChunkUtils.WorldToChunk(b.transform.position) == coord)
                b.SetVisible(visible);
    }

    // Enter Play Mode에서 Domain Reload를 꺼도 이전 플레이의 건물 목록이 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _live.Clear();
        _unresolved.Clear();
    }
}
