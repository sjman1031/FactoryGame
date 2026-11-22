using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>새 게임 → WorldPreGenerator → 게임 씬 전환. 실패하면 이유와 재시도 방법을 보여 준다.</summary>
public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private string              _gameSceneName = "MainScene";
    [SerializeField] private WorldPreGenerator   _generator;
    [SerializeField] private LoadingUIController _ui;
    [SerializeField] private int                 _saveSlot = 0;

    // LoadingContext에서 받은 실제 슬롯을 보관.
    // _saveSlot은 컨텍스트가 없을 때의 Inspector 폴백값이므로
    // OnGenerationDone()에서 _saveSlot을 직접 쓰면 슬롯 1·2가 항상 0으로 초기화되는 버그 발생.
    private int    _activeSlot;
    private string _saveName;
    private bool   _failed;
    private bool   _busy;

    private void Start()
    {
        if (_generator == null || _ui == null)
        {
            Debug.LogError("[LoadingSceneController] _generator 또는 _ui가 연결되지 않았습니다.", this);
            return;
        }

        // 씬 이름이 틀리거나 Build Settings에 없으면 맵을 다 만든 뒤에야 LoadScene에서 실패한다 — 시작 전에 알린다
        // 설정 오류라 재시도(= 새 게임으로 덮어쓰기)로는 고쳐지지 않는다. Fail()을 쓰면 세이브만 날아가므로 알리기만 한다.
        if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            string message = $"게임 씬 '{_gameSceneName}'을(를) 불러올 수 없습니다.\n" +
                             "씬 이름과 File › Build Settings의 씬 목록을 확인하세요.";
            Debug.LogError($"[LoadingSceneController] {message}", this);
            _ui.SetStatus(message);
            return;
        }

        var ctx = LoadingContext.Current;
        _activeSlot = ctx?.slot ?? _saveSlot;
        _saveName   = ctx?.saveName;

        // 메뉴 없이 로딩 씬을 바로 실행해 컨텍스트가 없으면, 저장 파일이 없을 때 새 게임으로 시작한다.
        bool isNewGame = ctx?.isNewGame ?? !SaveSystem.Exists(_activeSlot);

        // `_ = Task`는 예외를 삼킨다 — Forget이 콘솔에 남긴다
        if (isNewGame) StartNewGame().Forget(this);
        else           LoadExistingGame().Forget(this);
    }

    private async Task StartNewGame()
    {
        _busy = true; _failed = false;
        _ui.HideError();
        _ui.SetStatus("새 게임 준비 중...");
        float seed = Random.Range(0f, 9999f);

        _generator.OnCompleted -= OnGenerationDone;
        _generator.OnFailed    -= OnGenerationFailed;
        _generator.OnCompleted += OnGenerationDone;
        _generator.OnFailed    += OnGenerationFailed;
        await _generator.GenerateAll(_activeSlot, seed);
        _busy = false;
    }

    private async Task LoadExistingGame()
    {
        _ui.SetStatus("저장 파일 확인 중...");
        if (!SaveSystem.Exists(_activeSlot))
        {
            Fail($"슬롯 {_activeSlot}에 저장 파일이 없습니다.");
            return;
        }
        await Task.Yield();
        if (this == null) return;   // 대기 중 씬이 바뀌었으면 중단
        OnGenerationDone();
    }

    private void OnGenerationDone()
    {
        _ui.SetStatus("게임 시작 중...");
        // SaveSystem을 초기화해야 게임 씬의 WorldStreamingManager가
        // SaveSystem.Instance.LoadChunk()를 호출할 수 있다.
        // 열지 못하면 게임 씬으로 넘어가지 않는다 — 지형 없는 빈 공간에 떨어지지 않게.
        if (!SaveSystem.Initialize(_activeSlot))
        {
            Fail($"저장 파일을 열 수 없습니다.\n{SaveSystem.LastError}");
            return;
        }
        SceneManager.LoadScene(_gameSceneName);
    }

    private void OnGenerationFailed(string reason) => Fail($"맵 생성에 실패했습니다.\n{reason}");

    private void Fail(string message)
    {
        _failed = true;
        _busy   = false;
        Debug.LogError($"[LoadingSceneController] {message}", this);
        _ui.ShowError(message, RetryAsNewGame);
    }

    /// <summary>이 슬롯을 새 게임으로 덮어쓴다. 기존 world.bin은 새 생성이 끝까지 성공해야 교체된다.</summary>
    private void RetryAsNewGame()
    {
        if (_busy) return;
        LoadingContext.SetNewGame(_activeSlot, _saveName);
        StartNewGame().Forget(this);
    }

    private void Update()
    {
        if (_generator == null || _ui == null) return;

        if (_failed)
        {
            // 재시도 버튼을 연결하지 않았을 때의 대체 입력
            if (!_ui.HasRetryButton && Input.GetKeyDown(KeyCode.Return)) RetryAsNewGame();
            return;
        }
        if (_busy) _ui.UpdateProgress(_generator.Progress, _generator.StatusText);
    }
}
