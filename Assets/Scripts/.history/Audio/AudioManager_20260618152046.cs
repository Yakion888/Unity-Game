using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

/// <summary>
/// 全局 BGM 管理器 —— Addressables 异步加载版
///
/// ═══════════════════════════════════════════════════════════
/// 【架构说明】
///
/// 旧版问题：直接 public AudioClip 硬引用 → 游戏启动时所有 BGM 瞬间
///          加载进内存，即使玩家全程不进入战斗也会占用战斗 BGM 的内存。
///
/// 新版方案：
///   1. Inspector 中使用 AssetReferenceT&lt;AudioClip&gt; 替代 AudioClip 直接引用。
///      这只存了一个"地址字符串"，不持有音频数据。
///   2. SetCombatState() 触发时，用 Addressables.LoadAssetAsync() 异步
///      加载目标音轨。加载期间旧 BGM 继续播放，玩家无感知。
///   3. 新音轨加载完成后执行交叉淡化（旧音轨淡出 + 新音轨淡入）。
///   4. 淡化完成后立刻调用 Addressables.Release() 释放旧音轨的内存句柄，
///      确保同一时刻只有一支 BGM 占用内存。
///   5. 使用切换序列号（_transitionSerial）防止快速连点导致的并发混乱。
///
/// 【双 AudioSource 设计】
///   - exploreSource：承载探索/常规 BGM
///   - combatSource：承载战斗 BGM
///   两个 AudioSource 轮流担任"活跃角色"和"静音角色"，通过交叉淡化
///   实现 0 卡顿的音乐过渡。
///
/// 【内存策略】
///   任意时刻最多只有 1 首 BGM 的 AudioClip 驻留内存。
///   进入战斗 → 加载战斗 BGM → 释放探索 BGM。
///   退出战斗 → 加载探索 BGM → 释放战斗 BGM。
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    // ============================================================
    // Inspector 可配置字段
    // ============================================================

    [Header("📁 Addressables 音频资源引用（仅存地址，不占内存）")]
    [Tooltip("探索 / 常规背景音乐 → 对应 Resources_moved/Audio/bgm/常驻bgm.mp3")]
    public AssetReferenceT<AudioClip> exploreBgmRef;

    [Tooltip("战斗背景音乐 → 对应 Resources_moved/Audio/bgm/战斗bgm.mp3")]
    public AssetReferenceT<AudioClip> combatBgmRef;

    [Tooltip("主菜单背景音乐 → 对应 Resources_moved/Audio/bgm/主菜单bgm.mp3")]
    public AssetReferenceT<AudioClip> mainMenuBgmRef;

    [Header("🔊 场景 AudioSource（请在 Inspector 中拖入）")]
    [Tooltip("探索 BGM 播放源 —— 场景中命名为 NormalBgmSource 的那个 AudioSource")]
    public AudioSource exploreSource;

    [Tooltip("战斗 BGM 播放源 —— 场景中命名为 CombatBgmSource 的那个 AudioSource")]
    public AudioSource combatSource;

    [Header("🎚 音量与渐变参数")]
    [Range(0f, 1f)]
    [Tooltip("BGM 最大音量（交叉淡化结束后的目标音量）")]
    public float maxBGMVolume = 0.5f;

    [Tooltip("交叉淡化持续时间（秒），数值越大过渡越平滑")]
    public float crossfadeDuration = 2.0f;

    // ============================================================
    // 内部状态 —— 内存句柄
    // ============================================================

    /// <summary>
    /// 探索 BGM 的 Addressables 加载句柄。
    /// 使用 Nullable 类型：null = 未加载 / 已释放；有值 = 已加载且有效。
    /// </summary>
    private AsyncOperationHandle<AudioClip>? _exploreHandle;

    /// <summary>战斗 BGM 的 Addressables 加载句柄</summary>
    private AsyncOperationHandle<AudioClip>? _combatHandle;

    /// <summary>主菜单 BGM 的 Addressables 加载句柄</summary>
    private AsyncOperationHandle<AudioClip>? _mainMenuHandle;

    // ============================================================
    // 内部状态 —— 播放状态
    // ============================================================

    /// <summary>当前是否正在播放战斗 BGM</summary>
    private bool _isInCombat;

    /// <summary>是否正在执行交叉淡化（防止用户狂按导致状态混乱）</summary>
    private bool _isCrossfading;

    /// <summary>
    /// 切换序列号 —— 每次调用 SetCombatState() 自增 1。
    /// 异步加载完成后比对序列号：如果不匹配说明在这期间状态又变了，
    /// 本次加载的结果应直接丢弃。
    /// </summary>
    private int _transitionSerial;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Awake()
    {
        // 单例
        Instance = this;

        if (exploreSource == null || combatSource == null)
        {
            Debug.LogError(
                "[AudioManager] 严重：Inspector 中 exploreSource 或 combatSource 未拖入！\n" +
                "请在场景中找到挂载此脚本的 GameObject，" +
                "把场景中的 NormalBgmSource 和 CombatBgmSource 分别拖入对应槽位。");
            return;
        }

        // ======================================================
        // 统一配置两个 AudioSource 的基础参数
        // - loop = true   → BGM 循环播放
        // - spatialBlend = 0 → 纯 2D 音频，不受角色位置影响
        // - playOnAwake = false → 由代码控制播放时机
        // ======================================================
        ConfigureAudioSource(exploreSource);
        ConfigureAudioSource(combatSource);

        // 初始静音 —— 等待 Start() 中异步加载完成后才播放
        exploreSource.volume = 0f;
        combatSource.volume = 0f;
    }

    private async void Start()
    {
        // 游戏启动：异步加载探索 BGM，加载完成后自动淡入播放
        await LoadAndPlayInitialBgmAsync();
    }

    private void OnDestroy()
    {
        // ══════════════════════════════════════════════════════
        // 【内存安全】释放所有尚未释放的 Addressables 句柄。
        // 如果不手动释放，切换场景时 Addressables 引用计数不会归零，
        // AudioClip 将永远无法被 GC 回收 → 内存泄漏。
        // ══════════════════════════════════════════════════════
        ReleaseHandle(ref _exploreHandle);
        ReleaseHandle(ref _combatHandle);
        ReleaseHandle(ref _mainMenuHandle);
    }

    // ============================================================
    // 公开 API
    // ============================================================

    /// <summary>
    /// 切换战斗 / 探索背景音乐。
    ///
    /// 典型调用场景：
    ///   - 刚发现敌人（进入战斗） → SetCombatState(true, forceRestart: true)
    ///   - 已经在战斗中（换目标） → SetCombatState(true, forceRestart: false)
    ///   - 战斗结束（退出战斗）    → SetCombatState(false)
    ///   - 玩家休息（强制回探索）  → SetCombatState(false)
    ///
    /// 此方法是 async void（fire-and-forget），调用方无需 await。
    /// 内部使用 _transitionSerial 防止快速连点导致的竞态条件。
    /// </summary>
    /// <param name="inCombat">true = 切换到战斗 BGM；false = 切换到探索 BGM</param>
    /// <param name="forceRestart">
    ///   仅 inCombat=true 时生效。
    ///   true  = 战斗 BGM 从头播放（time=0），用于"刚发现敌人"的新鲜感；
    ///   false = 从缓存进度继续（若已加载），用于已在战斗中换目标。
    /// </param>
    public async void SetCombatState(bool inCombat, bool forceRestart = false)
    {
        // 已处于目标状态 且 没有正在进行的交叉淡化 → 无需操作
        // 例外：forceRestart 强制重播时即使已在战斗也要执行
        if (_isInCombat == inCombat && !_isCrossfading && !forceRestart)
            return;

        // 更新状态标记
        _isInCombat = inCombat;

        // 递增序列号 —— 所有"在此之前发起的异步加载"都将被视为过期
        int mySerial = ++_transitionSerial;

        try
        {
            if (inCombat)
                await TransitionToCombatAsync(mySerial, forceRestart);
            else
                await TransitionToExploreAsync(mySerial);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[AudioManager] 切换 BGM 时发生异常：{ex.Message}\n" +
                $"调用栈：{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 切换到主菜单 BGM。
    /// 供 MainMenuController 在主菜单场景启动时调用。
    /// </summary>
    public async void PlayMainMenuBgm()
    {
        int mySerial = ++_transitionSerial;

        try
        {
            await TransitionToMainMenuAsync(mySerial);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AudioManager] 播放主菜单 BGM 失败：{ex.Message}");
        }
    }

    // ============================================================
    // 异步切换逻辑（核心流程）
    // ============================================================

    /// <summary>
    /// 【游戏启动专用】异步加载探索 BGM 并从静音淡入。
    ///
    /// 流程：
    ///   1. Addressables.LoadAssetAsync 异步加载 AudioClip
    ///   2. 赋给 exploreSource.clip
    ///   3. exploreSource.Play()
    ///   4. 从 0 音量渐变到 maxBGMVolume
    /// </summary>
    private async Task LoadAndPlayInitialBgmAsync()
    {
        // ---------- 第一步：查缓存或异步加载探索 BGM ----------
        AudioClip exploreClip = GetCachedBgmClip(_exploreHandle);
        if (exploreClip == null)
        {
            var (clip, handle) = await LoadBgmFromReferenceAsync(exploreBgmRef, "探索BGM");
            _exploreHandle = handle;
            exploreClip = clip;
        }

        if (exploreClip == null)
        {
            Debug.LogError("[AudioManager] 探索 BGM 加载失败 —— 游戏将无背景音乐！");
            return;
        }

        // ---------- 第二步：挂载到 AudioSource 并开始播放 ----------
        exploreSource.clip = exploreClip;
        exploreSource.volume = 0f;    // 从静音开始
        exploreSource.Play();         // 启动播放（音量为 0，听不到）

        // ---------- 第三步：平滑淡入到目标音量 ----------
        await CrossfadeAsync(
            fadeOutSource: null,          // 没有需要淡出的音源
            fadeInSource: exploreSource,  // 淡入探索音源
            targetOutVol: 0f,
            targetInVol: maxBGMVolume,
            duration: crossfadeDuration);

        //Debug.Log($"[AudioManager] 探索 BGM 已开始播放，音量 = {maxBGMVolume}");
    }

    /// <summary>
    /// 【进入战斗】加载战斗 BGM → 交叉淡化（探索→战斗）→ 释放探索内存。
    ///
    /// 流程图：
    ///   播放探索BGM ──┬→ 异步加载战斗BGM（探索继续播放，玩家无感知）
    ///                  ├→ 加载完成 → 战斗音源启动（音量0）→ 交叉淡化
    ///                  ├→ 淡化完成 → 探索音源暂停
    ///                  └→ Addressables.Release(探索句柄) ← 释放内存！
    /// </summary>
    /// <param name="serial">切换序列号，用于过期检测</param>
    /// <param name="forceRestart">true=战斗BGM从头播放；false=从缓存进度继续</param>
    private async Task TransitionToCombatAsync(int serial, bool forceRestart)
    {
        // ---------- 第一步：查缓存或异步加载战斗 BGM ----------
        // 注意：加载期间探索 BGM 继续播放，不会中断
        bool wasCached = true;
        AudioClip combatClip = GetCachedBgmClip(_combatHandle);
        if (combatClip == null)
        {
            wasCached = false;
            var (clip, handle) = await LoadBgmFromReferenceAsync(combatBgmRef, "战斗BGM");
            _combatHandle = handle;
            combatClip = clip;
        }

        // 加载完成时检查：我的切换请求是否已经"过期"？
        // 例如：加载期间玩家又退出了战斗，此时应放弃切入
        if (!IsSerialStillValid(serial)) return;
        if (combatClip == null) return;

        // ---------- 第二步：配置并启动战斗音源 ----------
        combatSource.clip = combatClip;
        combatSource.volume = 0f;          // 从静音开始

        // forceRestart 逻辑：
        //   - 新加载的 clip：天然从 0 开始，无需操作
        //   - 已缓存的 clip：需要手动拨回 0 秒，实现"从头播放"的冲击感
        if (forceRestart && wasCached)
            combatSource.time = 0f;

        if (!combatSource.isPlaying)
            combatSource.Play();           // 静音播放（准备交叉淡化）

        // ---------- 第三步：交叉淡化 ----------
        _isCrossfading = true;
        await CrossfadeAsync(
            fadeOutSource: exploreSource,  // 探索淡出
            fadeInSource: combatSource,    // 战斗淡入
            targetOutVol: 0f,
            targetInVol: maxBGMVolume,
            duration: crossfadeDuration);
        _isCrossfading = false;

        // 淡化完成后再次检查序列号
        if (!IsSerialStillValid(serial)) return;

        // ---------- 第四步：释放探索 BGM 的内存 ----------
        // 此时探索音源已静音 + 暂停，可以安全释放其 AudioClip
        ReleaseHandle(ref _exploreHandle);
        //Debug.Log("[AudioManager] ✅ 已切换到战斗 BGM，探索 BGM 内存已释放");
    }

    /// <summary>
    /// 【退出战斗】加载探索 BGM → 交叉淡化（战斗→探索）→ 释放战斗内存。
    ///
    /// 流程与 TransitionToCombatAsync 完全对称。
    /// </summary>
    private async Task TransitionToExploreAsync(int serial)
    {
        // ---------- 第一步：查缓存或异步加载探索 BGM（重新加载，之前已释放） ----------
        AudioClip exploreClip = GetCachedBgmClip(_exploreHandle);
        if (exploreClip == null)
        {
            var (clip, handle) = await LoadBgmFromReferenceAsync(exploreBgmRef, "探索BGM");
            _exploreHandle = handle;
            exploreClip = clip;
        }

        if (!IsSerialStillValid(serial)) return;
        if (exploreClip == null) return;

        // ---------- 第二步：配置并启动探索音源 ----------
        exploreSource.clip = exploreClip;
        exploreSource.volume = 0f;
        if (!exploreSource.isPlaying)
            exploreSource.Play();

        // ---------- 第三步：交叉淡化 ----------
        _isCrossfading = true;
        await CrossfadeAsync(
            fadeOutSource: combatSource,   // 战斗淡出
            fadeInSource: exploreSource,   // 探索淡入
            targetOutVol: 0f,
            targetInVol: maxBGMVolume,
            duration: crossfadeDuration);
        _isCrossfading = false;

        if (!IsSerialStillValid(serial)) return;

        // ---------- 第四步：释放战斗 BGM 的内存 ----------
        ReleaseHandle(ref _combatHandle);
        //Debug.Log("[AudioManager] ✅ 已切换到探索 BGM，战斗 BGM 内存已释放");
    }

    /// <summary>
    /// 【主菜单】加载主菜单 BGM → 交叉淡化 → 释放当前 BGM 内存。
    /// </summary>
    private async Task TransitionToMainMenuAsync(int serial)
    {
        AudioClip menuClip = GetCachedBgmClip(_mainMenuHandle);
        if (menuClip == null)
        {
            var (clip, handle) = await LoadBgmFromReferenceAsync(mainMenuBgmRef, "主菜单BGM");
            _mainMenuHandle = handle;
            menuClip = clip;
        }

        if (!IsSerialStillValid(serial)) return;
        if (menuClip == null) return;

        // 主菜单复用 exploreSource 播放
        exploreSource.clip = menuClip;
        exploreSource.volume = 0f;
        if (!exploreSource.isPlaying)
            exploreSource.Play();

        // 找出当前哪个音源在响 → 让它淡出
        AudioSource currentActive = _isInCombat ? combatSource : exploreSource;
        AudioSource fadeOutSource = (currentActive == exploreSource && exploreSource.clip != menuClip)
            ? exploreSource
            : combatSource;

        _isCrossfading = true;
        await CrossfadeAsync(
            fadeOutSource: fadeOutSource,
            fadeInSource: exploreSource,
            targetOutVol: 0f,
            targetInVol: maxBGMVolume,
            duration: crossfadeDuration);
        _isCrossfading = false;

        if (!IsSerialStillValid(serial)) return;

        // 释放所有非主菜单句柄
        ReleaseHandle(ref _exploreHandle);
        ReleaseHandle(ref _combatHandle);
        //Debug.Log("[AudioManager] ✅ 已切换到主菜单 BGM，其他 BGM 内存已释放");
    }

    // ============================================================
    // 底层工具方法
    // ============================================================

    /// <summary>
    /// 尝试从已加载的句柄中获取缓存的 AudioClip。
    /// 如果句柄有效（已加载且未释放），直接返回结果，不需要重新 IO。
    /// </summary>
    /// <returns>缓存的 AudioClip；如果未加载或已释放则返回 null</returns>
    private static AudioClip GetCachedBgmClip(AsyncOperationHandle<AudioClip>? handle)
    {
        if (handle.HasValue && handle.Value.IsValid())
            return handle.Value.Result;
        return null;
    }

    /// <summary>
    /// 异步加载一个 BGM 音轨（纯加载逻辑，不含缓存判断）。
    ///
    /// 注意：C# 不允许 async 方法使用 ref/out/in 参数（编译器错误 CS1988），
    /// 因此缓存逻辑被提取到 GetCachedBgmClip() 中，由调用方决定是否需要加载。
    ///
    /// 返回值是 C# 7 元组：
    ///   - clip:  加载成功的 AudioClip，失败为 null
    ///   - handle: 本次加载的 Addressables 句柄，失败为 null
    ///
    /// 调用方拿到 tuple 后应把 handle 赋值给对应的 _xxxHandle 字段，
    /// 以便后续通过 GetCachedBgmClip 复用，或通过 ReleaseHandle 释放。
    /// </summary>
    private async Task<(AudioClip clip, AsyncOperationHandle<AudioClip>? handle)>
        LoadBgmFromReferenceAsync(AssetReferenceT<AudioClip> reference, string debugName)
    {
        // ---- 校验：引用是否配置 ----
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            Debug.LogError(
                $"[AudioManager] {debugName} 的 AssetReference 未配置或 RuntimeKey 无效！\n" +
                "请在 Inspector 中将对应的 Addressables 资源拖入 AudioManager 的槽位。");
            return (null, null);
        }

        // ---- 开始异步加载 ----
        //Debug.Log($"[AudioManager] 开始异步加载 {debugName} …");
        AsyncOperationHandle<AudioClip> op = reference.LoadAssetAsync();

        try
        {
            // await op.Task 会挂起当前 async 方法（不阻塞主线程）
            // 直到 Addressables 完成资源加载（本地读取或远程下载）
            await op.Task;

            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                //Debug.Log($"[AudioManager] ✅ {debugName} 加载完成");
                return (op.Result, op);
            }
            else
            {
                Debug.LogError($"[AudioManager] {debugName} 加载状态异常：{op.Status}");
                Addressables.Release(op);
                return (null, null);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[AudioManager] ❌ {debugName} 异步加载失败！\n" +
                $"异常类型：{ex.GetType().Name}\n" +
                $"异常信息：{ex.Message}");
            Addressables.Release(op);
            return (null, null);
        }
    }

    /// <summary>
    /// 释放 Addressables 资源句柄。
    ///
    /// ⚠️ 极度重要：
    ///   必须使用 Addressables.Release(handle) 而不是直接 handle.Dispose()！
    ///
    ///   原因：Addressables 内部使用引用计数。LoadAssetAsync 会让引用计数 +1，
    ///   只有调用 Addressables.Release() 才能让计数 -1。当计数归零时，
    ///   Addressables 才会真正卸载 AssetBundle 并释放内存。
    ///
    ///   如果用 handle.Dispose() 强行释放，引用计数不会归零 → 内存泄漏。
    /// </summary>
    private void ReleaseHandle(ref AsyncOperationHandle<AudioClip>? handle)
    {
        if (handle.HasValue && handle.Value.IsValid())
        {
            Addressables.Release(handle.Value);
            handle = null; // 清空标记，防止二次释放
        }
    }

    /// <summary>
    /// 判断切换序列号是否仍然有效。
    ///
    /// 如果 serial != _transitionSerial，说明在"await 异步加载"期间，
    /// 外部又调用了 SetCombatState()，产生了更新的切换请求。
    /// 此时当前请求应自我废止，避免：
    ///   - A 请求的加载结果覆盖 B 请求的目标状态
    ///   - 多个交叉淡化同时运行导致音量错乱
    /// </summary>
    private bool IsSerialStillValid(int serial)
    {
        if (serial != _transitionSerial)
        {
            //Debug.Log($"[AudioManager] 切换请求 #{serial} 已过期（当前序列号 = {_transitionSerial}），丢弃");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 交叉淡化（async Task → 内部启动协程）。
    ///
    /// 为什么不用纯 async/await + Task.Delay？
    ///   因为 Task.Delay 回调可能不在 Unity 主线程，而 AudioSource.volume
    ///   等 Unity API 必须在主线程调用 → 会导致崩溃或静默失败。
    ///
    /// 这里的做法：用 TaskCompletionSource + StartCoroutine 将协程
    /// "包装"成可 await 的 Task。协程在 Unity 主线程逐帧执行 → 安全。
    /// </summary>
    /// <param name="fadeOutSource">淡出的 AudioSource（可为 null）</param>
    /// <param name="fadeInSource">淡入的 AudioSource（可为 null）</param>
    /// <param name="targetOutVol">淡出目标音量</param>
    /// <param name="targetInVol">淡入目标音量</param>
    /// <param name="duration">淡化持续时间（秒）</param>
    private async Task CrossfadeAsync(
        AudioSource fadeOutSource,
        AudioSource fadeInSource,
        float targetOutVol,
        float targetInVol,
        float duration)
    {
        // 创建 TaskCompletionSource —— 协程完成时通过它通知 async 方法继续
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(CrossfadeCoroutine(
            fadeOutSource, fadeInSource, targetOutVol, targetInVol, duration, tcs));
        await tcs.Task; // 挂起 async 方法，等待协程完成
    }

    /// <summary>
    /// 交叉淡化的协程实现（每帧更新一次音量）。
    ///
    /// 原理：
    ///   fadeOutSource.volume: 当前值 → targetOutVol（通常是 0）
    ///   fadeInSource.volume:  当前值 → targetInVol（通常是 maxBGMVolume）
    ///   两路音量同步渐变 → 听感上是无缝过渡。
    ///
    /// 完成后：
    ///   - 淡出音源如果音量降到了 0 → 暂停播放，节省 CPU
    /// </summary>
    private System.Collections.IEnumerator CrossfadeCoroutine(
        AudioSource fadeOutSource,
        AudioSource fadeInSource,
        float targetOutVol,
        float targetInVol,
        float duration,
        TaskCompletionSource<bool> tcs)
    {
        // 记录渐变开始时的音量（可能不为 0，例如上一次淡化被中断）
        float startOutVol = fadeOutSource != null ? fadeOutSource.volume : 0f;
        float startInVol = fadeInSource != null ? fadeInSource.volume : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 0 → 1 的进度

            // 逐帧更新两个音源的音量
            if (fadeOutSource != null)
                fadeOutSource.volume = Mathf.Lerp(startOutVol, targetOutVol, t);
            if (fadeInSource != null)
                fadeInSource.volume = Mathf.Lerp(startInVol, targetInVol, t);

            yield return null; // 等待下一帧
        }

        // ---- 渐変完成，锁定精确的最终值 ----
        if (fadeOutSource != null)
        {
            fadeOutSource.volume = targetOutVol;
            // 音量降到 0 → 暂停 AudioSource，节省 CPU（不再处理无声的音频数据）
            if (Mathf.Approximately(targetOutVol, 0f))
                fadeOutSource.Pause();
        }

        if (fadeInSource != null)
        {
            fadeInSource.volume = targetInVol;
        }

        // 通知 async 方法：交叉淡化完成
        tcs.TrySetResult(true);
    }

    // ============================================================
    // 私有辅助
    // ============================================================

    /// <summary>统一配置一个 AudioSource 的基础参数</summary>
    private void ConfigureAudioSource(AudioSource source)
    {
        if (source == null) return;

        source.loop = true;           // BGM 循环播放
        source.spatialBlend = 0f;     // 纯 2D：音量不受角色与音源距离影响
        source.playOnAwake = false;   // 不由场景启动触发，全部交由代码控制
        source.dopplerLevel = 0f;     // 关闭多普勒效应（BGM 不需要）
        source.priority = 0;          // 最高优先级（0 = 最高，不会被挤掉）
    }
}
