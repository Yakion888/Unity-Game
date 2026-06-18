using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioPoolManager : MonoBehaviour
{
    public static AudioPoolManager Instance;

    [Header("池子容量设置")]
    public int initialPoolSize = 10;

    // ───────────────────────────────────────
    // 【GC 优化】仅声明，不隐式实例化。
    // 隐式 new Queue<T>() 底层数组默认容量 = 0，
    // Awake 中循环 Enqueue 会触发多次 ×2 扩容 → 旧数组被抛弃 → GC 尖峰。
    // 在 Awake 预热循环之前用 initialPoolSize 精准初始化，
    // 底层 T[] 一次分配到位，零扩容。
    // ───────────────────────────────────────
    private Queue<AudioSource> audioPool;

    private void Awake()
    {
        Instance = this;

        // ── GC 优化：传入容量，底层数组一次分配到位 ──
        audioPool = new Queue<AudioSource>(initialPoolSize);

        // 预热：一次性创建好备用 AudioSource，全部入池
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewAudioSource();
        }
    }

    private AudioSource CreateNewAudioSource()
    {
        GameObject audioObj = new GameObject("PooledAudioSource");
        audioObj.transform.SetParent(transform);
        AudioSource source = audioObj.AddComponent<AudioSource>();

        // 默认设置：无物理变调（多普勒）
        source.spatialBlend = 0.5f;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;

        audioObj.SetActive(false);
        audioPool.Enqueue(source);
        return source;
    }

    /// <summary>
    /// 核心播放方法。
    /// 池中有闲置则复用；池空则临时新建（保证不卡播放，但会产生单次 GC）。
    /// </summary>
    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f,
        Transform attachParent = null, bool is2D = false)
    {
        if (clip == null) return;

        AudioSource source;
        if (audioPool.Count > 0)
            source = audioPool.Dequeue();
        else
            source = CreateNewAudioSource(); // 池耗尽时按需扩容

        source.gameObject.SetActive(true);
        source.clip = clip;
        source.volume = volume;

        // UI 声音强制纯 2D (0f)，场景音效为 3D (1f)
        source.spatialBlend = is2D ? 0f : 1f;

        if (!is2D)
            source.transform.position = position;

        // 需要跟随挂载（如大招滞空）且非 UI 声音
        if (attachParent != null && !is2D)
        {
            source.transform.SetParent(attachParent);
        }

        source.Play();

        // 播放完毕后自动回池
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (source == null) yield break;

        source.Stop();
        source.gameObject.SetActive(false);
        source.transform.SetParent(transform); // 解除跟随，收回池子
        audioPool.Enqueue(source);
    }
}
