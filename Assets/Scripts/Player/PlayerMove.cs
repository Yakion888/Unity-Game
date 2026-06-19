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

    // 【代理 wrapper】：让旧逻辑不用改一行代码也能自动读取新状态机
    public bool isDead => currentState == ActionState.Dead;
    private bool isAttacking => currentState == ActionState.HeavyAttack;
    private bool isLightAttacking => currentState == ActionState.LightAttack;
    private bool isRunningAttack => currentState == ActionState.RunAttack;
    private bool isCasting => currentState == ActionState.SkillCast;
    private bool isUltimateCasting => currentState == ActionState.Ultimate;
    private bool isDodging => currentState == ActionState.Dodging;
    private bool isHit => currentState == ActionState.Hit;

    private float fsmStateTimer = 0f;

    [Header("战斗设置")]
    public float hitStopDuration = 0.05f;  // 命中停顿时间
    private float lastEventTime = 0f;    //记录上一次触发动画事件的真实时间，用于防抖

    [Header("攻击设置")]
    public float comboInputWindow = 0.3f;
    public bool canMoveWhileAttacking = false;


    [Header("全局基础挂载与特效")]
    public GameObject hitEffect;                 // 保底火花特效
    public Transform weaponPoint;                // 武器挂载点（剑尖）


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

    // ═══════════════════════════════════════════════════════
    // 【架构重构】事件驱动：订阅怪物死亡事件，自动发放经验金币。
    // 替代敌人 Die() 中 GetComponent<EldenRingMovement>() 的强耦合写法。
    // ═══════════════════════════════════════════════════════
    private void OnEnable()
    {
        BasicEnemyTest.OnEnemyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        BasicEnemyTest.OnEnemyDied -= OnEnemyDied;
    }

    private void OnDestroy()
    {
        // 释放当前装备武器的所有 Addressables 句柄
        _loadedWeaponAssets?.ReleaseAll();
        _loadedWeaponAssets = null;
    }

    /// <summary>收到怪物死亡事件 → 根据怪物配置自动发放经验与金币奖励</summary>
    private void OnEnemyDied(BasicEnemyTest enemy)
    {
        if (enemy == null) return;
        PlayerDataManager.Instance.AddXP(enemy.xpReward);
        PlayerDataManager.Instance.AddGold(enemy.goldReward);
    }

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
    
    public void TriggerHitStop() { StartCoroutine(HitStop()); }

    // ==============================================================
    // 数据驱动架构：当前装备的武器数据包
    // ==============================================================
    [Header("武器库与挂载")]
    public Transform weaponMountPoint;
    public List<WeaponDataSO> weaponInventory;
    private GameObject currentWeaponModel;
    private int currentWeaponIndex = 0;

    [Header("当前装备武器")]
    public WeaponDataSO currentWeapon;

    /// <summary>当前武器已加载的运行时资产（特效、音效、模型等）及其 AA 句柄</summary>
    private WeaponRuntimeAssets _loadedWeaponAssets;

    /// <summary>
    /// 武器加载代数。EquipWeaponAsync 每次调用 +1，异步加载完成后比对，
    /// 不匹配说明中途又有新切换 → 丢弃本次结果。
    /// </summary>
    private int _weaponLoadGeneration;

    // ══════════════════════════════════════════════════════
    // 代理属性
    // 数值字段 → 直接从 SO 读取（纯数据，无内存问题）
    // 资产字段 → 从 _loadedWeaponAssets 读取（运行时异步加载）
    // ══════════════════════════════════════════════════════

    // ── 数值（来源：SO）──
    public int[] heavyAttackDamage => currentWeapon.heavyAttackDamage;
    public int[] lightAttackDamage => currentWeapon.lightAttackDamage;
    public int runningAttackDamage => currentWeapon.runningAttackDamage;
    public float[] lightAttackForwardOffset => currentWeapon.lightAttackForwardOffset;
    public float[] lightAttackRadius => currentWeapon.lightAttackRadius;
    public float[] lightAttackAngle => currentWeapon.lightAttackAngle;
    public float[] heavyAttackKnockback => currentWeapon.heavyAttackKnockback;
    public float[] lightAttackKnockback => currentWeapon.lightAttackKnockback;
    public float runningAttackKnockback => currentWeapon.runningAttackKnockback;
    public float[] heavyAttackStaminaCost => currentWeapon.heavyAttackStaminaCost;
    public float runningAttackStaminaCost => currentWeapon.runningAttackStaminaCost;
    public float lightAttackRage => currentWeapon.lightAttackRage;
    public float heavyAttackRage => currentWeapon.heavyAttackRage;
    public float runningAttackRage => currentWeapon.runningAttackRage;
    public Vector3[] heavyAttackVFXRotations => currentWeapon.heavyAttackVFXRotations;

    // ── 资产（来源：_loadedWeaponAssets）──
    public GameObject[] heavyAttackEffects => _loadedWeaponAssets?.heavyAttackEffects;
    public GameObject[] heavyAttackHitEffects => _loadedWeaponAssets?.heavyAttackHitEffects;
    public GameObject[] lightAttackEffects => _loadedWeaponAssets?.lightAttackEffects;
    public GameObject runningAttackEffect => _loadedWeaponAssets?.runningAttackEffect;
    public AudioClip[] attackSwingSounds => _loadedWeaponAssets?.heavySwingSounds;
    public AudioClip[] attackHitSounds => _loadedWeaponAssets?.heavyHitSounds;
    public AudioClip[] lightAttackSwingSounds => _loadedWeaponAssets?.lightSwingSounds;
    public AudioClip[] lightAttackHitSounds => _loadedWeaponAssets?.lightHitSounds;
    public AudioClip slidingWhooshSound => _loadedWeaponAssets?.slidingWhooshSound;
    public AudioClip[] heavyAttackVoices => _loadedWeaponAssets?.heavyVoices;
    public AudioClip[] lightAttackVoices => _loadedWeaponAssets?.lightVoices;
    public AudioClip[] runningAttackVoices => _loadedWeaponAssets?.runningVoices;

    private Skill_WaveSlash skillWaveSlash;
    private Skill_QTEUltimate skillQTEUltimate;


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
    // ==============================================================
    // 动作缓冲与连段系统 (Combo Buffer System)
    // ==============================================================
    private bool comboPending = false;      // 玩家是否提前按下了攻击键？
    private bool canCombo = false;          // 当前动画是否处于“允许连招”的窗口期？
    private int currentAttackCombo = 0;     // 当前段数（0~4）

    private bool isProcessingAttackEnd;

    //格挡相关
    public bool isBlocking;

    // 技能相关
    private float castStartTime;

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

        skillWaveSlash = GetComponent<Skill_WaveSlash>();
        if (skillWaveSlash != null) skillWaveSlash.Initialize(this, animHandler);

        skillQTEUltimate = GetComponent<Skill_QTEUltimate>();
        if (skillQTEUltimate != null) skillQTEUltimate.Initialize(this, animHandler, inputHandler);
        
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
        PlayerDataManager.Instance.ApplySaveDataToScene(this);

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

        _ = EquipWeaponAsync(0); // 游戏开始时异步装备第一把武器

        // 7. 瞬间重置相机到背后
        GetComponent<PlayerCameraController>().ResetCameraBehindPlayer();
    }
    
    void Update()
    {
        if (isDead || isResting) return; // 死亡或休息时锁死逻辑

        fsmStateTimer += Time.deltaTime;

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
        if (isUIOpen || SettingsMenu.IsOpen)
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
        // 设置面板打开时屏蔽所有战斗输入，防止 UI 点击被缓存在游戏世界触发攻击
        if (SettingsMenu.IsOpen) return;

        // 1. 武器切换 (按下 Tab 且处于绝对自由状态时)
        if (inputHandler.SwitchWeaponInput && currentState == ActionState.IdleMove)
        {
            _ = EquipWeaponAsync(currentWeaponIndex + 1);
        }

        // =========================================================
        // 【架构绝杀：硬直状态拦截】
        // 如果玩家处于 受击、死亡、闪避、放大招、放技能 的状态中，
        // 直接 return 拦截！下面的 攻击、跳跃、格挡 全部失效！
        // 彻底消灭满天飞的 && !isHit && !isDodging...
        // =========================================================
        if (currentState == ActionState.Hit || currentState == ActionState.Dead || 
            currentState == ActionState.Dodging || currentState == ActionState.SkillCast || 
            currentState == ActionState.Ultimate || currentState == ActionState.RunAttack)
        {
            return; 
        }

        // 2. 格挡 (只有在自由站立或移动时才能举盾)
        bool wasBlocking = isBlocking;
        isBlocking = inputHandler.BlockInput && currentState == ActionState.IdleMove;
        if (isBlocking != wasBlocking)
        {
            if (isBlocking) GetComponent<IdleSelector>()?.ResetIdleTimer();
            else
            {
                animHandler.anim.SetFloat(animHandler.idleIndexHash, 0f);
                GetComponent<IdleSelector>()?.ResetIdleTimer();
            }
        }
        animHandler.anim.SetBool(animHandler.isBlockingHash, isBlocking);

        // 3. 闪避
        if (inputHandler.DodgeInput && currentState == ActionState.IdleMove && !isBlocking)
        {
            TryDodge();
            return; // 闪避优先级高，触发后直接 return
        }

        // 4. 重攻击
        if (inputHandler.HeavyAttackInput && !isBlocking)
        {
            if (currentWeapon == null) return; 

            // 如果当前在走/跑，直接发动第一段攻击
            if (currentState == ActionState.IdleMove)
            {
                if (locomotion.isRunning) StartRunningAttack();
                else StartHeavyAttack(); 
            }
            // 如果已经在重击状态中了...
            else if (currentState == ActionState.HeavyAttack)
            {
                // 如果当前正好处于“连击窗口期”，毫不犹豫，直接强制切到下一刀！
                if (canCombo) 
                {
                    ProceedToNextHeavyAttack();
                }
                // 如果还没砍中敌人（在举剑的前摇里），就把这次按键“缓存”起来！
                else 
                {
                    comboPending = true;
                }
            }
        }
        // 5. 轻攻击
        else if (inputHandler.LightAttackInput && !isBlocking)
        {
            if (currentWeapon == null) return; 

            // 如果当前在走/跑，直接发动第一段轻击
            if (currentState == ActionState.IdleMove) 
            {
                StartLightAttack();
            }
            // 如果已经在轻击状态中了...
            else if (currentState == ActionState.LightAttack)
            {
                // 如果处于“连击窗口期”，强制切到下一脚/肘！
                if (canCombo) 
                {
                    ProceedToNextLightAttack();
                }
                // 还没到判定点，缓存按键！
                else 
                {
                    comboPending = true;
                }
            }
        }

        // 6. 专属武器战技
        if (inputHandler.WeaponSkillInput && currentState == ActionState.IdleMove && !isBlocking)
        {
            if (currentWeapon == null) return; 

            if (currentRage >= maxRage)
            {
                currentRage = 0f;
                if (rageSlider != null) rageSlider.value = currentRage;

                fsmStateTimer = 0f;
                
                if (currentWeapon.exclusiveSkill == WeaponSkillType.WaveSlash && skillWaveSlash != null) skillWaveSlash.ExecuteSkill();
                else if (currentWeapon.exclusiveSkill == WeaponSkillType.QTEUltimate && skillQTEUltimate != null) skillQTEUltimate.ExecuteSkill();
            }
        }
    }

    private void HandleStatsAndTimers()
    {
        // 工业级 FSM 兜底机制：任何攻击动作超过 3 秒没收招，绝对是卡 Bug 了，强制重置！
        if (currentState == ActionState.HeavyAttack || currentState == ActionState.LightAttack || 
            currentState == ActionState.RunAttack || currentState == ActionState.SkillCast || 
            currentState == ActionState.Ultimate)
        {
            if (fsmStateTimer > 6.0f)
            {
                //Debug.LogWarning("警告：动作状态严重超时！强制解锁玩家 FSM！");
                OnAttackFinished();
            }
        }


        // 交给物理引擎处理急停判定
        locomotion.HandleStopTimers(hasMoveInput);

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

            if (enemy != null && enemy.currentState != BasicEnemyTest.EnemyState.Hidden && !enemy.isDead && enemy.currentState != BasicEnemyTest.EnemyState.Hit) // 可根据需求放宽条件
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
        fsmStateTimer = 0f; // 每次发动攻击，重置计时器
        if (!ConsumeStamina(runningAttackStaminaCost)) return;
        
        comboPending = false;      
        
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
    
    private void StartHeavyAttack()
    {
        //完美闪避奖励判定：如果触发了奖励，下标直接变成 3（代表第 4 段攻击）
        int comboIndex = nextHeavyAttackIsFourth ? 3 : 0; 

        float staminaCost = currentWeapon.heavyAttackStaminaCost[comboIndex];
        if (!stats.ConsumeStamina(staminaCost)) return;

        currentState = ActionState.HeavyAttack;
        currentAttackCombo = comboIndex;     
        canCombo = false;           
        comboPending = false;       
        fsmStateTimer = 0f; 

        animHandler.anim.SetLayerWeight(attackLayerIndex, 1f);
        animHandler.ResetAllTriggers(); 

        // 消耗掉闪避奖励标记
        if (nextHeavyAttackIsFourth)
        {
            nextHeavyAttackIsFourth = false;
        }

        // 精准播放对应段数的动画
        animHandler.anim.Play(animHandler.heavyAttackHashes[currentAttackCombo], attackLayerIndex, 0f);
        locomotion.ResetSpeed();
    }
    
    
    private void ProceedToNextHeavyAttack()
    {
        if (currentAttackCombo >= animHandler.heavyAttackHashes.Length - 1) return;

        float staminaCost = currentWeapon.heavyAttackStaminaCost[currentAttackCombo + 1];
        if (!stats.ConsumeStamina(staminaCost)) return;

        currentAttackCombo++;       
        canCombo = false;           
        comboPending = false;       
        fsmStateTimer = 0f;

        // 还原旧逻辑精髓：后续连招交给 Animator 的连线去平滑过渡！
        animHandler.anim.SetTrigger(animHandler.comboTrigger);
    }

    // 动画事件 1：打开连击窗口（在动画帧里，加在“伤害判定点”的下一帧！）
    public void Event_OpenComboWindow()
    {
        canCombo = true; 
        
        // 如果玩家刚才在举剑时狂点鼠标（存下了缓存），一到判定点，瞬间自动挥出下一刀！
        if (comboPending) 
        {
            if (currentState == ActionState.HeavyAttack) ProceedToNextHeavyAttack();
            else if (currentState == ActionState.LightAttack) ProceedToNextLightAttack();
        }
    }

    // 动画事件 2：关闭连击窗口（加在动画大后摇的中间，惩罚那些按晚了的玩家）
    public void Event_CloseComboWindow()
    {
        canCombo = false;
        comboPending = false;
    }

     private void StartLightAttack()
    {
        currentState = ActionState.LightAttack;
        currentAttackCombo = 0;     
        canCombo = false;           
        comboPending = false;       
        
        fsmStateTimer = 0f;

        animHandler.anim.SetLayerWeight(attackLayerIndex, 1f);
        animHandler.ResetAllTriggers();

        animHandler.anim.Play(animHandler.lightAttackHashes[currentAttackCombo], attackLayerIndex, 0f);
        locomotion.ResetSpeed();
    }
    
    private void ProceedToNextLightAttack()
    {
        if (currentAttackCombo >= animHandler.lightAttackHashes.Length - 1) return;

        currentAttackCombo++;       
        canCombo = false;           
        comboPending = false;       
        fsmStateTimer = 0f;

        // 轻击连段触发器
        animHandler.anim.SetTrigger(animHandler.lightComboTrigger);
    }

    // 动画事件 3：攻击彻底结束（加在动画的最后一帧）
    public void OnAttackFinished()
    {
        if (currentState != ActionState.HeavyAttack && currentState != ActionState.LightAttack && currentState != ActionState.RunAttack) return;

        // 解除攻击状态，归还移动权限
        currentState = ActionState.IdleMove;
        canCombo = false;
        comboPending = false;

        // 直接把攻击层的权重（Weight）归零！
        // 绝不去调用 CrossFade，底层的 Base Layer 自然会接管玩家的身体！
        if (attackLayerIndex >= 0) 
        {
            animHandler.anim.SetLayerWeight(attackLayerIndex, 0f);
        }
        
        GetComponent<IdleSelector>()?.ResetIdleTimer();
    }
    
    // 动态武器切换系统
    /// <summary>
    /// 异步装备武器。
    ///
    /// ═══════════════════════════════════════════════════════════
    /// 【加载流程】
    ///   1. 递增 _weaponLoadGeneration（作废所有正在进行的旧加载）
    ///   2. 异步加载新 SO 的全部 AssetReference（模型、特效、音效）
    ///   3. 校验代数 → 不通过说明加载期间又切了一次 → 丢弃本次结果
    ///   4. 释放旧 _loadedWeaponAssets（全部 AA 句柄 → Addressables.Release）
    ///   5. 销毁旧模型、实例化新模型
    ///   6. 同步 PlayerDataManager
    ///
    /// 【异常安全】
    ///   - 玩家死亡 → 方法内不检查，外部 caller 应在死亡时跳过调用
    ///   - 加载中途切武器 → 代数校验丢弃旧结果
    ///   - 加载失败 → 保留现有武器不变
    /// ═══════════════════════════════════════════════════════════
    /// </summary>
    public async System.Threading.Tasks.Task EquipWeaponAsync(int index)
    {
        if (weaponInventory == null || weaponInventory.Count == 0) return;

        if (index >= weaponInventory.Count) index = 0;
        if (index < 0) index = weaponInventory.Count - 1;

        WeaponDataSO targetWeapon = weaponInventory[index];

        // 若已是当前武器且资产已加载 → 无操作
        if (currentWeapon == targetWeapon && _loadedWeaponAssets != null) return;

        // ── 代数递增，旧加载全部作废 ──
        int myGeneration = ++_weaponLoadGeneration;

        // ── 异步加载新武器的全部资产 ──
        WeaponRuntimeAssets newAssets = null;
        try
        {
            newAssets = await new WeaponRuntimeAssets().LoadAsync(targetWeapon);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EquipWeapon] 加载武器 '{targetWeapon.weaponName}' 失败：{ex.Message}");
        }

        // ── 代数校验：加载期间是否又被切走了？ ──
        if (myGeneration != _weaponLoadGeneration)
        {
            newAssets?.ReleaseAll();
            return;
        }

        // ── 原子交换 ──
        // 1. 释放旧资产
        if (_loadedWeaponAssets != null)
        {
            _loadedWeaponAssets.ReleaseAll();
            _loadedWeaponAssets = null;
        }

        // 2. 销毁旧 3D 模型
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
            currentWeaponModel = null;
        }

        // 3. 替换 SO 引用
        currentWeaponIndex = index;
        currentWeapon = targetWeapon;

        // 4. 挂载新运行时资产
        _loadedWeaponAssets = newAssets;

        // 5. 实例化新 3D 模型
        if (_loadedWeaponAssets?.weaponModelPrefab != null && weaponMountPoint != null)
        {
            currentWeaponModel = Instantiate(_loadedWeaponAssets.weaponModelPrefab, weaponMountPoint);
            currentWeaponModel.transform.localPosition = Vector3.zero;
            currentWeaponModel.transform.localRotation = Quaternion.identity;
            currentWeaponModel.transform.localScale = _loadedWeaponAssets.weaponModelPrefab.transform.localScale;
            currentWeaponModel.SetActive(true);

            Transform tip = currentWeaponModel.transform.Find("WeaponPoint");
            if (tip != null) weaponPoint = tip;
        }

        // 6. 同步数据中心
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.weaponName = currentWeapon.weaponName;
            PlayerDataManager.Instance.weaponBaseAttack = currentWeapon.weaponBaseAttack;
            PlayerDataManager.Instance.RecalculateAttributes(this);
        }

        //Debug.Log($"[EquipWeapon] 已装备：{currentWeapon.weaponName}");
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

                // 【数据驱动】：根据当前攻击状态，智能选取对应的受击火花
                GameObject vfxToUse = hitEffect; // 全局保底白字火花

                if (isAttacking && heavyAttackHitEffects != null && currentAttackCombo >= 0 && currentAttackCombo < heavyAttackHitEffects.Length)
                {
                    vfxToUse = heavyAttackHitEffects[currentAttackCombo];
                }
                else if (isLightAttacking && lightAttackEffects != null && lightAttackEffects.Length > 0)
                {
                    // 轻击时拿轻击的火花 (建议你也给轻击在 SO 里加一个 lightAttackHitEffects 数组，这里为了兼容先这样写)
                    vfxToUse = hitEffect; 
                }
                // 滑行攻击等可以以此类推...

                // 从对象池生成这朵专属火花
                SpawnHitEffect(vfxToUse, sparkPos, attachTarget);
            
                // 【音效播放】
                PlayAttackHit(sparkPos);
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


    // ⚔️ 优雅重构 1：获取伤害
    int GetCalculatedDamage()
    {
        float baseDamage = 10f;
        if (isLightAttacking && currentAttackCombo < lightAttackDamage.Length) 
            baseDamage = lightAttackDamage[currentAttackCombo];
        else if (isAttacking && currentAttackCombo < heavyAttackDamage.Length) 
            baseDamage = heavyAttackDamage[currentAttackCombo];
        else if (isRunningAttack) 
            baseDamage = runningAttackDamage;

        float totalDamage = baseDamage + attackPowerBonus;
        return Mathf.RoundToInt(totalDamage * Random.Range(0.9f, 1.1f));
    }

    // 重构 2：获取击退力
    float GetCurrentKnockbackForce()
    {
        if (isLightAttacking && currentAttackCombo < lightAttackKnockback.Length) return lightAttackKnockback[currentAttackCombo];
        if (isAttacking && currentAttackCombo < heavyAttackKnockback.Length) return heavyAttackKnockback[currentAttackCombo];
        if (isRunningAttack) return runningAttackKnockback;
        return 5f;
    }

    // 重构 3：播放攻击与受击音效 (将原本繁琐的方法合并精简)
    public void PlayAttackSwing()
    {
        AudioClip[] clips = isLightAttacking ? lightAttackSwingSounds : attackSwingSounds;
        if (clips != null && currentAttackCombo < clips.Length && clips[currentAttackCombo] != null)
        {
            audioSource.pitch = (isAttacking && currentAttackCombo == 3) ? 1.2f : 1f; // 第4段重击稍微提音调
            audioSource.PlayOneShot(clips[currentAttackCombo], 0.9f);
            StartCoroutine(ResetPitch());
        }
    }

    public void PlayAttackHit(Vector3 hitPos)
    {
        AudioClip[] clips = isLightAttacking ? lightAttackHitSounds : attackHitSounds;
        if (clips != null && currentAttackCombo < clips.Length && clips[currentAttackCombo] != null)
        {
            // 加上 null, true，强制变为极其震撼的 2D 贴耳音效！
            AudioPoolManager.Instance.PlaySound(clips[currentAttackCombo], hitPos, 0.7f, null, true);
        }
    }
    

     // 命中停顿协程
    System.Collections.IEnumerator HitStop()
    {
        //设为极其接近于0的微小数值（比如 0.05f 或 0.1f），保留引擎渲染刷新！
        Time.timeScale = 0.05f;
        
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
    }
    

    // ==========================================
    //  极致优化版：重攻击特效生成
    // ==========================================
    // 【架构秘籍】：在动画机打 Event 时，直接传入 int 类型的参数 (0, 1, 2, 3, 4)
    // 彻底消灭 GetCurrentAnimatorStateInfo 的字符串硬编码！
    public void SpawnHeavyEffect(int index)
    {
        if (!isAttacking && !isLightAttacking && !isRunningAttack) return;
        if (heavyAttackEffects == null || index < 0 || index >= heavyAttackEffects.Length) return;

        GameObject effectPrefab = heavyAttackEffects[index];
        if (effectPrefab == null || weaponPoint == null) return;

        // 1. 动态限距（防穿模）
        Vector3 defaultSpawnPos = weaponPoint.position;
        Vector3 playerChest = transform.position + Vector3.up * 1.2f;
        Vector3 dirToWeapon = defaultSpawnPos - playerChest;
        Vector3 finalSpawnPos = defaultSpawnPos;

        if (Physics.SphereCast(playerChest, 0.3f, dirToWeapon.normalized, out RaycastHit hit, dirToWeapon.magnitude, enemyLayer, QueryTriggerInteraction.Ignore))
        {
            finalSpawnPos = playerChest + dirToWeapon.normalized * Mathf.Max(0, hit.distance - 0.1f);
        }

        // 2. 数据驱动的特效旋转
        Quaternion spawnRot = weaponPoint.rotation;
        if (heavyAttackVFXRotations != null && index < heavyAttackVFXRotations.Length)
        {
            // 从 SO 数据包中动态读取欧拉角进行旋转
            spawnRot *= Quaternion.Euler(heavyAttackVFXRotations[index]);
        }

        // 3. 对象池生成
        GameObject effect = VFXPoolManager.Instance.SpawnFromPool(effectPrefab, finalSpawnPos, spawnRot);
        effect.SetActive(false);
        effect.SetActive(true);
        StartCoroutine(DelayedPlay(effect));
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

        // 轻击和滑行特效直接使用剑尖的默认旋转即可
        // 彻底抛弃被删除的 GetAttackRotation 硬编码方法
        Quaternion spawnRot = weaponPoint.rotation;
        
        // 【重构】：用对象池拿取
        GameObject effect = VFXPoolManager.Instance.SpawnFromPool(effectPrefab, finalSpawnPos, spawnRot);
    
        effect.SetActive(false);
        effect.SetActive(true);
        StartCoroutine(DelayedPlay(effect));
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
        bool isHyperArmor = (currentState == ActionState.SkillCast || currentState == ActionState.Ultimate);
        if (isHyperArmor)
        {
            // 霸体状态下：有 40% 的免伤，并且只扣血，绝对不触发受击硬直！
            currentHealth -= finalDamage * 0.6f;
            if (healthSlider != null) healthSlider.value = currentHealth;
            
            //Debug.Log("FSM 施法霸体生效，已承受伤害但免疫打断！");
            if (currentHealth <= 0) Die();     
            return; // 强行 return，底下的打断攻击和受击动画统统不执行！
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
            StopCoroutine("DodgeRoutine");
            currentState = ActionState.Hit;
            
            targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持 

            animHandler.ResetAllTriggers();
            
            if (attackLayerIndex >= 0)  anim.SetLayerWeight(attackLayerIndex, 0f);   
        }
        StopCoroutine("DodgeRoutine");
        currentState = ActionState.Hit;
        
        // 播放受击音效 (根据最终伤害判定)
        if (playerHitSounds != null && playerHitSounds.Length >= 5)
        {
            AudioClip clip = finalDamage < 15 ? playerHitSounds[Random.Range(0, 3)] : playerHitSounds[Random.Range(3, 5)];
            audioSource.PlayOneShot(clip, 0.8f);
        }

        if (anim != null)
        {
            animHandler.anim.SetTrigger(animHandler.hitTrigger);
        }
        
        StopCoroutine(nameof(ResetHit));
        StartCoroutine(nameof(ResetHit));

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
            animHandler.anim.SetTrigger(animHandler.blockHitTrigger);
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
        targetLeftHandIKWeight = 1f;

        locomotion.ResetSpeed();

        if (anim != null)
        {
            animHandler.ResetAllTriggers();
            
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
        targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持
        
        locomotion.ResetSpeed();

        if (anim != null)
        {
            // 手动清除所有手残多按的触发器缓存
            animHandler.ResetAllTriggers();

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
            animHandler.anim.ResetTrigger(animHandler.hitTrigger);
            animHandler.anim.ResetTrigger(animHandler.blockHitTrigger);
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
            animHandler.ResetAllTriggers();
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
            animHandler.ResetAllTriggers();
            
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
            animHandler.anim.SetTrigger(animHandler.dieTrigger);
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