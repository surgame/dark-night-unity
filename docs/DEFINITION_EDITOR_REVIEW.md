# Definition 与 Editor 复评及最终 Review

2026-10-02；基线 `9c9b8cd`。本轮先复核四个现行工具、聚合窗口、原生工坊 Presenter 与保存 API，再盘点游戏 Res 中全部 44 个 Definition、装备／角色／设备运行入口、冻结目录、商店和内容指纹。原始清单保存在 `artifacts/definition-editor-review-20261002/definitions-inventory.json`。现有其他未提交工作保留。

## 架构判定标准

Definition 应声明对象身份、业务能力及能力使用的配置或规则引用；YYGC Behaviour／实例 State 拥有可写状态，会话负责组合、调度与跨对象事务。Core 的纯算法及只读规则、网络展示 DTO、UI 不承担另一份权威状态。

`balance.json` 是项目规定的数值唯一来源。ActorRuleConfig、BuildingRuleConfig、WorksiteRuleConfig 从 Definition 显式引用其中条目是合规接入；不能为了“全部放进 Definition”重复复制 HP、价格、波次和全局远征规则。关键缺口是具体对象能力仍由枚举、固定 Key 或集中方法决定，或者 Definition 仅提供显示身份。

## Editor 最终 Review

| 编号／优先级 | 已核实问题 | 修正方向 |
|---|---|---|
| E1／P1 | 采集页保存使用可空配置校验，错选普通 Definition 也能显示“已校验保存”；未检查配置与 Behaviour 同时存在，白名单是否真的指向矿床 | 保存前进行无副作用的装备、采集／矿床装配与引用校验；失败不调用原生保存 |
| E2／P1 | 采集面板与 Definition 浏览器的目标、子页、查询只在 VisualElement 内；域重载会回到默认目标 | 由 EditorWindow 序列化导航状态，面板绑定这份视图状态；不存业务配置副本 |
| E3／P2 | 主窗口每次搜索都重建详情，源资产下拉回到首项；相同 Definition 列表刷新仍可能通过选择事件重建编辑树 | 详情按入口身份复用；源选择持久化；列表恢复使用无通知选择，保留原生编辑树 |
| E4／P1 | Native 工坊包装器没有显式 Undo／Redo 刷新与状态切换订阅；250ms 刷新前控件初始仍启用；OnModified 没有 Play／编译守卫 | 使用 attach／detach 管理事件，初始化立即更新可写状态；原生属性树在 Undo／Redo 后更新，修改回调同样检查状态 |
| E5／P2 | 地形业务窗口 Reload 丢弃旧 SerializedObject 而不 Dispose，关闭时也未释放该句柄 | 替换与关闭时显式释放，保留现有草稿与编译流程 |
| E6／P2 | 矿床选择时 DefaultMaterial 在保护代码之外调用 SingleOrDefault，重复配置会把字段回调变成异常 | 异常转成面板提示，选错资产与作者配置不完整均可继续编辑 |

继续保留的正确决策：UI Toolkit 管导航与布局；公开 WorkshopInspectorPresenter 管原生能力和 SharedConfigs；不调用会在绘制时同步 NetType 的普通 Inspector；Definition 定位仅 Ping；复杂航程、地形编译与岩壁离屏预览保留独立生命周期。通用 Definition 保存允许作者保存尚未装配完成的资产；“采集校验并保存”是具有明确业务校验承诺的专用操作，两者不能混用。

框架级剩余边界：原生工坊能力槽 GenericMenu 的回调直接操作 Presenter 目标，普通 DisabledScope 不能证明跨 Play／关闭后的旧弹出菜单回调安全。包装器守卫只能保护自己的回调。该边界列为后续 YYGC 隔离修正与专门验收项，本轮不通过修改缓存依赖或反射替换框架私有逻辑绕过。

## 尚未完成 Definition 能力化的运行逻辑

