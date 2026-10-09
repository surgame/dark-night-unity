# Dark Nights · Unity

2026-10-09 [Debug Hub 物体面板改造](docs/DEBUG_HUB_ICON_PANEL_20261009.md)位于 `fix-20261009-debug-hub-icons`：48×48 单格选择、32px 图标及单一滚动，按业务身份筛出30个物体；复用29个图标／静态帧，喷气背包由指定 provider 生图派生。正式Host界面、筛选、三种宽度及添加／移除回执已检查；协议31／存档v23／AMP1 schema2不变，YYGC未修改，没有新增Player验收。

2026-10-09 本分支已接入[可复用照明与手电道具](docs/REUSABLE_LIGHTING_20261009.md)：共用环境照明／目标补光Prefab及头部挂载变体，手电进入四格库存、占一格，切工具后继续照明。当前协议 **31**／存档 **v23**／AMP1 schema **2**；专项10/10及正式Bootstrap短时探针1/1通过，扩展回归3个旧玩法失败留账，YYGC保持。

2026-10-09 已按用户要求将 Runtime Debug Hub UITK 候选合入 `ft-20261008-flashlight-lighting`，统一协议 **30**／存档 **v22**／AMP1 schema **2**。保留手电控制、局部照明、地形加载修复及调试物体网格和房主操作。合并验证与工作树保全结果见[集成记录](docs/RUNTIME_DEBUG_HUB_MERGE_20261009.md)；旧候选和旧 Player 结果不计入本次合并验收。

2026-10-08 新候选 `ft-20261008-flashlight-lighting` 接入正式手电与局部洞穴照明，协议 **28**／存档 **v22**／AMP1 schema **2**；沿用下方地面玩法范围。当前验证边界和人工验收表见[实施记录](docs/FLASHLIGHT_IMPLEMENTATION_20261008.md)。YYGC保持锁定版本。

当前开发候选 `ref-20261007-ground-gameplay-baseline` 保留地图地形、主角移动／跳跃、镜头、联机、基础保存恢复、购买与飞船驾驶／起飞／降落；按用户追加确认恢复矿镐光标、采集、手枪／爆破／手持投射物和喷气背包。开局直接在星球地面；其他玩法暂时退出运行。游戏协议 **27**、存档 **v21**、AMP1 schema **2**，Unity Editor 锁定 **6000.4.9f1**，见[实施与边界](docs/GROUND_GAMEPLAY_BASELINE.md)。

地形与原生矿层仍由会话托管的AnyRuleD地图拥有，客户端分别局部订阅和加载；手持采集与道具作用走同一权威事务，采矿收益仅进入个人货袋。NPC、警戒刷怪、物流、出售、升级与返航结算继续暂停。作者资产、离线制作工具和暂退实现保留，原矿层历史实现见[记录](docs/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)。

本轮验证发生F10缺字警告反馈引起的Unity内存耗尽，现已加入有界日志、中文回退和独立内存保护，见[事故记录](docs/UNITY_MEMORY_INCIDENT_20261007.md)。当前状态见[开发执行计划](docs/DEVELOPMENT.md)。旧Player结果不能代替本候选；前台性能、最终手感、IL2CPP与双机器仍按各自证据记录。

| 入口 | 内容 |
| --- | --- |
| [文档索引](docs/README.md) | 当前合同、配置、操作与进行中工作 |
| [目录用途](docs/WORKSPACE.md) | 工程、工具、实验、证据与待清理的归属 |
| [快速上手](docs/QUICK_START.md)、[Player 指南](docs/PLAYER_GUIDE.md) | 依赖准备、试玩和专项检查 |
| [场景索引](docs/SCENES.md)、[工作台](docs/EDITOR_WORKBENCH.md) | 正式入口、预览、回归与参考 |
| [Unity CLI](docs/UNITY_CLI_WORKFLOW.md) | 当前 Local 的后台 Editor 通道 |
| [历史索引](docs/archive/README.md) | 历史摘要、保留专题、周报及必要来源 |
| [协作约定](AGENTS.md) | 工作区、框架修改授权、分支与验收规则 |

`Game/` 是唯一日常 Unity 宿主。依赖通过锁定配置接入，见[依赖说明](docs/DEPENDENCIES.md)；源码与作者资源位于 `Game/Assets/DarkNights/Scripts` 和 `Res`。

2026-10-07 已将重复归档说明整合为4份主题摘要，并移除部分旧实现证据，见[精简回执](docs/archive/DOCUMENT_CONSOLIDATION_20261007.md)。项目演进见[历史摘要](docs/archive/PROJECT_HISTORY.md)；2026-10-05临时产物归档仍见[原整理回执](docs/archive/FOLDER_ORGANIZATION_20261005.md)。
