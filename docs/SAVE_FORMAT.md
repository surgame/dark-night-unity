# Unity 世界存档 v2

2026-09-13，正式入口使用 ObjectWorldSaveJson 与 GameSaveStore 保存统一 YYGC 世界。U5 已删除旧档入口，134 项 Editor／Play 回归通过，包含新格式 44 项与文件存储 20 项断言。U6 最终同一 Mono 产物已通过活跃恢复 14/14、四人恢复 24/24 与九组弱网各 24/24，见[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)。

本页替代原 v1 当前合同。Godot 旧档和 Unity v1 不再读取或自动迁移；旧文件不自动修改／删除，显式选择旧格式返回“不支持的存档版本。”并保留当前营地。历史 v1 证据见[原存储记录](evidence/world-save-2026-09-11.json)。

## 文件合同

无 BOM 的 UTF-8 JSON，上限 **4,000,000 字节**，解析深度 32。根对象严格只有 7 个字段：

| 字段 | v2 合同 |
|---|---|
| format | dark-nights.world |
| format_version | 整数 2 |
| random_algorithm | SimulationRandom.Algorithm，当前为 godot-pcg32-clz-f32-v1 |
| rules_sha256 | 实际只读 GameCatalog 的规范化 SHA-256 |
| layout_sha256 | 实际场景导出 LevelLayout 的规范化 SHA-256 |
| identity_sha256 | 当前规则→DefinitionGuid、PlacementKey→规则关系的排序摘要 |
| world | 深度冻结的完整权威数据 |

world 包含 level_id、economy、wave、elapsed、speed、paused、next_entity_id、rng_seed、rng_state、actors、buildings、worksites、projectiles、stats，以及 mode、identities。总实体上限 256，在飞箭矢上限 1024；数量、字段类型、有限数字、范围与实体关系由 SnapshotValidator 验证。64 位 RNG seed/state 使用有符号十进制字符串保存原位模式，不经过浮点转换。

每个 identity 严格包含 id、definition_guid、placement_key；实体与身份一一对应，GUID 必须匹配该实体 RuleKey。非空放置键必须来自当前场景，职业替换仅允许合法单位定义间沿用原放置身份。重复键、未知／缺失字段、重复 JSON 属性、尾随内容和不兼容摘要均拒绝。

文件不包含相机、选择、epoch、连接代次、FishNet 身份或房间共享策略。Core 的 SessionSnapshot 只承载 v2 冻结合同，不再补入旧显示字段；旧 GameSaveJson、LegacySnapshotJson、LegacyDisplayState 及旧世界映射器已退出。

## 内容摘要

SaveContentFingerprint 直接使用本次会话的 GameCatalog 与经过校验的 LevelLayout。规则覆盖配置名称、说明、资源、单位／建筑／工位参数、关卡 seed 和有序波次；布局覆盖边界、地面、出生点与按出生顺序排列的放置记录，排除本地 CameraX。

规则与布局继续使用已冻结的二进制编码域 dark-nights.rules.v1／dark-nights.layout.v1：小端整数、IEEE 754 float/double、UTF-8 字符串及显式集合长度。字典按 Ordinal Key 排序，布局和敌人序列保留顺序。这两个域标记描述摘要编码，并不表示存档仍支持 v1。

identity_sha256 对按 Ordinal 排序的 definitions／placements 对象做紧凑 JSON UTF-8 SHA-256，值来自实际加载定义和场景放置关系。新增格式字段或修改规范编码须明确升级合同。摘要用于内容一致性，不是文件签名，也不能替代协议 6 的完整握手摘要。

## 保存与恢复顺序

GameSaveStore 注入专用目录与本局 ObjectWorldSaveJson，构造无磁盘写入。正式入口使用独立 v2 子目录，提供槽位 0–9，文件名为 slot-00.dnsave.json 至 slot-09.dnsave.json；客户端不能提交任意文件路径。

1. 权威端在模拟边界通过 ObjectSnapshotMapper 捕获全部 Behaviour State 的冻结副本。
2. 后台 Save 校验并编码，写同目录唯一临时文件，Flush(true) 后关闭句柄。
3. 提交前复查取消；已有文件用 File.Replace，首次用同目录 File.Move。没有先删原文件的后备覆盖路径，失败保留原档。
4. 失败仅清理本次临时文件，清理错误不掩盖保存错误；成功提交后不再报告取消。其他孤立临时文件不自动成为存档。
5. Read 在分配缓冲区前检查长度，以严格 UTF-8 读取；它不构造世界。ObjectWorldSaveJson.Parse 完整验证后返回冻结 DTO。
6. 房主加载取得票据并停止模拟推进，ObjectWorldRestore 在未激活上下文准备真实 YYGC 对象；准备期间不改变原场景对象。
7. 同步提交完整状态与索引，精确重接可复用场景对象，退休旧对象；成功增加 epoch、清空旧 Ready／去重窗口并重新发完整投影。房间共享策略保持，失败保留当前世界和暂停／倍速。

后台任务不持有可写 State、ObjectInstance 或跨线程 SessionScope。同实例文件操作串行；加载票据、连接身份和取消在 SessionAuthority／SessionStorage 处理，文件层不自行决定联网权限。

## 验证

U5 的 GameSaveScenarios 检查 v2 活跃状态恢复、两局确定性继续模拟、坏字段／关系／身份／内容摘要，以及旧版本明确拒绝。GameSaveFileScenarios 覆盖首次保存、原子替换、真实文件锁冲突、取消、坏 UTF-8、超限、并发保存和孤立临时文件；均通过真实 YYGC 测试装配入口执行，见[覆盖迁移](YYGC_UNIFIED_TEST_COVERAGE.md)。

U6 的最终 Player 已复验施工、训练、在飞箭矢组合恢复及继续模拟，以及四人保存／加载、晚加入、凭据恢复和重开；同一产物下坏档与旧版本明确拒绝并保留世界。具体报告见 [U6 证据](evidence/yygc-unified-u6.json)。历史 v1／独立纯计算通过数不计作新版存档通过。

测试只写本任务产物目录，不访问玩家旧存档。正常 Windows 原子替换和锁冲突检查不等于实际断电、磁盘耗尽或跨操作系统持久性验证；IL2CPP 和双机器 LAN 的状态另列。
