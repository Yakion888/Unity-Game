using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM 音源")]
    public AudioSource normalBgmSource;    // 常规BGM音源
    public AudioSource combatBgmSource;    // 战斗BGM音源（暂未使用，预留）

    [Header("BGM 淡入淡出时间")]
    public float fadeDuration = 1f;

    private bool isInCombat = false;
    private float normalVolume;
    private float combatVolume;
    private Coroutine currentFadeRoutine = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 场景切换时保留
    }

    private void Start()
    {
        if (normalBgmSource == null)
            Debug.LogError("Normal BGM AudioSource not assigned!");
        else
        {
            normalVolume = normalBgmSource.volume;
            normalBgmSource.loop = true;
            normalBgmSource.Play();
        }

        if (combatBgmSource != null)
        {
            combatVolume = combatBgmSource.volume;
            combatBgmSource.loop = true;
            combatBgmSource.volume = 0;
            combatBgmSource.Play();
        }
    }

    /// <summary>
    /// 设置战斗状态（由玩家脚本调用）
    /// </summary>
    /// <param name="inCombat">是否进入战斗</param>
    /// <param name="restartCombatBgm">是否从头播放战斗BGM（仅当 inCombat = true 时有效）</param>
    public void SetCombatState(bool inCombat, bool restartCombatBgm = false)
    {
        if (isInCombat == inCombat) return;

        if (inCombat)
        {
            // 如果需要重启战斗 BGM，则先停止并从头播放
            if (restartCombatBgm)
            {
                combatBgmSource.Stop();
                combatBgmSource.Play();
                combatBgmSource.volume = 0f;  // 静音开始，等待淡入
            }
            else
            {
                // 不重启：确保战斗 BGM 正在播放（如果暂停则恢复）
                if (!combatBgmSource.isPlaying)
                    combatBgmSource.UnPause();
            }
        }

         // 执行淡入淡出切换
        if (currentFadeRoutine != null)
            StopCoroutine(currentFadeRoutine);   
        
        if (inCombat)
            currentFadeRoutine = StartCoroutine(FadeBGM(normalBgmSource, combatBgmSource, fadeDuration));

        else
            currentFadeRoutine = StartCoroutine(FadeBGM(combatBgmSource, normalBgmSource, fadeDuration));

        isInCombat = inCombat;    
    }

    private System.Collections.IEnumerator FadeBGM(AudioSource fadeOutSource, AudioSource fadeInSource, float duration)
    {
        float startOutVol = fadeOutSource.volume;
        float startInVol = fadeInSource.volume;
        float targetOutVol = 0f;
        float targetInVol = (fadeInSource == normalBgmSource) ? normalVolume : combatVolume;

        // 淡入开始前：确保淡入音源已经处于“播放”状态（如果是暂停则恢复）
        if (fadeInSource == normalBgmSource && !normalBgmSource.isPlaying)
        {
            normalBgmSource.UnPause();
        }
        else if (fadeInSource != normalBgmSource && !fadeInSource.isPlaying)
        {
            fadeInSource.Play();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            fadeOutSource.volume = Mathf.Lerp(startOutVol, targetOutVol, t);
            fadeInSource.volume = Mathf.Lerp(startInVol, targetInVol, t);
            yield return null;
        }

        fadeOutSource.volume = targetOutVol;
        fadeInSource.volume = targetInVol;
        if (targetOutVol == 0) fadeOutSource.Pause();

        // 淡入完成后：强制确保淡入音源正在播放
        if (fadeInSource == normalBgmSource)
        {
            if (!normalBgmSource.isPlaying)
                normalBgmSource.UnPause();
        }
        else
        {
            if (!fadeInSource.isPlaying)
                fadeInSource.Play();
        }

        Debug.Log($"Fade complete: {fadeInSource.name} volume = {fadeInSource.volume}, isPlaying = {fadeInSource.isPlaying}");
    }
}