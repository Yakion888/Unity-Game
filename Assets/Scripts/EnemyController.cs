using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI; //引入导航网格库

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))] //强制要求挂载导航组件
public class BasicEnemyTest : MonoBehaviour
{
    public enum EnemyState { Hidden, Idle, Patrol, Chase, Attack, MagicCast, Hit, Dead }
    public EnemyState currentState = EnemyState.Hidden;

    public static List<BasicEnemyTest> allEnemies = new List<BasicEnemyTest>();

    [Header("References")]
    public Transform player;
    public Transform lockOnPoint;

    [Header("潜伏与现身设置")]
    public bool startHidden = true;                
    public float appearDistance = 15f;             // 玩家靠近到多少米时，怪物从草丛跳出来
    public GameObject appearEffect;                
    private bool hasAppeared = false;              

    [Header("巡逻系统 (Waypoints)")]
    public Transform[] patrolPoints;               // 巡逻点数组
    public float patrolWaitTime = 2.0f;            // 到达巡逻点后发呆多久
    private int currentWaypointIndex = 0;
    private float patrolTimer = 0f;

    [Header("感知系统 (Sensory)")]
    public float sightDistance = 12f;              // 视线最远距离
    public float fovAngle = 90f;                   // 视野扇形角度（前方90度）
    public float hearingRadius = 4f;               // 听觉半径（背后靠近也会被发现）
    public LayerMask obstacleMask;                 // 视线阻挡层（墙壁、大石头）

    [Header("战斗与移动参数")]
    public float attackDistance = 2.5f;
    public float walkSpeed = 2f;
    public float runSpeed = 6f;
    public float enemyDefense = 20f;       
    public int xpReward = 150;             
    public int goldReward = 50;            
    public int maxHealth = 300;
    public int attackDamage = 20;

    //闪电攻击
    private MonsterLightningAttack lightningSkill;
    private float lightningCooldownTimer = 0f;
    private float fsmStateTimer = 0f; // AI 防卡死计时器

    [Header("受击与物理")]
    public float knockbackForce = 15f;      
    public float hitStunDuration = 0.5f;   
    public bool isSuspended = false;        
    public bool isDead = false; 
    private bool isHitStunned;              
    private float currentStunDuration = 0.5f; 
    private Vector3 knockbackDirection;     
    private Vector3 impact;                 
    private float verticalVelocity;
    private float gravity = -9.81f;

    [Header("UI 设置")]
    public Slider healthSlider;
    public Canvas uiCanvas; 
    private Camera playerCamera;

    // 内部组件缓存
    private Animator anim;
    private CharacterController controller;
    private NavMeshAgent agent;

    private int currentHealth;
    private float currentSpeed = 0f;
    private float currentDirection = 0f;

    private Vector3 initialPosition;     
    private Quaternion initialRotation;  
    
    // 胶囊体缓存
    private float originalHeight;
    private float originalRadius;
    private Vector3 originalCenter;
    private float originalStepOffset; 

    void Awake()
    {
        if (!allEnemies.Contains(this)) allEnemies.Add(this);
    }

    void OnDestroy()
    {
        if (allEnemies.Contains(this)) allEnemies.Remove(this);
    }

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        anim = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        lightningSkill = GetComponent<MonsterLightningAttack>();

        // 【核心】：接管 NavMeshAgent 的旋转和物理，因为我们有自己的动画和重力
        agent.updatePosition = true; 
        agent.updateRotation = false; // 我们自己写平滑转身

        if (controller != null)
        {
            originalHeight = controller.height;
            originalRadius = controller.radius;
            originalCenter = controller.center;
            originalStepOffset = controller.stepOffset;
        }
        
        if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;
        if (lockOnPoint == null) lockOnPoint = transform;

        currentHealth = maxHealth;
        if (healthSlider != null) { healthSlider.maxValue = maxHealth; healthSlider.value = currentHealth; }

        if (uiCanvas != null)
        {
            playerCamera = uiCanvas.worldCamera != null ? uiCanvas.worldCamera : Camera.main;
            uiCanvas.transform.localPosition = new Vector3(0, 2f, 0); 
        }

