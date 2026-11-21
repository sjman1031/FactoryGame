using UnityEngine;

// ── 이동 수치 (인스펙터에서 편집) ─────────────────────────────
/// <summary>이동·점프·카메라 수치. PlayerMovementManager 인스펙터에서 편집한다.</summary>
[System.Serializable]
public class MovementConfig
{
    [Header("속도")]
    public float walkSpeed        = 8f;
    public float sprintSpeed      = 16f;
    public float airControl       = 0.4f;

    [Header("점프 / 중력")]
    public float jumpHeight       = 3f;
    public float gravity          = -25f;

    [Header("카메라")]
    public float mouseSensitivity = 2f;
    public float pitchMin         = -80f;
    public float pitchMax         =  80f;
}

/// <summary>CC.Move()를 호출하는 유일한 지점.</summary>
public class PlayerMovementManager : SingletonManager<PlayerMovementManager>
{
    [SerializeField] private MovementConfig _config = new();

    private PlayerController   _controller;
    private PlayerStateManager _stateManager;
    private Vector3            _velocity;
    private float              _pitch;

    protected override void OnInitialize()
    {
        _stateManager = new PlayerStateManager();
        _stateManager.OnStateChanged += OnStateChanged;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Register(PlayerController controller)
        => _controller = controller;

    /// <summary>등록된 본인일 때만 해제한다 — 새 플레이어가 먼저 등록된 뒤 이전 플레이어가 파괴돼도 지워지지 않게.</summary>
    public void Unregister(PlayerController controller)
    {
        if (_controller == controller) _controller = null;
    }

    // [최적화] 카메라 회전(HandleLook)을 FixedUpdate에서 Update로 이동.
    // FixedUpdate는 물리 프레임(기본 50Hz)에서 실행되는데,
    // 카메라 회전을 여기서 처리하면 모니터 주사율(60~144Hz)과 어긋나
    // 화면이 끊겨 보이는 현상(jitter)이 발생한다.
    // 카메라처럼 시각적 반응이 중요한 건 렌더 프레임과 동기화되는 Update에서 처리해야 한다.
    private void Update()
    {
        if (_controller == null) return;
        HandleLook(InputManager.Instance.Current);
    }

    private void FixedUpdate()
    {
        if (_controller == null) return;

        InputData   input    = InputManager.Instance.Current;
        // 점프는 Update에서 래치된 값을 여기서 한 번만 꺼낸다 — 물리 프레임이 없는 렌더 프레임에 눌러도 사라지지 않게
        input.jumpPressed    = InputManager.Instance.ConsumeJump();
        bool        grounded = _controller.CC.isGrounded;
        PlayerState state    = _stateManager.Evaluate(input, grounded, _velocity);

        Vector3 move = CalcHorizontal(input, state);
        CalcVertical(input, grounded);

        _controller.CC.Move((move + _velocity) * Time.fixedDeltaTime);
    }

    private void HandleLook(InputData input)
    {
        _controller.transform.Rotate(0f, input.lookAxis.x * _config.mouseSensitivity, 0f);
        _pitch = Mathf.Clamp(
            _pitch - input.lookAxis.y * _config.mouseSensitivity,
            _config.pitchMin, _config.pitchMax);
        _controller.CameraArm.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    private Vector3 CalcHorizontal(InputData input, PlayerState state)
    {
        float speed = state == PlayerState.Sprint ? _config.sprintSpeed : _config.walkSpeed;
        if (state is PlayerState.Jump or PlayerState.Fall) speed *= _config.airControl;

        Transform t = _controller.transform;
        return (t.right * input.moveAxis.x + t.forward * input.moveAxis.y).normalized * speed;
    }

    private void CalcVertical(InputData input, bool grounded)
    {
        if (grounded && _velocity.y < 0f) _velocity.y = -2f;
        if (input.jumpPressed && grounded)
            _velocity.y = Mathf.Sqrt(_config.jumpHeight * -2f * _config.gravity);
        _velocity.y += _config.gravity * Time.fixedDeltaTime;
    }

    private void OnStateChanged(PlayerState from, PlayerState to)
    {
        // TODO: AudioManager.Instance.OnMovementState(to);
        Debug.Log($"[PlayerMovementManager] {from} → {to}");
    }
}
