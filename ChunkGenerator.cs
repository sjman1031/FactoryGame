using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 절차적 지형 + 광맥 띠 생성.
/// SetOreTable()로 IOreTable 교체 가능 — Excel 연동 시 한 줄만 변경.
/// </summary>
public static class ChunkGenerator
{
    private const float FLAT_RADIUS_SQR       = 30f * 30f;  // [최적화] sqrt 제거 → sqrMagnitude 비교
    private const float TRANSITION_RADIUS_SQR = 50f * 50f;
    private const float FLAT_RADIUS           = 30f;        // Lerp 계산에만 사용
    private const float TRANSITION_RADIUS     = 50f;

    private static float     _seed     = 0f;
    private static IOreTable _oreTable = new HardcodedOreTable();

    // [최적화] GetAll() 결과를 캐싱 — Generate() 호출마다 리스트 반환 객체 생성 방지
    private static IReadOnlyList<OreDefinition> _cachedOres;

    public static void SetSeed(float seed)          => _seed     = seed;
    public static void SetOreTable(IOreTable table)
    {
        _oreTable   = table;
        _cachedOres = null; // 테이블 교체 시 캐시 무효화
    }

    public static ChunkData Generate(Vector2Int coord)
    {
        // [최적화] 캐시 없을 때만 GetAll() 호출
        _cachedOres ??= _oreTable.GetAll();

        var data = new ChunkData(coord);

        for (int x = 0; x < ChunkUtils.CHUNK_SIZE; x++)
        for (int z = 0; z < ChunkUtils.CHUNK_SIZE; z++)
        {
            float wx = coord.x * ChunkUtils.CHUNK_SIZE + x + _seed;
            float wz = coord.y * ChunkUtils.CHUNK_SIZE + z + _seed;
            float h  = SampleHeight(wx, wz);
            data.SetHeight(x, z, h);
            TryPlaceOre(data, x, z, wx, wz, h);
        }
        return data;
    }

    private static float SampleHeight(float wx, float wz)
    {
        // [최적화] Mathf.Sqrt 제거 — 대소 비교는 제곱값으로 충분
        float sqrDist = wx * wx + wz * wz;
        float terrain = SampleTerrain(wx, wz);

        if (sqrDist <= FLAT_RADIUS_SQR) return 0f;

        if (sqrDist <= TRANSITION_RADIUS_SQR)
        {
            // Lerp 비율 계산에는 실제 거리값이 필요하므로 여기서만 sqrt
            float dist = Mathf.Sqrt(sqrDist);
            float t    = (dist - FLAT_RADIUS) / (TRANSITION_RADIUS - FLAT_RADIUS);
            return Mathf.Lerp(0f, terrain, Mathf.SmoothStep(0f, 1f, t));
        }

        return terrain;
    }

    private static float SampleTerrain(float wx, float wz)
    {
        float h  = Mathf.PerlinNoise(wx * 0.01f, wz * 0.01f) * 20f;
              h += Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f) * 5f;
              h += Mathf.PerlinNoise(wx * 0.10f, wz * 0.10f) * 1f;
        return h;
    }

    private static void TryPlaceOre(ChunkData data,
                                    int lx, int lz,
                                    float wx, float wz, float height)
    {
        foreach (var ore in _cachedOres)
        {
            if (height < ore.minHeight || height > ore.maxHeight) continue;

            float band = Mathf.PerlinNoise(
                wx * ore.bandFrequency + ore.bandSeedOffset,
                wz * ore.bandFrequency + ore.bandSeedOffset * 0.7f);
            if (Mathf.Abs(band - ore.bandThreshold) > ore.bandTolerance) continue;

            float cluster = Mathf.PerlinNoise(
                wx * ore.clusterFrequency + ore.bandSeedOffset * 1.3f,
                wz * ore.clusterFrequency + ore.bandSeedOffset * 0.9f);
            if (cluster < ore.clusterThreshold) continue;

            float roll = Mathf.PerlinNoise(
                wx * 0.3f + ore.bandSeedOffset * 2.1f,
                wz * 0.3f + ore.bandSeedOffset * 1.7f);
            if (roll > ore.SpawnWeight) continue;

            float strength = (cluster - ore.clusterThreshold) / (1f - ore.clusterThreshold);
            NodePurity purity = strength > 0.7f ? Upgrade(ore.purity)
                              : strength < 0.2f ? Downgrade(ore.purity)
                              : ore.purity;

            data.AddNode(lx, lz, ore.id, purity);
            break;
        }
    }

    private static NodePurity Upgrade(NodePurity p) => p switch
    {
        NodePurity.IMPURE => NodePurity.NORMAL,
        NodePurity.NORMAL => NodePurity.PURE,
        _                 => p
    };

    private static NodePurity Downgrade(NodePurity p) => p switch
    {
        NodePurity.PURE   => NodePurity.NORMAL,
        NodePurity.NORMAL => NodePurity.IMPURE,
        _                 => p
    };
}
