# C 方案重构执行计划

2026-09-12 执行更新：**R1–R4 已实施并完成最终 Ready 修复后的 Mono 复验。** 并发、活跃加载、九组四进程弱网、战斗晚加入、三夜及容量上限均通过；实际文件、结果和仍未执行的性能 A/C 对比见[实施记录](C_REFACTOR_IMPLEMENTATION.md)。本批不推进 M5 画面修正。以下保留原规划的清单、估算及历史核对状态，不能将其当作已执行证据。

基线提交：`3cde9bc4dea4aea2afd96a8797b8e814bda87bff`。最新开发状态以 [DEVELOPMENT](DEVELOPMENT.md) 为准：正式规则、四人联机和恢复主体已有实现；M5 画面、普通前台 Player 性能、IL2CPP 和双机器边界仍未全部收口。不能把本重构的完成等同于 M5 完成。

## 1. 决策与范围

采用讨论中的 C：**Core 个体拥有真实状态，公共规则系统处理相应业务，ObjectsV2 会话对象拥有权威运行生命周期，Local 个体 Behaviour 接管自己的绑定、表现与操作入口。**

本批是保持现有玩法的结构重构：
- 不改变经济、工位占用、AI、伤害、攻击前摇、箭矢、波次、随机数调用顺序。
- 保留协议 5、现有命令与状态 wire 字段、可靠整局投影、现有发布条件和频率。
- 保留 60 Hz 服务 tick、暂停和倍速入口；Host 与客户端都消费冻结展示副本。
- 保留角色、建筑、工位的 Local Definition；不改为逐实体 NetworkObject，不引入每实体 NetworkTransform。
- 不引入 ECS、通用状态机框架、第二套 DI 或事件总线、应用级游戏单例。
- 本批不新增装备、Buff、技能、物品库存玩法；第 8 节给出后续落点，禁止先建空类或占位目录。
- 不在本次重构中开展全量 Core 拆类、通用池化或网络拆流；现有 Actor、Worksite 等已有清晰职责，保持代码与冻结证据。
- 前次 [空装配定义盘点](OBJECT_DEFINITION_AUDIT.md) 中的“15 类外观可迁出 ObjectsV2”是另一候选方案；本次 C 选择继续使用并装配 Behaviour，不同时执行该迁出方案。

工作区已有修改：AGENTS.md、Farm.prefab、Worker.asset。Worker.asset 属于未来定义更新目标，必须按字段合并；Farm.prefab 本批只验证，不覆盖。现有差异不能提交为本重构成果，也不能通过恢复 HEAD 清除。

## 2. 目标职责与数据流

~~~mermaid
flowchart TD
    N["SessionNetwork：连接与事件适配"] --> L["SessionObjectLink：框架角色与装配回调"]
    L --> B["CampSessionBehaviour：本局生命周期所有者"]
    B --> S["SessionServer：连接身份、回执、发布与存储编排"]
    S --> A["SessionAuthority：权限、队列、去重、Ready、epoch"]
    A --> C["GameSession / Core 个体与规则"]
    C --> P["冻结投影 → WorldSessionBehaviour / StateSynchronizer"]
    P --> R["SessionClient / WorldReplica"]
    R --> E["SessionEntityViews：登记、创建、时间线、分发"]
    E --> V["Actor / Building / Worksite PresentationBehaviour"]
    V --> W["NativeVisual：原生资源与绘制操作"]
~~~

| 责任 | 唯一所有者 | 允许的协作 |
|---|---|---|
| HP、任务、生产和个体真实状态 | Core 的 Actor / Building / Worksite | Core 系统受控修改；View 不持有可写实体 |
| 世界、权限、业务请求历史、epoch | SessionAuthority | 网络适配提供可信连接；保留有界去重，不做回滚重模拟 |
| 权威服务创建、运行、停止与释放 | 新 CampSessionBehaviour | 外部可提出停止请求，不能再直接 new / Dispose 其 SessionServer |
| 发布整局投影 | SessionServer + 现有 WorldSessionBehaviour | 不在 StatefulBehaviour 内重算 HP 或经济 |
| 实体显示状态 | 个体 Behaviour 持有当前冻结 ViewData 引用 | 只读；连接代次、epoch 或绑定失效即清除 |
| 显示时间线与实体视图登记 | SessionEntityViews | 一条公共时间线，明确向个体分发；不为每个实体建立网络订阅 |
| 位置、姿态、阶段选择与本地反馈 | 各类表现 Behaviour | 使用 NativeVisual 的明确绘制方法 |
| Sprite、锚点、动画资产、预览与残骸绘制基础 | NativeVisual | 保留序列化字段和已有引用，不能靠节点名兜底 |

