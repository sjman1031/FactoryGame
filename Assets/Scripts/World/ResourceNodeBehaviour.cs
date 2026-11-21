using UnityEngine;

/// <summary>
/// 씬에 배치되는 자원 노드 오브젝트
/// 데이터는 ResourceNodeData 에서 참조 - 이 컴프넌트는 위치 정보만 담당
/// </summary>
public class ResourceNodeBehaviour : MonoBehaviour
{
    public ResourceNodeData nodeData;

    // Scene 뷰에서 노드의 범위를 시각화하기 위한 Gizmos
    private void OnDrawGizmos()
    {
        if (nodeData == null) return;

        Gizmos.color = nodeData.purity switch
        {
            NodePurity.PURE   => Color.blue,
            NodePurity.NORMAL => Color.yellow,
            NodePurity.IMPURE => Color.red,
            _                 => Color.white
        };

        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}