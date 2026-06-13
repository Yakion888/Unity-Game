using UnityEngine;
using System.Collections;
using System.Collections.Generic;  
using UnityEngine.UI;
using UnityEngine.Animations.Rigging; // 引入动画绑定库

public class EldenRingMovement : MonoBehaviour
{
   
    // ==============================================================
    // 架构：有限状态机 (FSM)
    // ==============================================================
    public enum ActionState 
    { 
        IdleMove,       // 自由移动与待机
        HeavyAttack,    // 正在重击
        LightAttack,    // 正在轻击
        RunAttack,      // 正在滑行攻击
        SkillCast,      // 正在释放1技能
        Ultimate,       // 正在释放大招
        Dodging,        // 正在闪避
        Hit,            // 正在受击硬直
        Dead            // 死亡
    }

    [Header("玩家当前状态 (FSM)")]
    public ActionState currentState = ActionState.IdleMove;

    // 【代理 wrapper】：让旧逻辑不用改一行代码也能自动读取新状态机！
    public bool isDead => currentState == ActionState.Dead;
    private bool isAttacking => currentState == ActionState.HeavyAttack;
    private bool isLightAttacking => currentState == ActionState.LightAttack;
    private bool isRunningAttack => currentState == ActionState.RunAttack;
    private bool isCasting => currentState == ActionState.SkillCast;
    private bool isUltimateCasting => currentState == ActionState.Ultimate;
    private bool isDodging => currentState == ActionState.Dodging;
    private bool isHit => currentState == ActionState.Hit;

    [Header("战斗设置")]
    public float hitStopDuration = 0.05f;  // 命中停顿时间
    private float lastEventTime = 0f;    //记录上一次触发动画事件的真实时间，用于防抖

    [Header("攻击设置")]
    public float comboInputWindow = 0.3f;
    public bool canMoveWhileAttacking = false;


    [Header("1技能特效")]
    public GameObject skillEffect;               // 技能特效
    public GameObject hitEffect;                 // 命中特效
    public GameObject skillHitEffect;            // 1技能专属的带火属性的命中爆点特效
    public Transform weaponPoint;                // 武器挂载点（剑尖）

    [Header("1技能音效效")]
    public AudioClip castSound;                // 施法音效

    [Header("转场与表现")]
    public CanvasGroup fadeCanvasGroup;  // 拖入刚才做的 FadeBlackScreen
    public float fadeDuration = 1.0f;    // 黑屏渐变的时长
    public bool isResting = false;       // 是否正在休息（用于锁死按键操作）
    public bool isUIOpen = false;        // 是否打开了UI面板

    [Header("太刀双刀流系统")]
    public GameObject swordInHand;         // 挂在右手骨骼下的刀
    public GameObject swordInScabbard;     // 挂在腰部的刀

    [Header("IK 动态控制系统")]
    public TwoBoneIKConstraint leftHandIK; // 拖入 Left_Hand_IK 组件
    private float targetLeftHandIKWeight = 1f; // 目标 IK 权重 (1=握紧, 0=松开)

    // ==============================================================
    // 架构解耦：连接到独立【数据中心】的 API 快捷通道
    // ==============================================================
    public float maxHealth => PlayerDataManager.Instance.maxHealth;
    public float maxStamina => PlayerDataManager.Instance.maxStamina;
    public float attackPowerBonus => PlayerDataManager.Instance.attackPowerBonus;
    public float defensePower => PlayerDataManager.Instance.defensePower;
    public float rageGainMultiplier => PlayerDataManager.Instance.rageGainMultiplier;
    public Vector3 respawnPosition => PlayerDataManager.Instance.respawnPosition;
    public Quaternion respawnRotation => PlayerDataManager.Instance.respawnRotation;

    // 留给外部 UI 或敌人调用的旧接口，直接转发给数据中心！
    public void AddXP(int amount) => PlayerDataManager.Instance.AddXP(amount);
    public void AddGold(int amount) => PlayerDataManager.Instance.AddGold(amount);
    public bool TryLevelUp(string statName) => PlayerDataManager.Instance.TryLevelUp(statName, this);
    public bool UpgradeWeapon() => PlayerDataManager.Instance.UpgradeWeapon();
    public void RewardXP(int amount) => PlayerDataManager.Instance.RewardXP(amount);
    public void RewardGold(int amount) => PlayerDataManager.Instance.RewardGold(amount);

    // --- 补全 RPG 基础数据的读取通道（给 UI 面板用的） ---
    public int currentLevel => PlayerDataManager.Instance.currentLevel;
    public int currentXP => PlayerDataManager.Instance.currentXP;
    public int currentGold => PlayerDataManager.Instance.currentGold;
    
    public int statVigor => PlayerDataManager.Instance.statVigor;
    public int statEndurance => PlayerDataManager.Instance.statEndurance;
    public int statStrength => PlayerDataManager.Instance.statStrength;
    public int statResistance => PlayerDataManager.Instance.statResistance;
    public int statSpirit => PlayerDataManager.Instance.statSpirit;
    
    public int weaponLevel => PlayerDataManager.Instance.weaponLevel;
    public string weaponName => PlayerDataManager.Instance.weaponName;
    public float weaponBaseAttack=> PlayerDataManager.Instance.weaponBaseAttack;
    public float upgradeAttackBonus=> PlayerDataManager.Instance.upgradeAttackBonus;

    public int GetXPRequirementForNextLevel() => PlayerDataManager.Instance.GetXPRequirementForNextLevel();
    
    [Header("技能系统")]
    public int castDamage = 100;
    public float castDuration = 3.0f;  // 施法动画时长
    public float castDamageScalingMultiplier = 2.5f; //大招的力量加成倍率 (比如 2.0 代表享受 200% 的攻击力加成)
    public float castKnockbackForce = 15f; //// 水平击退
    public float castKnockupForce = 15f;  //浮空力度
    public float castRadius = 3f;  // 技能范围
    public float skillWavePushForce = 6f;  // 剑气每段的水平推力（建议4~6，用来匹配剑气飞行速度）
    public float skillWaveUpForce = 2f;    // 剑气每段的微小浮空力（给个2f能抵消地面摩擦力，让怪更丝滑地跟着飞）
    public float skillWaveLifetime = 1.5f; // 剑气物理存活时间（默认1.5秒）

    [Header("终极大招 (QTE系统)")]
    public int ultimateSlashBaseDamage = 30;  // 前四段上挑的基础伤害（通常比最后一下低，用来打连击）
    public int ultimateBaseDamage = 100;      // 大招基础伤害
    public float ultimateQTEBonus = 3.0f;     // QTE成功后的伤害倍率 (比如4倍！)
    public float ultimateLaunchForce = 12f;   // 第五段的挑飞力度
    public float ultimateSlamForce = -20f;    // 最后砸地的下坠力度
    private bool isWaitingForQTE = false;     // 是否正处于子弹时间等待按键
    private bool qteSuccess = false;          // QTE是否按成功了

    // ==============================================================
    // 数据驱动架构：当前装备的武器数据包
    // ==============================================================
    [Header("当前装备武器")]
    public WeaponDataSO currentWeapon;

    // --- 魔法代理：让旧代码无缝读取 SO 数据包里的配置 ---
    public int[] heavyAttackDamage => currentWeapon.heavyAttackDamage;
    public int[] lightAttackDamage => currentWeapon.lightAttackDamage;
    public int runningAttackDamage => currentWeapon.runningAttackDamage;

    public float[] lightAttackForwardOffset => currentWeapon.lightAttackForwardOffset;
    public float[] lightAttackRadius => currentWeapon.lightAttackRadius;
    public float[] lightAttackAngle => currentWeapon.lightAttackAngle;

    public GameObject[] heavyAttackEffects => currentWeapon.heavyAttackEffects;
    public GameObject[] lightAttackEffects => currentWeapon.lightAttackEffects;
    public GameObject runningAttackEffect => currentWeapon.runningAttackEffect;

    public float[] heavyAttackKnockback => currentWeapon.heavyAttackKnockback;
    public float[] lightAttackKnockback => currentWeapon.lightAttackKnockback;
    public float runningAttackKnockback => currentWeapon.runningAttackKnockback;

    public float[] heavyAttackStaminaCost => currentWeapon.heavyAttackStaminaCost;
    public float runningAttackStaminaCost => currentWeapon.runningAttackStaminaCost;

    public float lightAttackRage => currentWeapon.lightAttackRage;
    public float heavyAttackRage => currentWeapon.heavyAttackRage;
    public float runningAttackRage => currentWeapon.runningAttackRage;

    public AudioClip[] attackSwingSounds => currentWeapon.attackSwingSounds;
    public AudioClip[] attackHitSounds => currentWeapon.attackHitSounds;
    public AudioClip[] lightAttackSwingSounds => currentWeapon.lightAttackSwingSounds;
    public AudioClip[] lightAttackHitSounds => currentWeapon.lightAttackHitSounds;
    public AudioClip slidingWhooshSound => currentWeapon.slidingWhooshSound;

    public AudioClip[] heavyAttackVoices => currentWeapon.heavyAttackVoices;
    public AudioClip[] lightAttackVoices => currentWeapon.lightAttackVoices;
    public AudioClip[] runningAttackVoices => currentWeapon.runningAttackVoices;


    [Header("终极大招音效 (QTE与连段)")]
    public AudioClip ultChargeSFX;          // 蓄力音效（按下大招瞬间播放）
    public AudioClip[] ultUpwardSlashSFXs;  // 4段上挑音效（建议容量填4，放入4个不同的破风声）
    public AudioClip ultSlowMotionSFX;      // 空中滞留慢放音效（子弹时间高频耳鸣声）
    public AudioClip ultQTESuccessSFX;      // QTE按键成功音效（清脆的“叮”声，若空则默认用完美闪避音效）
    public AudioClip ultSlamSFX;            // 终结重击砸地音效（沉重的爆炸/砸地声）
    public AudioClip ultHitSFX;             // 大招刀刃砍进肉里的固定受击音效

    [Header("终极大招特效")]
    public GameObject[] ultSlashEffects; // 前 4 段的不同角度剑光
    public GameObject ultLaunchEffect;   // 第 5 段的垂直升龙剑光！
    public GameObject ultFinalSlashEffect; //QTE 终结下劈的专属剑光
    public GameObject ultSlamEffect;     // 砸地爆发特效
    public GameObject ultHitEffect;


    [Header("闪避设置")]
    public float dodgeDistance = 3f;           // 闪避位移距离
    public float dodgeDuration = 0.4f;         // 闪避动画时长/无敌时长
    public float dodgeStaminaCost = 25f;       // 闪避消耗耐力
      
    [Header("音效设置")]
    public AudioSource audioSource;
    public AudioClip[] blockSounds;            // 格挡音效
    public AudioClip[] playerHitSounds;        // 玩家受击音效
    public AudioClip perfectDodgeStartSFX;   // 启动音效（一播放）
    

    [Header("角色语音")]
    public AudioClip[] skillVoices;         // 技能语音数组（可选）
    public AudioClip deathSFX;              // 死亡语音


    [Header("战斗音乐冷却")]
    public float combatCooldownDuration = 2f;   // 脱战冷却时间（秒）
    private float combatCooldownTimer = 0f;
    private bool isInCombatEffective = false;   // 经过冷却过滤后的实际战斗状态

