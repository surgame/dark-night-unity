# 氧气候选：弱网与旧路线失败原因

2026-10-03。整批验证未通过。这里区分已确认的失败机制和仍待逐包定位的部分；没有通过传送、改图、放宽门控或减少丢包来消除失败。

## 弱网：反复作废地图基线，最终Ready超时

独立Host＋Client，同一静音Mono，200 ms RTT＋5% loss＋25 ms jitter。常规130/130；弱网一轮完成22项后到达新地图超时，另一轮初始连接超时。追加只读AMP1启动诊断也失败，隐藏窗口不变量正常，44次原有相机离屏渲染未使数据就绪。因此这不是缺少前台绘制导致的Ready失败。

### 已确认的代码链

1. [SessionTerrainNetwork.CreateTransport](../Game/Assets/DarkNights/Scripts/Runtime/Terrain/SessionTerrainNetwork.cs)订阅整个地图范围，背景基线另走可靠消息。启动诊断的完整地图流约1,587,068字节（AMP1诊断字节，不等于UDP线上总字节）。
2. 锁定依赖`.deps/YYGC-grid-business/AnyRuleD~/Packages/com.tsgame.anyrules.networking.fishnet/Runtime/FishNetMapTransport.cs`每100 ms调用Replica.Tick，失效时发Resync；每帧最多8包／预算64 KiB。Reliable并不保证大基线在应用超时前完成。
3. `com.tsgame.anyrules.networking/Protocol/ChunkReplicaStateMachine.cs`的pending commit从收到首个分片起累计Age；`ProtocolLimits.RetryTicks=120`，约12秒，收到后续分片不会清零Age。必须集齐Manifest列出的全部分片才原子安装，缺少任意块则Commit不推进。
4. 超时Resync调用ClearPending，丢弃未完整提交的分片；服务端OnServer调用MapInterestService.Subscribe，generation加1、清流队列／投影账本并重新发布整基线；客户端新Hello也清pending、把Commit归零并重置重试计数。
5. 该过程重复，客户端Commit始终0、数据始终未就绪；[SessionServer.Advance](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionServer.cs)按[SessionPeer.ReadyTimeoutSeconds=90](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionPeer.cs)主动Disconnect(true)。Host堆栈指向该检查，Client记录RemoteConnectionClose。失败发生在地图同步与Ready门控，不经过氧气或战斗死亡链。

只读桥观测（相对启动秒数，约1秒采样，非逐包精确时间）：

| 秒 | generation | Commit | pending／needsResync | 累计接收字节 |
| --- | --- | --- | --- | --- |
| 14.66 | 2 | 0 | 1／false | 183,140 |
| 25.94 | 2 | 0 | 1／true | 1,587,068 |
| 26.94 | 3 | 0 | 1／false | 1,588,668 |
| 39.25 | 3 | 0 | 1／true | 2,947,308 |
| 42.29 | 4 | 0 | 1／false | 3,175,348 |
| 54.57 | 4 | 0 | 1／true | 4,398,124 |
| 59.65 | 5 | 0 | 1／false | 4,762,028 |
| 71.89 | 5 | 0 | 1／true | 5,984,804 |
| 95.37 | 7 | 0 | 1／false | 8,003,320 |
| 104.65 | 0（断线重置） | 0 | 0／false | 8,818,504 |

首代客户端累计收到与基线相同的字节量，仍未安装；结合超时清pending的源码，支持“慢传输中途作废，迟到分片无法再组成完整提交”的机制，后续代次重复该过程。桥只有摘要及最近8项事件，不能精确指出首代哪个chunk或Manifest越过12秒边界，也不能区分可靠队列拥塞、分片重传、背景消息争用各占多少；底层因素仍待逐包诊断。

relay实际received10799、dropped536、forwarded10263、reordered7406，115.81秒。原始amp1.jsonl、Host／Client日志、报告和relay统计位于`artifacts/oxygen-removal-20261003/weak-diagnostic/network-20261003-032907-459055-mono-weak-2p-client/`。

上述地图协议、重同步逻辑和90秒Ready门控没有被氧气改动修改，AMP1 schema仍为2。当前实测证明候选弱网失败；没有重建移除前Player做同条件对照，不能宣称“基线弱网一定通过”或“移除前已实测失败”。已观察失败机制发生于地图同步层，与耗氧、氧气站、低氧返船没有直接调用关系。

