using UnityEngine;

public class MonsterLightningAttack : MonoBehaviour
{
    [Header("基础特效配置")]
    public GameObject lightningPrefab; // 你的闪电预制体

    [Header("闪电伤害配置")]
    public float lightningDamage = 35f;    // 这里保持 float 也没关系了
    public float isBlockinglightningDamage = 0f; 
    public float lightningRadius = 2.5f;   // 闪电轰炸的波及半径
    public float lightningPushForce = 15f; // 闪电爆炸击退力

    private Transform playerTransform;

    void Start()
    {
        // 游戏开始时，自动通过 "Player" 标签抓取主角的坐标
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("怪物找不到带有 'Player' 标签的主角！请检查主角的 Tag 设置。");
        }
    }

    // 这个函数用来给怪物的攻击动画事件（Animation Event）调用
    public void MonsterCallLightning()
    {
        if (lightningPrefab == null || playerTransform == null) return;

        Vector3 targetPos = playerTransform.position;
        Vector3 rayStartPos = targetPos + Vector3.up;

        if (Physics.Raycast(rayStartPos, Vector3.down, out RaycastHit hit, 40f))
        {
            // 1. 生成闪电视觉特效
            GameObject lightning = Instantiate(lightningPrefab, hit.point, Quaternion.identity);
            Destroy(lightning, 3.0f);

            // ============ 2. 对接你的 EldenRingMovement 逻辑 ============
            
            // 在闪电落地点进行球形范围检测
            Collider[] hitColliders = Physics.OverlapSphere(hit.point, lightningRadius);
            foreach (var col in hitColliders)
            {
                // 尝试获取你的核心玩家脚本
                EldenRingMovement playerScript = col.GetComponent<EldenRingMovement>();
                
                if (playerScript != null)
                {
                    // 计算从【闪电落地点】推向【玩家】的纯水平物理方向
                    Vector3 knockbackDir = (playerScript.transform.position - hit.point).normalized;
                    knockbackDir.y = 0; // 保持纯水平击退

                    // 3. 判断玩家是否格挡 (在 lightningDamage 前面加上 (int) 强转)
                    if (playerScript.isBlocking)
                    {
                        playerScript.TakeBlockDamage((int)isBlockinglightningDamage, knockbackDir, lightningPushForce * 0.5f);
                    }
                    else
                    {
                        playerScript.TakeDamage((int)lightningDamage, knockbackDir, lightningPushForce);
                    }

                    Debug.Log("玩家被怪物的闪电大招击中！");
                    break; // 击中主角后跳出循环
                }
            }
        }
    }
}