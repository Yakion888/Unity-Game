using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    /// <summary>设置菜单是否打开（供其他脚本检查，避免抢鼠标）</summary>
    public static bool IsOpen { get; private set; }

    [Header("音量")]
    public Slider volumeSlider;
    public TextMeshProUGUI volumeLabel;

    [Header("画质")]
    public TMP_Dropdown qualityDropdown;

    [Header("按钮")]
    public Button btnResume;
    public Button btnMainMenu;
    public Button btnQuit;

    [Header("场景名")]
    public string mainMenuSceneName = "MainMenu";

    [Header("面板")]
    [Tooltip("拖入设置菜单的视觉面板子节点")]
    public GameObject panelContent;

    private Image _rootImage; // 根节点自带的背景图（Unity Panel 默认生成）

    private void Awake()
    {
        // ── 根节点自带的半透明背景 Image 是万恶之源 ──
        //     它会永远挡在屏幕上（因为根节点必须活跃），
        //     并且 raycastTarget=true 拦截所有鼠标点击。
        _rootImage = GetComponent<Image>();
        if (_rootImage != null)
        {
            _rootImage.raycastTarget = false; // 不再拦截点击
            _rootImage.color = Color.clear;   // 不再显示半透明框
        }

        // ── 强制找到视觉面板 ──
        if (panelContent == null)
        {
            Transform t = transform.Find("Panel");
            if (t != null) panelContent = t.gameObject;
        }
        if (panelContent == null)
        {
            panelContent = gameObject;
        }

        // ── 无论 Inspector 里怎么勾，启动时强制隐藏 ──
        panelContent.SetActive(false);
    }

    private void Start()
    {
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
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.value = savedQuality;
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        if (btnResume != null)   btnResume.onClick.AddListener(Hide);
        if (btnMainMenu != null) btnMainMenu.onClick.AddListener(GoToMainMenu);
        if (btnQuit != null)     btnQuit.onClick.AddListener(QuitGame);
    }

    // ============================================================
    // 显隐
    // ============================================================

    public void Show()
    {
        IsOpen = true;
        if (_rootImage != null) _rootImage.color = new Color(0, 0, 0, 0.5f);
        panelContent.SetActive(true);
        panelContent.transform.SetAsLastSibling();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        IsOpen = false;
        if (_rootImage != null) _rootImage.color = Color.clear;
        panelContent.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (panelContent.activeSelf)
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
    // 返回主菜单
    // ============================================================

    private void GoToMainMenu()
    {
        Time.timeScale = 1f; // 恢复时间，否则主菜单也暂停
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
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
