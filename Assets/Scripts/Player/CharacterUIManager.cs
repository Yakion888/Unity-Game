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
    public TextMeshProUGUI weaponText;    
    public TextMeshProUGUI goldText;

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

        int nextLevelXP = playerStats.GetXPRequirementForNextLevel();

        // 使用 TMP SetText(格式化模板, 参数...) 替代 string 插值：
        //   SetText 直写 TMP 内部 char[] 缓冲区 → 零堆分配
        //   而 $"{}" 会在托管堆上分配临时 string → 触发 GC

        levelText.SetText("等级: {0} \n经验: {1} / {2}",
            playerStats.currentLevel, playerStats.currentXP, nextLevelXP);

        vigorText.SetText("生命力: {0}    最大HP: {1}",
            playerStats.statVigor, playerStats.maxHealth);

        enduranceText.SetText("持久力: {0}     最大耐力: {1}",
            playerStats.statEndurance, playerStats.maxStamina);

        strengthText.SetText("力量: {0}     攻击力加成: +{1}",
            playerStats.statStrength, playerStats.attackPowerBonus);

        resistanceText.SetText("坚韧度: {0}     物理防御: {1}",
            playerStats.statResistance, playerStats.defensePower);

        spiritText.SetText("精神力: {0}     怒气获取倍率: {1:0.00}x",
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
            goldText.SetText("持有金币: <color=#FFD700>{0}</color> G",
                playerStats.currentGold);
        }
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