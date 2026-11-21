using UnityEngine;

/// <summary>한 프레임의 입력 상태를 담는 값 타입.</summary>
public struct InputData
{
    public Vector2 moveAxis;
    public Vector2 lookAxis;
    public bool    jumpPressed;
    public bool    sprintHeld;
    public bool    isAnyMove => moveAxis.sqrMagnitude > 0.01f;
}

/// <summary>입력 수집만 담당. 로직 없음.</summary>
// 다른 매니저의 Update보다 먼저 돌아야 같은 프레임의 입력을 읽는다 (이전에는 순서가 정해지지 않아 1프레임 지연 가능)
[DefaultExecutionOrder(-100)]
public class InputManager : SingletonManager<InputManager>
{
    public InputData Current { get; private set; }

    // GetButtonDown은 그 Update 프레임에만 true다. FixedUpdate가 그 프레임에 돌지 않으면 점프가 사라지므로
    // 눌림을 래치해 두고 FixedUpdate 쪽에서 ConsumeJump()로 한 번만 꺼내 쓴다.
    private bool _jumpLatched;

    private void Update()
    {
        if (Input.GetButtonDown("Jump")) _jumpLatched = true;

        Current = new InputData
        {
            moveAxis    = new Vector2(Input.GetAxisRaw("Horizontal"),
                                      Input.GetAxisRaw("Vertical")),
            lookAxis    = new Vector2(Input.GetAxis("Mouse X"),
                                      Input.GetAxis("Mouse Y")),
            jumpPressed = _jumpLatched,
            sprintHeld  = Input.GetKey(KeyCode.LeftShift),
        };
    }

    /// <summary>래치된 점프 입력을 꺼낸다. 물리 프레임에서 한 번만 호출한다.</summary>
    public bool ConsumeJump()
    {
        bool pressed = _jumpLatched;
        _jumpLatched = false;
        return pressed;
    }
}
