using UnityEngine;

public class MonsterLightningAttack : MonoBehaviour
{
    [Header("闪电特效")]
    public GameObject lightningPrefab;       // 落地闪电特效

    [Header("伤害配置")]
    public float lightningDamage = 35f;
    public float isBlockinglightningDamage = 20f;
    public float lightningRadius = 2.5f;
    public float lightningPushForce = 15f;

    [Header("时间&距离")]
    public float attackTriggerDistance = 16f; // 触发距离
    public float warningTime = 2f;           // 预警2秒
    public float attackCooldown = 8f;       // 技能冷却

    [Header("动态警示圈设置")]
    public Color circleColor = Color.red;    // 圈颜色
    public float circleLineWidth = 0.15f;   // 线条粗细
    public int circleSegment = 36;          // 圆圈分段数(越高越圆)

    private Transform playerTransform;
    private float currentCooldown;
    private float warningTimer;
    private bool isWarning;
    private Vector3 targetGroundPos;
    private LineRenderer warningCircle;     // 代码生成的画线组件

    void Start()
    {
        // 查找玩家
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("未找到Tag为Player的物体！");
        }

        // 自动创建画线物体 + LineRenderer（纯代码生成，不用预制）
        GameObject circleObj = new GameObject("WarningCircle");
        circleObj.transform.SetParent(transform);
        warningCircle = circleObj.AddComponent<LineRenderer>();
        
        // 初始化画线参数
        warningCircle.enabled = false;
        warningCircle.useWorldSpace = true;
        warningCircle.startColor = circleColor;
        warningCircle.endColor = circleColor;
        warningCircle.startWidth = circleLineWidth;
        warningCircle.endWidth = circleLineWidth;
        warningCircle.positionCount = circleSegment + 1;

        // 状态初始化
        currentCooldown = 0;
        warningTimer = 0;
        isWarning = false;
    }

    void Update()
    {
        // 冷却倒计时
        if (currentCooldown > 0)
            currentCooldown -= Time.deltaTime;

        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool playerInRange = distance <= attackTriggerDistance;

        // 离开范围 / 冷却中：关闭警示圈、重置状态
        if (!playerInRange || currentCooldown > 0)
        {
            warningCircle.enabled = false;
            isWarning = false;
            warningTimer = 0;
            return;
        }

        // 开始预警
        if (!isWarning)
        {
            StartWarning();
        }

        // 预警计时
        if (isWarning)
        {
            warningTimer += Time.deltaTime;
            DrawWarningCircle(); // 实时绘制地面圆圈

            // 预警结束，释放闪电
            if (warningTimer >= warningTime)
            {
                SpawnLightningAndDamage();
                warningCircle.enabled = false;
                isWarning = false;
                warningTimer = 0;
                currentCooldown = attackCooldown;
            }
        }
    }

    // 开启预警
    void StartWarning()
    {
        isWarning = true;
        warningTimer = 0;

        // 射线取地面落点
        Vector3 rayStart = playerTransform.position + Vector3.up;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 40f))
        {
            targetGroundPos = hit.point;
            warningCircle.enabled = true;
        }
    }

    // 代码绘制地面圆形圈
    void DrawWarningCircle()
    {
        float radius = lightningRadius;
        for (int i = 0; i <= circleSegment; i++)
        {
            float rad = Mathf.Deg2Rad * (i * 360f / circleSegment);
            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;
            Vector3 pos = targetGroundPos + new Vector3(x, 0.05f, z); // y抬高一点防贴地闪烁
            warningCircle.SetPosition(i, pos);
        }
    }

    // 生成闪电 + 造成伤害
    void SpawnLightningAndDamage()
    {
        if (lightningPrefab == null) return;
        GameObject lightning = Instantiate(lightningPrefab, targetGroundPos, Quaternion.identity);
        Destroy(lightning, 3f);

        Collider[] hitColliders = Physics.OverlapSphere(targetGroundPos, lightningRadius);
        foreach (var col in hitColliders)
        {
            EldenRingMovement playerScript = col.GetComponent<EldenRingMovement>();
            if (playerScript != null)
            {
                Vector3 knockbackDir = (playerScript.transform.position - targetGroundPos).normalized;
                knockbackDir.y = 0;

                if (playerScript.isBlocking)
                {
                    playerScript.TakeBlockDamage((int)isBlockinglightningDamage, knockbackDir, lightningPushForce * 0.5f);
                }
                else
                {
                    playerScript.TakeDamage((int)lightningDamage, knockbackDir, lightningPushForce);
                }
                Debug.Log("闪电命中玩家");
                break;
            }
        }
    }
}