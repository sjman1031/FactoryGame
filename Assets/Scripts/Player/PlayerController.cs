using UnityEngine;

/// <summary>
/// 데이터 보유만 하는 클래스
/// 이동 관련 로직 없음
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public CharacterController CC { get; private set; }
    public Inventory           PlayerInventory { get; } = new(20);

    [Header("카메라 암 - 카메라를 자식으로 두는 빈 오브젝트 연결")]
    [SerializeField] private Transform _cameraArm;
    public Transform CameraArm => _cameraArm;

    private void Awake()
        => CC = GetComponent<CharacterController>();


    // Awake 순서 보장을 위해 Start에서 등록
    private void Start()
       =>  PlayerMovementManager.Instance.Register(this);


    public void OnDestroy()
        => PlayerMovementManager.Instance?.Register(null);

}   