# Unity世界存档v27

本页维护当前文件字段、身份／关系、基础会话零值约束与原子保存恢复。当前候选入口仅接受 **v27**，正式目录 `Saves/v27`；v26及更旧Unity／Godot文件拒绝、不迁移、不覆盖、不自动删除。v27增加共享星图勘测位图与坐标航程合同，当前候选的真实写盘／恢复验收见[执行状态](DEVELOPMENT.md)。网络epoch与房间权限见[联机合同](MULTIPLAYER.md)。

## 文件合同

无BOM UTF-8 JSON，上限 **4,000,000字节**、深度32，根对象严格8个字段：

| 字段 | 合同 |
| --- | --- |
| format | `dark-nights.world` |
| format_version | 整数27 |
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

四格库存以canonical Definition GUID为持有依据，选中装备、动作、数量和配置指纹必须合法。喷气背包暂退，既有独立能力持有／装备／燃料字段继续按原关系校验、捕获与恢复；合法v25数据不清零或迁移，正式运行不据此启用喷气或恢复燃料。`light_definition`为规范GUID或空字符串，非空必须指向角色实际库存中的照明道具；没有合法手电不能light_enabled。移除同时清引用和开关，恢复不重新配发已移除手电。方向按角色朝向初始化，光场、调参、渲染缓存和后台任务不保存。

相机、选择、epoch、策略版本、连接代次、FishNet身份、ControllerSlot／Generation、ControlLease、默认人物偏好、按钮及输入序号不进入文件。恢复后连接只接回仍标记manual_control的合法主角；原专属ID指向普通闲置角色或无可恢复人物时新建默认主角，不恢复旧连接所有权或重放输入。

## 基础会话与两层地图

CL02人工一点引入v25：`terrain.geology_sites`冻结SiteId、Definition GUID／配方内容摘要、配置、允许改形／退避及可选关联；`terrain.geology_progress`逐点保存阶段、模拟时钟与退休原因，数量必须覆盖同一静态清单，最多一点。最终前景耐久／矿层与事件进度从同一模拟边界捕获；源岩脱离、瓦砾填格与阶段同事务安装。恢复校验定义、阶段与最终地图，不从初始布局补回源岩／瓦砾，已清理和已退休状态不重触发。地图格式2使用同一静态站点字段，运行进度必须为空；静态地图及原始背景不保存WorldId／epoch／EntityId，加载时才注入本局地图身份。旧v24文件原位保留，不自动迁移；真实写盘、加载和重启恢复仍待验。

当前v27保留CL-01／04首片引入的`terrain.nests`与`terrain.nest_progress`，地图对象严格17字段；前者冻结地图内编号、封壳位置及首片试参，后者逐巢保存阶段、时钟、位置、垂直速度与HP。最多32巢，最终保存覆盖同一静态编号集合，清理阶段与零HP一致，HP不得超过该巢初值。休眠、前兆、出虫及已清理状态均由TerrainGameplayBehaviour捕获；不得从静态巢位补回已清理幼虫。房间读取上限与作者合同统一为80，覆盖默认34洞室。旧v23文件仍留原目录，不迁移、不覆盖。

CL-06的`expedition.journey`保持严格12字段；v27的`Navigation`严格12字段：原`speed/descent_seconds/x/y/run_id/settled_run_id/last_income/nodes`加`survey_bits/coordinate_flight/target_x/target_y`。`survey_bits`为规范Base64，严格32768字节／43692字符，512×512位覆盖正负4096坐标、每格16单位；勘测半径28单位，首次登记首个正式星球及真实出生船位，此后仅按Host实际航迹累加，不截断旧探索。位图和船位同批保存，加载不重新播种；当前船位及初始星体必须已勘测。坐标航程只允许Transit，JourneyId非零N格式GUID、PlanetId／MapId／Seed为空；非坐标航程的目标坐标必须为零。仅Orbit／Landed允许写盘，坐标航行也须等到抵达待命或取消后再保存。

节点仍有1–32个的有界目录合同，但UI只列出位图已揭露节点，不显示实际总数；本轮不确定正式星球数量，也不新增正式星球。单节点严格含`id/name/x/y/danger/risk/preview_only`，坐标有限且绝对值不超过4096，危险等级1–3，预览节点不提供正式落地资格；真实节点必须对应已开放星球。配置与当前冻结目录／内容摘要匹配；RunId标识本次远征，SettledRunId与LastIncome记录最近已交接结果，余额、被扣货物及结算身份仍从同一事务捕获。坐标抵达不换图／结算，改道、重连与恢复不再次到账。个人相机、选中项、未确认坐标及窗口状态不入档；地形SourceFingerprint与审核地图格式2不变。

