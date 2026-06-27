# MyThirdPersonDemo

> 一款受《艾尔登法环》启发的第三人称动作 RPG | Unity 6 + URP | 独立开发

<!-- TODO: 在此放置 2-3 张战斗/场景 GIF 或截图 -->
<!-- 推荐截图内容：剑气波技能、QTE 终极连斩、休息点交互 -->

---

## 项目简介

**MyThirdPersonDemo** 是一个基于 Unity 6 开发的第三人称动作 RPG 原型。项目从零搭建，实现了魂系战斗、敌人 AI、RPG 成长、存档系统等完整的玩法循环，并在此过程中完成了从 Resources 到 Addressables 的资产管理重构。

- **引擎：** Unity 6000.3.11f1
- **渲染管线：** Universal Render Pipeline (URP) 17.3
- **核心依赖：** Cinemachine 3.1、Input System 1.19、Addressables 2.9、AI Navigation 2.0
- **目标平台：** PC / Android (IL2CPP)
- **开发周期：** 个人独立开发

---

## 核心玩法

### 魂系战斗

- **有限状态机驱动**：玩家在 IdleMove / LightAttack / HeavyAttack / RunAttack / Dodging / SkillCast / Ultimate / Hit / Dead 九个状态间流转，状态转换规则集中管理
- **连击系统**：轻攻击 3 段链、重攻击 5 段链，基于动画事件开关连击窗口 + 输入缓冲（提前按下可在窗口开启时自动衔接下一段）
- **闪避 & 完美闪避**：消耗耐力闪避，动画事件控制无敌帧起止；完美闪避触发短暂子弹时间 + 下次攻击暴击
- **双武器技能**：每把武器绑定专属技能——剑气波（WaveSlash）或 QTE 终极连斩（QTEUltimate），消耗怒气释放

### 敌人 AI

- **感知系统**：视野锥（FOV 90°）+ 听觉半径 + 射线遮挡检测，判定是否发现玩家
- **行为状态**：Hidden → Idle → Patrol ⇄ Chase → Attack / MagicCast，受击/死亡可打断任意状态
- **NavMesh 寻路**：巡逻走点、追击玩家，手动控制朝向（Slerp），受击时禁用 Agent 并通过 Warp 恢复
- **闪电法术**：远程敌人可在追击中释放范围预警闪电攻击

### RPG 系统

- **五维属性**：Vigor（生命）/ Endurance（耐力）/ Strength（力量）/ Resistance（抗性）/ Spirit（精神），每项独立加点
- **武器强化**：最高 +25 级，每级 +8 攻击力；强化后自动存档
- **经验 & 等级**：升级消耗公式 `500 + level² × 50`，升级获得属性点
- **休息点**：类篝火机制——交互激活后成为重生点 & 快速旅行节点，休息时自动存档、刷新敌人
- **任务 & NPC**：击杀任务追踪 + 商人对话系统

---

## 技术架构

### 整体分层

```
┌─────────────────────────────────────────┐
│  表现层 (UI / Animation / VFX / Audio)    │
├─────────────────────────────────────────┤
│  逻辑层 (FSM / AI / Skill / Quest)        │
├─────────────────────────────────────────┤
│  数据层 (ScriptableObject / JSON Save)    │
├─────────────────────────────────────────┤
│  资源层 (Addressables / Object Pool)      │
└─────────────────────────────────────────┘
```

**核心脚本结构：**

