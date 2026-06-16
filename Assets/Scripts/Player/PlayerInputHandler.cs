using UnityEngine;

// ==========================================
// 🎮 工业级架构：硬件输入分离处理中心
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
    public bool LockOnInput { get; private set; }
    
    // 👇 新增的武器专属战技与切换键
    public bool WeaponSkillInput { get; private set; }
    public bool SwitchWeaponInput { get; private set; }

    private EldenRingMovement playerState;

    private void Start()
    {
        playerState = GetComponent<EldenRingMovement>();
    }

    private void Update()
    {
        // 核心拦截：死亡、休息、打开UI时锁死输入
        if (playerState != null && (playerState.isDead || playerState.isResting || playerState.isUIOpen))
        {
            ClearAllInputs();
            return;
        }

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
        
        WeaponSkillInput = Input.GetKeyDown(KeyCode.E);      // E 键放专属战技
        SwitchWeaponInput = Input.GetKeyDown(KeyCode.Tab);   // Tab 键切武器
    }

    private void ClearAllInputs()
    {
        MoveInput = Vector2.zero;
        RunInput = false;
        JumpInput = false;
        DodgeInput = false;
        BlockInput = false;
        HeavyAttackInput = false;
        LightAttackInput = false;
        LockOnInput = false;
        WeaponSkillInput = false;
        SwitchWeaponInput = false;
    }
}