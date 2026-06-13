using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioPoolManager : MonoBehaviour
{
    public static AudioPoolManager Instance;

    [Header("池子容量设置")]
    public int initialPoolSize = 10;

    private Queue<AudioSource> audioPool = new Queue<AudioSource>();

    private void Awake()
    {
        Instance = this;

        // 游戏启动时，一次性创建好一堆 AudioSource 备用
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

    // 🌟 核心播放方法（注意这里括号里的最后一个参数：bool is2D = false）
    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, Transform attachParent = null, bool is2D = false)
    {
        if (clip == null) return;

        AudioSource source;
        if (audioPool.Count > 0) source = audioPool.Dequeue();
        else source = CreateNewAudioSource(); // 不够用再临时造

        source.gameObject.SetActive(true);
        source.clip = clip;
        source.volume = volume;

        // 【架构核心】：UI 声音强制为纯 2D (0f)，战斗声音强制为 3D (1f)
        source.spatialBlend = is2D ? 0f : 1f;

        // 如果是 2D 声音，位置无所谓；如果是 3D 声音，设置实际位置
        if (!is2D) source.transform.position = position;

        // 如果需要声音跟着人走（比如大招滞空）且不是 2D UI声音
        if (attachParent != null && !is2D)
        {
            source.transform.SetParent(attachParent);
        }

        source.Play();

        // 播放完自动回收到池子里
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 防御性编程
        if (source == null) yield break;

        source.Stop();
        source.gameObject.SetActive(false);
        source.transform.SetParent(transform); // 取消跟随，收回池子管理
        audioPool.Enqueue(source);
    }
}