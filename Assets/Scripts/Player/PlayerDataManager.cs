using UnityEngine;
using System.IO;
using System.Collections.Generic;

// 全局唯一的玩家数据中心（单例模式）
public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("RPG 属性系统")]
    public int currentLevel = 1;
    public int currentXP = 0;
    public int statPoints = 0;
    public int currentGold = 0;

    [Header("=== 武器强化系统 ===")]
    public string weaponName = "狼的末路";
    public int weaponLevel = 0;
    public int maxWeaponLevel = 25;
    public float weaponBaseAttack = 40f;
    public float upgradeAttackBonus = 8f;

    [Header("基础加点属性")]
    public int statVigor = 10;       // 生命力（影响最大生命值）
    public int statEndurance = 10;   // 持久力（影响最大耐力）
    public int statStrength = 10;    // 力量（增加物理攻击力）
    public int statResistance = 10;  // 坚韧度（增加物理防御力）
    public int statSpirit = 10;      // 精神力（提高怒气获取效率）

    [Header("面板衍生属性 (自动计算)")]
    public float maxHealth;
    public float maxStamina;
    public float attackPowerBonus;
    public float defensePower;
    public float rageGainMultiplier;

    [Header("存档与复活系统")]
    public Vector3 respawnPosition;
    public Quaternion respawnRotation;

    private static string SavePath => Application.persistentDataPath + "/savegame.json";

    private void Awake()
    {
        Instance = this;
    }

    public static bool SaveFileExists()
    {
        return File.Exists(SavePath);
    }

    public static void DeleteSaveFile()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    // ==========================================
    // 面板数值计算
    // ==========================================
    public void RecalculateAttributes(EldenRingMovement player)
    {
        maxHealth = 100f + (statVigor * 20f);
        maxStamina = 100f + (statEndurance * 10f);
        
        float currentWeaponAttack = weaponBaseAttack + (weaponLevel * upgradeAttackBonus);
        attackPowerBonus = currentWeaponAttack + (statStrength * 3f);
        
        defensePower = statResistance * 2f;
        rageGainMultiplier = 1f + (statSpirit * 0.02f);

        // 更新UI最大值
        if (player != null)
        {
            if (player.healthSlider != null) player.healthSlider.maxValue = maxHealth;
            if (player.staminaSlider != null) player.staminaSlider.maxValue = maxStamina;
        }
    }

    // ==========================================
    // 硬盘读写系统 (JSON 文件存档)
    // ==========================================
    public void SaveGame(Vector3 currentRespawnPos, Quaternion currentRespawnRot)
    {
        respawnPosition = currentRespawnPos;
        respawnRotation = currentRespawnRot;

        SaveData data = new SaveData();
        data.currentLevel = currentLevel;
        data.currentXP = currentXP;
        data.statPoints = statPoints;
        data.currentGold = currentGold;
        data.weaponName = weaponName;
        data.weaponLevel = weaponLevel;
        data.statVigor = statVigor;
        data.statEndurance = statEndurance;
        data.statStrength = statStrength;
        data.statResistance = statResistance;
        data.statSpirit = statSpirit;
        data.respawnPosX = respawnPosition.x;
        data.respawnPosY = respawnPosition.y;
        data.respawnPosZ = respawnPosition.z;
        data.respawnRotY = respawnRotation.eulerAngles.y;

        data.activeRestPointNames = new List<string>();
        foreach (var rp in RestPoint.allActiveRestPoints)
        {
            if (rp != null)
                data.activeRestPointNames.Add(rp.restPointName);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"存档已保存至: {SavePath}");
    }

    public void LoadGame(EldenRingMovement player)
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            currentLevel = data.currentLevel;
            currentXP = data.currentXP;
            statPoints = data.statPoints;
            currentGold = data.currentGold;
            weaponName = data.weaponName;
            weaponLevel = data.weaponLevel;
            statVigor = data.statVigor;
            statEndurance = data.statEndurance;
            statStrength = data.statStrength;
            statResistance = data.statResistance;
            statSpirit = data.statSpirit;

            respawnPosition = new Vector3(data.respawnPosX, data.respawnPosY, data.respawnPosZ);
            respawnRotation = Quaternion.Euler(0, data.respawnRotY, 0);

            // 恢复已激活的休息点
            if (data.activeRestPointNames != null && data.activeRestPointNames.Count > 0)
            {
                RestPoint[] allRestPoints = FindObjectsOfType<RestPoint>(true);
                foreach (string name in data.activeRestPointNames)
                {
                    foreach (var rp in allRestPoints)
                    {
                        if (rp.restPointName == name)
                        {
                            rp.Activate();
                            break;
                        }
                    }
                }
            }

            // 强行把玩家传送到最后一次存档的赐福点
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = respawnPosition;
            player.transform.rotation = respawnRotation;
            if (cc != null) cc.enabled = true;

            Debug.Log("读取存档成功！已传送到最后一次休息的赐福点。");
        }
        else
        {
            // 没存过档，把出生点设为当前位置
            respawnPosition = player.transform.position;
            respawnRotation = player.transform.rotation;
        }

        RecalculateAttributes(player);
    }

    // ==========================================
    // 游戏经济与角色强化系统
    // ==========================================
    public bool UpgradeWeapon()
    {
        if (weaponLevel < maxWeaponLevel)
        {
            weaponLevel++;
            RecalculateAttributes(null); 
            SaveGame(respawnPosition, respawnRotation); 
            
            if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"武器强化成功！太刀 +{weaponLevel}");
            return true; 
        }
        return false; 
    }

    public void AddXP(int amount)
    {
        currentXP += amount;
        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"击败敌人，获得 {amount} 经验");
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"拾取掉落，获得 {amount} 金币");
    }

    public void RewardXP(int amount)
    {
        currentXP += amount;
        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"领取奖励，获得 {amount} 经验");
    }

    public void RewardGold(int amount)
    {
        currentGold += amount;
        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"领取奖励，获得 {amount} 金币");
    }

    public int GetXPRequirementForNextLevel()
    {
        return 500 + (currentLevel * currentLevel * 50);
    }

    public bool TryLevelUp(string statName, EldenRingMovement player)
    {
        int requiredXP = GetXPRequirementForNextLevel();
        if (currentXP >= requiredXP)
        {
            currentXP -= requiredXP;
            currentLevel++;

            switch (statName)
            {
                case "Vigor": statVigor++; break;
                case "Endurance": statEndurance++; break;
                case "Strength": statStrength++; break;
                case "Resistance": statResistance++; break;
                case "Spirit": statSpirit++; break;
            }

            RecalculateAttributes(player); 
            player.currentHealth = maxHealth; // 升级送回血
            if (player.healthSlider != null) player.healthSlider.value = player.currentHealth;

            SaveGame(respawnPosition, respawnRotation); 
            if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"升级成功！【{statName}】属性已提升");
            return true;
        }
        return false;
    }
}

[System.Serializable]
public class SaveData
{
    public int currentLevel = 1;
    public int currentXP = 0;
    public int statPoints = 0;
    public int currentGold = 0;
    public string weaponName = "狼的末路";
    public int weaponLevel = 0;
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