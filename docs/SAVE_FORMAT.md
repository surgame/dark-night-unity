# Unity 世界存档 v10

2026-09-22 [可步入远征飞船](WALKABLE_EXPEDITION_SHIP.md)升级为 **v10**／协议 **14**。`SessionSnapshot.CurrentVersion` 与正式握手常量分别为 10、14；冻结 `ExpeditionShipData` 随远征状态进入同一权威存档，恢复时校验候选并原子替换。空中读档保留位置与乘员，释放驾驶占用并归零速度；旧版本严格拒绝，原文件不自动删除。具体飞船行为和本批验证见切片记录；下方按日期标记的旧版本段落只用于追溯。

2026-09-22 [静态背景候选](STATIC_CAVE_BACKGROUND_EXECUTION.md)升级为 **v9**／协议 **13**，文件进入独立 `v9` 目录。`world.terrain` 现在严格包含 10 个字段：`world_id`、`seed`、`materials`、`protection`、`soft_rock`、`shapes`、`rooms`、`deposits`、`expedition`、`background`。新增 `background` 为版本化参考封套的 Base64（固定旧地图可为 null，远征必需）；参考材料和坡形在首次生成后冻结，与当前已挖格子分别保存。封套包含版本、持久世界身份、布局种子、样式内容摘要、参考 SHA-256 和有界材料／坡形成对 RLE。校验失败拒绝整个恢复候选，不替换地图或对象。v8 及更旧文件不删除、不迁移、不重新生成伪初始源。下方字段表与旧版本说明为历史切片。

2026-09-19 地图遗漏修复将正式格式升级为 **v6**，目录使用 `v6` 子目录；协议同步为 10。Actor 保存 `explosive_charges`，矿床保存最终剩余量与枯竭阶段。`terrain.deposit.*` 是唯一新增的动态放置身份，且只允许对应 `mineral-deposit` 定义。本轮已删除钻机次数、钻进、输出缓冲和钻机对象字段，不接受旧格式迁移。

2026-09-17 [随机灰松谷](archive/RANDOM_PINEWATCH.md)将格式升级为 **v4**，文件位置使用 `v4` 子目录。`world.terrain` 为随机模板必需的对象，固定 Pinewatch 为 null；对象严格包含 `world_id`（32 位十六进制 GUID）、`seed`（1–80 字符）、`materials` 与 `protection`（各 61,440 字节的 Base64，材料 0–8、保护位 0/1）。记录最终格子，不按 seed 重新生成。严格校验材料、底部基岩、营地保护区域和随机布局是否匹配；实体与地图候选一起恢复，地图运行代次不沿用存档。原 v3 文件不迁移或删除。

2026-09-16，[主角与输入联合切片](archive/HERO_INPUT_EXECUTION.md)将正式格式升级为 v3：增加高度、纵向速度、平台支撑、下穿计时及道具状态。正式入口仍为 ObjectWorldSaveJson 与 GameSaveStore，状态仍来自所属 YYGC Behaviour。当前验收记录见联合执行文档；U5／U6 的 v2 计数按历史输入保留在[实施记录](archive/YYGC_UNIFIED_IMPLEMENTATION.md)。

当前只接受 v10；旧格式不读取或自动迁移，旧文件不自动修改／删除，显式选择旧格式返回“不支持的存档版本。”并保留当前世界。下方根字段以当前源码为准；按日期标记的 v1–v9 说明仅用于追溯。历史 v1 证据见[原存储记录](archive/evidence/world-save-2026-09-11.json)。

## 文件合同

无 BOM 的 UTF-8 JSON，上限 **4,000,000 字节**，解析深度 32。v10 根对象严格只有 8 个字段：

| 字段 | v10 合同 |
|---|---|
| format | dark-nights.world |
| format_version | 整数 10 |
| random_algorithm | SimulationRandom.Algorithm，当前为 godot-pcg32-clz-f32-v1 |
| rules_sha256 | 实际只读 GameCatalog 的规范化 SHA-256 |
| layout_sha256 | 实际场景导出 LevelLayout 的规范化 SHA-256 |
| identity_sha256 | 当前规则→DefinitionGuid、PlacementKey→规则关系的排序摘要 |
| equipment_sha256 | 当前手持装备配置的指纹 |
| world | 深度冻结的完整权威数据 |