对象级状态访问和操作入口：
- ActorPresentationBehaviour 提供当前实体 Id / Epoch、只读 ActorViewData（其中已有 Hp）、可用性；当前 MaxHp 可以从只读角色配置获取。
- 实体命令复用现有 InputIntent 与 SessionUiController → SessionClient 路径，通过 Bind 时传入的窄回调提交显式实体 ID；不另建通用命令接口族，不读取全局 SelectedIds 代替参数。
- 本批只接现有操作。装备/Buff API 不能声明成已经实现，也不为它们增加无效按钮或空入口。
- 此处个体 Behaviour 是 PooledBehaviour 路径，不是个体 StatefulBehaviour；真实状态仍在 Core，属于 C 而非 B。

## 3. 源码核对到的关键依赖

1. SessionNetwork.Connect 当前创建 SessionServer，Update 推进它，Disconnect 释放它。三项必须实质迁走，Server 属性可保留为当前 CampSessionBehaviour 的只读转发，供现有诊断读取。
2. YYGC StateSynchronizer 已在定义装配就绪后触发 ObjectInstance.TriggerStartServer / TriggerStartClient，存在 IStartServer / IStartClient。
3. 当前 IBehaviour 没有对应的 IStopServer / IStopClient。使用已有 SessionObjectLink 的 FishNet 停止回调做窄适配，不能假定新增一个 Behaviour 就自然覆盖停止边界。
4. AppStartupContext.Register 是现有应用服务表，不等于 ObjectInstance 的 DIContainer 注册。工厂异步加载之后才实例化，不能在 SessionScope 内跨 await，也不能假设应用注册的服务能自动被 Behaviour 注入。
5. OnInitializeCompleted 触发时机与服务端角色启动不同。生命周期要同时等待“依赖已配置”和“网络角色已就绪”，两者无论先后都只启动一次。
6. 当前同一个 NativeVisual Prefab 也被 SessionPlacementView 用作建造预览，被 SessionEffects 用作尸体/残骸。新增 Behaviour 在未 Bind 前必须静默；预览、残骸和 Editor 样本不能订阅活实体或启动规则。
7. 本批只增加非 Stateful 的会话/表现 Behaviour。WorldSession 的唯一 StatefulBehaviour 及 wire 注册顺序应保持；仍须验证实际装配索引和生成产物。
8. SessionProjector 等普通 C# 类型移出 Runtime/Session 后，tools/CoreRegression 的显式 Compile 输入必须同步调整，不能让其意外编译引擎/网络依赖。

以上是已核对的代码事实；目标新类、升级工具和测试尚不存在。

## 4. 可执行批次

### R0：记录输入和锁定验收基线（预计 0.5–1 人日）

输入：当前提交、工作区差异、锁定依赖、现有 Mono 产物与证据。

执行：
- 记录游戏源码、涉及 Definition/Prefab/.meta、配置、布局、依赖锁定和生成注册摘要；特别保留 Worker.asset / Farm.prefab 的用户差异。
- 先核对现有 Mono 是否能对应当前游戏和依赖输入。可复现时复用；不能对应时，基线 A 可单独构建一次 Mono 并归档，与最终 C 属于不同输入，不能冒用历史性能数据。
- 锁定规则/旧档冻结夹具及比较入口；确认 Player 指南中的脚本路径和固定产物目录。
- 性能比较固定硬件、分辨率、前台窗口状态、负载、连接数、采样区间和仪表开销；基线与 C 同条件。原隐藏窗口及不完整窗口分位数不能直接充当完整基线。
- 为每批设置隔离日志/证据目录，记录输入哈希；不清理用户改动，不运行首版素材初始化器。

输出：输入清单、可用基线说明、验收配置。任何缺失都如实记录，不用重新生成冻结夹具补齐。

### R1：会话对象拥有业务生命周期（预计 1.5–2.5 人日）

