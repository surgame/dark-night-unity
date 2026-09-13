# Dark Nights Unity 开发约定

先读 [README](README.md)、[移植方案](docs/MIGRATION_PLAN.md)、[开发执行计划](docs/DEVELOPMENT.md)、[技术架构](docs/ARCHITECTURE.md) 和[联机设计](docs/MULTIPLAYER.md)。正式玩法、15 类原生对象、UI、四人联机和恢复主体已有实现，M5 尚未完成。按 [YYGC 统一对象重构计划](docs/YYGC_UNIFIED_REFACTOR_PLAN.md)推进，U0–U5 已完成：旧模型和旧档入口已删除；U6 最新协议 7／YYGC `745f3d2` 通过 144 项 Editor／Play、同一 Mono 的 350 项自动检查及 240 秒容量检查 21 项。前台验收由用户明确暂缓，性能签署和被自动审批拦截的清理仍未完成。先核对[实施记录](docs/YYGC_UNIFIED_IMPLEMENTATION.md)与[性能验收](docs/YYGC_UNIFIED_PERFORMANCE.md)，不能把计划目录、接口和测试写成已完成实现，也不能把后台容量功能通过写成前台性能达标。

## 范围与工作区

- 游戏名称为 Dark Nights，当前内容为灰松谷一个关卡。2–4 人合作、共享营地已确认。联机入口与托管方式的估算假设见 README。
- `../projects` 是已提交的游戏基线，`../reference projects` 是研究与素材来源。Unity 的日常导入、构建和运行必须独立于这两个目录。
- `D:\Developer\YYGC` 是用户维护的框架仓库。用户于 2026-09-12 授权必要时更新 YYGC，并于 2026-09-13 明确允许针对能力限制或 BUG 升级适配：先核实具体缺口，优先在隔离 checkout 中验证，保留用户已有改动，不代为清理或覆盖。不因当前框架限制长期保留两套游戏对象／状态系统。游戏继续使用可重现的锁定依赖；完成后必须逐项列出 YYGC 的修改文件、原因、落点与验证结果，维护 [YYGC 改动账本](docs/YYGC_CHANGES.md)。历史记录中的 UGUIManager 暂存和 IDRegistry 备份不代表当前仍有这些差异。
- 2026-09-13 已实施 YYGC 统一对象路线，分支为 `codex/yygc-unified-object-migration`：运行实体与实例状态归 YYGC ObjectInstance／业务 Behaviour，Core 只保留纯算法、只读配置和数据合同。U5 已删除旧实体、旧世界及过渡入口，不重新引入并行运行模型；U6 最终验收完成前不宣称整个迁移已交付。
- 本次无需旧数据适配：正式游戏不再要求 Godot 旧档、Unity v1 存档、协议 6／5 客户端或旧 Kind／整数身份兼容；旧入口已退出。新格式自身的保存恢复、严格校验和原子性仍必须验收。保留当前人工资产、资源 GUID 和冻结玩法证据，不自动删除用户旧存档；独立 Sample 和 YYGC 其他使用者的兼容边界另行保留。
- 框架接入通过 UPM 和锁定版本完成。实验性修正使用隔离 checkout；本机 `.deps/` 不提交，取得稳定版本后提交可重现的依赖配置与锁文件。
- 不擅自改变既有数值、布局、波次、素材字节、文字或攻击时机。联机需要改变的权限和会话语义单独记录并验证。
- 用户于 2026-09-14 明确要求使用 Linear 色彩空间。纹理与作者颜色进入线性照明和混合；Godot OpenGL Compatibility 参考的 Gamma 色差单独记录，不为逐像素追平而改成 Gamma 或在纹理乘色前反向编码。

<a id="execution-efficiency"></a>

## 执行效率与批量操作

