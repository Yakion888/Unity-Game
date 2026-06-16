using UnityEngine;
using System.Collections;

// 彻底剥夺它的 Update 权利，变成听主脚本指挥的纯战技组件！
public class MonsterLightningAttack : MonoBehaviour
{
    [Header("闪电特效")]
    public GameObject lightningPrefab;       

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
        circleObj.transform.SetParent(null); // 不要作为子物体，防止随怪物移动
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

    // 由怪物主脚本调用！
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
        // 圈圈死死跟着玩家，给玩家极大的压迫感！
        float trackTimer = 0f;
        while (trackTimer < 1.0f)
        {
            // 如果怪物在这个期间被打断/死了，直接取消施法！
            if (enemyMaster.currentState == BasicEnemyTest.EnemyState.Hit || enemyMaster.isDead)
            {
                warningCircle.enabled = false;
                yield break;
            }

            trackTimer += Time.deltaTime;
            
            // 射线贴地
            if (Physics.Raycast(playerTransform.position + Vector3.up, Vector3.down, out RaycastHit hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                targetPos = hit.point;
            }
            DrawWarningCircle(targetPos);
            yield return null;
        }

        // 【节奏二：死锁爆破期 - 0.5秒】
        // 圈圈不再移动，颜色变亮闪烁，告诉玩家：立刻翻滚！
        warningCircle.startColor = Color.yellow;
        warningCircle.endColor = Color.yellow;
        
        float lockTimer = 0f;
        while (lockTimer < 0.5f)
        {
            // 同样，如果怪物被打断，法术取消
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
        warningCircle.startColor = circleColor; warningCircle.endColor = circleColor; // 恢复颜色

        if (lightningPrefab != null)
        {
            GameObject lightning = Instantiate(lightningPrefab, targetPos, Quaternion.identity);
            Destroy(lightning, 3f);
        }

        // 伤害判定
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

        // 【核心修复】：闪电劈完后，如果怪物还在施法状态，强行把他一脚踹回追逐状态
        // 彻底抛弃不可靠的动画事件！
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