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

    /// <summary>QTE 面板异步加载中标记（防抖）</summary>
    private bool _isLoadingQTE;

    /// <summary>
    /// HideQTE / ForceHideQTE 被调用时若面板还在异步加载，暂存意图，加载完立刻执行。
    /// null = 无挂起请求；true = 成功动画；false = 失败/强制关闭。
    /// </summary>
    private bool? _pendingHideSuccess;

    /// <summary>ShowQTE 被调用后，加载完成时是否自动显示面板</summary>
    private bool _shouldShowAfterLoad;

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
            //Debug.Log("[SystemUIManager] 角色面板正在加载中，已忽略重复按键");
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
                //Debug.Log("[SystemUIManager] ✅ 角色面板加载完成");
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
    /// 预加载 QTE 面板（不显示）。
    /// 在 ExecuteSkill 中提前调用，利用大招前几段动画的时间异步加载，
    /// 确保 Event_TriggerQTE 时面板已就绪，0 延迟弹出。
    /// </summary>
    public void PreloadQTEPanel()
    {
        if (_cachedQTEPanel != null || _isLoadingQTE) return;
        _ = LoadQTEPanelAsync(); // fire-and-forget
    }

    /// <summary>
    /// 显示 QTE 战斗面板（懒加载 + 永久缓存）。
    /// </summary>
    public async void ShowQTE()
    {
        if (_cachedQTEPanel != null)
        {
            _cachedQTEPanel.ShowQTE();
            return;
        }

        _shouldShowAfterLoad = true;

        if (!_isLoadingQTE)
            await LoadQTEPanelAsync();
        // 若已在加载中（由 PreloadQTEPanel 触发），LoadQTEPanelAsync 完成时会检查 _shouldShowAfterLoad
    }

    /// <summary>共享的 Addressables 异步加载逻辑</summary>
    private async System.Threading.Tasks.Task LoadQTEPanelAsync()
    {
        _isLoadingQTE = true;

        try
        {
            if (qtePanelRef == null || !qtePanelRef.RuntimeKeyIsValid())
            {
                Debug.LogError("[SystemUIManager] qtePanelRef 未配置或无效！");
                return;
            }

            var handle = qtePanelRef.InstantiateAsync(mainCanvas);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[SystemUIManager] QTE 面板加载失败：{handle.Status}");
                return;
            }

            GameObject obj = handle.Result;
            _cachedQTEPanel = obj.GetComponent<QTEUIManager>();

            if (_cachedQTEPanel == null)
            {
                Debug.LogError("[SystemUIManager] QTE 预制体上未找到 QTEUIManager 组件！");
                Addressables.ReleaseInstance(obj);
                return;
            }

            obj.SetActive(false);

            // 加载完成 → 处理挂起请求（优先级：hide > show > 纯预加载不做任何事）
            if (_pendingHideSuccess.HasValue)
            {
                _cachedQTEPanel.HideQTE(_pendingHideSuccess.Value);
                _pendingHideSuccess = null;
            }
            else if (_shouldShowAfterLoad)
            {
                _cachedQTEPanel.ShowQTE();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SystemUIManager] QTE 面板加载异常：{ex.Message}");
        }
        finally
        {
            _isLoadingQTE = false;
            _shouldShowAfterLoad = false;
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
        }
        else
        {
            // 面板还在异步加载 → 暂存意图，加载完立刻执行
            _pendingHideSuccess = success;
        }
    }

    /// <summary>
    /// 强制关闭 QTE 面板 —— 无动画，直接 SetActive(false)。
    /// </summary>
    public void ForceHideQTE()
    {
        if (_cachedQTEPanel != null)
        {
            _cachedQTEPanel.ForceHide();
        }
        else
        {
            _pendingHideSuccess = false;
        }
    }
}
