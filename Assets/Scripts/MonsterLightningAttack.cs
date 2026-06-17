using UnityEngine;
using System.Collections;

// 重构：全池化、状态机协同的怪物追踪法术组件
public class MonsterLightningAttack : MonoBehaviour
{
    [Header("闪电特效与音效")]
    public GameObject lightningPrefab;       // 落地闪电特效
    public AudioClip lightningStrikeSFX;     // 👈 【新增】：雷击爆发音效

    [Header("伤害配置")]
    public float lightningDamage = 35f;
    public float lightningRadius = 2.5f;
    public float lightningPushForce = 15f;

    [Header("动态警示圈设置")]
    public Color circleColor = Color.red;    
    public float circleLineWidth = 0.15f;   
    public int circleSegment = 36;          

    private LineRenderer warningCircle;     
    private Transform playerTransform;
    private BasicEnemyTest enemyMaster;

    void Start()
    {
        enemyMaster = GetComponent<BasicEnemyTest>();
        playerTransform = GameObject.FindWithTag("Player")?.transform;

        GameObject circleObj = new GameObject("WarningCircle");
        circleObj.transform.SetParent(null); 
        warningCircle = circleObj.AddComponent<LineRenderer>();
        
        warningCircle.material = new Material(Shader.Find("Sprites/Default"));
        warningCircle.enabled = false;
        warningCircle.useWorldSpace = true;
        warningCircle.startColor = circleColor;
        warningCircle.endColor = circleColor;
        warningCircle.startWidth = circleLineWidth;
        warningCircle.endWidth = circleLineWidth;
        warningCircle.positionCount = circleSegment + 1;
    }

    public void ExecuteLightningStrike()
    {
        StartCoroutine(LightningRoutine());
    }

    private IEnumerator LightningRoutine()
    {
        if (playerTransform == null) yield break;

        warningCircle.enabled = true;
        Vector3 targetPos = playerTransform.position;

        // 【节奏一：死亡追踪期 - 1秒】
        float trackTimer = 0f;
        while (trackTimer < 1.0f)
        {
            if (enemyMaster.currentState == BasicEnemyTest.EnemyState.Hit || enemyMaster.isDead)
            {
                warningCircle.enabled = false;
                yield break;
            }

            trackTimer += Time.deltaTime;
            
            if (Physics.Raycast(playerTransform.position + Vector3.up, Vector3.down, out RaycastHit hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                targetPos = hit.point;
            }
            DrawWarningCircle(targetPos);
            yield return null;
        }

        // 【节奏二：死锁爆破期 - 0.5秒】
        warningCircle.startColor = Color.yellow;
        warningCircle.endColor = Color.yellow;
        
        float lockTimer = 0f;
        while (lockTimer < 0.5f)
        {
            if (enemyMaster.currentState == BasicEnemyTest.EnemyState.Hit || enemyMaster.isDead)
            {
                warningCircle.enabled = false;
                warningCircle.startColor = circleColor; warningCircle.endColor = circleColor;
                yield break;
            }
            lockTimer += Time.deltaTime;
            yield return null;
        }

        // 【节奏三：天罚降临】
        warningCircle.enabled = false;
        warningCircle.startColor = circleColor; warningCircle.endColor = circleColor; 

        // ==============================================================
        // 核心升级：特效与音效全面接入全局对象池！0 GC
        // ==============================================================
        
        // 1. 播放 3D 爆炸音效（注意不加 true 参数，利用引擎天然的距离衰减）
        if (lightningStrikeSFX != null && AudioPoolManager.Instance != null)
        {
            // 音量可微调（这里写1.0f），位置传入 targetPos 保证声源在雷击处！
            AudioPoolManager.Instance.PlaySound(lightningStrikeSFX, targetPos, 1.0f, null, true);
        }

        // 2. 从特效池拿取闪电特效，3秒后自动回收
        if (lightningPrefab != null && VFXPoolManager.Instance != null)
        {
            GameObject lightning = VFXPoolManager.Instance.SpawnFromPool(lightningPrefab, targetPos, Quaternion.identity);
            VFXPoolManager.Instance.ReturnToPool(lightning, 3f);
        }

        // 3. 伤害判定
        Collider[] hitColliders = Physics.OverlapSphere(targetPos, lightningRadius);
        foreach (var col in hitColliders)
        {
            EldenRingMovement playerScript = col.GetComponent<EldenRingMovement>();
            if (playerScript != null)
            {
                Vector3 knockbackDir = (playerScript.transform.position - targetPos).normalized; knockbackDir.y = 0;
                if (playerScript.isBlocking) playerScript.TakeBlockDamage((int)(lightningDamage * 0.5f), knockbackDir, lightningPushForce * 0.5f);
                else playerScript.TakeDamage((int)lightningDamage, knockbackDir, lightningPushForce);
            }
        }

        // 技能放完后，强行一脚把怪物踹回追逐状态
        if (enemyMaster != null && enemyMaster.currentState == BasicEnemyTest.EnemyState.MagicCast)
        {
            enemyMaster.currentState = BasicEnemyTest.EnemyState.Chase;
        }
    }

    void DrawWarningCircle(Vector3 center)
    {
        for (int i = 0; i <= circleSegment; i++)
        {
            float rad = Mathf.Deg2Rad * (i * 360f / circleSegment);
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * lightningRadius, 0.05f, Mathf.Sin(rad) * lightningRadius);
            warningCircle.SetPosition(i, pos);
        }
    }
}