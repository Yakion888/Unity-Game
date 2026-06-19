using UnityEngine;
using System.Collections;

public class Skill_WaveSlash : MonoBehaviour
{
    [Header("技能系统 (裂地剑气)")]
    public int castDamage = 100;
    public float castDuration = 3.0f;  
    public float castDamageScalingMultiplier = 2.5f;  //大招的力量加成倍率 (比如 2.0 代表享受 200% 的攻击力加成)
    public float castRadius = 3f;  
    public float skillWavePushForce = 6f;  // 剑气每段的水平推力（建议4~6，用来匹配剑气飞行速度）
    public float skillWaveUpForce = 2f;    // 剑气每段的微小浮空力（给个2f能抵消地面摩擦力，让怪更丝滑地跟着飞）
    public float skillWaveLifetime = 1.5f; // 剑气物理存活时间（默认1.5秒）

    [Header("1技能特效与音效")]
    public GameObject skillEffect;               
    public GameObject skillHitEffect;            
    public AudioClip castSound;                

    private EldenRingMovement player;
    private PlayerAnimatorHandler animHandler;

    public void Initialize(EldenRingMovement p, PlayerAnimatorHandler a)
    {
        player = p;
        animHandler = a;
    }

    public void ExecuteSkill()
    {
        //Debug.Log("裂地剑气 施法开始");
        player.currentState = EldenRingMovement.ActionState.SkillCast;
        
        animHandler.anim.SetFloat("Speed", 0f);
        animHandler.anim.SetFloat("Direction", 0f);
        animHandler.anim.SetBool("IsMoving", false);
        animHandler.anim.SetBool("IsRunning", false);
        animHandler.anim.SetTrigger(animHandler.castTrigger);

        if (castSound != null) 
            AudioPoolManager.Instance.PlaySound(castSound, transform.position, 1f, null, true);

        StartCoroutine(CastRoutine());
    }

    private IEnumerator CastRoutine()
    {
        if (animHandler.anim == null)
        {
            yield return new WaitForSeconds(castDuration);
            OnCastFinished();
            yield break;
        }

        int initialState = animHandler.anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
        float timeout = 0f;
        while (animHandler.anim.GetCurrentAnimatorStateInfo(0).shortNameHash == initialState && !animHandler.anim.IsInTransition(0))
        {
            timeout += Time.deltaTime;
            if (timeout > 0.5f) break; 
            yield return null;
        }

        AnimatorStateInfo stateInfo = animHandler.anim.IsInTransition(0) ? animHandler.anim.GetNextAnimatorStateInfo(0) : animHandler.anim.GetCurrentAnimatorStateInfo(0);
        float actualAnimLength = stateInfo.length <= 0.1f ? castDuration : stateInfo.length;

        float timer = 0f;
        while (timer < actualAnimLength * 0.95f) 
        {
            if (player.isDead) yield break; 
            timer += Time.deltaTime;
            yield return null;
        }

        OnCastFinished();
    }

    // 动画事件调用
    public void OnCastFinished()
    {
        if (player.currentState == EldenRingMovement.ActionState.SkillCast)
        {
            player.currentState = EldenRingMovement.ActionState.IdleMove;
        }
        
        if (animHandler.anim != null) 
        {
            animHandler.anim.ResetTrigger(animHandler.castTrigger);
            animHandler.anim.SetFloat("IdleIndex", 0f); 
        }

        GetComponent<IdleSelector>()?.ResetIdleTimer();
    }

    // 动画事件调用
    public void CastDamage()
    {
        float scaledBonusDamage = player.attackPowerBonus * castDamageScalingMultiplier;
        int finalDamage = Mathf.RoundToInt((castDamage + scaledBonusDamage) * Random.Range(0.9f, 1.1f));

        if (skillEffect != null)
        {
            Vector3 vfxPos = transform.position + transform.forward * 1.5f + Vector3.up * 1.0f; 
            GameObject waveVFX = VFXPoolManager.Instance.SpawnFromPool(skillEffect, vfxPos, transform.rotation * skillEffect.transform.rotation);

            SkillWave waveScript = waveVFX.GetComponent<SkillWave>();
            GameObject vfxToPass = skillHitEffect != null ? skillHitEffect : player.hitEffect;    
            waveScript.Initialize(finalDamage, 10, skillWavePushForce, skillWaveUpForce, player.enemyLayer, transform.forward, vfxToPass);

            VFXPoolManager.Instance.ReturnToPool(waveVFX, skillWaveLifetime);
        }
    }
}