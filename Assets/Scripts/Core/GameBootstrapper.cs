using UnityEngine;

/// <summary>
/// 씬에 단 하나 존재하는 진입점
/// 초기화 순서를 명시적으로 제어
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    [Header("Manager 프리팹")]
    [SerializeField] private InputManager           _inputManager;
    [SerializeField] private TickManager            _tickManager;
    [SerializeField] private PlayerMovementManager  _movementManager;
    [SerializeField] private WorldStreamingManager  _worldManager;


    private void Awake()
    {
        InitManager(_inputManager,    "InputManager");
        InitManager(_tickManager,     "TicManager");
        InitManager(_movementManager, "PlayerMovementManager");
        InitManager(_worldManager,    "WorldStreamingManager");

    }

    private void InitManager<T>(T manager, string name) where T : MonoBehaviour
    {
        if (manager == null)
        {
            Debug.LogError($"[BootStrap] {name}이 연결되지 않았습니다.");
            return;
        }
        // SingletonManager.Awake() 가 DontDestroyOnLoad 처리
        // 여기선 null 체크만 담당
    }

}