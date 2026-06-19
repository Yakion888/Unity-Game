using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 存档选择面板 —— 多槽位列表 UI
///
/// ═══════════════════════════════════════════════════════════
/// 【使用方法】
///   1. 在主菜单 Canvas 下创建一个 Panel，挂载此脚本
///   2. 拖入 slotButtonPrefab（一个带 Button + TMP_Text 的预制体）
///   3. 拖入 contentParent（ScrollView 的 Content Transform）
///   4. 拖入 MainMenuController 引用（用于触发场景过渡）
///   5. 设定 maxSlotCount（默认 5）
///
/// 【展示逻辑】
///   已占用槽位 → 显示槽位号、等级、时间戳、游玩时长，点击进入
///   空闲槽位   → 显示"空槽位 — 新游戏"，点击创建新档
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class SaveSlotPanel : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("单个槽位按钮的预制体（需有 Button + TMP_Text 子物体）")]
    public GameObject slotButtonPrefab;

    [Tooltip("按钮的父节点（ScrollView Content）")]
    public Transform contentParent;

    [Tooltip("主菜单控制器，用于触发场景过渡")]
    public MainMenuController menuController;

    [Header("设置")]
    [Tooltip("最多显示多少个槽位")]
    public int maxSlotCount = 5;

    [Header("返回按钮（可选）")]
    public Button btnBack;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Start()
    {
        if (btnBack != null)
            btnBack.onClick.AddListener(HidePanel);

        BuildSlotList();
    }

    private void OnEnable()
    {
        // 每次面板打开时刷新列表
        BuildSlotList();
    }

    // ============================================================
    // 构建槽位列表
    // ============================================================

    private void BuildSlotList()
    {
        // 清除旧按钮
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        var allSaves = SaveSlotManager.GetAllSaves();

        // 建立槽位ID → 存档数据的映射
        var saveMap = new Dictionary<int, SaveData>();
        foreach (var s in allSaves)
            saveMap[s.slotId] = s;

        for (int slotId = 1; slotId <= maxSlotCount; slotId++)
        {
            GameObject btnObj = Instantiate(slotButtonPrefab, contentParent);
            Button btn = btnObj.GetComponent<Button>();
            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();

            if (saveMap.ContainsKey(slotId))
            {
                // ── 已占用槽位 ──
                var data = saveMap[slotId];
                if (label != null)
                {
                    string playTimeStr = FormatPlayTime(data.playTimeSeconds);
                    label.text = $"槽位 {slotId}  |  {data.weaponName}  |  Lv.{data.currentLevel}\n" +
                                 $"{data.saveTime}  |  游玩 {playTimeStr}";
                }

                int capturedSlot = slotId;
                btn.onClick.AddListener(() => OnSlotClicked(capturedSlot, isNew: false));
            }
            else
            {
                // ── 空闲槽位 ──
                if (label != null)
                    label.text = $"槽位 {slotId}\n（空 — 点击创建新游戏）";

                int capturedSlot = slotId;
                btn.onClick.AddListener(() => OnSlotClicked(capturedSlot, isNew: true));
            }
        }
    }

    // ============================================================
    // 槽位点击
    // ============================================================

    private void OnSlotClicked(int slotId, bool isNew)
    {
        SaveSlotManager.PendingLoadSlotId = slotId;
        SaveSlotManager.PendingIsNewGame = isNew;

        if (menuController != null)
            menuController.StartGameTransition();
        else
            Debug.LogError("[SaveSlotPanel] menuController 未拖入！");
    }

    // ============================================================
    // 面板显隐
    // ============================================================

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        BuildSlotList();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 工具
    // ============================================================

    private static string FormatPlayTime(float seconds)
    {
        if (seconds < 60f) return $"{seconds:F0}秒";
        if (seconds < 3600f) return $"{seconds / 60f:F0}分";
        return $"{(int)(seconds / 3600f)}时{(int)(seconds % 3600f / 60f)}分";
    }
}