依赖：R0；配置与布局由现有 Entry 准备完成。

执行：
- 新增 CampSessionBehaviour，拥有 SessionServer。保留 SessionServer 对 Authority、Clock、Storage 的组合，不为它们分别建立 Behaviour。
- SessionObjectLink 通过序列化引用/绑定取得本对象和 StateSynchronizer，改为装配完成事件及明确角色回调接线，移除每帧发现行为的 Update 轮询。
- 复用 AppStartupContext 和本对象既有 DI/绑定：应用参数通过一次显式 Configure 交给行为；本对象内依赖通过既有装配取得。不新增容器，不把可写世界注册成应用全局服务。
- 设置 configured / server-role-ready 两个启动条件；只有都满足且当前实例未启动时，行为才创建一个服务。客户端角色只接观察，不创建权威服务。
- 从 SessionNetwork.Update 移除 Server.Advance；由行为接入框架更新并使用未缩放时间调用既有 SessionClock。不能把框架 IUpdate 的 Time.deltaTime 当成无条件等价输入，也不改用 Unity 默认 FixedUpdate 步长。
- SessionNetwork 保留连接、鉴权装配、玩家入口生成和网络事件转接。命令订阅只路由到当前活动行为拥有的服务；没有活动会话时按原未就绪语义处理。
- 服务端停止、Despawn、创建失败、应用退出均由行为的同一个幂等停止入口释放服务；停止前使旧实例失效，清除外部活动引用，再停止推进并清理存储/订阅。
- Host 只停止客户端时只移除本地观察，不能结束仍运行的服务端；只停止服务端时必须立即停止权威推进，不能等待客户端对象最终销毁。
- 旧对象迟到的回调/异步结果以实例引用和连接尝试代次拒绝；旧 Detach 不能解除新房间的活动服务。
- WorldSession.asset 增加一个非 Stateful 的 CampSessionBehaviour；WorldSession.prefab 只增加必要的显式组件绑定，保留 GUID、网络组件和已有布局。
- 用有限升级工具修改已经存在的资产；旧初始化入口仅更新为将来空目录安装时产生正确合同，不能拿它们覆盖现有资产。

验收：配置与角色回调两种先后顺序、Host/纯客户端、延迟定义装配、三个连续开关房间、只停客户端、只停服务端、场景/对象销毁、加载 epoch、失败创建和旧异步结果。服务实例计数只允许 0/1；停止后无推进、无重复订阅、无重复支付。

### R2：个体表现职责落到 ObjectsV2（预计 2–3 人日）

依赖：R1 生命周期稳定；同批新 View 类型已编译并生成绑定/注册。

执行：
- 新增一个有限的 EntityPresentationBehaviour 生命周期基类及 Actor / Building / Worksite 三个具体类；一个手写主体，保持 C# 9、中文 XML summary 和 300 行硬上限。
- 基类只管 Bind / Unbind、身份有效性、已有 visual 绑定和清理；业务姿态选择由三个具体类实现，避免基类变成类别 switch 总表。
- 初始化仅取得已验证组件。Bind 之后才接收活实体更新和输入回调；未 Bind 的预览/残骸保持被动。
- SessionEntityViews 保留工厂创建、异步代次检查、增删/转职重建、最新帧和公共时间线，改为向已绑定 Behaviour 分发。
- 将 NativeVisual.Apply 中的角色动作选择/时间映射、建筑阶段和工位状态解释移到相应 Behaviour；NativeVisual 提供明确的采样、朝向、着色和可见性操作。若新增 Behaviour 仅转发 Apply 而没有接管职责，不算完成。
- 保留 NativeVisual 的序列化字段、Prefab 上的 visual 键、Preview / PresentRemnant 语义及 IEntityVisuals.Visual 查询合同；状态条、头像、拾取等不为此次重构整体改接口。
- 当前 SessionEntityViews 每角色查找目标工位的 FirstOrDefault 可改为新投影到达时建立一次的索引，避免同帧重复线性查找；不顺带改 Core 索敌或改变实体顺序。
- 所有类型用已有 DefinitionReference / ObjectInstanceFactory 创建；15 个 Local Definition 各配置对应共用 Behaviour，保持对象名、Key/GUID、PrefabRef、NetType 和 Addressable 身份。
- 首先验证工人+树的采集切片，通过后在同一已编译类型批次完成其余同类资源装配验证；同一逻辑资产批次只主动提交一次。
- 解绑时清除冻结副本、回调和身份；转职保留 EntityId，但旧外观对象必须彻底解绑；晚到工厂结果不能复活旧 epoch 或旧职业。
- 继续使用当前实际的释放路径；继承 PooledBehaviour 不代表 GameObject 已实现池化，本批不新增另一套对象池。

