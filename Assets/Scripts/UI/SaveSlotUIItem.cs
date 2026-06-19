using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// 挂载在单个存档槽位预制体上的 UI 组件
public class SaveSlotUIItem : MonoBehaviour
{
    [Header("UI 组件")]
    public Button mainButton;           // 点击读取/新建档的按钮
    public Button deleteButton;         // 删除按钮 (空档位时隐藏)
    public TextMeshProUGUI infoText;    // 显示信息的文字

    private int currentSlotId;
    private bool isNewGame;

    // 委托回调，把点击事件上报给 SaveSlotPanel
    private Action<int, bool> onSlotClickCallback;
    private Action<int> onDeleteClickCallback;

    /// <summary>
    /// 初始化槽位 UI
    /// </summary>
    /// <param name="slotId">槽位编号 (1~5)</param>
    /// <param name="data">存档数据 (如果是空档则为 null)</param>
    public void Setup(int slotId, SaveData data, Action<int, bool> onClick, Action<int> onDelete)
    {
        currentSlotId = slotId;
        onSlotClickCallback = onClick;
        onDeleteClickCallback = onDelete;

        // 清理并重新绑定按钮事件
        mainButton.onClick.RemoveAllListeners();
        mainButton.onClick.AddListener(OnMainButtonClicked);

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }

        // ==========================================
        // 根据是否有数据，呈现不同的 UI 状态
        // ==========================================
        if (data != null)
        {
            // 有存档：显示元数据，显示删除按钮
            isNewGame = false;
            if (deleteButton != null) deleteButton.gameObject.SetActive(true);

            // 格式化时长显示 (把秒转成 时:分)
            TimeSpan time = TimeSpan.FromSeconds(data.playTimeSeconds);
            string playTimeStr = $"{(int)time.TotalHours}h {time.Minutes}m";

            // 按照文档规范的模板渲染文字
            if (infoText != null)
            {
                infoText.text = $"槽位 {slotId} | {data.weaponName} | Lv.{data.currentLevel}\n" +
                                $"<size=80%><color=#A0A0A0>{data.saveTime} | 游玩时长: {playTimeStr}</color></size>";
            }
        }
        else
        {
            // 空存档：显示"新建存档"，隐藏删除按钮
            isNewGame = true;
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);

            if (infoText != null)
            {
                infoText.text = $"槽位 {slotId} | <color=#FFD700> [ 空闲槽位 - 点击新建 ] </color>";
            }
        }
    }

    private void OnMainButtonClicked()
    {
        // 告诉总管：我被点了！
        onSlotClickCallback?.Invoke(currentSlotId, isNewGame);
    }

    private void OnDeleteButtonClicked()
    {
        // 告诉总管：我要删档！
        onDeleteClickCallback?.Invoke(currentSlotId);
    }
}