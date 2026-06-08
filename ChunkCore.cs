using System;
using System.Collections.Generic;
using UnityEngine;

// ── 좌표 변환 ─────────────────────────────────────────────────
public static class ChunkUtils
{
    public const int CHUNK_SIZE = 64;

    public static Vector2Int WorldToChunk(Vector3 worldPos) => new(
        Mathf.FloorToInt(worldPos.x / CHUNK_SIZE),
        Mathf.FloorToInt(worldPos.z / CHUNK_SIZE));

    public static Vector3 ChunkToWorld(Vector2Int coord)
        => new(coord.x * CHUNK_SIZE, 0f, coord.y * CHUNK_SIZE);

    // result를 외부에서 전달받아 재사용 — 매 호출마다 new HashSet 생성 방지
    public static void GetCoordsInRange(Vector2Int center, int range, HashSet<Vector2Int> result)
    {
        result.Clear();
        for (int x = -range; x <= range; x++)
        for (int z = -range; z <= range; z++)
            result.Add(new Vector2Int(center.x + x, center.y + z));
    }
}

// ── 청크 데이터 ───────────────────────────────────────────────
[Serializable]
public class ChunkData
{
    public Vector2Int coord;
    public float[]    heights   = new float[ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE];
    public List<NodeEntry>        nodes     = new();
    public List<BuildingSaveData> buildings = new();

    public ChunkData(Vector2Int coord) => this.coord = coord;

    public void SetHeight(int x, int z, float h)
        => heights[z * ChunkUtils.CHUNK_SIZE + x] = h;
    public float GetHeight(int x, int z)
        => heights[z * ChunkUtils.CHUNK_SIZE + x];
    public void AddNode(int x, int z, string nodeDataId, NodePurity purity)
        => nodes.Add(new NodeEntry
        {
            localX = x, localZ = z,
            nodeDataId = nodeDataId, purity = purity,
            chunkCoordX = coord.x, chunkCoordZ = coord.y
        });
}

[Serializable]
public class NodeEntry
{
    public int        localX, localZ;
    public string     nodeDataId;
    public NodePurity purity;
    public int        chunkCoordX, chunkCoordZ;
}

[Serializable]
public class BuildingSaveData
{
    public string     buildingId, instanceId, recipeId;
    public Vector3    position;
    public Quaternion rotation;
}
