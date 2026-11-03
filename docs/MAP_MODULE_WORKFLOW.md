# MapModule模块开发工作流

本文件是把设计图、visualize或文字方案推进到新版MapModule的统一开发入口。用户可直接引用本文件并附上设计，承接会话按下述流程完成原生模块与必要玩法接入；不依赖某个历史聊天，也不要求用户分别寻找地形、物理、联机或资源装配会话。

本文件维护执行方法与定位，实际进度、能力就绪和验证结果只写[DEVELOPMENT](DEVELOPMENT.md)，长期切片归[ROADMAP](ROADMAP.md#cl-mapmodule-plan)。引用文档本身不启动开发；“按此工作流开发所附设计”等明确指令才确定本次范围。仅要求评估或整理文档时，停留在对应工作，不创建玩法资产。

## 直接交给开发

用户最小输入为“本工作流＋设计＋开发要求”，例如：

> 按所引用的MAP_MODULE_WORKFLOW，将上述visualize开发成新版MapModule原生样例，完成地形、设计所需交互、地图工坊登记及启动接线；区分实现完成、编译通过和实际验收。

承接方自行从输入提取模块用途、主路／支路、入口／出口、交互及失败／返程逻辑。读取visualize实际来源文件；截图只作为结构参考，不当成可执行资产或精确碰撞。优先使用本会话已给出的文件、设计说明和DEVELOPMENT中登记的来源；仅在设计缺失或有冲突时追溯指定聊天，不先全文回读历史聊天。

将明确需求和仍待讨论的提议分开。原型中的尺寸、速度、摆幅、按键、收益及成功预演不直接覆盖现有合同或数值；沿实际角色、单位和既有输入校准。关键规则缺失且会影响开发范围时集中提出，其余可逆制作选择由承接方完成。

已有M17实例可直接复用装配方式：`MapModuleRopeRiftStudy.unity`绑定`Res/Terrain/MapModule/M17/`下的原生地形及环境吊绳，操作见[地图工坊](EDITOR_WORKBENCH.md#mapmodule-workbench)。环境绳复用PhysicsSessionBehaviour及现有物理人物；新的模块可以配置独立HangingRopePlacement及Profile，不重新实现人物运动。M17StudySetup仅用于目标空目录首建，不覆盖已有作者资产；实际能力和待验范围仍查DEVELOPMENT。

## 开工与定向阅读

先完成AGENTS要求的README、文档索引、当前阶段目标／相关ROADMAP切片和DEVELOPMENT阅读，再核对当前分支、未提交修改及本次共享文件归属。已读且未变的信息复用，不因换切片重复全文读取所有合同。

| 本次涉及的工作 | 按需进入的现行依据 |
| --- | --- |
| 模块形状、占格、端口、作者资源与Bake | 本文[作者与制作版本](#作者与制作版本)、[地图工坊操作](EDITOR_WORKBENCH.md#mapmodule-workbench) |
| 代码、状态、对象装配、资源绑定 | [ARCHITECTURE](ARCHITECTURE.md)，重点唯一所有者、程序集和装配事务 |
| 动态运动、绳索、可推物与约束 | [物理后端合同](ARCHITECTURE.md#physics-backend-evolution)、[物理候选操作](EDITOR_WORKBENCH.md#cooperative-physics)，并核对DEVELOPMENT中的当前能力 |
| 权限、交互竞争、投影、重连或恢复 | [MULTIPLAYER](MULTIPLAYER.md)、[SAVE_FORMAT](SAVE_FORMAT.md)相关部分 |
| 矿层、采掘、塌方或生态效果 | 从[文档索引](README.md)进入对应现行合同，不恢复暂停玩法 |
| Editor执行、编译或已授权验收 | [UNITY_CLI_WORKFLOW](UNITY_CLI_WORKFLOW.md)，沿单一Local通道和有限批次 |
| YYGC使用、依赖缺失或框架缺口 | [依赖准备](DEPENDENCIES.md)、[YYGC账本](YYGC_CHANGES.md)及AGENTS的具体授权边界；普通游戏开发不默认改框架 |

在DEVELOPMENT登记本次负责会话、模块／切片、设计来源、拟改范围、依赖与验证授权。先明确最终应交付哪一个可从工坊打开、编辑并启动的样例，避免只制作一张静态地形或另建独立演示。

## 开发执行

1. **提取设计与能力缺口**：用简短工作记录列出静态地形、动态交互、表现及运行接入，区分已有可复用能力和必须补的游戏侧能力。已有“绳索”类型并不等于已经支持固定锚点、抓放、回收等所有绳索玩法；逐项核对真实实现。用户明确要求的玩法在授权范围内连续完成，框架源码／补丁／升级需求另按AGENTS处理。
2. **制作原生模块**：为本次模块确定稳定ID、占格、闭合实体、端口与必要挂点，写入对应Prefab；模块内部源归Prefab，实例布局／连接／出生／镜头锚点归原生Scene。新增主题沿现有研究组织接入，不能覆盖已有入口或人工资产；真实格形无法表达时明确报告，不以平滑外观冒充碰撞。
3. **接入玩法**：按原Behaviour／State、会话时钟、可信命令和资源绑定实现交互，静态配置与运行状态分别归属。地图唯一权威仍为SessionTerrain，角色沿现有Actor；不在visualize、Prefab显示脚本或新的模块世界里再算一套运动／收益。需要物理候选时显式绑定Profile，补齐模块专属端点、释放、重开及生命周期，不把已有候选当作已验。
4. **贯通工坊与启动**：登记模块／样例与选择入口，核对工坊能找到本次研究，而非仍固定打开旧入口。依次保存作者源、检查、生成新的只读Bake，再沿Bootstrap／MapModuleEntry／现有Host会话启动。新玩法的配置身份沿现有内容校验纳入；不假定地形Bake自动包含所有动态装配。研究默认不读写玩家档，不自动替换正式默认地图。
5. **编译与获批验证**：集中写入同批输入，等待依赖屏障后统一导入／生成／编译。按AGENTS预检磁盘和物理内存，复用Local单一Editor。默认仅主动确认本次代码编译；用户明确要求的试玩／测试／验收直接执行指定最小范围。美术／资源编辑、保存重开、真实Host＋Client及恢复按受影响合同和实际授权分项记录，不主动扩大为Player或IL2CPP构建。
6. **交付并更新状态**：集中检查本次差异、GUID／引用、源码与派生产物身份，保留失败及旧版本。更新DEVELOPMENT中的产出、编译、已验／未验、阻塞及下一步；仅在合同或输入变化时改对应专题与ROADMAP。实现完成不自动结束CL或核销运行待验。

## 工作台接入与UI分区

新增内容必须进入同一地图工坊的选择结构，不能为每个模块在顶部追加专用按钮。用户只需选择地图，再查看其中的模块及交互物；不展示“研究→当前研究中的案例／样例”这层重复选择。内部仍以作者Scene／StudyId、CaseId及Bake身份定位可独立制作／启动的布局，不因UI扁平化合并或丢失身份。新增模块优先扩展已有作者源，只有研究目的或Rig／玩法配置需要独立时才新增Scene。

| 区域 | 固定职责 |
| --- | --- |
| 左侧导航 | 扁平地图列表／搜索 → 地图中的模块 → 模块内交互物；一个可运行布局直接占一条，不再列“当前研究中的案例” |
| 中间画布 | 所选地图的真实作者轮廓、模块边界、端口、出生比例及交互物初始位置；浏览、缩放、平移及选择只影响工坊 |
| 右侧属性 | 跟随当前所选模块或交互物，提供位置、关键配置摘要及原生Prefab／Inspector入口；不只为某一主题固定显示 |
| 顶部操作 | 所选地图上下文、显式打开作者场景、保存、检查、准备制作版本及启动调试；不放M17等内容专用入口 |
| 制作与来源 | 初始化空目录、定位Scene及来源说明等低频操作收进左侧折叠区，不与内容选择混排 |

地图列表枚举`Res/Scenes/Workbenches/MapModule/`内的原生Scene并展开各Case；单布局标题复用主工作台场景目录，多布局直接使用各布局名称。中文列表无需暴露Study／Case层级，检查、制作及启动仍绑定同一对内部身份。

**浏览不得驱动Unity跟随切场景。** 点击地图、模块或交互物，只更新工坊的选择、画布和属性；不得调用OpenScene／SetActiveScene切换作者源，不改变原生Hierarchy中的已加载作者场景、Selection或已有脏标记，也不弹保存／丢弃对话框。尚未加载的来源使用窗口拥有的只读原生PreviewScene，关闭／换选／域重载时只释放自有预览；不启动业务／网络，不创建另一份可编辑地图草稿。Unity手动切场景也不覆盖工坊已选地图。

“打开作者场景”是明确的原生编辑操作：仅此操作或用户点击“启动调试”才允许切换Unity活动场景，并沿原生提示处理未保存修改，不自动保存或丢弃。来源已在Editor加载时，属性代理仍写同一原生作者对象并保留Undo；仅持有只读预览时，位置、保存及准备制作版本等写入控件禁用并提示先打开作者源。检查可只读执行，Prefab／Inspector编辑入口须明确指向真实资产或已打开作者对象。

每个新内容交付前核对：左侧直接找到地图，选中后画布看到实际地形和交互物、右侧同步；连续点选不同地图时Unity活动场景、作者场景清单、Selection及脏标记保持，没有自动保存／丢弃提示；只有显式打开作者源／调试才转场。另核对检查／制作／启动的内部身份始终来自当前地图。Editor截图和引用检查只证明UI装配，不能代替游戏运动或联机验收。

## 快速源码定位

以下路径相对`Game/Assets/DarkNights/`，用于定向阅读；实现有变化时按类型名局部搜索，不先全仓遍历。类型仍存在不等于所有能力已完成，当前状态以DEVELOPMENT和实际源码为准。

| 职责 | 首查位置 |
| --- | --- |
| 模块形状／端口与样例布局 | `Scripts/View/MapModule/MapModuleAuthoring.cs`、`MapModulePort.cs`、`MapModuleCaseAuthoring.cs`、`MapModuleStudyAuthoring.cs` |
| 来源身份、冻结制作版本 | `Scripts/View/MapModule/MapModuleSource.cs`、`MapModuleBake.cs` |
| 精确格形与编译检查 | `Scripts/Editor/MapModule/MapModuleCompiler.cs`、`Scripts/Core/Logic/MapModule/` |
| 工坊地图目录、独立预览与显式作者打开 | `Scripts/Editor/MapModule/MapModuleWorkbenchWindow.cs`、`MapModuleStudyNavigation.cs`、`MapModuleStudyPreview.cs`、`MapModuleDetailsPanel.cs` |
| 作者Scene定位与启动校验 | `Scripts/Editor/MapModule/MapModulePaths.cs`、`MapModuleStartupValidation.cs` |
| 研究运行、地图安装与出生 | `Scripts/Entry/MapModule/MapModuleEntry.cs`、`Scripts/Runtime/Objects/MapModuleSessionSetup.cs`、`Scripts/Runtime/Terrain/SessionTerrainNetwork.cs` |
| 物理配置、作者道具与运行所有者 | `Scripts/Runtime/Physics/PhysicsProfile.cs`、`PhysicsPropPlacement.cs`、`Scripts/Runtime/Objects/PhysicsSessionBehaviour.cs` |
| 唯一状态与资源装配 | `Scripts/Runtime/Objects/ObjectSession.cs`及对应Behaviour／State，按ARCHITECTURE追踪绑定链 |
| 原生资源落点 | `Res/Terrain/MapModule/Modules/`、`Bakes/`；作者研究Scene位于`Res/Scenes/Workbenches/MapModule/` |

## 交付检查

交付至少说明以下结果，不用“开发完成”混合替代：

- **实现**：模块／样例ID、原生Prefab与Scene、工坊入口、设计中的交互完成项及明确缺口；未运行时只报告已配置启动，不宣称可玩性已验。
- **来源与装配**：作者源与新Bake身份一致，引用／绑定有效，已有人工资产和GUID保全；动态配置及内容身份能追溯。
- **编译**：当前候选的编译结果及回执；纯文档改动仅做文档核验。
- **验收**：实际执行的操作、预期与结果、输入身份和证据。未获运行授权时列出待测流程及关键边界，不借历史结果报通过。
- **交接**：DEVELOPMENT已更新。YYGC具体变更只有获批后才写账本；研究样例接入与正式整图替换分别说明。

## 专题职责与沿用边界

AGENTS只保留长期硬约束、触发条件及导航；本文负责模块开发执行。YYGC授权摘要继续留在AGENTS，锁定输入／准备方法归DEPENDENCIES，具体修改与验证来源归YYGC_CHANGES；架构、联机、存档和Unity批次各由现行合同承接。本次无需复制或批量迁移这些内容，也不以引用某份文档扩大授权。

以下保留MapModule制作、接入与界面方案的详细依据；涉及历史首片范围或暂缓界面时，按用户本次明确范围及DEVELOPMENT核对后使用。

2026-11-03原生首片范围：用户暂缓独立游戏内调试台，授权新版原生工作台开发，调试从工坊选中样例快速启动。该首片实施MM-00／01及首对模块所需MM-02接线，不注册MapModule F1专页；下文独立调试台界面及问题记录为保留候选，未因此实施。原生启动仍走既有FishNet／YYGC与固定制作版本，后续任务范围及实际状态见DEVELOPMENT。

2026-11-03方案来源：用户建议工程命名MapModule，要求新版与旧地图工作台分离，并考虑FishNet、YYGC Stateful、R3、UniTask等现有服务。此前设计轮仅交付方案与UI原型；后续原生实施授权以上一段为准，未自动扩大为运行验收授权。长期切片维护于[ROADMAP](ROADMAP.md#cl-mapmodule-plan)，实际状态唯一维护于DEVELOPMENT；当前原生操作见[工作台文档](EDITOR_WORKBENCH.md#mapmodule-workbench)。

工程前缀采用`MapModule`。中文显示建议为“地图工坊”，运行研究入口为“地图试验场”；单个资源称“地形模块”，入口／洞道／洞室是模块类别。此前CaveAssembly命名和CA切片被本方案替代，原方案保留于`artifacts/mapmodule-workflow-20261103/previous-cave-assembly-plan.md`。

## 复评结果

| 原方案缺口 | 修订 |
| --- | --- |
| 新入口可能被理解为旧工作台加页签 | 独立MapModuleWorkbenchWindow、作者目录、制作版本和Scene；主工作台只共享打开入口 |
| 以LocalWhitebox起步，再补联机 | LocalWhitebox仅一名worker，WhiteboxSession没有SessionNetwork；首个可运行样例直接走既有FishNet Host，下一切片验证独立Client |
| “复用网络”未落实地图准备边界 | SessionTerrainNetwork.SelectNew仍关联旧配方／ReviewedMap；增加游戏侧显式已准备地图安装入口，缺失或失配拒绝，不回退旧生成器 |
| 先扩12模块，后验证运行基础 | 先以1对模块打通编辑、Bake、Host、Client，再扩六组内容 |
| 原生曲线／岩棚能力未解决 | 作者支持闭合实体、多空腔和多个端口；首片精确落入既有格形／12坡形，任意曲线另列权威查询扩展 |
| 异步与可变状态边界偏笼统 | R3承担UI和只读状态组合，UniTask承担异步资源链；按窗口、样例、连接和输入版本释放、取消与拒绝旧结果 |

## 独立制作链

拟入口为`Dark Nights → 工作台 → 游戏／地图 → 地图工坊`，打开独立UITK EditorWindow。正式顶层Unity菜单仍只有现有工作台。

新工坊按“模块 → 拼装 → 试跑”组织任务：

1. 模块：打开原生Prefab，制作局部形状、接口、前景、后壁和生态挂点。
2. 拼装：在原生Scene摆放模块实例、显式连接和样例出生／镜头锚点，保存单片、片段或整图布局。
3. 试跑：检查当前源、显式准备只读制作版本，再将固定版本交给地图试验场。

Scene是组装位置的唯一可编辑来源，模块内部形状归对应Prefab；工坊画布只代理这些作者对象，使用Undo／保存，不另外持有可手改的布局JSON。Editor预览只加载表现，不创建业务会话、网络或玩家存档。

独立边界包括窗口类型、状态所有者、UXML／USS、作者目录、制作版本和检查／烘焙命令。不得引用旧MapWorkbenchWindow、TerrainGenerationPreview、旧八步空间生成或ExpeditionFlowDraft作为新版的作者控制器。YYGC工厂、地形、角色、输入、资源等基础服务按能力复用。

旧MapWorkbench在新入口就绪后标记“旧版地图工作台／参考”，退出日常新地图制作。旧资源、GUID、冻结结果、失败证据和存档原位保留，代码级退役与正式默认图切换按切片实施，不把当前文档修改当作已完成。

## 作者与制作版本

| 来源 | 内容 | 不变量 |
| --- | --- | --- |
| MapModuleAuthoring所在原生Prefab | 稳定模块ID、局部实体／空腔轮廓、占格、端口、分层表现和挂点 | 修改内部形状进入Prefab模式；派生网格不手改 |
| MapModule研究Scene | 主题／样例ID、原生模块实例、位置、连接、出生／镜头锚点 | 各样例明确边界，一次选中一组；不放多套Entry／输入／会话 |
| MapModuleBake | 格式版本、StudyId／CaseId、来源摘要、布局／形状摘要、表现身份及不可变完整初始地图 | 只读派生；不保存本局EntityId／epoch／耐久进度，不反写作者布局 |

建议工作尺寸仍为未定稿的1U=3.2×3.2单位，模块按内容声明整数占格，不限于1×1／2×1／1×2／2×2。2026-11-03用户指出原入口像“钻狗洞”，明确要求重设计人物与洞穴实体比例；当前入口及浅厅两片改为各4×3U（12.8×9.6单位），总横向25.6单位，保留H≈0.48主角、运动数值与每格0.16单位。地表平台、下行坡道、洞檐和较厚的山体覆岩均计入入口；出口不固定回到地表高度或接悬崖。这是当前研究样例的制作尺寸，不是全部未来模块的强制占格。

当前样例以站立身高H作视觉参照：入口坡段代表点净高1.92单位／4H，接口净高2.72单位／约5.7H，浅厅主体净高约4单位／8.3H；宽度及纵深随模块同步展开。净高数值描述作者轮廓，不是自动人物通行、跳跃或返程检查门槛。Prefab内部轮廓／端口与Scene平移分别修改，整体仍使用单位缩放；派生网格与新Bake由同一轮廓生成，旧Bake保留。镜头缩放不承担改变人物与地形相对尺寸的职责。

端口定义方向、局部位置、开口区间和共享边界带，可同边多口；连接比较空实／坡形／材料、坐标相位与轮廓走势。模块原点与现有半格坐标的转换明确记录。首片只允许格对齐平移，不开放任意旋转／缩放；无独立过渡块，不通过伸缩或自动挖墙消除冲突。

现有Floor／Ceiling不能表达同一X上的多层岩棚，原点单位变换约束也不支持多块摆放。新版作者轮廓应支持闭合实体和多空腔，但烘焙结果仍须精确匹配当前整格及12种坡形。无法表达时报告具体位置，不用圆滑外观或全格台阶冒充；任意连续曲线需要独立的权威形状／Movement查询切片。

所有选中模块合成一份初始地形，由SessionTerrain唯一拥有。首片保持320×192底层格上限，越界明确拒绝；不因占格单位改大就自动扩大传输／存档尺寸。WFC在开发端按需添加，用于审核候选选块；先建立确定配对和手工组装，不承诺任意互拼或永不失败，运行开挖不重排模块。

“未检查／存在冲突／可烘焙／源已变化／试跑版本可用／运行绑定旧版本”是不同状态。源变更使本次检查和Bake失效；旧版本可以只读查看，但不能误当当前源启动新试跑。检查、烘焙和正式引用切换分别显式执行。

## 基础服务与运行接入

当前本地锁定输入为R3 1.3.1、UniTask 2.5.11、MemoryPack 1.21.4和锁定FishNet／YYGC／AnyRuleD包，依据manifest、packages-lock及依赖合同。本方案不升级包或修改YYGC源码、`.deps`及补丁。

| 服务 | 新工作流的复用职责 |
| --- | --- |
| FishNet | 现有NetworkManager、SessionNetwork、房间／可信连接和Gateway／Sender／Processor；单人也走本机Host，加入客户端沿同一会话 |
| YYGC Stateful | 原CampSessionBehaviour、WorldSessionBehaviour／StateSynchronizer、ActorBehaviour／ActorState、对象工厂和定义绑定；Host唯一写入，客户端只读冻结副本 |
| 地图同步 | SessionTerrainNetwork、前景／矿层NativeMapTransport、epoch、Ready及实际绘制回执；初始地图来源显式改为MapModuleBake |
| R3 | 模块／样例选择、源脏标记、检查／烘焙／连接状态及只读投影的UI组合；每个窗口、面板和连接拥有并释放订阅 |
| UniTask | 资源、Scene／Prefab预览、制作包准备和会话开始／结束；捕获输入版本与取消令牌，旧完成不能覆盖新选择 |
| UITK及YYGC UI／输入 | EditorWindow与原生运行UIDocument分开，运行面板接既有UIManager和Interaction Sessions；输入交给GameInputActions |
| 现有资源与序列化 | DefinitionReference／PrefabRef、Addressables租约、MemoryPack及已注册消息按实际需要复用，不另建DI／全局事件或业务世界 |

运行链为：选中样例 → 冻结制作版本 → 本机Host／加入Host → 既有SessionNetwork → 唯一权威ObjectSession／Stateful对象／SessionTerrain → 冻结投影与局部地图流 → 本端表现和诊断。

不新增MapModuleWorldState、独立运动积分、第二套时钟或逐模块NetworkObject。研究上下文只组合不可变启动参数、对象引用和资源租约；对象、地图和RNG仍归原所属状态。

必要游戏侧适配：

1. MapModule唯一Scene入口与启动上下文，明确选中研究样例，保留Bootstrap主入口。
2. 显式研究布局校验，允许无建筑、2–4人通过Ready创建各自主角及非零出生高度；不借用LocalWhitebox的一人／不联机约束，不放假船满足旧合同。
3. SessionTerrainNetwork在连接前安装已验证MapModuleBake的有限入口，保持原CreateAuthority及传输；EnsureSelected不得重新走QuickTestPreset／旧生成器。
4. 研究制作包身份加入现有内容一致性校验：CaseId、BakeHash及表现身份匹配后才能进入；客户端持有同版资源包，地形事实仍从Host与既有地图流取得。
5. 原生模块显示接入PresentationReady，等待对象、前景、矿层及实际本端绘制。无矿样例也要合法空矿层基线；Unknown不当Empty，不提前显示未知区域的整块地形。

研究默认不读写玩家存档；正式整图接入再核对地图、协议、存档和内容身份变化，不能预先宣称兼容。新地图未就绪或版本不匹配时明确失败，不偷偷载入旧地图。

R3订阅者在回调内将池状态冻结为本端副本，不保留可变引用跨回调／await。窗口关闭、域重载、切样例、断线时释放相应订阅；普通按钮回调不强行增加响应式层。

UniTask准备发生在业务事务外，版本令牌防止旧完成回写；Scene加载等不能真正取消的操作在完成后再次检查版本并释放。纯计算和Unity对象安装分开，Unity资源安装在主线程，不把SessionScope或池状态跨await／线程。

## 地图工坊UI

中文主标题“地图工坊”，小字MapModule标识来源；新流程不沿用旧八步导航。统一采用画布优先与左侧扁平地图目录，详细行为以上方[UI规范](#工作台接入与ui分区)为准：

- 地图目录直接选择可运行布局，主画布查看真实作者对象或只读预览；不附加研究／案例选择层，不驱动Unity切场景。
- 模块、拼装、试跑是任务切换；单片、片段、整图是观察范围，两组概念分别表达。
- 属性栏只显示当前模块／连接／样例相关参数；形状、接口、生态和启动设置分别按选中内容出现。
- 工坊保存作者源、检查、准备试跑版本分别呈现；试跑按钮受冲突／过期／准备状态约束。
- 同镜头切换六组变体，边界／真实形状／生态为显示开关，显示操作不修改作者数据。

源未保存、检查失败、Bake过期、缺表现资源和正在运行固定版本均有明确状态；不能用一个“完成”混合表达。本轮交互原型中的保存／烘焙／启动均为设计演示，不操作Unity或项目文件。

## 地图试验场UI

主题按原生Scene组织，当前入口片段、未来其他片段及完整地图复用同一运行接线。建议首Scene为`MapModuleEntranceStudy.unity`；一套Rig组合入口、主角、输入、相机和运行UI，样例组只包含模块／连接／锚点。

默认“画面优先”，备选“联机排查”。主画面保持扁平正侧视及真实主角比例。顶部呈现样例、观察范围、固定制作版本与本端角色；底部或折叠栏提供房主控制和只读诊断，不覆盖常用跳跃／落点视野。

运行绑定当前样例版本，换样例须结束当前试跑；作者源变化不热换房间地形。暂停／重开只由房主经原请求验证执行；客户端仅显示状态、自己的观察开关和退出入口。暂停不停止网络或UI，不用Time.timeScale停全局。暂不增加单步模拟，避免暗增另一套时钟。

联机排查展示本端收到的版本、对象／地形／矿层／绘制准备与当前连接状态，不显示假Ping或未经采集的性能数字，不假定可远程直播另一个进程画面。真实Host＋Client验证使用独立进程，不能由两块视口替代。

### 游戏内F1调试入口

复用现有唯一Runtime Debug Hub，拟注册稳定ID`dark_nights.mapmodule`的“地图试验场”页；Hub继续拥有F1、窗口生命周期和唯一Interaction Session输入租约，页面不另建第二个Hub或重复租约。接入遵循[Debug Hub合同](RUNTIME_DEBUG_HUB_SPEC.md)。

游戏画面常驻可收起的本端观察栏，显示主题／样例、固定制作版本、本端身份和状态；展开操作进入F1。F1打开阻断本机玩法输入，关闭后恢复；打开窗口不自动暂停权威世界。关闭F1不取消已提交的房主请求。

页面分为观察、问题点、联机、试跑四组：观察开关控制本端模块边界、真实权威碰撞、端口与生态，点选位置关联模块ID和局部坐标；问题点归本端有界诊断记录；联机页读取本端真实回执；试跑页选择已准备版本、开始／结束及房主暂停／重开。工作流为收起面板实玩→打开F1定位→记录→同版重开→返回工坊修改并准备新版本。

正式碰撞叠层必须读取当前权威地形／坡形及本端可见范围，不把作者曲线当作真实碰撞。当前界面预演只画“碰撞占位示意”，不计算正式运动或证明12坡形映射。角色位置和跳跃预演亦不能替代正式主角控制／通行验收。

记录建议关联StudyId、CaseId、BakeHash、来源／表现身份、模块ID、局部坐标／实际地形单元、角色位置、问题类型、截图和已收到的epoch／tick；未知字段明确缺失。首版界面保留最近4条，完整导出能力随实现确定，不暗开无限日志或伪造截图／联机信息。诊断记录不反写作者Prefab／Scene，也不写玩家存档。

运行时选择限已准备版本；试跑期间锁定样例／身份，结束后才可换例。重开复用同一制作版本并重建本次运行，保留本端问题记录；跨版本记录保留其原版本身份，不套用新版本。客户端只观察、记录和退出本端，暂停／重开经可信房主服务端校验，UI禁用仅作提示。

F1激活／停用按Hub范围订阅和取消；隐藏页停止刷新，退出会话释放对应R3、异步准备和资源租约。研究启动仍沿MM-02／03接入FishNet及YYGC，不以旧QuickTestPreset／F1矿镐快速局生成替代固定Bake。

首片被动生态表现草／垂根摆动、菌盖回弹与持续水流，独立启停和释放；复杂Spine拆件／绑定后续按资源与依赖核对，效果资源归Res/Effects。地形显示应消费当前权威格，正式采掘后不能留下整块静止岩面挡住挖空区域；首片研究无采掘，与正式能力保留分列。

### 旧工作台资料保留

旧界面结构、作者／候选所有者、文件保存域、八步过程及运行入口差别整理为[旧工作台参考索引](archive/MAP_WORKBENCH_REFERENCE_20261103.md)。74份核心工作台源码／meta／界面文件有定向快照与SHA清单，原文件和历史原型保持；这不是完整工程备份，也不代表旧代码已退役。新版只按明确能力复用基础服务与交互经验，不继承旧制作控制器。

## 代码、资源与执行

拟代码落点为Scripts/Editor/MapModule、Scripts/View/MapModule、Scripts/View/UI/MapModule、Scripts/Entry/MapModule，以及首片所需Core/Config/MapModule与Core/Logic/MapModule。现有Runtime/Network、Terrain、Session仅做上述有限适配，不为本片新建程序集。

原生资源归Res/Terrain/MapModule/Modules与Bakes，作者场景归Res/Scenes/Workbenches/MapModule，运行UI归Res/UI/MapModuleLab，Editor UXML／USS放对应Editor目录。所有路径以Game/Assets/DarkNights为前缀；不覆盖人工Prefab／Scene／AnimationClip／Theme，新增meta由Unity生成。

执行顺序为MM-00合同与隔离、MM-01一对模块的原生制作链、MM-02本机Host、MM-03独立Client、MM-04六组内容、MM-05片段／整图与正式替换，详见ROADMAP切片表。先贯通一个端到端样例，再扩大资源批次。

必要验收按未来获批切片执行：作者编辑／Undo／保存／重开、源变化使Bake失效、唯一样例与真实显示、Host权限／输入一次执行、独立Client版本匹配与Ready、错包拒绝、晚加入／断线及资源退出。几何检查不证明人物通行，D-19不建设自动可达／返程筛选的边界保持。实现默认只主动编译；本轮UI设计核对不等于授权运行游戏测试／Play／Player。

## 复评依据

仓库源码：SessionNetwork、SessionObjectLink、WorldSessionBehaviour、SessionClient、SessionTerrainNetwork、LevelLayout、WhiteboxEntry、WhiteboxSession及MapWorkbenchWindow。当前源码身份与保护输入在`artifacts/mapmodule-workflow-20261103/`中记录。

官方能力参考：[FishNet SyncTypes](https://fish-networking.gitbook.io/docs/guides/features/network-communication/synchronizing)、[R3](https://github.com/Cysharp/R3)、[UniTask](https://github.com/Cysharp/UniTask)、[Unity UITK EditorWindow](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-HowTo-CreateEditorWindow.html)。本地锁定源码和项目合同优先于外部最新版示例，本轮不执行版本更新。
