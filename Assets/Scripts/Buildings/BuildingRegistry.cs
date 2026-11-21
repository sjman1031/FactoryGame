using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 청크 언로드 시 씬 건물을 직렬화, 재로드 시 복원
/// instanceId 기반 참조로 청크 경계를 넘는 연결을 유지
/// </summary>
public static class BuildingRegistry
{
    // 청크 좌표별 직렬화 데이터
    private static readonly Dictionary<Vector2Int, List<BuildingSaveData>> _registry = new();

    // instanceId -> 씬 오브젝트 (로드된 청크 건물만 존재)
    private static readonly Dictionary<string, BuildingBase> _liveBuildings = new();

    // 등록 / 해제
    /// <summary> 건물 배치 시 등록. BuildingBase.Start()에서 호출 </summary>
    public static void Register(string instanceId, BuildingBase building)
        => _liveBuildings[instanceId] = building;

    public static void Unregister(string instanceId)
        => _liveBuildings.Remove(instanceId);

    /// <summary> instanceId로 씬 건물 검색. 언로드 상태면 null </summary>
    public static BuildingBase FindById(string instanceId)
        => _liveBuildings.TryGetValue(instanceId, out var building) ? building : null;

    // 키 복사 버퍼 재사용 - SerializeChunk 호출마다 새로운 List 생성 방지용
    private static readonly List<string> _keyBuffer = new();

    // 직렬화 / 복원
    /// <summary> 청크 언로드 시: 씬 건물 -> 데이터로 변환 후 비활성화 </summary>
    public static void SerializeChunk(Vector2Int coord)
    {
        var saveList = new List<BuildingSaveData>();

        _keyBuffer.Clear();
        _keyBuffer.AddRange(_liveBuildings.Keys);

        foreach (var id in _keyBuffer)
        {
            if (!_liveBuildings.TryGetValue(id, out var building) || building == null) continue;

            if (ChunkUtils.WorldToChunk(building.transform.position) != coord) continue;

            saveList.Add(building.Serialize());
            building.gameObject.SetActive(false);
        }

        _registry[coord] = saveList;
    }

    /// <summary> 청크 로드시: 저장 데이터 -> 씬 오브젝트 스폰 </summary>
    public static void RestoreChunk(Vector2Int coord, BuildingCategory category,
                                    List<BuildingSaveData> buildings,
                                    List<NodeEntry> nodes)
    {
        if (buildings == null) return;
        
        foreach (var save in buildings)
        {
            var prefab = category.Get(save.buildingId);
            if (prefab == null) continue;
            Object.Instantiate(prefab, save.position, save.rotation).Deserialize(save);
        }
        // TODO: nodes -> ResourceNodeBehavior 복원   
    }
}