2026-10-28暂退飞船后，既有可空Ship／Journey字段承接严格的无船地面变体：Ship和Journey同时为null，Devices和船体Building为空，全部主角Boarded=false。含船变体仍要求唯一合法船体、对应设备及原阶段／乘员关系；不允许null船体搭配非空航程或乘船主角。JSON字段和编码形状未新增，格式仍为v27；缺失字段仍拒绝，显式null与缺字段区分。匹配原配置的已有v27含船档继续按原最终状态恢复，其他旧版本保留文件且仍不自动迁移。

允许当前主角姿态、四格库存／信用点、合法装备动作／燃料／炸药数量、手持投射物、个人铁／金货袋、可选飞船及最终两层地图。正式自动航程不占用PilotId；Ship的Id、Phase、VelocityX/Y、DoorClock、DockX／Height等关系按阶段验证。只允许Orbit和Landed稳定阶段保存；Preparing／Transit／ArrivalSync／Descent／Gathering／Ascent拒绝保存。船货不得超过既有ShipCapacity；再次落地赋予新RunId及地图身份，不恢复已撤离旧矿区。

当前actors只接受非敌方、manual_control为真的worker，旧目标／自动命中／强制攻击状态拒绝。NPC、工位、旧箭矢、自动任务、风险、探索计时、舱段、损失和旧返航结算遵循现有基础会话零值／空集合约束。设备在含船变体只有飞船，无船变体必须为空；乘员只有主角；保留的JSON字段必须满足这些约束。暂停范围见[地面玩法](GROUND_GAMEPLAY_BASELINE.md#暂停的玩法)。

正式world.mineral_deposits为空。`terrain.mineral_map`保存固定61440格最终矿种／耐久／储量及规则指纹的有界压缩数据，`terrain.deposits`为压缩初始静态元数据，不能据它补回已采空格。前景最终格／damage与矿层最终状态独立保存，两张候选和对象一起校验／准备后切换。

矿层解码核验格数、压缩／解压上限、尾随数据、矿种、耐久、初始容量和最终储量，不保存临时流代次。当前矿格业务及来源见[地形合同](TERRAIN_GENERATION.md)，旧按床实例方案只留历史。

## 内容摘要

地表起伏是制作输入，不用它重生成旧存档；文件继续保存最终地形及原始背景。逐图装饰另存在SchemaVersion＝1的View布局资产，通过Style引用及规范化原始背景摘要匹配，不向v27或ReviewedMineMap格式2追加字段。恢复后用原背景选择同一布局，再根据当前前景确认支撑；装饰隐藏／重新显示是只读表现，不新增采木、掉落或经济保存状态。相关实际恢复验收仍见DEVELOPMENT。

2026-10-27生成与身份收口不改世界文件格式，也不重绘存档。已有完整地图按原来源摘要及`dn-space-planet-v5`航程身份读取，旧字段只由`TerrainHistoricalIdentity`投影，不进入当前生成；原字段顺序及空分隔保持。新保存地图使用`terrain-source-v4:`及稳定的`dn-space-planet-v6`配置身份，来源只核对有效生成输入／所选泊位／地质点，完整会话身份仍包含实际玩法与航程配置。新旧保存结果均从最终状态恢复，不根据配方重生成；切换正式地图或实际内容配置后仍遵守严格身份匹配，不承诺跨配置接受旧存档。协议35／存档v27不变，真实写盘和恢复验收另记。

SaveContentFingerprint从本局GameCatalog和已校验LevelLayout捕获。规则覆盖配置、资源、单位／建筑／工位参数、seed及有序波次；布局覆盖边界、地面、出生点、有序放置及主角／平台配置，排除本地CameraX。保留的冻结规则参与内容一致性，不自动开放暂停调度。

规则／布局编码域为 `dark-nights.rules.v5`／`dark-nights.layout.v2`，小端整数、IEEE754浮点、UTF-8字符串及明确集合长度；字典Ordinal排序，有序布局／波次不重排。编码域与文件格式版本是不同概念。

identity摘要由按Ordinal排序definitions／placements的紧凑JSON UTF-8求SHA-256，装备摘要由实际冻结能力目录生成。新增字段、规范编码或关系语义须明确升级合同，不能仅调整文字掩盖输入变化。摘要校验内容，不是签名，也不代替完整联机握手。

## 保存与恢复顺序

GameSaveStore注入专用目录及ObjectWorldSaveJson，构造不写磁盘。正式路径来自Unity persistentDataPath下 `Saves/v27`，槽位0–9，文件 `slot-00.dnsave.json` 至 `slot-09.dnsave.json`；客户端不能提交任意路径。

主菜单“开始游戏”不展示存档选择。新局启动前只在当前版本目录选择第一个不存在的槽位，不删除／覆盖已有文件；十槽都占用时阻止新局并提示整理。继续选择当前目录最后修改的已有槽位，实际恢复仍走下述原子加载。选择不写盘、不承诺自动保存，暂停菜单仍可显式切槽；文件格式、旧版拒绝和保存权限不因界面调整改变。

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
