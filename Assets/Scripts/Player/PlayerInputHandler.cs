using UnityEngine;

// ==========================================
// 工业级架构：硬件输入分离处理中心
// ==========================================
public class PlayerInputHandler : MonoBehaviour
{
    [Header("实时输入数据 (只读)")]
    public Vector2 MoveInput { get; private set; }
    public bool RunInput { get; private set; }
    public bool JumpInput { get; private set; }
    public bool DodgeInput { get; private set; }
    public bool BlockInput { get; private set; }
    public bool HeavyAttackInput { get; private set; }
    public bool LightAttackInput { get; private set; }
    public bool SkillInput { get; private set; }
    public bool UltimateInput { get; private set; }
    public bool LockOnInput { get; private set; }

    // 临时获取主脚本的状态，用于在死亡或打开UI时强行锁死输入
    private EldenRingMovement playerState;

    private void Start()
    {
        playerState = GetComponent<EldenRingMovement>();
    }

    private void Update()
    {
        // 核心拦截：如果玩家死了、在篝火休息、或者打开了UI，直接清空所有按键信号！
        // 这样主脚本里就再也不用写满天飞的 "!isUIOpen" 了！
        if (playerState != null && (playerState.isDead || playerState.isResting || playerState.isUIOpen))
        {
            ClearAllInputs();
            return;
        }

        // 读取轴向与硬件按键 (未来如果要接入手柄，只需要修改这里即可)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(h, v).normalized;

        RunInput = Input.GetKey(KeyCode.LeftShift);
        JumpInput = Input.GetButtonDown("Jump");
        DodgeInput = Input.GetKeyDown(KeyCode.F);
        BlockInput = Input.GetKey(KeyCode.LeftControl);
        
        HeavyAttackInput = Input.GetMouseButtonDown(0);
        LightAttackInput = Input.GetMouseButtonDown(1);
        LockOnInput = Input.GetMouseButtonDown(2);
        
        SkillInput = Input.GetKeyDown(KeyCode.Alpha1);
        UltimateInput = Input.GetKeyDown(KeyCode.Alpha2);
    }

    // 强行清空所有输入
    private void ClearAllInputs()
    {
        MoveInput = Vector2.zero;
        RunInput = false;
        JumpInput = false;
        DodgeInput = false;
        BlockInput = false;
        HeavyAttackInput = false;
        LightAttackInput = false;
        SkillInput = false;
        UltimateInput = false;
        LockOnInput = false;
    }
}