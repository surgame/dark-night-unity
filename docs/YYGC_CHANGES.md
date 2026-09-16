# YYGC 修改授权与改动账本

<a id="hero-input"></a>

## 2026-09-16：输入封装、主角接线与 Input Actions Sample

用户要求保持原命名、对比缺陷后合并实施，并指出 YYGC 另一个会话正在引入插件。本批只在 `D:/Developer/YYGC-worktrees/input-actions` 的 `codex/input-actions` 修改，输入基线 `745f3d2`，提交 **`0c7cec00b7a7f9cec0287bb56d0af9fc45c9d143`**。用户 `D:/Developer/YYGC` 的 master `56afcad` 及 AnyRule 未提交文件保持原状；不切换或合并那个工作区，也未推送。

下表逐项列出本提交的 **54 个文件**，路径相对隔离框架根目录。所有文件同时存在于游戏锁定的 `.deps/YYGC-unified`；44 个 Sample 文件另导入到游戏 `Assets/Samples/YYGCInputActions`，与隔离框架源逐文件核对。原有七份 tracked 补丁及两个友元文件在依赖更新前后字节一致，准备脚本精确校验通过。UPM manifest／lock 的本地路径保持不变，完整提交由 `tools/prepare-lan-sample.ps1` 锁定。

| 文件 | 修改原因与内容 | 验证 |
|---|---|---|
| `Documentation~/INPUT_ACTIONS.md` | 接入、旧新方案对比、API 与生命周期、限制和测量说明 | 按实际实现及证据核对 |
| `Documentation~/INPUT_ACTIONS_VALIDATION.json` | 归档 34 个不同用例的来源与校准后路由测量 | 输入 XML 按影响合并生成 |
| `Runtime/PlayerInputs/YYInputActionService.cs` | 新增动作到 Interaction Sessions 的薄接线、即时取消及同帧 Button 隔离 | 模式／模态／失效／重入取消与路由微测量 |
| `Runtime/PlayerInputs/YYInputActionService.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Runtime/PlayerInputs/YYInputRebindingHandle.cs` | 新增界面拥有的改键句柄，Dispose 取消，拒绝同动作重复拥有 | 取消／释放／重复启动／模式许可回归 |
| `Runtime/PlayerInputs/YYInputRebindingHandle.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Runtime/PlayerInputs/YYInputRebindingService.cs` | 保留原签名；修改前校验、异常恢复、托管入口及可选查重范围 | 原 API、组合绑定、异常、查重回归 |
| `Runtime/PlayerInputs/YYInputSettingsData.cs` | 保留 v1 字段与默认值，补职责注释 | 既有字段与保存恢复回归 |
| `Runtime/PlayerInputs/YYInputSettingsStore.cs` | 保留路径与 API；严格信封、候选验证、失败回滚、原子刷盘替换 | 坏 JSON／未知版本／真实文件锁／清除失败回归 |
| `Samples~/InputActions/Content.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/Demo.mat` | 示例矩形的独立材质 | 实际渲染截图 |
| `Samples~/InputActions/Content/Demo.mat.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/InputActions.inputactions` | 原生 Player／Camp／UI 动作资产 | 移动跳跃、模式、UGUI 与改键回归 |
| `Samples~/InputActions/Content/InputActions.inputactions.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/InputActions.unity` | 实际序列化场景，包含角色、PlayerInput、UGUI 和模态页 | 保存重开、实际场景 1/1 |
| `Samples~/InputActions/Content/InputActions.unity.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/Pixel.png` | 75 字节白色像素，独立示例图形来源 | 实际渲染截图 |
| `Samples~/InputActions/Content/Pixel.png.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Cancel.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Cancel.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Click.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Click.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Navigate.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Navigate.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Point.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Point.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_ScrollWheel.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_ScrollWheel.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Submit.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Submit.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor/InputActionsSampleBuilder.cs` | 仅空目录初建场景与原生 UI 引用，普通导入不执行 | 初建、保存、重开与运行 |
| `Samples~/InputActions/Editor/InputActionsSampleBuilder.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor/YYGC.InputActions.Editor.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Editor/YYGC.InputActions.Editor.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/README.md` | 用户操作、导入、维护入口与真实验收范围 | 按已交付场景核对 |
| `Samples~/InputActions/README.md.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime/InputActionsSample.cs` | 独立本地移动／跳跃／使用、模式／模态、改键与保存演示 | 实际场景 1/1、两张渲染截图 |
| `Samples~/InputActions/Runtime/InputActionsSample.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime/YYGC.InputActions.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Runtime/YYGC.InputActions.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputRebindingTests.cs` | 旧 API、错误索引、托管取消、回调和查重边界 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputRebindingTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputRoutingTests.cs` | 模式、通道、同帧泄漏、重入与校准分配测量 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputRoutingTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputSampleSceneTests.cs` | 官方 InputTestFixture 驱动实际场景、鼠标 UI、焦点、改键重载 | 最终场景 1/1，通过截图复核 |
| `Samples~/InputActions/Tests/InputSampleSceneTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputSettingsTests.cs` | 真实文件和原生资产上的原子设置／坏输入边界 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputSettingsTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/YYGC.InputActions.Tests.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Tests/YYGC.InputActions.Tests.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `package.json` | 注册 Input Actions Sample 导入入口 | UPM 导入及实际场景检查 |

