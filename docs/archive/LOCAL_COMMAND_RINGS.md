# 右键指令圈本地化与复用

2026-09-15，修复联机时其他玩家也能看到右键指令圈的问题。指令圈现在由发起者的本地输入立即显示，使用八个预热实例循环复用；单位指令继续经过原有服务端验证与执行入口。

## 实现与边界

原先 `ObjectWorkOrders` 在服务端完成派工后发布 `VisualCue("command", ...)`，圆圈因此进入公共表现事件并广播。现在移除这个事件来源，网络投影校验也拒绝 `command` 效果；公共事件游标不再管理指令圈的寿命。

`CampInput.IssueOrders` 与实际右键共用输入互斥和显式选区参数。`SessionEffects` 订阅本地意图，在 Ready、有效选区、当前世界和已知控制权限允许时调用 `LocalCommandRings`。圆圈表示本地输入反馈，不能表示服务端已经接受命令，也不结算移动、支付或伤害。

- 池通过既有 YYGC `effect.command` 定义、PrefabRef 和 `effect` 绑定预热八个对象及其网格；每次点击复用槽位，超过容量时重启最早槽位。
- 保留原世界坐标、椭圆几何、颜色和 0.8 秒寿命。使用未缩放时间，暂停期间也正常消失；不改现有 Prefab、材质、场景、素材或 Linear 色彩空间。
- 断线、重连和新 epoch 清除活动圆圈，保留池供后续复用；宿主销毁时释放整个池。
- 池显式释放运行网格，覆盖 EditMode 没有触发销毁回调及预热后从未显示的情况。`NativeEffect.OnDestroy` 复用同一幂等清理入口。
- 固定画面验收中的指令圈也改为本地注入；来宾对照需要显式显示自己的圈，不能再依赖 Host 的投影。

本切片没有新增 YYGC、FishNet 或依赖修改，沿用 YYGC `745f3d2c844a66389e39bb84cd878d5d80f77962` 及既有宿主补丁。协议仍为 7，保存格式仍为 v2。

## 验证结果

| 检查 | 结果 |
|---|---|
| Unity `6000.4.9f1` 相关 Editor 回归 | **24/24**：本地池 2、效果资源 4、投影 wire 5、会话合同 13 |
| 架构守卫 | 294 个手写文件、12 个自测、0 错误 |
| 同一 Mono：启动 | 6/6 |
| 同一 Mono：带渲染的独立 Host＋客户端指令圈 | 22/22 |
| 同一 Mono：基础会话、共享支付与权限 | 13/13 |
| 同一 Mono：真实战斗及晚加入 | 9/9 |
| 同一 Mono：弱网下独立 Host＋客户端指令圈 | 22/22 |

Mono 合计 **72/72**。两个指令圈批次覆盖双方只显示自己的圈、普通业务请求不创建圆圈、暂停到期、密集点击容量、仅房主权限、重开 epoch、断线与重连。每个进程全程只创建八个原生 Command 对象。弱网使用真实 UDP 中继：名义 RTT 200 ms、每方向抖动 25 ms、5% 丢包；实际丢弃 36 个包，记录 217 次乱序。

四张双方点击前后的截图已查看，发起端地面出现原有椭圆，对端不显示该圆圈。截图位于 `artifacts/local-command-rings/session-20260915-054954-297`，分别为 `guest-own-click.png`、`host-after-guest-click.png`、`host-own-click.png`、`guest-after-host-click.png`。

上个任务留下的首次池测试在释放后仍保留运行网格，失败报告保存在 `artifacts/local-command-rings/test-local-pool.json`。修正资源释放后上述 24 项一次通过，没有修改或删除失败证据。

本批 Mono 只构建一次，复用主工程导入缓存，输出 `artifacts/local-command-rings/player-mono/DarkNights.exe`。2,629 个源码／资源／配置输入及 412 个产物文件在构建和验证后哈希一致；另外单独核对的 1,507 项受保护资源无差异。构建总量 199,189,636 字节，Entry SHA-256 为 `FFE4AA62CA8B131EE0A39F01DCBC1E24D5BFFE7DC505AE94E0D4306C3FCBAC88`。

本次是相关功能回归，没有重跑完整游戏矩阵或固定世界画面对照，也未进行新的前台性能、IL2CPP 或双机器 LAN 验收；M5 状态不变。原始报告和机器摘要见 [验证证据](evidence/local-command-rings-2026-09-15.json)。

## 复验入口

Editor 批次使用 `LocalCommandRingsTests`、`NativeEffectTests`、`ProjectionWireTests`、`SessionRegressionTests` 四个测试类。Mono 构建沿用 `GamePlayerBuild.MonoToEmptyDirectory`，必须传入空输出目录；本批具体参数保存在 `artifacts/local-command-rings/build-process.json`。

```powershell
& ./tools/test-game-command-rings.ps1 -PlayerPath ./artifacts/local-command-rings/player-mono/DarkNights.exe -Capture
```

弱网批次复用 `tools/lan-netem.py`，再为同一脚本传入 Host 的 `-Port` 和中继的 `-ClientPort`。本次顺序与参数保存在 `artifacts/local-command-rings/ValidateMono.ps1`；其余入口为 `test-game-startup.ps1`、`test-game-session.ps1`、`test-game-battle.ps1 -BatchMode`。指令圈脚本显式使用本批独立存档目录。

## 产物与受限清理

本轮清理前确认目标的绝对路径、父目录与子目录均无重解析链接，相关 Editor、Player 和编译进程已经退出。清理命令被自动审批以 `blocked by policy` 整批拒绝，未执行任何删除或 `dotnet clean`，释放量为 **0**；没有重试、更换方式或删除父目录。

| 保留目标，相对仓库根目录 | 字节 | 原因及保留条件 |
|---|---:|---|
| `artifacts/local-command-rings/build-temp` | 2,459,425 | 编译器影子副本和临时文件；审批拒绝，留待后续获准清理 |
| `artifacts/local-command-rings/player-mono/DNights_BurstDebugInformation_DoNotShip` | 245,248 | 本次原生调试副本；审批拒绝，留待后续获准清理 |
| `tools/ArchitectureGuard/bin` | 32,927,744 | 工具编译输出；整批命令拒绝，clean 未执行 |
| `tools/ArchitectureGuard/obj` | 210,381 | 工具中间输出和恢复元数据；整批命令拒绝，clean 未执行 |

四个目标互不包含，合计 **35,842,798 字节，约 34.18 MiB**，不与历史清单或 Player 总量相加。清理记录时 C／D 可用空间分别为 5,059,026,944／29,339,082,752 字节；盘符变化包含其他进程活动，不能当成本任务释放量。

本批完整 Player、截图、输入清单、成功／失败报告及复现脚本继续保留；日志、截图和报告按目录列在 `artifacts/local-command-rings/evidence-inventory.json`，独立测试存档目录及共享测试输出单独标明。既有带宿主补丁的依赖、共享导入缓存和历史受限目录未清理，继续遵循[原清理交接](STAGE_CLEANUP_INVENTORY.md)。
