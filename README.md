# Dark Nights · Unity

当前本地 `main` 已集成 `ft-20261004-embedded-minerals` 的开发成果：游戏协议 **25**、存档 **v19**、AMP1 schema **2**，Unity Editor 锁定 **6000.4.9f1**。默认从 Bootstrap 进入太空船与星球远征，2–4 人合作；旧营地保留专项回归入口。

矿物采用会话托管的独立 AnyRuleD 地图，矿格耐久与储量只有一个权威所有者。拆前景墙只露出对应矿格，采空与恢复由地图保存；客户端前景与矿层均局部订阅、加载。实现与验收边界见[当前矿层记录](docs/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)。

后续弱网补测的最终 Player 矩阵仍有未完成组，不能将前一构建的通过数作为最终矩阵结论。当前状态见[开发执行计划](docs/DEVELOPMENT.md)。专项前台性能、最终手感、IL2CPP 与双机器等门槛仍按各自证据记录。

| 入口 | 内容 |
| --- | --- |
| [文档索引](docs/README.md) | 当前合同、配置、操作与进行中工作 |
| [目录用途](docs/WORKSPACE.md) | 工程、工具、实验、证据与待清理的归属 |
| [快速上手](docs/QUICK_START.md)、[Player 指南](docs/PLAYER_GUIDE.md) | 依赖准备、试玩和专项检查 |
| [场景索引](docs/SCENES.md)、[工作台](docs/EDITOR_WORKBENCH.md) | 正式入口、预览、回归与参考 |
| [Unity CLI](docs/UNITY_CLI_WORKFLOW.md) | 当前 Local 的后台 Editor 通道 |
| [历史索引](docs/archive/README.md) | 旧方案、过程、周报及冻结证据 |
| [协作约定](AGENTS.md) | 工作区、框架修改授权、分支与验收规则 |

`Game/` 是唯一日常 Unity 宿主。依赖通过锁定配置接入，见[依赖说明](docs/DEPENDENCIES.md)；源码与作者资源位于 `Game/Assets/DarkNights/Scripts` 和 `Res`。

2026-10-05 已整理文档与可确认的临时产物，详见[整理回执](docs/archive/FOLDER_ORGANIZATION_20261005.md)。旧根 README 的时间线与最初评估输入保存在[历史快照](docs/archive/PROJECT_CHANGELOG_20261005.md)。
