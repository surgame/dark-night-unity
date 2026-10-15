# 编辑工作台、场景与诊断操作

本页集中原生编辑器入口、草稿／保存、独立预览和只读运行诊断。日常打开 `Dark Nights → 工作台`；地图权威规则、工具能力、照明及执行批次分别查专项合同。本页不以历史测试计数声明当前画面通过。

CL-00后独立洞穴工作台默认关闭喷气，沿用真实基础跳跃；原喷气勾选仅供显式历史预览，不能据此签署正式矿洞通行。正式商店及Debug Hub添加均关闭喷气，定义、原GUID和作者资源保留。

## 导航与作者编辑

| 分类 | 操作 | 草稿／保存归属 |
| --- | --- | --- |
| 对象与装备 | 原生Definition Workshop、Viewer、工具／矿床／商店／会话目标，采集校验 | 编辑／搜索／装配／SharedConfigs归Workshop；项目辅助区只读匹配 |
| 星球与航程 | 原配置窗口、会话目标、balance.json | 原窗口拥有草稿、冲突检查、应用／取消和预览；保留工具不恢复太空玩法 |
| 地图与表现 | 地形业务／耐久、Cave Wall Tuner、原生AnyRuleD | 原窗口拥有编译／草稿／离屏画布及释放 |
| 界面资源 | UI Builder、UXML／USS、Theme和PanelSettings | 作者源与原生Inspector，显式保存 |
| 场景与测试 | 搜索／用途过滤、定位及打开 | 沿Unity未保存提示，不自动Play；退役项仅定位 |
| 工程维护 | 原安装／迁移／构建／专项检查 | 搜索不运行命令，显式执行显示用途；遵循空目录初始化及有限批次规则 |

导航分类、标题、用途来自DarkNightsWorkbenchCatalog.Tasks。分类与内容分别滚动，窄窗口卡片纵排；常规左栏212 px，小于900 px缩至168，最低680×480。顶部搜索覆盖工具、场景、维护命令及旧菜单文字，Ctrl／Cmd+K聚焦，Esc／清除回导航；场景页独立过滤。

原生YY菜单保持，游戏侧不复制Definition Browser／Editor来另做配置保存。点击采集校验只转交选定Definition给Workshop，校验成功不替代距离、遮挡、权限和货袋容量。

## 独立浮窗与保存边界

工作台、Workshop、Viewer、地形业务、Tuner和采集校验复用独立浮窗及原草稿。新窗居中，已有浮动窗保留位置；手动停靠后再次从工作台打开恢复浮窗。切分类不自动打开工具，关闭工作台不关闭专用原生窗；Viewer按需直接打开。

窗口只保存导航／来源／匹配／搜索状态。重建／关闭释放项目Inspector、事件和调度，不提交其他窗口草稿；来源缺失显示路径，不生成资产。Play／导入／编译期间暂停项目编辑及场景打开，资料定位仍可用。操作实际资产前等待编译／导入屏障，保留人工Prefab、场景、Theme和GUID。

<a id="cl04-workbench-design"></a>

## CL-04地图与巢穴编辑方案（UI原型，未实现）

