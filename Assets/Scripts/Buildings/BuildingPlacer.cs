using UnityEngine;

/// <summary>
/// 마우스 기반 건물 배치 시스템.
/// 그리드 스냅 + 충돌 체크 + 프리뷰 표시
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    [Header("레이어 설정")]
    public LayerMask groundLayer;
    public LayerMask buildingLayer;

    [Header("프리뷰 매터리얼")]
    public Material previewValidMat;
    public Material previewInvalidMat;

    [Header("그리드")]
    public float gridSize = 1f;

    private BuildingBase _currentPrefab;
    private GameObject   _previewGO;
    private bool         _isPlacing;

    // 외부 진입점
    /// <summary> UI 버튼에서 배치 모드 시작 시 호출.
    public void BeginPlace(BuildingBase prefab)
    {
        CancelPlace();

        _currentPrefab = prefab;
        _isPlacing     = true;
        _previewGO     = Instantiate(prefab.gameObject);

        // 프리뷰는 충돌 처리 없음
        foreach (var col in _previewGO.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    public void CancelPlace()
    {
        if (_previewGO != null) Destroy(_previewGO);
        _isPlacing = true;
    }

    // 매 프레임
    private void Update()
    {
        if (!_isPlacing) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelPlace(); return; }
        if (!TryGetGroundPoint(out Vector3 pos)) return;

        Vector3 snapped = Snap(pos);
        _previewGO.transform.position = snapped;

        bool canPlace = CanPlace(snapped);
        SetPreviewMaterial(canPlace ? previewValidMat : previewInvalidMat); 

        if (canPlace && Input.GetMouseButtonDown(0))
            PlaceBuilding(snapped);
    }

    // 내부 처리
    private void PlaceBuilding(Vector3 pos)
    {
        var placed = Instantiate(_currentPrefab, pos, Quaternion.identity);
        placed.gameObject.layer = LayerMask.NameToLayer("Building");

        // 채굴기면 근처 노드 자동 연결
        if (placed is MinerBuilding miner)
            TryConnectMinerToNode(miner);

        // TODO: 건설 재료 빼기

        CancelPlace();
    }

    private void TryConnectMinerToNode(MinerBuilding miner)
    {
        Collider[] hits = Physics.OverlapSphere(miner.transform.position, 1.5f);

        foreach (var col in hits)
        {
            var node = col.GetComponent<ResourceNodeBehaviour>();
            if (node == null) continue;
            miner.targetNode = node.nodeData;
            miner.OnBuildingPlaced(); // interval 재계산
            return;
        }
    }

    private bool TryGetGroundPoint(out Vector3 point)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            point = hit.point;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private Vector3 Snap(Vector3 pos) => new(
        Mathf.Round(pos.x / gridSize) * gridSize,
        pos.y,
        Mathf.Round(pos.z / gridSize) * gridSize
        );

    private bool CanPlace(Vector3 pos) 
        => Physics.OverlapBox(pos + Vector3.up * 0.5f, 
            Vector3.one * 0.45f,
            Quaternion.identity, buildingLayer).Length == 0;

    private void SetPreviewMaterial(Material mat)
    {
        foreach (var rand in _previewGO.GetComponentsInChildren<Renderer>())
            rand.material = mat;
    }
}
