# 主角手持装备冒烟／回归执行文档

本页是本次装备的唯一测试入口；功能和素材合同见[功能说明](HERO_HANDHELD_EQUIPMENT.md)。

## 当前结果与阻断

2026-09-20，测试输入 `9778a98613902d04dcf0ec595a4289ca57b8aa5d`，Unity 6000.4.9f1、协议 10／存档 v5。依赖锁 442/442 一致。自动化选择 198 项，完成 78 项：**77 通过、1 超时失败、120 项没有最终结果**；其中主角四类测试 **22/22 通过**（新增手枪／炸药 7 项）。[逐项结果及未完成清单](evidence/hero-handheld-tests-2026-09-20.json)保留原始证据。

阻断为 `LocalCommandRingsTests.BurstsReuseNativeObjectsAndMeshesAndResetLifetime` 预热等待 180.203 秒超时；下一项 `NeverShownPoolReleasesEveryPrewarmedMesh` 仍在初始化时取消批次。疑似未使用既有 `EditorAssetLoading`，导致后台 EditMode 模拟加载延迟无法推进，尚未修复复验。先处理这两个用例，再补跑未完成项。`FormalObjectContentTests` 的 BehaviourUpdateManager 错误日志由 `LogAssert.Expect` 显式预期，不是额外失败。

实际进入 Play 的生命周期用例、素材／真实输入验收、Mono 及独立进程联机均未完成。后续联机脚本 `tools/test-game-hero.ps1:150` 仍硬编码存档 v3，运行前须按当前合同改为严格校验 v5。历史编译证据及本批原始日志保留，不混计通过状态；IL2CPP、双机器 LAN 和 M5 状态不变。

## 人工接入：先做本地体验检查

人工现在可以独立进行以下体验检查，记录实际结果；这不表示自动化阻断已解除，也不签署完整验收。

1. 使用 Unity **6000.4.9f1** 打开仓库下 `Game` 项目，等待编译结束。保存自己的场景编辑，然后打开 **`Assets/Scenes/Bootstrap.unity`**。当前 `TerrainDebugBootstrap` 是地形观察场景，不用于装备验收。
2. 点击 Play，在主菜单点击“选择地图：灰松谷 · 生成新地图”，等待生成完成，再点“开始守夜”。点击 Game 视图使其获得输入焦点；开局应出现专属角色和底部四槽栏。先检查 Console 是否有新异常。
3. 按下表完成约 10 分钟冒烟，在 1280×800 和 1600×900 两种 Game 视图分辨率观察一次。详细边界场景使用下方 B–E 编号记录。

| 操作 | 观察重点 |
|---|---|
| A/D 移动、空格跳跃，鼠标上下左右瞄准 | 道具贴手、朝向正确，没有双重职业武器 |
| 1 选手枪，左键点按／长按 | 单发与连射、枪焰、枪口位置、墙面阻挡；敌人命中后不重复扣血 |
| 2 选矿镐，左键点按／长按 | 完整挥舞与回收；不采矿、不派工、不伤害目标 |
| 3 选炸药，左键短按及蓄力后松开 | 蓄力距离增加后封顶、抛物线、墙地黏附、抛出约 3 秒后爆炸 |
| 蓄力中切道具、Esc 开菜单或 P 暂停 | 取消蓄力，不补扔；恢复后重新按下能正常使用 |
| 4 选背包，左键装备，空中按住跳跃 | 飞行和燃料正常；切道具不会误开火 |
| 点击四槽图标、滚轮切换 | 选择正确；点击 UI 不意外发射／投掷 |

本地体验阶段先不按 F5/F9、不覆盖已有存档。保存恢复测试安排到下面带独立存档目录的 Player 中。完成后退出 Play；出现异常时记录第一条 Console 错误和完整堆栈。

## 人工接入：自动化复验与联机

