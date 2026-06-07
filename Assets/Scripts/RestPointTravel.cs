using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class RestPointTravel : MonoBehaviour
{
    [Header("UI References")]
    public Transform listContainer;
    public GameObject optionTemplate;
    public TextMeshProUGUI bottomPrompt;

    private List<RestPoint> availablePoints;
    private EldenRingMovement playerMovement;
    private int selectedIndex = 0;
    private List<TextMeshProUGUI> optionTexts = new List<TextMeshProUGUI>();

    // 由 RestPoint 调用，传入已激活的休息点列表
    public void SetAvailablePoints(List<RestPoint> points, EldenRingMovement player)
    {
        availablePoints = points;
        playerMovement = player;
        GenerateOptions();
        UpdateSelection();
        bottomPrompt.text = "传送（E）";

        if (playerMovement != null)
            playerMovement.isUIOpen = true;
    }

    void GenerateOptions()
    {
        // 清除现有选项（保留模板）
        foreach (Transform child in listContainer)
        {
            if (child.gameObject != optionTemplate)
                Destroy(child.gameObject);
        }
        optionTemplate.SetActive(false);

        for (int i = 0; i < availablePoints.Count; i++)
        {
            var newOption = Instantiate(optionTemplate, listContainer);
            newOption.SetActive(true);
            var text = newOption.GetComponent<TextMeshProUGUI>();
            text.text = availablePoints[i].restPointName;
            text.color = Color.white;
            optionTexts.Add(text);
        }
    }

    void Update()
    {
        if (availablePoints == null || availablePoints.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            RestPoint.CloseTravelPanel();
            return;
        }
        //列表的选择，上下移动列表选项，按E选择传送目的地
        if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = (selectedIndex - 1 + availablePoints.Count) % availablePoints.Count;
            UpdateSelection();
        }
        else if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = (selectedIndex + 1) % availablePoints.Count;
            UpdateSelection();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            Teleport();
        }
    }

    //更新列表保证所选休息地为黄色
    void UpdateSelection()
    {
        for (int i = 0; i < optionTexts.Count; i++)
        {
            optionTexts[i].color = (i == selectedIndex) ? Color.yellow : Color.white;
        }
    }

    //传送代码
    private void Teleport()
    {
        if (availablePoints == null || availablePoints.Count == 0) return;

        var targetPoint = availablePoints[selectedIndex];
        Vector3 targetPos = targetPoint.GetSpawnPosition();
        Quaternion targetRot = targetPoint.GetSpawnRotation();

        // 关闭面板（先关闭，避免传送后仍存在）
        RestPoint.CloseTravelPanel();

        // 调用玩家的传送方法
        if (playerMovement != null)
            playerMovement.StartTeleport(targetPos, targetRot);
        else
            Debug.LogError("playerMovement 为空，无法传送");
    }


    void OnDestroy()
    {
        if (playerMovement != null)
            playerMovement.isUIOpen = false;
    }
}