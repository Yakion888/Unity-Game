using UnityEngine;
using UnityEngine.UI;
using TMPro; // 引入 TextMeshPro 命名空间

public class CharacterUIManager : MonoBehaviour
{
    [Header("玩家脚本引用")]
    public EldenRingMovement playerStats;

    [Header("UI 面板")]
    public GameObject characterPanel;

    [Header("文本组件引用")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI vigorText;
    public TextMeshProUGUI enduranceText;
    public TextMeshProUGUI strengthText;
    public TextMeshProUGUI resistanceText;
    public TextMeshProUGUI spiritText;

    void Start()
    {
        // 确保一开始面板是关闭的
        if (characterPanel != null)
        {
            characterPanel.SetActive(false);
        }
    }

    void Update()
    {
        // 如果玩家死亡或者正在休息转场，不允许开面板
        if (playerStats.isDead || playerStats.isResting) return;

        // 按下 C 键，开启或关闭面板
        if (Input.GetKeyDown(KeyCode.C))
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        bool isActive = !characterPanel.activeSelf;
        characterPanel.SetActive(isActive);

        // 同步给玩家脚本，告诉它UI打开了（用于解锁鼠标等）
        playerStats.isUIOpen = isActive;

        // 如果面板打开了，立刻刷新一遍数据
        if (isActive)
        {
            UpdateStatUI();
        }
    }

    // 更新面板上的文本数据
    public void UpdateStatUI()
    {
        if (playerStats == null) return;

        // 获取升下一级所需的经验
        int nextLevelXP = playerStats.GetXPRequirementForNextLevel();

        // 拼接文字显示（格式参考：等级: 5  (卢恩: 1500 / 3000) ）
        levelText.text = $"等级: {playerStats.currentLevel} \n经验: {playerStats.currentXP} / {nextLevelXP}";

        vigorText.text = $"生命力: {playerStats.statVigor} \n   最大HP: {playerStats.maxHealth}";
        
        enduranceText.text = $"持久力: {playerStats.statEndurance} \n   最大耐力: {playerStats.maxStamina}";
        
        strengthText.text = $"力量: {playerStats.statStrength} \n   攻击力加成: +{playerStats.attackPowerBonus}";
        
        resistanceText.text = $"坚韧度: {playerStats.statResistance} \n   物理防御: {playerStats.defensePower}";
        
        spiritText.text = $"精神力: {playerStats.statSpirit} \n   怒气获取倍率: {playerStats.rageGainMultiplier:F2}x";
    }

    // ==========================================
    // 按钮点击事件（绑定到面板的 "+" 按钮上）
    // ==========================================
    public void OnClickUpgradeVigor()
    {
        // 尝试加点，如果成功扣除了卢恩并升级，就立即刷新UI面板的文字
        if (playerStats.TryLevelUp("Vigor")) UpdateStatUI();
    }

    public void OnClickUpgradeEndurance()
    {
        if (playerStats.TryLevelUp("Endurance")) UpdateStatUI();
    }

    public void OnClickUpgradeStrength()
    {
        if (playerStats.TryLevelUp("Strength")) UpdateStatUI();
    }

    public void OnClickUpgradeResistance()
    {
        if (playerStats.TryLevelUp("Resistance")) UpdateStatUI();
    }

    public void OnClickUpgradeSpirit()
    {
        if (playerStats.TryLevelUp("Spirit")) UpdateStatUI();
    }
}