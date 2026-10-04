# Dark Nights 工作台

2026-10-05 样式、菜单与浮窗改进。入口仍为 **Dark Nights → 工作台**。内容制作的四类任务改为左栏纵向导航，运行与检查区增加“场景与测试”；分类与内容分别滚动，后续分类增加不会挤成顶部页签。右侧固定显示当前分类标题、用途说明和常用场景行，下方显示工具卡片或完整场景目录。继续复用原生窗口，编辑、草稿和保存归属沿用上次聚合合同。

左栏常规宽度为212px，窗口小于900px时缩至168px并保留文字分类；右侧工具卡片改为上下排布，动作与文字不互相挤压。最低窗口680×480，左栏不足高度时独立滚动。分类ID、标题、简述和分组统一放在 `DarkNightsWorkbenchCatalog.Tasks`，窗口从这份元数据生成导航与页头。

| 项目任务 | 直接操作 | 状态归属 |
| --- | --- | --- |
| 对象与装备 | 原生 Definition Workshop、YYGC Viewer；矿镐、矿床、手枪、炸药、喷气背包、商店、出售服务、会话常用目标；采集辅助区 | 列表、分类、搜索、装配、共享配置、保存归 Workshop；项目辅助区只读校验 |
| 星球与航程 | 星球与航程编辑器、会话能力快捷目标、balance.json | 原窗口拥有草稿、冲突检查、应用／取消和预览 |
| 地图与表现 | 地形业务与耐久、Cave Wall Tuner | 原窗口拥有编译、草稿、离屏画布和释放 |
| 界面资源 | 原生 UI Builder／源文件打开、USS／主题、PanelSettings | 作者源文件和原生 Inspector，单资产显式保存 |

右侧常用场景行保留正式游戏、正式远征、地形预览、岩层参考。完整的14项场景目录从底部折叠区移到左栏“场景与测试”，使用独立搜索与可滚动列表；保留用途筛选、定位和当前场景标记。旧营地不回到导航，退役项仍只定位。打开场景沿用未保存提示，不自动 Play。对象页的“常用对象”单独分组，“采集装配与目标匹配”默认收起，展开状态跨切分类和界面重建保留。

## 独立浮窗与操作边界

工作台、Workshop、Viewer、地形业务、Cave Wall Tuner 和采集校验统一以独立浮窗打开。新窗口在 Unity 主窗口区域居中；已停靠窗口使用公开 `EditorWindow.position` 属性解除停靠，复用同一实例与草稿，已浮动窗口保留位置。仍可手动拖动停靠，下次从工作台打开时恢复浮窗。没有修改 YYGC 框架窗口源码。接口行为参见 [Unity position 文档](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/editorwindow/position)。

切换任务只更新项目操作区，不自动拉起编辑器。点击编辑动作才打开对应浮窗；关闭工作台不关闭专用编辑器。Viewer 直接按需打开，不调用会拉起其他管理窗口的 ObjectManagerSuite。

## 搜索、视觉与菜单

UXML／USS 使用统一的深浅主题色、青绿色主按钮、低强调快捷按钮、导航选中态、悬停、按下、键盘焦点和禁用反馈。窄窗口保留文字导航，卡片转为纵向排列。顶部全局搜索覆盖工具、场景、维护命令及原菜单路径；Ctrl／Cmd+K 聚焦，Esc／清除返回原任务，搜索内容跨界面重建保留。场景页保留独立筛选与路径搜索。

36 项原 Content／Art／Build／Verify／Terrain 命令迁入“工程维护”，按安装与迁移、构建、专项验证折叠分组。原方法继续执行，不通过已移除的菜单路径间接调用；搜索和打开工作台均不会运行这些命令，显式运行前显示用途确认。顶部保留工作台和“Debug／主角移动 8×”。同期矿层迁移任务新增的“Content／升级矿层地图配置”尚未被工作台取代，保留其入口与未提交源码。YYGC 原生 YY 菜单不在本次修改范围。

本批仅修改游戏 Editor 入口、样式与相关测试；不修改玩法、协议、存档、依赖版本，不构建 Player。2026-10-03 的框架授权及历史计数不代表本批修改或验收。

游戏侧 `DarkNightsDefinitionBrowser`、浏览器状态与 `DarkNightsDefinitionEditor` 已退出；对象列表、搜索和保存不再有游戏侧副本。采集辅助区与独立“采集校验”窗口复用同一只读面板：保留装配、白名单、材料／等级与目标匹配，按钮把选定工具或矿床转交 Workshop。辅助区不提供第二套配置编辑或保存。校验通过不替代正式采矿的距离、遮挡、状态和权限检查。

任务、作者来源、采集目标、匹配输入和场景搜索保存在窗口导航状态中。界面来源与规则来源分别记忆；重建、切任务和关闭释放项目 Inspector、事件与定时器，不关闭原生窗口、不提交其他窗口草稿。Play、导入、编译期间暂停项目编辑入口与场景打开；资料定位仍可用。缺失来源显示路径，不生成资产。

## 2026-10-05 本批验收

