using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 存档选择面板控制器
public class SaveSlotPanel : MonoBehaviour
{
    [Header("UI 挂载与预制体")]
    public Transform contentParent;        // 拖入 ScrollView 的 Content
    public GameObject slotButtonPrefab;    // 拖入 SlotButton 预制体

    [Header("配置")]
    public int maxSlotCount = 5;           // 默认显示 5 个槽位
    public Button btnBack;                 // 返回按钮

    [Header("引用")]
    public MainMenuController menuController; // 主菜单管家

    private void Awake()
    {
        if (btnBack != null)
        {
            btnBack.onClick.AddListener(HidePanel);
        }
    }

    private void OnEnable()
    {
        // 每次面板激活时，重新生成列表
        BuildSlotList();
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // 核心：构建 1~5 号槽位的 UI 列表
    private void BuildSlotList()
    {
        // 1. 清空旧列表
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 从底层获取所有已存在的存档
        List<SaveData> allSaves = SaveSlotManager.GetAllSaves();

        // 3. 将列表转换为字典，方便按 SlotID 查找 (避免 O(N^2) 查找)
        Dictionary<int, SaveData> saveDict = new Dictionary<int, SaveData>();
        foreach (var save in allSaves)
        {
            saveDict[save.slotId] = save;
        }

        // 4. 固定生成 maxSlotCount 个槽位 (1 到 5)
        for (int i = 1; i <= maxSlotCount; i++)
        {
            GameObject slotObj = Instantiate(slotButtonPrefab, contentParent);
            SaveSlotUIItem slotItem = slotObj.GetComponent<SaveSlotUIItem>();

            if (slotItem != null)
            {
                // 如果字典里有这个 ID 的存档，传数据；否则传 null 代表空槽位
                SaveData slotData = saveDict.ContainsKey(i) ? saveDict[i] : null;
                
                // 注入回调函数
                slotItem.Setup(i, slotData, OnSlotClicked, OnDeleteClicked);
            }
        }
    }

    // ==========================================
    // 回调事件处理
    // ==========================================

    private void OnSlotClicked(int slotId, bool isNewGame)
    {
        // 1. 严格遵守文档规范：设置跨场景通信的全局变量
        SaveSlotManager.PendingLoadSlotId = slotId;
        SaveSlotManager.PendingIsNewGame = isNewGame;

        //Debug.Log($"准备加载槽位 [{slotId}]，是否为新游戏: {isNewGame}");

        // 2. 关闭面板，呼叫主菜单播放黑屏动画并跳转场景
        HidePanel();
        if (menuController != null)
        {
            menuController.StartGameTransition();
        }
        else
        {
            Debug.LogError("[SaveSlotPanel] menuController 未拖入！请在 Inspector 中将 MainMenuController 拖入 SaveSlotPanel 的 menuController 槽位。");
        }
    }

    private void OnDeleteClicked(int slotId)
    {
        //Debug.Log($"玩家要求删除槽位 [{slotId}]");
        SaveSlotManager.DeleteSave(slotId);
        
        // 删档后立刻刷新 UI
        BuildSlotList();
    }
}