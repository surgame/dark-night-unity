# Dark Nights 操作与本机验收

正式入口为 `DarkNights.exe`，必须保留同目录的 Data、UnityPlayer 和 Addressables 内容。当前正式联机协议为 **14**，房间内各端使用同一构建；旧协议和内容摘要不匹配的连接会被拒绝。项目使用 Linear 色彩空间。当前飞船 Mono 的入口与验收范围见[可步入远征飞船](WALKABLE_EXPEDITION_SHIP.md)；下列旧构建记录只代表各自历史输入。

2026-09-16 默认村民生成修正后的输出目录为 `artifacts/hero-input/player-mono-generated-villager-r2`，执行范围、实际通过项与输入 Sample 见[联合执行文档](archive/HERO_INPUT_EXECUTION.md)。下列旧构建记录只代表历史输入。

2026-09-14 当时的 Mono 位于 `artifacts/m5-world/player-mono`，源码为 `a4a5450`，Entry DLL SHA-256 为 `3EDBC8AD0BCDD12ECF4848086D3B0326E50C2BB86B25614CE25F2D5C98C1552B`。完整目录 **408 个文件／198,999,138 字节**，最终哈希已核对。该批完整 Editor／Play 155/155、同产物 77/77 及六种固定局面／来宾画面复核通过，见[世界表现验收](archive/M5_WORLD_PRESENTATION.md)和[完整文件清单](archive/evidence/m5-world-player-mono-files-2026-09-14.json)。旧迁移、U6 性能、UI 和结果页 Player 按各自输入保留，不能混用通过项。

## 开始与合作

主菜单“新游戏”建立本机房间，单人使用相同 Host 权威路径。其他玩家填写房主的局域网 IPv4 地址并加入；产品端口固定为 UDP 27777。最多四人，默认共同控制同一营地。房主可切为 HostOnly，来宾仍能观察、选择和移动镜头，但不能修改营地。

默认进入主角模式。每名玩家首次完成完整投影 Ready 后，服务端会直接生成一名新的专属村民并接管，不会拿场景里已有的闲置村民；这次默认生成不扣招募资源或触发招募冷却。重复 Ready 不重复生成，断线会释放原村民，真正重连上线会再生成新人；HostOnly 恢复 SharedCamp 时接回原专属人物。当前产品 UI 不显示会遮挡画面的顶部状态／道具／改键工具栏，并隐藏营地建造、训练、招募和修缮入口；下列快捷键仍直接生效。

| 主角操作 | 行为 |
|---|---|
| A／D | 左右移动，镜头跟随 |
| 空格 | 从地面或平台起跳；W 预留向上交互 |
| S | 下穿当前站立的单向平台，不穿地面 |
| 1／2／3、滚轮 | 切换职业武器／工作工具／喷气背包 |
| 左键 | 使用当前道具；武器点击范围内敌人，工作工具点击附近工位并按住工作 |
| 选中喷气背包后左键 | 装备／卸下；装备后空中按住空格喷气，落地恢复燃料 |

当前提供三格基础道具栏，未实现拾取、掉落、制作或交易；没有新增跳跃素材，空中继续使用现有姿态。职业速度、原伤害与前摇不变，主角不会自动寻路到远处目标。

旧营地控制的后端、权限和测试仍保留，但不属于当前玩家入口。开发回归可在启动时显式传入 `--dn-camp-mode`，恢复顶部工具栏、改键按钮、左键选择、右键命令、建造和训练等旧操作；不得据此把旧入口写成当前可见产品功能。房主仍可用 P 暂停，并通过菜单控制倍速、提前入夜与存取档。

房主控制暂停、1×／2× 和提前入夜。暂停停止玩法时间，连接心跳、菜单和恢复继续工作；暂停时仍可提交合法订单及支付。打开菜单／帮助只锁住当前玩家的世界输入，不自动暂停整个房间。

## 保存与恢复

暂停菜单的槽位按钮在 0–9 间循环，保存、读取及 F5/F9 使用所选槽位；主菜单“继续”读取所选已有槽位。只允许房主操作文件，一次处理一个任务；失败提示保留当前世界。读取与重开保持当前房间控制策略，增加 epoch，各端等待新快照 Ready；服务端优先接回保存的手动主角，缺少可恢复对象时才生成新村民。