验收：三类行为分工、读取 HP、明确 ID 的现有操作、采集/耗尽、施工/训练/转职、死亡与残骸、建造预览、暂停/倍速插值、清理和再次开局。15 类 Prefab 编辑器打开、保存、重开和 Play 外观引用完整；原动画关键帧、坐标和素材哈希保持。

### R3：Session 目录按现有职责归类（预计 0.5–1 人日）

依赖：R1/R2 合同稳定。仅移动五个已有普通类型，保留 .meta/GUID：
- SessionProjector、SessionEventJournal → Runtime/Network。
- SessionStorageRequest → Runtime/Save。
- SessionMeasurements、MeasurementSeries → Runtime/Diagnostics。

同步使用处 namespace 和 CoreRegression 编译输入。SessionAuthority 继续保存权限、队列、去重、epoch、Ready 和加载票据。SessionConnection 本批保留类型名，其 summary 明确这是游戏参与者能力，不是重写的 FishNet 连接；不为目录观感追加一轮大规模改名。

验收：CoreRegression 可独立编译/执行；引用没有漏改；Runtime 仍只有一个程序集；View 不依赖 Runtime/Logic；移动文件及目录 meta 由 Unity 按既有规则处理。

### R4：统一验证与交付（预计 1.5–2.5 人日）

依赖：上述源码、资源、注册和导入全部完成。

先运行完整静态/Core/Editor矩阵；通过后针对最终 C 输入只构建一次 Mono，并复用同一产物执行正式进程矩阵和图形检查。若前序失败立即停止依赖阶段，修复后只重跑受影响项；输入未变不能以等待超时为由重启任务或重建。

输出每阶段 success/failure、通过/失败项、计数、输入哈希、日志和产物路径。性能问题用新测量定位，不将本次结构重构宣称为性能优化已通过。IL2CPP 仅在用户明确确认后构建；双机器需真实第二台 Windows 主机。

总估算：约 **6–10 人日**，包含实施与回归，不含装备/Buff 新玩法、框架未知缺口、M5 既存画面修复、IL2CPP 与双机器外部等待。该估算不是性能或工期保证。

## 5. 具体文件清单

以下路径均相对仓库根目录；M=预计修改、N=计划新增、R=移动。机器可读完整清单见 [规划清单](evidence/c-refactor-plan-2026-09-12.json)。该 JSON 不代表这些文件已经修改。

### 5.1 新增的八个手写 C# 文件

| 路径 | 批次与职责 |
|---|---|
| Game/Assets/DarkNights/Scripts/Runtime/Network/CampSessionBehaviour.cs | R1；权威服务生命周期与角色启动门槛 |
| Game/Assets/DarkNights/Scripts/View/EntityPresentationBehaviour.cs | R2；有限的绑定/解绑基类 |
| Game/Assets/DarkNights/Scripts/View/ActorPresentationBehaviour.cs | R2；角色副本、姿态、状态访问和现有操作入口 |
| Game/Assets/DarkNights/Scripts/View/BuildingPresentationBehaviour.cs | R2；施工/建筑状态解释 |
| Game/Assets/DarkNights/Scripts/View/WorksitePresentationBehaviour.cs | R2；资源点变体、存量耗尽和局部反馈 |
| Game/Assets/DarkNights/Scripts/Editor/CRefactorContentUpgrade.cs | R1/R2；有限资产升级、预检、保存与报告 |
| Game/Assets/DarkNights/Scripts/Tests/WorldSessionLifecycleTests.cs | R1；角色、装配和清理合同 |
| Game/Assets/DarkNights/Scripts/Tests/EntityPresentationTests.cs | R2；绑定、状态解释和预览隔离合同 |

新增 .meta 由 Unity 生成；不手工分配 GUID。Behaviour 工厂新增四个具体类型，抽象基类不应作为可装配类型注册。

