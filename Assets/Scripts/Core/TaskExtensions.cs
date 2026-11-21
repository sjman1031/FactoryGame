using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>async Task 보조 기능. 순수 C# — MonoBehaviour 아님.</summary>
public static class TaskExtensions
{
    /// <summary>
    /// 반환값을 버리는 호출용. `_ = SomeAsync();`는 예외가 Task 안에 갇혀 콘솔에 뜨지 않으므로
    /// 대신 `SomeAsync().Forget(this);`로 호출해 예외를 반드시 남긴다.
    /// 취소(OperationCanceledException)는 오브젝트가 파괴된 정상 종료이므로 남기지 않는다.
    /// </summary>
    public static async void Forget(this Task task, UnityEngine.Object context = null)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e, context); }
    }
}