Unity 6000.4.9f1／Input System 1.19.0 下 **34 个不同 PlayMode 用例按影响合并通过**（服务／文件 33 项、最终真实场景 1 项），并非一次全绿 34 项运行。末次场景复跑禁用音频；虚拟键鼠驱动原生 UGUI，两张真实截图已检查。路由内核 10,000 次 Refresh＋全部 CanRead：2 动作 2.4844 ms、32 动作 75.8918 ms；校准后 GC.Alloc 未检测到分配。该数字不覆盖 UI、网络或前台帧率。Sample 未单独构建 Player，游戏 Mono 不能替代其独立发布验收。

游戏侧切片与验收见[联合执行文档](HERO_INPUT_EXECUTION.md)，输入详细结果见[机器证据](evidence/hero-input-framework.json)。保留原 `StartInteractiveRebind` 原生返回类型；旧调用者提前结束仍须先 Cancel 再 Dispose。新增管理入口用于界面生命周期，不重写 Unity 的设备或按钮状态机。



<a id="unified-u6-performance"></a>

## 2026-09-13：U6 装配校验热点修正

框架提交 `745f3d2c844a66389e39bb84cd878d5d80f77962`，先在 `.deps/YYGC-unified` 验证；确认用户仓库干净、master 仍为 `8faf74f` 后本地 fetch 并快进 `D:\Developer\YYGC`，未推送。游戏准备脚本锁定新完整提交，六份既有补丁及友元文件保留。

