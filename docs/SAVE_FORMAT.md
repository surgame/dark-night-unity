# Unity世界存档v23

本页维护当前文件字段、身份／关系、基础会话零值约束与原子保存恢复。当前入口仅接受 **v23**，正式目录 `Saves/v23`；v22及更旧Unity／Godot文件拒绝、不迁移、不覆盖、不自动删除。网络epoch与房间权限见[联机合同](MULTIPLAYER.md)，运行版本和验收见[执行状态](DEVELOPMENT.md)。

## 文件合同

无BOM UTF-8 JSON，上限 **4,000,000字节**、深度32，根对象严格8个字段：

| 字段 | 合同 |
| --- | --- |
| format | `dark-nights.world` |
| format_version | 整数23 |
| random_algorithm | SimulationRandom.Algorithm，`godot-pcg32-clz-f32-v1` |
| rules_sha256 | 会话只读GameCatalog规范化SHA-256 |
| layout_sha256 | 校验后LevelLayout规范化SHA-256 |
| identity_sha256 | 规则→DefinitionGuid、PlacementKey→规则关系排序摘要 |
| equipment_sha256 | 当前冻结手持装备配置指纹 |
| world | 深度冻结的权威数据 |

精确嵌套字段以 `ObjectWorldSaveJson`、`SnapshotDocumentJson`、`SnapshotEntityJson`、`TerrainSaveJson`、`MineralSaveJson`、`ExpeditionSaveJson` 及对应Validator的显式映射为准。缺失／未知字段、重复JSON属性、尾随内容、非法enum、非有限数、越界值、重复身份或不兼容摘要拒绝，不用历史样例绕过校验。

world保留level_id、terrain、expedition、economy、wave、elapsed、speed、paused、next_entity_id、rng_seed／rng_state、actors、buildings、worksites、projectiles、stats、mode和identities等已有合同形状。格式保留字段不代表对应暂停业务可以恢复；见下方基础会话约束。64位RNG seed/state用有符号十进制字符串保存原位模式，不经浮点。

通用存档容器上限为256实体／1024投射物；当前手持非旧箭矢类型另有128个池容量约束。基础会话拒绝Kind=0旧箭矢，另施加合法对象类型及零值限制；不能把通用容器上限当作当前合法并发数或许可旧NPC。实际边界由SnapshotValidator及所属Validator联合校验。

## 身份与装备关系

每个identity严格含id、definition_guid、placement_key，实体与身份一一对应。GUID须匹配对应规则；非空放置键须来自当前作者布局，合法职业替换沿用身份的通用能力不授权恢复暂停业务。正式旧整数ID和Kind别名不兼容，独立Sample的LegacyV1另保隔离边界。

actors的姿态／装备包括height、vertical_speed、support_platform、ignored_platform、drop_remaining、manual_control、selected_item、selection_revision、jetpack_equipped／jetpack_fuel、explosive_charges及库存／货袋／照明字段；这是概要，不是完整字段列表。高度原地面为零、向上为正；原随机地图下限和平台支撑关系按当前布局校验。支撑0为权威地图，-1为空中，正数平台身份仅在对应合法布局有效。

四格库存以canonical Definition GUID为持有依据，选中装备、动作、数量和配置指纹必须合法。喷气背包是独立能力，燃料／装备关系仍校验。`light_definition`为规范GUID或空字符串，非空必须指向角色实际库存中的照明道具；没有合法手电不能light_enabled。移除同时清引用和开关，恢复不重新配发已移除手电。方向按角色朝向初始化，光场、调参、渲染缓存和后台任务不保存。

相机、选择、epoch、策略版本、连接代次、FishNet身份、ControllerSlot／Generation、ControlLease、默认人物偏好、按钮及输入序号不进入文件。恢复后连接只接回仍标记manual_control的合法主角；原专属ID指向普通闲置角色或无可恢复人物时新建默认主角，不恢复旧连接所有权或重放输入。

## 基础会话与两层地图

允许当前主角姿态、四格库存／信用点、合法装备动作／燃料／炸药数量、手持投射物、个人铁／金货袋、唯一飞船及最终两层地图。星球本地飞行属于Landed环境，Ship的Id、Phase、PilotId、VelocityX/Y、DoorClock、DockX／Height等关系按阶段验证，可恢复飞行阶段和高度。

