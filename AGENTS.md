# Dark Nights Unity 开发约定

先读 [README](README.md)、[开发执行计划](docs/DEVELOPMENT.md)、[技术架构](docs/ARCHITECTURE.md) 和[联机设计](docs/MULTIPLAYER.md)。当前是评估与筹备仓库，不能把计划目录、接口和测试写成已完成实现。

## 范围与工作区

- 游戏名称为 Dark Nights，当前内容为灰松谷一个关卡。2–4 人合作、共享营地已确认。联机入口与托管方式的估算假设见 README。
- `../projects` 是已提交的游戏基线，`../reference projects` 是研究与素材来源。Unity 的日常导入、构建和运行必须独立于这两个目录。
- `D:\Developer\YYGC` 是用户维护的框架仓库。本轮只读评估；保留其已暂存 UGUIManager 和 IDRegistry 备份，不代为清理、提交或覆盖。
- 框架接入通过 UPM 和锁定版本完成。实验性修正使用隔离 checkout；本机 `.deps/` 不提交，取得稳定版本后提交可重现的依赖配置与锁文件。
- 不擅自改变既有数值、布局、波次、素材字节、文字或攻击时机。联机需要改变的权限和会话语义单独记录并验证。

## 代码与结构

- 目标兼容 Unity 6.2 的 C# 9 和 .NET Standard 2.1；确切 Editor 补丁版本先在 M0 锁定。不能把 Godot 的 C# 12／.NET 8 配置直接带入。
- 职责目录与程序集按架构文档执行。Core 不引用 Unity、Godot、GameCore、FishNet、R3、VitalRouter、文件系统或表现资源；引擎、网络与存储适配放 Runtime。
- 采用四个运行程序集 Core、Runtime、Presentation、Bootstrap，以及隔离的 Editor/Tests。不要为每个小文件再建一层服务接口或一个程序集。
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

- `GameSession/WorldState` 的可写实例只存在于权威端。经济、生产、单位 AI、伤害、箭矢、波次、胜负和随机数都只有一个写入者。
- 客户端及 Host 的表现只读取展示副本。不得将网络 DTO、ScriptableObject 或 `StatefulBehaviour` 再变成另一套经济／HP 状态。
- 业务命令带明确 EntityId 和参数，不能读取一个全局 SelectedIds／BuildKind 来代替请求参数。镜头、选择、悬停与建造预览属于各客户端。
- 身份从服务端连接上下文取得。请求中的 PlayerId、SenderObjectId、资源数量和伤害值都不构成授权；服务端验证共享营地权限、合法目标、范围、版本、序号和支付。
- Host 使用同一个验证与命令处理入口，保证一次输入只执行一次。客户端可以显示待确认反馈，不先结算支付或伤害。
- 模拟默认 60 Hz，倍速只在一个入口生效。暂停时网络、心跳、重连与 UI 继续运行；不用 `Time.timeScale = 0` 停掉整个服务进程。
- 稳定实体 ID、内容 ID、YYGC DefinitionId、FishNet ObjectId、玩家连接 ID 分开。载入世界增加 epoch，拒绝旧世界的命令和快照。
- 快照是冻结数据；异步发送、插值、存档不能持有已归还池的状态引用。不要让 SessionScope 跨 await 或线程。
- 不默认采用锁步、回滚、ECS、并行模拟、每实体 NetworkTransform、房主迁移或专服集群。增加这些方案前给出具体需求和测量依据。

## Prefab、美术与内容

- 角色、建筑、工位、特效和 UI 使用原生 Prefab；Pinewatch 场景在未进入 Play 时能看到布局与外观。正式场景不得回退为一个空节点加全局创建脚本。
- Prefab、AnimationClip、场景和 Theme 等正式资源由人工维护；迁移脚本只在指定空目录输出首版样板，普通导入／构建不得覆盖美术编辑。
- 场景初始布局只有一份可编辑来源。派生关卡数据可在构建时生成，但必须能追溯到场景标记且不能反向覆盖它。
- balance/波次 JSON 保持规则唯一来源。ObjectDefinition 的共享配置保存内容映射和表现设置，不重复维护 HP、成本或实例进度。
- 角色根对齐脚底，ArtOffset、Facing、StatusAnchor、SelectionAnchor 与玩法占地分离；动画和物理碰撞不能结算游戏伤害。
- 原始 551 项素材保持来源和 SHA-256，改图放 Authored。最近邻采样、关闭不需要的有损压缩，按适配方案转换坐标、原点和动画帧序。
- Editor 预览只产生表现，不启动网络或会话，不使用游戏随机数，不访问玩家存档。Editor API 和测试代码不得进入 Player 程序集。
- `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/` 提交；Library、用户存档、本机配置、密钥、依赖缓存不提交。Unity 场景启用文本序列化与 Visible Meta Files。

## 验证与完成

- 运行与改动相匹配的规则、场景或联机检查；不为文档修改伪造 Unity 构建通过记录。
- 联机验证包含独立进程中的 Host＋客户端。Host 单窗口、单个状态序列化测试不能替代联机验收。
- 核验并发扣款、共享工位、重发去重、非法目标、初始快照、晚加入、重连、丢包乱序、暂停、加载 epoch 和 Host 单次执行。
- 原玩法和旧档夹具是冻结证据，不用当前结果重生成来掩盖差异。跨引擎规则一致性与跨 GPU 画面近似分别验收。
- 美术相关变更完成 Prefab 编辑、保存、重开和运行检查；发布相关变更完成实际 Player 构建并在独立进程运行。
- 保持文档的“已完成／计划／待验证”清晰，更新执行状态、依赖和验证证据。提交前查看差异，提交当前仓库内的本次成果，不推送远端。
