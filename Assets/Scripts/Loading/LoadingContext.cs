/// <summary>씬 전환 시 데이터를 넘기는 컨텍스트 (static).</summary>
public class LoadingContext
{
    public static LoadingContext Current { get; private set; }

    public bool   isNewGame;
    public int    slot;
    public string saveName;

    public static void SetNewGame(int slot, string saveName)
        => Current = new LoadingContext { isNewGame = true, slot = slot, saveName = saveName };

    public static void SetLoadGame(int slot)
        => Current = new LoadingContext { isNewGame = false, slot = slot };

    public static void Clear() => Current = null;

    // Enter Play Mode에서 Domain Reload를 꺼도 이전 플레이의 컨텍스트가 남지 않게 초기화
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;
}
