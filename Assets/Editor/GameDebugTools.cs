using UnityEngine;
using UnityEditor; // 必须引入这个命名空间

public class GameDebugTools
{
    // 这行代码会在 Unity 顶部的菜单栏生成一个新选项
    [MenuItem("Tools/开发工具/一键清除所有本地存档")]
    public static void ClearAllSaveData()
    {
        // 彻底清空所有 PlayerPrefs 数据
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        Debug.LogWarning("⚠️ 警告：所有的本地存档数据（位置、等级、属性、武器）已被彻底清空！下次运行游戏将从零开始。");
    }
}