world 包含 level_id、terrain、expedition、economy、wave、elapsed、speed、paused、next_entity_id、rng_seed、rng_state、actors、buildings、worksites、projectiles、stats，以及 mode、identities。`expedition.Ship` 保存 Id、Phase、PilotId、VelocityX/Y、DoorClock、DockX/Height；其他远征乘员、设备、结算与舱段数据同档。总实体上限 256，在飞箭矢上限 1024；数量、字段类型、有限数字、范围与实体关系由 SnapshotValidator 验证。64 位 RNG seed/state 使用有符号十进制字符串保存原位模式，不经过浮点转换。精确嵌套字段由 `ObjectWorldSaveJson`、`SnapshotDocumentJson`、`TerrainSaveJson` 和 `ExpeditionSaveJson` 的显式映射共同定义，不能用旧版样例代替验证。

每个 identity 严格包含 id、definition_guid、placement_key；实体与身份一一对应，GUID 必须匹配该实体 RuleKey。非空放置键必须来自当前场景，职业替换仅允许合法单位定义间沿用原放置身份。重复键、未知／缺失字段、重复 JSON 属性、尾随内容和不兼容摘要均拒绝。

actors 的运动与装备字段包括 height、vertical_speed、support_platform、ignored_platform、drop_remaining、manual_control、selected_item、selection_revision、jetpack_equipped、jetpack_fuel、explosive_charges；这不是当前 Actor 的完整字段清单。高度以原地面为零、向上为正；随机模板允许负高度至底部基岩顶面 -2416，支撑 0 表示权威地图支撑，-1 为空中；固定模板的正数表示场景平台 ID。支撑关系、范围、燃料和库存数量必须合法。

worksites 中的 `mineral-deposit` 保存 RoomKind、Y、Rarity、Capacity、Remaining 和 Stage。最终剩余量必须从该 YYGC 对象状态恢复，不能从初始 seed 或全局 Economy 推导。

文件不包含相机、选区、epoch、连接代次、FishNet 身份或房间共享策略；也不保存 ControllerSlot、ControllerGeneration、ControlLease、默认人物偏好、输入序号和按钮意图。恢复后的手动角色保留姿态、手动标记与装备；新 epoch 完整投影 Ready 后，服务端只接回这些已保存主角。连接仍记录的专属 ID 若指向不带手动标记的普通闲置村民，必须视为不可恢复并新建默认村民；旧连接所有权和输入不会恢复或重放。

## 内容摘要

SaveContentFingerprint 直接使用本次会话的 GameCatalog 与经过校验的 LevelLayout。规则覆盖配置名称、说明、资源、单位／建筑／工位参数、关卡 seed 和有序波次；布局覆盖边界、地面、出生点与按出生顺序排列的放置记录，排除本地 CameraX。

规则与布局使用二进制编码域 dark-nights.rules.v2／dark-nights.layout.v2，纳入 hero_control 和有序平台定义：小端整数、IEEE 754 float/double、UTF-8 字符串及显式集合长度。字典按 Ordinal Key 排序，布局和敌人序列保留顺序。这两个域标记描述摘要编码，不表示接受 v2 存档。

identity_sha256 对按 Ordinal 排序的 definitions／placements 对象做紧凑 JSON UTF-8 SHA-256，值来自实际加载定义和场景放置关系；动态 `terrain.deposit.*` 身份只允许对应 `mineral-deposit` 定义且全局唯一。`equipment_sha256` 校验手持装备配置。新增格式字段或修改规范编码须明确升级合同。摘要用于内容一致性，不是文件签名，也不能替代协议 14 的完整握手摘要。

## 保存与恢复顺序

GameSaveStore 注入专用目录与本局 ObjectWorldSaveJson，构造无磁盘写入。正式入口使用独立 v10 子目录，提供槽位 0–9，文件名为 slot-00.dnsave.json 至 slot-09.dnsave.json；客户端不能提交任意文件路径。

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
