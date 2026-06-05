using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BussesMan : MonoBehaviour
{
    [Header("交互设置")]
    public string interactKey = "e";
    public GameObject talkPromptUI;      // “按E交谈”提示
    public GameObject dialogPanel;       // 对话框
    public TextMeshProUGUI dialogText;   // 对话框文字
    public GameObject acceptButton;      // 接受任务按钮
    public GameObject refuseButton;      // 拒绝任务按钮

    [Header("对话内容")]
    [TextArea] public string[] dialogLines;

    private Animator anim;
    private bool isPlayerInRange = false;
    private bool isDialogOpen = false;
    private int currentLine = 0;

    void Start()
    {
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
        if (isPlayerInRange && !isDialogOpen && Input.GetKeyDown(interactKey))
            StartDialog();
        else if (isDialogOpen && Input.GetKeyDown(interactKey))
        {
            if (currentLine < dialogLines.Length - 1)
                NextLine();
            // 最后一句按E无效
        }
    }

    void StartDialog()
    {
        isDialogOpen = true;
        currentLine = 0;
        talkPromptUI?.SetActive(false);
        dialogPanel?.SetActive(true);
        UpdateDialogText();
        if (anim != null) anim.SetInteger("state", 1); // 站立
    }

    void NextLine()
    {
        currentLine++;
        UpdateDialogText();

        // 每次按E点头
        if (anim != null) anim.SetTrigger("triggerNod");

        // 示例：第三句话（索引2）生气的动画
        if (currentLine == 2 && anim != null)
            anim.SetTrigger("triggerYell");
    }

    void UpdateDialogText()
    {
        if (dialogText != null && currentLine < dialogLines.Length)
            dialogText.text = dialogLines[currentLine];

        // 最后一句：隐藏对话框，显示接受/拒绝按钮
        if (currentLine == dialogLines.Length - 1)
        {
            dialogPanel?.SetActive(false);
            acceptButton?.SetActive(true);
            refuseButton?.SetActive(true);
        }
    }

    public void AcceptQuest()
    {
        if (anim != null)
        {
            anim.SetInteger("state", 0); // 回待机
            anim.SetTrigger("triggerClap");
        }
        CloseAllUI();
        Debug.Log("接受任务");
    }

    public void RefuseQuest()
    {
        if (anim != null) anim.SetInteger("state", 0);
        CloseAllUI();
        Debug.Log("拒绝任务");
    }

    void CloseAllUI()
    {
        isDialogOpen = false;
        dialogPanel?.SetActive(false);
        acceptButton?.SetActive(false);
        refuseButton?.SetActive(false);
        if (isPlayerInRange) talkPromptUI?.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (!isDialogOpen) talkPromptUI?.SetActive(true);
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