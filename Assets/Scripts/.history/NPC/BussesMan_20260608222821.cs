using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BussesMan : MonoBehaviour
{
    [Header("交互设置")]
    public string interactKey = "e";
    public KeyCode refuseKey = KeyCode.X;    // 拒绝任务的按键
    public GameObject talkPromptUI;          // 对话UI对象
    public GameObject dialogPanel;           // 对话框对象
    public TextMeshProUGUI dialogText;       // 对话文本对象
    public GameObject acceptButton;          // 接受任务UI对象
    public GameObject refuseButton;          // 拒绝任务UI对象
    public TaskManager taskManager;          // 任务栏对象
    public int xpReward = 250;               // 任务经验奖励
    public int goldReward = 100;             // 任务金币奖励

    private bool hasTaskInProgress = false;   // 是否已接受任务且未完成
    private string[] currentDialogLines;      // 当前使用的对话数组
    private bool isTaskCompletedForDialog;    // 任务是否完成标志

    private bool canInteract = true;          // 是否可交互（领取奖励后变为false）
    public GameObject temporaryRestPoint;     // 场景中的临时休息点（初始隐藏）
    private bool hasLeft = false;             // 商人是否离开的标志

    [Header("对话内容")]
    [TextArea] public string[] dialogLines;

    [Header("完成任务后的对话")]
    [TextArea] public string[] completionDialogLines;

    [Header("References")]
    public Transform player;

    private Animator anim;
    private bool isPlayerInRange = false;
    private bool isDialogOpen = false;
    private int currentLine = 0;

    void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;
        anim = GetComponent<Animator>();
        if (anim == null) Debug.LogWarning("未找到Animator组件");

        talkPromptUI?.SetActive(false);
        dialogPanel?.SetActive(false);
        acceptButton?.SetActive(false);
        refuseButton?.SetActive(false);

        if (anim != null) anim.SetInteger("state", 0);
    }

    void Update()
    {
        if (hasTaskInProgress && taskManager != null && taskManager.IsTaskCompleted)
        {
            hasTaskInProgress = false;
            Debug.Log("任务完成，商人可以再次发布任务");
        }

        if (isPlayerInRange && !isDialogOpen && !hasTaskInProgress && canInteract && Input.GetKeyDown(interactKey))
        {
            StartDialog();
        }
        else if (isDialogOpen)
        {
            if (Input.GetKeyDown(interactKey))
            {
                // 如果是完成任务后的对话，最后一句领取奖励
                if (isTaskCompletedForDialog)
                {
                    if (currentLine == currentDialogLines.Length - 1)
                        GiveReward();
                    else
                        NextLine();
                }
                else
                {
                    // 普通任务对话：最后一句接受任务，否则下一句
                    if (currentLine == currentDialogLines.Length - 1)
                        AcceptQuest();
                    else
                        NextLine();
                }
            }
            else if (Input.GetKeyDown(refuseKey))
            {
                RefuseQuest();
            }
        }
    }

    void StartDialog()
    {
        // 检查任务是否已完成（通过 TaskManager 的 IsTaskCompleted 属性）
        bool taskCompleted = (taskManager != null && taskManager.IsTaskCompleted);
        isTaskCompletedForDialog = (taskManager != null && taskManager.IsTaskCompleted);

        if (taskCompleted)
        {
            // 使用完成任务后的对话
            currentDialogLines = completionDialogLines;
        }
        else
        {
            // 使用原任务发布对话
            currentDialogLines = dialogLines;
        }

        // 如果对话数组为空或长度为0，则无法打开对话框
        if (currentDialogLines == null || currentDialogLines.Length == 0)
        {
            Debug.LogWarning("没有可用的对话内容");
            return;
        }

        isDialogOpen = true;
        currentLine = 0;
        dialogPanel?.SetActive(true);
        UpdateDialogText();
        if (anim != null) anim.SetInteger("state", 1);
    }

    void NextLine()
    {
        currentLine++;
        UpdateDialogText();

        if (anim != null) anim.SetTrigger("triggerNod");

        // 示例：第三句话（索引2）生气的动画
        if (currentLine == 2 && anim != null)
            anim.SetTrigger("triggerYell");
    }

    void UpdateDialogText()
    {
        if (dialogText != null && currentLine < currentDialogLines.Length)
            dialogText.text = currentDialogLines[currentLine];

        // 如果是原任务发布对话的最后一句，显示接受/拒绝按钮
        if (!isTaskCompletedForDialog && currentLine == currentDialogLines.Length - 1)
        {
            talkPromptUI?.SetActive(false);
            acceptButton?.SetActive(true);
            refuseButton?.SetActive(true);
        }
    }

    public void GiveReward()
    {
        // 获取玩家脚本并发放奖励
        if (player != null)
        {
            EldenRingMovement playerMovement = player.GetComponent<EldenRingMovement>();
            if (playerMovement != null)
            {
                playerMovement.RewardXP(xpReward);
                playerMovement.RewardGold(goldReward);
                Debug.Log($"发放奖励：经验 +{xpReward}，金币 +{goldReward}");
            }
            else
            {
                Debug.LogWarning("玩家身上没有 EldenRingMovement 脚本");
            }
        }
        else
        {
            Debug.LogError("玩家对象未找到，无法发放奖励");
        }

        // 播放鼓掌动画
        if (anim != null)
        {
            anim.SetInteger("state", 0);
            anim.SetTrigger("triggerClap");
        }

        // 关闭所有 UI
        CloseAllUI();
        taskManager.CloseTaskBar();
        talkPromptUI?.SetActive(false);
        isPlayerInRange = false;   // 强制玩家必须远离才能再次交互

        canInteract = false;
    }

    public void AcceptQuest()
    {
        if (hasTaskInProgress) return; // 任务进行中不能再次接受

        if (anim != null)
        {
            anim.SetInteger("state", 0);
            anim.SetTrigger("triggerClap");
        }

        CloseAllUI();
        talkPromptUI?.SetActive(false);
        isPlayerInRange = false;

        if (taskManager != null)
        {
            taskManager.AcceptTask();
            hasTaskInProgress = true;
        }

        Debug.Log("接受任务");
    }

    public void RefuseQuest()
    {
        if (anim != null) anim.SetInteger("state", 0);
        CloseAllUI();
        talkPromptUI?.SetActive(false);
        isPlayerInRange = false;
        Debug.Log("拒绝任务");
    }

    void CloseAllUI()
    {
        isDialogOpen = false;
        dialogPanel?.SetActive(false);
        acceptButton?.SetActive(false);
        refuseButton?.SetActive(false);
        // 注意：这里不再根据 isPlayerInRange 显示 talkPromptUI
        // 交互提示的显示/隐藏完全由触发器进出控制
    }

    public void MerchantLeave()
    {
        if (hasLeft) return;
        hasLeft = true;

        // 播报消息
        if (ActionLogManager.Instance != null)
            ActionLogManager.Instance.ShowMessage("商人已离开，并为你建设了一个临时休息处");
        else
            Debug.Log("商人已离开，并为你建设了一个临时休息处");

        // 显示临时休息点
        if (temporaryRestPoint != null)
        {
            var restPoint = temporaryRestPoint.GetComponent<RestPoint>();
            if (restPoint != null) restPoint.isActive = false;
            temporaryRestPoint.SetActive(true);
        }
        else
            Debug.LogWarning("未指定临时休息点对象，无法显示");

        // 商人消失
        Transform parent = transform.parent;
        if (parent != null && parent.name == "1stMission")
            parent.gameObject.SetActive(false);
        else
            gameObject.SetActive(false); // 降级方案：只禁用自身
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (!isDialogOpen)
            {
                // 只有可以交互且没有进行中的任务时，才显示交互提示
                if (canInteract && !hasTaskInProgress)
                    talkPromptUI?.SetActive(true);
                else
                    talkPromptUI?.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            if (isDialogOpen) CloseAllUI();
            else talkPromptUI?.SetActive(false);
        }
    }
}