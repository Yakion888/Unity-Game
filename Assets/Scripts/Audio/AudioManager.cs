using UnityEngine;
using System.Collections;

// 行业标准的全局 BGM 交叉推流管理器 (0延迟 0卡顿)
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("绑定场景中你手动创建的音源")]
    [Tooltip("把你场景里的 NormalBgmSource 拖到这里")]
    public AudioSource exploreSource;  
    [Tooltip("把你场景里的 CombatBgmSource 拖到这里")]
    public AudioSource combatSource;   
    
    [Header("音量与渐变设置")]
    [Range(0f, 1f)] public float maxBGMVolume = 0.5f; 
    public float crossfadeDuration = 2.0f;            

    private bool isCombatBGMPlaying = false;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        Instance = this;

        if (exploreSource == null || combatSource == null)
        {
            Debug.LogError("严重错误：请在 AudioManager 面板中拖入探索和战斗的 AudioSource！");
            return;
        }

        // 强行接管你的手动配置，保证参数不出错
        exploreSource.loop = true;
        combatSource.loop = true;
        exploreSource.spatialBlend = 0f; // 强制2D，防声音忽远忽近
        combatSource.spatialBlend = 0f;

        // 游戏启动时，两首歌都在后台“同时”跑起来！
        // 把战斗音量锁死为 0，这样绝不会被听到
        exploreSource.volume = maxBGMVolume;
        combatSource.volume = 0f;

        if (!exploreSource.isPlaying) exploreSource.Play();
        if (!combatSource.isPlaying) combatSource.Play();
    }

    public void SetCombatState(bool inCombat, bool forceRestart = false)
    {
        if (exploreSource == null || combatSource == null) return;
        if (isCombatBGMPlaying == inCombat && !forceRestart) return;

        isCombatBGMPlaying = inCombat;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (inCombat)
        {
            // 如果要求强制重头放（刚发现敌人），我们不动 Play，而是直接把进度条拨回 0 秒！极致性能！
            if (forceRestart) combatSource.time = 0f; 
            fadeCoroutine = StartCoroutine(CrossfadeBGM(exploreSource, combatSource));
        }
        else
        {
            // 脱战
            fadeCoroutine = StartCoroutine(CrossfadeBGM(combatSource, exploreSource));
        }
    }

    private IEnumerator CrossfadeBGM(AudioSource fadeOutSource, AudioSource fadeInSource)
    {
        float timer = 0f;
        float startFadeOutVol = fadeOutSource.volume;
        float startFadeInVol = fadeInSource.volume;

        while (timer < crossfadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / crossfadeDuration;

            fadeOutSource.volume = Mathf.Lerp(startFadeOutVol, 0f, t);
            fadeInSource.volume = Mathf.Lerp(startFadeInVol, maxBGMVolume, t);

            yield return null;
        }

        // 强行对齐最终音量
        fadeOutSource.volume = 0f;
        fadeInSource.volume = maxBGMVolume;
    }
}