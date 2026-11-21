using UnityEngine;

/// <summary>데이터 보유만. 이동 로직 없음.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public CharacterController CC              { get; private set; }
    public Inventory           PlayerInventory { get; } = new(20);

    [SerializeField] private Transform _cameraArm;
    public Transform CameraArm => _cameraArm;

    private void Awake() => CC = GetComponent<CharacterController>();

    private void Start()
        => PlayerMovementManager.Instance.Register(this);

    private void OnDestroy()
    {
        // ?.는 Unity의 파괴 검사를 건너뛰므로 == null로 확인한다
        var manager = PlayerMovementManager.Instance;
        if (manager != null) manager.Unregister(this);
    }
}
