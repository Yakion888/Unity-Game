using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 武器运行时资产容器 —— 异步加载 + 统一释放
///
/// ═══════════════════════════════════════════════════════════
/// 【职责】
///   1. 接收 WeaponDataSO（仅含 AssetReference 地址），异步加载全部资源
///   2. 持有所有 AsyncOperationHandle，供释放时统一 Addressables.Release
///   3. 暴露已加载的 GameObject[] / AudioClip[] 等字段，供 PlayerMove 代理属性读取
///
/// 【加载顺序】
///   LoadAsync(so) → 并行加载模型、特效、音效 → 全部完成后返回
///
/// 【释放】
///   ReleaseAll() → 遍历 _allHandles → Addressables.Release → 清空
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class WeaponRuntimeAssets
{
    // ============================================================
    // 已加载的资产（供 PlayerMove 代理属性读取）
    // ============================================================

    public GameObject weaponModelPrefab;

    public GameObject[] heavyAttackEffects;
    public GameObject[] heavyAttackHitEffects;
    public GameObject[] lightAttackEffects;
    public GameObject runningAttackEffect;

    public AudioClip[] heavySwingSounds;
    public AudioClip[] heavyHitSounds;
    public AudioClip[] heavyVoices;
    public AudioClip[] lightSwingSounds;
    public AudioClip[] lightHitSounds;
    public AudioClip[] lightVoices;
    public AudioClip slidingWhooshSound;
    public AudioClip[] runningVoices;

    // ============================================================
    // 内部状态
    // ============================================================

    /// <summary>所有异步加载句柄，ReleaseAll 时统一释放</summary>
    private readonly List<AsyncOperationHandle> _allHandles = new List<AsyncOperationHandle>();

    /// <summary>是否已释放</summary>
    private bool _released;

    // ============================================================
    // 异步加载
    // ============================================================

    /// <summary>
    /// 从 WeaponDataSO 异步加载全部运行时资产。
    /// 返回 this，调用方通过返回的实例访问已加载资产。
    /// </summary>
    public async Task<WeaponRuntimeAssets> LoadAsync(WeaponDataSO so)
    {
        if (so == null) return this;

        // ── 并行加载所有单项 ──
        var tasks = new List<Task>();

        tasks.Add(LoadSingleRef(so.weaponModelRef, clip => weaponModelPrefab = clip as GameObject));

        tasks.Add(LoadRefArray(so.heavyAttackEffectRefs, arr => heavyAttackEffects = arr));
        tasks.Add(LoadRefArray(so.heavyHitEffectRefs, arr => heavyAttackHitEffects = arr));
        tasks.Add(LoadRefArray(so.lightAttackEffectRefs, arr => lightAttackEffects = arr));
        tasks.Add(LoadSingleRef(so.runningAttackEffectRef, obj => runningAttackEffect = obj as GameObject));

        tasks.Add(LoadAudioRefArray(so.heavySwingSoundRefs, arr => heavySwingSounds = arr));
        tasks.Add(LoadAudioRefArray(so.heavyHitSoundRefs, arr => heavyHitSounds = arr));
        tasks.Add(LoadAudioRefArray(so.heavyVoiceRefs, arr => heavyVoices = arr));
        tasks.Add(LoadAudioRefArray(so.lightSwingSoundRefs, arr => lightSwingSounds = arr));
        tasks.Add(LoadAudioRefArray(so.lightHitSoundRefs, arr => lightHitSounds = arr));
        tasks.Add(LoadAudioRefArray(so.lightVoiceRefs, arr => lightVoices = arr));
        tasks.Add(LoadSingleAudioRef(so.slidingWhooshSoundRef, clip => slidingWhooshSound = clip));
        tasks.Add(LoadAudioRefArray(so.runningVoiceRefs, arr => runningVoices = arr));

        await Task.WhenAll(tasks);
        return this;
    }

    // ============================================================
    // 释放
    // ============================================================

    /// <summary>
    /// 释放所有 Addressables 句柄，清空资产引用。
    /// 调用时机：切武器 / 场景卸载。
    /// </summary>
    public void ReleaseAll()
    {
        if (_released) return;
        _released = true;

        foreach (var handle in _allHandles)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        _allHandles.Clear();

        // 清空所有引用，帮助 GC
        weaponModelPrefab = null;
        heavyAttackEffects = null;
        heavyAttackHitEffects = null;
        lightAttackEffects = null;
        runningAttackEffect = null;
        heavySwingSounds = null;
        heavyHitSounds = null;
        heavyVoices = null;
        lightSwingSounds = null;
        lightHitSounds = null;
        lightVoices = null;
        slidingWhooshSound = null;
        runningVoices = null;
    }

    // ============================================================
    // 内部工具
    // 关键：使用 Addressables.LoadAssetAsync<T>(key) 而非
    //       AssetReference.LoadAssetAsync()。
    //       后者会在 AssetReference 实例上缓存内部 handle，
    //       第二次调用时冲突 → "already been loaded" 错误。
    //       RuntimeKey 直传绕过缓存，每次创建独立 handle。
    // ============================================================

    private async Task LoadSingleRef(AssetReferenceGameObject reference, System.Action<Object> onLoaded)
    {
        if (reference == null || !reference.RuntimeKeyIsValid()) return;

        var handle = Addressables.LoadAssetAsync<GameObject>(reference.RuntimeKey);

        await System.Threading.Tasks.Task.Delay(UnityEngine.Random.Range(500, 2500)); 

        lock (_allHandles) _allHandles.Add(handle);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            onLoaded?.Invoke(handle.Result);
    }

    private async Task LoadSingleAudioRef(AssetReferenceT<AudioClip> reference, System.Action<AudioClip> onLoaded)
    {
        if (reference == null || !reference.RuntimeKeyIsValid()) return;

        var handle = Addressables.LoadAssetAsync<AudioClip>(reference.RuntimeKey);
        lock (_allHandles) _allHandles.Add(handle);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            onLoaded?.Invoke(handle.Result);
    }

    private async Task LoadRefArray(AssetReferenceGameObject[] refs, System.Action<GameObject[]> onLoaded)
    {
        if (refs == null || refs.Length == 0) return;

        var results = new GameObject[refs.Length];
        var subTasks = new List<Task>();

        for (int i = 0; i < refs.Length; i++)
        {
            int index = i;
            var reference = refs[i];
            if (reference == null || !reference.RuntimeKeyIsValid()) continue;

            var handle = Addressables.LoadAssetAsync<GameObject>(reference.RuntimeKey);
            lock (_allHandles) _allHandles.Add(handle);

            subTasks.Add(handle.Task.ContinueWith(_ =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    results[index] = handle.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext()));
        }

        await Task.WhenAll(subTasks);
        onLoaded?.Invoke(results);
    }

    private async Task LoadAudioRefArray(AssetReferenceT<AudioClip>[] refs, System.Action<AudioClip[]> onLoaded)
    {
        if (refs == null || refs.Length == 0) return;

        var results = new AudioClip[refs.Length];
        var subTasks = new List<Task>();

        for (int i = 0; i < refs.Length; i++)
        {
            int index = i;
            var reference = refs[i];
            if (reference == null || !reference.RuntimeKeyIsValid()) continue;

            var handle = Addressables.LoadAssetAsync<AudioClip>(reference.RuntimeKey);
            lock (_allHandles) _allHandles.Add(handle);

            subTasks.Add(handle.Task.ContinueWith(_ =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    results[index] = handle.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext()));
        }

        await Task.WhenAll(subTasks);
        onLoaded?.Invoke(results);
    }
}