- 以一个可验证的功能切片或同类资源批次组织工作。执行前汇总已知输入、依赖顺序、输出和验收项；能够一起准备、一起执行的操作必须合并，避免逐文件、逐资源发起工具请求。批次保持可审查，不把整个移植合成难以定位失败的大任务。
- 已授权范围内连续完成准备、修改、生成和验证，不逐步向用户请求“继续”或重复确认。只有必须由用户补充的必要信息、超出授权范围或尚未获准的不可逆操作，才集中提出所需问题。
- 独立查询合并读取；同一任务已取得的文档、工具 schema、路径和状态按需复用。文件未变且没有新疑点时，不反复全文读取、枚举全部工具或输出完整日志；优先定向搜索、差异、摘要和失败上下文。
- 同批代码、程序集声明和可直接落盘的资源先集中写入，再统一触发所需导入／刷新，让 Unity 批量生成新增文件及目录的 `.meta`。不得每写一个文件就启动 Editor、刷新或重编译；自动导入已完成时复用结果，不叠加手动刷新。已有资产移动保留 `.meta`／GUID；不得为提速重建已有 `.meta`、随意分配 GUID 或绕过 Unity 的资源引用检查。
- 同批 Prefab、ObjectDefinition、绑定及 Addressable 条目，依赖已就绪时通过已有批量工具或一个有限的 Editor 操作顺序完成，集中保存、生成和检查。新增脚本必须先编译就绪，依赖导入结果的步骤必须等待结果；批处理暂停导入期间不得等待编译或读取尚未导入的资源，异常必须释放暂停状态。初始化仍只输出到指定空目录，批处理不放宽人工资源保护。
- “一次触发”指每个依赖已就绪的逻辑批次只主动提交一次；Unity 内部必要的导入、生成源码再编译及域重载不算重复请求。可由现有任务入口自动续接的步骤一起编排，不依赖 AI 多轮对话逐步驱动；不为凑成一次调用新建通用调度框架，也不跳过真实依赖屏障。
- 同一 Editor 的写入、生成、编译和构建串行执行，不用多个 MCP／CLI 请求竞争状态。长任务只提交一次，保留任务 ID，采用完成通知或有界等待；需轮询时通常间隔 20–30 秒，未变化则退避，单次阻塞等待不超过 60 秒。状态未变不重复取全量日志，不因等待超时重新启动仍在运行的任务。
- 按改动影响一次安排完整验证矩阵，每个后端／配置构建一次，再复用同一产物执行对应场景和基础／弱网检查。明确前置条件与失败即停规则；失败只修正并重跑受影响阶段。已通过检查只有在输入、配置或相关依赖变化，或出现新证据时才重跑，不以节省交互为由减少必要验收。
- 每阶段开始和编译／构建前检查相关磁盘剩余空间；阶段完成即清理本阶段可重建的中间编译产物、过期验证副本和不再使用的 Player 构建缓存，记录释放量与剩余空间。复用当前 Editor 导入缓存和后续验收需要的同一 Player，避免复制整套 Library；保留源码、人工资源、未保存场景备份、冻结夹具及报告。删除前核验绝对路径、链接及活动进程，不清理正在使用的编译目录；空间不足时先清理本任务可重建产物，再开始下一批。
- Player 构建默认优先使用 Mono 进行快速可运行验证。未经用户明确确认，不主动生成、覆盖或验证 IL2CPP Player；需要 IL2CPP 时先报告原因、范围和预计产物，再等待确认。确认后每个受影响配置只构建一次，并复用同一 IL2CPP 产物完成对应检查；Mono 通过不代表 IL2CPP 已验收，通过状态必须分别记录。
- 批量入口返回成功／失败、完成及失败项、关键计数和日志／产物路径；详细输出落文件，需要定位时再读取。批次完成后集中检查差异、引用和输出完整性；额外触发刷新、生成、重编译或构建时说明新增输入或失败原因，不把重复调用本身当作进展。

## 代码与结构

