using System;
using UnityEngine;

// ── 이동 수치 ──────────────────────────────────────────────────
[Serializable]
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

// ── 상태 열거 ──────────────────────────────────────────────────
public enum PlayerState { Idle, Walk, Sprint, Jump, Fall }

/// <summary>
/// 입력값 + 물리 상태로 PlayerState를 결정.
/// 순수 C# — PlayerMovementManager 내부에서 new로 생성.
/// </summary>
public class PlayerStateManager
{
    public PlayerState Current { get; private set; } = PlayerState.Idle;
    public event Action<PlayerState, PlayerState> OnStateChanged;

    public PlayerState Evaluate(InputData input, bool isGrounded, Vector3 velocity)
    {
        PlayerState next;

        if (!isGrounded && velocity.y < -0.1f) next = PlayerState.Fall;
        else if (!isGrounded)                   next = PlayerState.Jump;
        else if (!input.isAnyMove)              next = PlayerState.Idle;
        else if (input.sprintHeld)              next = PlayerState.Sprint;
        else                                    next = PlayerState.Walk;

        if (next != Current)
        {
            OnStateChanged?.Invoke(Current, next);
            Current = next;
        }
        return Current;
    }
}
