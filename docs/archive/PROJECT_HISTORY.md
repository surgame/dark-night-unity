# 项目演进摘要

2026-10-07 整合根README、开发计划、架构、联机、存档、场景、地形和工作台的重复历史快照。现行版本与完成状态始终以[开发执行计划](../DEVELOPMENT.md)为准。

## 已集成的主要切片

| 时段 | 项目演进 | 继续保留的查阅入口 |
| --- | --- | --- |
| 2026-09-10～13 | Godot规则与原生资源迁移、可信会话、YYGC统一对象、旧模型退出及有界投影 | [迁移摘要](MIGRATION_HISTORY.md)、[YYGC账本](../YYGC_CHANGES.md)、[回归映射](YYGC_UNIFIED_TEST_COVERAGE.md) |
| 2026-09-14～16 | Linear画面校准、原生主视图、结果UI、主角输入及默认专属worker | [表现摘要](PRESENTATION_HISTORY.md)、[Player指南](../PLAYER_GUIDE.md) |
| 2026-09-17～22 | 随机地图、手采／恢复、洞穴工作台、远征营地、独立岩层、三层背景和可步入飞船 | [地形摘要](TERRAIN_HISTORY.md)、[飞船记录](WALKABLE_EXPEDITION_SHIP.md) |
| 2026-09-24～29 | 局部地图联网、太空到星球流程、原地安全着陆、交易装备和冲刺 | [航程实现](SPACE_TO_PLANET_IMPLEMENTATION.md)、[航程验收](SPACE_TO_PLANET_ACCEPTANCE.md)、[交易验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md) |
| 2026-09-30～10-03 | 采矿网格、Definition能力、工作台、地形Modifier、氧气移除与现有背景衔接 | [工具合同](../TOOL_DEFINITION_HARVESTING.md)、[氧气记录](OXYGEN_REMOVAL_VALIDATION.md)、[工作台](../EDITOR_WORKBENCH.md) |
| 2026-10-04～05 | 内嵌矿从逐床状态迁到原生地图、墙后发现、两层局部流；主角跳跃发送与Host展示修复 | [矿层合同](../TERRAIN_GENERATION.md)、[执行状态](../DEVELOPMENT.md) |
| 2026-10-06～07 | 地形网格检查、Cave Wall Tuner双网格／快捷键、草稿保存与Modifier启停展示集成到main | [地形调试操作](../EDITOR_WORKBENCH.md#运行地图与网格检查)、[执行状态](../DEVELOPMENT.md) |
| 2026-10-07～09 | 地面玩法收敛与装备入口恢复、日志／内存保护、手电和可复用库存照明、Runtime Debug Hub合并及物体图标面板 | [地面范围](../GROUND_GAMEPLAY_BASELINE.md)、[照明合同](../LIGHTING.md)及[原证据](REUSABLE_LIGHTING_20261009.md)、[Debug Hub集成](RUNTIME_DEBUG_HUB_MERGE_20261009.md)、[图标面板](DEBUG_HUB_ICON_PANEL_20261009.md) |

2026-10-07该次集成保留已保存的WorldSession入口走道Modifier关闭配置，除该开关外配置语义不变；合并只核对Git差异和引用，未重跑Unity或构建Player。当时本地main为协议25／存档v19／AMP1 schema2，Editor6000.4.9f1；后续状态查开发执行计划，不沿用此批次版本。

## 仍有效的决策与边界

游戏以2–4人合作、共享营地为基础；2026-10-07起正式默认入口收敛为Bootstrap→星球地面，开放／暂停玩法以[地面范围](../GROUND_GAMEPLAY_BASELINE.md)和后续获批切片为准。旧营地、历史太空航程和独立工作台的代码／场景保留不代表当前产品已开放。日常Unity宿主只有Game，旧基线与参考项目不作导入、构建和运行目录。

所有业务由YYGC对象／会话唯一权威写入；客户端及Host表现读取冻结副本。旧整数身份、旧运行模型和历史存档成功导入不再是产品要求；原始素材、冻结规则及用户旧存档继续保护。当前身份、目录、权限、存档和框架授权分别以长期合同与AGENTS.md为准。

前台性能由用户明确暂缓；IL2CPP需单独授权，双机器需实际设备。后台容量、单层小样、隐藏截图、测试调度成功或旧Player的通过数均不能替代当前验收。弱网矩阵、主角手感、改键和洞室通行按现行执行状态核销；矿工路线、结算及二次航程随对应玩法恢复再验，不因历史待办自动扩大当前范围。

2026-10-05目录整理将311个临时文件约48MiB集中移入待清理，归档不释放空间、不授权永久删除；[原整理回执](FOLDER_ORGANIZATION_20261005.md)继续保留。2026-10-07整理仅精简仓库文档和选定旧证据，未整理这些产物、.codex数据或其他聊天工作树。

## 历史原文

周报、可操作说明、美术来源、框架逐文件账本、未完成验收和清理清单仍在[归档索引](README.md)。已整合的全文及重复机器摘要不再常驻工作树；从[2026-10-07整理回执](DOCUMENT_CONSOLIDATION_20261007.md)列出的Git基线和旧路径恢复，不把已移除文件描述为仍在目录中。

AGENTS中移出的20段阶段记录及仍有效约束的承接，见[2026-10-09协作约定整理回执](AGENTS_CONSOLIDATION_20261009.md)；完整原文保留在该回执列出的Git基线。

## 顶层合同与过程分离

2026-10-09按用户批准的32份逐篇清单将顶层收敛为17份现行入口，现行范围、对象／地图、权限、文件、工具、照明、操作和依赖账本各自有承接；15份过程及独立样板退出顶层。原Bootstrap并发准备、工具能力、地面装备／日志、矿层及照明的原计数／失败保留在对应机器证据和Git基线，当前状态统一DEVELOPMENT。

完整移动／承接、未完成项与原文恢复见[整理记录](README.md#2026-10-09顶层职责整理)。本次只核验文档，没有新增Unity或Player通过结论。
