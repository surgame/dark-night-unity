# C 方案实施与验收

2026-09-12，执行基线 `e34f2a28`。[原执行方案](C_REFACTOR_PLAN.md)的 R1–R4 已实施并完成复验。最终 Ready 修复后的同一 Mono 产物已通过基础双进程、并发、活跃加载、九组四进程弱网、战斗晚加入、三夜和容量上限。性能 A/C 对比留待后续；本批不推进 M5 既有画面修正，不构建 IL2CPP，不宣称双机器 LAN 或 M5 完成。

## 实际职责

- `CampSessionBehaviour : PooledBehaviour` 唯一创建、推进、释放 `SessionServer`。配置与框架服务端角色同时就绪才启动，重复角色回调不会创建第二个服务；框架 `IUpdate` 显式读取未缩放时间，保留既有 60 Hz 时钟与发布条件。
- `SessionNetwork` 保留连接、可信身份、命令转接与客户端 Ready 推进，`Server` 只转发当前行为持有的服务。异步加载后检查尝试代次，旧 Ready 失败只影响原尝试。
- `SessionObjectLink` 使用显式 `instance`／`synchronizer` 和装配事件，不再逐帧查找。客户端停止只解除观察，服务端停止立即关闭权威；当前对象消失会清空副本并使迟到实体创建失效。
- `ActorPresentationBehaviour` 持有只读角色副本和规则配置，解释动作、攻击动画时间映射、朝向及受击／训练颜色，提供明确 ID 的现有指令入口。`BuildingPresentationBehaviour` 解释施工、训练展示并提交修缮／派工；`WorksitePresentationBehaviour` 解释资源变体、耗尽和农田关联。
- `EntityPresentationBehaviour` 只负责生成的 `visual` 绑定、身份、Bind／Unbind 与回调清理。预览、残骸和 Editor 样本不 Bind 真实实体。`NativeVisual` 保留序列化字段与明确绘制操作。
- `SessionEntityViews` 保留工厂创建、公共时间线和统一分发。每次异步创建有独立票据，连接／epoch／职业失效不能复活旧结果；工位种类索引在新投影到达时更新。

Core 游戏源码、配置、波次、随机数顺序、攻击时机、存档格式和协议 5 wire 字段均保持。装备、Buff、技能未新增。并未增加每实体网络状态或网络订阅。

## 明确的会话语义

旧 `SessionAuthority.Disconnect(host)` 会立即关闭世界。现在增加显式 `closeHostedSession: false` 路径供本地观察端断开使用：撤销该参与者能力、拒绝其排队指令，服务端与其他玩家继续运行。默认调用仍关闭 Host 会话；UI 正常离房仍经 `SessionNetwork.Disconnect()` 释放权威及两种网络角色。

当前会话对象 Despawn 时，`SessionClient.Unobserve` 仅在对象引用匹配时清空副本并更新本地代次；旧对象 Detach 不影响后来的观察对象。WorldSession 仍只有一个 `StatefulBehaviour`，状态索引 0 不变；四个新增非 Stateful 类型使正式装配 Behaviour 种类从 7 增加为 11，命令／状态注册数仍为 2／1。

网络工厂在加载资源后、Spawn 前必须检查旧尝试。游戏先使用已锁定 YYGC 的 `FastInstantiator.GetOrLoadComponentAsync<ObjectView>` 完成缓存加载，再核对代次并进入现有定义工厂；锁定源码中工厂唯一 await 即该缓存加载，命中缓存后装配／Spawn 连续完成。此处复用已有 `DarkNights.Runtime` 友元访问，不新增框架 API、容器或调度框架。YYGC 升级若改变工厂的 await 边界，须重新验证此合同。

容量回归暴露既有 Ready 重试边界：每秒重试更换序号，可能持续丢弃较早请求的成功回执，出现服务端已 Ready 而客户端未 Ready。现同一 epoch 握手重试复用逻辑请求序号，成功后忽略迟到的拒绝；新 epoch 重置握手序号，连接和 epoch 过滤保持。新增 `SessionClientReadyTests` 四项回归；该修正改变运行输入，因此重新构建 Mono，Editor、真实 Play、启动及双进程通过。最终同一 Mono 产物随后完成容量及完整弱网复验，结果见下表。

## 资源与目录

16 个 Definition 只追加对应 Behaviour，WorldSession Prefab 只增加同步器引用。用户明确授权将 Farm Prefab 的错误键 `Farm` 恢复为 `visual`，最终与已提交基线相同；组件引用和美术字段未变。Worker 原有 `legacyIdAliases` 空值序列化差异保留，提交只纳入新增 Behaviour 字段。