| 文件 | 原因与实际修改 | 落点与验证 |
|---|---|---|
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs` | 每次客户端完整投影都重新反射 Behaviour 的配置及组件绑定声明，256 次校验微测量均值 101.64 ms。按类型缓存不变声明，不保存 Definition、配置／组件实例或成功结果；实际配置、工厂、能力和绑定仍逐次验证 | 隔离 checkout 与用户仓库同路径；最终 138/138 Editor／Play，56.68 秒，包括预热后删除配置、清空／重复绑定的拒绝回归。缓存声明后的两次微测量为 16.66／9.76 ms；不据此宣称 Player 帧率通过 |

类型解析缓存候选没有显示明确收益，已撤回，`BehaviourTypeResolver.cs` 无最终差异；该候选的 139 项回归不增加当前通过数。本次无新增 YYGC 文件或 `.meta`。游戏 `a7bb926`／本框架提交的正式 Mono 完整矩阵 347 项通过，Sample 基础／弱网各 30 项通过。后续容量积压修正只落在游戏投影编码及验收工具中：`4e3798f`／同框架通过 144 项 Editor／Play、正式 Mono 350 项与另一次 240 秒容量 21 项。Sample 代码及共用框架未变，沿用其已有 60 项证据。收尾再次核对用户仓库干净且 HEAD 仍为 `745f3d2`，未推送；本次没有追加 YYGC 或 FishNet 修改。前台性能由用户暂缓，详情见[性能切片](YYGC_UNIFIED_PERFORMANCE.md)。

<a id="unified-u5"></a>

## 2026-09-13：U5 已完成资源的异步等待修正

框架提交为 `8faf74f03d9eac4e025d6fe0f81f0c26a9eac9d4`。先在 `.deps/YYGC-unified` 验证；确认用户仓库干净、仍在 `master` 且 HEAD 为 `0305eb7` 后，本地 fetch 并快进 `D:\Developer\YYGC` 至同一提交，没有推送。游戏准备脚本锁定完整提交；UPM manifest／lock 的隔离路径不变，六文件 Sample／UI／单例补丁及友元文件通过精确校验并保留。

| 文件 | 具体缺口与修正 | 落点与验证 |
|---|---|---|
| `Runtime/Utils/FastInstantiator.cs` | 后台 Editor 中，Addressables 句柄已完成，但 `handle.Task` 仍等待 ResourceManager 的延迟完成回调，导致会话预加载停滞。AcquireComponentAsync 改为现有 UniTask.Addressables 的 `handle.ToUniTask`，已完成句柄直接返回；取消时不由适配器自动释放，继续由原租约异常路径唯一释放，组件访问回主线程 | 隔离与用户仓库同一路径；26/26 会话测试 3.07 秒、整批 134/134 Editor／Play 54.02 秒，含真实 Worker 工厂、取消、两种域重载和三夜。U6 已从无旧 Library 的源码目录构建 Mono，同产物 347 项自动检查通过 |

本次没有新增 YYGC 文件、程序集引用或 `.meta`。Sample 未调用本次修改的 AcquireComponentAsync／PrepareAsync 路径，其既有 API 未改；不重复构建未受影响的 Sample。先前尝试仅更改 Task 续接上下文仍会阻塞，失败与取消记录保留，不作为修正通过证据。

U6 未新增框架修改；2026-09-13 收尾再次核对用户 YYGC 工作区干净且 HEAD 为上述完整提交。最终 Mono 使用游戏 `9e69a76`，通过活跃恢复、四人重开、九组弱网、三夜及容量功能；报告见 [U6 证据](evidence/yygc-unified-u6.json)。容量性能尚未签署，IL2CPP／双机器未验收，不将功能结果扩展为框架全平台或性能保证。

<a id="unified-u2"></a>

## 2026-09-13：U2 网络会话装配与跨对象提交

框架提交为 `0305eb74bbc2677a3d9025f684d8ded16481be4a`，在 `.deps/YYGC-unified` 验证。用户仓库 `D:\Developer\YYGC` 在同步前及 fetch 后均检查为干净、HEAD 为 `ddce2ff`，随后仅执行本地快进；当前具有相同提交，没有推送。下表每个文件均落在隔离和用户仓库的同一路径。

| 文件 | 原因与修改 | 实际验证 |
|---|---|---|
| `Runtime/Objects/NetworkStates/SessionStateChange.cs` | 新增复制／验证／安装／通知／释放的同步批量状态提交；异常通知继续处理其余状态 | 真实支付、同一通知读取多个最终 State、异常订阅和撤权重入用例通过 |
| `Runtime/Objects/NetworkStates/SessionStateChange.cs.meta` | Unity 自动生成的新脚本元数据 | Editor 导入、Mono 两配置通过 |
| `Runtime/Objects/NetworkStates/StatefulBehaviour.cs` | 状态引用与通知时点分开；权威和只读副本均可准备批量候选，保留池的所有权 | 原状态／池回归、新事务及副本失败重试、Mono 装配与 Sample 通过 |
| `Runtime/Objects/NetworkStates/StateSynchronizer.cs` | Spawn 前绑定显式可信会话，可延迟激活；停止后释放上下文引用 | 真实 Play、正式 Host＋两客户端、原 Sample 四进程通过 |
| `Runtime/Objects/NetworkStates/StateDataTypeStartupModule.cs` | 运行校验仅收集生成器支持的 StateDataAttribute 类型，修复本地测试状态误阻断启动 | 先复现 Bootstrap 失败，修正后两种域重载重复 Play 通过；带标记状态仍严格检查 |
| `Runtime/Objects/Runner/ObjectInstanceFactory.cs` | 显式会话参数贯通网络／本地初始化器；await 后验证生命周期 | 正式网络会话首次创建及复用原 Sample 通过 |
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | 公开持久初始化器所持有的原实例用于准备阶段预检 | 精确场景原实例接管、重开及保存恢复通过 |
| `Runtime/Objects/Runner/ObjectInstance.cs` | 批量通知期间拒绝装配、激活、退休及释放 | 同步通知退休兄弟对象被拒绝，所有 State 和支付仍完整提交 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs` | 批量通知期间拒绝激活／退休上下文 | 撤权重入用例、退出和换 epoch 通过 |
| `Documentation~/OBJECT_SESSION_LIFECYCLE.md` | 记录批量提交、网络上下文、瞬时池引用和注册范围 | 与实际 API 及验收边界核对 |

