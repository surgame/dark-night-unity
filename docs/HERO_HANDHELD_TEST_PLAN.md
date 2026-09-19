# 主角手持装备冒烟／回归执行文档

本次开发依用户要求只编译。**下列项目全部待执行，没有通过计数。** 对应[实现合同](HERO_HANDHELD_EQUIPMENT.md)、[编译证据](evidence/hero-handheld-2026-09-20.json)。执行时另建带日期及实际提交的结果文件，不能把本计划或旧版本 Player 结果当作证据。

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

## A. 自动化影响批次（全部待执行）

通过 Unity Test Runner 按类筛选，在一次批次内运行 `HeroCombatTests`、`HeroBombTests`、`HeroControlTests`、`HeroRecoveryTests`，以及现有存档、投影、地形和原生对象合同用例。编译通过不代表用例通过。

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