- **自动化复验**：打开 `Window > General > Test Runner`，在 EditMode 中定位 `DarkNights.Tests.LocalCommandRingsTests`，复现并处理两项初始化问题。主角四类用例在 EditMode 下运行；名称带 Play 的生命周期测试本身会进入 Play，不能把名字等同于 Test Runner 的 PlayMode 标签。修复后按 JSON 中未完成清单补跑，记录 XML 结果。
- **联机前置**：自动化和本地检查通过后，按下面命令构建本分支的新 Mono 到空目录。同一产物开两个独立进程，不能用历史随机地图／主角 Player 验证本次装备。
- **人工开房**：房主从正式菜单选图并“开始守夜”；另一进程输入 `127.0.0.1` 后点“加入房间”。等双方都有专属角色，再同时开枪／投弹，交替观察对方；之后覆盖暂停、晚加入、断线重连和保存加载。四人检查再增加两个相同版本进程。
- **隔离存档启动**：在仓库根目录运行 `& '<新构建绝对路径>/DarkNights.exe' --dn-save-dir "$PWD/artifacts/handheld/manual-saves"`，每个窗口都使用本次专用目录；首次使用该目录确认没有要保留的测试档。同机双进程不等于双机器 LAN 验收。
- **反馈格式**：`用例 ID｜提交/构建｜分辨率｜Host/Guest｜操作步骤｜预期｜实际｜通过/失败｜截图或日志路径`。报错附首条 Console 完整堆栈；动作／黏附问题优先录屏，并记录地图种子和位置。没有遇到对应敌人、墙面或状态的项记“未测”，不能凭没有报错记通过。

## 准备与顺序

1. 记录实际 commit、Unity 6000.4.9f1、Windows／GPU、分辨率、协议 10、存档 v5、装备配置哈希、YYGC `12b253c`／AnyRules `aa450a7`。执行 `tools/prepare-map-packages.ps1`，确认 442 文件哈希一致。只运行一个 Unity 写入通道；开始前记录磁盘余量。
2. 使用正式 Bootstrap → 随机灰松谷，另以原 Pinewatch 覆盖无地形后备碰撞。不要用独立 Terrain Debug Bootstrap 代替正式角色／联机。另设独立测试存档目录，保留现有用户存档。
3. 先一次运行影响范围内的 Editor／Play 测试批次，再做下方 Editor 操作／资产冒烟。任一编译、加载、绑定、协议或存档完整性错误先停止后续阶段。
4. Editor 通过后，Mono **只构建一次**到新空目录；Host＋客户端复用该产物，记录构建文件哈希。先双进程，再四进程与弱网，最后旧功能回归。失败只修复并重跑受影响阶段。不要自动构建 IL2CPP。
5. 每项记录输入、预期、实际、通过／失败／阻塞、日志／截图及复现信息。结束后列出本批可重建中间产物与保留理由，不删除用户档、当前 Player、共享缓存或人工资源。

Mono 构建示例（仅供后续执行；先关闭同项目 Editor，替换实际路径）：

```powershell
& 'D:/Program Files/Unity 6000.4.9f1/Editor/Unity.exe' `
  -batchmode -projectPath '<仓库>/Game' `
  -executeMethod DarkNights.Editor.GamePlayerBuild.MonoToEmptyDirectory `
  -darkNightsOutput '<新空目录>/DarkNights.exe' `
  -quit -logFile '<报告目录>/build-mono.log'
```

## A. 自动化影响批次（部分完成，见当前结果）

首次执行通过 Unity Test Runner 按类筛选运行 `HeroCombatTests`、`HeroBombTests`、`HeroControlTests`、`HeroRecoveryTests`，以及现有存档、投影、地形和原生对象合同用例。编译通过不代表用例通过。

| 用例／覆盖 | 核心断言 |
|---|---|
| HeroCombatTests.ShortClickHitsOnceAndDuplicateInputCannotFireAgain | 短按真实发射，命中一次；重发序号不补射 |
| HeroCombatTests.RepeatedFireReusesBoundedSlotsAndRetiresExpiredShots | 飞行槽长度恒定，反复发射复用，过期全部归还 |
| HeroCombatTests.NonfiniteAimAndOtherConnectionCannotFire | 非有限角度及非持有连接不能开火 |
| HeroBombTests.LongerChargeProducesHigherThrowSpeed | 点按与长按的权威发射速度不同 |
| HeroBombTests.TimeoutAndExplicitCancelNeverThrow | 超时和显式取消均不抛出 |
| HeroBombTests.GroundAdhesionAndFuseSurviveAtomicSaveRestore | 黏地状态／年龄恢复，余下引信后爆炸并退池 |
| HeroBombTests.SelectionAndPauseCancelCharge | 换装和暂停取消蓄力 |
| HeroControlTests.PickaxeOnlyAnimatesWithoutAssigningWorkOrProducingResources | 产生动作，不派工、不增资源 |
| 其他主角控制／恢复用例 | 四玩家归属、重连、租约、policy、喷气背包新槽 4、燃料及 epoch |
| 存档／投影／内容合同 | v5 严格校验、配置指纹、完整冻结副本、正式定义与绑定；若旧测试硬编码 v4／三槽，应按新合同修正，不能放宽校验 |

