using UnityEngine;
using TMPro;

// 挂载在预制体根节点，它不知道自己是怎么被造出来的，只管显示数据！
public class CharacterPanelUI : MonoBehaviour
{
    private EldenRingMovement playerStats;

    [Header("文本组件引用")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI vigorText;
    public TextMeshProUGUI enduranceText;
    public TextMeshProUGUI strengthText;
    public TextMeshProUGUI resistanceText;
    public TextMeshProUGUI spiritText;
    public TextMeshProUGUI weaponText;    
    public TextMeshProUGUI goldText;

    // 接收生成器传来的玩家引用，并立即刷新数据
    public void Initialize(EldenRingMovement player)
    {
        playerStats = player;
        UpdateStatUI();
    }

    public void UpdateStatUI()
    {
        if (playerStats == null) return;

        int nextLevelXP = playerStats.GetXPRequirementForNextLevel();

        // 使用 TMP SetText(格式化模板, 参数...) 替代 string 插值：
        //   SetText 直写 TMP 内部 char[] 缓冲区 → 零堆分配
        //   而 $"{}" 会在托管堆上分配临时 string → 触发 GC

        levelText.SetText("等级: {0} \n经验: {1} / {2}",
            playerStats.currentLevel, playerStats.currentXP, nextLevelXP);

        vigorText.SetText("生命力: {0} \n   最大HP: {1}",
            playerStats.statVigor, playerStats.maxHealth);

        enduranceText.SetText("持久力: {0} \n   最大耐力: {1}",
            playerStats.statEndurance, playerStats.maxStamina);

        strengthText.SetText("力量: {0} \n   攻击力加成: +{1}",
            playerStats.statStrength, playerStats.attackPowerBonus);

        resistanceText.SetText("坚韧度: {0} \n   物理防御: {1}",
            playerStats.statResistance, playerStats.defensePower);

        spiritText.SetText("精神力: {0} \n   怒气获取倍率: {1:0.00}x",
            playerStats.statSpirit, playerStats.rageGainMultiplier);

        if (weaponText != null)
        {
            float currentWeaponAtk = playerStats.weaponBaseAttack + (playerStats.weaponLevel * playerStats.upgradeAttackBonus);
            // NOTE: TMP SetText 格式化重载只接受 float 参数，weaponName 是 string 无法隐式转换。
            // 此处用 string.Format 兜底 —— UpdateStatUI 仅面板打开/升级时触发，非逐帧调用，GC 可忽略。
            weaponText.text = string.Format("装备武器: {0} +{1} \n   武器攻击力: {2}",
                playerStats.weaponName, playerStats.weaponLevel, currentWeaponAtk);
        }

        if (goldText != null)
        {
            // 富文本标记保留在模板中，只需替换数字
            goldText.SetText("持有金币: <color=#FFD700>{0}</color> G",
                playerStats.currentGold);
        }
    }

    // ==========================================
    // 按钮点击事件
    // ==========================================
    public void OnClickUpgradeVigor() { if (playerStats.TryLevelUp("Vigor")) UpdateStatUI(); }
    public void OnClickUpgradeEndurance() { if (playerStats.TryLevelUp("Endurance")) UpdateStatUI(); }
    public void OnClickUpgradeStrength() { if (playerStats.TryLevelUp("Strength")) UpdateStatUI(); }
    public void OnClickUpgradeResistance() { if (playerStats.TryLevelUp("Resistance")) UpdateStatUI(); }
    public void OnClickUpgradeSpirit() { if (playerStats.TryLevelUp("Spirit")) UpdateStatUI(); }
    
    // 【新增】关闭按钮功能（如果你面板上有一个 X 按钮，可以绑这个方法）
    public void OnClickClose()
    {
        FindObjectOfType<SystemUIManager>().ToggleCharacterPanel();
    }
}