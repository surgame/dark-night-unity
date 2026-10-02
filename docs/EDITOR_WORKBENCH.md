# Dark Nights 统一工作台

2026-10-02。常用配置、预览与正式场景统一从 **Dark Nights → 工作台** 进入。窗口左侧按用途分组，支持分类筛选与名称、用途、资产路径搜索；左右栏可拖动调整，右侧显示编辑方式和唯一作者来源。完整聚合评估见[初版评估](EDITOR_WORKBENCH_ASSESSMENT.md)，最新 Editor 修正、运行侧 Definition 缺口与执行顺序见[复评及最终 Review](DEFINITION_EDITOR_REVIEW.md)。

| 分组 | 入口 | 编辑方式 |
|---|---|---|
| 原生对象 | Definition 浏览器 | 游戏 Res 中的对象与界面定义搜索、原生 Inspector |
| 玩法 | 工具与采集能力 | 主工作台直接编辑工具／矿床，实时匹配验证；支持独立窗口 |
| 玩法 | 星球与航程 | 原有表格、草稿应用与蓝图预览 |
| 玩法 | 飞船交易与装备 | 工作台内原生 Definition Inspector |
| 玩法 | 规则与会话 | balance.json 外部编辑、WorldSession 原生 Inspector |
| 地图与表现 | 地形业务与耐久 | 原有 Profile、耐久编译与单格试采 |
| 地图与表现 | 岩壁、背景与地表 | 原有 Cave Wall Tuner |
| 界面 | 飞船装备界面 | PanelSettings 原生 Inspector，UXML／USS／主题原生编辑器 |
| 场景 | ReferenceChamber、RandomCave、Expedition | 既有安全场景打开流程 |

原来的“配置／工具与采集能力”“配置／星球与航程”、Terrain 下的网格业务、Cave Wall Tuner 和三个正式场景快捷菜单已收进工作台。Content、Art、Build、Verify、Debug 和旧地形实验入口保留，避免初始化、资源安装和构建混入日常编辑。

工作台使用 UI Toolkit；采集编辑和 Definition 浏览器直接嵌入，原生 YYGC 配置工坊的 Odin 编辑区由 IMGUIContainer 承载。航程、地形编译和岩壁大画布点击按钮打开现有专用窗口，保留其草稿、应用／取消、预览资源和生命周期；切换工作台栏目不会应用或丢弃专用窗口草稿。资产页直接编辑原资产，显示未保存状态，保存按钮仅保存当前资产。采集匹配可切换矿床／前景岩壁、材料 Key 与稀有矿材料，复用正式冻结匹配规则并显示具体阻止原因。Play 或编译期间资产页的编辑控件禁用，各专用编辑器继续执行自身限制。

工具能力仍由工具 Definition 持有，矿床仍描述自身要求；地形与航程仍保存到原配置来源。导航目录没有业务配置副本，不改变游戏数值、资源身份、网络协议 22、存档 v15 或 YYGC 框架。

新增工具在 `DarkNightsWorkbenchCatalog` 登记分组、说明、作者资产和现有打开动作，不再新增日常配置顶层菜单。目录中的资产不存在时显示明确提示，不自动安装或重建。

本批仅改 Editor 工具。交互探针 190/190、原生重绘与定位保持各 5/5 通过，浏览器覆盖 44 个游戏 Definition。验收结果记录于 [机器证据](evidence/editor-workbench-20261002.json)，不沿用旧 Player 构建作为本批验证，也不重复构建 Player。

2026-10-02 复评后补齐专用采集保存的装配／白名单校验、窗口选择与匹配输入序列化、同入口搜索复用、原生 Undo／Redo 刷新和句柄／事件释放。通用 Definition 保存允许作者继续编辑未完成的装配；专用采集“校验并保存”要求完整且有效的采集装配。专项 Editor 11/11，生命周期及作者资产保持 52/52；运行侧装备与远征能力迁移按复评清单另行推进。