- Editor 沿用已锁定的 `6000.4.9f1`；游戏代码兼容 C# 9 和 .NET Standard 2.1。不能把 Godot 的 C# 12／.NET 8 配置直接带入。
- 职责目录与程序集按架构文档执行。Core 不引用 Unity、Godot、GameCore、FishNet、R3、VitalRouter、文件系统或表现资源；引擎、网络与存储适配放 Runtime。
- 正式代码放 `Assets/DarkNights/Scripts`，资源放 `Assets/DarkNights/Res`；采用 Core、Runtime、View、Entry 四个运行程序集，以及隔离的 Editor/Tests。View、Entry 分别承担原方案 Presentation、Bootstrap 的职责；不改现有 Bootstrap 场景或 Sample 类型名。不要为每个小文件再建一层服务接口或一个程序集。
- 代码目录使用 Config、Logic、ViewData、Save、Network 等直观名称，具体归属见架构文档；Scripts/Res 不加入命名空间。ViewData 仅为展示副本；现有 Core/Logic 世界按统一重构阶段退出，目标业务 Behaviour／State 放 Runtime/Objects，Core/Logic 仅留纯计算。不预建空目录和占位类型。
- 文件名与主要类型一致，命名空间与职责目录一致，根命名空间 `DarkNights`。不建立无限扩张的 Manager/Utils 汇总文件。
- 手写 C# 目标 150–250 行，硬上限 300 行，包含空行与注释；一文件一个主要命名类型。按职责拆分，不压缩语句或用多个 partial 文件绕过上限。
- YYGC／MemoryPack／绑定生成器要求的类型可以 `partial`，但每个类型仍只有一份手写主体。生成输出放明确目录，记录输入和重建方式；不手改生成结果。
- 每个类、record、struct、enum、interface 添加中等详尽中文 XML summary，说明职责、状态归属以及关键生命周期／不变量。注释不逐行翻译代码。
- C# 9 使用块级 namespace、普通构造函数和显式集合初始化；不能使用文件级 namespace、required、主构造函数、C# 12 集合表达式。
- 不复制整个 Godot 数学库或建立通用引擎抽象。只迁移实际使用的坐标、数学和可恢复随机数能力。
- 沿用 YYGC 现有启动、DI、视图、资源与 UI 接口，不另造并列的 DI 容器或全局事件框架。框架本身的历史长文件不在本次全面拆分范围内。
- 联机实现先读 [YYGC能力复评](docs/YYGC_REASSESSMENT.md)。优先修正并复用 Gateway/Sender/Processor、类型注册/序列化和会话 StatefulBehaviour/StateSynchronizer；游戏仅补权限、业务去重、投影、Ready、epoch和恢复。先验证可靠完整投影，测量后决定分块/拆流；局部后备网络适配必须有现有路径无法满足需求的具体证据。本地输入互斥复用 Interaction Sessions。
- 独立联机模板遵循 [LAN Sample 规范](docs/LAN_SAMPLE.md)：样板放 `Assets/Samples/LanCoop`，正式代码不反向引用；构建不覆盖样板原生资产。必要的 R3 用于状态订阅及生命周期；VitalRouter 只保留 YYGC 命令链必需的显式适配，新增业务路由／过滤器必须先说明具体必要性和调试路径。不要为模板预建 Steam、Lobby、多 transport 或房主迁移抽象。

## 权威状态与联机

- 经济、生产、单位 AI、伤害、箭矢、波次、胜负和随机数都只有一个权威写入者。状态由所属 YYGC 业务 Behaviour／实例 State 拥有；ObjectSession 组合会话能力，索引只引用对象，不另存一份状态。GameSession／WorldState 旧运行类型已删除。
- 客户端及 Host 的表现只读取冻结展示副本。StatefulBehaviour 接管权威状态时必须撤除旧状态所有者；网络 DTO、ScriptableObject 和展示副本不能再自行结算经济／HP。
- 业务命令带明确 EntityId 和参数，不能读取一个全局 SelectedIds／BuildKind 来代替请求参数。镜头、选择、悬停与建造预览属于各客户端。
- 身份从服务端连接上下文取得。请求中的 PlayerId、SenderObjectId、资源数量和伤害值都不构成授权；服务端验证共享营地权限、合法目标、范围、版本、序号和支付。
- Host 使用同一个验证与命令处理入口，保证一次输入只执行一次。客户端可以显示待确认反馈，不先结算支付或伤害。
- 共享控制使用会话级 SharedCamp / HostOnly 策略，服务端统一校验；关闭时同时限制直接命令、建造自动派工和训练等营地修改。切换增加 PolicyRevision，拒绝旧策略未执行请求，已生效任务继续；不通过转移小人的 FishNet 所有权实现。
- 模拟默认 60 Hz，倍速只在一个入口生效。暂停时网络、心跳、重连与 UI 继续运行；不用 `Time.timeScale = 0` 停掉整个服务进程。
- 稳定实体 ID、规则引用、场景放置键、YYGC Guid / Key、FishNet ObjectId、玩家连接 ID 分开。正式游戏已使用 DefinitionReference 与 GuidFirst／GuidV2；新重构不恢复旧整数兼容，独立 Sample 的 LegacyV1 单独保留。载入世界增加 epoch，拒绝旧世界命令和快照，保持当前房间控制模式。
- 快照是冻结数据；异步发送、插值、存档不能持有已归还池的状态引用。不要让 SessionScope 跨 await 或线程。
- 不默认采用锁步、回滚、ECS、并行模拟、每实体 NetworkTransform、房主迁移或专服集群。增加这些方案前给出具体需求和测量依据。

