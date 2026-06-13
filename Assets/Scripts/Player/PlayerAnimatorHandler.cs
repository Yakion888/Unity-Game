using UnityEngine;

// ==========================================
// 工业级架构：动画表现与哈希缓存处理中心
// ==========================================
public class PlayerAnimatorHandler : MonoBehaviour
{
    public Animator anim { get; private set; }
    public int attackLayerIndex { get; private set; }

    // 将所有的动画字符串转换为整型哈希值并缓存
    // 永远不要在 Update 里直接用 "String" 去控制动画！
    public readonly int speedHash = Animator.StringToHash("Speed");
    public readonly int directionHash = Animator.StringToHash("Direction");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");
    public readonly int isRunningHash = Animator.StringToHash("IsRunning");
    public readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    public readonly int isStoppingHash = Animator.StringToHash("IsStopping");
    public readonly int isBlockingHash = Animator.StringToHash("IsBlocking");
    public readonly int idleIndexHash = Animator.StringToHash("IdleIndex");

    // 初始化获取组件
    public void Initialize()
    {
        anim = GetComponent<Animator>();
        if (anim != null)
        {
            attackLayerIndex = anim.GetLayerIndex("AttackLayer");
            if (attackLayerIndex >= 0)
            {
                anim.SetLayerWeight(attackLayerIndex, 0f);
            }
        }
    }

    // ==========================================
    // 核心动画同步：每帧调用的物理状态更新（极致性能版）
    // ==========================================
    public void SyncLocomotionStates(bool hasMoveInput, bool isRunning, bool isGrounded, bool isStopping, bool isAttacking, bool isLightAttacking, bool isUltimateCasting)
    {
        bool isMoving = hasMoveInput && !isAttacking && !isLightAttacking && !isUltimateCasting;
        
        // 直接使用缓存的 Int 驱动状态机，速度极快，0 GC分配！
        anim.SetBool(isMovingHash, isMoving);
        anim.SetBool(isRunningHash, isRunning);
        anim.SetBool(isGroundedHash, isGrounded);
        anim.SetBool(isStoppingHash, isStopping);
    }

    // 更新具体的移动数值
    public void SyncMovementValues(float targetSpeed, float targetDirection, float damping)
    {
        // 读取当前的数值，进行平滑插值过渡，再赋值进去
        float currentSpeed = anim.GetFloat(speedHash);
        float currentDir = anim.GetFloat(directionHash);

        anim.SetFloat(speedHash, Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * damping));
        anim.SetFloat(directionHash, Mathf.Lerp(currentDir, targetDirection, Time.deltaTime * damping));
    }

    // 直接赋值（跳跃时用）
    public void SetSpeedDirectly(float speed)
    {
        anim.SetFloat(speedHash, speed);
        anim.SetFloat(directionHash, 0f);
    }
}