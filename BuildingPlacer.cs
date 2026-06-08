using UnityEngine;

/// <summary>마우스 기반 건물 배치. 그리드 스냅 + 충돌 체크 + 프리뷰.</summary>
public class BuildingPlacer : MonoBehaviour
{
    [Header("레이어")]
    public LayerMask groundLayer;
    public LayerMask buildingLayer;

    [Header("프리뷰 머티리얼")]
    public Material previewValidMat;
    public Material previewInvalidMat;

    [Header("그리드")]
    public float gridSize = 1f;

    private BuildingBase _currentPrefab;
    private GameObject   _previewGO;
    private bool         _isPlacing;

    public void BeginPlace(BuildingBase prefab)
    {
        CancelPlace();
        _currentPrefab = prefab;
        _isPlacing     = true;
        _previewGO     = Instantiate(prefab.gameObject);
        foreach (var col in _previewGO.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    public void CancelPlace()
    {
        if (_previewGO != null) Destroy(_previewGO);
        _isPlacing = false;
    }

    private void Update()
    {
        if (!_isPlacing) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelPlace(); return; }
        if (!TryGetGroundPoint(out Vector3 pos)) return;

        Vector3 snapped = Snap(pos);
        _previewGO.transform.position = snapped;
        bool canPlace = CanPlace(snapped);
        SetPreviewMaterial(canPlace ? previewValidMat : previewInvalidMat);

        if (canPlace && Input.GetMouseButtonDown(0)) PlaceBuilding(snapped);
    }

    private void PlaceBuilding(Vector3 pos)
    {
        var placed = Instantiate(_currentPrefab, pos, Quaternion.identity);
        placed.gameObject.layer = LayerMask.NameToLayer("Building");
        if (placed is MinerBuilding miner) TryConnectMinerToNode(miner);
        // TODO: 건설 비용 차감
        CancelPlace();
    }

    private void TryConnectMinerToNode(MinerBuilding miner)
    {
        foreach (var col in Physics.OverlapSphere(miner.transform.position, 1.5f))
        {
            var node = col.GetComponent<ResourceNodeBehaviour>();
            if (node == null) continue;
            miner.targetNode = node.nodeData;
            miner.OnBuildingPlaced();
            return;
        }
    }

    private bool TryGetGroundPoint(out Vector3 point)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        { point = hit.point; return true; }
        point = Vector3.zero; return false;
    }

    private Vector3 Snap(Vector3 pos) => new(
        Mathf.Round(pos.x / gridSize) * gridSize, pos.y,
        Mathf.Round(pos.z / gridSize) * gridSize);

    private bool CanPlace(Vector3 pos)
        => Physics.OverlapBox(pos + Vector3.up * 0.5f,
               Vector3.one * 0.45f, Quaternion.identity, buildingLayer).Length == 0;

    private void SetPreviewMaterial(Material mat)
    {
        foreach (var rend in _previewGO.GetComponentsInChildren<Renderer>())
            rend.material = mat;
    }
}
