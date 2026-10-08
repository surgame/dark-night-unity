# Unity 世界存档 v21（地面装备玩法）

2026-10-08 手电候选采用 **v22**：新增角色 `light_definition`（规范 GUID 或空字符串）、`light_enabled`。身份必须指向完整照明能力 Definition；无手电不能开启。照明方向、输入占用和渲染缓存不保存，恢复按角色朝向初始化。v21不作为本候选输入，旧档不自动删除。见[实施记录](FLASHLIGHT_IMPLEMENTATION_20261008.md)。

2026-10-07[地面基础玩法](GROUND_GAMEPLAY_BASELINE.md)使用v21／协议27，独立 `Saves/v21` 槽位；旧档保留且不迁移。已有JSON映射形状保留，基础会话允许合法装备动作／燃料／炸药数量、手持投射物及个人货袋；仍拒绝NPC、工位、旧箭矢、自动任务、风险、探索计时、船仓货物、舱段和结算状态。保留主角、飞船、库存／信用点和最终两层地图；星球本地飞行可保存。下面矿层初轮通过数属于v19历史产物。

2026-10-05 [原生矿层](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)采用v19／协议25。`terrain.mineral_map`保存规则指纹及61440格最终矿种／耐久／储量的有界压缩载荷；初始静态矿床元数据另外压缩，旧`mineral_deposits`必须为空。最终空格覆盖初始矿格，解码及候选恢复严格校验，旧档保留且不自动迁移。全范围夹具完整717985字节，Editor受损／采空／无效恢复通过；当前同一Mono正常两人／四人及弱网两人的真实写盘、加载与重启恢复通过。

版本演进见[项目摘要](archive/PROJECT_HISTORY.md)，架构来源见[迁移摘要](archive/MIGRATION_HISTORY.md)。当前矿层合同优先以[实现记录](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)为准；下述通用会话合同需结合现行两层地图路径阅读。

上述写盘／恢复数字属于矿层初轮产物。长槽位路径修复后的最终Mono矩阵尚未完成，状态见[开发执行计划](DEVELOPMENT.md)。当前入口仅接受v21，旧格式拒绝读取并保留旧文件。

## 文件合同

无 BOM 的 UTF-8 JSON，上限 **4,000,000 字节**，解析深度32。v21根对象严格只有8个字段：

| 字段 | v21合同 |
|---|---|
| format | dark-nights.world |
| format_version | 整数21 |
| random_algorithm | SimulationRandom.Algorithm，当前为 godot-pcg32-clz-f32-v1 |
| rules_sha256 | 实际只读 GameCatalog 的规范化 SHA-256 |
| layout_sha256 | 实际场景导出 LevelLayout 的规范化 SHA-256 |
| identity_sha256 | 当前规则→DefinitionGuid、PlacementKey→规则关系的排序摘要 |
| equipment_sha256 | 当前手持装备配置的指纹 |
| world | 深度冻结的完整权威数据 |

world 包含 level_id、terrain、expedition、economy、wave、elapsed、speed、paused、next_entity_id、rng_seed、rng_state、actors、buildings、worksites、projectiles、stats，以及 mode、identities。`expedition.Ship` 保存 Id、Phase、PilotId、VelocityX/Y、DoorClock、DockX/Height；其他远征乘员、设备、结算与舱段数据同档。总实体上限 256，在飞箭矢上限 1024；数量、字段类型、有限数字、范围与实体关系由 SnapshotValidator 验证。64 位 RNG seed/state 使用有符号十进制字符串保存原位模式，不经过浮点转换。精确嵌套字段由 `ObjectWorldSaveJson`、`SnapshotDocumentJson`、`TerrainSaveJson` 和 `ExpeditionSaveJson` 的显式映射共同定义，不能用旧版样例代替验证。

每个 identity 严格包含 id、definition_guid、placement_key；实体与身份一一对应，GUID 必须匹配该实体 RuleKey。非空放置键必须来自当前场景，职业替换仅允许合法单位定义间沿用原放置身份。重复键、未知／缺失字段、重复 JSON 属性、尾随内容和不兼容摘要均拒绝。

actors 的运动与装备字段包括 height、vertical_speed、support_platform、ignored_platform、drop_remaining、manual_control、selected_item、selection_revision、jetpack_equipped、jetpack_fuel、explosive_charges；这不是当前 Actor 的完整字段清单。高度以原地面为零、向上为正；随机模板允许负高度至底部基岩顶面 -2416，支撑 0 表示权威地图支撑，-1 为空中；固定模板的正数表示场景平台 ID。支撑关系、范围、燃料和库存数量必须合法。

