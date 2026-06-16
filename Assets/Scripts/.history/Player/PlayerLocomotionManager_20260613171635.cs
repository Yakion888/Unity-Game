using UnityEngine;

// ==========================================
// 工业级架构：移动、重力与物理表现引擎
// ==========================================
public class PlayerLocomotionManager : MonoBehaviour
{
    [Header("移动速度")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 15f;

    [Header("跳跃与重力设置")]
    public float jumpHeight = 1.5f;
    public float jumpStaminaCost = 15f;
    public float gravityMultiplier = 2.5f;
    public float terminalVelocity = -30f;
    
    [Header("旋转设置")]
    public float rotationSpeed = 540f;
    public float idleRotationSpeed = 360f;

    [Header("急停设置")]
    public float stopAnimationDuration = 0.5f;

    [Header("草地脚步声")]
    public AudioClip[] grassFootsteps;

    // --- 内部状态与物理缓存 ---
    public bool isJumping { get; private set; }
    public float jumpStartSpeed { get; private set; }
    public bool isGroundedCached { get; private set; }
    public bool isStopping { get; private set; }
    public bool isRunning { get; private set; }
    public Vector3 targetMoveDirection { get; private set; }

    public float currentSpeed { get; private set; }
    private float verticalVelocity;
    private float gravity = -9.81f;
    private float airTimer = 0f;
    private Quaternion targetRotation;
    public float currentTurnAngle { get; private set; }
    
    private float stopTimer;
    private bool wasRunning;
    private bool wasMoving;
    
    private AudioSource footstepSource;
    private float footstepTimer = 0f;
    private float walkInterval = 0.5f;
    private float runInterval = 0.3f;

    // --- 架构解耦引用 ---
    private EldenRingMovement player;
    private PlayerInputHandler input;
    private PlayerAnimatorHandler animHandler;
    private PlayerStatsManager stats;
    private CharacterController controller;

    public void Initialize(EldenRingMovement p, PlayerInputHandler i, PlayerAnimatorHandler a, PlayerStatsManager s, CharacterController c)
    {
        player = p;
        input = i;
        animHandler = a;
        stats = s;
        controller = c;

        // 初始化独立的脚步声音源
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.spatialBlend = 0.5f;
        footstepSource.volume = 0.6f;
        LoadGrassFootsteps();
    }

    // 核心暴露方法：处理每一帧的物理移动
    public void HandleLocomotionAndGravity()
    {
        bool isAttacking = player.currentState == EldenRingMovement.ActionState.HeavyAttack;
        bool isLightAttacking = player.currentState == EldenRingMovement.ActionState.LightAttack;
        bool isUltimateCasting = player.currentState == EldenRingMovement.ActionState.Ultimate;
        bool isCasting = player.currentState == EldenRingMovement.ActionState.SkillCast;
        bool isHit = player.currentState == EldenRingMovement.ActionState.Hit;
        bool isDodging = player.currentState == EldenRingMovement.ActionState.Dodging;

        bool hasMoveInput = input.MoveInput.magnitude > 0.1f && !isAttacking && !isLightAttacking && !player.isBlocking && !isHit && !isCasting && !isDodging;

        targetMoveDirection = Vector3.zero;
        if (hasMoveInput)
        {
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = Camera.main.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            targetMoveDirection = (camForward * input.MoveInput.y + camRight * input.MoveInput.x).normalized;
        }

        float targetSpeed = 0f;
        if (hasMoveInput && !isAttacking && !isLightAttacking && !isUltimateCasting && !isCasting)
        {
            targetSpeed = input.RunInput ? runSpeed : walkSpeed;
        }
        float accel = hasMoveInput ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);
        if (isUltimateCasting || isCasting) currentSpeed = 0f;

        // 处理旋转
        if (player.isLockedOn && player.lockedTarget != null)
        {
            Vector3 dirToTarget = player.lockedTarget.position - transform.position;
            dirToTarget.y = 0;
            if (dirToTarget != Vector3.zero)
            {
                float rotSpeed = (isAttacking || isLightAttacking || isUltimateCasting || isCasting) ? rotationSpeed * 0.2f : rotationSpeed;
                targetRotation = Quaternion.LookRotation(dirToTarget);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotSpeed * Time.deltaTime);
            }
        }
        else if (hasMoveInput && targetMoveDirection.magnitude > 0.1f && (!isAttacking && !isLightAttacking || isUltimateCasting || isCasting))
        {
            targetRotation = Quaternion.LookRotation(targetMoveDirection);
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

        // 处理重力与跳跃
        isGroundedCached = IsGrounded();
        if (isGroundedCached)
        {
            airTimer = 0f;
            isJumping = false;
            if (verticalVelocity < 0) verticalVelocity = -1.5f;
        }
        else airTimer += Time.deltaTime;

        if (input.JumpInput && isGroundedCached && !isAttacking && !isLightAttacking && !isCasting && !isHit && !player.isBlocking)
        {
            if (stats.ConsumeStamina(jumpStaminaCost))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isJumping = true;
                jumpStartSpeed = Mathf.Clamp01(currentSpeed / runSpeed);
                animHandler.anim.SetFloat("Speed", jumpStartSpeed);
                animHandler.anim.SetFloat("Direction", 0f);
                animHandler.anim.SetBool("IsMoving", false);
                animHandler.anim.SetTrigger("Jump");
            }
        }