## Prefab、美术与内容

- 资源按对象／面板归组：`Res/Objects/Worker` 等目录集中所属 ObjectDefinition、Prefab、专用动画和材质；UI 同理。共用资源才放 Res/Shared，原始素材只保存一份，不因对象归组重复复制。
- Addressables 不要求游戏资源目录叫 Addressable／Addressables；Res 是项目约定，不自动注册资源。通过 Addressable 条目与分组管理加载，不使用特殊 Resources 目录存放 Addressable 资源。保留现有 AddressableAssetsData 配置位置，物理目录、分组、Address／Label 与 YYGC 定义身份分开。
- 正式对象通过 DefinitionReference 和 YYGC 定义／创建入口，由 ObjectDefinition.PrefabRef 驱动 Addressables；沿用组件绑定、注入与生成注册。检查绑定键、类型、引用及装配／池化／释放时机，不以 GetComponent、节点名或子节点索引兜底缺失绑定，不手改生成结果。详细合同见移植方案。
- 角色、建筑、工位、特效和 UI 使用原生 Prefab；Pinewatch 场景在未进入 Play 时能看到布局与外观。正式场景不得回退为一个空节点加全局创建脚本。
- Prefab、AnimationClip、场景和 Theme 等正式资源由人工维护；迁移脚本只在指定空目录输出首版样板，普通导入／构建不得覆盖美术编辑。
- 场景初始布局只有一份可编辑来源。派生关卡数据可在构建时生成，但必须能追溯到场景标记且不能反向覆盖它。
- balance/波次 JSON 保持规则唯一来源。ObjectDefinition 的共享配置保存内容映射和表现设置，不重复维护 HP、成本或实例进度。
- 角色根对齐脚底，ArtOffset、Facing、StatusAnchor、SelectionAnchor 与玩法占地分离；动画和物理碰撞不能结算游戏伤害。
- 原始 551 项素材放 Res/Art/Original，保持来源和 SHA-256；改图放 Res/Art/Custom。最近邻采样、关闭不需要的有损压缩，按适配方案转换坐标、原点和动画帧序。
- Editor 预览只产生表现，不启动网络或会话，不使用游戏随机数，不访问玩家存档。Editor API 和测试代码不得进入 Player 程序集。
- `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/` 提交；Library、用户存档、本机配置、密钥、依赖缓存不提交。Unity 场景启用文本序列化与 Visible Meta Files。

## 验证与完成

- 运行与改动相匹配的规则、场景或联机检查；不为文档修改伪造 Unity 构建通过记录。
- 联机验证包含独立进程中的 Host＋客户端。Host 单窗口、单个状态序列化测试不能替代联机验收。
- 核验并发扣款、共享工位、重发去重、非法目标、初始快照、晚加入、重连、丢包乱序、暂停、加载 epoch 和 Host 单次执行。
- 原玩法和旧档夹具是冻结证据，不用当前结果重生成来掩盖差异。旧档读取成功不再是统一重构的验收要求；仍有效的布局、规则、RNG 和时序断言迁到新对象入口，新格式完整恢复单独验收。跨引擎规则一致性与跨 GPU 画面近似分别验收。
- 美术相关变更完成 Prefab 编辑、保存、重开和运行检查；发布相关变更完成实际 Player 构建并在独立进程运行。
- 保持文档的“已完成／计划／待验证”清晰，更新执行状态、依赖和验证证据。提交前查看差异，提交当前仓库内的本次成果，不推送远端。
