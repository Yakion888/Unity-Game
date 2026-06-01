using UnityEngine;
using TMPro;
using System.Collections;

public class ActionLogManager : MonoBehaviour
{
    // 单例模式，方便全局任何脚本直接调用
    public static ActionLogManager Instance;

    [Header("UI 设置")]
    public Transform logContainer;       // 存放消息的父级面板
    public GameObject logTextPrefab;     // 单条消息的预制体

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 开放给其他脚本调用的方法
    public void ShowMessage(string msg)
    {
        if (logTextPrefab == null || logContainer == null) return;

        // 生成一条新的文字
        GameObject newLog = Instantiate(logTextPrefab, logContainer);
        TextMeshProUGUI textMesh = newLog.GetComponent<TextMeshProUGUI>();
        textMesh.text = msg;

        // 让它在屏幕上停留几秒后，自动慢慢消失
        StartCoroutine(FadeAndDestroy(textMesh));
    }

    private IEnumerator FadeAndDestroy(TextMeshProUGUI textMesh)
    {
        // 停留 2.5 秒
        yield return new WaitForSeconds(2.5f);

        // 逐渐变透明 (用 1 秒的时间淡出)
        float fadeDuration = 1.0f;
        float elapsed = 0f;
        Color startColor = textMesh.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // 完全透明后销毁物体
        Destroy(textMesh.gameObject);
    }
}