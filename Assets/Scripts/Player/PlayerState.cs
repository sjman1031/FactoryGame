using System;
using UnityEngine;

/// <summary>
/// 이동 수치 데이터
/// Inspector에서 조정 가능 
/// </summary>
[Serializable]
public class MovementConfig
{
    [Header("속도")]
    public float walkSpeed      = 8f;
    public float sprintSpeed    = 16f;
    public float airControl     = 0.4f; // 공중 이동 감쇠 (0 ~ 1)

    [Header("점프 / 중력")]
    public float jumpForce  = 3f;
    public float gravity    = -25f;

    [Header("카메라")]
    public float mouseSensitivity = 2f;
    public float pitchMin = -80f;
    public float pitchMax = 80f;
}

public enum PlayerState
{
    Idle,
    Walk,
    Sprint,
    Jump,
    Fall
}

/// <summary>
/// 입력값 + 물리 상태를 보고 PlayerState를 결정
/// MonoBehaviour 불필요 -> PlayerMovementManager 내부에서 new로 생성
/// </summary>
public class PlayerStateManager
{
    public PlayerState current { get; private set; } = PlayerState.Idle;

    /// <summary> 상태가 바뀔때만 발행 </summary>
    public event Action<PlayerState, PlayerState> OnStateChanged;

    public PlayerState Evaluate(InputData input, bool isGrounded, Vector3 velocity)
    {
        PlayerState next;

        // 우선순위 순서로 판정
        if (!isGrounded && velocity.y < -0.1f)  next = PlayerState.Fall;
        else if (!isGrounded)                   next = PlayerState.Jump;
        else if (input.sprintHeld)              next = PlayerState.Sprint;
        else if (input.isAnyMove)               next = PlayerState.Walk;
        else                                    next = PlayerState.Idle;   

        if (next != current)
        {   
            OnStateChanged?.Invoke(current, next);
            current = next;
        }

        return current;
    }
}
