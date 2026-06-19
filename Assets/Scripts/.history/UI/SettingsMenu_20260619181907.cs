using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏内设置菜单 —— 音量 / 画质 / 退出
///
/// ═══════════════════════════════════════════════════════════
/// 【Application.Quit() 行为】
///   Editor 中：只打印 "游戏正在退出..." 到 Console，不会真关
///   打包后 (.exe)：正常关闭进程，释放所有资源
///   移动端：被系统挂起（iOS/Android 规范不允许主动退出）
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("音量")]
    public Slider volumeSlider;
    public TextMeshProUGUI volumeLabel; // 显示 "100%"

    [Header("画质")]
    public TMP_Dropdown qualityDropdown; // Low / Medium / High

    [Header("按钮")]
    public Button btnResume;  // 返回游戏
    public Button btnQuit;    // 退出到桌面

    private void Start()
    {
        // ── 从已有设置初始化 UI ──
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedVolume;
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            UpdateVolumeLabel(savedVolume);
        }

        int savedQuality = QualitySettings.GetQualityLevel();
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(
                QualitySettings.names));
            qualityDropdown.value = savedQuality;
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        if (btnResume != null) btnResume.onClick.AddListener(Hide);
        if (btnQuit != null)   btnQuit.onClick.AddListener(QuitGame);

        // 默认隐藏
        gameObject.SetActive(false);
    }

    // ============================================================
    // 面板显隐
    // ============================================================

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Esc 键切换</summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameObject.activeSelf)
                Hide();
            else
                Show();
        }
    }

    // ============================================================
    // 音量
    // ============================================================

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeLabel != null)
            volumeLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    // ============================================================
    // 画质
    // ============================================================

    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
    }

    // ============================================================
    // 退出
    // ============================================================

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
