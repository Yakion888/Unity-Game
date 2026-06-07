using UnityEngine;
using System.Collections;
using System.Collections.Generic;  
using UnityEngine.UI;
using UnityEngine.Animations.Rigging; // 引入动画绑定库

public class EldenRingMovement : MonoBehaviour
{
    [Header("移动速度")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 15f;

    [Header("跳跃与重力设置")]
    public float jumpHeight = 1.5f;           // 跳跃高度
    public float jumpStaminaCost = 15f;       // 跳跃消耗耐力
    public float gravityMultiplier = 2.5f;    // 下落时的重力倍增（动作游戏标配，消除轻飘飘的感觉）
    public float terminalVelocity = -30f;     // 终端速度（最大下落速度限制）
    private bool isJumping;
    private float jumpStartSpeed = 0f;
    
    [Header("旋转设置")]
    public float rotationSpeed = 540f;
    public float idleRotationSpeed = 360f;
    
    [Header("相机设置")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;
    public float verticalLookLimit = 80f;
    public float cameraDistance = 5.5f;     // 相机距离（默认5.5，越大离得越远）
    public float cameraHeight = 1.5f;       // 相机自身的高度
    public float lookAtHeight = 1.2f;       // 相机的“准星”看着玩家身体的哪个高度（稍微调低能看到脚）

    [Header("相机碰撞")]
    public LayerMask cameraCollisionMask = -1;   // 相机碰撞的层（建议设置为地形、建筑等）
    public float cameraCollisionRadius = 0.2f;   // 球形检测半径
    public float cameraMinDistance = 0.5f;       // 相机最近距离（防止拉得过近）

    // ======= 锁定系统 =======
    [Header("锁定设置")]
    public float lockOnRadius = 20f;      // 搜索敌人的半径
    public LayerMask enemyLayer;          // 敌人所在的图层
    public Transform lockedTarget;        // 当前锁定的目标
    public bool isLockedOn;               // 是否处于锁定状态

    // ======= 锁定UI =======
    [Header("锁定UI设置")]
    public RectTransform lockOnUI;        // 拖入你的锁定图标 (Image的RectTransform)
    
    [Header("急停设置")]
    public float stopAnimationDuration = 0.5f;
    
    [Header("战斗设置")]
    public float hitStopDuration = 0.05f;  // 命中停顿时间
    private float lastEventTime = 0f;    //记录上一次触发动画事件的真实时间，用于防抖

    [Header("攻击设置")]
    public float comboInputWindow = 0.3f;
    public bool canMoveWhileAttacking = false;

    [Header("攻击伤害")]
    public int[] heavyAttackDamage = new int[5] { 15, 20, 20, 35, 50 };   // 5段重攻击伤害
    public int[] lightAttackDamage = new int[3] { 10, 12, 15 };            // 3段轻攻击伤害
    public int runningAttackDamage = 50;                                   // 滑行攻击伤害

    [Header("轻攻击范围动态调优")]
    // 3段轻攻击的【球体向前推的距离】（例如：肘击推0.8米，飞踢推1.2米）
    public float[] lightAttackForwardOffset = new float[3] { 1.0f, 1.5f, 1.5f }; 
    // 3段轻攻击的【球体半径大小】（例如：肘击范围0.5米，飞踢范围0.8米）
    public float[] lightAttackRadius = new float[3] { 0.5f, 0.8f, 0.8f };

    [Header("攻击特效")]
    public GameObject[] heavyAttackEffects;      // 重攻击特效（5段）
    public GameObject[] lightAttackEffects;      // 轻攻击特效（3段）
    public GameObject runningAttackEffect;       // 滑行攻击特效
    public GameObject skillEffect;               // 技能特效
    public GameObject hitEffect;                 // 命中特效
    public GameObject skillHitEffect;            // 1技能专属的带火属性的命中爆点特效
    public Transform weaponPoint;                // 武器挂载点（剑尖）

    [Header("击退值设置")]
    public float[] heavyAttackKnockback = new float[5] { 5f, 6f, 7f, 8f, 10f };  // 5段重攻击
    public float[] lightAttackKnockback = new float[3] { 3f, 4f, 6f };            // 3段轻攻击
    public float runningAttackKnockback = 8f;                                      // 滑行攻击

    // 玩家 UI 引用
    [Header("玩家UI设置")]
    public Slider healthSlider;
    public Slider staminaSlider;
    public Slider rageSlider;
    // 【新增】UI数值文本引用
    public TMPro.TextMeshProUGUI healthText;
    public TMPro.TextMeshProUGUI staminaText;
    public TMPro.TextMeshProUGUI rageText;
    // 用来记录上一帧数值，防止每帧重复刷新文字导致卡顿
    private int lastHealth = -1;
    private int lastStamina = -1;
    private int lastRage = -1;

    [Header("怒气系统")]
    public float maxRage = 100f;               // 最大怒气值
    public float currentRage = 0f;             // 当前怒气值
    public float baseRageRegenRate = 2f;       // 满血时的基础怒气恢复（每秒）
    public float maxRageRegenRate = 15f;       // 残血时的最大怒气恢复（每秒）
    public float ragePerDamageMultiplier = 0.15f; // 造成伤害转化为怒气的比例（比如打50点伤害，积攒7.5点怒气）
    public float rageDecayRate = 5f;           // 脱战后的怒气衰减速度（每秒掉多少怒气）

    [Header("存档与复活系统")]
    public Vector3 respawnPosition;      // 记录的复活位置
    public Quaternion respawnRotation;   // 记录的复活朝向

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

    [Header("RPG 属性系统")]
    public int currentLevel = 1;         // 当前等级
    public int currentXP = 0;            // 当前经验值（卢恩/灵魂）
    public int statPoints = 0;           // 可用的属性点（如果有系统直接加点，可以不用这个，直接消耗经验升级某项）
    public int currentGold = 0;          // 当前拥有的金币数量

    [Header("=== 武器强化系统 ===")]
    public string weaponName = "狼的末路";   // 当前佩戴的武器名称
    public int weaponLevel = 0;             // 武器当前等级 (0 = 未强化)
    public int maxWeaponLevel = 25;         // 最高强化等级（如老头环的+25）
    public float weaponBaseAttack = 40f;    // 武器初始自带的基础攻击力
    public float upgradeAttackBonus = 8f;   // 每次强化增加的攻击力

    [Header("基础加点属性")]
    public int statVigor = 10;           // 生命力（影响最大生命值）
    public int statEndurance = 10;       // 持久力（影响最大耐力）
    public int statStrength = 10;        // 力量（增加物理攻击力）
    public int statResistance = 10;      // 坚韧度（增加物理防御力）
    public int statSpirit = 10;          // 精神力（提高怒气获取效率）

    [Header("面板衍生属性 (根据加点自动计算)")]
    public float maxHealth;              
    public float maxStamina;             
    public float attackPowerBonus;       // 攻击力加成
    public float defensePower;           // 防御力
    public float rageGainMultiplier;     // 怒气获取倍率

    [Header("生命值系统")]
    //public float maxHealth = 300f;           // 最大生命值
    public float currentHealth;              // 当前生命值
    public bool isDead = false;              // 是否死亡
    

    [Header("耐力系统")]
    //public float maxStamina = 300f;           // 最大耐力
    public float currentStamina;              // 当前耐力
    public float staminaRegenRate = 15f;      // 每秒恢复速度
    public float staminaRegenDelay = 1f;      // 停止消耗后多久开始恢复
    private float staminaRegenTimer = 0f;     // 恢复延迟计时器

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
    private bool isUltimateCasting = false;   // 是否正在释放大招
    private bool isWaitingForQTE = false;     // 是否正处于子弹时间等待按键
    private bool qteSuccess = false;          // QTE是否按成功了



    [Header("终极大招音效 (QTE与连段)")]
    public AudioClip ultChargeSFX;          // 蓄力音效（按下大招瞬间播放）
    public AudioClip[] ultUpwardSlashSFXs;  // 4段上挑音效（建议容量填4，放入4个不同的破风声）
    public AudioClip ultSlowMotionSFX;      // 空中滞留慢放音效（子弹时间高频耳鸣声）
    public AudioClip ultQTESuccessSFX;      // QTE按键成功音效（清脆的“叮”声，若空则默认用完美闪避音效）
    public AudioClip ultSlamSFX;            // 终结重击砸地音效（沉重的爆炸/砸地声）

    [Header("终极大招特效")]
    public GameObject[] ultSlashEffects; // 前 4 段的不同角度剑光
    public GameObject ultLaunchEffect;   // 第 5 段的垂直升龙剑光！
    public GameObject ultFinalSlashEffect; //QTE 终结下劈的专属剑光
    public GameObject ultSlamEffect;     // 砸地爆发特效
    public GameObject ultHitEffect;


    [Header("耐力消耗")]
    public float sprintStaminaCost = 25f;         // 奔跑每秒消耗
    public float runningAttackStaminaCost = 25f; // 滑行攻击消耗
    public float[] heavyAttackStaminaCost = new float[5] { 10f, 12f, 14f, 16f, 18f };
    // 耐力耗尽禁止奔跑系统
    private float staminaBlockRemaining = 0f;
    private const float STAMINA_BLOCK_DURATION = 1.5f;  // 可改为 public 以便在 Inspector 调整

    [Header("闪避设置")]
    public float dodgeDistance = 3f;           // 闪避位移距离
    public float dodgeDuration = 0.4f;         // 闪避动画时长/无敌时长
    public float dodgeStaminaCost = 25f;       // 闪避消耗耐力
    private bool isDodging = false;            // 是否正在闪避
      
    [Header("音效设置")]
    public AudioSource audioSource;
    public AudioClip[] attackSwingSounds;      // 重攻击挥剑音效 (5段)
    public AudioClip[] attackHitSounds;        // 重攻击击中音效 (5段)
    public AudioClip[] lightAttackSwingSounds; // 轻攻击挥剑音效 (3段)
    public AudioClip[] lightAttackHitSounds;   // 轻攻击击中音效 (3段)
    public AudioClip slidingWhooshSound;       // 滑行时的大剑破风声
    public AudioClip castSound;                // 施法音效
    public AudioClip[] blockSounds;            // 格挡音效
    public AudioClip[] playerHitSounds;        // 玩家受击音效
    public AudioClip perfectDodgeStartSFX;   // 启动音效（一播放）
    

    [Header("角色语音")]
    public AudioClip[] heavyAttackVoices = new AudioClip[5];
    public AudioClip[] lightAttackVoices;   // 轻攻击语音数组
    public AudioClip[] runningAttackVoices; // 滑行攻击语音数组
    public AudioClip[] skillVoices;         // 技能语音数组（可选）
    public AudioClip deathSFX;              // 死亡语音

    [Header("草地脚步声")]
    public AudioClip[] grassFootsteps;        // 行走单步采样数组

    [Header("战斗音乐冷却")]
    public float combatCooldownDuration = 2f;   // 脱战冷却时间（秒）
    private float combatCooldownTimer = 0f;
    private bool isInCombatEffective = false;   // 经过冷却过滤后的实际战斗状态
    
    // 移动相关
    private Vector2 moveInput;
    private float currentSpeed;
    private bool isRunning;
    private float stopStartTime;
    private float lastSpeed;
    private bool wasMoving;

    // 重力相关
    private float verticalVelocity;
    private float gravity = -9.81f;
    private float airTimer = 0f; //// 悬空计时器（用于解决上下楼梯时的重力防抖）
    
    // 相机相关
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    
    // 攻击相关
    private bool isAttacking;
    private int attackLayerIndex;
    private bool comboPending;
    private float comboPendingTime;
    private bool isRunningAttack;
    private bool isProcessingAttackEnd;
    private bool isLightAttacking;
    private bool lightComboPending;
    private float lightComboPendingTime;
    public bool isBlocking;
    private int currentAttackCombo = 0;   // 当前重攻击段数（1~5）

    // 技能相关
    private bool isCasting;
    private float castStartTime;
    private bool isCastingInvincible;   // 施法霸体状态


    
    // 受击相关
    private bool isHit;
    private float hitRecoveryTime;
    private Vector3 impact;
    
    // 动画相关
    private Animator anim;
    private CharacterController controller;
    private Quaternion targetRotation;
    private float currentTurnAngle;

    // 急停相关状态控制
    private bool isStopping;
    private float stopTimer;
    private bool wasRunning;

    // 脚步声相关
    private AudioSource footstepSource;       // 脚步音源
    private float footstepTimer = 0f;         // 脚步计时器
    private float walkInterval = 0.5f;        // 行走脚步间隔（秒）
    private float runInterval = 0.3f;         // 奔跑脚步间隔（秒）

    // 闪避增强机制
    private bool isInvincible = false;          // 是否处于无敌状态
    private float dodgeStartTime = 0f;          // 闪避开始的时间戳
    private bool nextAttackIsCrit = false;      // 下一次攻击是否必定暴击
    private bool nextHeavyAttackIsFourth = false;   // 下一次重击是否直接变成第四段
    private float staminaRegenBuffTimer = 0f;   // 耐力恢复加速剩余时间

    //战斗状态缓存
    private bool isInCombatCached = false;
    private float combatCheckTimer = 0f;
    
    void Start()
    {
        anim = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();

        // ======= 初始化UI隐藏 =======
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
        
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (anim != null)
        {
            attackLayerIndex = anim.GetLayerIndex("AttackLayer");
            if (attackLayerIndex >= 0)
            {
                anim.SetLayerWeight(attackLayerIndex, 0f);
            }
        }
        

        // 【5.18新增】初始化复活点为游戏开始时的坐标
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;

        // 【5.19新增】尝试从硬盘读取上次的赐福点进度
        LoadGame();

        // 读完档算出真正的最大血量后，再把当前血量和耐力回满
        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentRage = 0f; 

        // 初始化 UI 面板
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
        if (rageSlider != null)
        {
            rageSlider.maxValue = maxRage;
            rageSlider.value = currentRage;
        }

        // 创建独立的脚步AudioSource（可选，便于单独控制音量）
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.spatialBlend = 0.5f;
        footstepSource.volume = 0.6f;

        // 🎵 动态加载草地脚步声（从 Resources 文件夹）
        LoadGrassFootsteps();
        
        // 初始化音效系统
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = 1f;
        
        // 彻底关闭玩家主音源的物理变调，保证所有打击声、挥刀声纯净无比！
        audioSource.dopplerLevel = 0f;

        Debug.Log($"相机前向: {Camera.main.transform.forward}");

        // 游戏启动的第一帧，强行把相机拉回主角宽阔的后背！
        ResetCameraBehindPlayer(); 
    }
    
    void Update()
    {
        if (isDead || isResting) return;   // 如果死亡 或者 正在休息转场中，彻底锁死玩家的所有移动和输入   
 
        // ============================================
        // 【新增】处理 UI 开启时的鼠标状态
        // ============================================
        if (isUIOpen)
        {
            // UI打开时：显示鼠标，解锁鼠标，停止移动和转角
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
        }
        else
        {
            // UI关闭时：隐藏并锁定鼠标
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // ======= 处理锁定输入 =======
        HandleLockOnInput();
        
        // ======= 战斗状态缓存（每0.5秒更新一次） =======
        combatCheckTimer -= Time.deltaTime;
        if (combatCheckTimer <= 0f)
        {
            isInCombatCached = IsInCombat();
            combatCheckTimer = 0.5f;
        }

        // 战斗冷却逻辑
        if (isInCombatCached)
        {
            // 发现敌人，重置冷却计时器
            combatCooldownTimer = combatCooldownDuration;
            if (!isInCombatEffective)
            {
                // 从非战斗进入战斗：通知 AudioManager 从头播放战斗 BGM
                isInCombatEffective = true;
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetCombatState(true, true);   // true 表示强制重启战斗 BGM
            }
            else
            {
                // 已在战斗中，无需重启 BGM（仅确保战斗 BGM 正在播放，但不重置）
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetCombatState(true, false);  // false 表示不重启
            }
        }
        else
        {
            if (combatCooldownTimer > 0f)
            {
                combatCooldownTimer -= Time.deltaTime;
                // 冷却期间仍然认为处于有效战斗状态，保持战斗 BGM 播放
                // 但不需要重新调用 SetCombatState，避免频繁操作
            }
            else
            {
                if (isInCombatEffective)
                {
                    // 冷却结束，真正脱战
                    isInCombatEffective = false;
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.SetCombatState(false, false);
                }
            }
        }       


        // ========== 输入 ==========
        //如果 UI 开着，强行把 WASD 和 鼠标滑动 设为 0
        float h = isUIOpen ? 0f : Input.GetAxisRaw("Horizontal");
        float v = isUIOpen ? 0f : Input.GetAxisRaw("Vertical");
        bool runInput = isUIOpen ? false : Input.GetKey(KeyCode.LeftShift);
        
        if (Mathf.Abs(h) < 0.1f) h = 0;
        if (Mathf.Abs(v) < 0.1f) v = 0;
        
        moveInput = new Vector2(h, v).normalized;

        bool hasMoveInput = moveInput.magnitude > 0.1f && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting && !isDodging;
        bool isCurrentlyRunning = runInput && hasMoveInput && Mathf.Abs(v) > 0.1f;
        
        // ========== 相机控制 ==========
        float mouseX = isUIOpen ? 0f : Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = isUIOpen ? 0f : Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        if (Mathf.Abs(mouseX) < 0.01f) mouseX = 0;
        if (Mathf.Abs(mouseY) < 0.01f) mouseY = 0;
        
        currentYaw += mouseX;
        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, -verticalLookLimit, verticalLookLimit);
        
        // ======= 修改：锁定时的相机接管 =======
        if (isLockedOn && lockedTarget != null)
        {
            Vector3 dirToTarget = lockedTarget.position - transform.position;
            // 强制接管Yaw（水平旋转）
            float targetYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * 10f);
            
            // 强制接管Pitch（垂直俯仰，让相机稍微低头看敌人）
            float distance = dirToTarget.magnitude;
            float deltaY = lockedTarget.position.y - (transform.position.y + 1.5f);
            float ratio = deltaY / Mathf.Max(distance, 1f);
            ratio = Mathf.Clamp(ratio, -1f, 1f);          // 防止浮点误差
            float targetPitch = -Mathf.Asin(ratio) * Mathf.Rad2Deg;
            currentPitch = Mathf.LerpAngle(currentPitch, targetPitch + 10f, Time.deltaTime * 5f);
        }
        else
        {
            // 自由视角下的鼠标控制
            currentYaw += mouseX;
            currentPitch -= mouseY;
        }
        // ======================================
        currentPitch = Mathf.Clamp(currentPitch, -verticalLookLimit, verticalLookLimit);

        if (cameraTransform != null)
        {
            // 【修改】：使用面板上的变量 cameraHeight 和 cameraDistance
            Vector3 desiredPosition = transform.position + Quaternion.Euler(currentPitch, currentYaw, 0) * new Vector3(0, cameraHeight, -cameraDistance);
    
            // 设定相机真正的观察焦点（准星位置）
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            
            // 计算相机到焦点的方向（反向）
            Vector3 cameraToPlayer = lookTarget - desiredPosition;
            float targetDistance = cameraToPlayer.magnitude;
            Vector3 direction = cameraToPlayer.normalized;
    
            // 进行球形碰撞检测
            RaycastHit hit;
            if (Physics.SphereCast(lookTarget, cameraCollisionRadius, -direction, out hit, targetDistance, cameraCollisionMask))
            {
                float distance = hit.distance - cameraCollisionRadius;
                distance = Mathf.Clamp(distance, cameraMinDistance, targetDistance);
                desiredPosition = lookTarget - direction * distance;
            }
    
            // 平滑移动相机
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, Time.deltaTime * 10f);
            cameraTransform.LookAt(lookTarget); // 看向新的焦点
        }
        
        // ========== 移动方向（基于相机）==========
        Vector3 targetMoveDirection = Vector3.zero;
        
        if (hasMoveInput)
        {
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            
            Vector3 camRight = Camera.main.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            
            targetMoveDirection = camForward * moveInput.y + camRight * moveInput.x;
            targetMoveDirection.Normalize();
        }
        
        // ========== 速度控制 ==========
        float targetSpeed = 0f;
        // 【新增】：大招期间不接受玩家的速度加成
        if (hasMoveInput && !isAttacking && !isLightAttacking && !isUltimateCasting && !isCasting)
        {
            targetSpeed = runInput ? runSpeed : walkSpeed;
        }
        
        float accel = hasMoveInput ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);
        // 【新增】：彻底锁死大招期间的脚本速度
        if (isUltimateCasting || isCasting) currentSpeed = 0f;
        
