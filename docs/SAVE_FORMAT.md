# Unity 世界存档 v1

2026-09-11。M1 已提供 `GameSaveJson` 与 `GameSaveStore`：从冻结快照保存、按内容兼容性校验并恢复一个新世界。独立 .NET 回归与 Unity Editor 检查已通过，证据见[本批记录](evidence/world-save-2026-09-11.json)。正式 UI、房主权限、会话切换与加载 epoch 尚未接入，不能据此认定已能在游戏中存取档。

## 文件合同

文件为无 BOM 的 UTF-8 JSON，上限 **4,000,000 字节**、解析深度 32。根对象只允许以下字段，不自动猜测旧档格式：

| 字段 | v1 合同 |
|---|---|
| `format` | `dark-nights.world` |
| `format_version` | 整数 `1`，独立于旧 Godot 的 `schema_version` |
| `random_algorithm` | 引用 `SimulationRandom.Algorithm`，当前为 `godot-pcg32-clz-f32-v1` |
| `rules_sha256` | 实际只读 GameCatalog 的规范化摘要 |
| `layout_sha256` | 实际场景导出 LevelLayout 的规范化摘要 |
| `world` | 完整权威世界数据 |

`world` 包含 `level_id`、`economy`、`wave`、`elapsed`、`speed`、`paused`、`next_entity_id`、`rng_seed`、`rng_state`、`actors`、`buildings`、`worksites`、`projectiles`、`stats`。其字段类型、实体数量上限（总计 256）、箭矢上限（1024）和完整关系校验复用 Core 已验证的快照合同。64 位 RNG seed/state 仍使用有符号十进制字符串的位表示，不经过浮点转换。

新档不包含相机、选择、epoch、连接代次、网络对象身份或营地控制模式。适配 Core 的旧快照模型时只在内存补充默认显示字段，不向任何客户端恢复显示设置。`Restore` 先检查外层版本、随机算法与两个摘要，再解析并验证完整世界；失败抛出异常，不持有或修改调用方当前 GameSession。

旧 v1 必须显式调用 `GameSaveJson.ImportLegacy` 或 `LegacySnapshotJson`；导入成功后捕获冻结快照可另存为新格式。新旧入口不会相互猜测。旧相机／选择需要时仍由已有 `LegacyDisplayState` 适配，不写入新世界档。旧档没有内容摘要，保持既有严格规则和关系校验，不声称它具有新格式的内容版本保证。

## 内容摘要

`SaveContentFingerprint` 不读取资源文件，也不接受调用方手填摘要。它直接使用本次恢复的 GameCatalog 和 LevelLayout，布局先经过校验。规则覆盖全部配置字段（含内容名称、说明、资源、单位／建筑／工位参数、关卡 seed 和有序波次）；布局覆盖边界、地面、出生点与按实际出生顺序排列的摆放记录，排除本地 CameraX。

编码由实际源码中的显式字段顺序定义：域标记 `dark-nights.rules.v1` / `dark-nights.layout.v1`；BinaryWriter 小端整数、IEEE 754 float/double、UTF-8 字符串（7-bit 编码字节长度前缀）、int32 集合长度。字典按 Ordinal Key 排序；布局、伤害范围及敌人序列保留原顺序。SHA-256 使用小写十六进制。新增配置字段或更改编码时必须重新评估并升级存档格式，不能悄悄改变 v1 的含义。

摘要是兼容性检查，不是文件签名或防作弊机制；改图或资产包身份不在这两个摘要中，不能将其替代联机握手的完整内容／类型注册摘要。

## 原子存储与调用顺序

`GameSaveStore` 由装配层注入专用绝对目录、实际目录和布局；构造无磁盘副作用。当前提供槽位 `0–9`，文件名固定为 `slot-00.dnsave.json` 至 `slot-09.dnsave.json`，操作不接收任意客户端路径。产品存档目录与 UI 入口留给正式会话装配。

1. 权威端在模拟边界同步 `SnapshotMapper.Capture`，得到深度冻结数据；后台操作不能持有可写 GameSession 或跨线程 SessionScope。
2. `Save` 校验快照并编码，写同目录唯一 `.tmp` 文件，`Flush(true)` 后关闭句柄。
3. 在提交前检查取消；已有档使用 `File.Replace`，首次使用同目录 `File.Move`。没有先删原档或非原子覆盖的后备路径，文件系统不支持替换时直接失败。
4. 失败只尝试清理本次临时文件；清理本身失败可留下孤立文件，不掩盖保存错误。提交成功后不再报告取消。
5. `Load` 在分配缓冲区前检查文件大小，以严格 UTF-8 解码；经过全部格式和关系校验后返回新的 GameSession。

同实例的存取操作通过锁串行，方法为同步 API，可在冻结数据就绪后由会话层选择后台执行。异常和取消直接交给调用方，不静默返回成功。`Load` 不扫描或回收其他操作的 `.tmp`，不自动从中恢复；未完成的临时文件不会替代成功存档。

实际会话协调器仍须实现：房主授权、Loading 状态、停止推进、失败保留暂停／倍速、成功切换世界并增加 epoch、保持房间控制策略、重新 Ready。存储层只返回临时世界，不承担这些联网语义。默认 JSON／文件路线遵循移植方案；YYArchive 如需接入，应将整个营地封装为一个模块，当前未建立另一套模块恢复流程。

## 验证与边界

独立回归 **1164/1164**，其中新增存档检查 **60 项**；Unity Editor 正式程序集 **34/34**。新增检查包含新旧格式隔离、冻结旧档经新格式恢复后继续 20 秒、暂停／倍速、非法字段／关系、内容变更、文化与字典顺序、首次／覆盖保存、真实文件锁导致提交失败、取消、损坏／超大文件、并发保存和孤立临时文件。

.NET 与 Unity Editor 生成相同的规则／布局摘要；.NET 保存的实际文件也已在 Unity Editor 中加载，恢复后全部 JSON 字段值精确一致。两个运行时输出的浮点数字文本可能不同，不要求文件字节一致；内容摘要使用独立二进制规范化编码。文件测试只写 `artifacts/migration/save-store/<run-id>`，保留诊断输入，不访问玩家目录。

```powershell
dotnet run --project tools/CoreRegression -- .
dotnet run --project tools/ArchitectureGuard -- .
unity command run_tests --mode editor --filter DarkNights.Tests --filter_type assembly --project-path Game --detach
```

本批未构建或运行 Mono／IL2CPP Player，未验证联机、UI、实际断电／进程强杀、磁盘耗尽或其他操作系统。Windows 上的正常原子替换与提交锁失败已经实测；这不等于设备断电情况下的持久性保证。
