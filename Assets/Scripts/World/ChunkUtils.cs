using System.Collections.Generic;
using UnityEngine;

/// <summary>월드 좌표 ↔ 청크 좌표 변환과 범위 계산.</summary>
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
