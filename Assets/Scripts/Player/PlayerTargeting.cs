using UnityEngine;

/// <summary>
/// 玩家锁定目标系统 —— 从 EldenRingMovement 拆出
///
/// ═══════════════════════════════════════════════════════════
/// 【职责】
///   1. 鼠标中键切换锁定/解锁
///   2. OverlapSphere 找视野前方 60° 内最近的敌人
///   3. 锁定校验（目标死亡/隐藏/超距 → 自动解锁）
///   4. LateUpdate 中驱动锁定 UI 准星跟随
///
/// 【对外接口】
///   CurrentTarget  — 当前锁定的 Transform（null = 未锁定）
///   IsLockedOn     — 是否锁定中
///   EnemyLayer     — 敌人 LayerMask（主脚本攻击判定也复用此值）
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    // ============================================================
    // Inspector
    // ============================================================

    [Header("锁定参数")]
    [Tooltip("最大锁定搜索半径")]
    public float lockOnRadius = 20f;

    [Tooltip("敌人所在 Layer")]
    public LayerMask enemyLayer;

    [Tooltip("鼠标中键切换锁定")]
    public KeyCode lockOnKey = KeyCode.Mouse2;

    [Header("锁定 UI")]
    [Tooltip("锁定准星 RectTransform（跟随敌人屏幕坐标）")]
    public RectTransform lockOnUI;

    // ============================================================
    // 对外只读属性
    // ============================================================

    /// <summary>当前锁定的目标 Transform（null = 未锁定）</summary>
    public Transform CurrentTarget { get; private set; }

    /// <summary>是否处于锁定状态</summary>
    public bool IsLockedOn { get; private set; }

    /// <summary>敌人 LayerMask（供主脚本 attack/skill 的 OverlapSphere 复用）</summary>
    public LayerMask EnemyLayer => enemyLayer;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Start()
    {
        if (lockOnUI != null)
            lockOnUI.gameObject.SetActive(false);
    }

    private void Update()
    {
        HandleLockOnInput();
    }

    private void LateUpdate()
    {
        FollowTargetWithUI();
    }

    // ============================================================
    // 锁定输入
    // ============================================================

    private void HandleLockOnInput()
    {
        // ── 切换锁定 ──
        if (Input.GetKeyDown(lockOnKey))
        {
            if (IsLockedOn)
                ClearLockOn();
            else
                FindLockOnTarget();
        }

        // ── 锁定校验：目标消失 / 超距 → 自动解锁 ──
        if (IsLockedOn)
        {
            if (CurrentTarget == null || !CurrentTarget.gameObject.activeInHierarchy)
            {
                ClearLockOn();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, CurrentTarget.position);
                if (dist > lockOnRadius * 1.5f)
                    ClearLockOn();
            }
        }
    }

    // ============================================================
    // 查找 / 清除
    // ============================================================

    private void FindLockOnTarget()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnRadius, enemyLayer);
        Transform bestTarget = null;
        float minAngle = float.MaxValue;

        foreach (var col in cols)
        {
            BasicEnemyTest enemy = col.GetComponent<BasicEnemyTest>();
            if (enemy == null) continue;
            if (enemy.currentState == BasicEnemyTest.EnemyState.Hidden || enemy.isDead)
                continue;

            Vector3 dirToEnemy = (col.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(Camera.main.transform.forward, dirToEnemy);

            if (angle < 60f && angle < minAngle)
            {
                minAngle = angle;
                bestTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
            }
        }

        if (bestTarget != null)
        {
            CurrentTarget = bestTarget;
            IsLockedOn = true;
            if (lockOnUI != null) lockOnUI.gameObject.SetActive(true);
        }
    }

    private void ClearLockOn()
    {
        IsLockedOn = false;
        CurrentTarget = null;
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }

    // ============================================================
    // UI 跟随
    // ============================================================

    private void FollowTargetWithUI()
    {
        if (!IsLockedOn || CurrentTarget == null || lockOnUI == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(CurrentTarget.position);

        if (screenPos.z < 0)
        {
            lockOnUI.gameObject.SetActive(false);
        }
        else
        {
            lockOnUI.gameObject.SetActive(true);
            lockOnUI.position = screenPos;
        }
    }

    // ============================================================
    // Editor 可视化
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);
    }
}
