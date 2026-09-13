# YYGC 统一对象重构：回归覆盖迁移

本页记录 U5 的有效断言落点。旧运行世界、旧存档读取和过渡模拟接口已经从产品与测试工程删除；测试通过状态以[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)及各阶段证据为准。删除旧兼容测试不计作通过，历史测试总数不作为新架构的验收目标。

## 独立纯计算与 Unity 集成的边界

`tools/CoreRegression` 只编译真实 Core、只读 JSON 配置读取、`RuleScenario`、`PureRuleScenarios` 和 `RandomCompatibilityScenarios`，不编入 Runtime/Session 或 YYGC 替身。工具以 .NET 8 执行；游戏 Core 仍用 C# 9／.NET Standard 2.1。U5 当前纯计算结果为 **1,043/1,043**，原始报告保存在 `artifacts/yygc-unified/u5/core-regression.json`。

依赖状态、装配或生命周期的断言在 Unity 中执行。`UnifiedSessionScope` 预加载正式 Addressables 定义，以真实 Session Prefab、ObjectInstance、ObjectSessionContext、Behaviour 和状态池建立独立营地；它只管理测试租约，不持有可写业务状态。每个场景结束释放本场创建的世界、资源与更新管理器。对照的两局使用相同放置键，避免把身份随机差异误判为规则差异。

`EditorAssetLoading` 仅在 EditMode 暂时取消 AssetDatabaseProvider 的模拟加载延迟，退出即恢复；准备阶段限时 30 秒，仍使用真实 Addressables、Prefab 与工厂。UnitySetUp 使用编译器生成的迭代器保存恢复点。YYGC 的资源租约直接等待 AsyncOperationHandle，避免已完成资源还在等待延迟 Task 回调；不在测试中手工推进生产、战斗或生成调度器。

## 旧入口与有效断言的落点

| 原覆盖 | U5 的实际入口 | 保留的验收内容 |
|---|---|---|
| 独立 Core 规则与 RNG | `PureRuleScenarios`、`RandomCompatibilityScenarios`、`CoreRegressionTests` | 只读配置、数学与原 Godot 随机向量；不创建世界 |
| `EconomyScenarios` | `UnifiedRuleScenarios.Economy` | 支付原子性、资源非负、初始人口／容量、采集时间与饥饿 |
| `WorkTrainingScenarios` | `UnifiedRuleScenarios.WorkAndTraining` | 工位独占、撤销、建造、训练、取消退款与职业替换 |
| `TimeCombatScenarios` | `UnifiedRuleScenarios.TimeAndCombat` | 单入口倍速、暂停、战斗伤害与箭矢时序 |
| `CampaignScenario` | `UnifiedCampaignTests` | 一倍速三夜和无人照料逐项对照冻结 Godot 报告；二倍速完整结果及胜负后停止 |
| 旧 `SaveMigrationScenarios` 的读取能力 | 已删除 | Godot 旧档、Unity v1 的成功读取不再是产品要求；冻结文件保留供版本拒绝及研究 |
| 新档字段、关系和内容约束 | `GameSaveScenarios`、`UnifiedTransactionTests` | v2 冻结往返、活跃状态恢复、坏字段／身份／摘要拒绝、失败保留世界 |
| 文件存储 | `GameSaveFileScenarios` | 槽位、首次保存／替换、锁冲突、取消、严格 UTF-8、超限、并发保存和孤立临时文件 |
| 命令、权限、去重 | `SessionCommandScenarios`、`SessionBoundaryScenarios` | 明确参数、可信连接、HostOnly、旧策略、非法目标、重发及改参重发 |
| 会话与加载生命周期 | `SessionLifecycleScenarios`、`WorldSessionLifecycleTests` | Ready、连接代次、加载票据、epoch、退出撤权、配置／角色次序、失败释放和重新装配 |
| 投影、副本和时钟 | `SessionProjectionScenarios`、`SessionReplicaScenarios`、`SessionClockScenarios` | 深度冻结、乱序／旧 epoch 拒绝、插值边界、未缩放时钟、暂停与积压 |
| 事件与后台存储协调 | `SessionEventScenarios`、`SessionStorageScenarios` | 事件去重、结果窗口、异步加载提交、取消和旧连接失效 |
| 真实序列化与 Ready 回执 | `ProjectionWireTests`、`SessionClientReadyTests` | MemoryPack 编码／解码、完整身份表、坏 GUID 拒绝、池归还隔离、Ready 迟到与重试 |

`SessionRegressionTests` 把其中 13 组场景按名称执行，逐条记录断言名、结果和数量；输出为 `artifacts/yygc-unified/u5/scenarios/*.json`。其余集成测试保留 NUnit XML，以便识别准备失败、断言失败和未执行项。

U5 最终整批 **134/134** 通过，用时 54.02 秒；13 组场景共 **291** 项有效断言全部通过。此前准备失败及取消的批次仍保留，但不计入通过。完整结果为 `u5/editor-134-passed.json`／`.xml`；针对加载问题的 26 项会话验证为 `u5/session-26-passed.json`／`.xml`，用时 3.07 秒。

## 真实对象、制作和多进程覆盖

| 入口 | 边界 |
|---|---|
| `UnifiedObjectAssemblyTests`、`UnifiedObjectRetirementTests`、`UnifiedObjectFactoryTests`、`UnifiedSliceTests`、`UnifiedTransactionTests` | 注入、激活、退休、池化、严格能力校验、跨对象原子提交、通知重入及异常清理 |
| `UnifiedGameplayTests` | 15 类定义、初始 ID、农田派生、转职、完整生产／战斗与状态映射 |
| `UnifiedObjectPlayTests` | 真正 PlayerLoop 的生成调度器；开关 Domain Reload 各重复进入／退出 |
| `UnifiedSlicePlayTests` | 正式 Bootstrap／Pinewatch／Host；原场景对象重接、v2 保存恢复、重开、退出及双重实例检查 |
| 制作、布局和美术测试 | Prefab／场景修改、保存、重开、绑定／GUID、复制放置键、15 类资源及原始 551 项素材哈希 |
| U6 的同一 Mono Player | 双进程／四进程并发、恢复、九组 UDP 弱网、第二夜晚加入、三夜、容量、画面与有效性能记录 |

纯序列化测试和 Host 单窗口不能替代多进程验收。IL2CPP 仍需用户明确确认，双机器 LAN 需真实第二台主机；没有证据时分别保留未验收状态。

## 冻结证据与执行方式

`tools/CoreRegression/Fixtures` 中的内容摘要、RNG、原三夜／无人照料报告和旧档保持原字节。新代码的输出另存，不用当前结果重生成旧期望。场景初始 ID、规则数值、素材字节和攻击时点的有效断言继续验收。

```powershell
dotnet run --project tools/CoreRegression -- .
dotnet run --project tools/ArchitectureGuard -- .
unity command run_tests --mode editor --filter DarkNights.Tests --filter_type assembly --async_tests --project-path Game --json
```

同一 Editor 的测试、导入与构建串行执行。首次完整测试保留全部 XML；修复后只重跑受影响组，并汇总有效覆盖。阶段结束执行相应 `dotnet clean`，保留报告及后续验收仍需复用的 Player，不把未执行或被取消的检查记为通过。
