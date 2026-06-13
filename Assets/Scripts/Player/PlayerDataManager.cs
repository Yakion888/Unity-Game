using UnityEngine;

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

    private void Awake()
    {
        // 【架构修复】：防自杀单例模式
        Instance = this;
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
    // 硬盘读写系统 (彻底独立)
    // ==========================================
    public void SaveGame(Vector3 currentRespawnPos, Quaternion currentRespawnRot)
    {
        respawnPosition = currentRespawnPos;
        respawnRotation = currentRespawnRot;

        PlayerPrefs.SetInt("HasSavedGame", 1);
        PlayerPrefs.SetFloat("PlayerPosX", respawnPosition.x);
        PlayerPrefs.SetFloat("PlayerPosY", respawnPosition.y);
        PlayerPrefs.SetFloat("PlayerPosZ", respawnPosition.z);
        PlayerPrefs.SetFloat("PlayerRotY", respawnRotation.eulerAngles.y);

        PlayerPrefs.SetInt("PlayerLevel", currentLevel);
        PlayerPrefs.SetInt("PlayerXP", currentXP);
        PlayerPrefs.SetInt("PlayerGold", currentGold);
        PlayerPrefs.SetInt("Vigor", statVigor);
        PlayerPrefs.SetInt("Endurance", statEndurance);
        PlayerPrefs.SetInt("Strength", statStrength);
        PlayerPrefs.SetInt("Resistance", statResistance);
        PlayerPrefs.SetInt("Spirit", statSpirit);
        PlayerPrefs.SetInt("WeaponLevel", weaponLevel); 

        // 存储休息点进度
        string activePoints = "";
        foreach (var rp in RestPoint.allActiveRestPoints)
        {
            if (rp != null)
            {
                if (!string.IsNullOrEmpty(activePoints)) activePoints += ",";
                activePoints += rp.restPointName;
            }
        }
        PlayerPrefs.SetString("ActiveRestPoints", activePoints);
        PlayerPrefs.Save();
    }

    public void LoadGame(EldenRingMovement player)
    {
        if (PlayerPrefs.GetInt("HasSavedGame", 0) == 1)
        {
            float x = PlayerPrefs.GetFloat("PlayerPosX");
            float y = PlayerPrefs.GetFloat("PlayerPosY");
            float z = PlayerPrefs.GetFloat("PlayerPosZ");
            float rotY = PlayerPrefs.GetFloat("PlayerRotY");

            currentLevel = PlayerPrefs.GetInt("PlayerLevel", 1);
            currentXP = PlayerPrefs.GetInt("PlayerXP", 0);
            currentGold = PlayerPrefs.GetInt("PlayerGold", 0);
            statVigor = PlayerPrefs.GetInt("Vigor", 10);
            statEndurance = PlayerPrefs.GetInt("Endurance", 10);
            statStrength = PlayerPrefs.GetInt("Strength", 10);
            statResistance = PlayerPrefs.GetInt("Resistance", 10);
            statSpirit = PlayerPrefs.GetInt("Spirit", 10);
            weaponLevel = PlayerPrefs.GetInt("WeaponLevel", 0);

            // 恢复已激活的休息点
            string savedActivePoints = PlayerPrefs.GetString("ActiveRestPoints", "");
            if (!string.IsNullOrEmpty(savedActivePoints))
            {
                string[] names = savedActivePoints.Split(',');
                RestPoint[] allRestPoints = FindObjectsOfType<RestPoint>(true);
                foreach (string name in names)
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

            respawnPosition = new Vector3(x, y, z);
            respawnRotation = Quaternion.Euler(0, rotY, 0);

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

        // 不管有没有旧存档，启动时都重新计算一遍属性！
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