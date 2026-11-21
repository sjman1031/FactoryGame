using System;
using UnityEngine;

/// <summary> 건물 언로드 시 저장하는 직렬화 데이터 </summary>
[Serializable]
public class BuildingSaveData
{
    public string buildingId; // 건물 종류 식별자
    public string instanceId; // 청크 경계 연결용 고유 ID
    public string recipeId;
    public Vector3 position;
    public Quaternion rotation;
}