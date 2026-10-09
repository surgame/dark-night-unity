# 编辑工作台、场景与诊断操作

本页集中原生编辑器入口、草稿／保存、独立预览和只读运行诊断。日常打开 `Dark Nights → 工作台`；地图权威规则、工具能力、照明及执行批次分别查专项合同。本页不以历史测试计数声明当前画面通过。

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
| 退役八房间调试 | `DarkNights/Res/Scenes/Archive/Terrain/TerrainDebugBootstrap(old).unity` |
| 退役旧洞穴 | `DarkNights/Res/Scenes/Archive/Terrain/CaveExploration(old).unity` |
| 退役早期背景 | `DarkNights/Res/Scenes/Archive/Terrain/CaveContourStatic.unity` |
| 默认空模板，不构建 | `Scenes/SampleScene.unity` |
| URP2D模板 | `Settings/Scenes/URP2DSceneTemplate.unity` |
| LAN／输入独立样例 | `Samples/LanCoop/Content/LanCoop.unity`、`Samples/YYGCInputActions/Content/InputActions.unity` |
| 第三方Console演示 | `Plugins/EdgarDev/Smart Console/Demo/Demo.unity` |

当前Build Settings及GamePlayerBuild显式列表保留Bootstrap、StaticCampRegression、RandomCampRegression和Expedition；日常入口只有Bootstrap。GameScenePaths／RandomLevelEntry管理正式路径，TerrainScenePaths管理参考／预览／探针／退役路径。营地回归和退役资产不因目录整理被移动或恢复为产品入口。

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

## 主角地面诊断

打开 `Dark Nights → Debug → 主角地面接触／跳跃调试 → 打开Profiler浮窗`，或Window→Analysis对应窗口。本地Host／离线工作台Play可开始、停止和显式导出；Client不记录权威运动步骤。只读记录不调跳跃、碰撞或土狼时间，编译Player时排除。

浮窗提供10 Hz曲线、状态、逐步和最近CSV回看。取消跟随或点曲线只冻结视图，关闭窗不停止记录；导出也不停止。最多7200步，停止／到上限／退出Play只冻结内存，SessionState跨脚本重载保留。成功开始新轮清空该轮内存、快照和导出路径，已导出磁盘文件保留；最近记录显式回看，失败保留原视图。

输入诊断schema2关联同一输入序号的本地采样、发送、权威接受／模拟处理，使用单调时钟；上限1024事件，epoch变化停止。实际Replica与Host即时展示分别标记，不能拿本地显示作为远端同步证据。导出到 `artifacts/hero-ground-trace/<时间与短ID>/` 的summary.json、steps.csv、terrain.json及jump-inputs.json；附近65×23碰撞格开始时冻结，地图版本变化另记。

“主角移动8×（快速跑图）”仅停止Play时设置、下次生效，只改变Host横向跑图；正常移动／跳跃手感验收关闭。未起跳丢支撑／近地空中只是诊断标签，结合真实格形状和轨迹判断。旧Host输入／展示修复与失败证据见[执行状态](DEVELOPMENT.md)及[主角原证据](evidence/hero-input-presentation-20261005.json)。

## 操作来源与验收

场景、运行预览和网格整合原记录见[场景快照](archive/SCENES.md)、[运行预览](archive/RUNTIME_TERRAIN_TUNER.md)、[网格记录](archive/TERRAIN_GRID_DEBUGGER.md)。双网格／快捷键／保存的机器来源分别见[网格](evidence/cave-wall-grid-modes-20261006.json)、[快捷键](evidence/cave-wall-grid-shortcuts-20261006.json)、[保存](evidence/cave-wall-modifier-save-20261006.json)。这些是原批次证据，当前待验只在DEVELOPMENT核销。