U2 分批覆盖 126 个不同 Editor／Play 用例，无未解决失败；Mono 装配 14/14，正式三进程切片 26/26，独立四进程 Sample 30/30。只构建正式 Mono 和 Sample Mono 各一次，复验复用产物。游戏准备脚本锁定完整提交；manifest／packages-lock 保持同一隔离路径。既有六文件 Sample／UI／单例补丁完整保留，启动排除 patch 仅更新新基线的上下文和 blob 哈希。详见 [U2 证据](evidence/yygc-unified-u2.json)。完整玩法、最终弱网／性能、IL2CPP 及双机器 LAN 不属于本阶段通过范围。

## 2026-09-13：U1 显式会话状态与对象装配

已提交 `ddce2ffdf422c8c9cb8e872fb5f20053cdedcda6`。先在 `.deps/YYGC-unified`／`codex/dark-nights-unified-objects` 验证；再次确认 `D:\Developer\YYGC` 工作区干净且仍在 `ccd61e0` 后，将用户仓库快进到该提交。没有推送。原 `.deps/YYGC` 的修改和缓存保留；游戏依赖改用 `.deps/YYGC-unified`，准备脚本锁定完整提交并精确校验既有四份补丁及友元文件。

下面 20 个文件均落在该框架提交，用户仓库具有相同路径。新 `.meta` 由 Unity 生成，没有重新分配已有资产 GUID。

