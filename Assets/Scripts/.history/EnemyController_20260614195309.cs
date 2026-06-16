using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class BasicEnemyTest : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hit }
    public EnemyState currentState = EnemyState.Idle;

    // ====== 【魂系复活系统】 ======
    // 用一个静态列表记录场景里所有敌人，性能远高于每次去 Find 查找
    public static List<BasicEnemyTest> allEnemies = new List<BasicEnemyTest>();

    private Vector3 initialPosition;     // 记录出生位置
    private Quaternion initialRotation;  // 记录出生朝向
    private bool initialHiddenState;     // 记录最初是否是隐藏状态

    void Awake()
    {
        // 怪物一加载，就把它自己加入到全局名单中
        if (!allEnemies.Contains(this)) allEnemies.Add(this);
    }

    void OnDestroy()
    {
        // 怪物被彻底摧毁时移出名单
        if (allEnemies.Contains(this)) allEnemies.Remove(this);
    }

    [Header("References")]
    public Transform player;

    [Header("战斗特效")]
    public GameObject damageTextPrefab; // 拖入刚才做好的漂字预制体

    // ========锁定聚焦点 =======
    [Header("锁定设置")]
    [Tooltip("创建一个空物体放在敌人胸口，拖到此处。如果不填则默认锁定脚底")]
    public Transform lockOnPoint;

    private Animator anim;
    private CharacterController controller;

    [Header("Settings")]
    public float chaseDistance = 10f;
    public float attackDistance = 2.5f;
    public float walkSpeed = 3f;
    public float runSpeed = 7f;

    [Header("RPG 属性")]
    public float enemyDefense = 20f;       // 敌人的护甲防御力
    public int xpReward = 150;             // 击杀掉落的经验/卢恩
    public int goldReward = 50;            // 击杀掉落的金币

    [Header("生命值")]
    public int maxHealth = 300;
    private int currentHealth;

    [Header("攻击设置")]
    public int attackDamage = 20;

    // 受击设置
    [Header("受击设置")]
    public float knockbackForce = 15f;      // 击退力度（调大）
    public float hitStunDuration = 0.5f;   // 硬直时间
    public bool isSuspended = false;        // 【新增】：是否被击飞滞空（无视重力）
    public bool isDead = false; // 🌟【新增】：标记是否已经彻底死亡
    private bool isHitStunned;              // 是否处于硬直状态
    private float currentStunDuration = 0.5f; // 当前的硬直时长
    private Vector3 knockbackDirection;     // 击退方向
    private Vector3 impact;                 // 冲击力（用于击退）
    

    [Header("UI 设置")]
    public Slider healthSlider;
    public Canvas uiCanvas; // 需要拖入挂载 Slider 的世界空间 Canvas

    [Header("隐藏与出现")]
    public bool startHidden = true;                // 是否初始隐藏
    public float appearDelay = 0f;                 // 出现延迟（秒）
    public bool autoChaseOnAppear = true;          // 出现后是否立即追逐（否则进入巡逻）
    public float detectionRange = 8f;              // 检测玩家的范围（触发器半径）
    public GameObject appearEffect;                // 出现时的特效预制体（可选）
    
    // ====== 【胶囊体修复系统】 ======
    private float originalHeight;
    private float originalRadius;
    private Vector3 originalCenter;
    private float originalStepOffset; // 🌟【新增】记录原版爬阶高度

    // 重力相关
    private float verticalVelocity;
    private float gravity = -9.81f;
    
    // 动画平滑过渡
    private float currentDirection = 0f;
    private float currentSpeed = 0f;

    private Camera playerCamera;

    void Start()
    {
        // 【新增】记录初始状态，为了赐福点复活做准备
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialHiddenState = startHidden;

        anim = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();

        // ✅ 【新增】：记录怪物原本的物理胶囊体大小
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

        // 初始化敌人血条
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // 初始化 UI 相机引用
        if (uiCanvas != null)
        {
            if (uiCanvas.worldCamera != null)
                playerCamera = uiCanvas.worldCamera;
            else
                playerCamera = Camera.main;

            if (playerCamera == null)
                Debug.LogError("找不到相机，血条无法面向相机！");
        }

        if (startHidden)
        {
            SetVisible(false);
            // ✅ 替换为精准禁用控制器：
            if (controller != null) controller.enabled = false;
            
            // 初始状态设为 Idle，不移动
            currentState = EnemyState.Idle;
        }
        else
        {
            // 不隐藏时，默认进入巡逻或追逐
            currentState = autoChaseOnAppear ? EnemyState.Chase : EnemyState.Patrol;
        }

        // 初始血条显隐（根据初始状态）
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(currentState == EnemyState.Chase || currentState == EnemyState.Attack);
        }

        // 可选：确保 Canvas 初始位置在敌人头顶
        // 如果你已经把 Canvas 作为子物体并调整好了局部坐标，这步可以省略
        if (uiCanvas != null)
            uiCanvas.transform.localPosition = new Vector3(0, 2f, 0); // 根据实际调整

    }

    void Update()
    {
        // 安全锁：如果控制器未启用（例如隐藏状态），直接跳过移动和重力
        if (controller == null) return;

        // ========== 🌟 死亡、硬直、滞空专用物理系统 ==========
        if (isHitStunned || currentState == EnemyState.Hit || isDead)
        {
            // 滞空时（例如被挑飞）忽略重力
            if (isSuspended)
            {
                verticalVelocity = 0f;
            }
            else
            {
                // 地面检测重置垂直速度
                if (controller.isGrounded && verticalVelocity < 0)
                    verticalVelocity = 0;

                verticalVelocity += gravity * Time.deltaTime;
                if (verticalVelocity < -20f) verticalVelocity = -20f;
            }

            // ✅ 修改点：将 frameVelocity 改为 velocity
            Vector3 velocity = new Vector3(0, verticalVelocity, 0);

            // 叠加冲击力（击退、击飞）
            if (impact.magnitude > 0.1f)
            {
                velocity += impact;
                // 如果死了，或者受到了巨大的击退力（大招砸地），摩擦力变小（3f），让它滑得极远！普通的击退摩擦力大（10f）
                float decay = (isDead || impact.magnitude > 10f) ? 3f : 10f;
                impact = Vector3.Lerp(impact, Vector3.zero, Time.deltaTime * decay);
            }

            // 物理分流：活着时用 CharacterController，尸体用 Transform 直接移动
            if (controller.enabled)
            {
                controller.Move(velocity * Time.deltaTime);
            }
            else
            {
                transform.position += velocity * Time.deltaTime;

               // 🌟【核心修复 2：无视触发器的射线】防止尸体打到自己的探测圈停在半空！
                if (!isSuspended && verticalVelocity < 0)
                {
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                        verticalVelocity = 0f;
                    }
                }
            }

            UpdateAnimator(0f, 0f);
            return; // 硬直/死亡期间不执行后续任何移动或状态切换
        }

        // ========== 🌟 正常存活状态（Idle / Chase / Attack） ==========
        if (!controller.enabled) return;
        // 重新计算重力（因为硬直分支可能已经修改过 verticalVelocity 但不会影响存活状态）
        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = 0;
        verticalVelocity += gravity * Time.deltaTime;
        if (verticalVelocity < -20f) verticalVelocity = -20f;

        // 当前帧的基础移动向量（只含重力）
        Vector3 frameVelocity = new Vector3(0, verticalVelocity, 0);

        // 叠加冲击力
        if (impact.magnitude > 0.1f)
        {
            frameVelocity += impact;
            impact = Vector3.Lerp(impact, Vector3.zero, Time.deltaTime * 10f);
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateAnimator(0f, 0f);
                controller.Move(frameVelocity * Time.deltaTime);
                // 只有已经出现（非隐藏）且玩家在追逐范围内才切换状态
                if (!startHidden && distanceToPlayer < chaseDistance)
                    currentState = EnemyState.Chase;
                break;

            case EnemyState.Chase:
                FaceTarget(player.position);
                float targetSpeedValue = distanceToPlayer > (attackDistance + 2f) ? 1.5f : 0.5f;
                float moveSpeed = distanceToPlayer > (attackDistance + 2f) ? runSpeed : walkSpeed;

                UpdateAnimator(0f, targetSpeedValue);

                // 追击时叠加向前的速度
                frameVelocity += transform.forward * moveSpeed;
                controller.Move(frameVelocity * Time.deltaTime);

                if (distanceToPlayer <= attackDistance)
                {
                    currentState = EnemyState.Attack;
                    anim.SetTrigger("Attack");
                    UpdateAnimator(0f, 0f);
                }
                else if (distanceToPlayer > chaseDistance)
                {
                    currentState = EnemyState.Idle;
                }
                break;

            case EnemyState.Attack:
                FaceTarget(player.position);
                controller.Move(frameVelocity * Time.deltaTime);
                break;
        }
    }

    void LateUpdate() 
    {
        // 1. 每帧刷新血条显隐（将其从 Update 底部移到了这里，这样就不会被跳过了）
        UpdateHealthBarVisibility();

        // 2. 让血条一直面向玩家的相机
        if (uiCanvas != null && playerCamera != null && uiCanvas.gameObject.activeSelf)
        {   
            uiCanvas.transform.rotation = playerCamera.transform.rotation;
        }
    }

    //===========敌人隐藏机制============
    private void SetVisible(bool visible)
    {
        // 启用/禁用所有渲染器（MeshRenderer, SkinnedMeshRenderer等）
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!startHidden) return;          // 已经出现，不再重复触发
        if (other.CompareTag("Player"))
        {
            StartCoroutine(AppearAndCombat());
        }
    }

    private IEnumerator AppearAndCombat()
    {
        if (appearDelay > 0)
            yield return new WaitForSeconds(appearDelay);

        // 出现特效（如果有）
        if (appearEffect != null)
            Instantiate(appearEffect, transform.position, Quaternion.identity);

        // 显示模型
        SetVisible(true);
        
        // ✅ 替换为精准启用控制器：
        if (controller != null) controller.enabled = true;

        startHidden = false;   // 防止再次触发

        // 决定出现后的状态
        if (autoChaseOnAppear)
        {
            currentState = EnemyState.Chase;
        }
        else
        {
            // 如果需要巡逻，请确保已经实现 Patrol 状态
            currentState = EnemyState.Patrol;
        }
    }

    private void UpdateHealthBarVisibility()
    {
        if (uiCanvas == null) return;
        bool shouldShow = (currentState == EnemyState.Chase || currentState == EnemyState.Attack || currentState == EnemyState.Hit);
        if (uiCanvas.gameObject.activeSelf != shouldShow)
        {
            uiCanvas.gameObject.SetActive(shouldShow);
        }
    }

    // 普通攻击受击（只有水平击退）
    public void TakeDamageWithDirection(Vector3 direction, float force, int rawDamage, int damageType = 0)
    {
        isSuspended = false;
        if (currentHealth <= 0) return;

        // 加上敌人的防御力减伤计算
        float damageReduction = 100f / (100f + enemyDefense);
        int finalDamage = Mathf.RoundToInt(rawDamage * damageReduction);
        finalDamage = Mathf.Max(1, finalDamage);

        currentHealth -= finalDamage;  // 使用最终计算好的伤害扣血
        if (healthSlider != null) healthSlider.value = currentHealth;

        // 生成伤害漂字
        if (damageTextPrefab != null)
        {
            // 在敌人头顶 1.5 米处生成
            Vector3 textPos = transform.position + Vector3.up * 2.0f;
            DamageTextPoolManager.Instance.ShowDamageText(textPos, damage, hitDamageType);
        }

        //Debug.Log($"敌人受到 {finalDamage} 伤害，剩余生命 {currentHealth}/{maxHealth}");
    
        // 必须在死亡 return 之前赋予击退力，这样无论死活，都能吃到物理惯性向后摩擦！
        knockbackDirection = direction;
        knockbackDirection.y = 0;
        impact = knockbackDirection * force; 

        if (currentHealth <= 0)
        {
            // 动态判定死亡动画
            // 检查 damageType，如果是 2 (技能大招伤害)，就传入 true 播放 DieBySkill (击飞倒地)！
            bool isSkillDeath = (damageType == 2);
            Die(isSkillDeath);
            return;
        }

        currentState = EnemyState.Hit;
        isHitStunned = true;

        if (anim != null) anim.SetTrigger("Hit");
        UpdateAnimator(0f, 0f);

        currentStunDuration = hitStunDuration; // 回归默认的短硬直 (0.5秒)

        StopCoroutine("EndHitStun"); 
        StartCoroutine("EndHitStun");
    }

    // 死亡方法
    void Die(bool isSkillDeath = false)
    {
        if (isDead) return;
        isDead = true;

        // ----- 新增：通知任务管理器 -----
        TaskManager tm = FindObjectOfType<TaskManager>();
        if (tm != null) tm.ReportEnemyKilled();

        // 给玩家加经验
        EldenRingMovement playerMovement = player.GetComponent<EldenRingMovement>();
        if (playerMovement != null)
        {
            playerMovement.AddXP(xpReward);
            playerMovement.AddGold(goldReward);
        } 

        if (anim != null)
        {
            if(isSkillDeath) anim.SetTrigger("DieBySkill");
            else anim.SetTrigger("Die");
        }

        currentState = EnemyState.Hit; 
        isHitStunned = true;
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(false);

        // 🌟 彻底关闭所有物理，它将通过 Update 里的 transform 移动，绝不挡路
        if (controller != null) controller.enabled = false;
        CapsuleCollider solidCollider = GetComponent<CapsuleCollider>();
        if (solidCollider != null) solidCollider.enabled = false;

        StartCoroutine(DisableAfterDeath());
    }

    private IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(2.5f); // 等待死亡动画播完
        // 动画播完，尸体停稳后，再彻底关闭物理
        if (controller != null) controller.enabled = false;
        GetComponent<Collider>().enabled = false;
        gameObject.SetActive(false); // 隐藏进内存，等待篝火刷新
    }

    // 技能受击（击飞效果）增加了一个 stunTime 参数，默认 0.5 秒，大招可以传更长的时间
    public void TakeKnockbackWithUp(Vector3 direction, float force, int rawDamage, float upForce = 5f, int damageType = 0, float stunTime = 0.5f)
    {
        if (isDead && upForce >= 0) return; 

        isSuspended = false; // 解除滞空

        if (!isDead)
        {
            float damageReduction = 100f / (100f + enemyDefense);
            int finalDamage = Mathf.RoundToInt(rawDamage * damageReduction);
            finalDamage = Mathf.Max(1, finalDamage);

            currentHealth -= finalDamage;
            if (healthSlider != null) healthSlider.value = currentHealth;

            if (damageTextPrefab != null)
            {
                Vector3 textPos = transform.position + Vector3.up * 2.0f;
                DamageTextPoolManager.Instance.ShowDamageText(textPos, damage, hitDamageType);
            }
        }
        
        knockbackDirection = direction;
        knockbackDirection.y = 0;

        // 必须先剥夺所有物理碰撞体积！防止下面的找地射线打到自己！
        if (stunTime > 1.0f || isDead || upForce < 0) 
        {
            if (controller != null) 
            { 
                controller.enabled = false; 
                controller.height = 0.2f; 
                controller.radius = 0.01f;
                controller.center = new Vector3(0, 0.1f, 0); 
                controller.stepOffset = 0f; 
            }
            CapsuleCollider solidCollider = GetComponent<CapsuleCollider>();
            if (solidCollider != null) solidCollider.enabled = false;
        }


        // 处理大招的“终极砸地坠落”！
        if (upForce < 0)
        {
           // 🌟【核心修复 3：忽略触发器！】完美穿透假碰撞体找到真实的物理地面
           if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit groundHit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (controller != null) controller.enabled = false;
                transform.position = groundHit.point; 
            }
            impact = knockbackDirection * force;
            verticalVelocity = -10f; 
        }
        else
        {
            // 普通击飞
            impact = knockbackDirection * force + Vector3.up * upForce;
        }


        // 死亡判定与动画处理分流   
        if (currentHealth <= 0)
        {
            // 如果它被砸这一下之前就已经死了，为了表现力，强制重播一次倒地被砸的动画！
            if (isDead && anim != null && upForce < 0) anim.SetTrigger("DieBySkill"); 
            Die(true); 
            return;
        }

        //没死
        if (stunTime > 1.0f) anim.SetTrigger("DieBySkill"); 
        else anim.SetTrigger("Hit");

        currentState = EnemyState.Hit;
        isHitStunned = true;
        UpdateAnimator(0f, 0f);

        currentStunDuration = stunTime;
        StopCoroutine(nameof(EndHitStun));
        StartCoroutine(nameof(EndHitStun));
    }

    // 【全新方法】：大招第五段专属击飞（强制滞空 + 播放特殊动画）
     public void TakeLaunchDamage(Vector3 direction, float force, int rawDamage, float upForce = 8f, int damageType = 2)
    {
        // 即使死了也允许被挑飞！这叫硬核鞭尸！
        if (!isDead)
        {
            float damageReduction = 100f / (100f + enemyDefense);
            int finalDamage = Mathf.RoundToInt(rawDamage * damageReduction);
            finalDamage = Mathf.Max(1, finalDamage);

            currentHealth -= finalDamage;
            if (healthSlider != null) healthSlider.value = currentHealth;

            if (damageTextPrefab != null)
            {
                Vector3 textPos = transform.position + Vector3.up * 2.0f;
                DamageTextPoolManager.Instance.ShowDamageText(textPos, finalDamage, damageType);
            }
        }

        // 极限缩小胶囊体半径（Radius），彻底解决玩家跳跃踩头（垫脚石）Bug，压扁胶囊体时必须把 stepOffset 归零
        if (controller != null)
        {
            controller.enabled = false;
            controller.height = 0.2f;
            controller.radius = 0.01f;
            controller.center = new Vector3(0, 0.1f, 0);
            controller.stepOffset = 0f; // 🌟 防止报错
            controller.enabled = true;
        }
        CapsuleCollider solidCollider = GetComponent<CapsuleCollider>();
        if (solidCollider != null) solidCollider.enabled = false;

        knockbackDirection = direction;
        knockbackDirection.y = 0;
        impact = knockbackDirection * force + Vector3.up * upForce;

        currentState = EnemyState.Hit;
        isHitStunned = true;
        isSuspended = true; // 激活悬浮状态

        if (currentHealth <= 0 && !isDead)
        {
            Die(true); // 首次死亡，触发死亡逻辑，但物理引擎会继续把它送上天
            return;
        }

        if (isDead)
        {
            if (anim != null) anim.SetTrigger("DieBySkill"); 
        }
        else
        {
            if (anim != null) anim.SetTrigger("KnockUp"); 
        }

        currentStunDuration = 1.0f; 

        StopCoroutine(nameof(EndHitStun));
        StartCoroutine(nameof(EndHitStun));
    }
    
    
    System.Collections.IEnumerator EndHitStun()
    {
         // 1. 等待两帧，确保 Animator 已经彻底响应了 Trigger 并切入受击/倒地状态
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        float elapsed = 0f;

        // 2. 🌟【核心修复 3：动态双锁检测！】
        // 我们不再死等一个静态的数值，而是每一帧都去查岗！
        while (true)
        {
            elapsed += Time.deltaTime;

            // 获取动画机当前状态
            bool isPlayingHitAnim = false;
            if (anim != null)
            {
                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                // 判断是不是还在播挨打、击飞、倒地动画
                isPlayingHitAnim = state.IsName("Hit") || state.IsName("DieBySkill") || state.IsName("KnockUp");
            }

            // 解除硬直的两个条件同时满足：
            // A. 保底的物理推力时间（currentStunDuration）必须结束，防止推力还没生效就解除
            // B. 受击动画必须播完（已经切回了 Idle 或 Chase 等）
            if (elapsed >= currentStunDuration && !isPlayingHitAnim)
            {
                break; // 动画播完了，立刻跳出循环，绝不发呆！
            }

            // 兜底防卡死锁（如果超过 5 秒强行解锁）
            if (elapsed > 5f) break;

            yield return null;
        }

        // 3. 彻底解除封印
        isHitStunned = false; 
        
        if (currentHealth > 0) 
        {
            currentState = EnemyState.Idle; 
            if (controller != null)
            {
                controller.enabled = false;
                controller.height = originalHeight; 
                controller.radius = originalRadius;
                controller.center = originalCenter;
                controller.stepOffset = originalStepOffset;
                transform.position += new Vector3(0, 0.05f, 0); // 略微防卡
                controller.enabled = true; // 🌟 重新接管双腿
            }
            CapsuleCollider solidCollider = GetComponent<CapsuleCollider>();
            if (solidCollider != null) solidCollider.enabled = true; 
        }
        
        Debug.Log($"敌人硬直彻底结束 (耗时 {elapsed:F2} 秒)，瞬间恢复战斗姿态！");
    }

    // 动画事件：造成伤害
    public void DealDamage()
    {
        // 球形检测参数
        float attackRadius = 1f;  // 与玩家闪避距离匹配
        Vector3 attackPoint = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
    
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint, attackRadius);
        foreach (var hit in hitColliders)
        {
            EldenRingMovement playerScript = hit.GetComponent<EldenRingMovement>();
            if (playerScript != null)
            {
                //计算出怪物推向玩家的纯水平物理方向
                Vector3 knockbackDir = (playerScript.transform.position - transform.position).normalized;
                knockbackDir.y = 0;
                // 假设怪物普攻击退力为 8f
                float enemyPushForce = 8f;

                if (playerScript.isBlocking)
                    playerScript.TakeBlockDamage(attackDamage, knockbackDir, enemyPushForce * 0.5f); // 玩家格挡，退一点点
                else
                    playerScript.TakeDamage(attackDamage, knockbackDir, enemyPushForce); // 玩家没防住，被狠狠击退
                Debug.Log("敌人造成伤害（球形检测）");
                break;
            }
        }
    }

    // 动画事件：攻击结束
    public void OnAttackFinished()
    {
        if (currentState == EnemyState.Hit) return;
        if (isHitStunned) return;
        
        float distance = Vector3.Distance(transform.position, player.position);
        currentState = distance <= attackDistance ? EnemyState.Attack : EnemyState.Chase;
        
        if (currentState == EnemyState.Attack) anim.SetTrigger("Attack");
    }

    // 动画事件：受击结束
    public void OnHitFinished()
    {
        if (isHitStunned) return;
        currentState = EnemyState.Idle;
    }

    private void UpdateAnimator(float targetX, float targetY)
    {
        if (anim == null) return;
        
        currentDirection = Mathf.Lerp(currentDirection, targetX, Time.deltaTime * 5f);
        currentSpeed = Mathf.Lerp(currentSpeed, targetY, Time.deltaTime * 5f);

        anim.SetFloat("Direction", currentDirection);
        anim.SetFloat("Speed", currentSpeed);

        bool isMoving = Mathf.Abs(targetY) > 0.1f || Mathf.Abs(targetX) > 0.1f;
        bool isRunning = Mathf.Abs(targetY) > 1f;

        anim.SetBool("IsMoving", isMoving);
        anim.SetBool("IsRunning", isRunning);
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);
        
        if (Application.isPlaying && knockbackDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, knockbackDirection * 2f);
        }
    }

    // 被玩家在赐福点休息时调用
    public void RespawnEnemy()
    {
        isDead = false;
        StopAllCoroutines(); // 打断所有正在运行的协程（比如还没播完的死亡协程）
        
        gameObject.SetActive(true); // 唤醒模型

        // 篝火复活时，同样恢复物理胶囊体
        if (controller != null)
        {
            controller.enabled = false; 
            controller.height = originalHeight;
            controller.radius = originalRadius;
            controller.center = originalCenter;
            controller.stepOffset = originalStepOffset;
        }
        
        // 恢复生命值
        currentHealth = maxHealth;
        if (healthSlider != null) healthSlider.value = currentHealth;
        
        // 恢复位置和朝向
        if (controller != null) controller.enabled = false; // 瞬移前先关控制器
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        // 恢复初始隐藏逻辑
        startHidden = initialHiddenState;
        if (startHidden)
        {
            SetVisible(false);
            if (controller != null) controller.enabled = false;
            GetComponent<Collider>().enabled = false;
            currentState = EnemyState.Idle;
        }
        else
        {
            SetVisible(true);
            if (controller != null) controller.enabled = true;
            GetComponent<Collider>().enabled = true;
            currentState = autoChaseOnAppear ? EnemyState.Chase : EnemyState.Patrol;
        }

        // 重置动画
        if (anim != null)
        {
            anim.Rebind();
            anim.Play("Idle");
            currentDirection = 0;
            currentSpeed = 0;
            anim.SetFloat("Direction", 0);
            anim.SetFloat("Speed", 0);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
        }

        isHitStunned = false;

        CapsuleCollider solidCollider = GetComponent<CapsuleCollider>();
        if (solidCollider != null) solidCollider.enabled = true;
    }
}