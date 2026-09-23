# 权威会话业务层

2026-09-13 当前实现：SessionAuthority 接收已准备的 ObjectSession，统一访问 YYGC 业务能力；旧 GameSession／WorldState 和 GameSaveJson 已删除。U5 的命令、权限、去重、加载与生命周期回归已通过；当前合同见[技术架构](../ARCHITECTURE.md)、[v2 存档](../SAVE_FORMAT.md)和[覆盖迁移](YYGC_UNIFIED_TEST_COVERAGE.md)。下文按日期保留旧实现记录，其中旧世界创建与旧档导入不再适用。

2026-09-12 C 重构已实施，按用户要求收尾并先交付架构验收。Core 1361、最终 Editor 95、Play 生命周期 20、Mono 启动 6 和双进程 13 项通过；容量检查未签署通过，Ready 修复后的完整弱网矩阵及性能对比留待下次。实际合同、此前通过记录和待办见[C 实施记录](C_REFACTOR_IMPLEMENTATION.md)。M5 既有待验收项保持，下文历史批次的当时边界保留。

2026-09-11，第七批。`Runtime/Session/SessionAuthority` 已实现普通 C# 会话服务，并通过独立回归与 Unity Editor 测试。这是 M2 的业务基础，尚未装配进 Bootstrap、WorldSessionBehaviour 或网络命令处理器；本页不构成正式联网／Player 验收。证据见[本批记录](evidence/session-authority-2026-09-11.json)。

## 状态归属与调用顺序

构造时注入真实 GameCatalog 与场景派生 LevelLayout，服务自行创建并独占 GameSession，不接受外部可写世界，也不对外返回它。`CaptureWorld()` 只给权威存储／测试生成冻结恢复快照，含 RNG；不能作为客户端展示投影。Core 数值、布局、AI、支付、攻击与随机调用顺序未修改。

`Connect(slot)` 是可信服务端适配入口：完成握手／恢复凭据认证后，服务端决定槽位 0–3，槽位 0 固定为房主。返回不能公开构造的 SessionConnection；Submit 通过对象引用匹配本实例当前连接，而非比较请求中的 PlayerId。替换同槽连接增加 Generation，原引用及其排队请求立即失效；来宾断开保留世界与已有任务，房主断开关闭会话。当前未实现 FishNet 连接映射、凭据签发／超时，不能直接把 Connect 暴露为客户端 RPC。

网络适配下一步必须复用已锁定 YYGC 的 `NetworkCommandContext.IsServerExecution`、`SenderConnection` 及 Processor 已验证的 sender ownership。Host 经同一 Submit 路径，不再额外调用 Core。池化 wire 回调结束前复制成 SessionRequest；这里没有增加 transport、DI 容器、事件框架或 VitalRouter 业务路由。

`AcknowledgeReady(connection, epoch, appliedRevision)` 检查当前能力、epoch，以及连接建立时 revision 到当前 revision 的范围。实际内容握手和完整投影应用证明仍由待实现的网络适配负责；这个方法本身不能证明客户端加载了场景或应用了快照。初始世界及加载后的世界等待房主重新 Ready 才推进；不等待所有来宾。房主已启动后，普通来宾加入不阻塞模拟。

所有写入、快照捕获及加载完成限创建线程。外层服务端时钟每个 60 Hz 调度点调用一次 Tick，并自行积累实际时间、保留积压；本批没有 Unity Update 驱动。Tick 先处理请求，再调用 `GameSession.Advance(1.0 / 60)`，倍速仅由 Core 乘一次。serverTick 在暂停和 Loading 时仍增加；暂停时合法指令、支付与分配继续，世界时间及生产停止。revision 标记已执行操作和模拟发布点，可以有间隔；它不是模拟 tick，也不是网络序列号。

## 请求、权限与结果

SessionRequest 不是 wire DTO。包含协议版本 1、epoch、PolicyRevision、正数 Sequence 和明确参数，不包含身份、资源、伤害或全局选择。输入列表最多 256 项，种类字符串最多 64 字符；复制时用显式循环保留每个 ID 首次出现的顺序。未知操作、无关负载字段、非法数值、无效／敌方单位和目标在修改世界前拒绝。

| 操作 | 参数 | SharedCamp 来宾 | HostOnly 来宾 |
|---|---|---|---|
| IssueOrders | 有序 ActorIds、TargetId（0 为移动）、X | 允许 | 拒绝 |
| PlaceBuilding | kind、X、候选 ActorIds（空列表沿用自动选工人） | 允许 | 拒绝 |
| TrainActors | 有序 ActorIds、spearman／archer | 允许 | 拒绝 |
| Recruit／Repair | 无参数／明确建筑 ID | 允许 | 拒绝 |
| SetPaused／SetSpeed／StartNight | 0或1／1或2／无参数 | 拒绝 | 拒绝 |
| SetControlMode／BeginLoad | 控制模式／槽位0–9 | 拒绝 | 拒绝 |

