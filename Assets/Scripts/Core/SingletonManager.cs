using UnityEngine;

/// <summary>
/// 모든 Manager의 베이스.
/// 중복 방지 + DontDestroyOnLoad를 한 곳에서 처리한다.
/// </summary>
public abstract class SingletonManager<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this as T;
        DontDestroyOnLoad(gameObject);
        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
