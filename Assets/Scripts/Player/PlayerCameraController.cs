using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("相机设置")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;
    public float verticalLookLimit = 80f;
    public float cameraDistance = 5.5f;     // 相机距离
    public float cameraHeight = 1.5f;       // 相机自身的高度
    public float lookAtHeight = 1.2f;       // 相机的“准星”看着玩家身体的哪个高度

    [Header("相机碰撞")]
    public LayerMask cameraCollisionMask = -1;   // 相机碰撞的层
    public float cameraCollisionRadius = 0.2f;   // 球形检测半径
    public float cameraMinDistance = 0.5f;       // 相机最近距离

    // 内部参数
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    // 获取主控脚本的引用，用于读取玩家状态（是否死亡、是否锁敌等）
    private EldenRingMovement player;

    void Start()
    {
        player = GetComponent<EldenRingMovement>();
        
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
    }

    // 【行业核心技巧】：相机跟随必须写在 LateUpdate 里！
    // 确保玩家在 Update 里走完移动后，相机再去追，彻底消除画面抖动！
    void LateUpdate()
    {
        // 如果玩家死了或者在休息转场，相机不再接收玩家的鼠标输入
        if (player.isDead || player.isResting) return;

        HandleCameraControl();
    }

    private void HandleCameraControl()
    {
        // 1. 读取鼠标输入（如果打开了UI，屏蔽滑动）
        float mouseX = player.isUIOpen ? 0f : Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = player.isUIOpen ? 0f : Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (Mathf.Abs(mouseX) < 0.01f) mouseX = 0;
        if (Mathf.Abs(mouseY) < 0.01f) mouseY = 0;

        currentYaw += mouseX;
        currentPitch -= mouseY;

        // 2. 锁定时的强制接管
        if (player.isLockedOn && player.lockedTarget != null)
        {
            Vector3 dirToTarget = player.lockedTarget.position - transform.position;
            float targetYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * 10f);

            float distance = dirToTarget.magnitude;
            float deltaY = player.lockedTarget.position.y - (transform.position.y + 1.5f);
            float ratio = Mathf.Clamp(deltaY / Mathf.Max(distance, 1f), -1f, 1f);
            float targetPitch = -Mathf.Asin(ratio) * Mathf.Rad2Deg;
            currentPitch = Mathf.LerpAngle(currentPitch, targetPitch + 10f, Time.deltaTime * 5f);
        }
        
        currentPitch = Mathf.Clamp(currentPitch, -verticalLookLimit, verticalLookLimit);

        // 3. 计算位置与碰撞避障
        if (cameraTransform != null)
        {
            Vector3 desiredPosition = transform.position + Quaternion.Euler(currentPitch, currentYaw, 0) * new Vector3(0, cameraHeight, -cameraDistance);
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            Vector3 cameraToPlayer = lookTarget - desiredPosition;
            float targetDistance = cameraToPlayer.magnitude;
            Vector3 direction = cameraToPlayer.normalized;

            if (Physics.SphereCast(lookTarget, cameraCollisionRadius, -direction, out RaycastHit hit, targetDistance, cameraCollisionMask))
            {
                float distance = Mathf.Clamp(hit.distance - cameraCollisionRadius, cameraMinDistance, targetDistance);
                desiredPosition = lookTarget - direction * distance;
            }

            // 平滑移动并看向目标
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, Time.deltaTime * 10f);
            cameraTransform.LookAt(lookTarget);
        }
    }

    // 重置相机到背后的功能，现在完全归这个脚本管了！
    public void ResetCameraBehindPlayer()
    {
        currentYaw = transform.eulerAngles.y;
        currentPitch = 15f; 

        if (cameraTransform != null)
        {
            Vector3 desiredPosition = transform.position + Quaternion.Euler(currentPitch, currentYaw, 0) * new Vector3(0, cameraHeight, -cameraDistance);
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            
            cameraTransform.position = desiredPosition;
            cameraTransform.LookAt(lookTarget);
        }
    }
}