编译为 0 错误／0 警告。按不同用例合并 **16/17 通过**：导航与来源恢复、搜索与原菜单路径检索、Ctrl+K／Esc、36 项命令注册、浮窗实例复用、航程草稿保持、采集校验与 Undo、1120×760／680×480 展开内容布局均已有通过证据。实际宽窗口检查发现的页头与场景行重叠已修复；窄窗口的独立滚动和纵向卡片几何检查通过。深色 USS 已提供，完整深色主题视觉验收不宣称通过。

剩余 `NativeWorkspaceEditorTests.WorkshopUsesOriginalWindowAndTargetWithoutEmbedding` 在焦点后的 `EditorApplication.delayCall` 哨兵等待超时；保留所有失败 XML，不改写成通过。另行执行真实 Editor 焦点回调，确认回调完成、配置内容不变、dirty 不变、窗口未停靠；该实测不覆盖自动测试失败计数。测试诊断改为写入本次 XML，避免覆盖 2026-10-03 的冻结证据。

2026-10-05 跟进复测 `workbench-focus-fix-20261005-r1` 仍为 **0/1 通过**，失败位置不变。在等待中请求 `QueuePlayerLoopUpdate` 并重复 Repaint 未解决超时，试验改动已撤回；没有修改 YYGC。按用户要求结束本次调查，将 Editor 验证通道交给另一聊天。后续需定位完整测试流程中 delayCall 未执行的原因，再复测该用例及受影响的原生窗口回归；当前不宣称修复。失败报告为 `artifacts/terrain-final-20260926/workbench-focus-fix-20261005-r1.xml`。

5 项涉及导航的作者资产哈希保持。没有生成美术位图；界面采用现有 UXML 与确定性 USS。验证输出与截图保留在 `artifacts/workbench-refresh-20261005/`，机器索引见 [本批证据](evidence/workbench-refresh-20261005.json)。临时命令发现输出集中到 `artifacts/待清理/20261005-workbench-refresh/` 并保留清单，未永久删除；共享 Library 与其他任务构建继续保留。

## YYGC 导航修正（历史）

接入时确认锁定 Workshop 在切目标时自动补齐配置，获得焦点时强制同步并标记未保存；有效配置虽不变，dirty 仍变为 true。用户在本聊天明确批准两个工坊文件、新导航测试、隔离验证及可重建依赖接入，随后完成修正。

当前打开、切目标、切焦点及 Undo 后只刷新编辑树；能力编辑、显式同步和显式保存仍补齐必需配置。配置服务仅在真实变化时 SetDirty，forceRefresh 只通知刷新。完整和缺失配置的导航都通过内容／dirty 保持检查；显式同步、保存重开与 Undo／Redo 通过。项目没有屏蔽原生焦点或反射移动控件。

落点为隔离 detached checkout `D:/Developer/YYGC-worktrees/workshop-navigation-20261003`，检查点 `0d461a2`；游戏接入锁定基线 `fee1864` 加更新后的 `tools/grid-business/yygc.patch`。没有切换或修改用户 YYGC master，其已有 RuntimeDebugHub 改动保留。逐文件账本见 [YYGC_CHANGES](YYGC_CHANGES.md)，授权范围与实际结果见[修正记录](WORKSHOP_NAVIGATION_FIX_PROPOSAL.md)。

## 原生聚合历史验证（01d881d）

真实 Local Unity 6000.4.9f1 单一 Editor 通道。游戏侧任务／选择恢复、场景折叠搜索、错误装配与只读匹配、Undo／Redo、释放；原生目标转交、窗口复用、不嵌套、原保存／Undo 入口、Viewer 按需打开及航程草稿保持已执行。按最终影响合并为 **16/16 个不同用例**：项目辅助区与状态恢复9项、场景导航2项、原生接入2项、框架导航3项。最终计数、失败历史、资产保持与画面检查统一记录在[机器证据](evidence/workbench-aggregation-20261003.json)。

初轮原生焦点采样早于 delayCall，误得无 dirty；增加明确回调完成屏障后确认旧行为，并在获批修正后复测。修正后导航诊断为 navigation_clean=true、focus_marks_dirty=false、config_changed=false。框架新增回归使用独立持久夹具覆盖显式保存、卸载后重新读取及 Undo／Redo；没有改写游戏作者资产。

仅涉及 Editor 导航、排版、辅助区、测试与文档。协议 23／存档 v16／AMP1 schema 2 不变，没有 Player、IL2CPP、联机或性能构建。开始时已有 ObjectArchetypeDatabase 改动单独保留，不计入本次提交。

10-02 初版浏览器与内嵌编辑、10-03 前一版三个工作区的计数均是历史记录，不能替代本批：[初版评估](EDITOR_WORKBENCH_ASSESSMENT.md)、[初版证据](evidence/editor-workbench-20261002.json)、[此前重排证据](evidence/workbench-layout-20261003.json)。

436项作者资产磁盘SHA-256保持；已有ObjectArchetypeDatabase改动未纳入提交。实际宽／窄布局与矿镐原生停靠已检查；浅色主题已实看，深色主题保留样式但未人工验图。临时框架夹具、可重建测试镜像和备用Git索引9项共449,195字节已集中移至 artifacts/待清理/20261003-workbench-aggregation/，清单记录原／目标绝对路径；没有永久删除，也不计作释放空间。最终报告、截图、薄隔离checkout和共享Unity缓存保留。