| 编号／优先级 | 对象／链路 | 具体证据与缺口 | 建议切片 |
|---|---|---|---|
| R1／P1 | 手枪 | item.pistol 只有 EquipmentItemConfig，BehaviourTypes 为空；HeroEquipment 按 Pistol 枚举派发，枪速／伤害／枪口／射击间隔从会话 HandheldConfig 读取 | 工具自身射击 Config＋Behaviour；目录冻结，原 ProjectileBehaviour 继续拥有弹体状态 |
| R2／P1 | 炸药 | item.bomb 只有身份配置；按 Bomb 枚举派发，蓄力／冷却／引信／爆炸数值来自会话；动作时长还有 0.25 常量 | 投掷能力配置与 Behaviour；发射时冻结完整弹体参数，既有伤害／地图事务复用 |
| R3／P1 | 喷气背包 | item.jetpack 没有能力 Behaviour；购买后只保存 JetpackOwned／Equipped bool，定义身份丢失；推力和燃料来自 HeroControlDefinition | 保留 ActorState 唯一燃料状态，增加实际装备定义身份与冻结喷气能力；需单独规划网络／存档升级 |
| R4／P1 | 商店与装备展示 | 服务端价格映射、三个购买按钮及槽名称固定；Entry/ShipEquipmentPanel 按视觉类型判重，无法区分同外观不同 Definition；Buy 仍读取活的 EquipmentItemConfig，目录仅冻结 Kind／Mining | 商店货架引用真实 Definition 与价格规则；装备元数据冻结；UI 从只读目录派生名称／购买能力，服务器保持全部授权与支付验证 |
| R5／P1 | 远征矿工 | expedition.miner 未装配矿床采集能力；ExpeditionDevices 按 RuleKey 分派，直接读取 item.pickaxe 的冻结 Damage 并调用 HitByHand，修改玩家矿镐会改变矿工伤害 | 独立自动采集能力与自己的规则引用；可复用匹配算法，玩家装备不再是 NPC 默认规则来源 |
| R6／P2 | 搬运机器人／侦察机 | hauler、scout-drone 只有通用角色／移动／战斗装配；搬运与侦察由 ExpeditionDevices／Drone 固定 RuleKey 启动，半径／巡检轨迹等为常量 | 各自能力 Behaviour，协调器保留任务分配；同一 ActorState 继续拥有任务与货物 |
| R7／P1 | 远征炮塔、氧气／仓储／灯与部署组合 | Definition 只有 BuildingRuleConfig；炮塔在 ExpeditionThreat 硬编码 160 范围／6 伤害／1.2 周期；设备功耗 4／3／1 和组合数组固定；氧气／仓储使用全局远征参数 | 设备能力与规则引用先拆炮塔；全局供电预算、风险、部署事务仍归远征会话，不复制平衡数值 |
| R8／P2 | 飞船驾驶／舱段 | 船 Definition 是 Building＋ShipServices，航行、舱段、召回按固定 ship 与集中协调器执行；配置同时散于全局规则和 ShipGeometry | 逐步声明可驾驶／舱段能力与来源引用；会话航程、场景到达事务与船体 BuildingState 保留 |

矿镐与矿床本轮已按自身 Definition 声明能力；保留采集算法、权威角色动作、地图耐久和矿床存量的原归属是正确的。角色／建筑／工位现有 RuleConfig 与通用 Behaviour，以及会话的航程／规则、AnyRuleD 原生材料、视觉 Style／Modifier、UI Definition 也不列为“未按 Definition 开发”。R5–R8 指的是特有远征能力尚未显式声明，不能将已存在的 YYGC 对象误称为完全未使用 Definition。

## 最终决策与开始执行范围

审查结论：保留现有聚合路线，先落实 E1–E6，把可恢复、可保存、可撤销和生命周期边界补齐。本轮实施 Editor 稳固切片，不改运行数值、资产 GUID、协议 22／存档 v15，也不构建 Player。

后续运行切片顺序为 R1＋R2 → R3＋R4 → R5 → R7 → R6＋R8。每批均先冻结能力与规则引用，复用现有 YYGC 状态、资源与权威事务；新增第二种同视觉不同定义内容验证扩展性，随后执行 Editor、单一 Mono 与独立 Host＋Client；涉及线缆／持久字段的切片显式升级协议与存档。既有弱网重连失败仍保留，不能用本轮 Editor 验收掩盖。

实施后进行最终代码复核与定向 Editor 验证：错误装配／错误白名单不能保存、作者源未被导航修改、视图状态序列化及 GUI 重建恢复、搜索不重置详情、Undo／Redo 恢复真实配置、attach／detach 订阅释放、地形句柄释放。结果与未验边界记录在机器证据中。

## 首批实施与再次代码复核结果

E1–E6 已落实；新增 WorkbenchEditorTests 专项 11/11，真实窗口生命周期与 44 个作者资产字节保持探针 52/52。保存会先应用尚未提交的序列化编辑，再作业务校验，之后才同步原生依赖配置并写盘；未持久化临时对象和已关闭编辑器拒绝保存。测试窗口序列化往返与 CreateGUI 恢复已验，未强制触发实际域重载。首轮 8/11 的三个失败属于未挂载面板的 UI 回调夹具，修正为真实 EditorWindow 后通过，初轮报告保留。

最终审查保留原方案的状态归属与原生编辑接入；本批修复有定向证据。R1–R8 和原生弹出菜单跨上下文边界仍是明确的后续工作，不将本批 Editor 修复称为整个 Definition 能力迁移完成。无运行数值、资源身份、网络／存档结构或 YYGC 框架改动；没有构建 Player。证据见 [机器摘要](evidence/definition-editor-review-20261002.json)。
