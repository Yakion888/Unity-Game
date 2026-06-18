using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance;

    // ───────────────────────────────────────
    // 【GC 优化】字典中嵌套队列的默认容量。
    // 每个特效种类首次创建 Queue<GameObject> 时传入此值，
    // 避免多次 ×2 数组扩容产生的废弃数组。
    // 20 是合理预估值：覆盖大多数特效的并发上限（如火球、闪电链等）。
    // ───────────────────────────────────────
    private const int DefaultVFXQueueCapacity = 20;

    // ───────────────────────────────────────
    // 【GC 优化】仅声明，不隐式实例化。
    // Awake 中统一初始化（虽然 Dictionary 的扩容频次远低于 Queue，
    // 但遵循"初始化必传容量"原则，保持一致性）。
    // ───────────────────────────────────────
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        Instance = this;

        // ── GC 优化：传入合理初始容量，减少 Dictionary 内部 buckets 重分配 ──
        // 项目中常见的特效种类（打击、闪电、技能波等）约 5~8 种，传入 8。
        poolDictionary = new Dictionary<string, Queue<GameObject>>(capacity: 8);
    }

    /// <summary>
    /// 从池中获取（或创建）特效，代替 Instantiate。
    /// </summary>
    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        // 首次遇到该特效 → 创建专属队列，并传入预估容量
        if (!poolDictionary.ContainsKey(key))
        {
            // ── GC 优化：传入 DefaultVFXQueueCapacity，底层数组一次分配到位 ──
            poolDictionary[key] = new Queue<GameObject>(DefaultVFXQueueCapacity);
        }

        GameObject objToSpawn = null;

        if (poolDictionary[key].Count > 0)
        {
            // 池中有闲置 → 复用
            objToSpawn = poolDictionary[key].Dequeue();
            objToSpawn.transform.position = position;
            objToSpawn.transform.rotation = rotation;
            objToSpawn.transform.SetParent(null);
            objToSpawn.SetActive(true);
        }
        else
        {
            // 池空 → 实例化新对象
            objToSpawn = Instantiate(prefab, position, rotation);
            objToSpawn.name = key; // 抹除 "(Clone)" 后缀
            objToSpawn.transform.SetParent(null);
        }

        return objToSpawn;
    }

    /// <summary>
    /// 延时回收特效到池中（代替 Destroy）。
    /// </summary>
    public void ReturnToPool(GameObject obj, float delay)
    {
        StartCoroutine(ReturnRoutine(obj, delay));
    }

    private IEnumerator ReturnRoutine(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null) yield break;

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (!poolDictionary.ContainsKey(obj.name))
        {
            // ── GC 优化：传入预估容量 ──
            poolDictionary[obj.name] = new Queue<GameObject>(DefaultVFXQueueCapacity);
        }

        // 安全锁：防止同一 GameObject 被双重回收（Double Free）
        if (!poolDictionary[obj.name].Contains(obj))
        {
            poolDictionary[obj.name].Enqueue(obj);
        }
    }
}