后续优先评估基线进度感知超时／续传、基线与增量不同预算及可靠队列背压。只增加游戏90秒等待不能解决12秒作废循环；不能移除Ready门控允许未完整地图参与业务。相关实现涉及锁定YYGC的AnyRules网络包，本轮按AGENTS约束未修改，须先单独提出具体范围并取得同意。

## 旧路线：测试假设与当前生成地图、导航能力不匹配

两项用例共用[ExpeditionTests.Create](../Game/Assets/DarkNights/Scripts/Tests/ExpeditionTests.cs)、种子EXPEDITION-QUICK-01。当前[ExpeditionTerrainGenerator](../Game/Assets/DarkNights/Scripts/Core/Logic/Terrain/ExpeditionTerrainGenerator.cs)转交完整PlanetTerrainGenerator，明确不再改写独立泊位、通路或矿床；旧用例仍假设只向右就必定到第一矿房、矿工能走到矿床中心。

### 旧主角矿房路线

StarterRouteUsesFiniteFuelAndMiningKeepsOreIndependent失败于行走断言，尚未执行后面的采矿／返程。可信输入执行1300 tick，向右、jump始终false，喷气仅在hero.X<416启用。角色从x385.87走至**x1343.4646、height -464.5354**，之后约760 tick位置保持，期望x>1390。

原图只读碰撞采样：x1353.46处，height -464.54至-432.54均为实体岩层，位于角色前方。此时喷气燃料仍为**2秒**。失败是旧脚本未在障碍处跳跃、喷气或开路，并非燃料耗尽、缺氧停止或自动返船。第一矿床为x1400、height -448。

后续旧断言仍发送离散UseHeroItem(kind=pickaxe,targetId=deposit.Id)；[SessionHeroControl](../Game/Assets/DarkNights/Scripts/Runtime/Session/SessionHeroControl.cs)转给[HeroInventoryBehaviour.Use](../Game/Assets/DarkNights/Scripts/Runtime/Objects/HeroInventoryBehaviour.cs)，该入口明确退出、返回false。当前采矿链为带目标HeroInputRequest→[HeroEquipment.TickPickaxe](../Game/Assets/DarkNights/Scripts/Runtime/Objects/HeroEquipment.cs)→MiningTool.Hit。因此只调整行走断言仍不能正确验收现行手采合同；当前MiningInput／MiningToolDefinition回归通过另有证据。

### 旧矿工采集交货路线

MinerCanReachStarterDepositAndDeliverCargo失败于第一条mine请求，尚未进入采矿或交货。链为Expedition(mine)→[ExpeditionDevices.AssignMiner](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionDevices.cs)→[ExpeditionNavigation.CanReach／Find](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionNavigation.cs)。Find无路径则返回0／NoEffect并提示先开路。

- 诊断复用原会话与原图，对全部12个矿床发送可信派工请求，全部NoEffect；只读原Find也全部0个路径点。
- 矿工起点为坡道脚(408,0)。搜索扩展8单位水平步，要求+18／-24高度范围存在支撑、22高／半宽5净空和视线，不搜索悬空下落、喷气或主动开路。沿右侧连续支撑诊断到x1248、前高-379时已找不到下一支撑点；这不是完整BFS失败点的替代证明，完整Find=0是不可达直接证据。
- 12个目标中11个矿床中心位于实体岩层，仅第一中心为空。派工把矿床中心当行走目标，没有寻找露出的可采工作位。掩埋矿床与旧“走到中心”合同不匹配；玩家未开路时被拒是当前规则结果，不能宣称矿工能在所有新星球自动采集并双向交货。

原始证据：`artifacts/oxygen-removal-20261003/route-r1.json`（位置、燃料、邻近碰撞、目标、路径点数）、`miner-reachability-r1.json`（12条实际派工回执）。外部Editor脚本在tools/oxygen-removal/OxygenRouteProbe.cs、OxygenMinerProbe.cs，没有新增正式运行组件。

生成器、寻路器、矿床坐标换算、旧手采退出入口、角色碰撞及输入合同相对移除基线baedc15没有改动；本轮只从矿工返程条件删除低氧阈值。派工失败在任务赋值前，行走失败时位置未重置、燃料未消耗。源差异与实测支持它们属于旧路线假设／现有导航能力的问题，但这些失败仍然保留，不能把该两项计作通过。

后续分开处理：用现行带目标装备输入更新手采用例，明确真实障碍和开路步骤；矿工先明确“只走已开通路”还是主动开路／选择工作位，再验证来回采集、满包及无矿返程。这涉及地图／NPC玩法合同，本轮不擅自挖通正式地图或弱化可达性断言。
