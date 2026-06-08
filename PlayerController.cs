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
        => PlayerMovementManager.Instance?.Register(null);
}
