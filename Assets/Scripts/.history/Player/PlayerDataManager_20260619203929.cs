using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局唯一的玩家数据中心（单例模式）—— 多存档槽位版
/// </summary>
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

    /// <summary>当前活跃的存档槽位 ID（-1 = 未关联任何槽位）</summary>
    public int ActiveSlotId { get; private set; } = -1;

    /// <summary>本次是否为新建存档（新游戏不传送，用场景出生点）</summary>
    private bool _isNewGame;

    /// <summary>读档时从 JSON 中恢复的休息点名称列表，供 ApplySaveDataToScene 使用</summary>
    private List<string> _loadedRestPointNames;

    /// <summary>本次游戏累计游玩时间（秒）</summary>
    private float _playTimeAccumulated;

    private void Awake()
    {
        Instance = this;

        // ── 从主菜单传入的待加载槽位 ──
        int slotToLoad = SaveSlotManager.PendingLoadSlotId;
        if (slotToLoad > 0)
        {
            if (SaveSlotManager.PendingIsNewGame)
                CreateNewGameFromSlot(slotToLoad);
            else
                LoadGameFromSlotManaged(slotToLoad);

            SaveSlotManager.PendingLoadSlotId = -1;
            SaveSlotManager.PendingIsNewGame = false;
        }
        else
        {
            // Editor 直接运行（跳过主菜单）→ 自动绑定槽位 1
            ActiveSlotId = 1;
            //Debug.Log("[PlayerDataManager] Editor 直接运行，自动绑定槽位 1");
        }
    }

    private void Update()
    {
        _playTimeAccumulated += Time.unscaledDeltaTime;
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

        if (player != null)
        {
            if (player.healthSlider != null) player.healthSlider.maxValue = maxHealth;
            if (player.staminaSlider != null) player.staminaSlider.maxValue = maxStamina;
        }
    }

    // ══════════════════════════════════════════════════════
    // 多存档槽位 保存 / 加载
    // ══════════════════════════════════════════════════════

    /// <summary>在赐福点休息时调用：覆盖当前槽位的 JSON 文件</summary>
    public void SaveGame(Vector3 currentRespawnPos, Quaternion currentRespawnRot)
    {
        if (ActiveSlotId <= 0)
        {
            Debug.LogWarning("[PlayerDataManager] ActiveSlotId 无效，自动使用槽位 1。");
            ActiveSlotId = 1;
        }

        respawnPosition = currentRespawnPos;
        respawnRotation = currentRespawnRot;

        SaveData data = CreateSaveDataFromMemory();
        SaveSlotManager.SaveGame(ActiveSlotId, data);
    }

    /// <summary>
    /// 从指定槽位加载存档数据，并补完场景实例（玩家传送、休息点恢复）。
    /// 供 Awake 中自动调用。
    /// </summary>
    private void LoadGameFromSlotManaged(int slotId)
    {
        SaveData data = SaveSlotManager.LoadGame(slotId);
        if (data == null) return;

        ApplySaveDataToMemory(data);
        ActiveSlotId = slotId;
        _playTimeAccumulated = data.playTimeSeconds;

        //Debug.Log($"[PlayerDataManager] 从槽位 {slotId} 读取存档成功！时间戳：{data.saveTime}");
    }

    /// <summary>加载存档后恢复场景状态（传送到赐福点、激活休息点）</summary>
    public void ApplySaveDataToScene(EldenRingMovement player)
    {
        // 恢复已激活的休息点（使用从 JSON 加载的列表，而非当前场景的初始状态）
        if (_loadedRestPointNames != null && _loadedRestPointNames.Count > 0)
        {
            RestPoint[] allRestPoints = FindObjectsOfType<RestPoint>(true);
            foreach (string name in _loadedRestPointNames)
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

        // ── 传送 ──
        if (_isNewGame || respawnPosition.sqrMagnitude < 0.01f)
        {
            // 新游戏 / 未初始化：以场景出生点为准，写入 respawnPosition 供后续使用
            respawnPosition = player.transform.position;
            respawnRotation = player.transform.rotation;
            _isNewGame = false;
        }
        else
        {
            // 读档：传送到存档中的赐福点
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = respawnPosition;
            player.transform.rotation = respawnRotation;
            if (cc != null) cc.enabled = true;
        }

        RecalculateAttributes(player);
    }

    /// <summary>创建全新存档（从主菜单点"新游戏"）</summary>
    private void CreateNewGameFromSlot(int slotId)
    {
        ActiveSlotId = slotId;
        _isNewGame = true;
        _playTimeAccumulated = 0f;

        // 写入初始空档（respawnPos 暂时为 0,0,0，等第一次赐福休息时覆盖）
        SaveData data = CreateSaveDataFromMemory();
        SaveSlotManager.SaveGame(slotId, data);

        //Debug.Log($"[PlayerDataManager] 新游戏 → 槽位 {slotId} 已初始化");
    }

    // ==========================================
    // 数据打包 / 解包
    // ==========================================

    /// <summary>将当前内存数值打包为 SaveData</summary>
    private SaveData CreateSaveDataFromMemory()
    {
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
        data.playTimeSeconds = _playTimeAccumulated;

        data.activeRestPointNames = new List<string>();
        foreach (var rp in RestPoint.allActiveRestPoints)
        {
            if (rp != null)
                data.activeRestPointNames.Add(rp.restPointName);
        }

        return data;
    }

    /// <summary>将 SaveData 解包回内存</summary>
    private void ApplySaveDataToMemory(SaveData data)
    {
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
        _loadedRestPointNames = data.activeRestPointNames;
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
            player.currentHealth = maxHealth;
            if (player.healthSlider != null) player.healthSlider.value = player.currentHealth;

            SaveGame(respawnPosition, respawnRotation);
            if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage($"升级成功！【{statName}】属性已提升");
            return true;
        }
        return false;
    }
}
