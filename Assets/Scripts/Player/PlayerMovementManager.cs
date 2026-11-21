using UnityEngine;

/// <summary>
/// CC.Move()를 호출하는 유일한 지점
/// FixedUpdate에서 입력 -> 상태 -> 수평이동 -> 수직속도 -> 이동 적용 순으로 처리
/// </summary>
public class PlayerMovementManager : SingletonManager<PlayerMovementManager>
{
    [SerializeField] private MovementConfig _config = new();

    private PlayerController    _controller;
    private PlayerStateManager  _stateManager;
    private Vector3             _velocity;      // 수직 속도 누적 (중력 + 점프)
    private float               _pitch;         // 카메라 상하 각도

    // 초기화
    protected override void OnInit()
    {
        _stateManager = new PlayerStateManager();
        _stateManager.OnStateChanged += OnStateChanged;

        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Register(PlayerController controller)
    {
        _controller = controller;
    }

    // 매 물리 프레임
    private void FixedUpdate()
    {
        if (_controller == null) return;

        InputData input     = InputManager.Instance.current;
        bool      grounded  = _controller.CC.isGrounded;

        // 1. 상태 갱신 - 상태가 바뀌면 OnStateChanged 이벤트 발생
        PlayerState state = _stateManager.Evaluate(input, grounded, _velocity);

        // 2. 카메라 회전 (Look)
        HandleLook(input);

        // 3. 수평 이동량 계산
        Vector3 move = CalcHorizontal(input, state);

        // 4. 수직 속도 계산 (중력 + 점프)
        CalcVertical(input, grounded);

        // 5. 이동 적용
        _controller.CC.Move((move + _velocity) * Time.fixedDeltaTime);
    }

    // 카메라 상하 회전
    private void HandleLook(InputData input)
    {
        // 좌우: 플레이어 오브젝트 자체를 Y축 회전
        _controller.transform.Rotate(0f, input.lookAxis.x * _config.mouseSensitivity, 0f);

        // 상하: 카메라 암만 X축 회전 (회전 범위 제한)
        _pitch = Mathf.Clamp(
            _pitch - input.lookAxis.y * _config.mouseSensitivity,
            _config.pitchMin, 
            _config.pitchMax
            );      
        _controller.CameraArm.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    // 수평 이동
    private Vector3 CalcHorizontal(InputData input, PlayerState state)
    {
        float speed = (state == PlayerState.Sprint)
            ? _config.sprintSpeed
            : _config.walkSpeed;

        // 공중이면 컨트롤 감쇠
        bool airborne = state is PlayerState.Jump or PlayerState.Fall;
        if (airborne) speed *= _config.airControl;

        Transform t = _controller.transform;
        Vector3   dir = t.right   * input.moveAxis.x 
                      + t.forward * input.moveAxis.y;

        return dir.normalized * speed;
    }

    // 수직 속도 계산
    private void CalcVertical(InputData input, bool grounded)
    {
        // 바닥에 붙이는 최소값 
        if (grounded && _velocity.y < 0f) 
            _velocity.y = -2f;

        if (input.jumpPressed && grounded)
            _velocity.y = Mathf.Sqrt(_config.jumpForce * -2f * _config.gravity);

        _velocity.y += _config.gravity * Time.fixedDeltaTime;
    }

    // 상태 변경 이벤트
    private void OnStateChanged(PlayerState from, PlayerState to)
    {
        // Todo -> Audio, Animation 등 상태 변경에 따른 효과 처리
        Debug.Log($"State changed: {from} -> {to}");
    }
}