using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>마우스 기반 건물 배치. 그리드 스냅 + 충돌 체크 + 프리뷰. R로 90° 회전.</summary>
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
    private Quaternion   _rotation = Quaternion.identity;

    private readonly List<(string oreId, float probability)> _oreBuffer = new();

    public void BeginPlace(BuildingBase prefab)
    {
        CancelPlace();
        _currentPrefab = prefab;
        _isPlacing     = true;
        _previewGO     = Instantiate(prefab.gameObject);
        foreach (var col in _previewGO.GetComponentsInChildren<Collider>())
            col.enabled = false;
        // 프리뷰는 보이기만 해야 한다. 건물 스크립트를 꺼 두면 Start가 돌지 않아
        // TickManager·BuildingRegistry에 등록되지 않고 저장 대상에도 들어가지 않는다.
        foreach (var building in _previewGO.GetComponentsInChildren<BuildingBase>())
            building.enabled = false;
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
        if (Input.GetKeyDown(KeyCode.R)) _rotation *= Quaternion.Euler(0f, 90f, 0f);
        if (!TryGetGroundPoint(out Vector3 pos)) return;

        Vector3 snapped = Snap(pos);
        _previewGO.transform.SetPositionAndRotation(snapped, _rotation);
        // 채굴기는 광맥이 있는 칸에만 — 광맥은 화면에 보이지 않으므로 프리뷰 색이 탐지기 역할을 한다
        bool canPlace = CanPlace(snapped) && (!(_currentPrefab is MinerBuilding) || HasOre(snapped));
        SetPreviewMaterial(canPlace ? previewValidMat : previewInvalidMat);

        if (canPlace && Input.GetMouseButtonDown(0)) PlaceBuilding(snapped);
    }

    private void PlaceBuilding(Vector3 pos)
    {
        var placed = Instantiate(_currentPrefab, pos, _rotation);
        // 레이어는 BuildingBase.Awake에서 자식까지 Building으로 정한다
        if (placed is MinerBuilding)
            Debug.Log("[BuildingPlacer] 채굴기 배치 — " +
                      string.Join(", ", _oreBuffer.Select(o => $"{o.oreId} {o.probability:P0}")));
        ReconnectBelts(placed, pos);
        // TODO: 건설 비용 차감
        CancelPlace();
    }

    // 놓은 칸 주변 벨트가 새 건물을 소스·타깃으로 잡도록 다시 연결한다.
    private void ReconnectBelts(BuildingBase placed, Vector3 pos)
    {
        // 방금 만든 건물의 콜라이더를 물리 쿼리가 보도록 동기화
        Physics.SyncTransforms();
        if (placed is ConveyorBelt belt) belt.ResolveConnections();
        foreach (var col in Physics.OverlapSphere(pos + Vector3.up * 0.5f, 1.5f))
        {
            var neighbor = col.GetComponentInParent<ConveyorBelt>();
            if (neighbor != null && neighbor != placed) neighbor.ResolveConnections();
        }
    }

    private bool HasOre(Vector3 pos)
    {
        ChunkGenerator.GetOreDistribution(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z), pos.y, _oreBuffer);
        return _oreBuffer.Count > 0;
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
