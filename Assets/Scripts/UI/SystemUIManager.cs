using UnityEngine;

// ==========================================
// 全局 UI 调度中枢
// ==========================================
public class SystemUIManager : MonoBehaviour
{
    public static SystemUIManager Instance; // UI 总管拥有单例特权

    [Header("玩家引用与挂载点")]
    public EldenRingMovement player;
    public Transform mainCanvas; 

    // 内部记录实例
    private GameObject currentCharacterPanel; 
    private QTEUIManager cachedQTEPanel; // 👈 【核心】：QTE 的永久缓存！

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (player == null || player.isDead || player.isResting) return;

        if (Input.GetKeyDown(KeyCode.C)) ToggleCharacterPanel();
    }

    // 1. 【按需加载 + 用完销毁】：适合笨重的人物属性面板
    public void ToggleCharacterPanel()
    {
        if (currentCharacterPanel == null)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/CharacterPanel");
            if (prefab != null)
            {
                currentCharacterPanel = Instantiate(prefab, mainCanvas);
                CharacterPanelUI panelScript = currentCharacterPanel.GetComponent<CharacterPanelUI>();
                if (panelScript != null) panelScript.Initialize(player);
                player.isUIOpen = true; 
            }
        }
        else
        {
            Destroy(currentCharacterPanel);
            player.isUIOpen = false; 
        }
    }

    // ==========================================
    // 2. 【懒加载 + 永久缓存】：适合极高频、要求 0 延迟的 QTE 战斗面板
    // ==========================================
    public void ShowQTE()
    {
        // 只有第一次放大招时，才会去硬盘读取！
        if (cachedQTEPanel == null)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/QTE_Panel");
            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, mainCanvas);
                cachedQTEPanel = obj.GetComponent<QTEUIManager>();
            }
            else
            {
                Debug.LogError("找不到 QTE 预制体！请确保它在 Resources/UI/QTE_Panel 目录下！");
                return;
            }
        }

        // 之后直接 0 延迟秒开！
        cachedQTEPanel.ShowQTE();
    }

    public void HideQTE(bool success)
    {
        if (cachedQTEPanel != null)
        {
            cachedQTEPanel.HideQTE(success); // HideQTE 内部是 SetActive(false)，绝不 Destroy！
        }
    }
}