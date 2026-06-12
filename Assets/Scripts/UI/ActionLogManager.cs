using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic; // 引入队列所需的命名空间

public class ActionLogManager : MonoBehaviour
{
    // 单例模式，方便全局任何脚本直接调用
    public static ActionLogManager Instance;

    [Header("UI 设置")]
    public Transform logContainer;       // 存放消息的父级面板
    public GameObject logTextPrefab;     // 单条消息的预制体

    [Header("播报排队设置")]
    public float messageInterval = 0.4f; // 两条消息连续弹出时的间隔时间（秒）
    
    // 存放待播报消息的队列
    private Queue<string> messageQueue = new Queue<string>();
    // 标记当前是否正在处理队列
    private bool isProcessingQueue = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 开放给其他脚本调用的方法
    public void ShowMessage(string msg)
    {
        // 将新消息加入队列
        messageQueue.Enqueue(msg);

        // 如果当前没有在播报队列，就启动播报协程
        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    // 处理消息队列的协程
    private IEnumerator ProcessQueue()
    {
        isProcessingQueue = true;

        // 只要队列里还有消息，就一直循环播报
        while (messageQueue.Count > 0)
        {
            // 从队列头部取出一条消息
            string currentMsg = messageQueue.Dequeue();

            // 生成UI实体
            CreateLogUI(currentMsg);

            // 等待设定的间隔时间，再播报下一条
            // 这样就算瞬间塞入10条消息，它们也会每隔0.4秒弹出一个
            yield return new WaitForSeconds(messageInterval);
        }

        // 队列播报完毕，关闭标记
        isProcessingQueue = false;
    }

    // 负责实际生成预制体的方法
    private void CreateLogUI(string msg)
    {
        if (logTextPrefab == null || logContainer == null) return;

        // 生成一条新的文字
        GameObject newLog = Instantiate(logTextPrefab, logContainer);
        TextMeshProUGUI textMesh = newLog.GetComponent<TextMeshProUGUI>();
        textMesh.text = msg;

        // 让它在屏幕上停留几秒后，自动慢慢消失
        StartCoroutine(FadeAndDestroy(textMesh));
    }

    // 淡出并销毁的协程（保持你的原逻辑不变）
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