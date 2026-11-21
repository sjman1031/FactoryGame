using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 씬의 저장·복원 진입점.
/// 시작할 때 live.json에서 플레이어와 건물을 되살리고, F5와 게임 종료 때 저장한다.
/// </summary>
public class SaveController : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private BuildingCatalog  _catalog;

    // 지형 메시는 청크 로드 후 다음 프레임에 생기므로, 그 사이 지면 아래로 빠지지 않게 살짝 띄워 배치한다
    private const float RESTORE_LIFT = 1.5f;

    // 불러올 때 GameDatabase에서 찾지 못한 플레이어 아이템 — 다음 저장에 그대로 다시 쓴다
    private List<InventoryEntry> _unresolvedPlayerItems;

    private void Start()
    {
        var save = SaveSystem.Instance;
        if (save == null)
        {
            Debug.LogWarning("[SaveController] SaveSystem이 없습니다 — 로딩 씬에서 시작해야 저장·복원이 동작합니다.");
            return;
        }
        if (_player == null)
        {
            Debug.LogError("[SaveController] PlayerController가 연결되지 않았습니다.", this);
            return;
        }
        RestorePlayer(save.LoadPlayer());
        BuildingRegistry.RestoreAll(_catalog, save.LoadAllBuildings());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) SaveNow(manual: true);
    }

    private void OnApplicationQuit() => SaveNow(manual: false);

    private void SaveNow(bool manual)
    {
        var save = SaveSystem.Instance;
        if (save == null || _player == null) return;

        if (save.LoadFailed)
        {
            // 읽지 못한 세션의 상태는 빈 데이터다. 자동 저장으로 원본을 덮어쓰면 되돌릴 수 없다.
            if (!manual)
            {
                Debug.LogWarning("[SaveController] 불러오기에 실패한 세션이라 종료 시 자동 저장을 하지 않았습니다." +
                                 $" 원본 백업: {save.CorruptBackupPath ?? "(없음)"}");
                return;
            }
            Debug.LogWarning("[SaveController] 불러오기에 실패한 세션에서 수동 저장합니다. 손상된 원본은 백업돼 있습니다: " +
                             (save.CorruptBackupPath ?? "(없음)"));
        }

        if (save.Save(BuildingRegistry.SerializeAll(), _player, _unresolvedPlayerItems))
            Debug.Log("[SaveController] 저장했습니다.");
    }

    private void RestorePlayer(PlayerLiveData data)
    {
        // CharacterController는 위치를 내부에 들고 있어서, 끈 상태에서 옮겨야 순간이동이 적용된다
        _player.CC.enabled = false;
        _player.transform.SetPositionAndRotation(
            new Vector3(data.px, data.py + RESTORE_LIFT, data.pz),
            Quaternion.Euler(0f, data.rotY, 0f));
        _player.CC.enabled = true;

        _unresolvedPlayerItems = InventorySaveUtil.Restore(_player.PlayerInventory, data.inventory, _player);
    }
}
