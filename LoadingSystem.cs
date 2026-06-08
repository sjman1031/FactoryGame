using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// ── 씬 간 데이터 전달 ────────────────────────────────────────
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
}

// ── 로딩 씬 진행자 ───────────────────────────────────────────
/// <summary>새 게임 → WorldPreGenerator → 게임 씬 전환.</summary>
public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private string              _gameSceneName = "GameScene";
    [SerializeField] private WorldPreGenerator   _generator;
    [SerializeField] private LoadingUIController _ui;
    [SerializeField] private int                 _saveSlot = 0;

    private void Start()
    {
        var ctx = LoadingContext.Current;
        if (ctx != null && ctx.isNewGame)
            _ = StartNewGame(ctx.saveName, ctx.slot);
        else
            _ = LoadExistingGame(ctx?.slot ?? _saveSlot);
    }

    private async Task StartNewGame(string saveName, int slot)
    {
        _ui.SetStatus("새 게임 준비 중...");
        float seed = UnityEngine.Random.Range(0f, 9999f);
        _generator.OnCompleted += OnGenerationDone;
        await _generator.GenerateAll(slot, seed);
    }

    private async Task LoadExistingGame(int slot)
    {
        _ui.SetStatus("저장 파일 확인 중...");
        if (!SaveSystem.Exists(slot))
        {
            Debug.LogError($"[Loading] 슬롯 {slot} 저장 파일 없음");
            return;
        }
        await Task.Yield();
        OnGenerationDone();
    }

    private void OnGenerationDone()
    {
        _ui.SetStatus("게임 시작 중...");
        // SaveSystem을 초기화해야 게임 씬의 WorldStreamingManager가
        // SaveSystem.Instance.LoadChunk()를 호출할 수 있다.
        SaveSystem.Initialize(_saveSlot);
        SceneManager.LoadScene(_gameSceneName);
    }

    private void Update()
    {
        if (_generator == null) return;
        _ui.UpdateProgress(_generator.Progress, _generator.StatusText);
    }
}
