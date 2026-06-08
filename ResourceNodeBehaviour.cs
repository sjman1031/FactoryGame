using UnityEngine;

/// <summary>씬에 배치되는 자원 노드 오브젝트. 데이터는 SO에서 참조.</summary>
public class ResourceNodeBehaviour : MonoBehaviour
{
    public ResourceNodeData nodeData;

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