```
Scripts/
├── Player/
│   ├── PlayerMove.cs          # 玩家 FSM（EldenRingMovement）
│   ├── PlayerHealth.cs        # 伤害/死亡/重生
│   ├── PlayerLocomotionManager.cs  # 移动 & 重力
│   ├── PlayerCameraController.cs   # 第三人称摄像机
│   ├── PlayerTargeting.cs     # 锁定目标
│   ├── PlayerInputHandler.cs  # 输入中心
│   ├── PlayerAnimatorHandler.cs    # 动画管理
│   ├── PlayerDataManager.cs   # RPG 数据中心（单例）
│   ├── PlayerStatsManager.cs  # HP/耐力/怒气实时管理
│   ├── CharacterUIManager.cs  # HUD
│   ├── Skill_WaveSlash.cs     # 技能：剑气波
│   ├── Skill_QTEUltimate.cs   # 技能：QTE 终极连斩
│   ├── SkillWave.cs           # 波次 VFX
│   ├── WeaponDataSO.cs        # 武器 ScriptableObject
│   └── WeaponRuntimeAssets.cs # 武器 Addressables 运行时加载
├── EnemyController.cs         # 敌人 AI（BasicEnemyTest）
├── MonsterLightningAttack.cs  # 敌人闪电法术
├── RestPoint.cs               # 休息点 / 快速旅行
├── TaskManager.cs             # 任务系统
├── NPC/
│   └── BussesMan.cs           # 商人 NPC
├── Audio/
│   ├── AudioManager.cs        # 全局 BGM 管理
│   └── AudioPoolManager.cs    # 音效对象池
├── UI/
│   ├── SystemUIManager.cs     # UI 调度（Addressables 触发）
│   ├── SaveSlotPanel.cs       # 存档界面
│   ├── DamageTextPoolManager.cs  # 伤害数字对象池
│   ├── DamageTextItem.cs      # 单个伤害数字
│   ├── ActionLogManager.cs    # 战斗日志
│   └── SettingsMenu.cs        # 设置菜单
├── VFXPoolManager.cs          # VFX 对象池
├── SaveData.cs                # 存档数据模型
└── SaveSlotManager.cs         # 多槽位存档 IO
```

### 玩家战斗 FSM

玩家所有行为由 `EldenRingMovement` 中的有限状态机统一管理：

```
                    ┌─────────────────────────┐
                    │       IdleMove          │ ← 自由移动/待机
                    └────┬───┬───┬───┬───────┘
                         │   │   │   │
              ┌──────────┘   │   │   └──────────┐
              ▼              ▼   ▼              ▼
         HeavyAttack   LightAttack  Dodging  SkillCast/Ultimate
              │              │        │           │
              ▼              ▼        ▼           ▼
          (连击链)       (连击链)  IdleMove    IdleMove
```

**关键设计决策：**

- **集中式状态锁**：`HandleActionInputs()` 入口处检查硬直状态（Hit/Dead/Dodging/SkillCast/Ultimate/RunAttack），命中时直接 return，阻止所有操作输入——避免分散在各处的状态检查
- **动画事件驱动**：连击窗口由 `Event_OpenComboWindow()` / `Event_CloseComboWindow()` 控制，而非基于时间——确保与动画精确同步，不受帧率影响
- **输入缓冲**：在连击窗口关闭期间提前按下攻击键 → 标记 `comboPending = true` → 窗口一开自动衔接，手感更宽容
- **FSM 看门狗**：攻击/技能状态超过 6 秒强制重置，防止动画事件丢失导致的卡死

### Addressables 资产管理

**迁移背景：** 项目初期所有资产放在 `Resources/` 下。随着资产增多，Resources 的缺陷逐渐暴露：
- 所有 Resources 资产在启动时会被索引，导致启动时间增长
- Resources 内的资产即使不使用也会被打包，增大包体
- `Resources.Load` 是同步操作，加载大资产时造成卡顿

**迁移方案：** 将运行时动态加载的资产（武器模型、VFX 预制体、UI 面板、音频）从 `Resources/` 移至 `Resources_moved/`（保留原始文件但脱离 Resources 系统），改为通过 Addressables 异步加载。

- 武器数据（`WeaponDataSO`）存储 `AssetReference` 而非直接引用，ScriptableObject 本身零资产内存占用
- `WeaponRuntimeAssets.LoadAsync()` 使用 **generation counter** 防止快速切武器时的竞态：每次加载分配一个递增世代号，回调中检查世代号是否仍然匹配，不匹配则丢弃过期结果
- 加载句柄集中管理，场景销毁时统一 `Addressables.Release()`

### 对象池系统

项目中实现了三类对象池，解决频繁 Instantiate/Destroy 导致的 GC 峰值：

| 池 | 类型 | 预热 | 扩容策略 | Addressables |
|---|---|---|---|---|
| **VFXPoolManager** | 多类型字典池 | 可配置 `preloadCount`，分帧预热 | 池空时 Instantiate 备用 | ✅ |
| **AudioPoolManager** | 单类型队列池 | 初始化 10 个 | 池空时即时创建 | ❌ |
| **DamageTextPoolManager** | 单类型队列池 | 初始化 20 个 | 池空时即时创建 | ✅ |

