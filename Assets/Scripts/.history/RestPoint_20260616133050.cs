using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class RestPoint : MonoBehaviour
{
    [Header("传送设置")]
    public bool isActive = false;           // 是否已激活（初始休息点勾选）
    public string restPointName = "休息点";  // 显示名称
    public GameObject fastTravelPanelPrefab; // 传送面板预制体
    public KeyCode travelKey = KeyCode.X;    // 打开传送面板的按键
    public static List<RestPoint> allActiveRestPoints = new List<RestPoint>();
    public GameObject parentToShow;   // 需要显示的父对象

    private static GameObject currentTravelPanel = null;

    [Header("自定义复活坐标点(可选)")]
    public Transform specificSpawnPoint; 

    [Header("UI提示设置")]
    public GameObject interactUI;
    public GameObject travelUI;

    [Header("音效表现")]
    public AudioClip restSound; // 拖入休息时的音效（篝火声/赐福声）

    private bool isPlayerNear = false;
    private EldenRingMovement playerMovement;

    [Header("References")]
    public Transform player;

    public float triggerRadius = 2f;

    void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;
        playerMovement = player.GetComponent<EldenRingMovement>();

        // 游戏开始时确保提示框是关闭的
        if (interactUI != null) interactUI.SetActive(false);
        if (travelUI != null) travelUI.SetActive(false);

        if (isActive)
        {
            if (!allActiveRestPoints.Contains(this))
                allActiveRestPoints.Add(this);
        }
    }

    // 用于从存档中恢复激活状态
    public void Activate()
    {
        // 确保父对象显示
        if (parentToShow != null && !parentToShow.activeSelf)
            parentToShow.SetActive(true);

        if (!isActive)
        {
            isActive = true;
            if (!allActiveRestPoints.Contains(this))
                allActiveRestPoints.Add(this);
            Debug.Log($"休息点 {restPointName} 已激活");
        }
    }

    void Update()
    {
        if (playerMovement == null) return;
        float dist = Vector3.Distance(transform.position, playerMovement.transform.position);
        bool shouldBeNear = dist <= triggerRadius;
        if (shouldBeNear != isPlayerNear)
        {
            isPlayerNear = shouldBeNear;
            if (isPlayerNear)
            {
                // 进入范围逻辑（显示UI等）
                if (interactUI != null) interactUI.SetActive(true);
                if (travelUI != null) travelUI.SetActive(true);
            }
            else
            {
                // 离开范围逻辑（隐藏UI等）
                if (interactUI != null) interactUI.SetActive(false);
                if (travelUI != null) travelUI.SetActive(false);
                CloseTravelPanel();
            }
        }

        if (!isPlayerNear) return;
        if (playerMovement.isUIOpen) return;

        // 按 E 休息
        if (Input.GetKeyDown(KeyCode.E))
        {
            InteractWithRestPoint();
        }
        // 按 X 打开传送面板
        else if (Input.GetKeyDown(travelKey))
        {
            OpenFastTravelPanel();
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

        // 检查是否需要让商人离开（仅当任务已完成且商人尚未消失时）
        BussesMan merchant = FindObjectOfType<BussesMan>();
        if (merchant != null && merchant.taskManager != null && merchant.taskManager.IsTaskCompleted && merchant.gameObject.activeSelf)
        {
            merchant.MerchantLeave();
        }

        if (!isActive)
        {
            isActive = true;
            if (!allActiveRestPoints.Contains(this))
                allActiveRestPoints.Add(this);
            Debug.Log($"休息点 {restPointName} 已激活");
        }

        // 调用全新的渐变转场休息流程，并把音效传进去
        playerMovement.StartRestSequence(spawnPos, spawnRot, restSound);

        // 【新增交互反馈】：按完 E 之后，先把提示框关掉，防止玩家连续狂按，也代表操作成功
        if (interactUI != null) 
        {
            interactUI.SetActive(false);
            travelUI.SetActive(false);

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
            travelUI.SetActive(true);
        }
    }

    private void OpenFastTravelPanel()
    {
        if (fastTravelPanelPrefab == null)
        {
            Debug.LogError("fastTravelPanelPrefab is null!");
            return;
        }

        // 关闭旧面板
        if (currentTravelPanel != null)
        {
            Destroy(currentTravelPanel);
            currentTravelPanel = null;
        }

        // 实例化预制体
        GameObject panel = Instantiate(fastTravelPanelPrefab);
        panel.name = "FastTravelCanvas_Clone";          // 便于识别
        currentTravelPanel = panel;

        // ------------------------------
        // 强制确保 Canvas 正确设置（即使预制体没有，代码也会添加）
        // ------------------------------
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = panel.AddComponent<Canvas>();
            Debug.Log("动态添加了 Canvas 组件");
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;              // 最高优先级，确保不被其他UI遮挡

        // 添加 GraphicRaycaster 以支持 UI 交互
        if (panel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 强制 RectTransform 铺满全屏
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetParent(null);                // 设为独立根对象
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        // 初始化传送面板逻辑
        var travelCtrl = panel.GetComponent<RestPointTravel>();
        if (travelCtrl != null)
        {
            travelCtrl.SetAvailablePoints(GetAllActiveRestPoints(), playerMovement);
        }
        else
        {
            Debug.LogError("RestPointTravel component missing on FastTravel prefab!");
            Destroy(panel);
            currentTravelPanel = null;
            return;
        }

        // 确保面板激活
        panel.SetActive(true);
        Debug.Log("新面板已实例化，名称: " + panel.name);
    }

    public static void CloseTravelPanel()
    {
        if (currentTravelPanel != null)
            Destroy(currentTravelPanel);
    }

    private List<RestPoint> GetAllActiveRestPoints()
    {
        // 返回所有已激活的休息点（需要你在全局管理或从静态列表获取）
        // 简化：假设有一个静态列表
        return RestPoint.allActiveRestPoints;
    }

    //获取重生所处位置
    public Vector3 GetSpawnPosition()
    {
        if (specificSpawnPoint != null)
            return specificSpawnPoint.position;
        // 默认：休息点前方 1.5 米，地面高度 +0.1 米
        return transform.position + transform.forward * 1.5f + Vector3.up * 0.1f;
    }
    //获取重生面朝方向
    public Quaternion GetSpawnRotation()
    {
        if (specificSpawnPoint != null)
            return specificSpawnPoint.rotation;
        return Quaternion.LookRotation(-transform.forward);
    }

    void OnDestroy()
    {
        // 移除自身
        if (allActiveRestPoints.Contains(this))
            allActiveRestPoints.Remove(this);
    }
}