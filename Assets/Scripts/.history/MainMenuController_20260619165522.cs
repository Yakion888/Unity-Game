using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;

/// <summary>
/// 主菜单控制器 —— 多存档槽位版
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI 引用")]
    public Button btnContinue;
    public Button btnNewGame;
    public Button btnLoadGame;  
    public Button btnQuit;
    public Image fadeScreen;
    public TextMeshProUGUI txtContinue;

    [Header("UI 面板引用")]
    public SaveSlotPanel saveSlotPanel; //存档列表面板

    [Header("音效与过渡配置")]
    public string gameSceneName = "Level_01";
    public float fadeDuration = 1.5f;

    [Tooltip("拖入挂载了主菜单BGM的 AudioSource")]
    public AudioSource bgmSource;

    private bool isTransitioning = false;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fadeScreen != null)
        {
            fadeScreen.color = new Color(0, 0, 0, 0);
            fadeScreen.raycastTarget = false;
        }

        // ── 扫描所有存档，最新档给"继续游戏"用 ──
        var allSaves = SaveSlotManager.GetAllSaves();
        bool hasSave = allSaves.Count > 0;

        btnContinue.interactable = hasSave;

        if (hasSave && txtContinue != null)
        {
            var latest = allSaves[0];
            txtContinue.text = $"继续游戏\n槽位 {latest.slotId} | {latest.saveTime} | Lv.{latest.currentLevel}";
            txtContinue.color = Color.white;
        }
        else if (txtContinue != null)
        {
            txtContinue.text = "继续游戏\n（无存档）";
            txtContinue.color = new Color(1, 1, 1, 0.3f);
        }

        btnContinue.onClick.AddListener(OnContinueClick);
        btnNewGame.onClick.AddListener(OnNewGameClick);
        btnQuit.onClick.AddListener(OnQuitClick);
    }

    // ============================================================
    // 公开入口：供 SaveSlotPanel 调用
    // ============================================================

    /// <summary>
    /// 外部（SaveSlotPanel）设置好 PendingLoadSlotId 后调用此方法触发场景过渡。
    /// </summary>
    public void StartGameTransition()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToScene());
    }

    // ============================================================
    // 继续游戏 → 加载最新存档
    // ============================================================
    private void OnContinueClick()
    {
        int latestSlot = SaveSlotManager.FindLatestSlot();
        if (latestSlot <= 0) return;

        SaveSlotManager.PendingLoadSlotId = latestSlot;
        SaveSlotManager.PendingIsNewGame = false;
        StartGameTransition();
    }

    private void OnNewGameClick()
    {
        SaveSlotManager.PendingLoadSlotId = SaveSlotManager.FindFirstFreeSlot();
        SaveSlotManager.PendingIsNewGame = true;
        StartGameTransition();
    }

    private void OnLoadGameClick()
    {
        if (isTransitioning || saveSlotPanel == null) return;
        
        // 打开多存档列表面板
        saveSlotPanel.ShowPanel();
    }

    // ============================================================
    // 退出
    // ============================================================
    private void OnQuitClick()
    {
        if (isTransitioning) return;
        Debug.Log("游戏正在退出...");
        Application.Quit();
    }

    // ============================================================
    // 黑屏过渡
    // ============================================================
    private IEnumerator TransitionToScene()
    {
        isTransitioning = true;

        float startVolume = bgmSource != null ? bgmSource.volume : 0f;

        if (fadeScreen != null)
        {
            fadeScreen.raycastTarget = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                fadeScreen.color = new Color(0, 0, 0, alpha);

                if (bgmSource != null)
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);

                yield return null;
            }

            fadeScreen.color = new Color(0, 0, 0, 1);
            if (bgmSource != null) bgmSource.volume = 0f;
        }

        yield return new WaitForSeconds(0.5f);

        SceneManager.LoadScene(gameSceneName);
    }
}