### 5.2 修改的 22 个已有源码/工程文件

| 路径 | 修改内容 |
|---|---|
| Game/Assets/DarkNights/Scripts/Runtime/Network/SessionNetwork.cs | 移出服务创建/推进/释放，持有当前行为引用，保留网络连接适配 |
| Game/Assets/DarkNights/Scripts/Runtime/Network/SessionObjectLink.cs | 事件式装配、角色停止与客户端观察适配，删除 Update 发现循环 |
| Game/Assets/DarkNights/Scripts/Runtime/Network/SessionServer.cs | 生命周期幂等性/所有者合同与 R3 引用更新，保留业务与发布语义 |
| Game/Assets/DarkNights/Scripts/Runtime/Framework/FormalObjectCatalog.cs | 校验新增会话行为注册和 WorldSession 合同；不引用 View 类型 |
| Game/Assets/DarkNights/Scripts/Runtime/Session/SessionAuthority.cs | 仅 R3 namespace 引用和职责注释；权限、队列与恢复算法保持 |
| Game/Assets/DarkNights/Scripts/Runtime/Save/SessionStorage.cs | StorageRequest 移动后的引用 |
| Game/Assets/DarkNights/Scripts/Entry/GameSessionStartupModule.cs | 参数和生命周期接线顺序；继续通过现有应用上下文装配 |
| Game/Assets/DarkNights/Scripts/Entry/SessionEntityViews.cs | 绑定/创建登记、分发、公共时间线、目标索引与异步清理 |
| Game/Assets/DarkNights/Scripts/Entry/SessionUiController.cs | 复用现有 InputIntent 执行入口接个体回调，不另建命令链 |
| Game/Assets/DarkNights/Scripts/Entry/SessionPlacementView.cs | 显式保持预览实例不 Bind 活实体 |
| Game/Assets/DarkNights/Scripts/Entry/SessionEffects.cs | 残骸使用被动表现实例，释放时满足新行为清理合同 |
| Game/Assets/DarkNights/Scripts/Entry/PlayerPerformanceCapture.cs | R3 Diagnostics 引用及本次前台采样所需的最小适配 |
| Game/Assets/DarkNights/Scripts/View/NativeVisual.cs | 抽离类别状态解释，保留序列化资源与绘制操作 |
| Game/Assets/DarkNights/Scripts/Editor/FormalObjectContentSetup.cs | 更新空目录首版合同及会话验证，不重跑覆盖现有资产 |
| Game/Assets/DarkNights/Scripts/Editor/NativeArtSetup.cs | 更新未来空目录安装的三类行为配置，不重生成素材/动画 |
| Game/Assets/DarkNights/Scripts/Editor/SessionNetworkSetup.cs | 更新 Link 显式绑定及验证，保留 NetworkObject/Sender |
| Game/Assets/DarkNights/Scripts/Editor/SessionLifecycleProbe.cs | 扩展真实角色停止、重开与实例清理验证 |
| Game/Assets/DarkNights/Scripts/Tests/FormalObjectContentTests.cs | Definition/绑定/生成注册断言 |
| Game/Assets/DarkNights/Scripts/Tests/NativeArtTests.cs | 三类表现装配与冻结原生外观断言 |
| Game/Assets/DarkNights/Scripts/Tests/NativeEffectTests.cs | 残骸与新个体行为互不接管的回归 |
| Game/Assets/DarkNights/Scripts/Tests/SessionEventScenarios.cs | R3 投影事件 namespace 更新，原断言保持 |
| tools/CoreRegression/CoreRegression.csproj | 显式纳入移动的纯类型，保持排除 Unity 网络适配 |

个体行为类型的运行验证放在 Entry/Editor/Tests；不能为方便在 Runtime/FormalObjectCatalog 引用 DarkNights.View，从而破坏程序集方向。

### 5.3 移动的五个已有文件

