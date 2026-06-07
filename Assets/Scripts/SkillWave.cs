using UnityEngine;
using System.Collections.Generic;

public class SkillWave : MonoBehaviour
{
    [Header("剑气运动设置")]
    public float moveSpeed = 8f;         
    
    [Header("【核心】精准判定盒调优")]
    public Vector3 hitboxSize = new Vector3(2f, 6f, 0.5f);  // 红框变成“竖着的门板”
    public float hitboxOffsetZ = 0f;                       // 判定盒的前后偏移（用来绝对对齐特效的视觉中心）
    public float hitboxOffsetY = 1.5f;                      //Y 轴上下偏移，方便对齐高大的竖向剑气
    
    public float tickInterval = 0.15f;    // 极快判定频率（0.15秒），让怪物死死“黏”在剑气上！
    //特效冷却时间！防止光污染
    public float vfxCooldown = 0.4f;   

    private int damagePerTick;
    private float pushForce;
    private float upForce;
    private LayerMask enemyLayer;
    private Vector3 moveDirection;
    private GameObject hitEffectPrefab;  
    private float timer = 0f;
    //用一个“字典”记住每一个怪物最后一次爆火花的时间
    private Dictionary<BasicEnemyTest, float> lastVfxTime = new Dictionary<BasicEnemyTest, float>();

    public void Initialize(int totalDamage, int totalTicks, float pForce, float uForce, LayerMask layer, Vector3 dir, GameObject hitVFX)
    {
        damagePerTick = Mathf.Max(1, totalDamage / totalTicks); 
        pushForce = pForce;
        upForce = uForce;
        enemyLayer = layer;
        moveDirection = dir.normalized;
        moveDirection.y = 0; 
        hitEffectPrefab = hitVFX; 

        DealDamageTick(); // 生成瞬间砍一刀
    }

    void Update()
    {
        // 剑气匀速飞行
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // 高频切割判定
        timer += Time.deltaTime;
        if (timer >= tickInterval)
        {
            timer = 0f;
            DealDamageTick();
        }
    }

    private void DealDamageTick()
    {
        // 修复 1：用薄薄的长方体（Box）取代圆球！绝不提前命中，绝不拖泥带水
        Vector3 boxCenter = transform.position + transform.forward * hitboxOffsetZ + transform.up * hitboxOffsetY;
        Collider[] hits = Physics.OverlapBox(boxCenter, hitboxSize / 2f, transform.rotation, enemyLayer, QueryTriggerInteraction.Ignore);
        
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in hits)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy);

                // 修复 2：“乘浪”击退！因为判定极快(0.15s)，怪物会被高频的小推力死死顶住，和剑气保持绝对的同步速度向后平移
                enemy.TakeKnockbackWithUp(moveDirection, pushForce, damagePerTick, upForce, 2, 0.4f);
                
                // 精准火花挂载
                if (hitEffectPrefab != null)
                {
                    // 检查字典：如果这个怪从来没爆过特效，或者距离上次爆特效已经过了 vfxCooldown (0.4秒)
                    if (!lastVfxTime.ContainsKey(enemy) || Time.time - lastVfxTime[enemy] >= vfxCooldown)
                    {
                        // 记录这次爆特效的时间
                        lastVfxTime[enemy] = Time.time;

                        // 特效排他性清理,寻找怪物身上有没有上一波还没消散完的旧火花（通过名字识别），如果有，直接销毁，为新火花腾地方
                        Transform oldSpark = enemy.transform.Find("Unique_SkillHitSpark");
                        if (oldSpark != null)
                        {
                            Destroy(oldSpark.gameObject);
                        }

                        // 生成特效
                        Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                        Vector3 sparkPos = chestPos - moveDirection * 0.2f;
                        GameObject effect = Instantiate(hitEffectPrefab, sparkPos, Quaternion.LookRotation(-moveDirection));
                        
                        //方便识别和清理
                        effect.name = "Unique_SkillHitSpark";

                        effect.transform.SetParent(enemy.transform, true);
                        Destroy(effect, 1.0f); 
                    }
                }
            }
        }
    }

    // 可视化排障：在 Scene 窗口亲眼看见你隐形的刀刃
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f); // 半透明红色
        // 随时间偏移的盒子中心
        Vector3 boxCenter = transform.position + transform.forward * hitboxOffsetZ + transform.up * hitboxOffsetY;
        // 把绘制矩阵和特效对齐
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        // 画出这个薄片判定盒
        Gizmos.DrawCube(Vector3.zero, hitboxSize);
    }
}