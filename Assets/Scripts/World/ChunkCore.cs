using System;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// 월드 좌표와 청크 좌표 변환 유틸
/// 모든 스트리밍 로직의 기반 - 상태 없음, 순수 static
/// </summary>
public static class ChunkUtils // 좌표 변환
{
    public const int CHUNK_SIZE = 64; // 64 * 64 유닛

    /// <summary> Vector3(130, 0 ,70) -> Vector2Int(2, 1)</summary>
    public static Vector2Int WorldToChunk(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CHUNK_SIZE),
            Mathf.FloorToInt(worldPos.z / CHUNK_SIZE)
            );
    }

    /// <summary> Vector2Int(2, 1) -> Vector3(128, 0, 64) </summary>
    public static Vector3 ChunkToWorld(Vector2Int coord)
    {
        return new Vector3(coord.x * CHUNK_SIZE, 0f, coord.y * CHUNK_SIZE);
    }

    /// <summary> 중심 좌표 기준 뷰 거리 내 모든 청크 좌표 반환 </summary>
    public static void GetCoordsInRange(Vector2Int center, int range, HashSet<Vector2Int> result)
    {
        result.Clear();
        for (int x = -range; x <= range; x++)
            for (int z = -range; z <= range; z++)
                result.Add(new Vector2Int(center.x + x, center.y + z));
    }
}

/// <summary> 청크 하나의 지형 + 노드 + 건물 저장 데이터. 순수 C# 데이터 컨테이너 </summary>
[Serializable]
public class ChunkData // 청크 데이터
{
    public Vector2Int coord;

    // 지형 높이맵 (64 * 64)
    public float[] heights = new float[ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE];

    // 자원 노드 위치 목록
    public List<NodeEntry> nodes = new();

    // 청크 언로드 시 직렬화된 건물 목록
    public List<BuildingSaveData> buildings = new();

    public ChunkData(Vector2Int coord) => this.coord = coord;

    public void SetHeight(int x, int z, float h)
        => heights[z * ChunkUtils.CHUNK_SIZE + x] = h;

    public float GetHeight(int x, int z)
        => heights[z * ChunkUtils.CHUNK_SIZE + x];

    public void AddNode(int x, int z, string nodeDataId, NodePurity purity)
     => nodes.Add(new NodeEntry { localX = x, localZ = z, nodeDataid = nodeDataId, purity = purity, chunkCoordX = coord.x, chunkCoordZ = coord.y });
}

/// <summary> 청크 내 자원 노드 위치 + SO id </summary>
[Serializable]
public class NodeEntry
{
    public int localX;
    public int localZ;
    public string nodeDataid; // ResourceNodeData.nodeId -> 런타임에 SO 조회
    public NodePurity purity;
    public int chunkCoordX, chunkCoordZ;
}