| 当前路径 | 目标路径 |
|---|---|
| Game/Assets/DarkNights/Scripts/Runtime/Session/SessionProjector.cs | Game/Assets/DarkNights/Scripts/Runtime/Network/SessionProjector.cs |
| Game/Assets/DarkNights/Scripts/Runtime/Session/SessionEventJournal.cs | Game/Assets/DarkNights/Scripts/Runtime/Network/SessionEventJournal.cs |
| Game/Assets/DarkNights/Scripts/Runtime/Session/SessionStorageRequest.cs | Game/Assets/DarkNights/Scripts/Runtime/Save/SessionStorageRequest.cs |
| Game/Assets/DarkNights/Scripts/Runtime/Session/SessionMeasurements.cs | Game/Assets/DarkNights/Scripts/Runtime/Diagnostics/SessionMeasurements.cs |
| Game/Assets/DarkNights/Scripts/Runtime/Session/MeasurementSeries.cs | Game/Assets/DarkNights/Scripts/Runtime/Diagnostics/MeasurementSeries.cs |

以上是源 → 目标；每一项同时移动既有 .meta。保留原类名，namespace 随职责目录更新；新 Diagnostics 目录只在实际移动时建立。

### 5.4 修改的 17 个资产

| 对象组 | Definition 精确路径 | 目标行为 |
|---|---|---|
| WorldSession | Game/Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset | 追加 CampSessionBehaviour，保留 WorldSessionBehaviour |
| Worker | Game/Assets/DarkNights/Res/Objects/Worker/Worker.asset | ActorPresentationBehaviour |
| Spearman | Game/Assets/DarkNights/Res/Objects/Spearman/Spearman.asset | ActorPresentationBehaviour |
| Archer | Game/Assets/DarkNights/Res/Objects/Archer/Archer.asset | ActorPresentationBehaviour |
| Zombie | Game/Assets/DarkNights/Res/Objects/Zombie/Zombie.asset | ActorPresentationBehaviour |
| Ghoul | Game/Assets/DarkNights/Res/Objects/Ghoul/Ghoul.asset | ActorPresentationBehaviour |
| Armored | Game/Assets/DarkNights/Res/Objects/Armored/Armored.asset | ActorPresentationBehaviour |
| House | Game/Assets/DarkNights/Res/Objects/House/House.asset | BuildingPresentationBehaviour |
| Tavern | Game/Assets/DarkNights/Res/Objects/Tavern/Tavern.asset | BuildingPresentationBehaviour |
| Barracks | Game/Assets/DarkNights/Res/Objects/Barracks/Barracks.asset | BuildingPresentationBehaviour |
| Farm | Game/Assets/DarkNights/Res/Objects/Farm/Farm.asset | BuildingPresentationBehaviour |
| Tower | Game/Assets/DarkNights/Res/Objects/Tower/Tower.asset | BuildingPresentationBehaviour |
| Trees | Game/Assets/DarkNights/Res/Objects/Trees/Trees.asset | WorksitePresentationBehaviour |
| Stone | Game/Assets/DarkNights/Res/Objects/Stone/Stone.asset | WorksitePresentationBehaviour |
| Iron | Game/Assets/DarkNights/Res/Objects/Iron/Iron.asset | WorksitePresentationBehaviour |
| Farmland | Game/Assets/DarkNights/Res/Objects/Farmland/Farmland.asset | WorksitePresentationBehaviour |

另外修改 Game/Assets/DarkNights/Res/Objects/WorldSession/WorldSession.prefab 的显式 Link 绑定。所有 16 个 Definition 的 GUID/Key/PrefabRef/NetType 保持；Worker.asset 只追加行为配置，不覆盖其已有用户序列化差异。

其余 15 个对象 Prefab（与表中对象同目录、同名 .prefab）默认只验证，不改层级、原点、组件资源字段或动画。若发现真实缺失绑定，升级工具应列出具体差异并停止该项，修复范围记入本批清单，不能用重新生成 Prefab 解决。

### 5.5 生成、依赖及文档

