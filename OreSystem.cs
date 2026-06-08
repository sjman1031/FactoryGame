using System.Collections.Generic;
using UnityEngine;

// ── OreDefinition ─────────────────────────────────────────────
/// <summary>
/// 광물 하나의 정의.
/// [Excel 연동 시] ExcelOreTable에서 SO → 이 클래스로 변환 후 주입.
/// </summary>
[System.Serializable]
public class OreDefinition
{
    public string id;
    public string displayName;

    [Range(0f, 1f)] public float rarity;

    public float minHeight, maxHeight;
    public float bandFrequency, bandThreshold, bandTolerance, bandSeedOffset;
    public float clusterFrequency, clusterThreshold;

    public NodePurity purity;

    /// <summary>rarity 0 → weight 1.0 / rarity 1 → weight 0.1</summary>
    public float SpawnWeight => Mathf.Lerp(1f, 0.1f, rarity);
}

// ── IOreTable ─────────────────────────────────────────────────
/// <summary>
/// 광물 테이블 추상화.
/// ChunkGenerator는 이 인터페이스만 알면 됨 — 데이터 소스 교체에 무관.
/// </summary>
public interface IOreTable
{
    IReadOnlyList<OreDefinition> GetAll();
}

// ── HardcodedOreTable ─────────────────────────────────────────
/// <summary>
/// 광물 데이터 직접 정의 구현체.
/// [Excel 연동 시] ChunkGenerator.SetOreTable(new ExcelOreTable(...)) 한 줄만 변경.
/// </summary>
public class HardcodedOreTable : IOreTable
{
    private readonly List<OreDefinition> _ores;

    public HardcodedOreTable()
    {
        _ores = new List<OreDefinition>
        {
            new OreDefinition { id="iron_ore",   displayName="철광석",   rarity=0.10f, minHeight=0f,  maxHeight=10f, bandFrequency=0.008f, bandThreshold=0.50f, bandTolerance=0.12f, bandSeedOffset=0f,     clusterFrequency=0.04f, clusterThreshold=0.52f, purity=NodePurity.NORMAL },
            new OreDefinition { id="copper_ore", displayName="구리광석", rarity=0.35f, minHeight=5f,  maxHeight=18f, bandFrequency=0.012f, bandThreshold=0.50f, bandTolerance=0.09f, bandSeedOffset=137.5f, clusterFrequency=0.06f, clusterThreshold=0.55f, purity=NodePurity.NORMAL },
            new OreDefinition { id="coal",       displayName="석탄",     rarity=0.20f, minHeight=2f,  maxHeight=20f, bandFrequency=0.015f, bandThreshold=0.50f, bandTolerance=0.07f, bandSeedOffset=271.3f, clusterFrequency=0.08f, clusterThreshold=0.58f, purity=NodePurity.NORMAL },
            new OreDefinition { id="quartz",     displayName="석영",     rarity=0.70f, minHeight=12f, maxHeight=26f, bandFrequency=0.018f, bandThreshold=0.50f, bandTolerance=0.05f, bandSeedOffset=412.7f, clusterFrequency=0.10f, clusterThreshold=0.62f, purity=NodePurity.IMPURE },
            new OreDefinition { id="uranium",    displayName="우라늄",   rarity=0.95f, minHeight=18f, maxHeight=26f, bandFrequency=0.020f, bandThreshold=0.50f, bandTolerance=0.03f, bandSeedOffset=587.1f, clusterFrequency=0.12f, clusterThreshold=0.68f, purity=NodePurity.PURE  },
        };
    }

    public IReadOnlyList<OreDefinition> GetAll() => _ores;
}
