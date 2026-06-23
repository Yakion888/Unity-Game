using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 玩家生命系统 —— 从 EldenRingMovement 拆出
///
/// ═══════════════════════════════════════════════════════════
/// 【职责】
///   1. 受击：护甲减伤 / 霸体免打断 / 格挡减伤 / 无敌闪避
///   2. 死亡 + 重生协程
///   3. 击退物理向量 (impact) 计算
///   4. 受击音效、完美闪避慢动作
///
/// 【事件驱动】
///   OnDamageTaken → 主脚本设状态机 Hit + 播动画
///   OnDied        → 主脚本播死亡动画 + 关控制器
///   不再直接改 currentState，完全解耦
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    // ============================================================
    // Inspector
    // ============================================================

    [Header("受击反馈")]
    public AudioClip[] playerHitSounds;
    public AudioClip deathSFX;
    public AudioClip perfectDodgeStartSFX;

    // ============================================================
    // 引用（由主脚本在 Start 中注入）
    // ============================================================

    [HideInInspector] public Slider healthSlider;
    [HideInInspector] public Slider staminaSlider;
    [HideInInspector] public Slider rageSlider;
    [HideInInspector] public AudioSource audioSource;
    [HideInInspector] public Animator anim;
    [HideInInspector] public CharacterController controller;
    [HideInInspector] public PlayerLocomotionManager locomotion;
    /// <summary>权威数据源：玩家数值（唯一真值，不要维护本地副本）</summary>
    [HideInInspector] public PlayerStatsManager stats;
    /// <summary>最大血量/耐力（由主脚本注入，Respawn 需要）</summary>
    [HideInInspector] public System.Func<float> getMaxHealth;
    [HideInInspector] public System.Func<float> getMaxStamina;

    [HideInInspector] public float defensePower;
    [HideInInspector] public System.Func<Vector3> getRespawnPosition;
    [HideInInspector] public System.Func<Quaternion> getRespawnRotation;

    /// <summary>由主脚本注入的回调</summary>
    public System.Func<bool> isInvincibleCheck;
    public System.Func<bool> isBlockingCheck;
    public System.Func<bool> isCastingOrUltimate;
    public System.Func<Vector3> getForward;

    // ============================================================
    // 对外只读
    // ============================================================

    /// <summary>当前击退力向量（LocomotionManager 每帧消费并衰减）</summary>
    public Vector3 impact;

    /// <summary>是否已死亡</summary>
    public bool IsDead { get; private set; }

    /// <summary>下一次攻击是否暴击（完美闪避奖励）</summary>
    public bool NextAttackIsCrit { get; set; }

    /// <summary>下一次重击是否直接跳到第四段</summary>
    public bool NextHeavyAttackIsFourth { get; set; }

    /// <summary>击杀后怒气增量</summary>
    public System.Action<float> onRageGain;

    // ============================================================
    // 事件
    // ============================================================

    /// <summary>受击（未死亡）：Vector3 = 击退方向</summary>
    public event System.Action<Vector3> OnDamageTaken;

    /// <summary>死亡</summary>
    public event System.Action OnDied;

    /// <summary>重生完成</summary>
    public event System.Action OnRespawned;

    // ============================================================
    // 内部
    // ============================================================

    // ============================================================
    // 受击
    // ============================================================

    /// <summary>由主脚本注入的 Animator Trigger 哈希值</summary>
    [HideInInspector] public int hitTriggerHash;
    [HideInInspector] public int dieTriggerHash;

    /// <summary>
    /// 受到伤害。敌人脚本直接调用此方法。
    /// 内部处理：防御减伤 → 霸体 → 格挡 → 无敌 → 扣血 → 击退 → 死亡判定。
    /// </summary>
    public void TakeDamage(int rawDamage, Vector3 knockbackDir = default, float knockbackForce = 0f)
    {
        if (IsDead) return;

        // ── 无敌帧 ──
        if (isInvincibleCheck != null && isInvincibleCheck())
        {
            StartCoroutine(PerfectDodgeReward());
            if (stats != null) stats.currentRage += 10f;
            onRageGain?.Invoke(10f);
            NextAttackIsCrit = true;
            NextHeavyAttackIsFourth = true;
            return;
        }

        int finalDamage = CalcFinalDamage(rawDamage);

        // ── 霸体 ──
        if (isCastingOrUltimate != null && isCastingOrUltimate())
        {
            int armorDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.6f));
            if (stats != null) stats.currentHealth -= armorDamage;
            if (healthSlider != null) healthSlider.value = stats.currentHealth;
            if (stats != null && stats.currentHealth <= 0) Die();
            return;
        }

        // ── 格挡 ──
        if (isBlockingCheck != null && isBlockingCheck())
        {
            TakeBlockDamage(rawDamage, knockbackDir, knockbackForce);
            return;
        }

        // ── 正常受击 ──
        ApplyKnockback(knockbackDir, knockbackForce);
        if (stats != null) stats.currentHealth -= finalDamage;
        if (healthSlider != null) healthSlider.value = stats.currentHealth;

        bool lethal = stats != null && stats.currentHealth <= 0;
        if (!lethal) PlayHitSound(finalDamage); // 死亡时不播受击音效，避免和死亡音效重叠

        if (lethal) { Die(); return; }

        OnDamageTaken?.Invoke(knockbackDir);
    }

    /// <summary>格挡受击</summary>
    public void TakeBlockDamage(int rawDamage, Vector3 knockbackDir = default, float knockbackForce = 0f)
    {
        int finalDamage = CalcFinalDamage(rawDamage);
        ApplyKnockback(knockbackDir, knockbackForce);

        int blockDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.5f));
        if (stats != null) stats.currentHealth -= blockDamage;
        if (healthSlider != null) healthSlider.value = stats.currentHealth;

        if (stats != null && stats.currentHealth <= 0)
        {
            Die();
            return;
        }

        OnDamageTaken?.Invoke(knockbackDir);
    }

    // ============================================================
    // 死亡 / 重生
    // ============================================================

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        StopAllCoroutines();
        Time.timeScale = 1f;

        if (deathSFX != null && audioSource != null)
            audioSource.PlayOneShot(deathSFX, 1.0f);

        if (anim != null && dieTriggerHash != 0) anim.SetTrigger(dieTriggerHash);
        if (controller != null) controller.enabled = false;

        OnDied?.Invoke();
        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(3f);
        Input.ResetInputAxes();

        NextAttackIsCrit = false;
        NextHeavyAttackIsFourth = false;

        // ── 写入权威源 ──
        float maxH = getMaxHealth != null ? getMaxHealth() : 100f;
        float maxS = getMaxStamina != null ? getMaxStamina() : 100f;
        if (stats != null) { stats.currentHealth = maxH; stats.currentStamina = maxS; }

        if (healthSlider  != null) healthSlider.value  = maxH;
        if (staminaSlider != null) staminaSlider.value = maxS;

        if (controller != null) controller.enabled = false;
        transform.position = getRespawnPosition != null ? getRespawnPosition() : transform.position;
        transform.rotation = getRespawnRotation != null ? getRespawnRotation() : transform.rotation;

        IsDead = false;
        OnRespawned?.Invoke();
    }

    // ============================================================
    // 卡肉 / 完美闪避
    // ============================================================

    public void TriggerHitStop(float duration = 0.05f)
    {
        StartCoroutine(HitStop(duration));
    }

    private IEnumerator HitStop(float duration)
    {
        float originalScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = originalScale;
    }

    private IEnumerator PerfectDodgeReward()
    {
        if (perfectDodgeStartSFX != null && audioSource != null)
        {
            var pool = AudioPoolManager.Instance;
            if (pool != null) pool.PlaySound(perfectDodgeStartSFX, transform.position, 0.8f, null, true);
        }

        float originalScale = Time.timeScale;
        Time.timeScale = 0.25f;
        yield return new WaitForSecondsRealtime(0.5f);
        Time.timeScale = originalScale;
    }

    // ============================================================
    // 工具
    // ============================================================

    private int CalcFinalDamage(int rawDamage)
    {
        float randomized = rawDamage * Random.Range(0.9f, 1.1f);
        float reduction = 100f / (100f + defensePower);
        return Mathf.Max(1, Mathf.RoundToInt(randomized * reduction));
    }

    private void ApplyKnockback(Vector3 dir, float force)
    {
        if (force > 0f)
            impact = dir * force;
    }

    private void PlayHitSound(int finalDamage)
    {
        if (playerHitSounds == null || playerHitSounds.Length == 0 || audioSource == null) return;
        int n = playerHitSounds.Length;
        AudioClip clip = n >= 5
            ? (finalDamage < 15 ? playerHitSounds[Random.Range(0, Mathf.Min(3, n))] : playerHitSounds[Random.Range(Mathf.Min(3, n-1), n)])
            : playerHitSounds[Random.Range(0, n)];
        audioSource.PlayOneShot(clip, 0.8f);
    }
}
