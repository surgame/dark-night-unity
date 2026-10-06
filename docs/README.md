# Dark Nights 文档索引

当前本地 `main` 已集成原生矿层与 `ft-20261006-terrain-debug-integration`，协议25／存档v19／AMP1 schema2。这里保留长期合同、日常入口与当前实现；历史过程以主题摘要为入口，操作、来源、未完成验收和周报从[归档索引](archive/README.md)进入。不同日期和 Player 的测试结果不能相互替代。

## 当前工作

- [开发执行计划](DEVELOPMENT.md)：当前完成情况、最终弱网矩阵与剩余门槛。
- [原生矿层实现](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)：矿格唯一状态、墙后发现、局部副本、保存恢复及本批证据。
- [弱网补测过程](archive/WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)：原始失败、修复、构建身份与中断状态；未完成组保持待测。

## 长期合同

| 文档 | 内容 |
| --- | --- |
| [技术架构](ARCHITECTURE.md) | 状态归属、程序集、装配和事务 |
| [联机设计](MULTIPLAYER.md) | 可信连接、权限、Ready、epoch 与恢复 |
| [存档格式](SAVE_FORMAT.md) | 当前 v19 格式、严格校验和原子保存 |
| [依赖说明](DEPENDENCIES.md) | 锁定版本与准备入口 |
| [YYGC 改动账本](YYGC_CHANGES.md) | 框架授权、逐项差异与验证边界 |

## 日常开发与操作

| 文档 | 内容 |
| --- | --- |
| [快速上手](QUICK_START.md) | 本地准备与开发入口 |
| [Player 指南](PLAYER_GUIDE.md) | 试玩、装备、采矿与手工检查 |
| [场景索引](SCENES.md) | 当前实际路径和用途 |
| [编辑工作台](EDITOR_WORKBENCH.md) | 原生编辑器、搜索、草稿与保存 |
| [工具与采集配置](TOOL_DEFINITION_HARVESTING.md) | 工具 Definition、材料要求与能力合同 |
| [运行时地形工作台](RUNTIME_TERRAIN_TUNER.md) | Play 预览、调参和拆填 |
| [地图与地形](TERRAIN_GENERATION.md) | 现行地图入口、状态与碰撞职责 |
| [运行中地形网格检查](TERRAIN_GRID_DEBUGGER.md) | 正式游戏前景／矿层、逻辑格和 DualGrid 四角检查 |
| [Smart Console](SMART_CONSOLE_INTEGRATION.md) | F10 日志与 F1 调试入口 |
| [Unity CLI](UNITY_CLI_WORKFLOW.md) | 后台有限操作、测试与构建 |
| [LAN Sample](LAN_SAMPLE.md) | 独立模板，正式代码不反向引用 |
| [目录用途](WORKSPACE.md) | 所有主要文件夹的用途和保留依据 |

## 历史与证据

- [归档索引](archive/README.md)：4份历史摘要，以及仍有用途的专题、审查、未完成验收和周报。
- [当前矿层机器证据](evidence/mineral-map-migration-20261005.json)：保留原始结果与输入身份。
- [主角输入与展示修复证据](evidence/hero-input-presentation-20261005.json)：69项定向检查、编译结果与源码身份；不代表当前 Player 联机或手感验收。
- [地形调试整合证据](evidence/terrain-grid-debug-integration-20261006.json)：两层只读检查、正式Host、Scene定位、退休释放与可重现补丁；未构建Player。
- [工作台双网格证据](evidence/cave-wall-grid-modes-20261006.json)：拆填默认逻辑、半格拾取、自由切换与实际画面；原未保存草稿保留。
- [工作台网格快捷键证据](evidence/cave-wall-grid-shortcuts-20261006.json)：1／2与小键盘切换、按钮高亮、字段与拖动保护、最小窗口布局。
- [工作台保存与Modifier状态证据](evidence/cave-wall-modifier-save-20261006.json)：顶部保存、Ctrl+S、启停卡片、保存重开与草稿保护。
- [历史机器证据目录](archive/evidence/)：按当前引用与用途保留的来源、框架、操作和待验记录；重复旧证据已按本次整理范围移除。
- [第三方声明](third-party/)：持续保留许可与来源。
- [2026-10-05 整理回执](archive/FOLDER_ORGANIZATION_20261005.md)：移动清单、保留项目与验证。

- [2026-10-07精简回执](archive/DOCUMENT_CONSOLIDATION_20261007.md)：归档合并、旧证据移除、保护范围和Git恢复入口。