    // ======= 锁定系统 =======
    [Header("锁定设置")]
    public float lockOnRadius = 20f;
    public LayerMask enemyLayer;
    public Transform lockedTarget;
    public bool isLockedOn;

    [Header("锁定UI设置")]
    public RectTransform lockOnUI;
    
    
    // 攻击相关
    private bool comboPending;
    private float comboPendingTime;
    private bool isProcessingAttackEnd;
    private bool lightComboPending;
    private float lightComboPendingTime;
    public bool isBlocking;
    private int currentAttackCombo = 0;   // 当前重攻击段数（1~5）

    // 技能相关
    private float castStartTime;
    private bool isCastingInvincible;   // 施法霸体状态

    
    // 受击相关
    private float hitRecoveryTime;
    public Vector3 impact;
    
    private PlayerLocomotionManager locomotion; // 物理移动引擎
    
    // 动画相关
    private PlayerAnimatorHandler animHandler; //动画管家
    // 让底层成百上千行的攻击逻辑、动画事件，以为 anim 还在自己身上！
    public Animator anim => animHandler.anim;
    public int attackLayerIndex => animHandler.attackLayerIndex;

    private CharacterController controller;
    private Quaternion targetRotation;
    private float currentTurnAngle;


    private PlayerStatsManager stats; // 状态与UI管家
    // ==============================================================
    //  代理：读写双向绑定，主脚本完美操控新管家的数据
    // ==============================================================
    public float currentHealth { get => stats.currentHealth; set => stats.currentHealth = value; }
    public float currentStamina { get => stats.currentStamina; set => stats.currentStamina = value; }
    public float currentRage { get => stats.currentRage; set => stats.currentRage = value; }
    public float maxRage => stats.maxRage;
    
    public float staminaBlockRemaining { get => stats.staminaBlockRemaining; set => stats.staminaBlockRemaining = value; }
    public float staminaRegenTimer { get => stats.staminaRegenTimer; set => stats.staminaRegenTimer = value; }
    public float staminaRegenBuffTimer { get => stats.staminaRegenBuffTimer; set => stats.staminaRegenBuffTimer = value; }
    public float sprintStaminaCost => stats.sprintStaminaCost;
    public float STAMINA_BLOCK_DURATION => stats.STAMINA_BLOCK_DURATION;

    public Slider healthSlider => stats.healthSlider;
    public Slider staminaSlider => stats.staminaSlider;
    public Slider rageSlider => stats.rageSlider;

    public bool ConsumeStamina(float amount) => stats.ConsumeStamina(amount);


    // 闪避增强机制
    private bool isInvincible = false;          // 是否处于无敌状态
    private float dodgeStartTime = 0f;          // 闪避开始的时间戳
    private bool nextAttackIsCrit = false;      // 下一次攻击是否必定暴击
    private bool nextHeavyAttackIsFourth = false;   // 下一次重击是否直接变成第四段

    //战斗状态缓存
    private bool isInCombatCached = false;
    private float combatCheckTimer = 0f;

    // ======= 【重构新增】缓存的每帧输入与状态 =======
    private float hInput;
    private float vInput;
    private bool runInput;
    private bool hasMoveInput;
    private bool isCurrentlyRunning;
    private Vector3 targetMoveDirection;
    private bool isGroundedCached;
    private PlayerInputHandler inputHandler; 
    
    void Start()
    {
        // ==========================================
        // 1. 【核心修复】：必须最先抓取所有管家组件！
        // 否则后续读档、赋值时会报 NullReferenceException 卡死游戏！
        // ==========================================
        inputHandler = GetComponent<PlayerInputHandler>(); 
        animHandler = GetComponent<PlayerAnimatorHandler>();
        stats = GetComponent<PlayerStatsManager>();
        
        // 初始化动画管家
        animHandler.Initialize();

        // 2. 必须先抓取 controller，再传给管家
        controller = GetComponent<CharacterController>();

        //初始化物理引擎
        locomotion = GetComponent<PlayerLocomotionManager>();
        locomotion.Initialize(this, inputHandler, animHandler, stats, controller);

        // 3. UI与鼠标设置
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // ==========================================
        // 4. 数据中心接管与初始化
        // ==========================================
        PlayerDataManager.Instance.LoadGame(this); 

        // 读完档算出真正的最大血量后，再把当前血量和耐力回满
        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentRage = 0f; 

        // 5. 初始化滑动条的最大值 (Slider Max Values)
        if (healthSlider != null) healthSlider.maxValue = maxHealth;
        if (staminaSlider != null) staminaSlider.maxValue = maxStamina;
        if (rageSlider != null) rageSlider.maxValue = maxRage;

        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = 1f;
        audioSource.dopplerLevel = 0f;

        // 7. 瞬间重置相机到背后
        GetComponent<PlayerCameraController>().ResetCameraBehindPlayer();
    }
    
    void Update()
    {
        if (isDead || isResting) return; // 死亡或休息时锁死逻辑

        // 1. 处理系统与UI状态
        HandleSystemAndUIState();

        // 2. 收集玩家所有的按键输入
        ReadPlayerInput();


        // 4. 处理移动、重力与跳跃
        locomotion.HandleLocomotionAndGravity();

        // 5. 处理战斗输入（攻击、技能、闪避、格挡）
        HandleActionInputs();

        // 6. 处理状态更新与计时器（耐力、连击、急停等）
        HandleStatsAndTimers();

        // 7. 处理音效与IK动画表现
        HandleAudioAndIK();
    }

    // =========================================================
    // 【架构重构】以下为从原 Update 抽离出的独立功能模块
    // =========================================================
    private void HandleSystemAndUIState()
    {
        // UI 鼠标状态
        if (isUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleLockOnInput();

        // 战斗状态与BGM检测
        combatCheckTimer -= Time.deltaTime;
        if (combatCheckTimer <= 0f)
        {
            isInCombatCached = IsInCombat();
            combatCheckTimer = 0.5f;
        }

        if (isInCombatCached)
        {
            combatCooldownTimer = combatCooldownDuration;
            if (!isInCombatEffective)
            {
                isInCombatEffective = true;
                if (AudioManager.Instance != null) AudioManager.Instance.SetCombatState(true, true);
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetCombatState(true, false);
            }
        }
        else
        {
            if (combatCooldownTimer > 0f) combatCooldownTimer -= Time.deltaTime;
            else if (isInCombatEffective)
            {
                isInCombatEffective = false;
                if (AudioManager.Instance != null) AudioManager.Instance.SetCombatState(false, false);
            }
        }
    }

    private void ReadPlayerInput()
    {
        // 直接从输入管家读取，抛弃旧变量
        hInput = inputHandler.MoveInput.x;
        vInput = inputHandler.MoveInput.y;
        runInput = inputHandler.RunInput;

        hasMoveInput = inputHandler.MoveInput.magnitude > 0.1f && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting && !isDodging;
        isCurrentlyRunning = runInput && hasMoveInput && Mathf.Abs(vInput) > 0.1f;
    }


    

    private void HandleActionInputs()
    {
        // 闪避
        if (inputHandler.DodgeInput && !isDodging && !isAttacking && !isLightAttacking && !isCasting && !isHit && !isBlocking)
        {
            TryDodge();
        }

        // 格挡
        bool wasBlocking = isBlocking;
        isBlocking = inputHandler.BlockInput;
        if (isBlocking != wasBlocking)
        {
            if (isBlocking) GetComponent<IdleSelector>()?.ResetIdleTimer();
            else
            {
                anim.SetFloat("IdleIndex", 0f);
                GetComponent<IdleSelector>()?.ResetIdleTimer();
            }
        }
        anim.SetBool("IsBlocking", isBlocking);

        // 攻击
        if (inputHandler.HeavyAttackInput && !isBlocking && !isHit && !isDodging && !isCasting && !isUltimateCasting)
        {
            if (!isAttacking && !isLightAttacking && !isRunningAttack)
            {
                if (isCurrentlyRunning) StartRunningAttack();
                else StartAttack();
            }
            else if (isAttacking && !isRunningAttack)
            {
                if (!anim.GetCurrentAnimatorStateInfo(attackLayerIndex).IsName("Attack5"))
                {
                    anim.SetTrigger("Combo");
                    comboPending = true;
                    comboPendingTime = comboInputWindow;
                }
            }
        }
        else if (inputHandler.LightAttackInput && !isBlocking && !isHit && !isDodging && !isCasting)
        {
            if (!isAttacking && !isLightAttacking && !isRunningAttack) StartLightAttack();
            else if (isLightAttacking)
            {
                if (!anim.GetCurrentAnimatorStateInfo(attackLayerIndex).IsName("LightAttack3"))
                {
                    anim.SetTrigger("LightCombo");
                    lightComboPending = true;
                    lightComboPendingTime = comboInputWindow;
                }
            }
        }

        // 技能
        if (inputHandler.SkillInput && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting)
        {
            if (currentRage >= maxRage)
            {
                currentRage = 0f;
                if (rageSlider != null) rageSlider.value = currentRage;
                StartCast();
            }
        }

        // 大招
        if (inputHandler.UltimateInput && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting && !isUltimateCasting)
        {
            StartUltimate();
        }

        // QTE
        if (isWaitingForQTE && inputHandler.HeavyAttackInput && !qteSuccess)
        {
            TriggerQTESuccess();
        }
    }

    private void HandleStatsAndTimers()
    {
        // 交给物理引擎处理急停判定
        locomotion.HandleStopTimers(hasMoveInput);

        // 连击计时
        if (comboPendingTime > 0)
        {
            comboPendingTime -= Time.deltaTime;
            if (comboPendingTime <= 0) comboPending = false;
        }
        if (lightComboPendingTime > 0)
        {
            lightComboPendingTime -= Time.deltaTime;
            if (lightComboPendingTime <= 0) lightComboPending = false;
        }

        // 动画状态与UI同步 (读取物理引擎算好的数据)
        animHandler.SyncLocomotionStates(hasMoveInput, locomotion.isRunning, locomotion.isGroundedCached, locomotion.isStopping, isAttacking, isLightAttacking, isUltimateCasting);
        UpdateAnimationValues(hasMoveInput);

        // 耐力与奔跑耗能逻辑
        if (stats.staminaBlockRemaining > 0f)
        {
            stats.staminaBlockRemaining -= Time.deltaTime;
            // 强行锁死奔跑
            locomotion.ResetSpeed(); 
        }
        else if (locomotion.isRunning && hasMoveInput && !isAttacking && !isLightAttacking && !isCasting && isInCombatCached)
        {
            float sprintCost = stats.sprintStaminaCost * Time.deltaTime;
            if (currentStamina >= sprintCost)
            {
                currentStamina -= sprintCost;
                stats.staminaRegenTimer = 0f;
            }
            else
            {
                stats.staminaBlockRemaining = stats.STAMINA_BLOCK_DURATION;
                locomotion.ResetSpeed();
            }
        }

        if (!isInCombatCached || (!locomotion.isRunning && !isAttacking && !isLightAttacking))
        {
            stats.staminaRegenTimer += Time.deltaTime;
            stats.RegenerateStamina(maxStamina); 
        }

        stats.UpdateUIBarTexts(maxHealth, maxStamina);
    }

    private void HandleAudioAndIK()
    {
        // 交给物理引擎播脚步声
        locomotion.HandleFootsteps(hasMoveInput);

        if (leftHandIK != null)
        {
            leftHandIK.weight = Mathf.Lerp(leftHandIK.weight, targetLeftHandIKWeight, Time.deltaTime * 15f);
        }
    }
    

    // ======= 锁定目标逻辑 =======
    void HandleLockOnInput()
    {
        if (inputHandler.LockOnInput) // 鼠标中键
        {
            if (isLockedOn) ClearLockOn();
            else FindLockOnTarget();
        }

        if (isLockedOn)
        {
            if (lockedTarget == null || !lockedTarget.gameObject.activeInHierarchy)
            {
                ClearLockOn();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, lockedTarget.position);
                if (dist > lockOnRadius * 1.5f) ClearLockOn();
            }
        }
    }