| 文件 | 修改原因与结果 | 验证 |
|---|---|---|
| `Runtime/Objects/Behaviours/BehaviourContext.cs` | 携带显式会话，不靠跨 await 的 SessionScope 找依赖 | 换会话注入、真实工厂通过 |
| `Runtime/Objects/NetworkStates/IStatefulBehaviour.cs` | 增加 Session 同步模式，区分状态权限和发送粒度 | 本地权威／只读副本通过 |
| `Runtime/Objects/NetworkStates/StateSynchronizer.cs` | 会话托管的个体 State 不占网络索引、不逐个发包 | 既有状态回归、Sample 四进程通过 |
| `Runtime/Objects/NetworkStates/StatefulBehaviour.cs` | 动态状态权限、深复制入口、失效 scope、异常回滚和退休中的回调清理 | Editor 状态用例及真实 Play 通过 |
| `Runtime/Objects/Runner/IObjectSessionInitializer.cs` | 显式会话装配合同，返回实际 ObjectInstance | Loader 原实例与工厂通过 |
| `Runtime/Objects/Runner/IObjectSessionInitializer.cs.meta` | 新接口的 Unity 元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/LocalObjectInstanceInitializer.cs` | 接入显式会话与延迟激活 | 场景、动态实例通过 |
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs` | Editor／Player 共用能力、配置、生成工厂和绑定检查 | 真实 Mono 负例全部被拒绝 |
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs.meta` | 新校验器元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | BindSession 接管同一个场景实例 | Prefab 连接、位置和实例引用保持 |
| `Runtime/Objects/Runner/ObjectInstance.cs` | 准备／激活／退休／释放；换会话注入；拒绝清理失败的 Behaviour 复用 | 重入、换定义、失败清理、两种重载 Play 通过 |
| `Runtime/Objects/Runner/ObjectInstanceFactory.cs` | 预加载返回同步装配租约 | Addressables、冷加载取消、Mono 通过 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs` | 可信动态权限、主线程会话及取消／退休 | 撤权、未激活、退出加载用例通过 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs.meta` | 新上下文元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/PreparedObjectDefinition.cs` | 预加载后同步创建，错误回收未激活对象，拒绝加载后修改 Prefab 引用 | 真实 Worker Prefab 通过 |
| `Runtime/Objects/Runner/PreparedObjectDefinition.cs.meta` | 新工厂租约元数据 | 导入及 Mono 构建通过 |
| `Runtime/Utils/ComponentAssetLease.cs` | 每次加载拥有独立 Addressables 引用 | 冷加载取消、释放后创建拒绝通过 |
| `Runtime/Utils/ComponentAssetLease.cs.meta` | 新资源租约元数据 | 导入及 Mono 构建通过 |
| `Runtime/Utils/FastInstantiator.cs` | AcquireComponentAsync 的取消／失败释放，不清理其他租约 | Editor 与独立 Mono 通过 |
| `Documentation~/OBJECT_SESSION_LIFECYCLE.md` | 记录 API 时序、权限、租约和状态快照边界 | 与实际实现核对 |

宿主额外修改 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，为真实 `DarkNights.Tests` 生成调度器增加友元访问。该文件继续作为游戏的锁定补丁，不混入通用框架提交。旧六文件补丁没有被清理或重复计为本轮框架修改。

分批验证覆盖原 95 项及新增 16 项 Editor／Play 用例，失败的测试驱动已修复并复测；独立 Mono 装配 14/14，Sample 四进程基础 30/30。详情见[U1 证据](evidence/yygc-unified-u1.json)与[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)。没有进行 IL2CPP 或双机器 LAN；U2–U6 尚未完成。

## 2026-09-13：统一对象架构的升级适配授权（仅规划）

用户明确允许在 YYGC 存在能力限制或 BUG 时升级适配；正式游戏后续采用一套 YYGC 对象／状态模型，不要求旧数据兼容。具体前置能力、阶段门槛和交付要求见 [YYGC 统一重构计划](YYGC_UNIFIED_REFACTOR_PLAN.md)。先在隔离 checkout 核实和验证，保留用户已有改动，游戏仍锁定可重现依赖；该授权不要求每项必要修正重复确认。

本次只读核对：用户 YYGC 仓库与隔离依赖均位于 `ccd61e01f15332b1197cfa5ee72af8777c4a0b49`，用户仓库状态为空；`.deps/YYGC` 的既有补丁差异保留。**本次 YYGC 修改文件数为 0，未创建框架提交、未升级依赖、未进行新 Unity／Player 验证。** 新计划中的状态权限、显式会话装配、同步创建和严格校验是待实施项，不能计入下方已实施账本。后续每阶段须逐文件补充原因、隔离／用户仓库落点、提交和验证结果。

<a id="scene-definitions"></a>

## 2026-09-13：Definition 场景入口

实际缺口：Loader 缺少统一公开定义引用，拖拽工具只写旧整数 ID、单例判重也使用旧 ID；现有分类不能表达单位和自然资源点。先修改隔离 `.deps/YYGC`，Unity `6000.4.9f1` 编译完成；确认用户 YYGC 工作区干净且 HEAD 为 `516f76c` 后，快进到 `ccd61e01f15332b1197cfa5ee72af8777c4a0b49`。没有推送远端。游戏准备脚本锁定此完整提交，manifest／packages-lock 的隔离路径不变；既有六文件运行补丁完整保留，准备脚本精确校验通过。

