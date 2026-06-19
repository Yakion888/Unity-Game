using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 多存档槽位管理器
///
/// ═══════════════════════════════════════════════════════════
/// 【文件命名】 save_slot_1.json, save_slot_2.json, ...
/// 【跨场景通信】 PendingLoadSlotId：主菜单选中槽位后设此值，
///               游戏场景 PlayerDataManager.Awake 中读取并加载。
/// 【健壮性】 目录自动创建、JSON 损坏跳过、IO 异常捕获。
/// ═══════════════════════════════════════════════════════════
/// </summary>
public static class SaveSlotManager
{
    /// <summary>主菜单选中槽位后，场景加载前设置</summary>
    public static int PendingLoadSlotId = -1;

    /// <summary>是否为新游戏（true = CreateNewGame 后设；false = 读已有档）</summary>
    public static bool PendingIsNewGame = false;

    private const string FilePrefix = "save_slot_";
    private const string FileExtension = ".json";

    private static string SaveDir => Application.persistentDataPath;

    /// <summary>获取指定槽位的完整文件路径</summary>
    public static string GetSlotPath(int slotId)
    {
        EnsureDirectoryExists();
        return Path.Combine(SaveDir, $"{FilePrefix}{slotId}{FileExtension}");
    }

    // ============================================================
    // 存档列表
    // ============================================================

    /// <summary>
    /// 扫描 persistentDataPath 下所有 save_slot_*.json，
    /// 返回按存档时间倒序排列的 SaveData 列表。
    /// JSON 损坏的文件自动跳过并记录日志。
    /// </summary>
    public static List<SaveData> GetAllSaves()
    {
        var saves = new List<SaveData>();
        EnsureDirectoryExists();

        string[] files;
        try
        {
            files = Directory.GetFiles(SaveDir, $"{FilePrefix}*{FileExtension}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] 扫描存档目录失败：{ex.Message}");
            return saves;
        }

        foreach (var filePath in files)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data != null)
                    saves.Add(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSlotManager] 跳过损坏的存档文件 {Path.GetFileName(filePath)}：{ex.Message}");
            }
        }

        // 按时间倒序：最新的在最前
        saves.Sort((a, b) => string.Compare(b.saveTime, a.saveTime, StringComparison.Ordinal));
        return saves;
    }

    // ============================================================
    // 单槽位操作
    // ============================================================

    /// <summary>将 SaveData 写入指定槽位，自动填充元数据</summary>
    public static void SaveGame(int slotId, SaveData data)
    {
        if (data == null) return;
        EnsureDirectoryExists();

        data.slotId = slotId;
        data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string path = GetSlotPath(slotId);
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            //Debug.Log($"[SaveSlotManager] 存档已保存 → 槽位 {slotId}：{path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] 保存槽位 {slotId} 失败：{ex.Message}");
        }
    }

    /// <summary>从指定槽位加载 SaveData，失败或不存在返回 null</summary>
    public static SaveData LoadGame(int slotId)
    {
        string path = GetSlotPath(slotId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSlotManager] 槽位 {slotId} 的存档文件不存在：{path}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                Debug.LogWarning($"[SaveSlotManager] 槽位 {slotId} JSON 反序列化返回 null");
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] 加载槽位 {slotId} 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>删除指定槽位的存档文件</summary>
    public static void DeleteSave(int slotId)
    {
        string path = GetSlotPath(slotId);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveSlotManager] 已删除槽位 {slotId} 的存档");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] 删除槽位 {slotId} 失败：{ex.Message}");
        }
    }

    // ============================================================
    // 便捷方法
    // ============================================================

    /// <summary>查找存档时间最新的槽位 ID，无存档返回 -1</summary>
    public static int FindLatestSlot()
    {
        var saves = GetAllSaves();
        return saves.Count > 0 ? saves[0].slotId : -1;
    }

    /// <summary>查找第一个空闲的槽位 ID（从 1 开始递增）</summary>
    public static int FindFirstFreeSlot()
    {
        var saves = GetAllSaves();
        var usedSlots = new HashSet<int>();
        foreach (var s in saves)
            usedSlots.Add(s.slotId);

        int slot = 1;
        while (usedSlots.Contains(slot))
            slot++;
        return slot;
    }

    /// <summary>检查指定槽位是否存在存档</summary>
    public static bool SlotExists(int slotId)
    {
        return File.Exists(GetSlotPath(slotId));
    }

    // ============================================================
    // 内部
    // ============================================================

    private static void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] 创建存档目录失败：{ex.Message}");
        }
    }
}
