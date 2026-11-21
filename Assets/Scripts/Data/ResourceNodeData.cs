using UnityEngine;

public enum NodePurity { IMPURE, NORMAL, PURE }

/// <summary>
/// 자원 노드 산출량 데이터
/// 엑셀에서 baseOutputPerMinute 만 바꾸면 게임에 반영됨
/// </summary>
[CreateAssetMenu(fileName = "New ResourceNode", menuName = "Game Data/ResourceNode")]
public class ResourceNodeData : ScriptableObject
{
    [Header("기본 정보")]
    public string     nodeID;
    public ItemData   item;
    public NodePurity purity;

    [Header("산출량")]
    [Tooltip("기준 분당 산출량. 순도 배율은 GetActualOutput()에서 자동 적용")]
    public float baseOutputPerMinute = 60f;

    /// <summary>
    /// 순도 배율 + 채굴기 효율을 적용한 실제 산출량 계산
    /// PURE x2 / NORMAL x1 / IMPURE x0.5
    /// </summary>
    public float GetActualOutput(float minerEfficiency = 1f)
    {
        float purityMultiplier = purity switch
        {
            NodePurity.PURE   => 2f,
            NodePurity.NORMAL => 1f,
            NodePurity.IMPURE => 0.5f,
            _                 => 1f
        };

        return baseOutputPerMinute * purityMultiplier * minerEfficiency;
    }
}