using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 절차적 지형 + 광맥 확률 분포.
/// SetOreTable()로 IOreTable 교체 가능 — Excel 연동 시 한 줄만 변경.
/// </summary>
public static class ChunkGenerator
{
    private const float FLAT_RADIUS_SQR       = 30f * 30f;  // [최적화] sqrt 제거 → sqrMagnitude 비교
    private const float TRANSITION_RADIUS_SQR = 50f * 50f;
    private const float FLAT_RADIUS           = 30f;        // Lerp 계산에만 사용
    private const float TRANSITION_RADIUS     = 50f;

    private static float     _seed       = 0f;
    private static Vector2   _flatCenter = Vector2.zero;
    private static IOreTable _oreTable   = new HardcodedOreTable();

    // [최적화] GetAll() 결과를 캐싱 — Generate() 호출마다 리스트 반환 객체 생성 방지
    private static IReadOnlyList<OreDefinition> _cachedOres;

    public static void SetSeed(float seed)          => _seed     = seed;
    /// <summary>평지 존의 중심(월드 x, z). 새 월드를 생성하기 전에 스폰 위치로 지정한다.</summary>
    public static void SetFlatCenter(Vector2 worldXZ) => _flatCenter = worldXZ;
    public static void SetOreTable(IOreTable table)
    {
        _oreTable   = table;
        _cachedOres = null; // 테이블 교체 시 캐시 무효화
    }

    // Enter Play Mode에서 Domain Reload를 꺼도 이전 플레이의 시드·평지 중심이 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _seed       = 0f;
        _flatCenter = Vector2.zero;
        _oreTable   = new HardcodedOreTable();
        _cachedOres = null;
    }

    public static ChunkData Generate(Vector2Int coord)
    {
        var data = new ChunkData(coord);

        for (int x = 0; x < ChunkUtils.CHUNK_SIZE; x++)
        for (int z = 0; z < ChunkUtils.CHUNK_SIZE; z++)
        {
            int gx = coord.x * ChunkUtils.CHUNK_SIZE + x;   // 월드 격자 좌표
            int gz = coord.y * ChunkUtils.CHUNK_SIZE + z;
            data.SetHeight(x, z, SampleHeight(gx, gz));
        }
        return data;
    }

    private static float SampleHeight(int gx, int gz)
    {
        // 평지 판정은 실제 위치로, 지형 노이즈는 시드를 더한 좌표로 계산한다.
        // (이전에는 거리 계산에도 시드가 섞여서 평지 존이 맵 밖에 생겼음)
        float dx      = gx - _flatCenter.x;
        float dz      = gz - _flatCenter.y;
        float sqrDist = dx * dx + dz * dz;   // [최적화] Mathf.Sqrt 제거 — 대소 비교는 제곱값으로 충분
        float terrain = SampleTerrain(gx + _seed, gz + _seed);

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

    /// <summary>
    /// 한 칸에서 캘 수 있는 광물별 확률(0~1)을 result에 채운다. 확률이 0인 광물은 넣지 않는다.
    /// 띠 중심선에 가까울수록, 군집 노이즈가 문턱보다 높을수록, 희귀도가 낮을수록 확률이 커진다.
    /// 확률의 합이 1보다 작으면 나머지는 '아무것도 없음'이다.
    /// 생성 때와 같은 시드 좌표를 쓰므로 게임 중 어느 위치든 다시 계산할 수 있다 — 저장하지 않는다.
    /// </summary>
    public static void GetOreDistribution(int gx, int gz, float height,
                                          List<(string oreId, float probability)> result)
    {
        _cachedOres ??= _oreTable.GetAll();
        result.Clear();

        float wx    = gx + _seed;
        float wz    = gz + _seed;
        float total = 0f;

        foreach (var ore in _cachedOres)
        {
            if (height < ore.minHeight || height > ore.maxHeight) continue;

            float band = Mathf.PerlinNoise(
                wx * ore.bandFrequency + ore.bandSeedOffset,
                wz * ore.bandFrequency + ore.bandSeedOffset * 0.7f);
            float bandWeight = 1f - Mathf.Abs(band - ore.bandThreshold) / ore.bandTolerance; // 띠 중심 1 → 가장자리 0
            if (bandWeight <= 0f) continue;

            float cluster = Mathf.PerlinNoise(
                wx * ore.clusterFrequency + ore.bandSeedOffset * 1.3f,
                wz * ore.clusterFrequency + ore.bandSeedOffset * 0.9f);
            float clusterWeight = Mathf.Clamp01(
                (cluster - ore.clusterThreshold) / (1f - ore.clusterThreshold));            // 문턱 0 → 최대 1
            if (clusterWeight <= 0f) continue;

            float probability = bandWeight * clusterWeight * ore.SpawnWeight;
            result.Add((ore.id, probability));
            total += probability;
        }

        // 여러 광맥이 겹쳐 합이 1을 넘으면 비율을 유지한 채 합을 1로 맞춘다
        if (total > 1f)
            for (int i = 0; i < result.Count; i++)
                result[i] = (result[i].oreId, result[i].probability / total);
    }
}
