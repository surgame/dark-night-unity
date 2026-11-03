# 旧地图工作台实现与新版承接索引

2026-11-03按用户“相关信息不要丢失”的要求，记录当前旧工作台实现、关键制作约定和新版MapModule的承接关系。本页是参考索引；旧工作台当前操作仍见[编辑工作台](../EDITOR_WORKBENCH.md#地图设计工作台现行入口)，实际验证状态见[执行台账](../DEVELOPMENT.md#cl-map-workbench-redesign-status)，新版方案见[MapModule工作流](../MAP_MODULE_WORKFLOW.md)。

## 保存身份与范围

本轮保留现有原文件，并额外快照74份工作台相关源码、meta、UXML和USS，合计260607字节。快照保留原相对路径；分支、HEAD、各文件SHA-256和四份既有界面原型身份记录于[保全清单](../../artifacts/mapmodule-debug-console-20261103/preservation-manifest.json)，副本位于同目录的`legacy-workbench-snapshot/`。

这是当前本地输入的定向快照，包含未提交版本；不是完整Unity工程或依赖备份，也不证明旧工作台已验收。未复制整个Game、Library或依赖缓存，未移动／覆盖旧源码、人工资产、冻结地图、GUID、存档或失败证据。相关操作与计划文档的本轮修改前副本位于同证据目录`before/`。

## 两代工作台与本次调试台

“旧MapWorkbench”指已存在的程序地图设计窗口；“新版地图工坊”指2026-11-03讨论的MapModule作者工作流，两者不能混为一套。本次新增游戏内F1设计只补新版运行研究界面，不替换或丢弃此前已讨论的新版作者工作台。

新版仍按模块→拼装→试跑组织，已选画布优先；Prefab拥有模块内部形状，Scene拥有组装布局，Bake为只读固定制作版本，源变化使检查／Bake失效；首片一对模块先接Host再接独立Client，之后扩六组与整图。原型源码继续保存在[上一轮两套工作流UI](../../experiments/mapmodule-workflow-v1/README.md)，原`mapmodule-workbench-ui.html`及`mapmodule-lab-ui.html`的路径／SHA已记录，本轮未覆盖。MapModule原生实现、尺寸定稿和正式切换仍未完成。

## 已经采用的界面与交互结构

2026-10-28确认并实施的布局为四区：最左配置库、制作模块、常驻地图画布、当前模块基本／高级属性。不是此前更早的三页概念稿，也不是把八步当作必经向导。

- 文件选择集中在最左配置库，支持地图配置和只读完整结果；模块页切换保留输入、镜头和选区。
- 制作模块分别处理配方、地表、洞室、道路、入口、矿物、环境、岩壁／后壁、设计点及文件；基本配置优先，高级配置默认收起。
- 设计／渲染两种显示共用画布；格线、区域、连接、矿层、站点等叠层只影响本机查看，不触发作者改动。
- 八步过程消费同一次真实生成的阶段输出，位于画布下方、默认收起；可拖动／点选进度、前后步进并返回完整结果。历史地图缺过程时不补造。
- 普通调参保留本次种子；“按策略生成”才按固定／随机／池策略决定是否抽取。约80ms合并输入，后台纯计算串行，只接收匹配当前输入的结果；失败或过期保留上次有效画面。
- Undo／Redo恢复作者草稿。临时拆填及本机矿巢实验保持实验边界，不混入正式保存。

完整字段／能力覆盖、后续修订和未迁移项继续见[旧重构方案](../ROADMAP.md#cl-map-workbench-redesign)及[生成流程重构](../ROADMAP.md#cl-map-generation-flow-refactor)，不以此摘要代替原表或恢复暂停玩法。

## 源码职责与恢复入口

| 现有文件或类型 | 职责与保留理由 |
| --- | --- |
| [MapWorkbenchWindow](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchWindow.cs) | 原生EditorWindow宿主，保留原GUID／MovedFrom、作者草稿、Undo、预览生命周期和输入变化门控 |
| [MapWorkbenchView](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchView.cs)、[UXML](../../Game/Assets/DarkNights/Res/Editor/Terrain/MapWorkbench.uxml) | UITK四区结构、属性页与常驻画布；已有IMGUI画布只负责现有绘制能力 |
| [MapWorkbenchState](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchState.cs) | 本机导航、选区、叠层、文档与检查身份；不拥有业务地图或生成输入 |
| [ExpeditionFlowDraft](../../Game/Assets/DarkNights/Scripts/Editor/ExpeditionFlowDraft.cs)、[TerrainGenerationPreview](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/TerrainGenerationPreview.cs) | 旧生成参数作者与候选计算／阶段来源；不作为新版MapModule作者控制器 |
| [MapWorkbenchPreview](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchPreview.cs)、[FlowPage](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchFlowPage.cs) | 现有画布、表现及同次过程查看；保留镜头、旧版差异和阶段诊断实现参考 |
| [MapWorkbenchCommands](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchCommands.cs) | 编辑Guard、Undo、生成／抽样、核对、保存、用于新局及打开Bootstrap的显式操作 |
| [MapWorkbenchFiles](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchFiles.cs)、[FileLoad](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchFileLoad.cs)、[Library](../../Game/Assets/DarkNights/Scripts/Editor/Terrain/MapWorkbenchLibrary.cs) | 配置库、异步打开／取消、只读结果查看及返回原草稿 |

其余`MapWorkbench*Page`、Fields、Controls、Picking、DisplayTools、Typography、Chrome、Progress、Import、AssetBridge及样式文件均在上述保全清单。恢复或比较前核对SHA和用户后续修改，不直接用快照覆盖当前文件。

## 保存与运行的关键边界

旧作者保存归`TerrainMapDocument`，新写格式2保存完整规则；生成完整结果、私有表现与装饰位于配置旁的`<配置名>-Data/<时间批次>/`。原位保存保留配置路径／GUID，另存创建新配置。历史结果打开后只读查看；重新生成与参数变化均不能覆盖未明确保存的原结果。

“保存配置”和“将当前地图用于新局”是不同操作；后者才改变正式引用。文件菜单打开Bootstrap不自动Play。已有F1矿镐快速局独立生成，不能核对工坊保存的这一版地图。新MapModule试验场必须显式接收自己的固定Bake，不复用旧快速局作为地图来源。

## MapModule承接关系

| 旧版信息或经验 | 新版处理 |
| --- | --- |
| 配置库、常驻画布、当前属性的组织经验 | 保留为UI参考；新作者目录、窗口和状态独立，不把旧生成器加页签改名 |
| 镜头保持、只读显示开关、明确脏标记／检查／来源身份 | 沿用语义，绑定新Scene／Prefab及固定制作版本 |
| 原生Undo、异步取消、输入版本与失败保留有效结果 | 按新版作者所有者接入，保留旧实现供比较 |
| 保存、另存、只读结果和正式引用分离 | 新版Scene／Prefab保存、检查、Bake、试跑和正式切换分别明确 |
| 八步程序生成、旧配方、旧候选与空间随机域 | 原位保留供参考，不成为MapModule作者来源或新地形生成控制器 |
| YYGC资源／输入／对象、现有地形服务、FishNet及F1 Hub | 核对能力后复用；新工作流不复制业务世界、不另建第二套调试中心 |
| 旧原生场景、实验原型、美术来源、冻结地图和存档 | 保留原位置／引用；代码退役和默认地图切换仍是后续切片 |

新版游戏内界面预演源码见[调试台原型](../../experiments/mapmodule-debug-console-v1/README.md)。本页不表示已实施MapModule原生窗口、F1页、联机或正式地图替换。
