using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 全局 UI 调度中枢 —— Addressables 异步实例化版
///
/// ═══════════════════════════════════════════════════════════
/// 【架构说明】
///
/// 旧版问题：使用 Resources.Load 同步加载 UI 预制体。
///   - Resources 目录下的所有资源无论用不用都会打进包体
///   - Resources.Load 是同步 IO 操作，会卡主线程
///   - Destroy 后 Resources 资源无法卸载，内存持续占用
///
/// 新版方案：
///   1. Inspector 中用 AssetReferenceGameObject 暴露预制体槽位（不占内存）
///   2. 角色面板（CharacterPanel）：按需异步实例化，关闭时 ReleaseInstance 彻底销毁
///   3. QTE 面板：首次异步加载后 SetActive(false) 永久缓存，后续秒开不复载
///   4. 连点防抖：加载中标记位阻止重复实例化
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class SystemUIManager : MonoBehaviour
{
    public static SystemUIManager Instance;

    // ============================================================
    // Inspector
    // ============================================================

    [Header("玩家引用与挂载点")]
    public EldenRingMovement player;
    [Tooltip("UI 的根 Canvas Transform（预制体会作为其子级实例化）")]
    public Transform mainCanvas;

    [Header("📁 Addressables 预制体引用")]
    [Tooltip("角色属性面板预制体")]
    public AssetReferenceGameObject characterPanelRef;

    [Tooltip("QTE 战斗面板预制体")]
    public AssetReferenceGameObject qtePanelRef;

    // ============================================================
    // 内部状态 —— 角色面板（按需加载 + 用完销毁）
    // ============================================================

    /// <summary>
    /// 当前角色面板实例。null 表示未打开或已销毁。
    /// </summary>
    private GameObject _characterPanelInstance;

    /// <summary>
    /// 是否正在异步加载角色面板。
    /// 用于连点防抖 —— 加载期间忽略 C 键。
    /// </summary>
    private bool _isLoadingCharacterPanel;

    // ============================================================
    // 内部状态 —— QTE 面板（懒加载 + 永久缓存）
    // ============================================================

    /// <summary>
    /// 缓存的 QTE 面板组件引用。
    /// 首次加载后永久持有，仅 SetActive(false) 隐藏，绝不 Release。
    /// </summary>
    private QTEUIManager _cachedQTEPanel;

    /// <summary>
    /// 是否正在异步加载 QTE 面板。
    /// </summary>
    private bool _isLoadingQTE;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (player == null || player.isDead || player.isResting)
            return;

        if (Input.GetKeyDown(KeyCode.C))
            ToggleCharacterPanel();
    }

    private void OnDestroy()
    {
        // ══════════════════════════════════════════════════════
        // 【内存安全】释放 Addressables 实例，防止场景卸载后
        // GameObject 残留 + 底层 AssetBundle 引用计数不归零。
        // ══════════════════════════════════════════════════════

        // 释放角色面板（如果正开着）
        if (_characterPanelInstance != null)
        {
            Addressables.ReleaseInstance(_characterPanelInstance);
            _characterPanelInstance = null;
        }

        // 释放缓存的 QTE 面板（如果已加载过）
        if (_cachedQTEPanel != null)
        {
            Addressables.ReleaseInstance(_cachedQTEPanel.gameObject);
            _cachedQTEPanel = null;
        }
    }

    // ============================================================
    // 1. 角色属性面板 —— 异步实例化 + 用完彻底销毁
    // ============================================================

    /// <summary>
    /// 按下 C 键时切换角色属性面板的打开/关闭。
    ///
    /// 打开：
    ///   1. 检查是否已在加载中（防抖）
    ///   2. characterPanelRef.InstantiateAsync(mainCanvas) 异步实例化
    ///   3. 完成后获取 CharacterPanelUI 组件并 Initialize(player)
    ///
    /// 关闭：
    ///   1. Addressables.ReleaseInstance 销毁 GameObject + 释放底层内存
    ///   2. 下次打开时重新加载（不占用常驻内存）
    /// </summary>
    public async void ToggleCharacterPanel()
    {
        // ── 连点防抖：正在加载中，忽略本轮按键 ──
        if (_isLoadingCharacterPanel)
        {
            Debug.Log("[SystemUIManager] 角色面板正在加载中，已忽略重复按键");
            return;
        }

        // ── 关闭面板 ──
        if (_characterPanelInstance != null)
        {
            // Addressables.ReleaseInstance 会：
            //   1. Destroy 这个 GameObject（包括所有子级）
            //   2. 减少 Addressables 底层 AssetBundle 的引用计数
            //   3. 引用计数归零时自动卸载 AssetBundle → 释放内存
            Addressables.ReleaseInstance(_characterPanelInstance);
            _characterPanelInstance = null;
            player.isUIOpen = false;
            return;
        }

        // ── 打开面板：异步加载 ──
        _isLoadingCharacterPanel = true;

        try
        {
            if (characterPanelRef == null || !characterPanelRef.RuntimeKeyIsValid())
            {
                Debug.LogError(
                    "[SystemUIManager] characterPanelRef 未配置或无效！\n" +
                    "请在 Inspector 中将对应 Addressables 预制体拖入槽位。");
                return;
            }

            // InstantiateAsync 返回 AsyncOperationHandle<GameObject>
            // await 其 Task 属性可以在不阻塞主线程的前提下等待加载完成
            AsyncOperationHandle<GameObject> handle =
                characterPanelRef.InstantiateAsync(mainCanvas);

            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _characterPanelInstance = handle.Result;

                // 初始化面板数据
                CharacterPanelUI panelScript =
                    _characterPanelInstance.GetComponent<CharacterPanelUI>();
                if (panelScript != null)
                {
                    panelScript.Initialize(player);
                }

                player.isUIOpen = true;
                Debug.Log("[SystemUIManager] ✅ 角色面板加载完成");
            }
            else
            {
                Debug.LogError(
                    $"[SystemUIManager] 角色面板加载失败！状态：{handle.Status}\n" +
                    $"异常：{handle.OperationException}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[SystemUIManager] 角色面板加载异常！\n" +
                $"类型：{ex.GetType().Name}\n信息：{ex.Message}");
        }
        finally
        {
            _isLoadingCharacterPanel = false;
        }
    }

    // ============================================================
    // 2. QTE 战斗面板 —— 懒加载 + 永久缓存（0 延迟秒开）
    // ============================================================

    /// <summary>
    /// 显示 QTE 战斗面板。
    ///
    /// 懒加载缓存模式：
    ///   - 首次调用：异步实例化 → 缓存 QTEUIManager 组件引用 → 显示
    ///   - 后续调用：直接 cachedQTEPanel.ShowQTE() → SetActive(true)，0 延迟
    ///
    /// 为什么不用"用完销毁"模式？
    ///   QTE 面板在 Boss 战中可能频繁开关（每次大招都要用），
    ///   反复 Load/Release 会导致：
    ///     1. 每次都有异步延迟（玩家感到卡顿）
    ///     2. 反复加载/卸载 AssetBundle（IO 压力 + 内存碎片）
    ///   永久缓存用少量常驻内存换取战斗中的 0 延迟体验。
    /// </summary>
    public async void ShowQTE()
    {
        // ── 缓存命中：直接唤醒，0 帧延迟 ──
        if (_cachedQTEPanel != null)
        {
            _cachedQTEPanel.ShowQTE();
            return;
        }

        // ── 连点防抖：正在加载中 ──
        if (_isLoadingQTE)
        {
            Debug.Log("[SystemUIManager] QTE 面板正在加载中，已忽略重复调用");
            return;
        }

        // ── 首次加载 ──
        _isLoadingQTE = true;

        try
        {
            if (qtePanelRef == null || !qtePanelRef.RuntimeKeyIsValid())
            {
                Debug.LogError(
                    "[SystemUIManager] qtePanelRef 未配置或无效！\n" +
                    "请在 Inspector 中将对应 Addressables 预制体拖入槽位。");
                return;
            }

            Debug.Log("[SystemUIManager] 首次加载 QTE 面板（仅此一次）…");
            AsyncOperationHandle<GameObject> handle =
                qtePanelRef.InstantiateAsync(mainCanvas);

            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject obj = handle.Result;
                _cachedQTEPanel = obj.GetComponent<QTEUIManager>();

                if (_cachedQTEPanel != null)
                {
                    // 初始状态设为隐藏，等待 ShowQTE 时唤醒
                    obj.SetActive(false);
                    _cachedQTEPanel.ShowQTE(); // 本次直接显示
                    Debug.Log("[SystemUIManager] ✅ QTE 面板已缓存，后续调用 0 延迟");
                }
                else
                {
                    Debug.LogError(
                        "[SystemUIManager] QTE 预制体上未找到 QTEUIManager 组件！\n" +
                        "加载的预制体已销毁。");
                    Addressables.ReleaseInstance(obj);
                }
            }
            else
            {
                Debug.LogError(
                    $"[SystemUIManager] QTE 面板加载失败！状态：{handle.Status}\n" +
                    $"异常：{handle.OperationException}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[SystemUIManager] QTE 面板加载异常！\n" +
                $"类型：{ex.GetType().Name}\n信息：{ex.Message}");
        }
        finally
        {
            _isLoadingQTE = false;
        }
    }

    /// <summary>
    /// 隐藏 QTE 面板。
    /// 仅 SetActive(false)，绝不销毁 —— 保证下次 ShowQTE 0 延迟秒开。
    /// </summary>
    /// <param name="success">true = QTE 成功，播放成功动画；false = 失败/中断</param>
    public void HideQTE(bool success)
    {
        if (_cachedQTEPanel != null)
        {
            _cachedQTEPanel.HideQTE(success);
            // ↑ HideQTE 内部执行 SetActive(false)，GameObject 保留在场景中
        }
    }

    /// <summary>
    /// 【Bug 修复】强制关闭 QTE 面板 —— 无动画，直接 SetActive(false)。
    /// 供大招结束（OnUltimateFinished）等极端情况使用，确保 UI 绝不残留。
    /// </summary>
    public void ForceHideQTE()
    {
        if (_cachedQTEPanel != null)
        {
            _cachedQTEPanel.ForceHide();
        }
    }
}
