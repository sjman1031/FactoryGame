/// <summary> 씬 전환 시 데이터를 넘기는 컨텍스트 (static) </summary>
public class LoadingContext
{
    public static LoadingContext Current { get; private set; }

    public bool isNewGame;
    public int slot;
    public string saveName;

    public static void SetNewGame(int slot, string saveName)
     => Current = new LoadingContext { isNewGame = true, slot = slot, saveName = saveName };
}