## B. 本地冒烟与美术（全部待执行）

| ID | 操作 | 预期／取证 |
|---|---|---|
| B01 | 打开四个 .aseprite，分别隐藏图层、选标签、修改副本并导出 | 材质层独立；三个装备 held／action／icon、爆炸四帧完整；与正式 PNG 对照 |
| B02 | 打开 Handheld、Ballistic、Worker、Spearman、Archer、Hero Prefab，保存并重开 | 所有引用保留，无 Missing；原有 GUID 不变；修改仅在副本或明确获准的资产上进行 |
| B03 | 1280×800、1600×900 正式开局，按 1–4、滚轮、点击图标 | 底部四槽可用；三图标清晰；鼠标在 UI 操作不意外射击／投掷 |
| B04 | 三职业分别接管，站立／走动／跳跃时左右及上下瞄准；再释放控制 | 手与道具贴合、层级正确；无旧职业武器重复；释放后职业动画恢复。录制近景 |
| B05 | 手枪短点、长按、贴敌、贴墙、朝斜上／斜下及地图边界射击 | 射速稳定，短按不丢；后坐与枪焰可见；命中一次，不穿墙，不伤友军；子弹出生点与枪口视觉吻合 |
| B06 | 同方向点按炸药、蓄 0.6 s、蓄 1.2 s、长按超过上限后松开 | 距离逐步增加后封顶；轨迹抛物线；按住期间不生成飞行物；从抛出才计 3 s 引信 |
| B07 | 向地面、竖墙、顶面、薄墙及角落抛掷，命中后移动人物 | 炸药停在首次碰撞面，不随人物移动、不穿透；黏附期间引信继续；爆炸四帧后退池 |
| B08 | 墙两侧布置敌军并在一侧爆炸，包含边缘范围和多敌 | 只伤范围内无遮挡敌军；每次爆炸单次伤害；无友伤、无地图破坏 |
| B09 | 蓄力中依次开菜单、点击 UI、切道具、失焦、暂停、死亡／释放控制 | 不补扔；充能提示复位；恢复并松开后再次按下可正常使用 |
| B10 | 朝空处、敌人、矿点、树、建筑挥镐，按住／连点／切换 | 有完整挥舞／回收动作和间隔；资源、目标 HP、地图格子、派工均不因挥镐变化 |
| B11 | 按 4 开关背包，移动／跳跃／燃料耗尽／恢复；切其他道具 | 原背包合同保持，四槽 UI 与键位一致，无误触发新武器 |

## C. 池化和生命周期（全部待执行）

| ID | 操作 | 预期／取证 |
|---|---|---|
| C01 | 连续射击 60 s，交替投弹；用 Profiler 检查创建与释放 | 每会话表现实例预热后恒为 128；射击不逐发创建 GameObject；记录实际 GC，不要求或声称全会话零分配 |
| C02 | 在隔离测试夹具中填满 128 槽，再发射；待过期重试 | 拒绝超限发射，不扩池、不抛异常；退池后可复用；不修改正式配置来强行超界 |
| C03 | 弹／炸药在空中及爆炸余帧时暂停、恢复、切换倍速 | 暂停时模拟时间不推进；倍速仅权威入口生效；恢复不重伤害 |
| C04 | 飞行中退出重进、断线、切换关卡／加载新 epoch | 旧表现归还，新会话可重新预热；退出释放 Addressable 实例，无跨世界残留 |
| C05 | 晚加入时含飞行弹、黏附炸药及爆炸余帧 | 展示当前状态，不从出生点重播、不重复伤害；极短寿命子弹可能在两次投影之间完成，重点检查伤害一致性及可接受观感 |

