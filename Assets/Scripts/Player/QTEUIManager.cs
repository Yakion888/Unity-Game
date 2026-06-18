using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI; // 引入 UI 库

public class QTEUIManager : MonoBehaviour
{
    [Header("UI 引用")]
    public CanvasGroup qteCanvasGroup;   // 控制整体透明度
    public RectTransform qteCircle;      // 需要跳动的圆形框
    public TextMeshProUGUI zhanText;     // "斩"字文本

    [Header("斩击特效 (新增)")]
    public GameObject slashEffectObj;    // 拖入你刚做的 SlashEffect 游戏物体
    public RectTransform slashRect;      // 拖入 SlashEffect 自身的 RectTransform
    public Image slashImage;             // 拖入 SlashEffect 自身的 Image 组件

    [Header("跳动设置")]
    public float pulseSpeed = 15f;       // 心跳速度
    public float minScale = 1.0f;        // 最小缩放
    public float maxScale = 1.3f;        // 最大缩放

    private Coroutine heartbeatCoroutine;
    private Coroutine _hideCoroutine; // 【Bug 修复】追踪 HideRoutine，防止与 ShowQTE 竞态

    private void Awake()
    {
        // 初始隐藏
        if (qteCanvasGroup != null)
        {
            qteCanvasGroup.alpha = 0f;
            qteCanvasGroup.gameObject.SetActive(false);
        }

        // 斩击初始隐藏
        if (slashEffectObj != null) slashEffectObj.SetActive(false);
    }

    // 呼出 QTE（开始心跳）
    public void ShowQTE()
    {
        if (qteCanvasGroup == null) return;

        // 【Bug 修复】如果 HideRoutine 还在播退出动画，立刻掐掉
        // 否则退出动画的最后一帧会把 SetActive(false) 执行在刚刚 Show 出来的 UI 上
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        qteCanvasGroup.gameObject.SetActive(true);
        qteCanvasGroup.alpha = 1f;

        // 恢复初始红色，并隐藏斩击特效
        if (zhanText != null) zhanText.color = new Color(0.8f, 0f, 0f, 1f);
        if (slashEffectObj != null) slashEffectObj.SetActive(false);

        // 停止之前的协程，开启新的心跳
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
    }

    // 隐藏 QTE（处理成功/失败特效）
    public void HideQTE(bool success)
    {
        if (qteCanvasGroup == null || !qteCanvasGroup.gameObject.activeSelf) return;

        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);

        // 【Bug 修复】如果上次 HideRoutine 还在跑，先停掉
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        _hideCoroutine = StartCoroutine(HideRoutine(success));
    }

    /// <summary>
    /// 【Bug 修复】强制关闭 QTE —— 无动画，直接 SetActive(false)。
    /// 供大招结束时的安全网使用。
    /// </summary>
    public void ForceHide()
    {
        if (heartbeatCoroutine != null) { StopCoroutine(heartbeatCoroutine); heartbeatCoroutine = null; }
        if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        if (qteCanvasGroup != null) qteCanvasGroup.gameObject.SetActive(false);
        if (slashEffectObj != null) slashEffectObj.SetActive(false);
    }

    // 心跳协程
    private IEnumerator HeartbeatRoutine()
    {
        while (true)
        {
            float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) / 2f);
            if (qteCircle != null)
            {
                qteCircle.localScale = new Vector3(scale, scale, 1f);
            }
            yield return null; 
        }
    }

    // QTE 结束时的特效（包含终极斩裂演出）
    private IEnumerator HideRoutine(bool success)
    {
        float elapsed = 0f;
        float duration = 0.2f; // 特效只有短短 0.2 秒，营造瞬间爆发的张力！

        Vector3 startScale = qteCircle.localScale;
        // 成功放大，失败缩小
        Vector3 targetScale = success ? new Vector3(2.5f, 2.5f, 1f) : new Vector3(0.5f, 0.5f, 1f); 

        // ==========================================
        // 【成功时刻】：触发斩击高光与剑气！
        // ==========================================
        if (success) 
        {
            if (zhanText != null) zhanText.color = new Color(1f, 0.8f, 0f, 1f); // 字变耀眼金
            
            if (slashEffectObj != null && slashRect != null)
            {
                slashEffectObj.SetActive(true);
                // 瞬间将剑光压缩成极短的线
                slashRect.localScale = new Vector3(0.1f, 0.5f, 1f);
                if (slashImage != null) slashImage.color = new Color(1f, 1f, 1f, 1f); // 纯白高光
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; 
            float t = elapsed / duration;

            // 圆圈底框放大/缩小，整体变透明
            if (qteCircle != null) qteCircle.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (qteCanvasGroup != null) qteCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            // ==========================================
            // 【特效动画】：剑光极速拉长、变宽，劈裂屏幕！
            // ==========================================
            if (success && slashEffectObj != null && slashRect != null)
            {
                // X轴(长度)瞬间暴涨到4倍，Y轴(厚度)变宽到2倍
                slashRect.localScale = Vector3.Lerp(new Vector3(0.1f, 0.5f, 1f), new Vector3(4.0f, 2.0f, 1f), t);
            }

            yield return null;
        }

        // 清理现场
        qteCanvasGroup.gameObject.SetActive(false);
        if (slashEffectObj != null) slashEffectObj.SetActive(false);
        _hideCoroutine = null; // 【Bug 修复】标记 HideRoutine 已完成
    }
}