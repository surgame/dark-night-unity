# 飞船远征与星图导航开发执行方案

日期：2026-10-19。对应[策划草案](SHIP_EXPEDITION_DESIGN.md)。本方案规定复用入口、状态归属、实施依赖和交付门槛；各切片实际进度只写[DEVELOPMENT](DEVELOPMENT.md#stage-cl)。

**开发沿现有YYGC会话架构扩展：所属Behaviour／State持有业务，SessionAuthority验证意图，ObjectMutationBatch提交短事务，既有可靠投影与双层地图流分发结果。** 不新建飞船世界、经济账本服务、网络通道、DI容器或全局事件框架。2026-10-19用户要求前置矿巢任务完成后新分支开发并做本机多客户端快速测试；前置已完成，随后明确采用共享信用点、按房间策略导航、正常全员上船及再次落地新远征，现按普通完整循环首片推进，正式进度与验证只见DEVELOPMENT；紧急／倒地及永久个人成长仍未决。

## 范围与实施前置

交付覆盖固定船舱、星图／列表联动、连续船位与动态耗时、全员目标／抵达时间／危险提示、回船存矿、集合撤离、安全交货及再次落地。危险资料和未制作星球可用明确标记的mock；不按mock新增正式刷怪、伤害或矿物收益。

当前正式主题仍为灰松谷。第二主题、紧急撤离、完整救援、永久升级、市场波动及自由驾驶扩展不混入首个闭环。既有手动飞行的正式入口如何替换，应与自动起降在同批明确，不长期让两个入口同时推进同一船体。

用户提出的方向已进入策划，以下细节仍需在相应切片实施前形成选择。问题的唯一决策记录仍在[阶段目标](EXPEDITION_CORE_LOOP_GOALS.md#5-待决问题与处理时点)，这里仅列依赖。

| 决策依赖 | 影响范围 | 可先开展的工作 |
| --- | --- | --- |
| Q-03 收益／装备归属 | 出售到账、共享余额支付、中途加入退出 | 货物转移、预计价值和一次结算合同；不默认确定收益分配 |
| Q-04 自动起降、导航资格与固定布局 | 原驾驶入口替换、改道、船内服务位置 | 只读星图、动态距离纯计算、UI资源和状态设计 |
| Q-05 远征实例与旧矿区 | 出生、离开后的地图保留／返回、再次落地、恢复 | 已审核完整地图加载接口和候选票据设计 |
| Q-08 异常人员与失败 | 倒地、断线携货、全员失败、紧急起飞 | 明确健康在线队员普通集合规则；异常不自动视为已同意抛弃 |
| Q-02／09 及首次交付节奏 | 携货／船容量、补给、起始资金和装备 | 沿用当前合法四格库存／购买，不擅加商品或数值 |

上表普通流程中的共享到账、自动起降／导航资格、正常集合和新远征已由续接选择解决；其余依赖在对应扩展正式写入口前解决，无依赖切片可继续。文档完成不表示以上选择全部通过，也不授权修改YYGC框架。

## 当前源码与复用清单

2026-10-19核对输入为游戏协议33、存档v25、锁定Unity 6000.4.9f1及现有YYGC补丁依赖。未来开工重新读取常量和锁定配置，不照抄本文版本号。源码存在与旧测试存在均不等于当前玩法已经开放或验收。

以下链接均指向游戏内已有入口，新增名称只作为实现语义说明，不宣称对应接口已存在。

| 职责 | 现有入口 | 复用方式与实际缺口 |
| --- | --- | --- |
| 航程唯一状态 | [ExpeditionJourneyBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionJourneyBehaviour.cs)、[ExpeditionJourneyState](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionJourneyState.cs) | 继续持有航程；新增位置、航线和结算身份字段，不在UI或Flow另存运行模型 |
| 航程流程／配置 | [ExpeditionFlowBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionFlowBehaviour.cs)、[ExpeditionFlowConfig](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionFlowConfig.cs) | 沿用配置冻结、调度和候选句柄；改造固定TransitSeconds、驾驶席绑定及旧生成路径 |
| 状态权威／事务 | [SessionStateBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Objects/SessionStateBehaviour.cs)、[ObjectMutationBatch](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectMutationBatch.cs) | 沿用SyncMode.Session、Edit权威检查与多所有者原子提交，不直接改池State |
| 船体／乘员 | [ExpeditionShip](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionShip.cs)、[ShipCabinMotion](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ShipCabinMotion.cs)、[ShipGeometry](../Game/Assets/DarkNights/Scripts/Core/Logic/Terrain/ShipGeometry.cs) | 保留唯一船体、舱门、登船和舱内位移；普通起飞现只做本地运动，不能当作撤离完成 |
| 采矿／货袋 | [HeroMining](../Game/Assets/DarkNights/Scripts/Runtime/Objects/HeroMining.cs)、[ExpeditionCargo](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionCargo.cs) | 沿用采集与容量校验；Transfer可作入舱事务核心，但正式入舱命令尚未接通 |
| 信用点／购买 | [EconomyBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Objects/EconomyBehaviour.cs)、[ShipTradeService](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ShipTradeService.cs) | 复用余额、价格与库存事务；Sell当前直接返回0，不恢复为船边即时卖个人货物 |
| 命令封送／可信入口 | [SessionClient](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionClient.cs)、[SessionCommand](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionCommand.cs)、[SessionServer](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionServer.cs) | 复用YYGC Gateway与Sender／Processor、不可变Freeze与可信连接映射 |
| 业务验证／权限 | [SessionAuthority](../Game/Assets/DarkNights/Scripts/Runtime/Session/SessionAuthority.cs)、[SessionJourneyControl](../Game/Assets/DarkNights/Scripts/Runtime/Session/SessionJourneyControl.cs)、[ObjectSessionCommands](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectSessionCommands.cs) | 复用队列、策略、租约、回执与去重；目的地／出售仍在暂停白名单之外，须按切片精确开放 |
| 对象投影 | [SessionProjector](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionProjector.cs)、[WorldSessionBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Network/WorldSessionBehaviour.cs)、[SessionObjectLink](../Game/Assets/DarkNights/Scripts/Runtime/Network/SessionObjectLink.cs) | 保持完整冻结帧通过会话YYGC状态与既有StateSynchronizer发布；不逐星球／货物增加NetworkObject |
| 航程网络合同 | [JourneyViewData](../Game/Assets/DarkNights/Scripts/Core/ViewData/JourneyViewData.cs)、[JourneyWire](../Game/Assets/DarkNights/Scripts/Runtime/Network/JourneyWire.cs)、[ProjectionValidation](../Game/Assets/DarkNights/Scripts/Runtime/Network/ProjectionValidation.cs) | 在现有读模型和有界校验内扩展，危险mock也来自同一冻结目录 |
| 地图与到达屏障 | [SessionJourneyTransfer](../Game/Assets/DarkNights/Scripts/Runtime/Session/SessionJourneyTransfer.cs)、[SessionTerrainNetwork](../Game/Assets/DarkNights/Scripts/Runtime/Terrain/SessionTerrainNetwork.cs)、[ReviewedMineMap](../Game/Assets/DarkNights/Scripts/Runtime/Terrain/ReviewedMineMap.cs) | 复用候选准备、地图交换、epoch与Ready；正式新局已有冻结地图加载，旧航程Pump仍需接入同一来源 |
| 保存与恢复 | [JourneySaveJson](../Game/Assets/DarkNights/Scripts/Runtime/Save/JourneySaveJson.cs)、[JourneyValidator](../Game/Assets/DarkNights/Scripts/Core/Save/JourneyValidator.cs)、[ObjectWorldRestore](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectWorldRestore.cs)、[SessionStorage](../Game/Assets/DarkNights/Scripts/Runtime/Save/SessionStorage.cs) | 沿用显式字段、候选对象和原子恢复；当前稳定阶段、空载体、驾驶席及船货零值校验均需同步调整 |
| UI与输入 | [ShipEquipmentPanel](../Game/Assets/DarkNights/Scripts/Entry/ShipEquipmentPanel.cs)、[ShipTradeHud](../Game/Assets/DarkNights/Scripts/Entry/ShipTradeHud.cs)、[DestinationPicker](../Game/Assets/DarkNights/Scripts/View/Expedition/DestinationPicker.cs)、[ExpeditionHud](../Game/Assets/DarkNights/Scripts/Entry/ExpeditionHud.cs) | 复用YYGC UIManager／UIPanel和Interaction Sessions；旧UGUI选择页为运行时创建，不能直接充当新的正式原生布局 |
| 船体／环境表现 | [ExpeditionShipView](../Game/Assets/DarkNights/Scripts/View/ExpeditionShipView.cs)、[JourneyEnvironment](../Game/Assets/DarkNights/Scripts/View/Expedition/JourneyEnvironment.cs) | 消费冻结船体／航程值，复用绑定、背景与释放；镜头近景保持本地 |

框架只读核对过`Runtime/Objects/NetworkStates/StatefulBehaviour.cs`、`StateSynchronizer.cs`、`Runtime/NetworkCommands/NetworkCommandGateway.cs`与`Runtime/Interactions/YYInteractionSessionDescriptor.cs`，落点为当前锁定`.deps/YYGC-grid-business`。它们提供的权威、会话同步、命令和本地交互边界足以承接本方案；暂未识别出必须修改框架的缺口。

若实施出现具体框架限制，先给出复现、拟改文件、依赖影响和验证计划，按[YYGC授权规则](../AGENTS.md#yygc-授权与锁定依赖)取得具体同意；游戏侧实现与当前锁定依赖继续复用，不借执行文档扩大框架授权。

## YYGC状态归属与事务边界

### 唯一所有者

| 数据 | 唯一权威所有者 | 不应持有的位置 |
| --- | --- | --- |
| 航程阶段、星图位置、当前目标、航线版本与模拟进度 | ExpeditionJourneyBehaviour／State | Flow字段、MonoBehaviour.Update、UI、客户端计时器 |
| 本次远征身份、结算资格与已结算标记／最近结果 | 同一ExpeditionJourneyState | SessionAuthority的连接窗口、仅内存的HashSet、表现事件 |
| 船体地面坐标、舱门与登船关系 | 既有BuildingState及ActorState所属Behaviour | 星图坐标副本或另一套乘员集合 |
| 个人矿物／装备 | 既有ActorState与库存能力 | 货物图标、拾取动画、UI缓存 |
| 舱内实存矿物 | 飞船所属BuildingState.CargoIron／CargoGold | JourneyState再复制一份可写货物账 |
| 信用点 | EconomyBehaviour／State | 结算面板、客户端预测余额、另建经济容器 |
| 前景／矿层及当前事件 | SessionTerrain及既有TerrainGameplay／Geology所属State | 飞船流程复制地图或另起飞行世界 |
| 节点坐标、危险资料、航速与图版本映射 | 已冻结的会话配置 | 各客户端临时抽签、可写运行State重复存整份作者配置 |
| 正在浏览的卡片、星图缩放、镜头和弹窗 | 各端本地表现／Interaction Session | 网络State、文件、其他玩家共享选择 |

继续由Flow协调现有所有者。Flow可持有候选和取消句柄，不成为另一份权威状态。现有Camp中ExpeditionRun／Phase等旧字段不得再启动平行远征流程；切片F0明确现有引用如何退休或只作单向统计，禁止双方共同写同一阶段或结算结果。

星图位置与船体世界位置属于不同坐标域，不能用BuildingState.X／Height表达跨星球距离。新增纯计算放Core；读取资产、实例装配和业务Tick留Runtime。只读ViewData不是状态所有者。

### 写入顺序

1. 在SessionAuthority执行点核验可信连接、政策、租约、阶段、目标、范围和版本。
2. 所有必要资源先在事务外准备；后台只接收冻结数据，不持有ObjectInstance、可写State或SessionScope。
3. 在`world.Mutations.Run`内通过所属Behaviour的`Edit()`提交货物、余额、结算标记及阶段；相关地图变化使用原事务安装／回滚接口。
4. 提交后再发送提示和表现事件；通知中不重入交易、不换世界、不重新触发结算。

入舱必须同时完成“角色扣除、船体增加”；出售必须同时完成“实际货物扣除、余额增加、远征标为已结算”。预检失败或异常不能留下其中一半。先算实际接收量／收入并检查容量与整数上限，再编辑状态。

State继续沿现有`[MemoryPackable, StateData]`与生成注册处理，生成输出按既有工具重建，不手改。对象由ObjectDefinition／DefinitionReference、显式绑定、现有工厂和租约装配；不以GetComponent、名称或子索引兜底缺失绑定。

## 生命周期与身份设计

### 沿现有阶段扩展

现有JourneyPhase包含Orbit、Preparing、Transit、ArrivalSync、Descent、Landed。下表是需要实现的业务语义；最终枚举值在F0合同中确定，不能把新增语义硬塞进旧值而跳过校验。

| 语义 | 进入条件与推进 | 退出条件与副作用 |
| --- | --- | --- |
| 安全待命 | 船员在舱、无正式目标 | 确认合法节点后开始准备／巡航，无目标不虚构ETA |
| 目的地准备 | 校验目录和审核图版本，准备有界候选 | 成功后巡航；失败保留船员、货物和当前世界，明确错误 |
| 巡航 | 服务端统一业务步长推进星图位置 | 改道从当前位置重算；到达进入同步等待，不由倒计时自行开舱 |
| 到达同步 | 同批地图与业务对象提交，更新epoch与Ready | 沿原屏障核对对象、双层基线及实际绘制后放行 |
| 自动降落 | 目标锁定，权威推进船体 | 真实安全支撑／净空满足后开舱并进入Landed |
| 矿区活动 | 采矿、返船存矿、使用已有余额购物 | 提出撤离进入集合；返船本身不建新图或结束远征 |
| 集合／关舱 | 按已选人员规则检查、倒计时与取消 | 离船／取消使关舱停止；封舱完成进入升空 |
| 自动升空 | 不依赖驾驶者持续在线或占座 | 达到安全交接点后提交本次出售资格与结算，进入安全待命／下一航段 |

自动飞行必须撤除旧`PilotAvailable()`对持续驾驶者的依赖；驾驶者断开取消旧航程的分支也需同步替换。导航操作资格不等于航程运行所有权，不能用转移FishNet所有权解决。

### 身份不要混用

- PlanetId标识目的地；审核地图身份／摘要标识内容；MapId／WorldId与epoch标识当前安装的运行地图与网络代次。
- 现有JourneyId继续承担航段身份；航线版本区分同航段内目标变化，旧准备结果不得安装到新航线。
- 本次远征结算身份独立于可改变的航段／目的地。改道、暂停、重连和候选重试均不能生成第二次出售资格。唯一持久标记归JourneyState，不能只靠当前epoch的请求去重。
- 准备中的目标地图身份与已安装地图身份分开处理；尚未提交不能提前改写当前地图，也不能让旧目标数据冒充当前世界。

航程状态只记录实际需要的有界字段：位置／航段起点终点、模拟进度、目标与版本、远征结算身份和结果。具体字段名、空值和状态组合由F0给出逐字段表，并同步CopyFrom、Capture、Wire、JSON与Validator；不预建通用航线调度框架。

## 命令与联机处理

```mermaid
flowchart LR
    UI[本地UI发送明确意图] --> Client[SessionClient Send]
    Client --> Gateway[YYGC Gateway Sender Processor]
    Gateway --> Server[SessionServer可信上下文与Freeze]
    Server --> Gate[SessionAuthority队列 权限 去重]
    Gate --> Route[现有业务适配入口]
    Route --> Tx[所属Behaviour与ObjectMutationBatch]
    Tx --> Projection[完整冻结投影]
    Projection --> Sync[WorldSessionBehaviour与StateSynchronizer]
    Sync --> Replica[Host与Client只读副本]
```

本地Host也经相同链提交一次，不另走直调Flow再回环发送。SessionServer从NetworkCommandContext和可信连接映射获取槽位／代次，客户端SenderObjectId、PlayerId、角色ID本身不构成授权。

| 意图 | 参数与执行点检查 | 结果 |
| --- | --- | --- |
| 查看星图／切个人卡片 | 只读最新冻结目录，不提交业务命令 | 仅本地选择变化 |
| 确认目标／途中改道 | 明确PlanetId、船体／角色EntityId、控制租约、当前航线版本；核对政策、位置、目标开放状态和阶段 | 同一航程更新；旧候选失效，广播结果 |
| 入舱提交矿物 | 明确本人角色和目标飞船，核对货物版本、距离、登船、容量与阶段 | 仅转移实际接收量，不出售 |
| 提出／取消撤离 | 明确飞船与当前阶段版本，核对权限、导航点距离及集合条件 | 统一进入或退出集合，不直接改钱 |
| 补给购买 | 复用BuyEquipment、InventoryRevision、商品Definition、价格与余额验证 | 支付与库存同事务，保持当前合法商品范围 |
| 自动出售／自动抵达 | 不提供客户端“增加金额”“出售数量”“已经抵达”请求 | 由Host在合法阶段边界推进一次 |

继续携带协议、epoch、PolicyRevision、请求序号、明确目标与租约。现有命令字段能清楚表达的语义沿用；需要新字段时统一扩展SessionCommand／Request／形状验证／SameIntent／归池重置，不把多参数塞进Kind字符串或借X当作客户端报价。

对象与角色入口要共同核查：`SessionOperation`、`SessionOperations`、`SessionHeroControl`、`ObjectSessionCommands.Valid`和UI发送端。仅打开UI按钮、移除一处拒绝或给房主放行均不代表正式接通。

SharedCamp与HostOnly沿现行合同执行，正式新增操作的最终权限表在F0确定。查看资格对所有人开放，星图入口按D-20限定在操控室；修改仍经过同一政策入口；切政策增加PolicyRevision并拒绝未执行旧意图。命令回执只解除待确认，权威结果来自投影；断线／旧epoch后不自动重发。

### 投影与时间显示

扩展JourneyViewData／JourneyWire与现有WorldWire、投影校验。星图、列表、HUD读取同一冻结节点目录和航程，危险mock不在本地随机。当前目录上限32，新增标签、字符串、坐标与数值须有有界校验；拒绝NaN、Infinity、重复节点和缺失引用。

服务端使用统一业务步长与一次倍速推进，暂停冻结航程。客户端按冻结位置、模拟时钟和航速平滑显示，可派生其他节点ETA，但不推进业务或自行宣布抵达。暂停／倍速／改道时重新锚定，不能简单使用本地墙钟持续倒数。抵达预计时间区分巡航、环境等待和降落，未知加载时间显示等待原因。

沿SessionProjector → WorldSessionBehaviour → 原StateSynchronizer可靠发布，再经SessionClient／WorldReplica及ObjectReplica消费。不开第二条星图RPC、不每帧独立广播倒计时、不为星球增加NetworkTransform。先测既有完整投影的开销，再讨论任何拆流。

按2026-10-23用户授权D-20，目标／ETA／危险信息收在操控室导航窗口；无全局星图入口或常驻航行条。集合期间只显示登船计数，装备与个人矿物在背包查看，常驻区域仅四格装备图标。晚加入和重连取得当前基线与现有航程，不重置计时、不重新结算。一次提示经SessionEventJournal，事件可丢失或不重播也不影响持久余额和已结算状态。

## 冻结地图与Ready屏障

正式开局已在SessionTerrainNetwork加载ReviewedMineMap，而旧Flow.Pump仍调用运行时GenerateCandidate。实施应统一到已审核完整地图来源；不因打开旧航程重新随机生成矿层、巢穴或地质站点。

1. 服务端冻结“目标节点 → 审核地图版本”的映射，验证来源和内容摘要，mock未开放节点拒绝正式降落。
2. 通过现有资源准备路径取地图与Definition；Unity资产读取留主线程，后台仅处理不可变数据。候选任务携带epoch、航段、目标／航线版本和内容身份票据。
3. 改道／取消使旧票据失效并取消任务；晚完成只释放自身租约，不安装旧地图、不覆盖新错误、不产生出售或新远征。
4. 复用SessionJourneyTransfer的提交和屏障结构，将前景、矿层、巢穴、CL02地质对象及船员关系作为同批候选处理。不得只交换Terrain而漏掉后续加入的动态能力。
5. 当前Install仅展示旧地图交换路径，不能据此认定巢穴／地质及空中返回全部已覆盖；对照ObjectWorldRestore现有候选装配与回滚，抽取实际公共步骤，避免复制第二套加载器。
6. 成功提交后更新epoch并退休旧Ready、命令窗口和局部流。使用原对象／前景／矿层基线与实际绘制确认，不用“资源下载完”替代Ready。
7. 保留现行Host与成员超时处理边界；未同步成员不得操纵新地图或在Unknown区下船。超时是否放行其他人按联机合同记录，不擅改为永远等全员或直接踢出。

从星球返回安全航行也必须明确地图环境：沿现有空载体和JourneyValidator.Environment合同准备一致的载体，或先提交明确的新环境合同；不能保留矿区地图却只把Phase改为Orbit。再次返回旧矿区或创建新实例须遵守Q-05选择，失败保留当前已提交状态。

### 配置身份的分离

导航坐标／航速／危险资料建议作为现有ExpeditionFlowConfig内独立的导航配置区，按PlanetId关联原冻结星球定义；不把星图坐标冒充地面泊位参数，也不再造第二份星球目录。

新增导航资料要进入会话内容摘要，危险mock同样参与一致性核验。`ReviewedMineMap.SourceFingerprint`当前包含完整Planets序列，直接往PlanetPreset加入展示字段会意外使地图来源失效。F1需明确区分导航展示依赖与地图生成依赖，保持地形／泊位／矿巢／地质实际变化仍使审核失效；不得简单删掉原来源检查或自动重生成作者图。

## UI资源与生命周期

2026-10-20用户认可Win98式迷雾星图并授权落地：标题与“目的地属性”边框分行留白，目录只列出已揭露节点且不显示总数。导航位图唯一由ExpeditionJourneyState拥有，Core.NavigationSurveyData只负责冻结与有限纯计算；512×512位／16单位格／半径28单位，旧已探索格不回收。初始只登记首个正式节点与真实船位，此后实际移动才显露。坐标确认沿原命令链携带X／Y与航线Revision，未知坐标也可前往，抵达回Orbit，不装地图、不结算。正式节点另校验已揭露／非PreviewOnly，现有星球开放资格不变；网络／文件细节见现行合同，不能将演示中的隐藏样本当作游戏内容。

正式船舱服务优先沿现有ShipEquipmentPanel的YYGC UIManager／UIPanel与UIToolkit资源链增加导航面板和只读航行条。旧UGUI DestinationPicker的个人浏览、只读目录和确认回调语义可复用，运行时拼GameObject布局不作为新正式布局来源；同批撤除其正式导航入口，保留独立调试边界。

UI代码仅绑定冻结数据并发意图。可编辑布局使用原生UXML／USS及既有主题、PanelSettings和Addressables；船体是原生Prefab，Definition／PrefabRef与显式锚点绑定。专用资源放既有Res/UI和Res/Objects归属中，不添加特殊Resources目录。不得重新搭UIManager或新增全局导航事件总线。

星图浏览弹窗复用YYInteractionSessionService，阻断本地冲突输入；被动航行条不创建交互Session。关闭、切世界、断线、租约／权限变化时释放modal、事件和R3订阅，取消旧页面待确认显示；关闭导航不会取消航程。

补给需要在巡航中可用，现有ShipTradeService.Near仅接受Orbit／Landed且要求无Pilot，ShipTradeHud也按旧阶段筛选，二者必须一起改造。新增交货／导航锚点不能继续依赖“恰好两个服务”和sale／shop二选一逻辑；按明确服务身份校验并沿绑定注入，禁止旧GetComponentsInChildren扫描成为新缺失引用的兜底。

新船体几何与ShipGeometry、坡道、角色脚底／锚点、服务范围和着陆净空同批调整。动画回调不驱动开门、成交或换图。镜头与JourneyEnvironment仅订阅状态，不用镜头到位决定业务完成。人工资产、GUID和未保存场景继续保护。

## 存档与结算一致性

存档必须从首个产生持久字段的切片同步设计，不能到全部实现后补写。当前v25要求船货和返航结算为零，JourneyValidator还限制稳定阶段、空载体和Pilot关系；只修改JSON字段仍会在关系校验中失败。

| 数据或阶段 | 保存／恢复要求 |
| --- | --- |
| 星图待命与矿区稳定阶段 | 保存航程位置、目录身份、远征身份、船货、余额及完整地图／事件，恢复到同一结果 |
| 巡航 | 目标是可保存已冻结的航线与模拟进度；恢复后重新准备非持久资源，不重置船位或重复出售。完成合同前明确禁止此阶段保存 |
| 资源准备、地图交换、未完成的原子结算 | 保存门禁与当前CanSave／SessionStorage／UI一致，返回可理解原因；不在半提交状态捕获 |
| 连接、控制租约、Ready、候选Task和本地UI | 不存文件；加载后由当前连接、资源准备和新的epoch重建 |
| 已结算结果 | 与余额和被扣货物同一模拟边界捕获，重连／重复请求／恢复已结算存档不得再发收入 |

复用ObjectSnapshotMapper、JourneySaveJson／ExpeditionSaveJson及所属JSON、SnapshotValidator／JourneyValidator／ExpeditionShipValidator、SaveContentFingerprint、SessionStorage和ObjectWorldRestore。网络模型与文件合同分别维护；旧兼用JourneyViewData的读取路径需明确网络允许阶段和文件可恢复阶段，不能放宽网络校验来绕过文件限制。

新增／改变持久字段和Wire语义时升级对应版本、严格摘要及独立存档目录；版本号在切片提交时以最新源码分配，不预占34／26或覆盖其他模块版本。原v25及更旧文件保留，不迁移、不删除、不在旧目录写入新语义。

出售与文件写盘的原子性是两个边界：前者由同步业务事务保证，后者复用现有冻结捕获、临时文件、Flush和原子替换。售后提示不是落盘证明；崩溃后只保证恢复最近成功保存的世界，不宣称外部账户级的跨任意旧档防刷。没有外部真实交易服务，也不为本切片增加该系统。

已提交的撤离收入不能因下一目标地图准备失败而逆转。失败重试保留“货已出售、钱已到账”的状态，新的航程只重试到达；重连、改道和目标重试均不重新授予结算资格。

## 实施切片与依赖

切片ID仅用于执行交接，不创建新阶段或替代CL模块。每片开工写清具体负责会话、共享文件和当前候选身份；未开工项不能提前记实现。跨模块状态、地图、协议、注册和存档由同批单一修改方处理。

| 切片 | 前置与交付 | 主要落点 | 完成门槛 |
| --- | --- | --- | --- |
| F0 合同与状态设计 | 解决对应Q项，给出字段／阶段转移／权限／保存表，确定旧驾驶与Camp字段退场方式 | 本方案及架构／联机／保存合同的候选变更清单 | 每条业务状态有唯一所有者；未决项与禁止开放入口对应清楚 |
| F1 导航配置与纯计算 | 按节点定义坐标／危险mock／开放状态，计算当前位置到各节点ETA；明确导航与地形摘要边界 | Core纯计算，FlowConfig与冻结目录，内容摘要 | 有界／有限数校验；不执行生成，不意外破坏审核图来源，算法与策划例子一致 |
| F2 状态与全员只读反馈 | 复用JourneyState、可靠投影、UIManager与Interaction Sessions，交付星图／列表／HUD | JourneyCapture／Wire／校验，正式UI资源与Entry接线 | 三处数据同源；所有玩家可看；本地浏览不发业务请求；暂未开放航行时明确显示非巡航状态 |
| F3 入舱事务与恢复 | Q容量及异常边界明确后接通个人货袋→船货，同时维护文件合同 | ExpeditionCargo、角色／船体State、命令门禁、投影与Save | 满舱不丢货、重复提交不增货；保存与恢复支持非零船货 |
| F4 自动航程与地图交接 | Q-04／05确定，F1／2就绪；巡航／改道／自动降落与升空共用原Ready和候选安装 | Flow、Ship、SessionJourneyControl／Transfer、Terrain及对象候选、输入与环境 | 不依赖Pilot；旧候选不安装；地图／巢穴／地质／船员同批；禁止绕过Ready开舱。出售未接通前不开放正式完整撤离 |
| F5 结算与正式循环 | F3／4及Q-03／08等前置完成；一次安全交货、到账、航途中购物与再次出发 | Journey结算状态、Economy、ShipTrade、Save及正式命令白名单 | 钱货标记同事务，加载／改道不重复出售；不恢复NPC、DIY或旧物流 |
| F6 固定船体与交付验证 | 布局审阅后迁移原生资源与绑定；核对各切片当前候选 | Prefab／Definition／ShipGeometry、UI、Addressables和合同 | 原生编辑可见、引用完整；授权范围内完成下节验证，结果分级记录 |

F1可先于经济决策进行；F2不建立临时业务模拟来伪装航行；F3／F4按共享文件串行集成，F5等待各自真实合同就绪。UI、美术方案可早作预览，但正式写入始终复用一个Local Editor。第一次可体验交付优先F1／2的导航信息，再接货物与完整航程。

每片结束更新DEVELOPMENT：产出、源码／配置身份、编译结果、已验／未验、失败、下一步及产物路径。同步相关长期合同和Player操作指南，不把本计划中的建议提前写成已开放能力。

## 验证与交付门槛

### 默认检查与授权边界

2026-10-19用户已授权新分支开发与简单／快速的本机多客户端联机测试，续接已扩展为普通完整交货循环，按候选变更或实测失败才重建，每份Mono Player复用进行本机验证。本机结果不覆盖其他机器LAN。其他代码切片默认仅检查编译和必要差异／引用；下表未被本次范围覆盖的验收仍待安排，不因列入计划而自动执行。

每批编译／Play／测试／构建前检查磁盘和可用物理内存；按2026-10-19现行规则，内存停止条件仅为可用物理内存低于1 GiB，Editor私有内存仅记录，系统提交余量不再采集或门控。构建另核对预计物理内存和磁盘需求。复用单一Local Editor，先短探针再扩大；按[有限批次流程](UNITY_CLI_WORKFLOW.md#有限批次与依赖屏障)保留任务ID、完成标记和输出路径。

### 必要验收矩阵

| 范围 | 操作与关键边界 | 预期 |
| --- | --- | --- |
| 距离与时间 | 靠近A后改去B；同坐标、零距离、非法值；暂停／倍速 | 重算基于当前船位，有限数且不倒退；倍速只生效一次 |
| 全员信息 | 2人，再覆盖4人；各自靠近／离开操控室，一人购物，其余关面板 | 仅操控室唤醒星图；目标／危险同源、ETA在既定平滑误差内；个人浏览不互相覆盖 |
| 权限与竞争 | Host／普通Client、SharedCamp／HostOnly、政策切换；两人同时改道 | 可信执行点检查有效，旧政策／版本拒绝；Host单次执行 |
| 货物事务 | 最后一份矿、满袋／满舱、部分接收、两人同时交货、重复序号和换参数重发 | 不丢货、不复制，实际接收／余额与投影一致 |
| 集合与驾驶退场 | 船外队员、倒计时离船、取消、导航者离座／掉线 | 按已定规则阻止或继续，不因旧Pilot分支失控，不误触提前出售 |
| 候选与加载 | A候选未完成就改B、取消、晚完成、错误内容与资源失败 | 旧任务只清自身；当前候选失败保留当前世界与已提交货账 |
| Ready与地图 | 独立Host＋Client、晚加入、重连、弱网、到达超时 | 未Ready不进入新图；旧epoch／revision不污染；双层地图、巢穴和地质进度同版 |
| 一次结算 | 重复请求、改道、售后加载失败、已结算存档读取 | 货扣除／到账／已结算标记一致，不重复给钱或重复清货 |
| 真正保存恢复 | 矿区有船货、安全待命、巡航稳定阶段，实际写盘、关闭进程并重启 | 恢复位置、货物、余额、最终矿图与事件；连接／UI不恢复；坏档不覆盖当前世界 |
| 原生资产与操作 | 编辑、保存、重开船体／UI，再运行；不同窗口尺寸、四人站位、坡道与安全落地 | GUID／引用完好，布局可读，进出／碰撞与画面一致，无运行时生成空壳替代正式布局 |

优先复用并按新合同修订Journey、ShipTrade、Ship、Projection及保存类已有测试。旧测试若要求暂停NPC或旧即时卖矿，不能为让它通过恢复旧业务；保留原失败与修改理由。避免只镜像字段赋值的测试，重点验证跨所有者原子性、权限、过期候选和恢复边界。

正式多人验收使用同一候选的独立进程Host＋Client；单机Editor或聊天demo不替代。Mono每配置构建一次复用，IL2CPP需明确授权，双机器范围单列。性能只记录本次测量，启动成功不作为前台性能通过。

### 收尾与可恢复交接

交付区分实现完成、当前Unity编译通过、指定验收通过及用户阶段确认。保存失败、旧夹具和正式证据；不重生成旧样本掩盖差异。中间产物按[工作区保全流程](WORKSPACE.md#阶段产物盘点与保全)登记，未经具体许可不删除。

提交前核对只包含本次授权成果、生成注册／meta／地址引用与合同版本一致；不提交.deps、缓存、用户存档或密钥，不自动推送。任何框架源码／补丁修改另走事先具体授权，不以本计划作为许可。

本轮执行文档核验与后续实际进度统一回到[CL台账](DEVELOPMENT.md#stage-cl)，CL阶段结束仍由用户明确判断。
