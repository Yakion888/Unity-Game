using System;
using System.Collections.Generic;

/// <summary>
/// 存档数据结构 —— 元数据 + 业务数据分离
/// </summary>
[Serializable]
public class SaveData
{
    // ══════════════════════════════════════════════════════
    // 元数据（主菜单 UI 展示用，不参与游戏逻辑）
    // ══════════════════════════════════════════════════════
    public int slotId;
    public string saveTime;     // DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
    public float playTimeSeconds;

    // ══════════════════════════════════════════════════════
    // 业务数据
    // ══════════════════════════════════════════════════════
    public int currentLevel = 1;
    public int currentXP = 0;
    public int statPoints = 0;
    public int currentGold = 0;
    public string weaponName = "狼的末路";
    public int weaponLevel = 0;
    public int equippedWeaponIndex = 0;
    public int statVigor = 10;
    public int statEndurance = 10;
    public int statStrength = 10;
    public int statResistance = 10;
    public int statSpirit = 10;
    public float respawnPosX;
    public float respawnPosY;
    public float respawnPosZ;
    public float respawnRotY;
    public List<string> activeRestPointNames;
}
