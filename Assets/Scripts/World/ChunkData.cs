using System;
using UnityEngine;

/// <summary>청크 하나의 높이 데이터. world.bin에 저장된다.</summary>
/// <remarks>
/// 건물은 청크가 아니라 BuildingRegistry가 들고 있다(건물 B안). 그래서 여기에는 지형만 있다.
/// (이전에는 쓰이지 않는 buildings 리스트가 청크마다 생겼음)
/// </remarks>
[Serializable]
public class ChunkData
{
    public Vector2Int coord;
    public float[]    heights;

    /// <summary>새로 생성할 때 — 빈 높이 배열을 만든다.</summary>
    public ChunkData(Vector2Int coord)
        : this(coord, new float[ChunkUtils.CHUNK_SIZE * ChunkUtils.CHUNK_SIZE]) { }

    /// <summary>파일에서 읽을 때 — 읽은 배열을 그대로 쓴다 (이전에는 빈 배열을 만든 뒤 바로 버렸음).</summary>
    public ChunkData(Vector2Int coord, float[] heights)
    {
        this.coord   = coord;
        this.heights = heights;
    }

    public void SetHeight(int x, int z, float h)
        => heights[z * ChunkUtils.CHUNK_SIZE + x] = h;
    public float GetHeight(int x, int z)
        => heights[z * ChunkUtils.CHUNK_SIZE + x];
}
