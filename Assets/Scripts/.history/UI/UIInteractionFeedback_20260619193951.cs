using UnityEngine;
using UnityEngine.EventSystems;

// 【工程化设计】：同时继承悬停进入、悬停离开、点击 三大 UI 事件接口
// 做到视觉与听觉的彻底解耦封装，挂在任何 UI 上都能直接生效！
public class UIInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("视觉目标")]
    [Tooltip("必须挂载被布局组控制的子对象（如 Text），防止与 LayoutGroup 冲突")]
    public RectTransform targetVisual;

    [Header("悬停视觉反馈")]
    public float hoverScale = 1.15f; 
    public Vector2 hoverOffset = new Vector2(20f, 0f); 
    public float smoothSpeed = 15f;  

    [Header("听觉反馈 (音频资源)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    // 内部状态缓存
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private Vector3 currentTargetScale;
    private Vector2 currentTargetPosition;

    private void Start()
    {
        if (targetVisual == null) 
            targetVisual = GetComponent<RectTransform>();

        // 缓存初始数据，遵循数据不可变原则
        originalScale = targetVisual.localScale;
        originalPosition = targetVisual.anchoredPosition;

        currentTargetScale = originalScale;
        currentTargetPosition = originalPosition;
    }

    private void Update()
    {
         // 使用 unscaledDeltaTime，无视时间静止魔法！
        targetVisual.localScale = Vector3.Lerp(targetVisual.localScale, currentTargetScale, Time.unscaledDeltaTime * smoothSpeed);
        targetVisual.anchoredPosition = Vector2.Lerp(targetVisual.anchoredPosition, currentTargetPosition, Time.unscaledDeltaTime * smoothSpeed);
    }

    // 接口：鼠标悬停
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 视觉变化
        currentTargetScale = originalScale * hoverScale;
        currentTargetPosition = originalPosition + hoverOffset;

        // 听觉分发：向对象池申请播放 2D 悬停音效
        PlayFeedbackSound(hoverSound);
    }

    // 接口：鼠标离开
    public void OnPointerExit(PointerEventData eventData)
    {
        // 视觉复位
        currentTargetScale = originalScale;
        currentTargetPosition = originalPosition;
    }

    // 接口：鼠标点击
    public void OnPointerClick(PointerEventData eventData)
    {
        // 听觉分发：向对象池申请播放 2D 点击音效
        PlayFeedbackSound(clickSound);
        
        // 点击瞬间可以加个极微小的视觉收缩反馈（可选）
        targetVisual.localScale = originalScale * 0.95f; 
    }

    // 统一的音频派发代理
    private void PlayFeedbackSound(AudioClip clip)
    {
        if (clip != null && AudioPoolManager.Instance != null)
        {
            // 注意最后一个参数传 true，代表这绝对是一个 2D 的 UI 音效！
            AudioPoolManager.Instance.PlaySound(clip, Vector3.zero, soundVolume, null, true);
        }
    }
}