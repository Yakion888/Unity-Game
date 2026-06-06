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
    private string[] currentDialogLines;   // 当前使用的对话数组
    private bool isTaskCompletedForDialog;

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

        if (isPlayerInRange && !isDialogOpen && !hasTaskInProgress && Input.GetKeyDown(interactKey))
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
        talkPromptUI?.SetActive(false);
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
                playerMovement.AddXP(xpReward);
                playerMovement.AddGold(goldReward);
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
        talkPromptUI?.SetActive(false);
        isPlayerInRange = false;   // 强制玩家必须远离才能再次交互

        // 重置任务管理器，以便以后可以再次接取任务（如果需要）
        if (taskManager != null)
            taskManager.ResetTask();

        Debug.Log("任务奖励已发放，商人重置");
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (!isDialogOpen)
            {
                if (hasTaskInProgress)
                    talkPromptUI?.SetActive(false);   // 不显示交互提示，或者改为显示“任务进行中”的提示
                else
                    talkPromptUI?.SetActive(true);
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