- 新 Behaviour/绑定由现有 YYGC.BehaviourRegistry、ViewBinding、DI Roslyn 生成器产生。编译器生成结果按当前导出/诊断机制检查，不捏造固定物理路径，不手改输出。
- Game/Assets/Scripts/Generated/IStateData.generated.cs 和 INetworkCommand.generated.cs 是现有文件；本批未新增 wire 类型，预期无语义变化，作为审查项而非手写修改目标。
- .asmdef 预期不变；生成程序集访问出现实际错误时再处理。View 不为本次分发增加 Runtime 引用，也不需要每实体 R3 订阅。
- Packages/manifest.json、packages-lock.json、.deps/YYGC 与用户 YYGC 预期不改。若框架回调/绑定生成确有无法通过现有窄适配解决的缺口，先在隔离 checkout 复现，再提交可重现依赖并逐文件更新 YYGC_CHANGES；不能预先将此项标为已修复。
- 实施完成后更新 DEVELOPMENT、ARCHITECTURE、MIGRATION_PLAN、FORMAL_NETWORK、NATIVE_ART、SESSION_AUTHORITY、SESSION_PROJECTION 和本计划状态；新验证结果写独立证据，旧通过记录保持。
- 预期无改动但必须检查的资产：Pinewatch 场景、ObjectDefinitionDatabase、DefaultPrefabObjects、Addressables 分组/设置和构建配置。出现写入需说明新输入或原因。

数量口径：22 个已有源码/工程文件修改、8 个新脚本、5 个原脚本移动、16 个 Definition + 1 个 Prefab；不含伴随 .meta、生成输出、文档和日志。该数是审查基线，实施时新增范围要明确记录。

## 6. 影响面与风险

| 范围 | 影响等级 | 主要风险 | 控制与验收 |
|---|---|---|---|
| 会话启动、停止、重入 | 高 | 双重服务、迟到回调清除新房间、只停 Host 客户端误停服务器 | 双条件启动、实例/代次校验、幂等停止、真实角色回调验证 |
| 对象表现与资源装配 | 中高 | 未绑定预览抢占活实体、转职后旧状态、生成绑定失败 | 三类行为、绑定门槛、原 visual 键与序列化字段保留、15 类原生复核 |
| 权限/去重/Ready/epoch | 高回归影响、低预期算法改动 | 生命周期变更绕过队列或提前 Ready | 原入口不变，多进程并发、重复/非法请求、晚加入和加载矩阵 |
| 存档 | 中 | 停止时异步任务提交到旧世界 | 既有票据/冻结机制保留，活跃施工训练箭矢组合恢复 |
| 网络格式 | 低预期格式影响、中高回归影响 | 行为装配影响注册或发布时机 | 保持唯一 Stateful 索引与协议 5；比较注册、固定投影编解码和握手 |
| Core 玩法/旧档 | 低预期源码影响、高敏感度 | 顺手优化改变顺序、随机数或时机 | 本批不改 Core；冻结规则与旧档回归必须通过 |
| 性能 | 待测 | 新回调/索引分配、重复绘制、未释放订阅 | 每帧统一分发，索引只在新帧更新，前台同负载 CPU/GC/载荷比较 |
| 框架与构建 | 中 | 新生成类型未进入 Player/裁剪合同 | Editor 注册验证后实际 Mono 启动；IL2CPP 单列待确认 |

存档规则与布局摘要的现有算法不因表现重构改变，不升级存档格式。协议 5 wire 不变也不等于新旧发行包混联已验收；本批验收默认双方使用同一新产物，需核对框架定义握手的实际摘要变化。

## 7. 验证矩阵与停止条件

| 阶段 | 现有入口/新增检查 | 通过要求 |
|---|---|---|
| 静态边界 | tools/ArchitectureGuard、tools/CoreBuild | C#9/netstandard2.1；Core 无引擎依赖；View 无可写 Logic/Runtime；长度/注释/生成隔离合规 |
| 冻结规则与会话 | tools/CoreRegression；原 Session*Scenarios、规则/旧档夹具 | 原断言通过，不重生成冻结结果；移动纯类型仍可独立编译 |
| 生命周期 | WorldSessionLifecycleTests、SessionLifecycleProbe | 角色组合、回调次序、延迟装配、三次开关、失败与旧回调；只存在一个权威服务 |
| 表现与资产 | EntityPresentationTests、FormalObjectContentTests、NativeArtTests、NativeEffectTests | 16 定义正确装配，15 类原生 Prefab 的编辑/保存/重开/Play；预览与残骸无活实体绑定 |
| 真实多进程 | tools/test-game-delivery.ps1 及其依赖脚本 | 最终 Mono 同一产物：启动、Host+独立客户端、并发、活跃载入、九组网络矩阵、战斗、三夜、容量上限 |
| 已有网络业务语义 | test-game-concurrency / session / recovery / network-matrix | 并发扣款/工位、重发去重、非法目标、HostOnly、Host单次执行、暂停、晚加入、重连与加载 epoch |
| 画面 | test-game-visual.ps1，两分辨率；人工可见前台复核 | 角色姿态、采集耗尽、施工、训练、残骸、预览、头像/选区/状态条不回退；既存 M5 缺陷另列 |
| 性能 | PlayerPerformanceCapture、已有 Measurements 加 OS 侧有效采样 | 固定同负载、完整窗口 p95/p99、CPU/GC/内存/网络/活动对象数；缺失计数器标不可用 |
| 额外平台 | IL2CPP、双机器 LAN | 仅在授权/设备具备时执行；Mono 通过不得代替 |