五个已有文件连同 `.meta` 移动，GUID 保持：

| 文件 | 当前目录 |
|---|---|
| SessionProjector、SessionEventJournal | Runtime/Network |
| SessionStorageRequest | Runtime/Save |
| SessionMeasurements、MeasurementSeries | Runtime/Diagnostics |

CoreRegression 显式编译输入同步调整，不引入 Unity/FishNet 网络适配。Runtime 保持单个程序集，View 不引用 Runtime 或可写 Core/Logic。

`CRefactorContentUpgrade.Upgrade` 为已有资产的有限升级入口，先预检全部目标再写入。实际第一次写入全部成功，但 Farm 旧组件缓存导致写后校验失败；修正校验缓存重建后，对已写结果单独调用 `Validate()` 通过，未重复升级或重生成美术。首版安装器只更新未来空目录装配合同。

实际范围比原估算多涉及 `SessionClient.cs`、`SessionConnection.cs`、`SessionBoundaryScenarios.cs`、`SessionClientReadyTests.cs` 以及启动和容量测试脚本，分别处理副本失效、职责注释、观察端断开回归、Ready 边界、新注册数和失败诊断。`GameSessionStartupModule` 与 `SessionStorage` 无须修改；现有接线与移动后的同目录引用已满足合同。

## 验证记录

本批原始记录位于 `Artifacts/c-refactor/`，实施摘要见[实施证据](evidence/c-refactor-2026-09-12.json)，最终矩阵见[复验证据](evidence/c-refactor-final-matrix-2026-09-12.json)。旧回归夹具与历史通过记录未重生成。

| 检查 | 当前结果 |
|---|---|
| CoreBuild / C#9 / netstandard2.1 | 通过，0 警告／错误 |
| 冻结规则、旧档与会话回归 | 1361 项，0 失败 |
| 架构守卫 | 223 个手写文件、10 自测、0 错误 |
| Unity Editor 全程序集 | 最终 95/95；含新增 28 项个体表现、5 项会话生命周期、4 项 Ready 用例 |
| 资源合同 | 16 定义、15 类 Prefab 副本保存／重开及生成装配通过；32 段动画、583 关键帧保持 |
| 真实 Play 生命周期 | 20/20；三次开关、单次支付、只停客户端、只停服务端、单独 Despawn 清理 |
| Mono 构建 | C 主体和 Ready 修复分别构建一次，共两次；最终 0 错误、21 警告，2465 个受检构建输入哈希不变 |
| 最终 Mono 基础检查 | 启动 6/6、独立 Host＋客户端 13/13 |
| 最终 Ready 修复后的 C 功能矩阵 | 并发 13/13、active-load 13/13、九组四进程弱网各 22/22、战斗晚加入 9/9、三夜 17/17；全部复用 Entry DLL `AAAB680A…C603517` |
| 画面基本回归 | 1280×800、1600×900 各 6/6，并人工查看日夜、菜单、暂停、帮助截图；M5 既有字体与布局问题未处理 |
| 合成容量上限 | 13/13；256 实体／1024 箭矢三客户端完整显示，四人保持 Ready；30.56 秒内发布从 272 增至 570，增量 298 > 门槛 150；真实 UDP 服务端至客户端 648,495,677 B |
| A/C 前台性能对比 | 用户指定后续执行；本批不作性能提升／无回退结论 |
| IL2CPP／第二台 Windows | 本批未执行，独立待验收 |

第一次 Unity 编译仅因新增测试遗漏 `BehaviourContext` 命名空间失败，修正后通过。第一次 Player 启动脚本仅因仍要求 7 类 Behaviour 而失败；已核实新增注册并更新为 11，复用相同 Mono 继续验证。失败记录保留，不计为通过。

## 最终复验结果

- [x] 最终 Ready 修复后的 Mono 容量上限已通过，保持原门槛；脚本记录起止发布／tick、测量窗口、四端 metrics 和真实 UDP 字节。
- [x] 最终 Ready 修改的并发、活跃加载、九组四进程弱网、战斗晚加入和三夜流程已全部复验通过。弱网共 198/198，实际丢弃 375 包、乱序 4,475 次。
- [ ] 独立建立可靠基线，执行同条件前台 A/C 性能对比；当前容量日志不构成前台性能结论。

M5 画面修正、IL2CPP 和第二台 Windows 联机仍为原项目的独立待办，不属于本轮新增重构内容。

YYGC 用户仓库、隔离依赖、依赖锁文件、生成 wire 注册、原始素材和正式布局无修改。本批不需要新增 YYGC 账本条目。
