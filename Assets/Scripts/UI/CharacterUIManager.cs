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

        levelText.text = $"等级: {playerStats.currentLevel} \n经验: {playerStats.currentXP} / {nextLevelXP}";
        vigorText.text = $"生命力: {playerStats.statVigor} \n   最大HP: {playerStats.maxHealth}";
        enduranceText.text = $"持久力: {playerStats.statEndurance} \n   最大耐力: {playerStats.maxStamina}";
        strengthText.text = $"力量: {playerStats.statStrength} \n   攻击力加成: +{playerStats.attackPowerBonus}";
        resistanceText.text = $"坚韧度: {playerStats.statResistance} \n   物理防御: {playerStats.defensePower}";
        spiritText.text = $"精神力: {playerStats.statSpirit} \n   怒气获取倍率: {playerStats.rageGainMultiplier:F2}x";

        if (weaponText != null)
        {
            float currentWeaponAtk = playerStats.weaponBaseAttack + (playerStats.weaponLevel * playerStats.upgradeAttackBonus);
            weaponText.text = $"装备武器: {playerStats.weaponName} +{playerStats.weaponLevel} \n   武器攻击力: {currentWeaponAtk}";
        }
        
        if (goldText != null)
        {
            goldText.text = $"持有金币: <color=#FFD700>{playerStats.currentGold}</color> G";
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