using UnityEngine;

/// <summary>
/// 숫자키 1~9로 건물 배치를 시작하는 임시 입력. BuildingPlacer.BeginPlace의 유일한 호출자.
/// 배치 로직은 BuildingPlacer에, 어떤 건물을 고를지는 여기에 둔다 — 나중에 건설 메뉴 UI로 바꿔도 Placer는 그대로다.
/// 같은 키를 다시 누르면 취소한다. (배치 중 Esc 취소·R 회전은 BuildingPlacer가 처리)
/// </summary>
public class BuildingHotbar : MonoBehaviour
{
    [SerializeField] private BuildingPlacer _placer;
    [Tooltip("0번 = 1키, 1번 = 2키 … 최대 9칸. BuildingCatalog에 넣은 프리팹과 같은 것을 넣는다.")]
    [SerializeField] private BuildingBase[] _slots = new BuildingBase[4];

    private const int MAX_SLOTS = 9;

    private void Awake()
    {
        if (_placer == null)
            Debug.LogError("[BuildingHotbar] BuildingPlacer가 연결되지 않았습니다.", this);
        if (_slots != null && _slots.Length > MAX_SLOTS)
            Debug.LogWarning($"[BuildingHotbar] 슬롯은 {MAX_SLOTS}칸까지만 키에 연결됩니다.", this);
    }

    private void Update()
    {
        if (_placer == null || _slots == null) return;

        int count = Mathf.Min(_slots.Length, MAX_SLOTS);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
            Select(i);
            return;
        }
    }

    private void Select(int index)
    {
        var prefab = _slots[index];
        if (prefab == null)
        {
            Debug.LogWarning($"[BuildingHotbar] {index + 1}번 슬롯이 비어 있습니다.", this);
            return;
        }

        // 같은 건물을 다시 고르면 토글로 취소
        if (_placer.IsPlacing && _placer.CurrentPrefab == prefab) _placer.CancelPlace();
        else                                                      _placer.BeginPlace(prefab);
    }
}
