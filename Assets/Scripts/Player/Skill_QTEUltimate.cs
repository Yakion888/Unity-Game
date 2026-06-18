using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Skill_QTEUltimate : MonoBehaviour
{
    [Header("终极大招 (QTE系统)")]
    public int ultimateSlashBaseDamage = 30;  
    public int ultimateBaseDamage = 100;      
    public float ultimateQTEBonus = 3.0f;     
    public float ultimateLaunchForce = 12f;   
    public float ultimateSlamForce = -20f;    
    public float castDamageScalingMultiplier = 2.5f;
    public float castKnockbackForce = 15f; 

    [Header("大招特效")]
    public GameObject[] ultSlashEffects; 
    public GameObject ultLaunchEffect;   
    public GameObject ultFinalSlashEffect; 
    public GameObject ultSlamEffect;     
    public GameObject ultHitEffect;

    [Header("大招音效")]
    public AudioClip ultChargeSFX;          
    public AudioClip[] ultUpwardSlashSFXs;  
    public AudioClip ultSlowMotionSFX;      
    public AudioClip ultQTESuccessSFX;      
    public AudioClip ultSlamSFX;            
    public AudioClip ultHitSFX;             

    public bool isWaitingForQTE { get; private set; }
    private bool qteSuccess = false;
    private float lastEventTime = 0f;

    /// <summary>
    /// QTE 倒计时协程引用。用于在 QTE 成功后 StopCoroutine 防止超时误关 UI。
    /// </summary>
    private Coroutine _qteCountdownCoroutine;

    /// <summary>
    /// 【Bug 修复】追踪被 Event_UltLaunchHit 击飞的敌人。
    /// TakeLaunchDamage 会禁用敌人 Collider（防止飞行中碰撞），
    /// 导致 Event_UltimateHit 的 OverlapSphere 无法再次检测到它们。
    /// 此 HashSet 绕过物理查询，直接对已击飞敌人执行终结砸地。
    /// </summary>
    private HashSet<BasicEnemyTest> _launchedEnemies = new HashSet<BasicEnemyTest>();

    private EldenRingMovement player;
    private PlayerAnimatorHandler animHandler;
    private PlayerInputHandler inputHandler;

    public void Initialize(EldenRingMovement p, PlayerAnimatorHandler a, PlayerInputHandler i)
    {
        player = p;
        animHandler = a;
        inputHandler = i;
    }

    private void Update()
    {
        if (isWaitingForQTE && inputHandler.HeavyAttackInput && !qteSuccess)
        {
            TriggerQTESuccess();
        }
    }

    public void ExecuteSkill()
    {
        player.currentState = EldenRingMovement.ActionState.Ultimate;
        qteSuccess = false;
        isWaitingForQTE = false;

        // 【Bug 修复】每次释放大招时清空上一轮的击飞记录
        _launchedEnemies.Clear();

        // 利用大招前几段上挑动画的时间预加载 QTE 面板，
        // 确保 Event_TriggerQTE 触发时面板已在内存中，0 延迟弹出
        if (SystemUIManager.Instance != null)
            SystemUIManager.Instance.PreloadQTEPanel();

        animHandler.anim.SetFloat("Speed", 0f);
        animHandler.anim.SetFloat("Direction", 0f);
        animHandler.anim.SetBool("IsMoving", false);
        animHandler.anim.SetBool("IsRunning", false);
        animHandler.anim.SetTrigger(animHandler.ultimateTrigger);

        if (ultChargeSFX != null) AudioPoolManager.Instance.PlaySound(ultChargeSFX, transform.position, 1.0f);
    }

    public void Event_TriggerQTE()
    {
        // 【Bug 修复】防止动画事件重复触发：
        // - qteSuccess：已成功按下 → 终结斩击进行中，绝不应再弹 QTE
        // - isWaitingForQTE：已在等待输入 → 不重复开第二套 UI
        if (qteSuccess || isWaitingForQTE) return;

        isWaitingForQTE = true;
        Time.timeScale = 0.1f;

        if (SystemUIManager.Instance != null) SystemUIManager.Instance.ShowQTE();
        if (ultSlowMotionSFX != null) AudioPoolManager.Instance.PlaySound(ultSlowMotionSFX, transform.position, 1.0f, null, true);

        // 停止上一轮残留的倒计时（防御性），然后开启新一轮
        if (_qteCountdownCoroutine != null) StopCoroutine(_qteCountdownCoroutine);
        _qteCountdownCoroutine = StartCoroutine(QTECountdown());
    }

    private IEnumerator QTECountdown()
    {
        yield return new WaitForSecondsRealtime(2.0f);
        if (isWaitingForQTE && !qteSuccess)
        {
            isWaitingForQTE = false;
            Time.timeScale = 1.0f;
            if (SystemUIManager.Instance != null) SystemUIManager.Instance.HideQTE(false);
        }
        _qteCountdownCoroutine = null;
    }

    private void TriggerQTESuccess()
    {
        qteSuccess = true;
        isWaitingForQTE = false;
        Time.timeScale = 1.0f;

        // 【Bug 修复】停止 QTE 超时倒计时，防止它到期时错误地调用 HideQTE(false) 覆盖成功动画
        if (_qteCountdownCoroutine != null)
        {
            StopCoroutine(_qteCountdownCoroutine);
            _qteCountdownCoroutine = null;
        }

        if (SystemUIManager.Instance != null) SystemUIManager.Instance.HideQTE(true);
        AudioClip clipToPlay = ultQTESuccessSFX != null ? ultQTESuccessSFX : player.perfectDodgeStartSFX;
        if (clipToPlay != null) AudioPoolManager.Instance.PlaySound(clipToPlay, transform.position, 0.6f, null, true);
    }

    // 动画事件 1：前四段上挑伤害判定
    public void Event_UltUpwardSlashHit(int index)
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime; 

        if (ultSlashEffects != null && index >= 0 && index < ultSlashEffects.Length)
        {
            if (ultSlashEffects[index] != null) player.SpawnPureEffect(ultSlashEffects[index], transform.position + transform.forward * 0.5f + Vector3.up * 1.0f);
        }

        float totalDamage = ultimateSlashBaseDamage + (player.attackPowerBonus * castDamageScalingMultiplier * 0.5f);
        int finalSlashDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));

        Collider[] hitColliders = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, 2.5f, player.enemyLayer, QueryTriggerInteraction.Ignore);
        HashSet<BasicEnemyTest> slashedEnemies = new HashSet<BasicEnemyTest>();
        Vector3 playerForward = transform.forward; playerForward.y = 0;

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !slashedEnemies.Contains(enemy))
            {
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized; dirToEnemy.y = 0;
                if (Vector3.Angle(playerForward, dirToEnemy) <= 90f)
                {
                    slashedEnemies.Add(enemy); 
                    enemy.TakeDamageWithDirection(dirToEnemy, castKnockbackForce * 0.3f, finalSlashDamage, 2);
                    player.TriggerHitStop(); 
                    
                    Vector3 sparkPos = enemy.transform.position + Vector3.up * 1.2f + (transform.position - enemy.transform.position).normalized * 0.3f;
                    Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                    
                    GameObject vfxToUse = ultHitEffect != null ? ultHitEffect : player.hitEffect;
                    player.SpawnHitEffect(vfxToUse, sparkPos, attachTarget);
                    
                    if (ultHitSFX != null) AudioPoolManager.Instance.PlaySound(ultHitSFX, sparkPos, 1.0f,null, true);
                }
            }
        }
    }

    // 动画事件 2：第五段专属升龙击飞
    public void Event_UltLaunchHit()
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime;

        if (ultLaunchEffect != null) player.SpawnPureEffect(ultLaunchEffect, transform.position + transform.forward * 1.0f + Vector3.up * 1.2f);

        float totalDamage = ultimateSlashBaseDamage + (player.attackPowerBonus * castDamageScalingMultiplier * 0.5f);
        int finalDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));

        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, 2.5f, player.enemyLayer, QueryTriggerInteraction.Ignore);
        Vector3 pForward = transform.forward; pForward.y = 0;

        foreach (var hit in hits)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !_launchedEnemies.Contains(enemy))
            {
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized; dirToEnemy.y = 0;
                if (Vector3.Angle(pForward, dirToEnemy) <= 90f)
                {
                    // 【Bug 修复】记录被击飞的敌人，供 Event_UltimateHit 直接使用
                    // （TakeLaunchDamage 会禁用敌人 Collider，后续无法通过 OverlapSphere 再次检测）
                    _launchedEnemies.Add(enemy);

                    bool wasAlreadyDead = enemy.isDead;
                    enemy.TakeLaunchDamage(dirToEnemy, castKnockbackForce * 0.5f, finalDamage, ultimateLaunchForce, 2);

                    // 【鞭尸支持】若上挑伤害直接击杀了敌人，延长尸体存活时间，
                    // 让它能挺过 QTE 窗口 (0~2s) + 终结斩击动画 (~1s) + 贴地滑行 (~2s)
                    if (!wasAlreadyDead && enemy.isDead)
                        enemy.ResetDeathCleanupTimer(6f);

                    player.TriggerHitStop();

                    Vector3 sparkPos = enemy.transform.position + Vector3.up * 1.2f + (transform.position - enemy.transform.position).normalized * 0.3f;
                    Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                    player.SpawnHitEffect(ultHitEffect != null ? ultHitEffect : player.hitEffect, sparkPos, attachTarget);
                    if (ultHitSFX != null) AudioPoolManager.Instance.PlaySound(ultHitSFX, sparkPos, 1.0f,null, true);
                }
            }
        }
    }

    // 动画事件 3：最后砸地（终结斩击）
    public void Event_UltimateHit()
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime;

        if (ultSlamSFX != null) AudioPoolManager.Instance.PlaySound(ultSlamSFX, transform.position, 1.2f, null, true);
        if (ultFinalSlashEffect != null) player.SpawnPureEffect(ultFinalSlashEffect, transform.position + transform.forward * 1.0f + Vector3.up * 1.2f);
        if (ultSlamEffect != null) player.SpawnPureEffect(ultSlamEffect, transform.position + transform.forward * 1.0f + Vector3.up * 0.1f);

        float totalDamage = ultimateBaseDamage + (player.attackPowerBonus * castDamageScalingMultiplier);
        if (qteSuccess) totalDamage *= ultimateQTEBonus;

        int finalDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));

        // 【Bug 修复】不再使用 OverlapSphere（敌人 Collider 已被 TakeLaunchDamage 禁用），
        // 直接遍历 _launchedEnemies 中记录的击飞敌人，确保终结砸地必定命中。
        foreach (var enemy in _launchedEnemies)
        {
            // 防御：敌人可能已因其他原因被销毁
            if (enemy == null) continue;

            Vector3 enemyPos = enemy.transform.position; enemyPos.y = 0;
            Vector3 playerPos = transform.position; playerPos.y = 0;
            Vector3 knockbackDir = (enemyPos - playerPos).magnitude > 0.01f ? (enemyPos - playerPos).normalized : transform.forward;

            int displayType = qteSuccess ? 1 : 2;
            bool wasDeadBefore = enemy.isDead;

            // ultimateSlamForce = -20f → 向下砸地 + 贴地滑行
            enemy.TakeKnockbackWithUp(knockbackDir, castKnockbackForce * 1.5f, finalDamage, ultimateSlamForce, displayType, 2.5f);

            // 【鞭尸支持】
            // 情况 A：敌人在上挑时已被击杀 → 上面 ResetDeathCleanupTimer(6f) 已延长过，
            //         这里再 Refresh 一次，保证从"此刻"起还有 3.5s 滑行 + 躺地时间。
            // 情况 B：敌人刚好在本次终结斩击伤害中被击杀 → 初次延长，3.5s 足够滑行。
            // 情况 C：敌人还活着（只受伤未死） → ResetDeathCleanupTimer 内部 isDead 检查会跳过，无害。
            if (enemy.isDead)
                enemy.ResetDeathCleanupTimer(3.5f);

            player.TriggerHitStop();
            Vector3 sparkPos = enemy.transform.position + Vector3.up * 1.2f + (transform.position - enemy.transform.position).normalized * 0.3f;
            Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
            player.SpawnHitEffect(ultHitEffect != null ? ultHitEffect : player.hitEffect, sparkPos, attachTarget);
        }
    }

    public void OnUltimateFinished()
    {
        if (player.currentState == EldenRingMovement.ActionState.Ultimate)
        {
            player.currentState = EldenRingMovement.ActionState.IdleMove;
        }
        isWaitingForQTE = false;
        Time.timeScale = 1.0f;

        // 【Bug 修复】停止残留的倒计时协程
        if (_qteCountdownCoroutine != null)
        {
            StopCoroutine(_qteCountdownCoroutine);
            _qteCountdownCoroutine = null;
        }

        // 【Bug 修复】大招结束，清空击飞追踪
        _launchedEnemies.Clear();

        if (animHandler.anim != null) animHandler.anim.ResetTrigger(animHandler.ultimateTrigger);
        GetComponent<IdleSelector>()?.ResetIdleTimer();
    }

    public void Event_UltUpwardSlashSound(int index)
    {
        if (ultUpwardSlashSFXs != null && index >= 0 && index < ultUpwardSlashSFXs.Length && ultUpwardSlashSFXs[index] != null)
        {
            AudioPoolManager.Instance.PlaySound(ultUpwardSlashSFXs[index], transform.position, 0.9f, null, true);
        }
    }
}