2026-10-15用户要求地图编辑工具台接入[巢穴协同生成](TERRAIN_GENERATION.md#cl04-generation-design)，提供地形／随机分布参数及共同预览，本轮先制作会话内visualize供审阅。原型包含“三栏协同编辑”和“画布优先编辑”两种布局，沿Cave Wall Tuner的地图生成、共用草稿及显式保存流程设计；不是新增Unity窗口或已接入生成器的功能。原型的参数值、范围、候选筛选及地图效果均为设计示例。

同日后续D-09将上述生成与参数编辑定位为**开发端地图生产**，CL-01与CL-04合并开发。工具目标扩为“作者草稿 → 共同生成候选 → 审核完整版本 → 导出入库”，游戏内只加载已审核版本；原型中的随机／重排操作不成为运行中重抽地图或巢穴的入口。生成、人工修整与最终加载边界见[预制地图目标](TERRAIN_GENERATION.md#cl01-approved-map-design)，设计指标见[CL-01目标](EXPEDITION_CORE_LOOP_GOALS.md#cl01-development-goals)。现有原型及24项浏览器结果继续只代表UI示例，不代表审核导出或Unity功能已实现。

| 编辑范围 | 参数／操作提案 | 适用与预览关系 |
| --- | --- | --- |
| 来源与复现 | 作者图选择、随机洞穴、预览种子、独立巢穴变化编号 | 作者图保留已有路线；随机图重建结构；仅重排巢穴保留基础地形输入 |
| 随机地形与矿脉 | 天然岔路、沿路距离分区／难度、竖向起伏、路边矿数量、露头比例／朝向、壁内延伸与规模档、分档矿种／储量预算 | 按D-11，深浅以路程及到达难度判定，竖向参数仅塑形；作者图按允许范围编辑，不生成梯子，不考虑回环；数值待定 |
| 富矿巢穴分布 | 富矿门槛、各路程档伴巢概率、数量上限／间距、空巢比例、类型／朝向及生物权重 | 富矿大概率伴巢且越深越高为已定方向，具体概率待定；显示空间／预算筛选后实际挂巢率及剔除原因，不把设定概率当实际数量 |
| 空间与风险约束 | 初始入口／出生安全、出虫净空、局部反应空间、塌方区域、允许改形范围、全堵／收窄与可挖性 | 初始地图与作者边界仍须合法；不以始终可返作为硬开关，梯具可破坏、动态封堵可断路，局部反应空间不代表保底逃生路 |
| 所选矿脉／巢位 | 稳定矿脉及SiteId、初始路径距离／档位、露头与壁内占格／储量、关联关系、巢位类型／朝向、必需标记及改形范围 | 点选关联矿脉与巢位，显示潜在规模和最终格容量；允许范围属于对应站点，必需静态点不合法仍阻止入库 |
| 预览与检查 | 改形前／协同结果、所选局部、巢位／净空／改形／退路／矿层／塌方／格线叠层、姿势切换、多种子抽样及点选 | 原型的状态姿势不推进业务；正式预览须复用最终地形、矿层与静态巢穴清单，不能用示意筛选冒充真实碰撞或联机验证 |

编辑继续写同一作者草稿，支持Undo和显式保存；作者槽位／改形范围最终归对应`.minemap`，分布与模板选择归冻结生成配置，不产生另一份运行权威规则。关闭自动预览后保留上一候选，明确标记草稿已变化，手动生成才替换预览；正式应用仍需现有来源冲突检查，不能用原型中的“保存草稿”当作Unity资产已写入。

联合实施还需显示完整版本ID／摘要、制作来源、基础能力和依赖身份、候选筛查／实走／事件核对状态及失败原因；保存草稿、生成预览与审核入库分别呈现。导出必须读取与审核相同的最终地形／矿层／站点数据，改动后标记受影响审核失效；不能在导出时另生成一次。图池选择属于后续正式加载，飞船关卡选择业务仍按Q-05处理。

按D-11，联合预览区分初始天然路线／路程深度、可见露矿／遮挡后的壁内矿脉、关联巢穴和玩家梯具需求；可在开发视图查看完整潜力，并查看玩家初始遮挡视图。显示初始往返与破坏后风险，不以自动补梯或修路保证返回；回程专项积怪刷怪后置。参数与旧原型不同的部分以[已答地图QA](EXPEDITION_CORE_LOOP_GOALS.md#cl01-map-qa)和[矿脉联合设计](TERRAIN_GENERATION.md#cl01-vein-nest-design)为准，不修改旧回执冒充新功能通过。

同日用户要求演示先对齐本地实际生成，实施组合见[四个演示阶段](ROADMAP.md#cl01-demo-milestones)。静态预览基线直接读取正式WorldSession引用的作者图／星球／已启用步骤，并调用本地已编译Core的`PlanetTerrainGenerator.GenerateCandidate`导出最终材料、坡形及矿床占格；记录程序集、配置、种子、作者源及输出摘要，不在浏览器另写随机地图。首版可先看地图／矿格及作者连接，声明连接不覆盖所有旧基线路径，也不证明角色可达；矿格“无岩覆盖”只指前景为空，不代替玩家视野或发现状态。路程深度、连续矿脉和巢穴尚未实现时不伪造现行结果，新增提案与已实现输出分别标识；参数变化后由本地生成器重新导出再显示，最终美术、实际操作和联机／保存仍需原生核对。

本轮浏览器核对只覆盖UI参数联动、地图点选与范围编辑、必需点失败、多种子选择、手动预览、Undo及两种布局／深浅主题／窄宽度；原始回执与截图保留在`artifacts/cl04-ui-20261015/`。正式编译、导入、生成一致性、碰撞／返程及运行验证均未执行，实施状态只见[DEVELOPMENT](DEVELOPMENT.md#stage-cl)。

## 场景目录

路径相对 `Game/Assets`。正式Bootstrap装配地面玩法，快速测试不新增场景；原场景名称中的Expedition不代表旧太空流程已开放。

| 用途 | 路径 |
| --- | --- |
| 正式入口／快速测试菜单 | `Scenes/Bootstrap.unity` |
| 正式地面内容 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` |
| 地形预览 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` |
| 编辑参考与同源Play预览 | `DarkNights/Res/Scenes/References/Terrain/ReferenceChamber.unity` |
| 静态营地专项回归 | `DarkNights/Res/Scenes/Regression/Camp/StaticCampRegression.unity` |
| 随机营地专项回归 | `DarkNights/Res/Scenes/Regression/Camp/RandomCampRegression.unity` |
| 单机地图探针 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` |
| 网络地图探针 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` |
| 场景功能开发／CL-02地质样板 | `DarkNights/Res/Scenes/Workbenches/Gameplay/GameplayLab.unity` |
| 退役八房间调试 | `DarkNights/Res/Scenes/Archive/Terrain/TerrainDebugBootstrap(old).unity` |
| 退役旧洞穴 | `DarkNights/Res/Scenes/Archive/Terrain/CaveExploration(old).unity` |
| 退役早期背景 | `DarkNights/Res/Scenes/Archive/Terrain/CaveContourStatic.unity` |
| 默认空模板，不构建 | `Scenes/SampleScene.unity` |
| URP2D模板 | `Settings/Scenes/URP2DSceneTemplate.unity` |
| LAN／输入独立样例 | `Samples/LanCoop/Content/LanCoop.unity`、`Samples/YYGCInputActions/Content/InputActions.unity` |
| 第三方Console演示 | `Plugins/EdgarDev/Smart Console/Demo/Demo.unity` |

当前Build Settings及GamePlayerBuild显式列表保留Bootstrap、StaticCampRegression、RandomCampRegression和Expedition；日常入口只有Bootstrap。GameScenePaths／RandomLevelEntry管理正式路径，TerrainScenePaths管理参考／预览／探针／退役路径。营地回归和退役资产不因目录整理被移动或恢复为产品入口。

### 场景功能开发样板

从「Dark Nights → 工作台 → 场景与测试 → 场景功能开发 · 地质事件」或「Dark Nights → 工作台 → 打开场景功能开发样板」打开；打开不自动Play。保存后正常Play由既有ScenePlaySelection转交Bootstrap服务，再初始化独立本地功能会话，不要求进入主菜单开房。场景未加入Player构建列表。

场景现以既有裂谷／断脉迷宫关卡的两个格子片段和正式岩壁／背景渲染展示，原矿洞源不改。初始出生点784，左侧近处低岩是支撑目标，右侧橙色矩形为开发用进入区域提示；顶部显示两事件阶段及安全停止原因。移动、跳跃、左键挥镐沿用正式动作／改键，鼠标从实际手高计算瞄准。前兆掉灰后3×3局部岩团分成九块，错峰重力下坠、逐块扬尘／散石，落地形成5／3／1实际瓦砾堆；不再是一格高横条或保证直接跳越。先避让，再用矿镐清障；占位只延后实体填格，不把石块悬停在半空。Esc暂停／继续同时冻结事件和粒子，恢复不补发输入。停止并重新Play重置，不保存运行破坏。

场景和 `Res/Effects/Geology/Event` 中的局部Definition／事件Prefab均可原生编辑，`Res/Effects/Geology/Debris.prefab` 集中编辑灰尘／石粒；源岩、落点和保护地面须与场景对应。初始化只制作首版，不能覆盖后续作者修改；`GeologyCaveSceneSetup.Upgrade` 为本批显式迁移入口，会重排本样板布局，后续人工编辑后不能把它当普通刷新执行。当前两段不能替代正式五图、完整返程、联机或Player验收；本批实际截图与限制见DEVELOPMENT。

待验：分别挖支撑和进入区域，确认前兆可读、源岩不可提前清除、落点占位不填岩；移开后瓦砾落地，矿镐逐格清理后不再触发；从两侧跳越并返回，前兆／落石／占位时暂停恢复，退出／再次Play无遗留对象。地图换代、异常来源、候选回滚及正式挥镐的定向源码见GeologyEventTests，执行结果只在DEVELOPMENT登记。此场景不证明正式矿洞全图返程、伤害、多人或事件存档通过。

## 编辑态地形与草稿

Cave Wall Tuner顶部“保存全部（Ctrl+S）”显式保存地图／航程／表现草稿，输入字段中也可用；底部可分别保存两类草稿。Modifier启停归地图／航程，状态卡片收起后仍显示启用／停用并可切换，顶部显示启用数量。关闭时丢弃还原作者配置，临时拆填不写正式地图。

地图画布获得焦点后1／小键盘1切逻辑网格，2／小键盘2切渲染网格；隐藏时也重新开启。青色逻辑网格中心为整数，金色渲染网格错半格；手动选择保持，再切拆／填才默认逻辑。笔刷始终操作逻辑格，切叠图不改目标或资产。字段输入、鼠标拖动、航程示意及Play不误触编辑态快捷键。

正式地图参数来自WorldSession的完整生成配置。固定蓝图选择与“应用地图到固定资产”是退出的旧操作，不在现行步骤使用。地图生成及作者来源见[地形合同](TERRAIN_GENERATION.md#生成与配置)。

## Play预览与调参

ReferenceChamber／RandomCave独立Play预览使用WorldSession配置，左栏F1收起／打开；这是独立预览面板，不是正式会话Hub。每页独立滚动：地图负责模式／拆填／撤销／材料／网格／种子／定位；岩壁负责外轮廓／造型／刷新；背景负责三层；显示负责UI／镜头；状态负责进度／错误和提交。

角色模式Tab切行走／观察，左键试采、右键预览爆破；其他模式左键拖动、中键平移、滚轮缩放。面板内滚轮只滚UI，文本焦点屏蔽角色快捷键；笔触从面板开始不穿透，进入面板／松键／失焦结束。UI缩放0.75–1.75及逻辑栏宽280–520独立于镜头，小窗口限制缩放、页签保持可访问。

样式约0.4秒后重建表现，保留当前预览格、笔触历史、角色及初始背景。应用样式建立取消基线，取消／字段恢复不写源；Editor Play中显式“保存样式资产”才保存共享样式及改动Modifier／点缀，按字段冲突检查及Undo拒绝覆盖其他窗改动。Player不显示写工程按钮。

笔刷历史按连续拖动事务记录预览差异；外部试采改过同格后撤销／取消拒绝覆盖。退出Play丢未保存修改，重载／R／换种子重置预览，已保存共享样式影响全部引用场景。这些操作不访问玩家存档，不能替代正式采集收益或联网验收。

## 运行地图与网格检查

| 入口 | 用途 |
| --- | --- |
| `YY → AnyRuleD → 地形工作台` | 编辑态制作规则／试画；Play选择已有前景或矿层并打开检查窗 |
| `YY → AnyRuleD → 网格调试器` | 只读逻辑格、DualGrid四角、配方、页事件 |
| AnyRuleD联网→同步调试器 | 前景副本／连接／流与权威对比；矿层尚未登记该联网诊断入口 |

从Bootstrap当前地图或现有快速局进入，选“前景地形”／“矿层地形”。输入U／V，或Scene按Shift＋左键选逻辑格；游戏(x,y)对应(U,V)=(x,-y)，宿主位置／缩放按绑定Transform转换。查看TileId／Height／Flags、材料Key／GUID、NW／NE／SW／SE来源、缺角mask、配方和资源版本；可开启Chunk／Page／halo有界叠图，不扫全图。

两层借用现有ARDMapController.Debugger，本端冻结局部输入构成表现地图，未订阅为Unknown。求解配方／页进度不证明GPU已绘制，也不代替网络权威对比。窗口不创建第二张权威地图，不推进耐久、收益或提交；切图／关窗不释放宿主，退休退出列表，退出Play撤销借用。独立Player没有此Editor桥。

运行地图另存画布仍需显式操作，作者目录匹配、至多4096格且无Unknown；不完整正式局部星球地图拒绝导出。数据生命周期不因诊断工具改变。

## 光照调参台

光照调参入口为 `Dark Nights → Debug → 手电光照调试`。左侧窄纵向栏切光束、返光、补光、场景、灯口、来源和历史；右侧复用正式矿洞／角色表现独立预览，可调格位置、方向、自动转向速度和镜头。只有底部「保存全部／Ctrl+S」写回，清单同时包含单灯、场景、灯口及命名调试记录。记录存于工程本地 `Game/UserSettings/DarkNights/LightingDebug/`，历史重放只恢复草稿，旧条目不覆盖。具体来源、失败恢复、关闭／Play和待验规则见[照明调参合同](LIGHTING.md#调试窗口持久化撤销与重做)。

## 主角地面诊断

打开 `Dark Nights → Debug → 主角地面接触／跳跃调试 → 打开Profiler浮窗`，或Window→Analysis对应窗口。本地Host／离线工作台Play可开始、停止和显式导出；Client不记录权威运动步骤。只读记录不调跳跃、碰撞或土狼时间，编译Player时排除。

浮窗提供10 Hz曲线、状态、逐步和最近CSV回看。取消跟随或点曲线只冻结视图，关闭窗不停止记录；导出也不停止。最多7200步，停止／到上限／退出Play只冻结内存，SessionState跨脚本重载保留。成功开始新轮清空该轮内存、快照和导出路径，已导出磁盘文件保留；最近记录显式回看，失败保留原视图。

输入诊断schema2关联同一输入序号的本地采样、发送、权威接受／模拟处理，使用单调时钟；上限1024事件，epoch变化停止。实际Replica与Host即时展示分别标记，不能拿本地显示作为远端同步证据。导出到 `artifacts/hero-ground-trace/<时间与短ID>/` 的summary.json、steps.csv、terrain.json及jump-inputs.json；附近65×23碰撞格开始时冻结，地图版本变化另记。

“主角移动8×（快速跑图）”仅停止Play时设置、下次生效，只改变Host横向跑图；正常移动／跳跃手感验收关闭。未起跳丢支撑／近地空中只是诊断标签，结合真实格形状和轨迹判断。旧Host输入／展示修复与失败证据见[执行状态](DEVELOPMENT.md)及[主角原证据](evidence/hero-input-presentation-20261005.json)。

## 操作来源与验收

场景、运行预览和网格整合原记录见[场景快照](archive/SCENES.md)、[运行预览](archive/RUNTIME_TERRAIN_TUNER.md)、[网格记录](archive/TERRAIN_GRID_DEBUGGER.md)。双网格／快捷键／保存的机器来源分别见[网格](evidence/cave-wall-grid-modes-20261006.json)、[快捷键](evidence/cave-wall-grid-shortcuts-20261006.json)、[保存](evidence/cave-wall-modifier-save-20261006.json)。这些是原批次证据，当前待验只在DEVELOPMENT核销。
