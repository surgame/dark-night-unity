# 编辑工具聚合评估

2026-10-02；评估基线 `b4f4bad`，适用 Unity 6000.4.9f1。初版聚合未提交，用户要求先完成评估后再调整。盘点覆盖项目 Editor 菜单、现有窗口／草稿／预览实现、原生配置类型、YYGC Definition 编辑器和游戏作者资产；不修改 YYGC 依赖。

## 现状与问题

基线 Editor 源码共 52 个 MenuItem 属性（含移动倍率校验属性），五个 EditorWindow：四个当前工具与一个旧地图生成器。当前工具分别处理采集 Definition、星球航程、地形业务和岩壁样式，分散在“配置”与 Terrain。Objects 目录有 37 个 asset 文件，另有 UI、特效与地形作者资产。详尽菜单表达式及路径盘点保存在 `artifacts/editor-workbench-20261002/assessment-inventory.json`。

| 能力 | 唯一作者来源 | 编辑／提交语义 | 生命周期 |
|---|---|---|---|
| 工具／矿床 | 各自 ObjectDefinition 的 SharedConfigs／BehaviourTypes | 原生 Inspector 直接编辑、显式保存；匹配预览只读 | 缓存 Editor 随目标切换释放 |
| 星球与航程 | WorldSession 的 ExpeditionFlowConfig | ExpeditionFlowDraft 草稿；应用检查源配置冲突；支持 Undo／取消 | 纹理、后台任务和订阅显式释放 |
| 地形耐久／Profile | WorldSession Profile、原生 Terrain／GameplayDefinition | Profile／耐久草稿；冲突校验；应用编译新目录 | 单格试采与序列化草稿随窗口释放 |
| 岩壁／背景／地表／Modifier | CaveTerrainStyle 及引用资产 | TerrainStyleDrafts 逐字段草稿与冲突校验 | 离屏预览场景、GPU 纹理、后台生成与 update 订阅 |
| 交易／角色／建筑／UI 等原生对象 | 对象或 UI Definition | 原生 Inspector；业务数值仍引用 balance.json | 不新建会话或对象实例 |
| 数值／UI 布局 | balance.json、UXML／USS／Theme／PanelSettings | 使用各自原生或外部编辑器 | 不产生业务配置副本 |
| 当前场景 | 原场景及 GUID | 沿用未保存场景提示后打开 | 不生成或覆盖场景 |
| 初始化／安装／构建／验证／旧实验 | 各已有命令 | 属于工程操作或回归，按原权限执行 | 不由日常配置导航自动触发 |

初版的问题是复杂流程仍以打开按钮为主，采集页没有在主工作台直接操作；资产范围是固定短清单，不能方便浏览其他原生对象；左栏不能拖动宽度，分类筛选与保存状态不足。把所有现有编辑器的私有 OnGUI 或根元素强行移入主窗口，会使窗口生命周期与草稿冲突校验失配，尤其影响岩壁离屏宿主。

## 方案比较与选择

| 方案 | 优点 | 风险／不足 | 结论 |
|---|---|---|---|
| 只整理菜单／链接 | 改动少，旧工具完整保留 | 仍需跳窗，主工作台交互较弱 | 初版基础，需补充 |
| 全部重写成统一配置表与统一应用 | 外观一致 | 复制原始配置、改写不同保存语义、扩大地形编译与预览风险 | 不采用 |
| UI Toolkit 工作台＋可复用业务面板＋原生编辑器 | 交互统一，沿用原始资产和现有工作流 | 重型预览暂保留专用窗口；原生 YYGC／Odin 部分仍需 IMGUIContainer | 本轮采用 |

UI Toolkit 负责可拖动双栏、搜索、分类、选择列表、对象选择器、状态与操作按钮；深入核验发现 YYGC 普通 Definition Inspector 在绘制时自动同步 NetType，且隐藏完整 SharedConfigs，转由配置工坊编辑。因此本轮直接复用公开 WorkshopInspectorPresenter，并以 IMGUIContainer 承载其 Odin 属性树，保留只读身份、能力与原生配置绘制；不调用普通 Inspector 的隐式同步，也不在载入或聚焦时补齐配置。显式能力编辑或保存时才沿用 WorkshopConfigService 和原生保存入口。无需为了外观统一修改 YYGC 或把配置再次序列化。

## 本轮实现边界

1. `Dark Nights → 工作台` 为日常入口，保留工程命令和旧实验归组；增加分类筛选、搜索与可拖动左右栏。
2. 采集编辑提取为可复用 UI Toolkit 面板，主工作台直接选择工具／矿床、编辑原生 Definition、校验保存并查看匹配结果；原独立窗口复用同一面板，避免两份业务编辑代码。
3. 增加原生 Definition 浏览器，只枚举游戏 Res 范围，搜索完整 Key／名称／路径，点击即显示原生 Inspector。稳定 GUID 仅展示，不提供自动重建、分配或改名。
4. 原生资产页显示未保存状态及单资产保存；新界面在 Play／编译时禁用写入。搜索与切页不保存配置，也不应用专用工具草稿。
5. 航程、地形编译、岩壁大画布沿用现有专用窗口，工作台明确说明来源、草稿与应用方式。此次不接管它们的私有生命周期；全部嵌入式迁移需要另行拆出这些预览控制器并验证资源释放。

## 验证与风险收口

只改 Editor 代码与入口，不改数值、运行对象、资源身份、协议 22／存档 v15，不构建 Player。验证真实 Local Editor 编译、工作台与原生编辑器创建、分类／搜索／选择、空结果、匹配理由与来源目标、四个既有工具打开、切页和关闭释放。检查导航前后作者资产字节一致；对 UI 重构新增的匹配与生命周期逻辑进行定向验收。截图或人工视觉不能完成时明确列为待验，不用结构探针冒充画面通过。

面板复用公开 WorkshopInspectorPresenter、WorkshopConfigService 与 ObjectDefinitionSaveUtility；不实例化普通 ObjectDefinitionEditor，不触发其状态检查或绘制时 NetType 同步。导航不引入新的后台服务。复杂工作台若同时编辑同一作者资产，既有草稿冲突检查继续生效，主窗口不提供绕过冲突的“全部应用”。现有未提交工作单独保留，提交仅包含本批代码、入口和文档。

复核中发现商店 Definition 的 NetType 出现一次 1→0 自动变化；用户确认没有手动修改并授权恢复，已仅恢复该字段且核对资源文件无差异。此发现促成本轮使用工坊 Presenter 的最终选择，YYGC 框架代码未修改。

Definition 的“定位”仅 Ping 作者资产，避免导航切换普通 Inspector 再触发自动同步；非 Definition 仍按原生选择方式定位。最终交互探针 190/190、重绘保持 5/5、定位保持 5/5 通过，浏览器发现游戏 Res 中 44 个 Definition；未执行像素级截图验收。