    void FindLockOnTarget()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnRadius, enemyLayer);
        Transform bestTarget = null;
        float minAngle = float.MaxValue;

        foreach (var col in cols)
        {
            BasicEnemyTest enemy = col.GetComponent<BasicEnemyTest>();

            if (enemy != null && !enemy.startHidden && !enemy.isDead && enemy.currentState != BasicEnemyTest.EnemyState.Hit) // 可根据需求放宽条件
            {
                Vector3 dirToEnemy = (col.transform.position - transform.position).normalized;
                // 优先锁定屏幕视野前方的敌人
                float angle = Vector3.Angle(Camera.main.transform.forward, dirToEnemy);

                if (angle < 60f && angle < minAngle)
                {
                    minAngle = angle;
                    // 如果敌人身上配置了专门的锁定点（比如胸口），则锁定该点
                    bestTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                }
            }
        }

        if (bestTarget != null)
        {
            lockedTarget = bestTarget;
            isLockedOn = true;
            //Debug.Log("锁定目标: " + bestTarget.parent?.name);
            // 显示UI
            if (lockOnUI != null) lockOnUI.gameObject.SetActive(true);
        }
    }

    void ClearLockOn()
    {
        isLockedOn = false;
        lockedTarget = null;
        //Debug.Log("解除锁定");
        // ======= 新加：隐藏UI =======
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }
    
    // ======= 新加：让UI实时跟随目标 =======
    void LateUpdate()
    {
        if (isLockedOn && lockedTarget != null && lockOnUI != null)
        {
            // 将锁定的3D世界坐标，转换为屏幕上的2D坐标
            Vector3 screenPos = Camera.main.WorldToScreenPoint(lockedTarget.position);

            // screenPos.z 代表物体在相机前方还是后方
            // 如果 z < 0 说明敌人在相机背后，此时虽然锁定了但为了不出现UI乱飞，可以暂时隐藏
            if (screenPos.z < 0)
            {
                lockOnUI.gameObject.SetActive(false);
            }
            else
            {
                lockOnUI.gameObject.SetActive(true);
                // 更新UI的位置
                lockOnUI.position = screenPos;
            }
        }
    }
    

    void UpdateAnimationValues(bool hasMoveInput)
    {
        if (animHandler == null || anim == null) return;
        if (isAttacking || isLightAttacking) return;

        // 跳跃期间锁定动画参数
        if (locomotion.isJumping)
        {
            animHandler.SetSpeedDirectly(locomotion.jumpStartSpeed);
            return;
        }    

        if (isLockedOn)
        {
            float targetDir = inputHandler.MoveInput.x; 
            float speedMag = locomotion.currentSpeed / locomotion.runSpeed; 
            float targetSpeedAnim = 0f;
            
            if (inputHandler.MoveInput.y != 0) targetSpeedAnim = inputHandler.MoveInput.y > 0 ? speedMag : -speedMag; 
            else if (inputHandler.MoveInput.x != 0) targetSpeedAnim = speedMag;

            animHandler.SyncMovementValues(targetSpeedAnim, targetDir, 10f);
        }
        else
        {
            float targetAnimSpeed = 0f;
            if (hasMoveInput)
            {
                float speedPercent = locomotion.currentSpeed / locomotion.runSpeed;
                targetAnimSpeed = Mathf.Lerp(0.3f, 1f, speedPercent);
            }
            
            // 从物理引擎获取转角并同步
            animHandler.SyncMovementValues(targetAnimSpeed, locomotion.currentTurnAngle, 50f);
        }
    }
    

    // ========== 攻击方法 ==========
    
    void StartRunningAttack()
    {
        if (!ConsumeStamina(runningAttackStaminaCost)) return;
        
        comboPending = false;
        comboPendingTime = 0;
        
        // 核心：一键切换到滑行攻击状态
        currentState = ActionState.RunAttack;
        
        anim.Play("RunningAttack", 0, 0f);
        if (controller != null) controller.Move(Vector3.zero);
        Invoke("ForceEndRunningAttack", 1.5f);
    }
    
    void ForceEndRunningAttack()
    {
        if (currentState == ActionState.RunAttack)
        {
            StartCoroutine(SmoothTransitionToIdle());
        }
    }
    
    System.Collections.IEnumerator SmoothTransitionToIdle()
    {
        yield return new WaitForSeconds(0.05f);
        anim.CrossFade("IdleSelector", 0.15f, 0, 0f);
        
        // 核心：收招，恢复自由状态！
        currentState = ActionState.IdleMove;
        
        GetComponent<IdleSelector>()?.ResetIdleTimer();
    }
    
    void StartAttack()
    {
        int attackComboIndex = nextHeavyAttackIsFourth ? 4 : 1;
        float staminaCost = heavyAttackStaminaCost[attackComboIndex - 1];
        if (!ConsumeStamina(staminaCost)) return;

        if (nextHeavyAttackIsFourth)
        {
            nextHeavyAttackIsFourth = false;  
            lightComboPending = false;
            lightComboPendingTime = 0;
            comboPending = false;
            comboPendingTime = 0;
            
            // 核心：一键切换到重击状态
            currentState = ActionState.HeavyAttack;
            currentAttackCombo = 4;   
            
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("LightCombo");
            anim.SetLayerWeight(attackLayerIndex, 1f);
            
            anim.Play(Animator.StringToHash("Attack4"), attackLayerIndex, 0f);
            if (controller != null) controller.Move(Vector3.zero);
            return;
        }

        currentAttackCombo = 1;     
        lightComboPending = false;
        lightComboPendingTime = 0;
        comboPending = false;
        comboPendingTime = 0;
        
        // 核心：一键切换到重击状态
        currentState = ActionState.HeavyAttack;
        
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Combo");
        anim.ResetTrigger("LightAttack");
        anim.ResetTrigger("LightCombo");
        anim.SetLayerWeight(attackLayerIndex, 1f);
        
        anim.Play(Animator.StringToHash("Attack1"), attackLayerIndex, 0f);
        if (controller != null) controller.Move(Vector3.zero);
    }
    
    void StartLightAttack()
    {
        if (attackLayerIndex < 0) return;
        
        lightComboPending = false;
        lightComboPendingTime = 0;
        comboPending = false;
        comboPendingTime = 0;
        
        // 核心：一键切换到轻击状态
        currentState = ActionState.LightAttack;
        
        anim.SetFloat("IdleIndex", 0f);
        anim.ResetTrigger("LightAttack");
        anim.ResetTrigger("LightCombo");
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Combo");
        
        anim.SetLayerWeight(attackLayerIndex, 1f);
        anim.Play("LightAttack1", attackLayerIndex, 0f);
        
        if (controller != null) controller.Move(Vector3.zero);
    }
    
    public void OnAttackFinished()
    {
        // 核心防卫：如果当前根本不是攻击状态，直接无视错误的动画事件！
        if (currentState != ActionState.HeavyAttack && 
            currentState != ActionState.LightAttack && 
            currentState != ActionState.RunAttack) return;

        if (isProcessingAttackEnd) return;
        isProcessingAttackEnd = true;
        
        if (currentState == ActionState.RunAttack)
        {
            StartCoroutine(SmoothTransitionToIdle());
            isProcessingAttackEnd = false;
            return;
        }
        
        if (anim == null) { isProcessingAttackEnd = false; return; }
        
        var stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        
        // ====== 轻击收招处理 ======
        if (stateInfo.IsName("LightAttack3"))
        {
            currentState = ActionState.IdleMove; // 解除状态
            lightComboPending = false;
            lightComboPendingTime = 0;
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            GetComponent<IdleSelector>()?.ResetIdleTimer();
            isProcessingAttackEnd = false;
            return;
        }
        else if (stateInfo.IsName("LightAttack1") || stateInfo.IsName("LightAttack2"))
        {
            if (lightComboPendingTime <= 0 && !lightComboPending)
            {
                currentState = ActionState.IdleMove; // 解除状态
                if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
                anim.SetFloat("IdleIndex", 0f);
            }
            isProcessingAttackEnd = false;
            return;
        }
        
        // ====== 重击收招处理 ======
        if (stateInfo.IsName("Attack5"))
        {
            currentState = ActionState.IdleMove; // 解除状态
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            GetComponent<IdleSelector>()?.ResetIdleTimer();
        }
        else if (comboPending)
        {
            int nextCombo = currentAttackCombo + 1;   
            float staminaCost = (nextCombo <= heavyAttackStaminaCost.Length) 
                            ? heavyAttackStaminaCost[nextCombo - 1] 
                            : heavyAttackStaminaCost[heavyAttackStaminaCost.Length - 1]; 
            
            if (!ConsumeStamina(staminaCost))
            {
                comboPending = false;
                comboPendingTime = 0;
                currentState = ActionState.IdleMove; // 耐力不足，强行解除状态
                if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
                anim.SetFloat("IdleIndex", 0f);
                GetComponent<IdleSelector>()?.ResetIdleTimer();
            }
            else
            {
                anim.SetTrigger("Combo");
                comboPending = false;
                comboPendingTime = 0;
                currentAttackCombo++;   // 保持 HeavyAttack 状态，进入下一段！
            }  
        }
        else
        {
            currentState = ActionState.IdleMove; // 玩家没有预输入，正常解除状态！
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            GetComponent<IdleSelector>()?.ResetIdleTimer();
        }
        
        isProcessingAttackEnd = false;
    }
    

    // ========== 攻击命中检测 ========== 
    public void CheckAttackHit()
    {
        // 如果距离上一次触发还不到 0.1 秒（真实时间），直接无视它（防动画引擎抽风双重调用）
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime; 

        // 防幽灵伤害判定
        if (!isAttacking && !isLightAttacking && !isRunningAttack) return;

        // 1. 获取当前攻击的击退力和基础伤害
        float knockbackForce = GetCurrentKnockbackForce();
        int damage = GetCalculatedDamage();
        int hitDamageType = 0;  

        // 应用完美闪避后的暴击奖励
        if (nextAttackIsCrit)
        {
            damage = Mathf.RoundToInt(damage * 1.65f);
            hitDamageType = 1;  
            nextAttackIsCrit = false;
        }
   
        // ==============================================================
        // 2. 【核心优化】根据不同的攻击流派，使用不同的判定模型收集敌人
        // ==============================================================
        List<Collider> validHits = new List<Collider>(); // 用于存放真正被命中的敌人

        if (isLightAttacking)
        {
            // 1. 获取当前正在播放的轻击是第几段
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
            int comboIndex = 0; // 默认为第一段 (Index 0)
        
            if (stateInfo.IsName("LightAttack2")) comboIndex = 1;
            else if (stateInfo.IsName("LightAttack3")) comboIndex = 2;

            // 2. 从数组中安全地读取对应的范围参数（增加防越界保护）
            float currentOffset = (comboIndex < lightAttackForwardOffset.Length) ? lightAttackForwardOffset[comboIndex] : 1.0f;
            float currentRadius = (comboIndex < lightAttackRadius.Length) ? lightAttackRadius[comboIndex] : 0.6f;
            float currentAngle = (lightAttackAngle != null && comboIndex < lightAttackAngle.Length) ? lightAttackAngle[comboIndex] : 60f;

            // 3. 应用动态计算出的中心点与半径
            Vector3 lightAttackCenter = transform.position + transform.forward * currentOffset + Vector3.up * 1f;
        
            // 免疫隐形触发器
            Collider[] hits = Physics.OverlapSphere(lightAttackCenter, currentRadius, enemyLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
                dirToEnemy.y = 0;
                Vector3 playerForward = transform.forward;
                playerForward.y = 0;
            
                // 应用读取的角度
                 if (Vector3.Angle(playerForward, dirToEnemy) <= currentAngle)
                {
                    validHits.Add(hit);
                }
            }
        }
        else if (isAttacking)
        {
            // 【大剑重击】: 大范围宽阔斩击
            // 特点：中心点偏远，半径极大，但引入【扇形过滤】防止“屁股砍人”
            Vector3 heavyAttackCenter = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
            float heavyRadius = 3.5f; // 大剑很长，给足3.5米半径
        
            Collider[] hits = Physics.OverlapSphere(heavyAttackCenter, heavyRadius, enemyLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
                dirToEnemy.y = 0;
                Vector3 playerForward = transform.forward;
                playerForward.y = 0;
            
                // 计算玩家前方和敌人的夹角，只允许前方 150度 (左右各75度) 范围内的敌人受击
                if (Vector3.Angle(playerForward, dirToEnemy) <= 75f)
                {
                    validHits.Add(hit);
                }
            }
        }
        else if (isRunningAttack)
        {
            // 【滑行攻击】: 携带巨大惯性的横扫或突进
            // 【滑行攻击】: 以自身为圆心的 360 度大范围全方位斩击！
            // 特点：圆心就在玩家自己身上，不需要算任何夹角，只要在范围内全部击飞！
            Vector3 runAttackCenter = transform.position + Vector3.up * 1f; // 中心拉回玩家身体（抬高1米对准胸口）
            float runRadius = 6.0f; 
        
            Collider[] hits = Physics.OverlapSphere(runAttackCenter, runRadius, enemyLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                // 没有任何角度 if 判断，直接全部加入命中名单！
                validHits.Add(hit);
            }
        }

        // ==============================================================
        // 3. 统一处理伤害表现与结算（你原本完美的逻辑）
        // ==============================================================
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in validHits)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy)) 
            {
                damagedEnemies.Add(enemy); // 加入名单，防止多层碰撞体重复扣血

                Vector3 attackDir = (enemy.transform.position - transform.position).normalized;
                attackDir.y = 0;

                enemy.TakeDamageWithDirection(attackDir, knockbackForce, damage, hitDamageType);
            

                // 【战斗反馈】：时间顿帧卡肉
                StartCoroutine(HitStop()); 

                // 【火花生成】：用绝对的数学逻辑定位胸口火花
                Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                Vector3 sparkPos = chestPos + (transform.position - enemy.transform.position).normalized * 0.3f;
                Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;

                SpawnHitEffect(hitEffect, sparkPos, attachTarget);
            
                // 【音效播放】
                if (isLightAttacking) PlayLightAttackHit();
                else if (isAttacking || isRunningAttack) PlayAttackHit();
            }
        }

        // ==============================================================
        // 4. 解绑伤害，按固定动作积攒怒气
        // ==============================================================
        int actualHitCount = damagedEnemies.Count; // 算出本次动作真正砍到了几个怪
        if (actualHitCount > 0 && currentRage < maxRage)
        {
            // 步骤A：根据当前的攻击动作，获取基础固定怒气值
            float baseActionRage = 0f;
            if (isLightAttacking) baseActionRage = lightAttackRage;
            else if (isAttacking) baseActionRage = heavyAttackRage;
            else if (isRunningAttack) baseActionRage = runningAttackRage;

            // 【机制奖励】：如果是完美闪避后的暴击一击，这一下不仅伤害高，怒气也给 1.5 倍！
            if (hitDamageType == 1) 
            {
                baseActionRage *= 1.5f;
            }
        
            // 步骤B：ARPG 群攻衰减公式 (打1个100%，打2个130%，打3个160%...)
            float aoeMultiplier = 1f + (actualHitCount - 1) * 0.3f; 

            // 步骤C：引入你的RPG精神力加成 
            // （rageGainMultiplier 已经在你的 RecalculateAttributes 中写好了：1 + statSpirit * 0.02f）
            float finalRage = baseActionRage * aoeMultiplier * rageGainMultiplier;

            // 步骤D：结算并更新 UI
            currentRage += finalRage;
            currentRage = Mathf.Clamp(currentRage, 0f, maxRage);
        }
    }


    float GetCurrentKnockbackForce()
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
    
        // 轻攻击
        if (isLightAttacking)
        {
            if (stateInfo.IsName("LightAttack1") && lightAttackKnockback.Length > 0)
                return lightAttackKnockback[0];
            else if (stateInfo.IsName("LightAttack2") && lightAttackKnockback.Length > 1)
                return lightAttackKnockback[1];
            else if (stateInfo.IsName("LightAttack3") && lightAttackKnockback.Length > 2)
                return lightAttackKnockback[2];
        }
    
        // 滑行攻击
        if (isRunningAttack)
        {
            return runningAttackKnockback;
        }
    
        // 重攻击（5段）
        if (isAttacking)
        {
            if (stateInfo.IsName("Attack1") && heavyAttackKnockback.Length > 0)
                return heavyAttackKnockback[0];
            else if (stateInfo.IsName("Attack2") && heavyAttackKnockback.Length > 1)
                return heavyAttackKnockback[1];
            else if (stateInfo.IsName("Attack3") && heavyAttackKnockback.Length > 2)
                return heavyAttackKnockback[2];
            else if (stateInfo.IsName("Attack4") && heavyAttackKnockback.Length > 3)
                return heavyAttackKnockback[3];
            else if (stateInfo.IsName("Attack5") && heavyAttackKnockback.Length > 4)
                return heavyAttackKnockback[4];
        }
    
        return 5f; // 默认击退值
    }

    // 在 CheckAttackHit 方法中，根据当前攻击类型获取伤害值
    int GetCalculatedDamage()
    {
        // 1. 获取动作的基础伤害
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        float baseDamage = 10f;

        if (isLightAttacking)
        {
            if (stateInfo.IsName("LightAttack1")) baseDamage = lightAttackDamage[0];
            else if (stateInfo.IsName("LightAttack2")) baseDamage = lightAttackDamage[1];
            else if (stateInfo.IsName("LightAttack3")) baseDamage = lightAttackDamage[2];
        }
        else if (isRunningAttack) baseDamage = runningAttackDamage;
        else if (isAttacking)
        {
            if (stateInfo.IsName("Attack1")) baseDamage = heavyAttackDamage[0];
            else if (stateInfo.IsName("Attack2")) baseDamage = heavyAttackDamage[1];
            else if (stateInfo.IsName("Attack3")) baseDamage = heavyAttackDamage[2];
            else if (stateInfo.IsName("Attack4")) baseDamage = heavyAttackDamage[3];
            else if (stateInfo.IsName("Attack5")) baseDamage = heavyAttackDamage[4];
        }

        // 2. 加上属性面板的攻击力加成
        float totalDamage = baseDamage + attackPowerBonus;

        // 3. 【真实感核心】加入 ±10% 的随机浮动
        float randomMultiplier = Random.Range(0.9f, 1.1f);
        
        return Mathf.RoundToInt(totalDamage * randomMultiplier);
    }

     // 命中停顿协程
    System.Collections.IEnumerator HitStop()
    {
        //设为极其接近于0的微小数值（比如 0.05f 或 0.1f），保留引擎渲染刷新！
        Time.timeScale = 0.05f;
        
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
    }
    

    // =========== 特效系统 ===========
    // 播放重攻击特效
    public void SpawnHeavyEffect()
    {     
        if (!isAttacking && !isLightAttacking && !isRunningAttack) return;

        if (heavyAttackEffects == null || heavyAttackEffects.Length == 0)
        {
            //Debug.LogWarning("heavyAttackEffects 数组为空！");
            return;
        } 
    
        // 获取当前攻击段数
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
    
        if (stateInfo.IsName("Attack1")) index = 0;
        else if (stateInfo.IsName("Attack2")) index = 1;
        else if (stateInfo.IsName("Attack3")) index = 2;
        else if (stateInfo.IsName("Attack4")) index = 3;
        else if (stateInfo.IsName("Attack5")) index = 4;



        if (index >= 0 && index < heavyAttackEffects.Length && heavyAttackEffects[index] != null)
        {
            SpawnEffect(heavyAttackEffects[index]);  // 这里应该被调用
        }
        else
        {
            //Debug.LogWarning($"无法播放特效 - 索引:{index}, 数组长度:{heavyAttackEffects.Length}");
        }
    }

    // 播放轻攻击特效
    public void SpawnLightEffect()
    {
        if (lightAttackEffects == null || lightAttackEffects.Length == 0) return;
    
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
    
        if (stateInfo.IsName("LightAttack1")) index = 0;
        else if (stateInfo.IsName("LightAttack2")) index = 1;
        else if (stateInfo.IsName("LightAttack3")) index = 2;
    
        if (index >= 0 && index < lightAttackEffects.Length)
        {
            SpawnEffect(lightAttackEffects[index]);
        }
    }

    // 播放滑行攻击特效
    public void SpawnRunningEffect()
    {
        if (runningAttackEffect != null)
        {
            SpawnEffect(runningAttackEffect);
        }
    }

    // 播放技能特效
    public void SpawnSkillEffect()
    {
        if (skillEffect != null)
        {
            SpawnEffect(skillEffect);
        }
    }

    // 播放命中特效（在击中敌人时调用）
    public void SpawnHitEffect(GameObject vfxPrefab, Vector3 hitPoint, Transform enemyTransform)
    {
        if (vfxPrefab != null)
        {
            // 【重构】：用对象池拿取特效，代替 Instantiate
            GameObject effect = VFXPoolManager.Instance.SpawnFromPool(vfxPrefab, hitPoint, Quaternion.identity);
            
            if (enemyTransform != null)
            {
                effect.transform.SetParent(enemyTransform, true);
            }
            
            effect.SetActive(false);
            effect.SetActive(true);
            StartCoroutine(DelayedPlay(effect));
        }
    }



    // 通用特效生成方法
    private void SpawnEffect(GameObject effectPrefab)
    {
         if (effectPrefab == null) return;
        if (weaponPoint == null) return;
    
        Vector3 defaultSpawnPos = weaponPoint.position;
        Vector3 playerChest = transform.position + Vector3.up * 1.2f;

        Vector3 dirToWeapon = defaultSpawnPos - playerChest;
        float maxDist = dirToWeapon.magnitude;
        Vector3 finalSpawnPos = defaultSpawnPos;

        if (Physics.SphereCast(playerChest, 0.3f, dirToWeapon.normalized, out RaycastHit hit, maxDist, enemyLayer))
        {
            finalSpawnPos = playerChest + dirToWeapon.normalized * Mathf.Max(0, hit.distance - 0.1f);
        }

        Quaternion spawnRot = GetAttackRotation();
        
        // 【重构】：用对象池拿取
        GameObject effect = VFXPoolManager.Instance.SpawnFromPool(effectPrefab, finalSpawnPos, spawnRot);
    
        effect.SetActive(false);
        effect.SetActive(true);
        StartCoroutine(DelayedPlay(effect));
    }

    // 根据攻击段数获取特效旋转
    private Quaternion GetAttackRotation()
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
    
        // 第一段攻击：从右上45度向左下挥动
        if (stateInfo.IsName("Attack1"))
        {
            // 旋转参数说明：
            // X: -35 让特效向前倾斜（配合挥砍方向）
            // Y: -90 让特效面向攻击方向
            // Z: 45 让特效呈45度角（右上到左下）
            return weaponPoint.rotation * Quaternion.Euler(20, -90, 245);
        }
        // 第二段攻击
        if (stateInfo.IsName("Attack2"))
        {
            return weaponPoint.rotation * Quaternion.Euler(90, 0, 5);
        }   
        // 第三段攻击
        if (stateInfo.IsName("Attack3"))
        {
            
            return weaponPoint.rotation * Quaternion.Euler(20, -30, -45);
        }
        // 第四段攻击
        if (stateInfo.IsName("Attack4"))
        {
            return weaponPoint.rotation * Quaternion.Euler(90, 0, 0);
        }
        // 第五段攻击
        if (stateInfo.IsName("Attack5"))
        {
            return weaponPoint.rotation * Quaternion.Euler(80, -20, 0);
        }
        // 其他攻击段数暂时保持默认
        return weaponPoint.rotation;
    }

    // ==========================================
    // 纯净版特效生成（专为 Wrapper 套娃预制体打造）
    // ==========================================
    public void SpawnPureEffect(GameObject vfxPrefab, Vector3 spawnPos)
    {
        if (vfxPrefab == null) return;
        
        // 【重构】：用对象池拿取
        GameObject effect = VFXPoolManager.Instance.SpawnFromPool(vfxPrefab, spawnPos, transform.rotation);
        
        effect.SetActive(false);
        effect.SetActive(true);
        StartCoroutine(DelayedPlay(effect));
    }

    IEnumerator DelayedPlay(GameObject effect)
    {
        yield return null; // 等待一帧
        float maxDuration = 1f; // 保底寿命
    
        //  1. 全面重启：所有粒子系统
        ParticleSystem[] allParticleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticleSystems)
        {
            if (ps.main.duration > maxDuration) maxDuration = ps.main.duration;
            
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear();
            ps.Play();
        }
    
        //  2. 全面重启：藏在任何角落的新版 Animator 动画机
        // 【核心修复】：加上 InChildren，把那个控制半透明圆环的动画机挖出来！
        Animator[] allAnimators = effect.GetComponentsInChildren<Animator>(true);
        foreach (var animator in allAnimators)
        {
            //必须确保有控制器，且组件本身是激活状态的
            if (animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) continue;
            animator.Rebind();
            animator.Update(0f);
            
        }
    
        //  3. 全面重启：藏在任何角落的旧版 Animation 动画组件
        Animation[] allAnimations = effect.GetComponentsInChildren<Animation>(true);
        foreach (var animation in allAnimations)
        {
            animation.Stop();
            animation.Play();
        }
    
        // 4. 根据攻击类型动态调整大小
        if (isLightAttacking)
        {
            effect.transform.localScale = Vector3.one * 0.8f;
        }
        else if (isAttacking)
        {
            effect.transform.localScale = Vector3.one * 1.2f;
        }
    
        // 【重构】用回收代替 Destroy，彻底断绝垃圾回收峰值
        VFXPoolManager.Instance.ReturnToPool(effect, maxDuration);
    
    }
    //====================================================


    //========技能释放==========
    void StartCast()
    {
        currentState = ActionState.SkillCast;
        isCastingInvincible = true;  // 开启霸体
        castStartTime = Time.time;
    
        // 停止移动
        locomotion.ResetSpeed();
    
        // 触发施法动画
        if (anim != null)
        {
            // 强制让 Animator 内部也停下来，防止之前步行的状态残留在混合树导致“原地太空步/平移”
            anim.SetFloat("Speed", 0f);
            anim.SetFloat("Direction", 0f);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);

            anim.SetTrigger("Cast");
            // 修复：用新协程替代原来的延迟机制
            StartCoroutine(CastRoutine());
            
        }
        if (castSound != null) audioSource.PlayOneShot(castSound);
    }

    IEnumerator CastRoutine()
    {
        if (anim == null)
        {
            yield return new WaitForSeconds(castDuration);
            OnCastFinished();
            yield break;
        }

        // 1. 获取触发施法前，小人的初始动画状态
        int initialState = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;

        // 2. 【核心修复】：动态等待！死死盯住动画机，直到它真正开始响应 Trigger 并进入技能状态
        float timeout = 0f;
        while (anim.GetCurrentAnimatorStateInfo(0).shortNameHash == initialState && !anim.IsInTransition(0))
        {
            timeout += Time.deltaTime;
            if (timeout > 0.5f) break; // 防卡死兜底
            yield return null;
        }

        // 3. 此时 100% 确定已经开始施法，抓取这套重劈动作的【绝对真实长度】
        AnimatorStateInfo stateInfo = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
        float actualAnimLength = stateInfo.length;

        if (actualAnimLength <= 0.1f) 
        {
            actualAnimLength = castDuration;
        }

        float timer = 0f;
        
        // 4. 【绝对死锁】：只要动画没播完，绝对不解除霸体，不归还移动权限！
        while (timer < actualAnimLength * 0.95f) 
        {
            if (isDead) yield break; 
            timer += Time.deltaTime;
            yield return null;
        }

        // 5. 动画彻底播完后，才允许收招和移动！
        OnCastFinished();
    }

    // 动画事件调用（在施法动画最后一帧添加）
    public void OnCastFinished()
    {
        currentState = ActionState.IdleMove;
        isCastingInvincible = false;
        
        if (anim != null) 
        {
            anim.ResetTrigger("Cast");
            // 强行归零待机动作索引，确保切回基础的战斗戒备姿势
            anim.SetFloat("IdleIndex", 0f); 
        }

        // 重置待机计时器，防止施法期间的时间累加导致直接进入休闲待机
        IdleSelector idleSelector = GetComponent<IdleSelector>();
        if (idleSelector != null)
        {
            idleSelector.ResetIdleTimer();
        }
    }

    //根据当前攻击段数获取消耗
    private float GetCurrentHeavyAttackStaminaCost()
    {
        if (currentAttackCombo >= 1 && currentAttackCombo <= heavyAttackStaminaCost.Length)
            return heavyAttackStaminaCost[currentAttackCombo - 1];
        else
            return heavyAttackStaminaCost[0]; // 默认第一段消耗
    }


    // ========== 攻击音效（数组索引版）==========
    
    // 重攻击挥剑音效
    public void PlayAttackSwing()
    {
        if (!isAttacking && !isLightAttacking && !isRunningAttack) return;
        if (audioSource == null) return;
        if (attackSwingSounds == null || attackSwingSounds.Length == 0) return;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;

        // 主判断：通过动画名称
        if (stateInfo.IsName("Attack1")) index = 0;
        else if (stateInfo.IsName("Attack2")) index = 1;
        else if (stateInfo.IsName("Attack3")) index = 2;
        else if (stateInfo.IsName("Attack4")) index = 3;
        else if (stateInfo.IsName("Attack5")) index = 4;

        // 备用：如果名称匹配失败，尝试通过动画状态哈希（更稳定）
        if (index == -1)
        {
            int hash = stateInfo.shortNameHash;
            if (hash == Animator.StringToHash("Attack1")) index = 0;
            else if (hash == Animator.StringToHash("Attack2")) index = 1;
            else if (hash == Animator.StringToHash("Attack3")) index = 2;
            else if (hash == Animator.StringToHash("Attack4")) index = 3;
            else if (hash == Animator.StringToHash("Attack5")) index = 4;
        }           

        // 最终保证：如果依然失败，检查动画的 normalizedTime 是否在 0.1~0.9 之间并且之前记录的 combo 段数
        // 但为了简单，上述哈希通常就足够了。

        if (index >= 0 && index < attackSwingSounds.Length && attackSwingSounds[index] != null)
        {
            if (index == 3) audioSource.pitch = 1.2f;
            else audioSource.pitch = 1f;
            audioSource.PlayOneShot(attackSwingSounds[index], 0.9f);
            StartCoroutine(ResetPitch());
            //Debug.Log($"播放重攻击挥剑音效: Attack{index + 1}");
        }
        else
        {
            //Debug.LogWarning($"PlayAttackSwing 无法识别当前攻击动画: {stateInfo.fullPathHash}");
        }
    }
    
    // 重攻击击中音效
    public void PlayAttackHit()
    {
        if (audioSource == null) return;
        if (attackHitSounds == null || attackHitSounds.Length == 0) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
        
        if (stateInfo.IsName("Attack1")) index = 0;
        else if (stateInfo.IsName("Attack2")) index = 1;
        else if (stateInfo.IsName("Attack3")) index = 2;
        else if (stateInfo.IsName("Attack4")) index = 3;
        else if (stateInfo.IsName("Attack5")) index = 4;
        
        if (index >= 0 && index < attackHitSounds.Length && attackHitSounds[index] != null)
        {
            // 根据攻击段数设置音量
            float volume = 1.0f;  // 默认音量
        
            if (index == 0)        // 第一段
            {
                volume = 1.2f;
            }
            else if (index == 1)   // 第二段
            {
                volume = 0.65f;
            }
            else if (index == 2)   // 第三段
            {
                volume = 0.65f;
            }
            else if (index == 3)   // 第四段
            {
                volume = 0.65f;
            }
            else if (index == 4)   // 第五段
            {
                volume = 0.65f;
            }
            audioSource.PlayOneShot(attackHitSounds[index], volume);
        }
    }
    
    // 轻攻击挥剑音效
    public void PlayLightAttackSwing()
    {
        if (audioSource == null) return;
        if (lightAttackSwingSounds == null || lightAttackSwingSounds.Length == 0) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
        
        if (stateInfo.IsName("LightAttack1")) index = 0;
        else if (stateInfo.IsName("LightAttack2")) index = 1;
        else if (stateInfo.IsName("LightAttack3")) index = 2;
        
        if (index >= 0 && index < lightAttackSwingSounds.Length && lightAttackSwingSounds[index] != null)
        {
            audioSource.PlayOneShot(lightAttackSwingSounds[index], 0.7f);
            //Debug.Log($"播放轻攻击挥剑音效: LightAttack{index + 1}");
        }
    }
    
    // 轻攻击击中音效
    public void PlayLightAttackHit()
    {
        if (audioSource == null) return;
        if (lightAttackHitSounds == null || lightAttackHitSounds.Length == 0) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
        
        if (stateInfo.IsName("LightAttack1")) index = 0;
        else if (stateInfo.IsName("LightAttack2")) index = 1;
        else if (stateInfo.IsName("LightAttack3")) index = 2;
        
        if (index >= 0 && index < lightAttackHitSounds.Length && lightAttackHitSounds[index] != null)
        {
            audioSource.PlayOneShot(lightAttackHitSounds[index], 0.8f);
        }
    }

    // 滑行攻击挥剑音效（大剑破风声）
    public void PlaySlidingWhoosh()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource 未初始化");
            return;
        }
    
        if (slidingWhooshSound != null)
        {
            audioSource.PlayOneShot(slidingWhooshSound, 1.0f);
            //Debug.Log("播放滑行攻击破风声");
        }
        else
        {
            // 如果没有设置专门的滑行音效，可以用重攻击挥剑音效代替
            if (attackSwingSounds != null && attackSwingSounds.Length > 0)
            {
                audioSource.PlayOneShot(attackSwingSounds[0], 0.9f);
            }
        }
    }
    
    // 重置音调协程
    IEnumerator ResetPitch()
    {
        yield return new WaitForSeconds(0.1f);
        if (audioSource != null)
        {
            audioSource.pitch = 1f;
        }
    }

    // 播放重攻击语音(根据段数索引)
    private void PlayHeavyAttackVoice()
    {
        if (audioSource == null) return;
        if (heavyAttackVoices == null || heavyAttackVoices.Length < 5) return;

        // 获取当前攻击段数（通过动画名称）
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        int index = -1;
        if (stateInfo.IsName("Attack1")) index = 0;
        else if (stateInfo.IsName("Attack2")) index = 1;
        else if (stateInfo.IsName("Attack3")) index = 2;
        else if (stateInfo.IsName("Attack4")) index = 3;
        else if (stateInfo.IsName("Attack5")) index = 4;

        if (index >= 0 && index < heavyAttackVoices.Length && heavyAttackVoices[index] != null)
        {
            audioSource.PlayOneShot(heavyAttackVoices[index], 0.7f);
            //Debug.Log($"播放重攻击语音: Attack{index + 1}");
        }
    }

    // ========== 受击方法 ==========
    public void TakeDamage(int rawDamage,Vector3 knockbackDir = default, float knockbackForce = 0f)
    {
         // 已死亡不再受击
        if (isDead) return;

        // 无敌帧判定（包括完美闪避）
        if (isInvincible)
        {
            //Debug.Log("完美闪避！触发慢动作和暴击");
            StartCoroutine(PerfectDodgeReward());
            currentRage += 10.0f;
            nextAttackIsCrit = true;
            nextHeavyAttackIsFourth = true; 
            return; // 不扣血，不进入受击硬直
        }
        
        // 1. 敌人的原始伤害加上随机浮动
        float randomizedRawDamage = rawDamage * Random.Range(0.9f, 1.1f);

        // 2. 结合自身的防御力计算真实承受伤害 (经典 RPG 防御公式)
        // 假设防御力为 50，则承受伤害变为 100 / (100+50) = 66%
        float damageReductionFactor = 100f / (100f + defensePower);
        int finalDamage = Mathf.RoundToInt(randomizedRawDamage * damageReductionFactor);

        // 保证强制扣血保底至少为 1
        finalDamage = Mathf.Max(1, finalDamage);

        // 施法霸体
        if (isCastingInvincible)
        {
            currentHealth -= finalDamage*0.6f;
            if (healthSlider != null) healthSlider.value = currentHealth;
            Debug.Log("施法霸体，已承受伤害但免疫受击硬直！");
            if (currentHealth <= 0) Die();     
            return;
        }

        if (isBlocking)
        {
            // 如果正在格挡，应该走格挡受击逻辑，但为了安全，这里也可以处理
            TakeBlockDamage(rawDamage);
            return;
        }

        if (isHit) return;
        
        //注入物理击退力
        if (knockbackForce > 0f)
        {
            impact = knockbackDir * knockbackForce;
        }

        currentHealth -= finalDamage;        // 正常扣血
        if (healthSlider != null) healthSlider.value = currentHealth;
        //Debug.Log($"玩家受到 {finalDamage} 伤害，剩余生命 {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // 强制结束当前攻击
        if (isAttacking || isLightAttacking || isRunningAttack || isUltimateCasting || isCasting)
        {
            currentState = ActionState.Hit;
            
            // 【核心修复】：必须彻底清空所有连击预输入缓存，防止复苏后自动乱挥刀
            comboPending = false;
            comboPendingTime = 0f;
            lightComboPending = false;
            lightComboPendingTime = 0f;
            isProcessingAttackEnd = false;
            targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持 

            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("LightCombo");
            
            if (attackLayerIndex >= 0)  anim.SetLayerWeight(attackLayerIndex, 0f);   
        }
        
        currentState = ActionState.Hit;
        
        // 播放受击音效 (根据最终伤害判定)
        if (playerHitSounds != null && playerHitSounds.Length >= 5)
        {
            AudioClip clip = finalDamage < 15 ? playerHitSounds[Random.Range(0, 3)] : playerHitSounds[Random.Range(3, 5)];
            audioSource.PlayOneShot(clip, 0.8f);
        }

        if (anim != null)
        {
            anim.SetTrigger("Hit");
        }
        
        StopCoroutine(nameof(ResetHit));
        StartCoroutine(nameof(ResetHit));

    }
    
    // 技能伤害方法（供动画事件调用）
    public void CastDamage()
    {
        Debug.Log("大招裂地剑气触发！");

        // 1. 计算出本次大招的【总真实伤害】
        float scaledBonusDamage = attackPowerBonus * castDamageScalingMultiplier;
        float totalDamage = castDamage + scaledBonusDamage;
        float randomMultiplier = Random.Range(0.9f, 1.1f);
        int finalTotalSkillDamage = Mathf.RoundToInt(totalDamage * randomMultiplier);

        // 2. 生成会飞的剑气波
        if (skillEffect != null)
        {
            // 在身前 1.5 米处生成，稍微抬高一点防止遁地
            Vector3 vfxPos = transform.position + transform.forward * 1.5f;
            vfxPos.y += 1.0f; 

            // 【重构】从池子拿取剑气
            GameObject waveVFX = VFXPoolManager.Instance.SpawnFromPool(skillEffect, vfxPos, transform.rotation * skillEffect.transform.rotation);

            // 直接 GetComponent 获取特效脚本
            SkillWave waveScript = waveVFX.GetComponent<SkillWave>();

            // 把它改为打 10 次伤害（3秒内高频切割）
            int totalTicks = 10;
            // 判断一下，如果有专属技能火花就用专属的，没有才用普通白字火花兜底！
            GameObject vfxToPass = skillHitEffect != null ? skillHitEffect : hitEffect;    
            //把硬编码替换为面板变量 skillWavePushForce 和 skillWaveUpForce
            waveScript.Initialize(finalTotalSkillDamage, totalTicks, skillWavePushForce, skillWaveUpForce, enemyLayer, transform.forward, vfxToPass);

            // 【重构】：用池子回收替代 Destroy
            VFXPoolManager.Instance.ReturnToPool(waveVFX, skillWaveLifetime);
        }
        else
        {
            Debug.LogWarning("技能特效为空，无法释放剑气波！");
        }
    }

    // ==========================================
    // 终极大招与 QTE 处决系统
    // ==========================================
    private void StartUltimate()
    {
        currentState = ActionState.Ultimate;
        isCastingInvincible = true; // 复用霸体
        qteSuccess = false;         // 重置QTE状态
        isWaitingForQTE = false;
        
        locomotion.ResetSpeed();

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.SetFloat("Direction", 0f);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
            anim.SetTrigger("Ultimate");
        }
        
        // 播放大招起手蓄力音效
        if (ultChargeSFX != null && audioSource != null)
        {
            audioSource.PlayOneShot(ultChargeSFX, 1.0f);
        }
    }


    // 动画事件 1：到达最高点，触发子弹时间！
    public void Event_TriggerQTE()
    {
        isWaitingForQTE = true;
        Time.timeScale = 0.1f; // 时间放慢 10 倍！极其震撼

        // 呼出居中 QTE 界面！
        if (QTEUIManager.Instance != null)
        {
            QTEUIManager.Instance.ShowQTE();
        }

        // 给空中滞留音效单独开一个小灶，防止被主音源的变调干扰！
        if (ultSlowMotionSFX != null)
        {
           // 直接从音效池里拿一个，并且挂载在玩家身上(transform)跟着走
            AudioPoolManager.Instance.PlaySound(ultSlowMotionSFX, transform.position, 1.0f, null, true);
        }


        // 开启一个独立于游戏时间的协程，计算玩家反应时间
        StartCoroutine(QTECountdown());
    }

    // 倒计时：只给玩家 2 秒的真实时间反应
    private IEnumerator QTECountdown()
    {
        yield return new WaitForSecondsRealtime(2.0f); 

        // 如果 2 秒后玩家还是没按成功，判定为失败
        if (isWaitingForQTE)
        {
            isWaitingForQTE = false;
            Time.timeScale = 1.0f; // 时间恢复正常

            // 告诉 QTE 界面玩家失败了（触发黯淡消失特效）
            if (QTEUIManager.Instance != null)
            {
                QTEUIManager.Instance.HideQTE(false);
            }
            
            //Debug.Log("QTE 失败：挥空或伤害大减！");
        }
    }

    // 玩家在子弹时间内按下了左键！
    private void TriggerQTESuccess()
    {
        qteSuccess = true;
        isWaitingForQTE = false;
        Time.timeScale = 1.0f; // 瞬间解除时间锁定，营造刀刃破空的极速感！

        // 告诉 QTE 界面玩家按成功了（触发金光斩裂特效）
        if (QTEUIManager.Instance != null)
        {
            QTEUIManager.Instance.HideQTE(true);
        }

        // 为 QTE 成功爆音也开一个独立的防变调音源！
        AudioClip clipToPlay = ultQTESuccessSFX != null ? ultQTESuccessSFX : perfectDodgeStartSFX;
        if (clipToPlay != null)
        {
            AudioPoolManager.Instance.PlaySound(clipToPlay, transform.position, 0.6f, null, true);
        }
    }

    // 动画事件：前四段上挑伤害判定
    // （在 Animator 里打 4 个事件点调用这个方法）
    public void Event_UltUpwardSlashHit(int index)
    {
        // 核心防抖锁】：如果距离上次触发还不到 0.1 秒，说明是 Unity 引擎在抽风双重调用，直接无视它
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime; // 记录本次触发时间

        //Debug.Log($"大招第 {index + 1} 段斩击判定触发");

        // 精准调用：根据传进来的数字，播放对应角度的特效包装盒
        if (ultSlashEffects != null && index >= 0 && index < ultSlashEffects.Length)
        {
            GameObject currentSlashVFX = ultSlashEffects[index];
            if (currentSlashVFX != null)
            {
                // 距离身前 0.5米，高度 1.0米
                Vector3 vfxPos = transform.position + transform.forward * 0.5f + Vector3.up * 1.0f;
                SpawnPureEffect(currentSlashVFX, vfxPos);
            }
        }

        // --- 下面是伤害与命中火花（循环内） ---
        float totalDamage = ultimateSlashBaseDamage + (attackPowerBonus * castDamageScalingMultiplier * 0.5f);
        int finalSlashDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));

        // 将判定球的中心微微往前推 1 米，半径限制为 2.5 米
        Vector3 slashCenter = transform.position + transform.forward * 1.0f;
        float ultSlashRadius = 2.5f;

        //加上 enemyLayer 和 QueryTriggerInteraction.Ignore
        Collider[] hitColliders = Physics.OverlapSphere(slashCenter, ultSlashRadius, enemyLayer, QueryTriggerInteraction.Ignore);

        HashSet<BasicEnemyTest> slashedEnemies = new HashSet<BasicEnemyTest>();
        Vector3 playerForward = transform.forward; 
        playerForward.y = 0;

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !slashedEnemies.Contains(enemy))
            {
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized;
                dirToEnemy.y = 0;

                // 【核心锁死】：大招挥击非常狂野，给个前方 180度 (左右各90度) 的大扇形判定，但绝对禁止打到背后！
                if (Vector3.Angle(playerForward, dirToEnemy) <= 90f)
                {
                    slashedEnemies.Add(enemy); 
                    
                    enemy.TakeDamageWithDirection(dirToEnemy, castKnockbackForce * 0.3f, finalSlashDamage, 2);
                    
                    StartCoroutine(HitStop()); 
                    
                    Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                    Vector3 sparkPos = chestPos + (transform.position - enemy.transform.position).normalized * 0.3f;
                    Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;
                    
                    GameObject vfxToUse = ultHitEffect != null ? ultHitEffect : hitEffect;
                    SpawnHitEffect(vfxToUse, sparkPos, attachTarget);
                    if (ultHitSFX != null) 
                    {
                        AudioPoolManager.Instance.PlaySound(ultHitSFX, sparkPos, 1.0f);
                    } 
                }
            }
        }
    }

    // 动画事件：第五段专属升龙击飞！
    public void Event_UltLaunchHit()
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime;

        Debug.Log("第五段击飞判定触发");

        // 垂直升龙剑光独立生成！
        if (ultLaunchEffect != null)
        {
            Vector3 vfxPos = transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;
            SpawnPureEffect(ultLaunchEffect, vfxPos);
        }

        // --- 下面是伤害与命中火花 ---
        float totalDamage = ultimateSlashBaseDamage + (attackPowerBonus * castDamageScalingMultiplier * 0.5f);
        int finalSlashDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));

        Vector3 launchCenter = transform.position + transform.forward * 1.0f;
        float ultLaunchRadius = 2.5f; 

        Collider[] hitColliders = Physics.OverlapSphere(launchCenter, ultLaunchRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        HashSet<BasicEnemyTest> slashedEnemies = new HashSet<BasicEnemyTest>(); 

        Vector3 playerForward = transform.forward;
        playerForward.y = 0;

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !slashedEnemies.Contains(enemy))
            {
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized;
                dirToEnemy.y = 0;

                // 【核心锁死】：升龙击飞也只判定前方 180度 内的敌人
                if (Vector3.Angle(playerForward, dirToEnemy) <= 90f)
                {
                    slashedEnemies.Add(enemy); 
                    
                    enemy.TakeLaunchDamage(dirToEnemy, castKnockbackForce * 0.5f, finalSlashDamage, ultimateLaunchForce, 2);
                    
                    StartCoroutine(HitStop());
                    Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                    Vector3 sparkPos = chestPos + (transform.position - enemy.transform.position).normalized * 0.3f; 
                    Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;

                    GameObject vfxToUse = ultHitEffect != null ? ultHitEffect : hitEffect;
                    SpawnHitEffect(vfxToUse, sparkPos, attachTarget);
                    if (ultHitSFX != null) 
                    {
                        AudioPoolManager.Instance.PlaySound(ultHitSFX, sparkPos, 1.0f);
                    }
                }
            }
        }
    }

    // 动画事件 2：大剑砸在地上的瞬间结算伤害！
    public void Event_UltimateHit()
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime;

        //Debug.Log("大招伤害判定触发");

        //播放砸地终结音效
        if (ultSlamSFX != null && audioSource != null)
        {
            audioSource.PlayOneShot(ultSlamSFX, 1.2f);
        }

        //生成向下重劈的剑光
        if (ultFinalSlashEffect != null)
        {
            // 位置在玩家身前 1 米，高度 1.2 米（和之前的上挑保持一致，显得连贯）
            Vector3 slashPos = transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;
            SpawnPureEffect(ultFinalSlashEffect, slashPos);
        }

        if (ultSlamEffect != null)
        {
            // 在玩家身前 1 米处生成
            Vector3 slamPos = transform.position + transform.forward * 1.0f;
            slamPos.y += 0.1f; // 稍微抬高一点点，防止贴图被地板吞没
            
            // 复用我们的纯净版特效生成器
            SpawnPureEffect(ultSlamEffect, slamPos);
        }
        
        // 计算基础大招伤害（带上属性加成）
        float totalDamage = ultimateBaseDamage + (attackPowerBonus * castDamageScalingMultiplier);
        
        // 【核心】：如果 QTE 成功，伤害暴涨 4 倍！！
        if (qteSuccess)
        {
            totalDamage *= ultimateQTEBonus; 
        }

        int finalDamage = Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));
        float slamRadius = 7.0f; 

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, slamRadius, enemyLayer,QueryTriggerInteraction.Ignore); // 范围比普通技能更大
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy); 
                
                // 【先抹平高度差，再计算方向！】保证 100% 水平击退力度！
                Vector3 enemyPos = enemy.transform.position;
                Vector3 playerPos = transform.position;
                enemyPos.y = 0;
                playerPos.y = 0;
                Vector3 knockbackDir = (enemyPos - playerPos).normalized;

                // 打出巨额伤害！击飞力度也可拉满 (比如 12f)
                // 最后一个参数用 2 代表技能伤害(紫色大字)，如果是 QTE 成功可以用 1 代表金色暴击
                int displayType = qteSuccess ? 1 : 2; 
                // 传入极大的击退力 (1.5倍)，极狠的下坠力 (SlamForce)，并给予长达 2.5 秒的倒地硬直 (最后一个参数)！
                enemy.TakeKnockbackWithUp(knockbackDir, castKnockbackForce * 1.5f, finalDamage, ultimateSlamForce, displayType, 2.5f);

                //加上最后的卡肉顿帧，以及 QTE 砸中的专属受击火花！
                StartCoroutine(HitStop()); 
                Vector3 chestPos = enemy.transform.position + Vector3.up * 1.2f;
                Vector3 sparkPos = chestPos + (transform.position - enemy.transform.position).normalized * 0.3f; 
                Transform attachTarget = enemy.lockOnPoint != null ? enemy.lockOnPoint : enemy.transform;

                GameObject vfxToUse = ultHitEffect != null ? ultHitEffect : hitEffect;
                SpawnHitEffect(vfxToUse, sparkPos, attachTarget);
            }
        }
    }

    // 动画事件 3：收招结束
    public void OnUltimateFinished()
    {
        currentState = ActionState.IdleMove;
        isCastingInvincible = false;
        isWaitingForQTE = false;
        Time.timeScale = 1.0f; // 兜底防止时间卡死
        
        if (anim != null) anim.ResetTrigger("Ultimate");
        
        IdleSelector idleSelector = GetComponent<IdleSelector>();
        if (idleSelector != null) idleSelector.ResetIdleTimer();
    }


    // 动画事件 4：播放大招四段上挑音效
    // （在 Animator 动画事件里填入该方法，并分别传入 0, 1, 2, 3）
    public void Event_UltUpwardSlashSound(int index)
    {
        if (audioSource == null || ultUpwardSlashSFXs == null) return;
        
        if (index >= 0 && index < ultUpwardSlashSFXs.Length && ultUpwardSlashSFXs[index] != null)
        {
            audioSource.PlayOneShot(ultUpwardSlashSFXs[index], 0.9f);
        }
    }


    public void TakeBlockDamage(int rawDamage,Vector3 knockbackDir = default, float knockbackForce = 0f)
    {
        if (isHit) return;
        
        //挡时也会有向后的推力
        if (knockbackForce > 0f)
        {
            impact = knockbackDir * knockbackForce;
        }

        float randomizedRawDamage = rawDamage * Random.Range(0.9f, 1.1f);
        float damageReductionFactor = 100f / (100f + defensePower);
        int finalDamage = Mathf.RoundToInt(randomizedRawDamage * damageReductionFactor);
        finalDamage = Mathf.Max(1, finalDamage);

        // 格挡减伤
        currentHealth -= finalDamage * 0.5f;
        if (healthSlider != null) healthSlider.value = currentHealth;

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        IdleSelector idleSelector = GetComponent<IdleSelector>();
        if (idleSelector != null) idleSelector.ResetIdleTimer();
        
        currentState = ActionState.Hit;

        if (anim != null)
        {
            anim.SetTrigger("BlockHit");
            anim.SetBool("IsBlocking", false);
        }
        StopCoroutine(nameof(ResetHit));
        StartCoroutine(nameof(ResetHit));
    }


    // ========== 休息与存档 ==========
    public void RestAndSetSpawnPoint(Vector3 spawnPos, Quaternion spawnRot)
    {
        // 1. 记录新的复活坐标和朝向

        // 2. 状态全满
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentRage = 0f;  // 【新增】坐篝火/复活后，清空怒气
        if (rageSlider != null) rageSlider.value = currentRage;

        // 3. 更新UI
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;

        // 4. 重置异常状态和攻击状态
        currentState = ActionState.IdleMove;

        // ============================================
        // 5. 【魂系核心】刷新世界上的所有敌人
        // ============================================
        foreach (BasicEnemyTest enemy in BasicEnemyTest.allEnemies)
        {
            if (enemy != null)
            {
                enemy.RespawnEnemy();
            }
        }

        // ============================================
        // 6. 【魂系核心】真正的硬核存档（写入电脑硬盘）
        // ============================================
        PlayerDataManager.Instance.SaveGame(spawnPos, spawnRot);

        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage("在赐福点休息，生命与状态已恢复(游戏进度保存)");
    }


    //===============================================
    // 带有视觉和听觉表现的完整休息转场流程
    // ===============================================
    //传送代码
    public void StartTeleport(Vector3 targetPos, Quaternion targetRot)
    {
        if (!isResting)
            StartCoroutine(TeleportSequence(targetPos, targetRot));
    }

    private IEnumerator TeleportSequence(Vector3 targetPos, Quaternion targetRot)
    {
        isResting = true;
        if (controller != null) controller.enabled = false;

        // 重置动画和状态标志（与 RestSequenceRoutine 相同）
        currentState = ActionState.IdleMove;
 
        locomotion.ResetSpeed();
        comboPending = false;
        lightComboPending = false;
        targetLeftHandIKWeight = 1f;

        locomotion.ResetSpeed();

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightCombo");
            anim.ResetTrigger("Cast");
            anim.ResetTrigger("Dodge");
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("BlockHit");
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.Play("Locomotion", 0, 0f);
            anim.SetFloat("Speed", 0f);
            anim.SetFloat("Direction", 0f);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsGrounded", true);
            anim.SetBool("IsStopping", false);
        }

        // 黑屏淡出
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = elapsed / fadeDuration;
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }
        // 移动玩家
        transform.position = targetPos;
        transform.rotation = targetRot;
        Physics.SyncTransforms();   // 立即同步物理世界
        yield return null;          // 等待一帧，让触发器状态更新
        GetComponent<PlayerCameraController>().ResetCameraBehindPlayer();

        // 重置世界（刷新所有敌人）
        foreach (BasicEnemyTest enemy in BasicEnemyTest.allEnemies)
        {
            if (enemy != null) enemy.RespawnEnemy();
        }

        // 可选：恢复满血满耐（与休息一致）
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;

        // 黑屏淡入
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = 1f - (elapsed / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        // 恢复控制
        if (controller != null) controller.enabled = true;
        isResting = false;
    }

    public void StartRestSequence(Vector3 spawnPos, Quaternion spawnRot, AudioClip restSFX)
    {
        if (!isResting) // 防短时间狂按
        {
            StartCoroutine(RestSequenceRoutine(spawnPos, spawnRot, restSFX));
        }
    }

    private IEnumerator RestSequenceRoutine(Vector3 spawnPos, Quaternion spawnRot, AudioClip restSFX)
    {
        isResting = true; // 锁死操作

        // 1. 强行停止玩家动作，切回待机状态
        if (controller != null) controller.enabled = false;

        // 彻底重置所有状态，加上跳跃和急停的重置，防止卡轴
        currentState = ActionState.IdleMove;
  
        locomotion.ResetSpeed();   
        comboPending = false;
        lightComboPending = false;
        targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持
        
        locomotion.ResetSpeed();

        if (anim != null)
        {
            // 手动清除所有手残多按的触发器缓存
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightCombo");
            anim.ResetTrigger("Cast");
            anim.ResetTrigger("Dodge");
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("BlockHit");

            // 强制把攻击层权重压回 0，彻底解开上半身动画锁死（解决平移滑步问题）
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);

            anim.Play("Locomotion", 0, 0f);
            anim.SetFloat("Speed", 0f);
            anim.SetFloat("Direction", 0f);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsGrounded", true);
            anim.SetBool("IsStopping", false);
        }

        // 2. 播放特殊音效（比如篝火点燃声、赐福水滴声）
        if (restSFX != null && audioSource != null)
        {
            // 降低主音调防止影响，单独播放
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(restSFX, 1.0f);
        }

        // 3. 屏幕逐渐变黑
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = elapsed / fadeDuration; // Alpha 从 0 渐变到 1
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        // --- 此时屏幕已经完全黑了，玩家什么都看不见 ---

        // 4. 在黑暗中执行真正的刷新逻辑（回血、存档、复活所有敌人）
        RestAndSetSpawnPoint(spawnPos, spawnRot);

        // 5. 在黑暗中把玩家精准传送到石碑/篝火前的站立位置
        transform.position = spawnPos;
        transform.rotation = spawnRot;
        GetComponent<PlayerCameraController>().ResetCameraBehindPlayer();

        // 让黑屏稍微停留一会儿，增加沉浸感，并掩盖掉模型瞬间移动的突兀感
        yield return new WaitForSeconds(0.6f);

        // 6. 屏幕逐渐亮起
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = 1f - (elapsed / fadeDuration); // Alpha 从 1 渐变到 0
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
        }

        // 7. 转场结束，恢复对角色的控制
        if (controller != null) controller.enabled = true;
        isResting = false;
    }

    //角色待机动画重置
    IEnumerator ResetHit()
    {
        // 1. 等待两帧，确保 Animator 已经收到了 Trigger 并开始向受击状态过渡
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        float actualAnimLength = 0.5f; // 默认保底锁死 0.5 秒

        // 2. 动态抓取当前受击动画的真实长度
        if (anim != null)
        {
            if (anim.IsInTransition(0))
            {
                actualAnimLength = anim.GetNextAnimatorStateInfo(0).length;
            }
            else
            {
                actualAnimLength = anim.GetCurrentAnimatorStateInfo(0).length;
            }
        }

        // 防御性兜底，万一没抓到，给个最低惩罚时间
        if (actualAnimLength <= 0.1f) actualAnimLength = 0.5f;

        // 3. 严格等待这个动画真实播放完毕（乘以 0.95 预留一点点过渡回 Idle 的手感时间）
        yield return new WaitForSeconds(actualAnimLength * 0.95f);
        
        currentState = ActionState.IdleMove;
        
        if (anim != null)
        {
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("BlockHit");
        }

        // 4. 重置待机计时器
        IdleSelector idleSelector = GetComponent<IdleSelector>();
        if (idleSelector != null)
        {
            idleSelector.ResetIdleTimer();
        }
    }
    
    public void OnHitFinished()
    {
        currentState = ActionState.IdleMove;
        if (anim != null)
        {
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("BlockHit");
        }
        // 受击结束后，如果格挡键还按着，重新进入格挡状态
        if (isBlocking)
        {
            anim.SetBool("IsBlocking", true);
            // 重置待机计时器，避免立即播放待机动画
            IdleSelector idleSelector = GetComponent<IdleSelector>();
            if (idleSelector != null) idleSelector.ResetIdleTimer();
        }
        else
        {
            // 没有按格挡键，重置待机
            IdleSelector idleSelector = GetComponent<IdleSelector>();
            if (idleSelector != null) idleSelector.ResetIdleTimer();
        }
         
    }
    
    
    //===============角色重生================
    private IEnumerator Respawn()
    {
        // 死亡后等待3秒钟（播放死亡动画）
        yield return new WaitForSeconds(3f);

        // 清除所有缓存的输入
        Input.ResetInputAxes();

        // 重置玩家状态
        nextAttackIsCrit = false;
        nextHeavyAttackIsFourth = false;
        comboPending = false;
        lightComboPending = false;
    
        currentHealth = maxHealth;
        currentStamina = maxStamina;
 
        currentState = ActionState.IdleMove;

        locomotion.ResetSpeed();
        targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持

        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;
    
        // ======= 把人物传送到记录的复活点 =======
        if (controller != null) controller.enabled = false; 
        
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;

        // 复活时同样瞬间重置相机到背后
        GetComponent<PlayerCameraController>().ResetCameraBehindPlayer();
        
        // ======= 重置动画 =======
        if (anim != null)
        {
            // 清理触发器防幽灵动画
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightCombo");
            anim.ResetTrigger("Cast");
            anim.ResetTrigger("Dodge");
            anim.ResetTrigger("Hit");
            anim.ResetTrigger("BlockHit");
            
            // 强行压制攻击层
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);

            anim.Play("Locomotion", 0, 0f);      
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsGrounded", true);
            anim.SetBool("IsStopping", false);
            anim.SetFloat("Speed", 0f);
            anim.SetFloat("Direction", 0f);
        } 
        
        if (controller != null) controller.enabled = true; // 传送完重新开启控制器

        // ============================================
        // 【新增】玩家死亡复活时，强制刷新世界上的所有敌人
        // ============================================
        foreach (BasicEnemyTest enemy in BasicEnemyTest.allEnemies)
        {
            if (enemy != null)
            {
                enemy.RespawnEnemy();
            }
        }

        // 稍微等待一小会儿以确保状态稳固
        yield return new WaitForSeconds(0.2f);
        
        //Debug.Log("玩家死亡后在最后一个赐福点复活！并且所有敌人已刷新！");
    }

    //==========闪避==================
    private void TryDodge()
    {
        // 检查耐力
        if (!ConsumeStamina(dodgeStaminaCost))
        {
            //Debug.Log("耐力不足，无法闪避");
            return;
        }

        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        currentState = ActionState.Dodging;
        anim.SetTrigger("Dodge");

        Vector3 dodgeDirection = GetDodgeDirection();
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + dodgeDirection * dodgeDistance;

        float elapsed = 0f;
        while (elapsed < dodgeDuration)  // dodgeDuration 现在仅用于位移时长，无敌由动画事件控制
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dodgeDuration;
            t = 1 - Mathf.Pow(1 - t, 2);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        currentState = ActionState.IdleMove;
        // 无敌已经在动画事件中关闭，这里不再需要
    }

    private Vector3 GetDodgeDirection()
    {
        // 获取输入方向
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            // 有输入时，向输入方向闪避
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = Camera.main.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            return (camForward * v + camRight * h).normalized;
        }
        else
        {
            // 无输入时，向后闪避
            return -transform.forward;
        }
    }

    // 闪避无敌开始（动画事件调用）
    public void OnDodgeInvincibleStart()
    {
        isInvincible = true;
        dodgeStartTime = Time.time;
        // 如果你希望完美窗口单独控制，可以再开一个协程，但更推荐再做一个事件
    }
    // 闪避无敌结束
    public void OnDodgeInvincibleEnd()
    {
        isInvincible = false;
        staminaRegenBuffTimer = 1.5f; // 耐力恢复加速
    }


    private IEnumerator PerfectDodgeReward()
    {
        // 播放完美闪避启动音效
        if (perfectDodgeStartSFX != null)
        {
           AudioPoolManager.Instance.PlaySound(perfectDodgeStartSFX, transform.position, 0.8f, null, true);
        }

        // 保存原始时间缩放，避免嵌套慢动作
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.25f;                    // 慢动作

        yield return new WaitForSecondsRealtime(0.5f); // 实际时间 0.5 秒

        Time.timeScale = originalTimeScale;
    }
    //====================================================================



    private void Die()
    {
        //Debug.Log("Die() 被调用！当前血量：" + currentHealth);
        if (isDead) return;
        currentState = ActionState.Dead;
        StopAllCoroutines();                // 停止所有协程
        Time.timeScale = 1f;                // 确保时间缩放恢复
        //Debug.Log("玩家死亡");
    
        // 播放死亡音效
        if (deathSFX != null && audioSource != null)
            audioSource.PlayOneShot(deathSFX, 1.0f);


        // 停止所有动作
        currentState = ActionState.IdleMove;

        locomotion.ResetSpeed();
    
        // 播放死亡动画（需要在 Animator 中添加 Death 状态和 Trigger 参数 "Die"）
        if (anim != null)
        {
            anim.SetTrigger("Die");
        }
    
        // 禁用角色控制器，防止移动
        if (controller != null)
        {
            controller.enabled = false;
        }
    
        // 可选：显示死亡 UI，延迟重生等
        // 这里简单地在 3 秒后重新激活（示例，你可以改成显示菜单）
        StartCoroutine(Respawn());
    }


    private void UpdateCombatBGM()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetCombatState(isInCombatCached);
    }




    private bool IsInCombat()
    {
        // 复用已有的锁定半径 lockOnRadius 来搜索敌人
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, lockOnRadius, enemyLayer);
        foreach (var col in hitColliders)
        {
            BasicEnemyTest enemy = col.GetComponent<BasicEnemyTest>();
            if (enemy != null)
            {
                // 如果敌人处于追逐或攻击状态，视为战斗中
                if (enemy.currentState == BasicEnemyTest.EnemyState.Chase || 
                    enemy.currentState == BasicEnemyTest.EnemyState.Attack ||
                    enemy.currentState == BasicEnemyTest.EnemyState.Hit)   // 受击也算战斗中
                {
                    return true;
                }
            }
        }
        return false;
    }

    
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2);

        // ======= 绘制搜索锁定半径 =======
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);
    }


    // ==========================================
    // 动画事件 (Animation Events) 调用的专属方法
    // ==========================================
    
    // 拔刀：瞬间切刀
    public void Event_DrawSword()
    {
        if (swordInScabbard != null) swordInScabbard.SetActive(false);
        if (swordInHand != null) swordInHand.SetActive(true);
    }

    // 收刀：瞬间切刀
    public void Event_SheathSword()
    {
        if (swordInHand != null) swordInHand.SetActive(false);
        if (swordInScabbard != null) swordInScabbard.SetActive(true);
    }

    // 左手松开刀柄 (单手挥刀时调用)
    public void Event_FreeLeftHand()
    {
        targetLeftHandIKWeight = 0f;
    }

    // 左手重新握住刀柄 (收招时调用)
    public void Event_HoldLeftHand()
    {
        targetLeftHandIKWeight = 1f;
    }

}