        // ========== 旋转控制 ==========
        
            // ======= 锁定时的强制朝向 =======
            if (isLockedOn && lockedTarget != null)
            {
                Vector3 dirToTarget = lockedTarget.position - transform.position;
                dirToTarget.y = 0;
                if (dirToTarget != Vector3.zero)
                {
                    // 1技能 (isCasting) 锁定转向变慢
                    float rotSpeed = (isAttacking || isLightAttacking || isUltimateCasting || isCasting) ? rotationSpeed * 0.2f : rotationSpeed;
                    targetRotation = Quaternion.LookRotation(dirToTarget);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotSpeed * Time.deltaTime);
                }
            }
            // ======= 自由视角的 WASD 转向 =======
            else if (hasMoveInput && targetMoveDirection.magnitude > 0.1f && (!isAttacking && !isLightAttacking || isUltimateCasting || isCasting))
            {
                 targetRotation = Quaternion.LookRotation(targetMoveDirection);
                // 1技能 (isCasting) 自由转向也变慢
                float currentRotSpeed = (isUltimateCasting || isCasting) ? rotationSpeed * 0.4f : (isRunning ? rotationSpeed : rotationSpeed * 0.8f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentRotSpeed * Time.deltaTime);
        
                float angle = Vector3.SignedAngle(transform.forward, targetMoveDirection, Vector3.up);
                currentTurnAngle = Mathf.Lerp(currentTurnAngle, Mathf.Clamp(angle / 90f, -1f, 1f), Time.deltaTime * 10f);
            }
            else if (!hasMoveInput && !isAttacking && !isLightAttacking && !isUltimateCasting)
            {
                targetRotation = Quaternion.LookRotation(transform.forward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, idleRotationSpeed * Time.deltaTime);
                currentTurnAngle = Mathf.Lerp(currentTurnAngle, 0f, Time.deltaTime * 5f);
            }
        

        
        // ========== 垂直移动、跳跃与重力（重构统合处理） ==========
        bool isGrounded = IsGrounded();
        
        if (isGrounded)
        {
            airTimer = 0f; // 重置悬空计时器
            
            // 核心修复1：只要检测到在地上了，无条件解除跳跃状态！恢复脚步声
            isJumping = false; 

            if (verticalVelocity < 0)
            {
                // 核心修复2：将 -2f 改为 -1f 或 -1.5f。
                // 向下的压力太大也会导致角色死死卡在楼梯立面上无法触发自动上台阶
                verticalVelocity = -1.5f; 
            }
        }
        else
        {
            airTimer += Time.deltaTime; // 累加悬空时间
        }

        // 跳跃输入
        if (Input.GetButtonDown("Jump") && isGrounded && !isAttacking && !isLightAttacking && !isCasting && !isHit && !isBlocking)
        {
            if (ConsumeStamina(jumpStaminaCost))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isJumping = true;
                jumpStartSpeed = Mathf.Clamp01(currentSpeed / runSpeed); 

                anim.SetFloat("Speed", jumpStartSpeed);
                anim.SetFloat("Direction", 0f);
                anim.SetBool("IsMoving", false);
                anim.SetTrigger("Jump");
            }
        }

        // 应用重力与终端速度
        if (!isGrounded)
        {
            // 防抖机制：只有悬空时间超过 0.15 秒，才认为是真正的掉落，施加多倍重力
            // 完美避免了爬楼梯时那 1、2 帧的微小脱地导致重力剧增卡死
            if (verticalVelocity < 0 && airTimer > 0.15f)
            {
                verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        // 限制最大下落速度，防止高空穿模或速度异常
        verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
    
        // ========== 移动统一执行==========
        if (controller != null && controller.enabled)
        {
            Vector3 horizontalVelocity = Vector3.zero;

            if (isCasting)
            {
                moveInput = Vector2.zero;
                isRunning = false;
                currentSpeed = 0f;
                horizontalVelocity = Vector3.zero; 
                UpdateAnimation(false);
            }
             // 大招期间，脚本强制不提供任何水平位移，全权交给动画自身的 Root Motion
            else if (isUltimateCasting)
            {
                horizontalVelocity = Vector3.zero;
            }
            else if (targetMoveDirection.magnitude > 0.1f && !isAttacking && !isLightAttacking)
            {
                horizontalVelocity = targetMoveDirection * currentSpeed;
                lastSpeed = currentSpeed;
            }

            //叠加玩家受击时的击退冲击力
            if (impact.magnitude > 0.1f)
            {
                horizontalVelocity += impact;
                // 摩擦力衰减（数值10f代表减速的快慢）
                impact = Vector3.Lerp(impact, Vector3.zero, Time.deltaTime * 10f); 
            }

            // 合并水平与垂直速度（m/s），并统一在此处乘以 Time.deltaTime
            Vector3 finalVelocity = horizontalVelocity + new Vector3(0, verticalVelocity, 0);
            controller.Move(finalVelocity * Time.deltaTime);
        }
        
        // 同步耐力条 UI
        if (staminaSlider != null)  staminaSlider.value = currentStamina;

        // ===========================================
        // 【新增】怒气动态自动积攒逻辑（战时积攒，脱战消退；血量越低，积攒越快）
        // ===========================================
        if (!isDead)
        {
            if (isInCombatCached) 
            {
                // 1. 处于战斗中：自动积攒怒气（血越低越快）
                if (currentRage < maxRage)
                {
                    float healthPercent = currentHealth / maxHealth;
                    float currentRegenRate = Mathf.Lerp(maxRageRegenRate, baseRageRegenRate, healthPercent);
                    
                    currentRage += currentRegenRate * Time.deltaTime;
                    currentRage = Mathf.Clamp(currentRage, 0f, maxRage);
                }
            }
            else 
            {
                // 2. 脱离战斗：缓慢流失怒气
                if (currentRage > 0f)
                {
                    currentRage -= rageDecayRate * Time.deltaTime;
                    currentRage = Mathf.Clamp(currentRage, 0f, maxRage);
                }
            }

            // 同步怒气条UI
            if (rageSlider != null) rageSlider.value = currentRage;
        }
            

        // ========== 状态更新 ==========
        bool wasMovingPrev = wasMoving;
        bool wasRunningPrev = wasRunning; // 记录上一帧的真实奔跑状态
        isRunning = runInput && hasMoveInput && (Mathf.Abs(v) > 0.1f || Mathf.Abs(h) > 0.1f);
        wasMoving = hasMoveInput;
        wasRunning = isRunning; // 刷新当前帧的奔跑状态

        // 急停判定与打断逻辑
        // 触发条件：上一帧在移动，当前帧松开了方向键，且之前的速度大于步行速度（即处于奔跑状态）
        if (wasMovingPrev && !hasMoveInput && wasRunningPrev) 
        {
            isStopping = true;
            stopTimer = stopAnimationDuration; // 使用你 Header 中定义的 stopAnimationDuration
            //Debug.Log("【急停测试】脚本已成功下发急停指令！");
        }

        // 打断与结束条件
        if (isStopping)
        {
            // 如果玩家进行任何操作（输入方向、攻击、闪避、跳跃、施法、受击、格挡等），立即打断急停
            if (hasMoveInput || isAttacking || isLightAttacking || isDodging || isJumping || isCasting || isHit || isBlocking)
            {
                isStopping = false;
            }
            else
            {
                // 如果没有操作，正常倒计时，时间结束恢复到 Locomotion
                stopTimer -= Time.deltaTime;
                if (stopTimer <= 0)
                {
                    isStopping = false;
                }
            }
        }

        // 同步地面状态给动画机（关键！）
        anim.SetBool("IsGrounded", isGrounded);
        
        // 处理禁止奔跑计时
        if (staminaBlockRemaining > 0f)
        {
            staminaBlockRemaining -= Time.deltaTime;
            isRunning = false;
            if (currentSpeed > walkSpeed) currentSpeed = walkSpeed;
            runInput = false;  // 可选：防止输入
        }
        else
        {
            // 正常奔跑耐力消耗
            if (isRunning && hasMoveInput && !isAttacking && !isLightAttacking && !isCasting)
            {
                if (isInCombatCached)
                {
                    float sprintCost = sprintStaminaCost * Time.deltaTime;
                    if (currentStamina >= sprintCost)
                    {
                        currentStamina -= sprintCost;
                        staminaRegenTimer = 0f;
                    }
                else
                    {
                        staminaBlockRemaining = STAMINA_BLOCK_DURATION;
                        isRunning = false;
                        currentSpeed = walkSpeed;
                        Debug.Log("耐力耗尽，禁止奔跑");
                    }
                }  
            }
        }

        // ========闪避输入（F键）========
        if (Input.GetKeyDown(KeyCode.F) && !isUIOpen && !isDodging && !isAttacking && !isLightAttacking && !isCasting && !isHit && !isBlocking)
        {
            TryDodge();
        }

        // ========== 格挡 ==========
        bool wasBlocking = isBlocking;
        isBlocking = Input.GetKey(KeyCode.LeftControl);
        
        if (isBlocking != wasBlocking)
        {
            if (isBlocking)
            {
                IdleSelector idleSelector = GetComponent<IdleSelector>();
                if (idleSelector != null) idleSelector.ResetIdleTimer();
            }
            else
            {
                anim.SetFloat("IdleIndex", 0f);
                IdleSelector idleSelector = GetComponent<IdleSelector>();
                if (idleSelector != null) idleSelector.ResetIdleTimer();
            }
        }
        anim.SetBool("IsBlocking", isBlocking);
        
        // ========== 连击计时 ==========
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
        
        // ========== 攻击输入 ==========
        // 【修复】：补上 !isHit 和其他状态限制，防止在硬直期间输入被错误吞掉
        if (Input.GetMouseButtonDown(0) && !isUIOpen && !isBlocking && !isHit && !isDodging && !isCasting && !isUltimateCasting)
        {
            Debug.Log($"攻击输入，当前 isAttacking={isAttacking}, comboPending={comboPending}");
            if (!isAttacking && !isLightAttacking && !isRunningAttack)
            {
                if (isCurrentlyRunning) StartRunningAttack();
                else StartAttack();
            }
            else if (isAttacking && !isRunningAttack)
            {
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
                if (!stateInfo.IsName("Attack5"))
                {
                    anim.SetTrigger("Combo");
                    comboPending = true;
                    comboPendingTime = comboInputWindow;
                }
            }
        }
        else if (Input.GetMouseButtonDown(1) && !isBlocking && !isHit && !isDodging && !isCasting)
        {
            if (!isAttacking && !isLightAttacking && !isRunningAttack)
            {
                StartLightAttack();
            }
            else if (isLightAttacking)
            {
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
                if (!stateInfo.IsName("LightAttack3"))
                {
                    anim.SetTrigger("LightCombo");
                    lightComboPending = true;
                    lightComboPendingTime = comboInputWindow;
                }
            }
        }

        // ========== 技能输入 ==========
        if (Input.GetKeyDown(KeyCode.Alpha1) && !isUIOpen && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting)
        {
            // 【修改】必须满怒气才能释放
            if (currentRage >= maxRage)
            {
                currentRage = 0f; // 释放瞬间清空怒气
                if (rageSlider != null) rageSlider.value = currentRage;
                
                StartCast();
            }
            else
            {
                Debug.Log($"怒气不足！当前怒气：{Mathf.FloorToInt(currentRage)}/{maxRage}");
            }
        }
        
        // ========== 终极大招输入 (测试键：2) ==========
        if (Input.GetKeyDown(KeyCode.Alpha2) && !isUIOpen && !isAttacking && !isLightAttacking && !isBlocking && !isHit && !isCasting && !isUltimateCasting)
        {
            StartUltimate();
        }

        // ========== QTE 瞬间的按键检测 ==========
        if (isWaitingForQTE)
        {
            // 如果在子弹时间内玩家按下了左键，且还没成功过
            if (Input.GetMouseButtonDown(0) && !qteSuccess)
            {
                TriggerQTESuccess();
            }
        }

        // ========== 动画更新 ==========
        UpdateAnimation(hasMoveInput);
        if (anim != null)
        {
            // 【修改】：防止在大招转圈时，底层的走路/跑步动画错误触发导致鬼畜
            anim.SetBool("IsMoving", hasMoveInput && !isAttacking && !isLightAttacking && !isUltimateCasting);
            anim.SetBool("IsRunning", isRunning);
            anim.SetBool("IsGrounded", isGrounded);
            anim.SetBool("IsStopping", isStopping);
        }

        // 耐力恢复：脱战后立即恢复（即使奔跑），战斗中按原逻辑
        if (!isInCombatCached) // 脱战状态
        {
            staminaRegenTimer += Time.deltaTime;
            RegenerateStamina();
        }
        else 
        {
            if(!isRunning && !isAttacking && !isLightAttacking)
            {
                staminaRegenTimer += Time.deltaTime;   // 累加延迟计时器
                RegenerateStamina();
            }    
        }

        // ========== 草地脚步声 ==========
        // 检测是否在地面上且正在移动（并且不是其他动作状态）
        bool isMovingOnGround = isGrounded && hasMoveInput && !isAttacking && !isLightAttacking && !isCasting && !isUltimateCasting && !isDodging && !isHit && !isJumping;
        if (isMovingOnGround)
        {
            // 根据奔跑或行走选择间隔
            float interval = isRunning ? runInterval : walkInterval;
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= interval)
            {
                footstepTimer = 0f;
                // 播放随机脚步声
                if (grassFootsteps != null && grassFootsteps.Length > 0)
                {
                    int idx = Random.Range(0, grassFootsteps.Length);
                    // 奔跑时提高音调，行走时正常
                    float originalPitch = footstepSource.pitch;
                    footstepSource.pitch = isRunning ? 1.3f : 1.0f;
                    footstepSource.PlayOneShot(grassFootsteps[idx], isRunning ? 0.7f : 0.5f);
                    footstepSource.pitch = originalPitch;
                }
            }
        }
        else
        {
            footstepTimer = 0f; // 停止移动或在空中时重置计时器
        }

        // 【新增】平滑过渡左手的 IK 权重，让松手和握手的动作如丝般顺滑
        if (leftHandIK != null)
        {
            leftHandIK.weight = Mathf.Lerp(leftHandIK.weight, targetLeftHandIKWeight, Time.deltaTime * 15f);
        }

        UpdateUIBarTexts();     
    }
    

    // ==========================================
    // 更新血条、耐力、怒气的数值文字
    // ==========================================
    private void UpdateUIBarTexts()
    {
        int currentH = Mathf.CeilToInt(currentHealth);
        int currentS = Mathf.CeilToInt(currentStamina);
        int currentR = Mathf.CeilToInt(currentRage);

        int maxH = Mathf.CeilToInt(maxHealth);
        int maxS = Mathf.CeilToInt(maxStamina);
        int maxR = Mathf.CeilToInt(maxRage);

        if (healthText != null && currentH != lastHealth)
        {
            healthText.text = $"{currentH} / {maxH}";
            lastHealth = currentH;
        }

        if (staminaText != null && currentS != lastStamina)
        {
            staminaText.text = $"{currentS} / {maxS}";
            lastStamina = currentS;
        }

        if (rageText != null && currentR != lastRage)
        {
            rageText.text = $"{currentR} / {maxR}";
            lastRage = currentR;
        }
    }

    // ======= 锁定目标逻辑 =======
    void HandleLockOnInput()
    {
        if (Input.GetMouseButtonDown(2)) // 鼠标中键
        {
            if (isLockedOn) ClearLockOn();
            else FindLockOnTarget();
        }

        // 目标失效或距离过远时自动解锁
        if (isLockedOn)
        {
            if (lockedTarget == null || !lockedTarget.gameObject.activeInHierarchy)
            {
                ClearLockOn();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, lockedTarget.position);
                if (dist > lockOnRadius * 1.5f) // 离开锁定范围的1.5倍后脱锁
                {
                    ClearLockOn();
                }
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
            Debug.Log("锁定目标: " + bestTarget.parent?.name);
            // 显示UI
            if (lockOnUI != null) lockOnUI.gameObject.SetActive(true);
        }
    }

    void ClearLockOn()
    {
        isLockedOn = false;
        lockedTarget = null;
        Debug.Log("解除锁定");
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
    
    //  先定义地面检测函数（放在 Update 外面，作为独立方法）
    private bool IsGrounded()
    {
        // 1. CharacterController 自带检测
        if (controller.isGrounded) return true;
    
        // 2. 辅助球形射线检测 (SphereCast)
        // 核心修复3：稍微再缩小一点半径，防止球体擦到身前楼梯的“垂直面”
        float radius = controller.radius * 0.75f; 
        
        // 发射起点设在脚底往上一点的位置
        Vector3 sphereCenter = transform.position + Vector3.up * (radius + 0.1f);
        
        // 往下探测 0.3f 距离
        if (Physics.SphereCast(sphereCenter, radius, Vector3.down, out RaycastHit hit, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // 核心修复4：排除撞到前方楼梯垂直面的情况！
            // 必须满足：法线朝上（是真正的地板或缓坡），或者 击中点确实在我们的脚底高度以下
            if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit + 5f || hit.point.y <= transform.position.y + 0.1f)
            {
                return true;
            }
        }
        
        return false;
    }

    void UpdateAnimation(bool hasMoveInput)
    {
        if (anim == null) return;
        if (isAttacking || isLightAttacking) return;

         // 跳跃期间锁定动画参数，避免空中切换移动/原地跳跃动画
        if (isJumping)
        {
            // 保持起跳时的动画速度，不更新 Speed/Direction
            anim.SetFloat("Speed", jumpStartSpeed);
            anim.SetFloat("Direction", 0f);
            return;
        }    

        if(isLockedOn)
        {
            // 锁定模式：PosX 是 Direction(横移)，PosY 是 Speed(前后或速度大小)
            float targetDir = moveInput.x; // A按键为-1，D按键为1
            
            // 计算当前的基础速度比例 (行走时约为1，奔跑时大于1)
            float speedMag = currentSpeed / runSpeed; 
            float targetSpeedAnim = 0f;
            
            if (moveInput.y != 0) 
            {
                // W/S 控制前后移动：向前为正，向后为负 (如果你的后退动画需要Y为负数)
                targetSpeedAnim = moveInput.y > 0 ? speedMag : -speedMag; 
            } 
            else if (moveInput.x != 0) 
            {
                // 纯A/D横移时，强行给PosY(Speed)赋值以满足你的要求 (Y>0.3触发慢横移，Y>1触发快横移)
                targetSpeedAnim = speedMag;
            }

            anim.SetFloat("Direction", Mathf.Lerp(anim.GetFloat("Direction"), targetDir, Time.deltaTime * 10f));
            anim.SetFloat("Speed", Mathf.Lerp(anim.GetFloat("Speed"), targetSpeedAnim, Time.deltaTime * 10f));
        }
        else
        {
            float animSpeed = 0f;
            float direction = 0f;
        
            if (hasMoveInput)
            {
                float speedPercent = currentSpeed / runSpeed;
                animSpeed = Mathf.Lerp(0.3f, 1f, speedPercent);
                direction = currentTurnAngle;
            }
            else
            {
                direction = currentTurnAngle;
            }
            anim.SetFloat("Speed", animSpeed);
            anim.SetFloat("Direction", direction);
        }
        
    }
    
    // 消耗耐力
    private bool ConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            staminaRegenTimer = 0f;  // 重置恢复延迟
            Debug.Log($"消耗耐力 {amount}，剩余 {currentStamina}");
            return true;
        }
        Debug.Log("耐力不足！");
        return false;  // 耐力不足
    }

    // 恢复耐力
    private void RegenerateStamina()
    {
        if (staminaBlockRemaining > 0f) return;
        if (staminaRegenTimer < staminaRegenDelay) return;

        float regenRate = staminaRegenRate;
        if (staminaRegenBuffTimer > 0f)
        {
            regenRate *= 2f;   // 加速翻倍
            staminaRegenBuffTimer -= Time.deltaTime;
        }

        if (currentStamina < maxStamina)
        {
            currentStamina += regenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }

    // ========== 攻击方法 ==========
    
    void StartRunningAttack()
    {
        // 检查耐力
        if (!ConsumeStamina(runningAttackStaminaCost))
        {
            Debug.Log("耐力不足，无法使用滑行攻击！");
            return;
        }
        //Debug.Log("奔跑攻击开始");
        isAttacking = false;
        isRunningAttack = false;
        comboPending = false;
        comboPendingTime = 0;
        isRunningAttack = true;
        isAttacking = true;
        anim.Play("RunningAttack", 0, 0f);
        lastSpeed = 0f;
        if (controller != null) controller.Move(Vector3.zero);
        Invoke("ForceEndRunningAttack", 1.5f);
    }
    
    void ForceEndRunningAttack()
    {
        if (isRunningAttack)
        {
            StartCoroutine(SmoothTransitionToIdle());
        }
    }
    
    System.Collections.IEnumerator SmoothTransitionToIdle()
    {
        yield return new WaitForSeconds(0.05f);
        anim.CrossFade("IdleSelector", 0.15f, 0, 0f);
        isAttacking = false;
        isRunningAttack = false;
        IdleSelector idleSelector = GetComponent<IdleSelector>();
        if (idleSelector != null) idleSelector.ResetIdleTimer();
    }
    
    void StartAttack()
    {
        // ===== 耐力消耗处理 =====
        // 确定本次攻击的段数（完美闪避奖励时为4，普通重击为1）
        int attackComboIndex = nextHeavyAttackIsFourth ? 4 : 1;
        float staminaCost = heavyAttackStaminaCost[attackComboIndex - 1];
        //Debug.Log($"StartAttack: 段数={attackComboIndex}, 消耗={staminaCost}, 当前耐力={currentStamina}");
        // 检查并消耗耐力
         if (!ConsumeStamina(staminaCost))
        {
            Debug.Log("耐力不足，无法重攻击");
            return;
        }

        // 如果标记了下一次重击为第四段，则直接播放 Attack4
        if (nextHeavyAttackIsFourth)
        {
            nextHeavyAttackIsFourth = false;  // 使用后清除标记
        
            // 设置攻击状态
            isLightAttacking = false;
            lightComboPending = false;
            lightComboPendingTime = 0;
            isRunningAttack = false;
            comboPending = false;
            comboPendingTime = 0;
            isAttacking = true;
            currentAttackCombo = 4;   // 当前段数设为4
        
            // 重置动画触发器
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Combo");
            anim.ResetTrigger("LightAttack");
            anim.ResetTrigger("LightCombo");
        
            // 设置 AttackLayer 权重
            anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetLayerWeight(attackLayerIndex, 1f);
        
            // 直接播放 Attack4 动画
            int attack4Hash = Animator.StringToHash("Attack4");
            anim.Play(attack4Hash, attackLayerIndex, 0f);
        
            // 确保动画正确播放（可选）
            var attackStateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
            if (attackStateInfo.fullPathHash != attack4Hash)
            {
                anim.SetLayerWeight(attackLayerIndex, 0f);
                anim.Update(0f);
                anim.SetLayerWeight(attackLayerIndex, 1f);
                anim.Play(attack4Hash, attackLayerIndex, 0f);
            }
        
            lastSpeed = 0f;
            if (controller != null) controller.Move(Vector3.zero);
            Debug.Log("完美闪避奖励：直接打出第四段重攻击");
            return;
        }

        currentAttackCombo = 1;     // 每次开始新的一套重攻击时，重置段数
        isLightAttacking = false;
        lightComboPending = false;
        lightComboPendingTime = 0;
        isRunningAttack = false;
        comboPending = false;
        comboPendingTime = 0;
        isAttacking = true;
        
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Combo");
        anim.ResetTrigger("LightAttack");
        anim.ResetTrigger("LightCombo");
        
        anim.SetLayerWeight(attackLayerIndex, 0f);
        anim.SetLayerWeight(attackLayerIndex, 1f);
        
        int attack1Hash = Animator.StringToHash("Attack1");
        anim.Play(attack1Hash, attackLayerIndex, 0f);
        
        var stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (stateInfo.fullPathHash != attack1Hash)
        {
            anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.Update(0f);
            anim.SetLayerWeight(attackLayerIndex, 1f);
            anim.Play(attack1Hash, attackLayerIndex, 0f);
        }
        
        lastSpeed = 0f;
        if (controller != null) controller.Move(Vector3.zero);
        //Debug.Log("第一段重攻击开始");
    }
    
    void StartLightAttack()
    {
        if (attackLayerIndex < 0) return;
        if (isAttacking || isRunningAttack) { Debug.Log("有其他攻击在进行，无法开始轻攻击"); return; }
        
        isLightAttacking = true;
        lightComboPending = false;
        lightComboPendingTime = 0;
        comboPending = false;
        comboPendingTime = 0;
        
        anim.SetFloat("IdleIndex", 0f);
        anim.ResetTrigger("LightAttack");
        anim.ResetTrigger("LightCombo");
        anim.ResetTrigger("Attack");
        anim.ResetTrigger("Combo");
        
        anim.SetLayerWeight(attackLayerIndex, 0f);
        anim.SetLayerWeight(attackLayerIndex, 1f);
        anim.Play("LightAttack1", attackLayerIndex, 0f);
        
        lastSpeed = 0f;
        if (controller != null) controller.Move(Vector3.zero);
        Debug.Log("轻攻击第一段开始");
    }
    
    public void OnAttackFinished()
    {
        // 【核心修复】：防御幽灵事件！如果玩家已经被打断了（非攻击状态），直接无视后台动画机发来的结束指令！
        if (!isAttacking && !isLightAttacking && !isRunningAttack) return;

        if (isProcessingAttackEnd) return;
        isProcessingAttackEnd = true;
        
        if (isRunningAttack)
        {
            Debug.Log("奔跑攻击自然结束");
            StartCoroutine(SmoothTransitionToIdle());
            isProcessingAttackEnd = false;
            return;
        }
        
        if (anim == null) { isProcessingAttackEnd = false; return; }
        
        var stateInfo = anim.GetCurrentAnimatorStateInfo(attackLayerIndex);
        
        if (stateInfo.IsName("LightAttack3"))
        {
            isLightAttacking = false;
            lightComboPending = false;
            lightComboPendingTime = 0;
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            IdleSelector idleSelector = GetComponent<IdleSelector>();
            if (idleSelector != null) idleSelector.ResetIdleTimer();
            Debug.Log("轻攻击序列完全结束");
            isProcessingAttackEnd = false;
            return;
        }
        else if (stateInfo.IsName("LightAttack1") || stateInfo.IsName("LightAttack2"))
        {
            if (lightComboPendingTime <= 0 && !lightComboPending)
            {
                isLightAttacking = false;
                if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
                anim.SetFloat("IdleIndex", 0f);
                Debug.Log("轻攻击结束（无连击）");
            }
            isProcessingAttackEnd = false;
            return;
        }
        
        if (stateInfo.IsName("Attack5"))
        {
            isAttacking = false;
            isLightAttacking = false;
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            IdleSelector idleSelector = GetComponent<IdleSelector>();
            if (idleSelector != null) idleSelector.ResetIdleTimer();
            Debug.Log("重攻击序列完全结束");
        }
        else if (comboPending)
        {
            int nextCombo = currentAttackCombo + 1;   // 下一段段数
            float staminaCost = (nextCombo <= heavyAttackStaminaCost.Length) 
                            ? heavyAttackStaminaCost[nextCombo - 1] 
                            : heavyAttackStaminaCost[heavyAttackStaminaCost.Length - 1]; // 超出则取最后一段消耗
            //Debug.Log($"连击前: 当前段数={currentAttackCombo}, 下一段={nextCombo}, 消耗={staminaCost}, 当前耐力={currentStamina}");
            if (!ConsumeStamina(staminaCost))
            {
                // 耐力不足，中断连击
                comboPending = false;
                comboPendingTime = 0;
                isAttacking = false;
                if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
                anim.SetFloat("IdleIndex", 0f);
                IdleSelector idleSelector = GetComponent<IdleSelector>();
                if (idleSelector != null) idleSelector.ResetIdleTimer();
                Debug.Log("耐力不足，连击中断");
            }
            else
            {
                anim.SetTrigger("Combo");
                comboPending = false;
                comboPendingTime = 0;
                currentAttackCombo++;   // 进入下一段攻击
            }  
        }
        else
        {
            isAttacking = false;
            if (attackLayerIndex >= 0) anim.SetLayerWeight(attackLayerIndex, 0f);
            anim.SetFloat("IdleIndex", 0f);
            IdleSelector idleSelector = GetComponent<IdleSelector>();
            if (idleSelector != null) idleSelector.ResetIdleTimer();
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

            // 3. 应用动态计算出的中心点与半径
            Vector3 lightAttackCenter = transform.position + transform.forward * currentOffset + Vector3.up * 1f;
        
            Collider[] hits = Physics.OverlapSphere(lightAttackCenter, currentRadius, enemyLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
                dirToEnemy.y = 0;
                Vector3 playerForward = transform.forward;
                playerForward.y = 0;
            
                // 依然保持严格的 60 度前方视角限制，防修脚防后背
                if (Vector3.Angle(playerForward, dirToEnemy) <= 60f)
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
            
                // 【怒气系统】：攻击命中积攒怒气
                if (currentRage < maxRage)
                {
                    currentRage += (damage * ragePerDamageMultiplier) * rageGainMultiplier;
                    currentRage = Mathf.Clamp(currentRage, 0f, maxRage);
                }

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
            Debug.LogWarning("heavyAttackEffects 数组为空！");
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
            Debug.LogWarning($"无法播放特效 - 索引:{index}, 数组长度:{heavyAttackEffects.Length}");
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
            // 生成指定的特效
            GameObject effect = Instantiate(vfxPrefab, hitPoint, Quaternion.identity);
            
            // 核心修复：强行把它变成怪物的子物体！
            // 这样怪物被击退、击飞时，伤口处的火花会死死粘在它身上跟着一起飞！
            if (enemyTransform != null)
            {
                effect.transform.SetParent(enemyTransform, true);
            }
            
            effect.SetActive(false);
            effect.SetActive(true);
            StartCoroutine(DelayedPlay(effect));

            // 0.5秒后销毁，保持 Hierarchy 干净清爽
            Destroy(effect, 0.5f);
        }
    }



    // 通用特效生成方法
    private void SpawnEffect(GameObject effectPrefab)
    {
        if (effectPrefab == null) return;
        if (weaponPoint == null) return;
    
        Vector3 defaultSpawnPos = weaponPoint.position;
        // 玩家胸口位置（作为射线起点）
        Vector3 playerChest = transform.position + Vector3.up * 1.2f;

        Vector3 dirToWeapon = defaultSpawnPos - playerChest;
        float maxDist = dirToWeapon.magnitude;
        Vector3 finalSpawnPos = defaultSpawnPos;

        // 动态限距核心逻辑：防穿模到敌人身后
        // 用一个粗一点的 SphereCast (半径0.3f) 扫向武器点
        if (Physics.SphereCast(playerChest, 0.3f, dirToWeapon.normalized, out RaycastHit hit, maxDist, enemyLayer))
        {
            // 如果撞到了敌人，强制把特效生成点拉回到击中点稍微靠玩家一点点的位置（减去0.1f防完全贴脸）
            finalSpawnPos = playerChest + dirToWeapon.normalized * Mathf.Max(0, hit.distance - 0.1f);
        }

        // 根据攻击段数获取不同的旋转
        Quaternion spawnRot = GetAttackRotation();
        GameObject effect = Instantiate(effectPrefab, finalSpawnPos, spawnRot);
    
        // 🔧 先禁用再启用，强制重新初始化
        effect.SetActive(false);
        effect.SetActive(true);
    
        // 延迟一帧再播放
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
        
        // 直接使用玩家当前的朝向 (transform.rotation)！
        // 因为预制体内部已经调好了斜挑的角度，所以生出来绝对完美匹配！
        GameObject effect = Instantiate(vfxPrefab, spawnPos, transform.rotation);
        
        effect.SetActive(false);
        effect.SetActive(true);
        StartCoroutine(DelayedPlay(effect));

        // 自动销毁防内存泄漏
        Destroy(effect, 1.5f);
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
            animator.Rebind();
            animator.Update(0f);
            
            // 【核心修复】：不管美术把这套动画起名叫 "Play" 还是 "Take 001"，直接抓取默认状态从头播放！
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(stateInfo.fullPathHash, -1, 0f);
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
    
        // 统一销毁，绝不留内存垃圾
        Destroy(effect, maxDuration);
    
    }
    //====================================================


    //========技能释放==========
    void StartCast()
    {
        Debug.Log("施法开始");
        isCasting = true;
        isCastingInvincible = true;  // 开启霸体
        castStartTime = Time.time;
    
        // 停止移动
        moveInput = Vector2.zero;
        currentSpeed = 0f;
        lastSpeed = 0f;
    
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
        isCasting = false;
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

        Debug.Log("施法彻底结束");
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
            Debug.LogWarning($"PlayAttackSwing 无法识别当前攻击动画: {stateInfo.fullPathHash}");
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
            Debug.Log("完美闪避！触发慢动作和暴击");
            StartCoroutine(PerfectDodgeReward());
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
        Debug.Log($"玩家受到 {finalDamage} 伤害，剩余生命 {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // 强制结束当前攻击
        if (isAttacking || isLightAttacking || isRunningAttack || isUltimateCasting || isCasting)
        {
            isAttacking = false;
            isLightAttacking = false;
            isRunningAttack = false;
            isCasting = false;
            isUltimateCasting = false; // 👈【新增】被挨打打断时解锁大招
            
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
        
        isHit = true;
        
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

            // 生成特效，保留预制体自身调整好的角度（比如 Z=90）
            GameObject waveVFX = Instantiate(skillEffect, vfxPos, transform.rotation * skillEffect.transform.rotation);

            // 直接 GetComponent 获取特效脚本
            SkillWave waveScript = waveVFX.GetComponent<SkillWave>();

            // 把它改为打 10 次伤害（3秒内高频切割）
            int totalTicks = 10;
            // 判断一下，如果有专属技能火花就用专属的，没有才用普通白字火花兜底！
            GameObject vfxToPass = skillHitEffect != null ? skillHitEffect : hitEffect;    
            //把硬编码替换为面板变量 skillWavePushForce 和 skillWaveUpForce
            waveScript.Initialize(finalTotalSkillDamage, totalTicks, skillWavePushForce, skillWaveUpForce, enemyLayer, transform.forward, vfxToPass);

            // 使用面板变量控制销毁时间 
            Destroy(waveVFX, skillWaveLifetime);
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
        isUltimateCasting = true;
        isCastingInvincible = true; // 复用霸体
        qteSuccess = false;         // 重置QTE状态
        isWaitingForQTE = false;
        
        moveInput = Vector2.zero;
        currentSpeed = 0f;

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
            GameObject slowMoAudioObj = new GameObject("UltSlowMoSFX");
            slowMoAudioObj.transform.position = transform.position;
            slowMoAudioObj.transform.parent = transform; // 跟着玩家移动

            AudioSource tempSource = slowMoAudioObj.AddComponent<AudioSource>();
            tempSource.clip = ultSlowMotionSFX;
            tempSource.volume = 1.0f;
            tempSource.spatialBlend = 0.5f; // 保持 3D 音效
            tempSource.pitch = 1.0f;        // 绝对锁定正常音调
            
            // 彻底关闭多普勒效应，防止角色高速升空时导致声音物理变调！
            tempSource.dopplerLevel = 0f;   
            
            tempSource.Play();

            // 音效播放完毕后自动销毁
            Destroy(slowMoAudioObj, ultSlowMotionSFX.length);
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
            
            Debug.Log("QTE 失败：挥空或伤害大减！");
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
            GameObject qteAudioObj = new GameObject("QTESuccessSFX");
            qteAudioObj.transform.position = transform.position;
            qteAudioObj.transform.parent = transform;

            AudioSource tempSource = qteAudioObj.AddComponent<AudioSource>();
            tempSource.clip = clipToPlay;
            tempSource.volume = 1.2f;
            tempSource.spatialBlend = 0.5f;
            tempSource.dopplerLevel = 0f; // 彻底关闭多普勒效应
            tempSource.Play();

            Destroy(qteAudioObj, clipToPlay.length);
        }
    }

    // 动画事件：前四段上挑伤害判定
    // （在 Animator 里打 4 个事件点调用这个方法）
    public void Event_UltUpwardSlashHit(int index)
    {
        // 核心防抖锁】：如果距离上次触发还不到 0.1 秒，说明是 Unity 引擎在抽风双重调用，直接无视它
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime; // 记录本次触发时间

        Debug.Log($"大招第 {index + 1} 段斩击判定触发");

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
                    PlayAttackHit(); 
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
                    PlayAttackHit(); 
                }
            }
        }
    }

    // 动画事件 2：大剑砸在地上的瞬间结算伤害！
    public void Event_UltimateHit()
    {
        if (Time.unscaledTime - lastEventTime < 0.1f) return;
        lastEventTime = Time.unscaledTime;

        Debug.Log("大招伤害判定触发");

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

        Vector3 slamCenter = transform.position + Vector3.up * 0.5f; 
        float slamRadius = 7.0f; 

        Collider[] hitColliders = Physics.OverlapSphere(slamCenter, slamRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        HashSet<BasicEnemyTest> damagedEnemies = new HashSet<BasicEnemyTest>();

        foreach (var hit in hitColliders)
        {
            BasicEnemyTest enemy = hit.GetComponent<BasicEnemyTest>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy); 
                
                // 【先抹平高度差，再计算方向】保证 100% 水平击退力度！
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
        isUltimateCasting = false;
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
        
        isHit = true;
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
        respawnPosition = spawnPos;
        respawnRotation = spawnRot;

        // 2. 状态全满
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentRage = 0f;  // 【新增】坐篝火/复活后，清空怒气
        if (rageSlider != null) rageSlider.value = currentRage;

        // 3. 更新UI
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;

        // 4. 重置异常状态和攻击状态
        isHit = false;
        isDodging = false;
        isAttacking = false;
        isLightAttacking = false;
        isRunningAttack = false;
        isCasting = false;

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
        SaveGame();

        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage("在赐福点休息，生命与状态已恢复");
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
        isAttacking = false;
        isLightAttacking = false;
        isRunningAttack = false;
        isCasting = false;
        isDodging = false;
        isJumping = false;
        isStopping = false;
        isHit = false;
        isUltimateCasting = false;
        comboPending = false;
        lightComboPending = false;
        targetLeftHandIKWeight = 1f;

        moveInput = Vector2.zero;
        currentSpeed = 0f;

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
        ResetCameraBehindPlayer();

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
        isAttacking = false;
        isLightAttacking = false;
        isRunningAttack = false;
        isCasting = false;
        isDodging = false;
        isJumping = false;   
        isStopping = false;  
        isHit = false;       
        isUltimateCasting = false; // 坐篝火时解锁大招
        comboPending = false;
        lightComboPending = false;
        targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持
        
        moveInput = Vector2.zero;
        currentSpeed = 0f;

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
        ResetCameraBehindPlayer();

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
        
        isHit = false;
        
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
        isHit = false;
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
        isDead = false;
        isHit = false;
        isDodging = false;
        isAttacking = false;
        isLightAttacking = false;
        isRunningAttack = false;
        isCasting = false;
        isUltimateCasting = false; // 👈【新增】复活时解锁大招
        isRunning = false;
        currentSpeed = 0f;
        targetLeftHandIKWeight = 1f; // 兜底：挨打/攻击结束/复活时，强制恢复双手握持

        if (healthSlider != null) healthSlider.value = currentHealth;
        if (staminaSlider != null) staminaSlider.value = currentStamina;
    
        // ======= 把人物传送到记录的复活点 =======
        if (controller != null) controller.enabled = false; 
        
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;

        // 复活时同样瞬间重置相机到背后
        ResetCameraBehindPlayer();
        
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
        
        Debug.Log("玩家死亡后在最后一个赐福点复活！并且所有敌人已刷新！");
    }

    //==========闪避==================
    private void TryDodge()
    {
        // 检查耐力
        if (!ConsumeStamina(dodgeStaminaCost))
        {
            Debug.Log("耐力不足，无法闪避");
            return;
        }

        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        isDodging = true;
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

        isDodging = false;
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
           GameObject dodgeAudioObj = new GameObject("PerfectDodgeSFX");
            dodgeAudioObj.transform.position = transform.position;
            dodgeAudioObj.transform.parent = transform;         

            AudioSource tempSource = dodgeAudioObj.AddComponent<AudioSource>();
            tempSource.clip = perfectDodgeStartSFX;
            tempSource.volume = 0.8f;
            tempSource.spatialBlend = 0.5f; 
            
            // 关闭完美闪避音效的多普勒效应！
            tempSource.dopplerLevel = 0f;   
            
            tempSource.Play();

            Destroy(dodgeAudioObj, perfectDodgeStartSFX.length);
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
        Debug.Log("Die() 被调用！当前血量：" + currentHealth);
        if (isDead) return;
        isDead = true;
        StopAllCoroutines();                // 停止所有协程
        Time.timeScale = 1f;                // 确保时间缩放恢复
        Debug.Log("玩家死亡");
    
        // 播放死亡音效
        if (deathSFX != null && audioSource != null)
            audioSource.PlayOneShot(deathSFX, 1.0f);


        // 停止所有动作
        isAttacking = false;
        isLightAttacking = false;
        isRunningAttack = false;
        isCasting = false;
        isDodging = false;
        isRunning = false;
        currentSpeed = 0f;
    
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



    private void LoadGrassFootsteps()
    {
        // 方法1：加载文件夹下所有音频（推荐）
        grassFootsteps = Resources.LoadAll<AudioClip>("Audio/grass walk");
        if (grassFootsteps.Length == 0)
        {
            Debug.LogWarning("未找到草地脚步声！请将音频放在 Resources/Audio/grass walk 文件夹");
        }
        else
        {
            Debug.Log($"成功加载 {grassFootsteps.Length} 个草地脚步声");
        }
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


    // 根据当前的基础属性，重新计算角色的真实面板数据
    public void RecalculateAttributes()
    {
        // 1. 生命力：每点提供 20 点血量（加上基础100）
        maxHealth = 100f + (statVigor * 20f);
        
        // 2. 持久力：每点提供 10 点耐力（加上基础100）
        maxStamina = 100f + (statEndurance * 10f);
        
        // 3. 最终攻击力加成 = 武器当前攻击力 + 力量属性点加成！
        float currentWeaponAttack = weaponBaseAttack + (weaponLevel * upgradeAttackBonus);
        attackPowerBonus = currentWeaponAttack + (statStrength * 3f);
        
        // 4. 坚韧：每点增加 2 点防御力
        defensePower = statResistance * 2f;
        
        // 5. 精神：每点增加 2% 的怒气积攒速度
        rageGainMultiplier = 1f + (statSpirit * 0.02f);

        // 更新UI最大值
        if (healthSlider != null) healthSlider.maxValue = maxHealth;
        if (staminaSlider != null) staminaSlider.maxValue = maxStamina;
    }

    // ==========================================
    // 供外部脚本（如铁匠 NPC）调用的武器升级接口
    // ==========================================
    public bool UpgradeWeapon()
    {
        if (weaponLevel < maxWeaponLevel)
        {
            weaponLevel++;
            
            // 武器升级后，强制重新计算面板攻击力
            RecalculateAttributes(); 
            
            // 升级后自动存档
            SaveGame(); 

            // 弹出屏幕播报
            if (ActionLogManager.Instance != null)
            {
                ActionLogManager.Instance.ShowMessage($"武器强化成功！太刀 +{weaponLevel}");
            }
            
            Debug.Log($"武器升级成功！当前等级：+{weaponLevel}");
            return true; // 返回 true 告诉铁匠组员：升级成功，可以扣除玩家的金币/强化石了！
        }
        else
        {
            Debug.LogWarning("武器已达最高等级，无法继续强化！");
            return false; // 返回 false 告诉铁匠：满级了，别扣玩家的钱！
        }
    }

    //===================升级与获取经验的方法================
    // 击杀敌人获取经验
    public void AddXP(int amount)
    {
        currentXP += amount;
        // 把 Debug.Log 改为 UI 播报：
        if (ActionLogManager.Instance != null)
        {
            ActionLogManager.Instance.ShowMessage($"击败敌人，获得 {amount} 卢恩");
        }
    }

    // 击杀敌人获取金币
    public void AddGold(int amount)
    {
        currentGold += amount;
        if (ActionLogManager.Instance != null)
        {
            ActionLogManager.Instance.ShowMessage($"拾取掉落，获得 {amount} 金币");
        }
    }

    // 领取任务奖励获取经验
    public void RewardXP(int amount)
    {
        currentXP += amount;
        // 把 Debug.Log 改为 UI 播报：
        if (ActionLogManager.Instance != null)
        {
            ActionLogManager.Instance.ShowMessage($"领取奖励，获得 {amount} 卢恩");
        }
    }

    // 领取任务奖励获取金币
    public void RewardGold(int amount)
    {
        currentGold += amount;
        if (ActionLogManager.Instance != null)
        {
            ActionLogManager.Instance.ShowMessage($"领取奖励，获得 {amount} 金币");
        }
    }

    // 计算升到下一级需要多少经验（典型的魂系递增公式）
    public int GetXPRequirementForNextLevel()
    {
        // 公式可自定义：基础 500 + 等级的平方 * 50
        return 500 + (currentLevel * currentLevel * 50);
    }

    // 操作 UI 进行加点
    public bool TryLevelUp(string statName)
    {
        int requiredXP = GetXPRequirementForNextLevel();
        if (currentXP >= requiredXP)
        {
            currentXP -= requiredXP;
            currentLevel++;

            switch (statName)
            {
                case "Vigor": statVigor++; break;
                case "Endurance": statEndurance++; break;
                case "Strength": statStrength++; break;
                case "Resistance": statResistance++; break;
                case "Spirit": statSpirit++; break;
            }

            RecalculateAttributes(); // 加点后重新计算面板
            currentHealth = maxHealth; // 升级送回血
            if (healthSlider != null) healthSlider.value = currentHealth;

            SaveGame(); // 升级后自动保存
           // 替换原来的 Debug.Log：
            if (ActionLogManager.Instance != null)
                ActionLogManager.Instance.ShowMessage($"升级成功！【{statName}】属性已提升");
            return true;
        }
        else
        {
            Debug.Log("经验不足，无法升级！");
            return false;
        }
    }



    // ================== 硬盘读写系统 ==================
    public void SaveGame()
    {
        // 记录有没有存过档的标记
        PlayerPrefs.SetInt("HasSavedGame", 1);

        // 存坐标
        PlayerPrefs.SetFloat("PlayerPosX", respawnPosition.x);
        PlayerPrefs.SetFloat("PlayerPosY", respawnPosition.y);
        PlayerPrefs.SetFloat("PlayerPosZ", respawnPosition.z);

        // 存旋转 (欧拉角)
        PlayerPrefs.SetFloat("PlayerRotY", respawnRotation.eulerAngles.y);

        // 【新增存储】
        PlayerPrefs.SetInt("PlayerLevel", currentLevel);
        PlayerPrefs.SetInt("PlayerXP", currentXP);
        PlayerPrefs.SetInt("PlayerGold", currentGold);
        PlayerPrefs.SetInt("Vigor", statVigor);
        PlayerPrefs.SetInt("Endurance", statEndurance);
        PlayerPrefs.SetInt("Strength", statStrength);
        PlayerPrefs.SetInt("Resistance", statResistance);
        PlayerPrefs.SetInt("Spirit", statSpirit);
        PlayerPrefs.SetInt("WeaponLevel", weaponLevel); // 存储武器等级

        // 立刻写入硬盘
        PlayerPrefs.Save();
        if (ActionLogManager.Instance != null) ActionLogManager.Instance.ShowMessage("游戏进度已保存");
    }

    public void LoadGame()
    {
        // 检查硬盘里有没有存过档
        if (PlayerPrefs.GetInt("HasSavedGame", 0) == 1)
        {
            // 提取数据
            float x = PlayerPrefs.GetFloat("PlayerPosX");
            float y = PlayerPrefs.GetFloat("PlayerPosY");
            float z = PlayerPrefs.GetFloat("PlayerPosZ");
            float rotY = PlayerPrefs.GetFloat("PlayerRotY");

            // 【新增读取】
            currentLevel = PlayerPrefs.GetInt("PlayerLevel", 1);
            currentXP = PlayerPrefs.GetInt("PlayerXP", 0);
            currentGold = PlayerPrefs.GetInt("PlayerGold", 0);
            statVigor = PlayerPrefs.GetInt("Vigor", 10);
            statEndurance = PlayerPrefs.GetInt("Endurance", 10);
            statStrength = PlayerPrefs.GetInt("Strength", 10);
            statResistance = PlayerPrefs.GetInt("Resistance", 10);
            statSpirit = PlayerPrefs.GetInt("Spirit", 10);
            weaponLevel = PlayerPrefs.GetInt("WeaponLevel", 0); // 读取武器等级

            respawnPosition = new Vector3(x, y, z);
            respawnRotation = Quaternion.Euler(0, rotY, 0);

            // 强行把玩家传送到最后一次存档的赐福点
            if (controller != null) controller.enabled = false;
            transform.position = respawnPosition;
            transform.rotation = respawnRotation;
            if (controller != null) controller.enabled = true;

            // 不管有没有旧存档，游戏启动时都必须根据属性值计算一遍攻击力和最大血量！
            RecalculateAttributes();

            Debug.Log("读取存档成功！已传送到最后一次休息的赐福点。");
        }
    }

    // ==========================================
    // 重置相机视角到玩家正后方（防止瞬移后视角错乱与穿模）
    // ==========================================
    public void ResetCameraBehindPlayer()
    {
        // 1. 强制让相机的水平角度 (Yaw) 与玩家当前的背部朝向完全一致
        currentYaw = transform.eulerAngles.y;
        
        // 2. 给相机一个稍微向下的舒适俯视角
        currentPitch = 15f; 

        // 3. 瞬间把相机移动过去，跳过 Update 里的缓慢 Lerp 平移，防止划过半个地图
        if (cameraTransform != null)
        {
            Vector3 desiredPosition = transform.position + Quaternion.Euler(currentPitch, currentYaw, 0) * new Vector3(0, cameraHeight, -cameraDistance);
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            
            cameraTransform.position = desiredPosition;
            cameraTransform.LookAt(lookTarget);
        }
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