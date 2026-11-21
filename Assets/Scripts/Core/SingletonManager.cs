using UnityEngine;

/// <summary>
/// 모든 Manager 클래스의 베이스
/// 중복 방지 + OnDestroyOnLoad 를 한 곳에서 처리함
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class SingletonManager<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        DontDestroyOnLoad(gameObject);
        OnInit();
    }
    
    /// <summary>
    /// 하위 클래스 초기화 진입점
    /// Awake 대신 이걸 오버라이드 함
    /// </summary>
    protected virtual void OnInit() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}