以下是本次 YYGC 的全部修改，路径相对于 `D:\Developer\YYGC`，隔离依赖具有相同提交。**仅编译完成，测试回归按用户要求待确认。**

| 文件 | 原因与实际修改 | 验证状态 |
|---|---|---|
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | 公开 DefinitionReference／ResolveDefinition；EditorConfigure 统一写 GUID 与初始化器，检测 PrefabRef 一致性；激活时拒绝未解析定义 | 编译完成；运行生命周期待回归 |
| `Editor/Objects/Runner/ObjectDefinitionDragHandler.cs` | 拖拽改用 GUID 配置入口，单例按解析后的 Definition 判重；公开 SceneObjectCreated 编辑器扩展事件 | 编译完成；实际拖拽和单例冲突待回归 |
| `Runtime/Objects/Types/ObjectType.cs` | 追加 Unit、Scenery_ResourceNode、World_Session、World_Connection，保留所有旧枚举值 | 编译完成；17 个游戏定义已配置 |
| `Runtime/Objects/Types/TypeCategory.cs` | 末尾追加 Unit 分类并补充职责注释，不重排旧值 | 编译完成；序列化回归待确认 |

本批没有新增 YYGC 文件或重建 `.meta`。游戏侧 16 个场景放置引用迁移和静态差异核对见 [实施记录](SCENE_DEFINITIONS.md)。

2026-09-12 用户授权：必要时可以更新 YYGC，并将这项约定加入项目知识；完成后必须一一列出 YYGC 改动。此授权允许为实际接入缺口修正框架，保留用户已有修改、隔离验证和锁定依赖的要求仍有效。`AGENTS.md` 已同步该约定。

| 本次变更 | 具体证据与原因 | 修改落点 | 当前验证 |
|---|---|---|---|
| 池化重入时重新登记本地／网络单例 Instance | 关闭 Domain Reload 再进入 Play，`CampInput.Initialize` 因 Interaction Sessions 服务为空而使正式会话模块失败；单例离场清空 Instance，但池化重入跳过首次 Initialize | 新增 `RestoreSingletonOnPooledReentry.patch`，将单例引用登记移至每次执行的 InitializeCore，首次 OnSingletonInitialize 仍只调用一次；同时覆盖两种单例基类，并接入准备脚本 | Editor 55/55；常规重载三次会话 9/9，关闭 Domain Reload 的连续两次 Play 各三次会话 10/10；协议 5 Mono 九组四进程恢复均 22/22 |
| 修正 UGUI 根组件的 Unity 空引用判断 | 空场景启动实际抛出 `MissingComponentException: Canvas`；`GetComponent<T>() ?? AddComponent<T>()` 未识别 Unity 的空组件包装对象 | 新增可复现补丁 `tools/lan-framework-patch/FixUguiRootUnityNull.patch`，修改隔离依赖 `UGUIRuntimeStartupModule` 中 Manager、Canvas、CanvasScaler 的三个判断；准备脚本验证并应用补丁，用户 YYGC 仓库未改动 | 修复后 Editor 实际创建五个正式面板和一个菜单 Interaction Session；字体及后续生命周期另行验证 |
| 为 `DarkNights.View` 增加 `InternalsVisibleTo` | UGUI Behaviour 的 YYGC 生成更新分派器读取 `CoreBehaviour._updateFlags`，Unity 编译报 CS1061；既有 Runtime／Sample 已有相同授权 | 本仓库 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，经核对旧文件等于已提交基线后更新 `.deps/YYGC/Runtime/NetworkCommands/SampleAssemblyAccess.cs`；用户维护的 YYGC 仓库尚未改动 | 依赖准备脚本通过；Unity 编译通过；原生 UGUI 首版资源创建完成，运行与生命周期验收继续执行 |