房主可执行所有合法操作。策略切换与普通操作位于同一 FIFO 队列；实际切换增加 PolicyRevision，不改变 epoch。执行时再检查当前策略版本，旧策略未执行请求返回 PolicyChanged；最新版本的越权请求返回 PermissionDenied。已执行工作不取消、不退款。相同模式设置不增加 PolicyRevision。

每连接最多 16 条待处理请求；全局最多 64 条，包含已断线／替换连接遗留的待处理项，重连不能绕开上限。每连接缓存最近 64 条完成结果及最多 16 条 pending 参数。序号只在入队时推进高水位：允许向前跳号；未缓存且不大于高水位的序号明确过期，不补执行遗漏的旧序号。该约束配合首版可靠有序命令入口使用。网络解码字节数、接收频率和超时限制仍需网络层实现。

缓存内同序号且规范化参数相同返回原冻结回执，包括业务失败；改参返回 SequenceConflict。过期结果返回 SequenceExpired。QueueFull／NotReady 等入队前拒绝不消耗序号。缓存命中先于当前策略复查，因为回执只报告已经发生的结果，不再写世界；旧连接／旧 epoch 则在缓存查询前拒绝。

Tick 返回本轮定向回执列表，Submit 重发可查询缓存。回执带发起槽位／连接代次、请求 epoch／序号、执行时 epoch／revision／PolicyRevision／serverTick，以及结果码、受影响数量和单个新建／修缮 EntityId。训练保持原顺序的部分成功，并返回成功数量；逐单位错误详情、Core 中文提示和世界音效／事件投影仍待接入，不能把当前回执写成完整 UI 反馈协议。回执不能代替下一份客户端只读世界投影。

## 加载票据

房主先按正常队列提交 BeginLoad，Value 为有限槽位。其 Applied 回执只表示取得加载票据，不能显示“加载成功”。服务端存储协调器保留这一请求的槽位和原回执对象，再读取指定文件；目前文件任务编排尚未实现。加载开始后的同批排队业务返回 Loading，新业务也被拒绝，Tick 与服务通信可继续。

读取失败／取消时回到创建线程调用 `CancelLoad(ticket)`。读取成功后调用 `CompleteLoad(ticket, json)`；只接受本次加载签发的同一个回执引用，禁止将客户端字段重新构造成票据。GameSaveJson 在临时世界中校验格式、实际规则／布局摘要、随机算法和全部关系。失败抛出原异常、解除 Loading，原世界及 Ready、暂停／倍速保持；成功才替换世界、增加 epoch、清空请求窗口和 Ready、重置 revision，保留当前房间控制模式／PolicyRevision。存档自身暂停／倍速及随机／箭矢状态完整恢复。

完成、取消、失败或房主退出后旧票据失效；失效票据不能解除另一次加载。BeginLoad 重发始终只是原回执，不会在加载失败后自动重启；重新尝试须使用新序号。成功后旧 epoch 命令和 Ready 不能访问新世界。

存储协调器还需接入既有 GameSaveStore 的固定槽位 IO、后台任务完成通知、错误提示及超时；目前 CompleteLoad 接受经边界校验的 JSON 文本，未把 GameSaveStore.Load 返回的可写世界交给会话。保存命令、重开、明确旧档导入 UI 及网络 Loading／Ready 通知也未装配。旧档测试先通过独立 ImportLegacy 转为新格式，再使用相同加载路径。

## 验证与边界

后续第八批已增加 CaptureProjection、冻结实体／HUD 副本、WorldReplica 及未缩放时间累积 SessionClock，详见[展示副本与时钟](SESSION_PROJECTION.md)。网络、Entry 和 UI 仍未接线。下述 1260／37 项为本页第七批历史验证；最新累计为 1316 项独立回归及 40 项 Editor 检查。

独立回归 **1260/1260**，其中新增会话 **96 项**；架构守卫 **109 个手写文件／10 项自测**零错误；Unity `6000.4.9f1` 最终完整 Editor 程序集 **37/37**。12 秒采集、住宅施工与训练对照直接 Core 的完整世界，加载冻结旧档并继续 20 秒对照冻结 Godot 结果。还覆盖并发建造库存不足、共享工位、部分训练支付、Host 重发、策略切换、过期请求、非法输入、四人队列上限、重连旧能力、跨线程拒绝、暂停／倍速、取消／坏档／陈旧加载完成及房主退出。

没有新增场景、Prefab、生成 wire 类型或网络路径，没有运行 Player、PlayMode、独立 Host＋客户端、弱网或美术检查。当前连接对象模拟只证明业务层判断；网络认证、真实投影 Ready、晚加入、重连凭据、网络去重及四连接带宽必须在正式多进程切片重新验收。Mono 与 IL2CPP 的历史探针结果不覆盖本批新增会话层；IL2CPP 仍需用户明确授权后才能构建验证。
