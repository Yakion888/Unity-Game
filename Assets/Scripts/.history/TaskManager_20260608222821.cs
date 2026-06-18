using UnityEngine;
using TMPro;

public class TaskManager : MonoBehaviour
{
    [Header("任务栏 UI")]
    public GameObject taskBar;                 // 任务栏面板
    public TextMeshProUGUI taskText;           // 任务文本（支持 TMP）
    public bool IsTaskCompleted => taskCompleted; //检测任务状态

    [Header("任务配置")]
    public string taskDescription = "击败强盗 0/1";
    public int requiredKills = 1;              // 需要击杀数量
    private int currentKills = 0;

    private bool taskActive = false;            // 任务是否已激活（接受任务后）
    private bool taskCompleted = false;

    void Start()
    {
        // 初始隐藏任务栏
        taskBar.SetActive(false);
    }

    // 接受任务时调用（由商人脚本触发）
    public void AcceptTask()
    {
        taskActive = true;
        taskCompleted = false;
        currentKills = 0;
        UpdateTaskUI();
        taskBar.SetActive(true);
    }

    public void CloseTaskBar()
    {
        taskBar.SetActive(false);
    }

    public void ResetTask()
    {
        taskActive = false;
        taskCompleted = false;
        currentKills = 0;
        taskBar.SetActive(false);
        // 如果需要重置文本颜色
        if (taskText != null) taskText.color = Color.yellow;
    }

    // 当敌人死亡时调用此方法（从敌人脚本中调用）
    public void ReportEnemyKilled()
    {
        if (!taskActive || taskCompleted) return;

        currentKills++;
        UpdateTaskUI();

        if (currentKills >= requiredKills)
        {
            taskCompleted = true;
            taskText.color = Color.green;   // 完成后变绿
            // 可选：完成任务后的其他逻辑（例如播放特效、奖励）
            Debug.Log("任务完成！");
        }
    }

    void UpdateTaskUI()
    {
        if (taskText != null)
        {
            taskText.text = $"击败强盗 {currentKills}/{requiredKills}";
            // 未完成时保持黄色
            if (!taskCompleted)
                taskText.color = Color.yellow;
            else
                taskText.color = Color.green;
        }
    }
}