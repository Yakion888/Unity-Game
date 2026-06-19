using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// VFX 对象池管理器 —— Addressables 预热加载版
///
/// ═══════════════════════════════════════════════════════════
/// 【预热机制】
///   在 Inspector 中拖入需要预热的 VFX AssetReference 列表，
///   Start() 时异步加载所有特效预制体到内存，然后每种预实例化
///   preloadCountPerType 个实例入池。后续 SpawnFromPool 命中
///   池子 → 零 Instantiate 开销。
///
/// 【向后兼容】
///   现有调用方依然使用 SpawnFromPool(GameObject prefab, ...)，
///   预加载的实例名与 prefab.name 一致，自动从池子复用。
///   未预加载的特效照旧走 Instantiate 即时生成。
///
/// 【内存安全】
///   OnDestroy 中遍历所有 AsyncOperationHandle，逐一
///   Addressables.Release，确保 AssetBundle 引用计数归零。
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance;

    // ============================================================
    // Inspector
    // ============================================================

    [Header("📁 Addressables 预热列表")]
    [Tooltip("每种特效单独配置预制体和预生成数量")]
    public VFXPreloadEntry[] preloadEntries;

    /// <summary>单条预热配置：特效预制体 + 预生成数量</summary>
    [System.Serializable]
    public struct VFXPreloadEntry
    {
        [Tooltip("特效预制体的 Addressables 引用")]
        public AssetReferenceGameObject assetRef;
        [Tooltip("预实例化数量（高频 3~5，低频 1~2，一次性 1）")]
        public int preloadCount;
    }

    // ============================================================
    // 内部状态
    // ============================================================

    /// <summary>池子：键=特效名，值=闲置实例队列</summary>
    private Dictionary<string, Queue<GameObject>> _pool;

    /// <summary>预加载的预制体引用：键=特效名，值=已加载的 GameObject 模板</summary>
    private Dictionary<string, GameObject> _loadedPrefabs;

    /// <summary>所有 Addressables 加载句柄，OnDestroy 统一释放</summary>
    private readonly List<AsyncOperationHandle<GameObject>> _handles =
        new List<AsyncOperationHandle<GameObject>>();

    /// <summary>预热是否完成（完成前 SpawnFromPool 走旧逻辑即时实例化）</summary>
    private bool _warmupComplete;

    private const int DefaultQueueCapacity = 30;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Awake()
    {
        Instance = this;
        _pool = new Dictionary<string, Queue<GameObject>>(capacity: 8);
        _loadedPrefabs = new Dictionary<string, GameObject>(capacity: 8);
    }

    private async void Start()
    {
        await WarmupAsync();
    }

    private void OnDestroy()
    {
        // ══════════════════════════════════════════════════════
        // 【内存安全】释放所有 Addressables 异步加载句柄。
        // 逐一 Release 而非直接清空列表：确保引用计数正确递减。
        // ══════════════════════════════════════════════════════
        foreach (var handle in _handles)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        _handles.Clear();
        _loadedPrefabs.Clear();
    }

    // ============================================================
    // 预热加载
    // ============================================================

    /// <summary>
    /// 异步加载 preloadRefs 中所有特效预制体 → 每种预实例化 preloadCountPerType 个入池。
    /// </summary>
    private async System.Threading.Tasks.Task WarmupAsync()
    {
        if (preloadEntries == null || preloadEntries.Length == 0)
        {
            _warmupComplete = true;
            return;
        }

        Debug.Log($"[VFXPoolManager] 开始预热加载 {preloadEntries.Length} 种特效…");

        foreach (var entry in preloadEntries)
        {
            if (entry.assetRef == null || !entry.assetRef.RuntimeKeyIsValid()) continue;

            int count = Mathf.Max(1, entry.preloadCount); // 至少 1 个

            // ── 异步加载预制体 ──
            var handle = entry.assetRef.LoadAssetAsync();
            _handles.Add(handle);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[VFXPoolManager] 预热失败：{entry.assetRef}");
                continue;
            }

            GameObject prefab = handle.Result;
            string key = prefab.name;
            _loadedPrefabs[key] = prefab;

            // ── 预实例化入池 ──
            if (!_pool.ContainsKey(key))
                _pool[key] = new Queue<GameObject>(DefaultQueueCapacity);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.name = key;
                instance.SetActive(false);
                _pool[key].Enqueue(instance);
            }

            // 每种特效预实例化后让 1ms，把 Instantiate 的 CPU/GC 压力分散到多帧
            await System.Threading.Tasks.Task.Delay(1);
        }

        _warmupComplete = true;
        Debug.Log($"[VFXPoolManager] 预热完成，共加载 {_loadedPrefabs.Count} 种特效");
    }

    // ============================================================
    // 公开 API（向后兼容）
    // ============================================================

    /// <summary>
    /// 从池中获取（或即时创建）特效实例。
    /// 预加载的特效会优先从池子复用；未预加载的照旧 Instantiate。
    /// </summary>
    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        string key = prefab.name;

        // 确保队列存在
        if (!_pool.ContainsKey(key))
            _pool[key] = new Queue<GameObject>(DefaultQueueCapacity);

        GameObject objToSpawn;

        if (_pool[key].Count > 0)
        {
            // ── 池中有闲置 → 复用 ──
            objToSpawn = _pool[key].Dequeue();
            objToSpawn.transform.SetParent(null);
            objToSpawn.transform.position = position;
            objToSpawn.transform.rotation = rotation;
            objToSpawn.SetActive(true);
        }
        else
        {
            // ── 池空 → 即时实例化（若已预热则用已加载的预制体，否则用传入的引用） ──
            GameObject template = _loadedPrefabs.ContainsKey(key) ? _loadedPrefabs[key] : prefab;
            objToSpawn = Instantiate(template, position, rotation);
            objToSpawn.name = key;
        }

        return objToSpawn;
    }

    /// <summary>
    /// 延迟回收特效到池子（代替 Destroy）。
    /// </summary>
    public void ReturnToPool(GameObject obj, float delay)
    {
        StartCoroutine(ReturnRoutine(obj, delay));
    }

    // ============================================================
    // 内部
    // ============================================================

    private IEnumerator ReturnRoutine(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj == null) yield break;

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        string key = obj.name;
        if (!_pool.ContainsKey(key))
            _pool[key] = new Queue<GameObject>(DefaultQueueCapacity);

        if (!_pool[key].Contains(obj))
            _pool[key].Enqueue(obj);
    }
}