`SessionAutomation` 报告新增 `ballisticActive` 与 `ballisticPool`，结合 Hierarchy／Profiler 验证。世界快照中的投射物用于权威计数，不能只看客户端粒子数。

## D. 独立进程联机与异常输入（全部待执行）

先用同一新 Mono 运行现有 `tools/test-game-hero.ps1 -PlayerPath <exe> -Capture` 与 `tools/test-game-hero-network.ps1 -PlayerPath <exe>`，保留各自报告。现有脚本覆盖主角基础／网络回归，**不等于自动覆盖本表所有新装备场景**；以下通过真人操作或扩展显式回放执行。

| ID | 场景 | 必须检查 |
|---|---|---|
| D01 | 独立 Host＋Guest 同时射击／投弹，交换观察 | 双方持物、瞄准、飞行和黏附一致；伤害只有服务端写入；Host 每输入只执行一次 |
| D02 | 4 个独立进程同时使用不同道具 | 各自角色／选择／蓄力隔离；共享投射物容量有界；未持有者不能控制别人 |
| D03 | SharedCamp／HostOnly 切换及队列中的旧请求 | 旧 policy／lease／generation 拒绝；蓄力取消；恢复共享控制仍走原角色恢复合同 |
| D04 | 重发、乱序、旧 observedTick、非法角度、旧选择版本、伪造角色／连接 | 不能重复发射、借别人的蓄力或提交客户端伤害；非法输入不污染当前帧 |
| D05 | 实际 UDP 弱网：延迟、抖动、丢包、乱序，蓄力中断连重连 | 短按／释放正常交付时只执行一次；超时取消，不幽灵投掷；恢复后新租约生效 |
| D06 | 暂停期间晚加入／掉线重连；飞行中加载档案 | Ready 与地图门控保持；旧 epoch 包拒绝，旧飞行表现退池 |
| D07 | 两端装备配置不一致、协议 9 连协议 10 | 握手拒绝，给出可定位日志；不以某端配置继续模拟 |

回放沿用 `HeroInputPlayback` 的 `input-hold`／`input-stop`／`input-raw`，本批增加 `aimAngle`、`selectionRevision`、`usePressed`、`useReleased`、`cancelUse`。例：先选炸药并等待权威选择回执，发送 `input-hold`（`actor` 使用报告实际 ID，`useHeld:true,usePressed:true,aimAngle:0`），等目标模拟时间后停止 hold，再发送单次 `useReleased:true`。hold 只在首包发送按下边沿；`input-stop` **不补释放包**，专用于输入超时。非法输入用 `input-raw` 固定序号／epoch；不得直接改服务器状态假装完成联机验收。

## E. 保存恢复与旧功能（全部待执行）

| ID | 操作 | 预期 |
|---|---|---|
| E01 | 子弹飞行、炸药飞行、黏附、爆炸余帧、角色冷却中分别保存加载 | v5 完整恢复位置、速度、年龄、黏附、动作与冷却；引信不重置、不重复爆炸；身份随新 epoch 重建 |
| E02 | 蓄力过程中保存加载；修改装备配置后尝试加载旧配置档 | 加载清空临时按键／蓄力，不自动投弹；配置哈希不符严格拒绝且当前会话不被部分替换 |
| E03 | 截断／非法 NaN／越界速度／超过 128 新飞行物／非法 kind／旧 v4 | 严格校验与原子失败；用户已有 v4 目录仍保留，程序写入 v5 |
| E04 | AI 工人采集／派工、矛兵职业原攻击、弓箭／塔箭、敌军波次与胜负 | 主角新装备没有改变旧职业 AI、经济、箭矢时序和结算 |
| E05 | 正式随机地图生成、碰撞、保存、晚加入完整地图；原 Pinewatch | 新飞行物查询与原地图坐标一致，Ready 门控和布局不回退 |
| E06 | 菜单、帮助、改键、营地调试模式、暂停和重开 | 帮助显示 1–4 新键位；菜单不泄漏输入；原 camp mode 后端可回归 |

本阶段不签署 IL2CPP、双机器 LAN 或 M5 前台性能。结果表建议字段：`ID | 构建哈希 | 执行人/时间 | 实际结果 | 状态 | 日志/截图 | 缺陷`。所有失败需保留首次证据，不能重生成冻结玩法夹具掩盖差异。