正式v21的 `world.mineral_deposits` 必须为空。最终矿格数据来自 `terrain.mineral_map`；`terrain.deposits`是压缩的初始静态元数据，不能据它重新生成最终矿格。前景 `damage` 与矿层最终值分别保存，完整候选一起校验后替换。

文件不包含相机、选区、epoch、连接代次、FishNet 身份或房间共享策略；也不保存 ControllerSlot、ControllerGeneration、ControlLease、默认人物偏好、输入序号和按钮意图。恢复后的手动角色保留姿态、手动标记与装备；新 epoch 完整投影 Ready 后，服务端只接回这些已保存主角。连接仍记录的专属 ID 若指向不带手动标记的普通闲置村民，必须视为不可恢复并新建默认村民；旧连接所有权和输入不会恢复或重放。

## 内容摘要

SaveContentFingerprint 直接使用本次会话的 GameCatalog 与经过校验的 LevelLayout。规则覆盖配置名称、说明、资源、单位／建筑／工位参数、关卡 seed 和有序波次；布局覆盖边界、地面、出生点与按出生顺序排列的放置记录，排除本地 CameraX。

规则与布局使用二进制编码域 dark-nights.rules.v5／dark-nights.layout.v2，纳入 hero_control 和有序平台定义：小端整数、IEEE 754 float/double、UTF-8 字符串及显式集合长度。字典按 Ordinal Key 排序，布局和敌人序列保留顺序。这两个域标记描述摘要编码，当前格式版本为v21。

identity_sha256 对按 Ordinal 排序的 definitions／placements 对象做紧凑JSON UTF-8 SHA-256，值来自实际加载定义和场景放置关系；正式矿床不创建实体身份，旧动态矿床规则见[地形历史摘要](archive/TERRAIN_HISTORY.md)。`equipment_sha256`校验装备配置。新增格式字段或修改规范编码须明确升级合同。摘要用于内容一致性，不是文件签名，也不能替代协议27的完整握手摘要。

## 保存与恢复顺序

GameSaveStore注入专用目录与本局ObjectWorldSaveJson，构造无磁盘写入。正式入口使用独立v21子目录，提供槽位0–9，文件名为slot-00.dnsave.json至slot-09.dnsave.json；客户端不能提交任意文件路径。

1. 权威端在模拟边界通过 ObjectSnapshotMapper 捕获全部 Behaviour State 的冻结副本。
2. 后台 Save 校验并编码，写同目录唯一临时文件，Flush(true) 后关闭句柄。
3. 提交前复查取消；已有文件用 File.Replace，首次用同目录 File.Move。没有先删原文件的后备覆盖路径，失败保留原档。
4. 失败仅清理本次临时文件，清理错误不掩盖保存错误；成功提交后不再报告取消。其他孤立临时文件不自动成为存档。
5. Read 在分配缓冲区前检查长度，以严格 UTF-8 读取；它不构造世界。ObjectWorldSaveJson.Parse 完整验证后返回冻结 DTO。
6. 房主加载取得票据并停止模拟推进，ObjectWorldRestore 在未激活上下文准备真实 YYGC 对象；准备期间不改变原场景对象。
7. 同步提交完整状态与索引，精确重接可复用场景对象，退休旧对象；成功增加 epoch、清空旧 Ready／去重窗口并重新发完整投影。房间共享策略保持，连接重新 Ready 时恢复已保存主角，必要时新建默认村民；失败保留当前世界和暂停／倍速。

后台任务不持有可写 State、ObjectInstance 或跨线程 SessionScope。同实例文件操作串行；加载票据、连接身份和取消在 SessionAuthority／SessionStorage 处理，文件层不自行决定联网权限。

## 验证

U5 的 GameSaveScenarios 检查 v2 活跃状态恢复、两局确定性继续模拟、坏字段／关系／身份／内容摘要，以及旧版本明确拒绝。GameSaveFileScenarios 覆盖首次保存、原子替换、真实文件锁冲突、取消、坏 UTF-8、超限、并发保存和孤立临时文件；均通过真实 YYGC 测试装配入口执行，见[覆盖迁移](archive/YYGC_UNIFIED_TEST_COVERAGE.md)。

U6 的最终 Player 已复验施工、训练、在飞箭矢组合恢复及继续模拟，以及四人保存／加载、晚加入、凭据恢复和重开；同一产物下坏档与旧版本明确拒绝并保留世界。具体报告见 [U6 证据](archive/evidence/yygc-unified-u6.json)。历史 v1／独立纯计算通过数不计作新版存档通过。

测试只写本任务产物目录，不访问玩家旧存档。正常 Windows 原子替换和锁冲突检查不等于实际断电、磁盘耗尽或跨操作系统持久性验证；IL2CPP 和双机器 LAN 的状态另列。
