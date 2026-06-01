using UnityEngine;
using UnityEngine.UI; // 如果你用的是 TextMeshPro，请加上 using TMPro;

public class RestPoint : MonoBehaviour
{
    [Header("自定义复活坐标点(可选)")]
    public Transform specificSpawnPoint; 

    [Header("UI提示设置")]
    public GameObject interactUI;

    [Header("音效表现")]
    public AudioClip restSound; // 拖入休息时的音效（篝火声/赐福声）

    private bool isPlayerNear = false;
    private EldenRingMovement playerMovement;

    void Start()
    {
        // 游戏开始时确保提示框是关闭的
        if (interactUI != null) interactUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            playerMovement = other.GetComponent<EldenRingMovement>();
            
            // 靠近时显示UI
            if (interactUI != null) interactUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            playerMovement = null;
            
            // 离开时隐藏UI
            if (interactUI != null) interactUI.SetActive(false);
        }
    }

    void Update()
    {
        // 玩家在附近 且 提示框处于激活状态 且 按下E键
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && playerMovement != null)
        {
            InteractWithRestPoint();
        }
    }

    private void InteractWithRestPoint()
    {
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (specificSpawnPoint != null)
        {
            spawnPos = specificSpawnPoint.position;
            spawnRot = specificSpawnPoint.rotation;
        }
        else
        {
            spawnPos = transform.position + transform.forward * 1.5f;
            spawnPos.y = playerMovement.transform.position.y; 
            spawnRot = Quaternion.LookRotation(-transform.forward); 
        }

        

        // 调用全新的渐变转场休息流程，并把音效传进去
        playerMovement.StartRestSequence(spawnPos, spawnRot, restSound);

        // 【新增交互反馈】：按完 E 之后，先把提示框关掉，防止玩家连续狂按，也代表操作成功
        if (interactUI != null) 
        {
            interactUI.SetActive(false);
            
            // 可选：如果你想让它过 2 秒后再次出现（方便玩家再次休息），可以开启一个协程
            StartCoroutine(ShowUIAgainAfterDelay());
        }
    }

    private System.Collections.IEnumerator ShowUIAgainAfterDelay()
    {
        // 模拟休息需要1.5秒时间
        yield return new WaitForSeconds(1.5f);
        
        // 如果 1.5 秒后玩家还在圈子里，就再次把 UI 显示出来
        if (isPlayerNear && interactUI != null)
        {
            interactUI.SetActive(true);
        }
    }
}