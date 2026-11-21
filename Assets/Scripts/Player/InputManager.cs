using UnityEngine;

/// <summary>
/// 한 프레임의 입력 상태를 담는 값
/// Manager 간 전달용 - 참조 오버헤드 없음
/// </summary>
public struct InputData
{
    public Vector2 moveAxis; // WASD -> (-1 ~ 1, -1 ~ 1) 
    public Vector2 lookAxis; // 마우스 델타
    public bool    jumpPressed;
    public bool    sprintHeld;   

    public bool isAnyMove => moveAxis.sqrMagnitude > 0.01f;
}

/// <summary>
/// 입력 수집만 담당
/// 로직 없음
/// </summary>
public class InputManager : SingletonManager<InputManager>
{
    public InputData current { get; private set; }

    private void Update()
    {
        current = new InputData
        {
            moveAxis    = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            lookAxis    = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
            jumpPressed = Input.GetButtonDown("Jump"),
            sprintHeld  = Input.GetKey(KeyCode.LeftShift),
        };
    }
}