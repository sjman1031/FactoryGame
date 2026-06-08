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
public class InputManager : SingletonManager<InputManager>
{
    public InputData Current { get; private set; }

    private void Update()
    {
        Current = new InputData
        {
            moveAxis    = new Vector2(Input.GetAxisRaw("Horizontal"),
                                      Input.GetAxisRaw("Vertical")),
            lookAxis    = new Vector2(Input.GetAxis("Mouse X"),
                                      Input.GetAxis("Mouse Y")),
            jumpPressed = Input.GetButtonDown("Jump"),
            sprintHeld  = Input.GetKey(KeyCode.LeftShift),
        };
    }
}