默认存储位于 Unity `Application.persistentDataPath/Saves`。现有宿主元数据为 `DefaultCompany/DNights`，Windows 对应 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/DNights/Saves`。验收脚本使用各自报告目录内的隔离槽位，不覆盖玩家文件。

当前文件保存在 Saves 下的 v3 子目录，只接受 v3 新档，拒绝 Godot 旧档和 Unity v1／v2，不自动导入或迁移，也不删除用户旧文件。空中位置、下穿计时、手动主角标记、道具和燃料会保存；玩家槽位、连接代次、租约和按钮输入不保存。恢复后由 Ready 流程把当前连接接到可恢复主角，不恢复旧连接所有权，也不拿普通闲置村民补位。新格式仍严格检查字段、实体关系和内容摘要。

来宾在同一进程、同一地址和端口重连，服务端为断线槽位保留 120 秒；凭据每次恢复后轮换，仅留在内存。关闭程序后重新加入使用新身份。房主退出会结束房间，当前版本没有房主迁移。连接失联检测为 15 秒，旧请求不会在新 epoch 自动重新结算。

## 开发者复跑

先用提交中的两个准备脚本核对 `.deps/YYGC-unified`／`.deps/FishNet` 的锁定输入和补丁，再按需要使用 Unity 6000.4.9f1 打开 Game；具体步骤见 [Quick start](QUICK_START.md)。不要运行任何 `Install Initial` 或初始化内容菜单；正式 Prefab 和场景已经存在。

1. 按本批改动运行纯计算／ArchitectureGuard／`DarkNights.Tests`，覆盖分工见[回归映射](archive/YYGC_UNIFIED_TEST_COVERAGE.md)。确有新输入需要构建时，在同一 Editor 串行生成一次 Mono，使用新的空输出目录；CLI 入口为 `DarkNights.Editor.GamePlayerBuild.MonoToEmptyDirectory`，参数与当前构建证据见[主角与输入联合执行](archive/HERO_INPUT_EXECUTION.md)。输入未变时复用已有产物。
2. 需要全矩阵且运行条件具备时，显式运行 `pwsh -NoProfile -File tools/test-game-delivery.ps1 -PlayerPath '<本批完整路径>/DarkNights.exe'`，复用指定产物完成串行检查。各脚本省略 `-PlayerPath` 时仍指向历史 `artifacts/migration/player-mono`，不能据此验证当前世界表现版本。入口不构建，不修改数值，不因等待重启正在运行的任务；失败即停，各阶段日志和报告保留在 artifacts/migration。
3. 修正后只重跑失败或受影响的单项脚本；需要继续后续阶段时使用 `-StartAt active-load` 等阶段名称，报告会标记本次并非完整矩阵。入口检查独立子进程退出码和每阶段前后 Entry DLL 哈希；跨次续跑仍需核对既有报告与产物身份，Entry 哈希不能替代完整内容归档。图形检查默认使用普通 Player；基础／弱网使用独立无图形进程。画面文件必须另行查看，截图尺寸检查不等于视觉通过。

`pwsh -NoProfile -File tools/verify-delivery-driver.ps1` 使用惰性文本夹具检查退出失败、续跑范围与 Entry DLL 变化检测；不启动 Unity 或 Player，也不计入玩法验收。

三夜性能同时保留通关时摘要和通关后 30 秒不变世界的内存采样。Unity Mono 的线程分配计数器经探针确认不受支持时显示 null；帧级 GC 记录仍有效。压力脚本的 256 实体／1024 箭矢是明确启用的合成投影，只证明完整传输和渲染边界，不冒充正常关卡的帧率。

前台三夜／容量性能已被用户明确暂缓，当前不启动可见游戏窗口，也不通过后台帧时签署前台性能。`test-game-campaign.ps1` 和 `test-game-pressure.ps1` 支持 `-ForegroundRole`；指定角色后要求至少 95% 帧区间具备操作系统前台证据，解除暂缓后才使用。不要为文档或归档修改重新运行整套 Player 验收。

用户目前没有第二台 Windows 电脑，因此双机器 LAN 待验收。本机四进程不能替代该项。IL2CPP 必须另获明确确认后构建；Mono 结果不能用于签署 IL2CPP。三项条件仍见 [M5 当前状态](archive/M5_EXECUTION.md)，M5 尚未全部完成。

每阶段编译前检查空间，结束后清理可重建中间产物；当前 Player、必要证据和原始资产保留。受限删除及其他候选按用户要求列于[清理清单](archive/STAGE_CLEANUP_INVENTORY.md)，不再次尝试已被拒绝的目录。
