using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// 로딩 Scene 진행자
/// <summary> 새 게임 -> WorldPreGenerator -> 게임 Scene 전환 </summary>
public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private string              _gameSceneName = "GameScene";
    [SerializeField] private WorldPreGenerator   _generator;
    [SerializeField] private LoadingUIController _ui;
    [SerializeField] private int                 _saveslot = 0;

    private void Start()
    {
        var currentContext = LoadingContext.Current;
        if (currentContext != null && currentContext.isNewGame)
            _ = StartNewGame(currentContext.saveName, currentContext.slot);
        else
            _ = LoadExistingGame(currentContext?.slot ?? _saveslot);
    }

    private async Task StartNewGame(string saveName, int slot)
    {
        _ui.SetStatus("새 게임 준비 중...");
        float seed = UnityEngine.Random.Range(0f, 9999f);
        _generator.OnComplete += OnGenerationDone;
        await _generator.GenerateAll(slot, seed);
    }

    private async Task LoadExistingGame(int slot)
    {
        _ui.SetStatus("저장 파일 확인 중...");
        if (!SaveSystem.Exists(slot))
        {
            Debug.LogWarning($"[Loading] 슬롯 {slot} 저장 파일 없음");
            return;
        }
        await Task.Yield();
        OnGenerationDone();
    }

    private void OnGenerationDone()
    {
        _ui.SetStatus("게임 시작 중...");
        // SaveSystem 을 초기화 해야 게임 Scene의 WorldStreamingManager가
        // SaveSystem.Instance.LoadChunk() 를 호출할 수 있다
        SaveSystem.Initialize(_saveslot);
        SceneManager.LoadScene(_gameSceneName);
    }

    private void Update()
    {
        if (_generator == null) return;
        _ui.UpdateProgress(_generator.Progress, _generator.StatusText);
    }
}