**共同设计模式：**
- 初始化时预实例化 → Dequeue 获取 → 使用完停用 → Enqueue 回收
- Addressables 池在 `OnDestroy` 中统一释放句柄
- 预热时插入 `Task.Delay(1)` 将 Instantiate 压力分散到多帧，避免单帧 GC spike
- DamageText 池额外设置 `_warmupComplete` 门控，预热完成前调用 `ShowDamageText()` 会被静默丢弃

### 其他设计要点

- **事件驱动解耦**：敌人死亡通过 `OnEnemyDied` 静态事件广播，玩家和任务系统订阅——替代了早期的 `FindObjectOfType` / `GetComponent` 耦合
- **存档系统**：`SaveData` 模型 → `SaveSlotManager` 多槽位 JSON 序列化 → `PlayerDataManager.ApplySaveDataToMemory()` 恢复运行时状态。存档内容包括属性、武器等级、已激活休息点、游戏时长等
- **护甲减伤公式**：`finalDamage = max(1, rawDamage × random(0.9, 1.1) × 100/(100+defense))` —— 减伤曲线递减，防止防御值堆满后无敌
- **命中停帧**：`Time.timeScale = 0.05` 持续 50ms，使用 `WaitForSecondsRealtime` 避免受 timeScale 影响

---

## 迭代历程 & 踩坑记录

### Resources → Addressables 重构

这是项目中最大的一次架构改动。初期所有资产通过 `Resources.Load` 同步加载，随着武器模型、VFX、音频资源增多，出现了以下问题：

- 游戏启动时 Resources 索引时间明显增长
- `Resources.Load` 同步加载大模型时主线程卡顿
- 不使用的资产也被打包，APK 体积膨胀

重构过程中遇到的主要难点：**快速切武器时的异步竞态**。玩家在 0.1 秒内连按切换键，可能同时发起 3-4 个异步加载请求，后发起的请求可能先完成，导致最终装备的武器与实际加载的模型不匹配。

**解决方案：** 在 `WeaponRuntimeAssets` 中引入 generation counter——每次 `LoadAsync()` 递增世代号并作为闭包变量捕获，回调中检查当前世代号是否仍然匹配，不匹配则调用 `Addressables.Release()` 立即释放过期结果。

### 命中停帧（Hit Stop）

命中停帧是提升打击感的关键手段，但实现中需要注意：
- 必须使用 `WaitForSecondsRealtime` 而非 `WaitForSeconds`，因为 `Time.timeScale` 已被设为 0.05
- 停帧时间不宜过长（当前 50ms），否则影响操作手感
- 必须在死亡/场景切换时重置 `Time.timeScale = 1`，防止残留

### 性能优化

- **对象池 + Addressables 组合**：VFX 和 DamageText 通过 Addressables 加载预制体 → 对象池管理实例生命周期，既避免 Resources 同步加载卡顿，也消除运行时的 GC Alloc
- **分帧预热**：池初始化时每 N 个 Instantiate 后 `await Task.Delay(1)`，将 CPU 压力分散，避免场景加载时出现明显卡顿
- **UI 字符串防抖**：`PlayerStatsManager` 中 HP/耐力/怒气数字只在整数值变化时才更新 TMP 文本，避免每帧分配字符串

---

## 开发环境

| 项目 | 版本 |
|---|---|
| Unity | 6000.3.11f1 |
| 渲染管线 | URP 17.3 |
| 脚本后端 | IL2CPP |
| 目标平台 | Windows / Android |

---

## 第三方资源

| 资源包 | 用途 |
|---|---|
| Mega Fantasy Props Pack | 场景道具 |
| Free Slash VFX | 斩击特效 |
| SkySeries Freebie | 天空盒 |
| Inguz Media Studio - Medieval Banners | 中世纪旗帜/道具 |
| Kenney UI Audio | UI 音效 |
| Vefects Zap VFX URP | 闪电特效 |

> ⚠️ 部分中文字体标注了"商用需授权"，如需商用请替换或购买授权。
