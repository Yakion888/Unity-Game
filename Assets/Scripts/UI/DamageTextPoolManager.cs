using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

/// <summary>
/// 伤害漂字对象池管理器 —— Addressables 预热加载版
///
/// ═══════════════════════════════════════════════════════════
/// 【预热流程】
///   Start() → LoadAssetAsync 异步加载漂字预制体 →
///   加载完成后 for i<initialPoolSize Instantiate 入池 →
///   后续 ShowDamageText 直接从池子 Dequeue，零 Instantiate 开销。
///
/// 【内存安全】
///   OnDestroy 中 Addressables.Release(_prefabHandle) 释放
///   预制体 AssetBundle 引用计数。
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class DamageTextPoolManager : MonoBehaviour
{
    public static DamageTextPoolManager Instance;

    // ============================================================
    // Inspector
    // ============================================================

    [Header("📁 Addressables 预制体引用")]
    [Tooltip("拖入伤害漂字预制体的 Addressables 引用")]
    public AssetReferenceGameObject damageTextPrefabRef;

    [Header("配置")]
    [Tooltip("漂字挂载的 Canvas（World Space 或 Overlay）")]
    public Transform canvasTransform;

    [Tooltip("初始预生成数量")]
    public int initialPoolSize = 20;

    // ============================================================
    // 内部状态
    // ============================================================

    /// <summary>池子：GC 优化 —— 构造时传入容量，零扩容</summary>
    private Queue<GameObject> _textPool;

    /// <summary>Addressables 加载句柄，OnDestroy 释放</summary>
    private AsyncOperationHandle<GameObject> _prefabHandle;

    /// <summary>已加载的漂字预制体（加载完成后赋值，避免每次 Instantiate 前重新 Load）</summary>
    private GameObject _loadedPrefab;

    /// <summary>预热是否已完成（完成前 ShowDamageText 等待）</summary>
    private bool _warmupComplete;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Awake()
    {
        Instance = this;
        _textPool = new Queue<GameObject>(initialPoolSize);
    }

    private async void Start()
    {
        await WarmupAsync();
    }

    private void OnDestroy()
    {
        // 释放 Addressables 加载的预制体
        if (_prefabHandle.IsValid())
            Addressables.Release(_prefabHandle);
    }

    // ============================================================
    // 预热加载
    // ============================================================

    /// <summary>
    /// 异步加载漂字预制体 → 预实例化 initialPoolSize 个 → 入池。
    /// </summary>
    private async System.Threading.Tasks.Task WarmupAsync()
    {
        if (damageTextPrefabRef == null || !damageTextPrefabRef.RuntimeKeyIsValid())
        {
            Debug.LogError("[DamageTextPoolManager] damageTextPrefabRef 未配置！请在 Inspector 中拖入 Addressables 预制体引用。");
            return;
        }

        Debug.Log("[DamageTextPoolManager] 开始预热加载漂字预制体…");

        _prefabHandle = damageTextPrefabRef.LoadAssetAsync();
        await _prefabHandle.Task;

        if (_prefabHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"[DamageTextPoolManager] 漂字预制体加载失败：{_prefabHandle.Status}");
            return;
        }

        _loadedPrefab = _prefabHandle.Result;

        // ── 预实例化入池（每 5 个让一帧，避免单帧 20 次 Instantiate 的 GC 峰值） ──
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewText();
            if ((i + 1) % 5 == 0)
                await System.Threading.Tasks.Task.Delay(1);
        }

        _warmupComplete = true;
        Debug.Log($"[DamageTextPoolManager] 预热完成，预生成 {initialPoolSize} 个漂字");
    }

    // ============================================================
    // 公开 API
    // ============================================================

    /// <summary>
    /// 显示伤害漂字。池中复用优先；池空则新建（预热完成后用已加载的预制体）。
    /// </summary>
    public void ShowDamageText(Vector3 spawnPosition, int damage, int damageType)
    {
        // 预热未完成时丢弃请求（极罕见：场景加载后 0.1s 内就有伤害）
        if (!_warmupComplete) return;

        GameObject textObj;
        if (_textPool.Count > 0)
            textObj = _textPool.Dequeue();
        else
            textObj = CreateNewText();

        textObj.SetActive(true);
        textObj.transform.position = spawnPosition;

        DamageTextItem item = textObj.GetComponent<DamageTextItem>();
        if (item != null)
            item.Setup(damage, damageType);
    }

    /// <summary>
    /// 回收漂字到池子（由 DamageTextItem 动画结束时调用）。
    /// </summary>
    public void ReturnToPool(GameObject textObj)
    {
        textObj.SetActive(false);
        textObj.transform.SetParent(canvasTransform);
        _textPool.Enqueue(textObj);
    }

    // ============================================================
    // 内部
    // ============================================================

    private GameObject CreateNewText()
    {
        if (_loadedPrefab == null)
        {
            Debug.LogError("[DamageTextPoolManager] 预制体未加载完成，无法创建漂字！");
            return null;
        }

        GameObject textObj = Instantiate(_loadedPrefab, canvasTransform);
        textObj.SetActive(false);
        _textPool.Enqueue(textObj);
        return textObj;
    }
}
