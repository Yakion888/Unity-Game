using UnityEngine;
using System.Collections.Generic;

public class SkillWave : MonoBehaviour
{
    [Header("剑气运动设置")]
    public float moveSpeed = 8f;         // 🌟【提高移速】：让大招显得更加迅猛
    public float damageRadius = 1.5f;    // 🌟【核心修复 1】：判定球缩小到 1.5 米！只有真正碰到的瞬间才掉血！
    public float tickInterval = 0.25f;   // 🌟【加快频率】：每 0.25 秒切一次，打击感更绵密

    // 接收从玩家那里传过来的属性
    private int damagePerTick;
    private float pushForce;
    private float upForce;
    private LayerMask enemyLayer;
    private Vector3 moveDirection;

    // 🌟【核心变量】：接收玩家的命中特效预制体（你刚才就是漏掉了这一行！）
    private GameObject hitEffectPrefab;  

    private float timer = 0f;

    // 接收参数的初始化方法
    public void Initialize(int totalDamage, int totalTicks, float pForce, float uForce, LayerMask layer, Vector3 dir, GameObject hitVFX)
    {
        damagePerTick = Mathf.Max(1, totalDamage / totalTicks); 
        pushForce = pForce;
        upForce = uForce;
        enemyLayer = layer;
        moveDirection = dir.normalized;
        moveDirection.y = 0; 
        
        hitEffectPrefab = hitVFX; // 将玩家传过来的特效存到变量里

        DealDamageTick();
    }

    void Update()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= tickInterval)
        {
            timer = 0f;
            DealDamageTick();
        }
    }

    private void DealDamageTick()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, enemyLayer);
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in hits)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy);

                // 把敌人击飞并向后推
                enemy.TakeKnockbackWithUp(moveDirection, pushForce, damagePerTick, upForce, 2, 0.6f);
                
                // 🌟【核心修复】：精准定位命中特效，并焊死在怪物身上！
                if (hitEffectPrefab != null)
                {
                    // 1. 锁定怪物胸口高度（1.2米）
                    Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                    
                    // 2. 为了看起来更真实，让火花往剑气飞来的方向（迎着刀刃）稍微偏移 0.3 米
                    Vector3 sparkPos = chestPos - moveDirection * 0.3f;

                    // 3. 生成特效，让火花面向剑气飞行的反方向喷射
                    GameObject effect = Instantiate(hitEffectPrefab, sparkPos, Quaternion.LookRotation(-moveDirection));
                    
                    // ✅ 【神级操作】：把生成的火花强行变成怪物的“子物体”！
                    // 这样怪物被击退、击飞在空中疯狂后仰时，伤口处的火花会死死粘在它胸口跟着一起动！
                    effect.transform.SetParent(enemy.transform, true);

                    // 1秒后销毁防止内存泄漏
                    Destroy(effect, 1.0f); 
                }
            }
        }
    }

    // 辅助测试：在 Scene 窗口画出剑气的杀伤范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}