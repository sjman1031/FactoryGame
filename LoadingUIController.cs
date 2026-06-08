using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>로딩 씬 UI — 진행률 바 + 상태 텍스트.</summary>
public class LoadingUIController : MonoBehaviour
{
    [SerializeField] private Slider   _progressBar;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _percentText;

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
}