        if (!isGroundedCached)
        {
            if (verticalVelocity < 0 && airTimer > 0.15f) verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
            else verticalVelocity += gravity * Time.deltaTime;
        }
        verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);

        // 应用最终位移
        if (controller != null && controller.enabled)
        {
            Vector3 horizontalVelocity = Vector3.zero;
            if (isCasting || isUltimateCasting) horizontalVelocity = Vector3.zero;
            else if (targetMoveDirection.magnitude > 0.1f && !isAttacking && !isLightAttacking)
            {
                horizontalVelocity = targetMoveDirection * currentSpeed;
            }

            if (player.impact.magnitude > 0.1f)
            {
                horizontalVelocity += player.impact;
                player.impact = Vector3.Lerp(player.impact, Vector3.zero, Time.deltaTime * 10f);
            }

            Vector3 finalVelocity = horizontalVelocity + new Vector3(0, verticalVelocity, 0);
            controller.Move(finalVelocity * Time.deltaTime);
        }
    }

    // 处理急停逻辑
    public void HandleStopTimers(bool hasMoveInput)
    {
        bool wasMovingPrev = wasMoving;
        bool wasRunningPrev = wasRunning;
        isRunning = input.RunInput && hasMoveInput;
        wasMoving = hasMoveInput;
        wasRunning = isRunning;

        if (wasMovingPrev && !hasMoveInput && wasRunningPrev)
        {
            isStopping = true;
            stopTimer = stopAnimationDuration;
        }

        if (isStopping)
        {
            if (hasMoveInput || player.currentState != EldenRingMovement.ActionState.IdleMove) isStopping = false;
            else
            {
                stopTimer -= Time.deltaTime;
                if (stopTimer <= 0) isStopping = false;
            }
        }
    }

    // 处理脚步声
    public void HandleFootsteps(bool hasMoveInput)
    {
        bool isMovingOnGround = isGroundedCached && hasMoveInput && player.currentState == EldenRingMovement.ActionState.IdleMove && !isJumping;
        if (isMovingOnGround)
        {
            float interval = isRunning ? runInterval : walkInterval;
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= interval)
            {
                footstepTimer = 0f;
                if (grassFootsteps != null && grassFootsteps.Length > 0)
                {
                    int idx = Random.Range(0, grassFootsteps.Length);
                    float originalPitch = footstepSource.pitch;
                    footstepSource.pitch = isRunning ? 1.3f : 1.0f;
                    footstepSource.PlayOneShot(grassFootsteps[idx], isRunning ? 0.7f : 0.5f);
                    footstepSource.pitch = originalPitch;
                }
            }
        }
        else footstepTimer = 0f;
    }

    public void ResetSpeed() { currentSpeed = 0f; isRunning = false; }

    private bool IsGrounded()
    {
        if (controller.isGrounded) return true;
        float radius = controller.radius * 0.75f; 
        Vector3 sphereCenter = transform.position + Vector3.up * (radius + 0.1f);
        if (Physics.SphereCast(sphereCenter, radius, Vector3.down, out RaycastHit hit, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit + 5f || hit.point.y <= transform.position.y + 0.1f) return true;
        }
        return false;
    }

    private void LoadGrassFootsteps()
    {
        grassFootsteps = Resources.LoadAll<AudioClip>("Audio/grass walk");
    }
}