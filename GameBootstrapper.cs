using UnityEngine;

/// <summary>
/// 씬에 단 하나 존재하는 진입점.
/// 초기화 순서를 명시적으로 제어한다 — 순서가 곧 의존성 선언.
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    [Header("Manager — Inspector에서 연결")]
    [SerializeField] private InputManager          _inputManager;
    [SerializeField] private TickManager           _tickManager;
    [SerializeField] private WorldStreamingManager _worldManager;
    [SerializeField] private PlayerMovementManager _movementManager;

    private void Awake()
    {
        InitManager(_inputManager,    "InputManager");
        InitManager(_tickManager,     "TickManager");
        InitManager(_worldManager,    "WorldStreamingManager");
        InitManager(_movementManager, "PlayerMovementManager");
    }

    private void InitManager<T>(T manager, string name) where T : MonoBehaviour
    {
        if (manager == null)
            Debug.LogError($"[Bootstrap] {name} 이 연결되지 않았습니다!");
    }
}
