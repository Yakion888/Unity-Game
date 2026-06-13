using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==========================================
// 工业级架构：状态数值与 UI 同步处理中心
// ==========================================
public class PlayerStatsManager : MonoBehaviour
{
    [Header("生命值系统")]
    public float currentHealth;

    [Header("耐力系统")]
    public float currentStamina;
    public float staminaRegenRate = 15f;
    public float staminaRegenDelay = 1f;
    public float staminaRegenTimer = 0f;
    
    [Header("耐力消耗")]
    public float sprintStaminaCost = 25f;
    public float staminaBlockRemaining = 0f;
    public float staminaRegenBuffTimer = 0f;
    public float STAMINA_BLOCK_DURATION = 1.5f;

    [Header("怒气系统")]
    public float maxRage = 100f;
    public float currentRage = 0f;

    [Header("玩家UI设置")]
    public Slider healthSlider;
    public Slider staminaSlider;
    public Slider rageSlider;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI rageText;

    // 用来记录上一帧数值，防止每帧重复刷新文字导致 UI 卡顿
    private int lastHealth = -1;
    private int lastStamina = -1;
    private int lastRage = -1;

    // 核心机制：扣除耐力
    public bool ConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            staminaRegenTimer = 0f;  
            return true;
        }
        return false;
    }

    // 核心机制：恢复耐力
    public void RegenerateStamina(float maxStamina)
    {
        if (staminaBlockRemaining > 0f) return;
        if (staminaRegenTimer < staminaRegenDelay) return;

        float regenRate = staminaRegenRate;
        if (staminaRegenBuffTimer > 0f)
        {
            regenRate *= 2f;   // 闪避后的加速翻倍
            staminaRegenBuffTimer -= Time.deltaTime;
        }

        if (currentStamina < maxStamina)
        {
            currentStamina += regenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }

    // 核心机制：每帧向屏幕同步数据
    public void UpdateUIBarTexts(float maxHealth, float maxStamina)
    {
        // 1. 刷新滑动条
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;
        if (rageSlider != null) rageSlider.value = currentRage;

        // 2. 刷新文字（带防抖优化）
        int currentH = Mathf.CeilToInt(currentHealth);
        int currentS = Mathf.CeilToInt(currentStamina);
        int currentR = Mathf.CeilToInt(currentRage);
        int maxH = Mathf.CeilToInt(maxHealth);
        int maxS = Mathf.CeilToInt(maxStamina);
        int maxR = Mathf.CeilToInt(maxRage);

        if (healthText != null && currentH != lastHealth)
        {
            healthText.text = $"{currentH} / {maxH}";
            lastHealth = currentH;
        }
        if (staminaText != null && currentS != lastStamina)
        {
            staminaText.text = $"{currentS} / {maxS}";
            lastStamina = currentS;
        }
        if (rageText != null && currentR != lastRage)
        {
            rageText.text = $"{currentR} / {maxR}";
            lastRage = currentR;
        }
    }
}