性能接受方式：R0 在看到重构结果前冻结负载与容差；先报告基线噪声。固定输入的规则/投影必须相同，网络注册与 wire 不应无意膨胀。若前台帧时、模拟/投影耗时或稳态分配出现超出基线噪声的回退，必须定位并修复或明确保留未通过状态，不能用综合评分替代测量。

失败即停：缺注册/绑定、双实例、停止后仍推进、冻结规则差异、旧 epoch 污染、预览启动活逻辑、异常覆盖美术资源中的任一项失败，停止后续构建或进程矩阵。

长任务只提交一次，保留任务 ID；有界等待，每次不超过 60 秒，状态轮询通常 20–30 秒并退避。未变化不读取完整日志。每个已就绪源码/资产批次集中导入/生成，不能为每个文件启动 Editor。

## 8. 后续装备/Buff 扩展边界（不属于本批）

C 支持“个体拥有状态、系统统一处理”，但基础重构完成并不意味着装备/Buff 已实现。只有后续功能范围确定后才创建以下类型；这里列的是候选落点：

| 后续职责 | 拟新增/修改的具体落点 |
|---|---|
| 个体效果/装备状态 | Core/Logic/Entities/ActorEffects.cs、ActorEquipment.cs；Actor.cs 组合这些模块 |
| 共用规则 | Core/Logic/Systems/BuffSystem.cs、AttributeRules.cs；GameSession.cs 在冻结的明确阶段推进 |
| 只读定义 | Core/Config/BuffDefinition.cs、EquipmentDefinition.cs；现有 Catalog/Balance 与 Runtime/Config 解析同步扩展 |
| 客户端显示 | Core/ViewData/BuffViewData.cs、EquipmentViewData.cs；修改 ActorViewData.cs、SessionProjector.cs、ActorPresentationBehaviour.cs |
| 存档 | Core/Save/BuffSnapshot.cs、EquipmentSnapshot.cs；修改 ActorSnapshot、SnapshotMapper 和 Runtime/Save 的映射/校验/版本兼容 |
| wire/命令 | Runtime/Network/BuffWire.cs、EquipmentWire.cs；修改 ActorWire、ProjectionCodec、SessionCommand、SessionRequest、SessionOperation、SessionOperations 与对应生成注册 |

本表路径均以 Game/Assets/DarkNights/Scripts/ 为前缀。新类型不是必须全部创建：最终以实际持久字段、客户端可见字段和规则规模决定，避免为每个微小能力再加一层接口。

这一扩展会真正改变游戏内容、存档与网络合同，需要另行明确：Buff 叠加/刷新、周期伤害、暂停倍速、死亡/转职保留、最大 HP 变化、当前攻击前摇受属性变化的影响、装备所有权和支付。对它们的验证不能由本次“玩法不变”的回归代替。

## 9. 交付与回退

- 每批完成后更新实际文件清单、阶段结果、关键计数和日志路径；用小范围提交记录源码与资源的完整依赖批次，不推送。
- 提交前审查定义字段、Prefab 引用、生成注册和 .meta；用户原有修改单独保留，不执行整个文件的恢复/覆盖。
- 不在同一已运行会话中切换 A/C 双模拟作为回退。回退通过停止 Player/Editor Play 后切回上一已验证代码与匹配资产/构建产物实现，世界加载仍使用原保存合同。
- 任何必要 YYGC 改动单列文件、原因、落点、提交/锁定与验证结果，更新 YYGC_CHANGES；本方案阶段尚无 YYGC 修改。
- 本方案评审交付只验证文档路径、清单和差异，不运行 Unity、不声称新的 Core/Editor/Player 测试通过。
