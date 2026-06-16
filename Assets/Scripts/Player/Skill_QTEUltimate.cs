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

        animHandler.anim.SetFloat("Speed", 0f);
        animHandler.anim.SetFloat("Direction", 0f);
        animHandler.anim.SetBool("IsMoving", false);
        animHandler.anim.SetBool("IsRunning", false);
        animHandler.anim.SetTrigger(animHandler.ultimateTrigger);
        
        if (ultChargeSFX != null) AudioPoolManager.Instance.PlaySound(ultChargeSFX, transform.position, 1.0f);
    }

    public void Event_TriggerQTE()
    {
        isWaitingForQTE = true;
        Time.timeScale = 0.1f; 

        if (SystemUIManager.Instance != null) SystemUIManager.Instance.ShowQTE();
        if (ultSlowMotionSFX != null) AudioPoolManager.Instance.PlaySound(ultSlowMotionSFX, transform.position, 1.0f, null, true);

        StartCoroutine(QTECountdown());
    }

    private IEnumerator QTECountdown()
    {
        yield return new WaitForSecondsRealtime(2.0f); 
        if (isWaitingForQTE)
        {
            isWaitingForQTE = false;
            Time.timeScale = 1.0f; 
            if (SystemUIManager.Instance != null) SystemUIManager.Instance.HideQTE(false);
        }
    }

    private void TriggerQTESuccess()
    {
        qteSuccess = true;
        isWaitingForQTE = false;
        Time.timeScale = 1.0f; 

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
        HashSet<BasicEnemyTest> enemies = new HashSet<BasicEnemyTest>(); 
        Vector3 pForward = transform.forward; pForward.y = 0;

        foreach (var hit in hits)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !enemies.Contains(enemy))
            {
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized; dirToEnemy.y = 0;
                if (Vector3.Angle(pForward, dirToEnemy) <= 90f)
                {
                    enemies.Add(enemy); 
                    enemy.TakeLaunchDamage(dirToEnemy, castKnockbackForce * 0.5f, finalDamage, ultimateLaunchForce, 2);
                    player.TriggerHitStop();
                    
                    Vector3 sparkPos = enemy.transform.position + Vector3.up * 1.2f + (transform.position - enemy.transform.position).normalized * 0.3f; 
                    Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                    player.SpawnHitEffect(ultHitEffect != null ? ultHitEffect : player.hitEffect, sparkPos, attachTarget);
                    if (ultHitSFX != null) AudioPoolManager.Instance.PlaySound(ultHitSFX, sparkPos, 1.0f,null, true);
                }
            }
        }
    }

    // 动画事件 3：最后砸地
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

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 7.0f, player.enemyLayer, QueryTriggerInteraction.Ignore); 
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy); 
                Vector3 enemyPos = enemy.transform.position; enemyPos.y = 0;
                Vector3 playerPos = transform.position; playerPos.y = 0;
                Vector3 knockbackDir = (enemyPos - playerPos).magnitude > 0.01f ? (enemyPos - playerPos).normalized : transform.forward;

                int displayType = qteSuccess ? 1 : 2; 
                enemy.TakeKnockbackWithUp(knockbackDir, castKnockbackForce * 1.5f, finalDamage, ultimateSlamForce, displayType, 2.5f);

                player.TriggerHitStop(); 
                Vector3 sparkPos = enemy.transform.position + Vector3.up * 1.2f + (transform.position - enemy.transform.position).normalized * 0.3f; 
                Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                player.SpawnHitEffect(ultHitEffect != null ? ultHitEffect : player.hitEffect, sparkPos, attachTarget);
            }
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