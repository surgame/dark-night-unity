# 工具 Definition 与采集能力

2026-10-02 实施于当前 Local 基线，沿用 YYGC 锁定依赖；本批没有修改框架源码。游戏协议 **22**，世界存档 **v15**，地图 AMP1 schema **2**。旧存档保留，当前入口拒绝旧格式，不自动迁移或删除。

## 人工配置

2026-10-03 当前入口：`Dark Nights / 工作台 → 对象与装备`，点击矿镐／矿床快捷目标进入原生 YYGC Definition Workshop。主工作台只保留采集装配与目标匹配辅助区；也可在 Project 中双击对应资产进入原生工坊。

- 正式矿镐：`Game/Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset`。`BehaviourTypes` 装配 `MiningToolBehaviour`；`SharedConfigs / 工具采集能力` 保存 `MiningToolConfig`。
- `Targets` 设置支持前景岩壁、独立矿床或二者。当前正式矿镐只支持前景岩壁，保持上一轮“不与隐藏矿床交互”的产品行为。
- `AllMaterials=true` 支持所有目标材料；关闭后只接受 `Materials` 中的稳定 Key。前景使用 AnyRuleD 材料 Key（如 slate、iron、gold），矿床使用其实际材料／资源身份（当前 iron、gold）。关闭且列表为空表示不支持任何材料。
- `Deposits` 为可选矿床 Definition 引用白名单；空列表表示所有矿床定义。只在 `Targets` 包含独立矿床时参与判断。多个矿床共享 Definition 时，使用 Materials 区分材料，不按实例 ID 配置静态能力。
- `Level` 与矿床的 `RequiredMiningLevel` 比较。当前数值：Damage=10，Reach=64，HandHeight=36，Seconds=0.48，ImpactFraction=0.6。Reach 在原生界面显示为“吸附与挥砍距离”，两者共用；本次从48扩大至64（4格）的原因和验证见[统一距离记录](archive/PICKAXE_UNIFIED_REACH.md)。
- 矿床资产：`Game/Assets/DarkNights/Res/Objects/MineralDeposit/MineralDeposit.asset`。矿床只配置最低采集等级、耐久、产量和材料；原 `AllowPickaxeHarvest` 已退出。

例如，要让某把镐只采铁矿床：在该工具 Definition 中启用 MineralDeposit，关闭 AllMaterials，Materials 填 iron；如还需限制矿床类型，在 Deposits 中选择对应 Definition。支持岩壁且限制材料时须同时列出需要的岩壁材料 Key。匹配预览使用与游戏同一纯匹配函数，但不替代距离、遮挡、权限和容量校验。

在采集辅助区检查装配、白名单与匹配；编辑与保存统一由原生 Workshop 执行，不编译地形或创建新地形目录。新会话加载冻结规则。地形业务工作台的工具页改为此入口；地形作者数据仍走原有独立编译流程。

## 状态与装配合同

工具能力通过 RequireConfig／Inject、生成注册和 YYGC ObjectInstance 装配。角色装备槽的唯一权威值为 canonical Definition GUID；Runtime 通过冻结目录解析对应 Definition，Core／存档／线缆只保存引擎无关的 GUID 字符串。视觉枚举仅由目录派生，两个同外观工具可同时拥有不同身份。

ObjectSessionResources 在启动时冻结装备目录并预加载采集工具。角色装备能力缓存只持有当前工具对象引用，不复制背包状态；换装、恢复、角色退池和会话退休释放对应装配。工具无独立耐久状态；动作计时与装备选择仍归 ActorState。

选取和服务端执行共享 MiningToolRules.BlockReason，比较目标类别、实际材料、采集等级和可选矿床定义。客户端只消费冻结装备目录与展示投影；服务端从自己的装备槽解析工具，不接受客户端声明的伤害或工具能力。

每轮动作冻结工具身份、选择版本、目标身份与瞄准角；换装取消待命中动作。矿床 Behaviour 最终匹配并修改自身耐久／存量；岩壁仍由现有 ARDMap 权威事务修改，不创建逐格对象。收益与目标修改沿用现有同一事务。

矿镐参数已从会话 HandheldConfig 移除；会话仍管理原有枪弹、炸弹及投射物状态。既有自动矿工伤害标定复用默认工具的冻结伤害，不受玩家工具目标白名单限制。独立地图工作台明确使用默认工具配置。

既有炸弹装备补齐独立 `item.bomb` 身份；定义复用已有 Prefab 引用，未添加新玩法或改动原炸弹数值。新资产通过 Unity 创建 meta 和 YYGC 的显式副本身份入口注册；现有工具、矿床及 Prefab GUID 保持。

## 验证与边界

- 六个程序集独立源码编译通过，Local Unity 导入／编译通过；剥离其他未提交改动后的暂存树六个程序集也独立编译通过。
- Editor 按影响合并 **62/62 个不同用例**。首轮 58/62，修正既有初始装备／空挥语义夹具及新增临时定义夹具后 61/62；最后一个恢复夹具保存稳定 actor ID 后专项 1/1。原失败报告保留，不合并为一次完整矩阵通过。
- 新增真实 YYGC 用例验证第二个工具 Definition 装配不同能力、冻结配置、同视觉类型不同装备身份、保存恢复和换装取消；实际采矿与去重由 MiningInputTests 覆盖。
- 单一 Mono 产物正常网络 Host＋Client **27/27**：购买、非法位置、重复支付、并发支付、暂停、晚加入、重连、canonical 装备身份以及实际写盘重启恢复。
- Core 回归 **1046/1048**，两项仍断言旧软岩／保护格规则；保留失败，不修改冻结规则证据来消除差异。
- ArchitectureGuard **20 项既有错误**，新增类型未新增守卫错误；其中 ObjectSession 超长、HeroInputSampler 内嵌类型仍是既有留账，不能宣称全局守卫通过。

弱网（200 ms RTT＋5% loss＋25 ms jitter）前段检查通过，但断开后立即重连未重新 Ready，100 秒后超时；报告包含 18 项通过检查（含退出后的日志检查），本批弱网整体失败。服务端已释放旧控制租约，新传输连接随后建立又被远端关闭；尚不能归因为工具身份或能力规则，不通过重复重跑掩盖此失败。弱网与各产物哈希见本批机器摘要。Player 中实际采矿、第二工具跨进程能力差异、四人、IL2CPP、双机器、完整人工画面和前台性能不计为通过。旧日期的构建结果不替代本批。

证据根：`artifacts/tool-definition-harvesting-20261002/`；正式机器摘要：`docs/archive/evidence/tool-definition-harvesting-20261002.json`。编译中间产物统一保留到 `artifacts/待清理/20261002-tool-definition-harvesting/`，不永久删除，不计作释放空间。