后两项已随原生 UI 批次 Mono 实际构建、启动 6/6、独立 Host＋客户端 13/13 和 Editor 鼠标 13/13 验证，见[实际证据](evidence/native-ui-2026-09-12.json)。用户维护的 `D:\Developer\YYGC` 未改动，修正位于可重现补丁与隔离依赖。

<a id="workshop-display"></a>

## 2026-09-12：Workshop 定义展示

原因：工坊平铺和树形均以 `[{Id}]` 开头、仅按旧 ID 排序并搜索，正式 GuidFirst 定义的零值无法区分对象，也不能按 Key 查找。用户确认原有资产文件名必须保留，因此采用名称／Key／文件名三行；名称 13 号加粗、Key 11 号偏蓝灰、文件名 10 号较淡，行高统一 60。长 Key 和文件名按宽度从中间省略，完整值保留在提示／复制和搜索中；有效旧 ID 只作为辅助标记。

落点：先在 `.deps/YYGC` 验证，提交 `516f76c4fe062fa82384f7b91ac46c453abbe80d`，再确认用户 YYGC 工作区干净且 HEAD 仍为 `10b8f0e`，将 `D:\Developer\YYGC` 的 `master` 快进至同一提交。没有推送远端。游戏通过 `tools/prepare-lan-sample.ps1` 锁定该完整提交；manifest／packages-lock 的隔离包路径未改变，既有运行补丁保留并通过精确校验。

以下为该 YYGC 提交的全部 10 个文件，路径相对于 `D:\Developer\YYGC`；隔离依赖包含相同提交。

| 文件 | 原因与修改 | 验证 |
|---|---|---|
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs` | 新增只读名称、身份及原文件名映射；按 Key／GUID／别名搜索，按名称及稳定身份排序 | 缺名称、零旧 ID、长 Key、GUID 与旧号搜索；实际窗口名称顺序通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs` | 新增共用三行视图、有效旧 ID 标记、完整提示与右键复制；复用时重置文件名和身份 | 两种栏宽／两种视图、完整 Key 复制、行复用通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopListPresenter.cs` | 平铺和树形共用三行视图及 60 像素行高；改名时整体隐藏／恢复三行 | 220／400 像素栏宽布局与搜索、F2／取消、文件名保留通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.uss` | 名称、Key、文件名的字号、配色、间距与省略；定义浅色主题对应颜色 | 当前深色 Editor 中实际布局无重叠和越界；浅色主题视觉复核未执行 |
| `Editor/Objects/Definition/Workshop/WorkshopAssetService.cs` | 去除按旧整数排序，使用共用名称比较器 | 实际 29 个定义名称顺序通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.cs` | 接入完整身份／文件名搜索，保存、项目变化和撤销后刷新列表并保持目标选择 | 实际窗口按大写完整 Key 搜索通过；关闭重开后 29 行均含文件名 |
| `Editor/Objects/Definition/Workshop/WorkshopInspectorPresenter.cs` | 基本属性显示可选中复制、自动换行的 Key／GUID；零旧 ID 不显示 | 实际 Worker 属性区绘制及窗口重开通过，Console 无错误 |
| `Documentation~/DEFINITION_IDENTITY.md` | 记录三行展示、长文本、旧号、搜索与身份制作边界 | 与本次实现和用户保留文件名的要求核对 |

验证：Unity `6000.4.9f1` 导入／编译通过，UI 检查 **47/47**；实际窗口搜索、名称排序与重开检查通过；2,457 个游戏资源、meta、Packages 和 ProjectSettings 输入哈希不变。当前依赖准备与全新隔离 clone 准备均通过。首次导入生成两份新 meta；用户提出三行要求后，仅对新增输入涉及的三份源码／样式统一再导入一次。未新增 Player、PlayMode 或联机验证，不改变 M5 状态。详见[本批证据](evidence/workshop-display-2026-09-12.json)。

已有的 Sample 注册排除、启动验证排除及 Sample／Runtime 友元声明是此前已提交补丁，本次没有改变这些行为。每次后续修复在本表新增独立行；最终交付逐项列出真实改动及各自通过／待验证状态，不把用户原有修改算作本次成果。