        // 初始化潜伏状态
        if (startHidden)
        {
            SetVisible(false);
            TogglePhysics(false);
            currentState = EnemyState.Hidden;
            hasAppeared = false;
        }
        else
        {
            hasAppeared = true;
            currentState = patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
        }
    }

    void Update()
    {
        // 尸体仍需走物理（滑行），但不能走 AI 逻辑
        if (isDead)
        {
            HandleHitPhysics();
            return;
        }

        // 处理硬直物理（击退/击飞时，关闭导航网格，使用原生物理）
        if (isHitStunned || currentState == EnemyState.Hit)
        {
            HandleHitPhysics();
            return;
        }

        if (lightningCooldownTimer > 0) lightningCooldownTimer -= Time.deltaTime;

        if (currentState == EnemyState.Attack || currentState == EnemyState.MagicCast)
        {
            fsmStateTimer += Time.deltaTime;
            // 如果一个攻击动作或施法卡了超过 3 秒，绝对是出 Bug 了！强制打断让他重新追击！
            if (fsmStateTimer > 3.0f) 
            {
                currentState = EnemyState.Chase;
                fsmStateTimer = 0f;
            }
        }
        else 
        {
            fsmStateTimer = 0f; // 不在攻击状态时清零
        }

        // 状态机大脑
        switch (currentState)
        {
            case EnemyState.Hidden:
                UpdateHiddenState();
                break;
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
            case EnemyState.MagicCast: // 处理施法状态
                UpdateMagicCastState();
                break;
        }

        // 同步动画机 (根据 NavMesh 算出的真实移速)
        UpdateAnimator();
    }

    void LateUpdate() 
    {
        bool shouldShowUI = (currentState == EnemyState.Chase || currentState == EnemyState.Attack || currentState == EnemyState.Hit) && hasAppeared && !isDead;
        if (uiCanvas != null && uiCanvas.gameObject.activeSelf != shouldShowUI)
        {
            uiCanvas.gameObject.SetActive(shouldShowUI);
        }

        if (uiCanvas != null && playerCamera != null && uiCanvas.gameObject.activeSelf)
        {   
            uiCanvas.transform.rotation = playerCamera.transform.rotation;
        }
    }

    // ==========================================
    // AI 状态树逻辑
    // ==========================================

    private void UpdateHiddenState()
    {
        if (Vector3.Distance(transform.position, player.position) <= appearDistance)
        {
            StartCoroutine(AppearRoutine());
        }
    }

    private void UpdateIdleState()
    {
        agent.isStopped = true;
        if (CanSeeOrHearPlayer()) 
        {
            currentState = EnemyState.Chase;
        }
    }

    private void UpdatePatrolState()
    {
        if (patrolPoints.Length == 0) return;

        // 如果发现玩家，瞬间切换到追逐！
        if (CanSeeOrHearPlayer())
        {
            currentState = EnemyState.Chase;
            return;
        }

        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.SetDestination(patrolPoints[currentWaypointIndex].position);
        
        // 巡逻时平滑看向自己行进的前方
        if (agent.velocity.sqrMagnitude > 0.1f) FaceTarget(transform.position + agent.velocity);

        // 核心修复：必须用 agent 自身的 stoppingDistance 作为到达判定标准！
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            patrolTimer += Time.deltaTime;
            agent.isStopped = true;

            if (patrolTimer >= patrolWaitTime)
            {
                patrolTimer = 0f;
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolPoints.Length; // 前往下一个点
            }
        }
    }

    private void UpdateChaseState()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // 如果追着追着玩家跑得太远了（脱战），回归巡逻
        if (distToPlayer > sightDistance * 1.5f)
        {
            currentState = patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
            return;
        }

        // 在 Chase 状态下，如果距离超过 8 米，且冷却好了，直接施法！
        if (distToPlayer > 8f && distToPlayer <= 16f && lightningCooldownTimer <= 0 && lightningSkill != null)
        {
            currentState = EnemyState.MagicCast;
            agent.isStopped = true;
            anim.SetTrigger("Cast");
    
            lightningSkill.ExecuteLightningStrike();
            lightningCooldownTimer = 8f; // 8秒冷却
    
            return;
        }

        if (distToPlayer <= attackDistance)
        {
            currentState = EnemyState.Attack;
            agent.isStopped = true;
            anim.SetTrigger("Attack");
        }
        else
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
            FaceTarget(agent.steeringTarget);
        }
    }

    private void UpdateMagicCastState()
    {
        agent.isStopped = true;
        // 细节优化：施法倒计时期间，怪物依然会死死盯着玩家转动身体
        FaceTarget(player.position); 
    }

    private void UpdateAttackState()
    {
        agent.isStopped = true;
        FaceTarget(player.position); // 攻击时死死盯住玩家
    }

    // ==========================================
    // 核心感知系统 (FOV & Hearing)
    // ==========================================
    private bool CanSeeOrHearPlayer()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // 1. 听觉判定：玩家靠得太近，直接察觉
        if (distToPlayer <= hearingRadius) return true;

        // 2. 视觉判定：在视线距离内
        if (distToPlayer <= sightDistance)
        {
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            // 在前方扇形夹角内
            if (angle < fovAngle / 2f)
            {
                // 射线检测：确保中间没有墙壁或大石头挡着！
                Vector3 eyePos = transform.position + Vector3.up * 1.5f;
                Vector3 playerChest = player.position + Vector3.up * 1.2f;
                if (!Physics.Linecast(eyePos, playerChest, obstacleMask))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ==========================================
    // 物理与表现
    // ==========================================
    private IEnumerator AppearRoutine()
    {
        currentState = EnemyState.Idle; // 临时切入空状态防止重复触发
        
        if (appearEffect != null) Instantiate(appearEffect, transform.position, Quaternion.identity);
        SetVisible(true);
        TogglePhysics(true);
        hasAppeared = true;

        // 刚跳出来时，先发呆 1.5 秒营造压迫感，然后进入巡逻
        yield return new WaitForSeconds(1.5f);
        
        // 发呆结束后，如果玩家贴脸了直接追，否则按计划巡逻
        currentState = CanSeeOrHearPlayer() ? EnemyState.Chase : (patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle);
    }

    private void HandleHitPhysics()
    {
        // 硬直和击飞期间，强行关掉导航，交给 CharacterController 处理物理击退！
        if (agent.enabled) agent.enabled = false;

        if (isSuspended)
        {
            verticalVelocity = 0f;
        }
        else
        {
            // ── 主路径：controller 启用 → 用原生 isGrounded 判断落地 ──
            if (controller.enabled)
            {
                if (controller.isGrounded && verticalVelocity < 0)
                    verticalVelocity = 0;
            }
            // ── 【Bug 修复】安全路径：controller 被关闭时（如 TakeKnockbackWithUp 的缩胶囊阶段），
            //     手动射线检测地面，防止 verticalVelocity 无限累积导致敌人沉入地底 ──
            else
            {
                float checkDist = 0.3f;
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, checkDist,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (verticalVelocity < 0)
                        verticalVelocity = 0;
                }
            }

            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = new Vector3(0, verticalVelocity, 0);

        if (impact.magnitude > 0.1f)
        {
            velocity += impact;
            // decay 控制摩擦力：值越小滑行越远。
            // 8f → 5f：半衰期从 0.09s 延长到 0.14s，滑行距离提升约 60%
            float decay = 5f;
            impact = Vector3.Lerp(impact, Vector3.zero, Time.deltaTime * decay);
        }

        if (controller.enabled)
            controller.Move(velocity * Time.deltaTime);
        else
            transform.position += velocity * Time.deltaTime;
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
    }

    private void TogglePhysics(bool isActive)
    {
        if (controller != null) controller.enabled = isActive;
        if (agent != null) agent.enabled = isActive;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = isActive;
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;
        
        // 直接读取 NavMeshAgent 算好的真实速度！
        float targetSpeed = (agent != null && agent.enabled && !agent.isStopped) ? agent.velocity.magnitude : 0f;
        
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);
        anim.SetFloat("Speed", currentSpeed / runSpeed); // 归一化到 0~1 匹配动画树

        bool isMoving = currentSpeed > 0.1f;
        anim.SetBool("IsMoving", isMoving);
        anim.SetBool("IsRunning", currentSpeed > walkSpeed + 0.5f);
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
        }
    }

    // ==========================================
    // 受击与战斗接口 (与原本完全保持一致，零缝合感)
    // ==========================================
    public void TakeDamageWithDirection(Vector3 direction, float force, int rawDamage, int damageType = 0)
    {
        isSuspended = false;
        if (currentHealth <= 0) return;

        float damageReduction = 100f / (100f + enemyDefense);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageReduction));

        currentHealth -= finalDamage;  
        if (healthSlider != null) healthSlider.value = currentHealth;

        if (DamageTextPoolManager.Instance != null)
        {
            DamageTextPoolManager.Instance.ShowDamageText(transform.position + Vector3.up * 2.0f, finalDamage, damageType);
        }

        knockbackDirection = direction;
        knockbackDirection.y = 0;
        impact = knockbackDirection * force; 

        if (currentHealth <= 0)
        {
            Die(damageType == 2);
            return;
        }

        currentState = EnemyState.Hit;
        isHitStunned = true;
        if (anim != null)
        {
            anim.ResetTrigger("Cast"); // 被打断时，清空施法指令
            anim.ResetTrigger("Attack"); 
            anim.SetTrigger("Hit");
        } 

        currentStunDuration = hitStunDuration; 
        StopCoroutine("EndHitStun"); 
        StartCoroutine("EndHitStun");
    }

    public void TakeKnockbackWithUp(Vector3 direction, float force, int rawDamage, float upForce = 5f, int damageType = 0, float stunTime = 0.5f)
    {
        if (isDead && upForce >= 0) return; 
        isSuspended = false; 

        if (!isDead)
        {
            float damageReduction = 100f / (100f + enemyDefense);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageReduction));
            currentHealth -= finalDamage;
            if (healthSlider != null) healthSlider.value = currentHealth;
            if (DamageTextPoolManager.Instance != null) DamageTextPoolManager.Instance.ShowDamageText(transform.position + Vector3.up * 2.0f, finalDamage, damageType);
        }
        
        knockbackDirection = direction;
        knockbackDirection.y = 0;

        if (stunTime > 1.0f || isDead || upForce < 0)
        {
            // 向上击飞才缩小碰撞体（防止飞行中剐蹭），砸地（upForce<0）保持原尺寸以正常滑行
            if (upForce >= 0 && controller != null)
            {
                controller.enabled = false;
                controller.height = 0.2f; controller.radius = 0.01f; controller.center = new Vector3(0, 0.1f, 0); controller.stepOffset = 0f;
            }
            Collider col = GetComponent<Collider>(); if (col != null) col.enabled = false;
        }

        if (upForce < 0)
        {
           if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit groundHit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = groundHit.point;
            }
            impact = knockbackDirection * force;
            verticalVelocity = 0f;

            // 恢复 Collider 全尺寸 → 贴地滑行摩擦力与存活状态完全一致
            if (controller != null)
            {
                controller.height = originalHeight;
                controller.radius = originalRadius;
                controller.center = originalCenter;
                controller.stepOffset = originalStepOffset;
                controller.enabled = true;
            }
        }
        else impact = knockbackDirection * force + Vector3.up * upForce;

        if (currentHealth <= 0)
        {
            if (isDead && anim != null && upForce < 0)
                anim.SetTrigger("DieBySkill"); // 鞭尸：砸地时重播死亡动画反应
            Die(true); return;
        }

        if (anim != null) 
        {
            anim.ResetTrigger("Cast"); // 被打断时，清空施法指令
            anim.ResetTrigger("Attack"); 
        }

        // 活着的敌人用 KnockDown（有出口过渡可恢复），死者用 DieBySkill（无出口过渡永久倒地）
        if (stunTime > 1.0f) anim.SetTrigger("KnockDown"); else anim.SetTrigger("Hit");
        currentState = EnemyState.Hit;
        isHitStunned = true;
        currentStunDuration = stunTime;
        StopCoroutine(nameof(EndHitStun)); StartCoroutine(nameof(EndHitStun));
    }

    private void EndMagicCast()
    {
        if (currentState == EnemyState.MagicCast) currentState = EnemyState.Chase;
    }   

    public void TakeLaunchDamage(Vector3 direction, float force, int rawDamage, float upForce = 8f, int damageType = 2)
    {
        if (!isDead)
        {
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * (100f / (100f + enemyDefense))));
            currentHealth -= finalDamage;
            if (healthSlider != null) healthSlider.value = currentHealth;
            if (DamageTextPoolManager.Instance != null) DamageTextPoolManager.Instance.ShowDamageText(transform.position + Vector3.up * 2.0f, finalDamage, damageType);
        }

        if (controller != null)
        {
            controller.enabled = false; controller.height = 0.2f; controller.radius = 0.01f; controller.center = new Vector3(0, 0.1f, 0); controller.stepOffset = 0f; controller.enabled = true;
        }
        Collider col = GetComponent<Collider>(); if (col != null) col.enabled = false;

        knockbackDirection = direction; knockbackDirection.y = 0;
        impact = knockbackDirection * force + Vector3.up * upForce;
        currentState = EnemyState.Hit; isHitStunned = true; isSuspended = true;

        if (currentHealth <= 0 && !isDead) { Die(true); return; }
        if (anim != null) { if (isDead) anim.SetTrigger("DieBySkill"); else anim.SetTrigger("KnockUp"); }

        currentStunDuration = 1.0f; 
        StopCoroutine(nameof(EndHitStun)); StartCoroutine(nameof(EndHitStun));
    }

    /// <summary>死亡后延迟清理尸体的协程引用，供外部重置计时</summary>
    private Coroutine _deathCleanupCoroutine;

    void Die(bool isSkillDeath = false)
    {
        if (isDead) return;
        isDead = true;

        TaskManager tm = FindObjectOfType<TaskManager>();
        if (tm != null) tm.ReportEnemyKilled();

        EldenRingMovement playerMovement = player.GetComponent<EldenRingMovement>();
        if (playerMovement != null) { playerMovement.AddXP(xpReward); playerMovement.AddGold(goldReward); }

        // 触发死亡动画，让敌人在消失前完整播放倒地动画
        if (anim != null) { if (isSkillDeath) anim.SetTrigger("DieBySkill"); else anim.SetTrigger("Die"); }

        currentState = EnemyState.Hit;
        isHitStunned = true;
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(false);

        // 【Bug 修复 / 鞭尸支持】技能击杀（如大招上挑/斩击）时保留物理组件，
        // 让尸体可以在后续终结斩击中正常被砸地、贴地滑行。
        // 普通死亡照旧关闭物理以节省性能。
        if (!isSkillDeath)
        {
            TogglePhysics(false);
        }

        _deathCleanupCoroutine = StartCoroutine(DisableAfterDeath(2.5f));
    }

    /// <summary>
    /// 【鞭尸支持】重置尸体清理倒计时。
    /// 外部（如 Skill_QTEUltimate）在终结斩击命中 / 砸地后调用，
    /// 确保尸体在滑行期间不会提前消失。
    /// </summary>
    public void ResetDeathCleanupTimer(float delay)
    {
        if (!isDead) return;
        if (_deathCleanupCoroutine != null)
        {
            StopCoroutine(_deathCleanupCoroutine);
            _deathCleanupCoroutine = null;
        }
        _deathCleanupCoroutine = StartCoroutine(DisableAfterDeath(delay));
    }

    private IEnumerator DisableAfterDeath(float delay = 2.5f)
    {
        yield return new WaitForSeconds(delay);
        TogglePhysics(false);
        gameObject.SetActive(false);
        _deathCleanupCoroutine = null;
    }

    System.Collections.IEnumerator EndHitStun()
    {
        yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;
            bool isPlayingHitAnim = false;
            if (anim != null)
            {
                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                isPlayingHitAnim = state.IsName("Hit") || state.IsName("DieBySkill") || state.IsName("KnockUp") || state.IsName("KnockDown");
            }

            if (elapsed >= currentStunDuration && !isPlayingHitAnim) break;
            if (elapsed > 5f) break;
            yield return null;
        }

        isHitStunned = false;
        if (currentHealth > 0)
        {
            // 恢复物理和导航
            TogglePhysics(true);
            if (controller != null)
            {
                controller.enabled = false;
                controller.height = originalHeight; controller.radius = originalRadius; controller.center = originalCenter; controller.stepOffset = originalStepOffset;
                transform.position += new Vector3(0, 0.05f, 0);
                controller.enabled = true;
            }

            // 【Bug 修复】NavMeshAgent 在 HandleHitPhysics 中被禁用（agent.enabled = false）。
            // 重新启用后其内部位置可能仍残留击飞前的高空坐标，导致代理把敌人"拉回"半空。
            // Warp 强制将代理采样到当前 transform.position 的 NavMesh 表面上。
            if (agent != null && agent.enabled && NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }

            // 硬直结束后，直接进入警戒追击状态！
            currentState = EnemyState.Chase;
        }
    }

    // ==========================================
    // 动画事件与其他接口
    // ==========================================
    public void DealDamage()
    {
        float attackRadius = 1f;  
        Vector3 attackPoint = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint, attackRadius);
        foreach (var hit in hitColliders)
        {
            EldenRingMovement playerScript = hit.GetComponent<EldenRingMovement>();
            if (playerScript != null)
            {
                Vector3 knockbackDir = (playerScript.transform.position - transform.position).normalized; knockbackDir.y = 0;
                float enemyPushForce = 8f;
                if (playerScript.isBlocking) playerScript.TakeBlockDamage(attackDamage, knockbackDir, enemyPushForce * 0.5f); 
                else playerScript.TakeDamage(attackDamage, knockbackDir, enemyPushForce); 
                break;
            }
        }
    }

    public void OnAttackFinished()
    {
        if (currentState == EnemyState.Hit || isHitStunned) return;
        
        // 攻击完后，如果玩家还在圈内继续打，否则追！
        currentState = Vector3.Distance(transform.position, player.position) <= attackDistance ? EnemyState.Attack : EnemyState.Chase;
        if (currentState == EnemyState.Attack) anim.SetTrigger("Attack");
    }

    //动画事件：施法动作彻底结束
    public void OnCastFinished()
    {
        // 只有当前确实在施法状态时才处理（防幽灵事件）
        if (currentState == EnemyState.MagicCast)
        {
            // 施法结束，解除定身，切回追击状态！
            currentState = EnemyState.Chase;
        }
    }

    public void OnHitFinished() { /* 逻辑已转移至协程 */ }

    public void RespawnEnemy()
    {
        isDead = false;
        StopAllCoroutines(); 
        gameObject.SetActive(true); 

        if (controller != null)
        {
            controller.enabled = false; 
            controller.height = originalHeight; controller.radius = originalRadius; controller.center = originalCenter; controller.stepOffset = originalStepOffset;
        }
        
        currentHealth = maxHealth;
        if (healthSlider != null) healthSlider.value = currentHealth;
        
        TogglePhysics(false); 
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        if (startHidden)
        {
            SetVisible(false);
            TogglePhysics(false);
            currentState = EnemyState.Hidden;
            hasAppeared = false;
        }
        else
        {
            hasAppeared = true;
            SetVisible(true);
            TogglePhysics(true);
            currentState = patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
        }

        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f); 
            currentDirection = 0; currentSpeed = 0;
            anim.SetFloat("Direction", 0); anim.SetFloat("Speed", 0);
            anim.SetBool("IsMoving", false); anim.SetBool("IsRunning", false);
        }

        isHitStunned = false;
    }

    // 绘制视野扇形和巡逻点辅助线
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, appearDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // 绘制前方扇形视野
        Vector3 leftBoundary = Quaternion.Euler(0, -fovAngle / 2f, 0) * transform.forward * sightDistance;
        Vector3 rightBoundary = Quaternion.Euler(0, fovAngle / 2f, 0) * transform.forward * sightDistance;
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawRay(transform.position + Vector3.up, leftBoundary);
        Gizmos.DrawRay(transform.position + Vector3.up, rightBoundary);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * sightDistance);

        // 画出巡逻路线
        if (patrolPoints != null && patrolPoints.Length > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform p1 = patrolPoints[i];
                Transform p2 = patrolPoints[(i + 1) % patrolPoints.Length];
                if (p1 != null && p2 != null) Gizmos.DrawLine(p1.position, p2.position);
            }
        }
    }
}