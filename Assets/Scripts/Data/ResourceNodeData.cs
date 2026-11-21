using UnityEngine;
using UnityEngine.Serialization;

public enum NodePurity { Impure, Normal, Pure }

/// <summary>광물별 산출 정의(기본 산출량·순도). nodeId는 광물 id와 같다.</summary>
[CreateAssetMenu(fileName = "New ResourceNode", menuName = "Game Data/ResourceNode")]
public class ResourceNodeData : ScriptableObject
{
    [Header("기본 정보")]
    // 이전 이름 nodeID로 저장된 에셋도 그대로 읽는다
    [FormerlySerializedAs("nodeID")]
    public string     nodeId;
    public ItemData   item;
    public NodePurity purity;

    [Header("산출량")]
    public float baseOutputPerMinute = 60f;

    public float GetActualOutput(float minerEfficiency = 1f)
    {
        float purityMult = purity switch
        {
            NodePurity.Pure   => 2.0f,
            NodePurity.Normal => 1.0f,
            NodePurity.Impure => 0.5f,
            _                 => 1.0f
        };
        return baseOutputPerMinute * purityMult * minerEfficiency;
    }
}
