using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro; 

public class MainMenuController : MonoBehaviour
{
    [Header("UI 引用")]
    public Button btnContinue;
    public Button btnNewGame;
    public Button btnQuit;
    public Image fadeScreen;
    public TextMeshProUGUI txtContinue; 

    [Header("音效与过渡配置")]
    public string gameSceneName = "Level_01"; 
    public float fadeDuration = 1.5f;
    
    // 👇【新增】：引用你的背景音乐播放器
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

        bool hasSave = PlayerPrefs.GetInt("HasSavedGame", 0) == 1;
        btnContinue.interactable = hasSave;
        
        if (!hasSave && txtContinue != null) 
        {
            txtContinue.color = new Color(1, 1, 1, 0.3f);
        }

        btnContinue.onClick.AddListener(OnContinueClick);
        btnNewGame.onClick.AddListener(OnNewGameClick);
        btnQuit.onClick.AddListener(OnQuitClick);
    }

    private void OnContinueClick()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToScene(false));
    }

    private void OnNewGameClick()
    {
        if (isTransitioning) return;
        
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        StartCoroutine(TransitionToScene(true));
    }

    private void OnQuitClick()
    {
        if (isTransitioning) return;
        Debug.Log("游戏正在退出...");
        Application.Quit();
    }

    // 核心黑屏与音乐淡出过渡协程
    private IEnumerator TransitionToScene(bool isNewGame)
    {
        isTransitioning = true;

        // 记录渐变开始前，音乐的初始音量（比如是 0.6）
        float startVolume = bgmSource != null ? bgmSource.volume : 0f;

        if (fadeScreen != null)
        {
            fadeScreen.raycastTarget = true; 
            
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                
                // 1. 屏幕逐渐变黑
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                fadeScreen.color = new Color(0, 0, 0, alpha);
                
                // 2. 👇【新增】：音乐音量逐渐变小到 0
                if (bgmSource != null)
                {
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                }

                yield return null;
            }
            
            // 确保最终状态
            fadeScreen.color = new Color(0, 0, 0, 1);
            if (bgmSource != null) bgmSource.volume = 0f;
        }

        // 让黑夜稍微沉淀 0.5 秒，万籁俱寂，给玩家极强的心理压迫感
        yield return new WaitForSeconds(0.5f);

        // 正式加载游戏场景
        SceneManager.LoadScene(gameSceneName);
    }
}