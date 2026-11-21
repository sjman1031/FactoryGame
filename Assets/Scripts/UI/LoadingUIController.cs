using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>로딩 씬 UI — 진행률 바 + 상태 텍스트 + 실패 시 "새 게임으로 덮어쓰기" 버튼.</summary>
public class LoadingUIController : MonoBehaviour
{
    [SerializeField] private Slider   _progressBar;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _percentText;

    [Header("실패 시 (선택) — 연결하지 않으면 Enter 키 안내로 대신한다")]
    [SerializeField] private GameObject _errorPanel;
    [SerializeField] private TMP_Text   _errorText;
    [SerializeField] private Button     _retryButton;

    public bool HasRetryButton => _retryButton != null;

    private void Awake()
    {
        if (_errorPanel != null) _errorPanel.SetActive(false);
    }

    public void UpdateProgress(float progress, string status)
    {
        if (_progressBar != null) _progressBar.value = progress;
        if (_percentText  != null) _percentText.text  = $"{Mathf.RoundToInt(progress * 100f)}%";
        if (_statusText   != null) _statusText.text   = status;
    }

    public void SetStatus(string status)
    {
        if (_statusText != null) _statusText.text = status;
    }

    /// <summary>실패 이유와 재시도 버튼을 보여 준다. 버튼이 없으면 상태 텍스트에 안내를 붙인다.</summary>
    public void ShowError(string message, Action onRetry)
    {
        if (_errorPanel != null) _errorPanel.SetActive(true);
        if (_errorText  != null) _errorText.text = message;

        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveAllListeners();
            _retryButton.onClick.AddListener(() => onRetry?.Invoke());
            SetStatus(message);
        }
        else
        {
            SetStatus($"{message}\nEnter 키를 누르면 이 슬롯에 새 게임을 만듭니다.");
        }
    }

    public void HideError()
    {
        if (_errorPanel != null) _errorPanel.SetActive(false);
    }
}