当前actors只接受非敌方、manual_control为真的worker，旧目标／自动命中／强制攻击状态拒绝。NPC、工位、旧箭矢、自动任务、风险、探索计时、船仓货物、舱段、损失和返航结算遵循现有基础会话零值／空集合约束。设备只有飞船、乘员只有主角；保留的JSON字段必须满足这些约束。暂停范围见[地面玩法](GROUND_GAMEPLAY_BASELINE.md#暂停的玩法)。

正式world.mineral_deposits为空。`terrain.mineral_map`保存固定61440格最终矿种／耐久／储量及规则指纹的有界压缩数据，`terrain.deposits`为压缩初始静态元数据，不能据它补回已采空格。前景最终格／damage与矿层最终状态独立保存，两张候选和对象一起校验／准备后切换。

矿层解码核验格数、压缩／解压上限、尾随数据、矿种、耐久、初始容量和最终储量，不保存临时流代次。当前矿格业务及来源见[地形合同](TERRAIN_GENERATION.md)，旧按床实例方案只留历史。

## 内容摘要

SaveContentFingerprint从本局GameCatalog和已校验LevelLayout捕获。规则覆盖配置、资源、单位／建筑／工位参数、seed及有序波次；布局覆盖边界、地面、出生点、有序放置及主角／平台配置，排除本地CameraX。保留的冻结规则参与内容一致性，不自动开放暂停调度。

规则／布局编码域为 `dark-nights.rules.v5`／`dark-nights.layout.v2`，小端整数、IEEE754浮点、UTF-8字符串及明确集合长度；字典Ordinal排序，有序布局／波次不重排。编码域与文件格式版本是不同概念。

identity摘要由按Ordinal排序definitions／placements的紧凑JSON UTF-8求SHA-256，装备摘要由实际冻结能力目录生成。新增字段、规范编码或关系语义须明确升级合同，不能仅调整文字掩盖输入变化。摘要校验内容，不是签名，也不代替完整联机握手。

## 保存与恢复顺序

GameSaveStore注入专用目录及ObjectWorldSaveJson，构造不写磁盘。正式路径来自Unity persistentDataPath下 `Saves/v23`，槽位0–9，文件 `slot-00.dnsave.json` 至 `slot-09.dnsave.json`；客户端不能提交任意路径。

1. 模拟边界经ObjectSnapshotMapper深度冻结所属Behaviour State及最终两层地图。
2. 后台校验／编码，在同目录唯一临时文件写入、Flush(true)并关闭句柄。
3. 提交前检查取消；已有文件File.Replace，首次File.Move；不先删原档再覆盖，失败保留原文件。
4. 只清理本次失败临时文件，清理错误不掩盖原错误；提交成功不再报告取消，其他孤立临时文件不自动成为存档。
5. Read分配缓冲前检查长度，以严格UTF-8解析完整DTO，文件读取不构造世界。
6. 房主加载取得票据，候选在未激活上下文准备真实YYGC对象和两张地图；不提前修改原世界／场景对象。
7. 同步提交状态及索引，重接可复用原对象、退休旧世界，增加epoch、清空旧Ready／去重并发布新基线。房间策略保持，连接Ready后重建控制租约；失败保留旧世界和暂停／倍速。

同实例文件操作串行，后台不持有可写State／ObjectInstance或跨线程SessionScope；连接、加载票据和取消由SessionAuthority／SessionStorage处理，文件层不自行授予网络权限。

## 验证要求

当前验收须覆盖真实YYGC装配、装备／手电／货袋、飞船飞行姿态、前景损伤、矿层受损／采空、RNG及严格摘要，实际写盘、加载、关闭进程后重启恢复。错误字段／关系、坏UTF-8、超限、锁冲突、取消、并发任务及旧格式拒绝均保留原文件／当前世界。

测试只使用本任务隔离目录，不访问或重生成玩家旧档。正常Windows原子替换及锁冲突不等于断电、磁盘耗尽或跨操作系统持久性已验。旧U5／U6和矿层v19结果仅是历史来源，当前未完成项见执行状态；[覆盖映射](archive/YYGC_UNIFIED_TEST_COVERAGE.md)及[原矿层记录](archive/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)继续保留。
