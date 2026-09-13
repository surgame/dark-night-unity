# M5 终局页面与重开修复

日期：2026-09-14。分支：`codex/yygc-unified-object-migration`。游戏源码提交：`af29950be41f13abe69c4c36a6b42650ffdd9cb9`。修复了胜负页面重开后仍覆盖新世界的问题；完整 Editor／Play **155/155**、新 Mono 的相关验收 **40/40** 通过。YYGC 继续锁定 `745f3d2`，FishNet 为 `de19b5d`，本切片没有框架或资源修改。**前台验收继续按用户选择暂缓，M5 整体尚未完成。**

本页接续 [UI 校准](M5_UI_CALIBRATION.md)与[总重构计划](YYGC_UNIFIED_REFACTOR_PLAN.md)。完整证据见[本批记录](evidence/m5-result-ui-2026-09-14.json)及[408 个 Player 文件与哈希](evidence/m5-result-ui-player-mono-files-2026-09-14.json)。

## 分阶段执行结果

| 阶段 | 实际工作 | 验收与状态 |
|---|---|---|
| 1. 复现 | 对上一批 Mono 执行真实 Host＋来宾测试，载入合成胜利状态后点击“再次守夜” | 红测确认世界已为 epoch 3／Playing，两端仍显示 Result；保留原失败报告 |
| 2. 修复 | `SessionUiController` 在连接代次或 epoch 改变时关闭旧 Result 并释放模态；重开回执不再提前关闭，等待新世界发布 | 源码已提交为 `af29950`；不改变规则、资产、权限或网络协议 |
| 3. Editor 与构建 | 复用 U6 隔离 checkout 和 Library，完整运行测试一次，再构建 Mono 一次 | 155/155 通过；Mono 构建退出 0，约 41.95 秒；没有复制 Library |
| 4. 独立 Player | 同一产物依次执行胜负页、重开、退出、启动及双进程会话；Host 1280×800、来宾 1600×900 | 终局 21/21、启动 6/6、会话 13/13，共 40/40；四张终局截图已复核 |
| 5. 归档与空间 | 核对输入和产物、关闭诊断 Editor、记录清理预检与结果 | 2,603 个构建输入、408 个 Player 文件哈希不变；本批 245,875 字节调试目录删除被自动审批拒绝，保留待办 |

## 缺陷与修复合同

终局页面属于打开它的世界。旧代码收到 Restart 回执后先关闭页面，但旧 Won／Lost 展示帧仍可能把它重新打开；收到新 Playing 世界时又没有关闭残留 Result。修复后以新 epoch 的展示帧作为关闭边界，Host 和来宾均走相同逻辑。连接代次改变也清理旧结果页。暂停、帮助等其他页面沿用既有行为。

`tools/test-game-result-ui.ps1` 在两个独立 Mono 进程中使用正式 UI 绑定、SessionClient 和服务器加载／重开入口。检查两次重开分别只增加一个 epoch、两端恢复 17 个初始实体并关闭结果页；来宾退出后房主仍在原结果页，房主与来宾各自退出后活动展示数归零。

## 本批输入与证据

| 检查 | 实际结果 | 证据路径 |
|---|---|---|
| 修复前红测 | 5/6；`host_won_restart_closes_result` 失败 | `artifacts/migration/result-ui-20260914-023018-527/result.json` |
| 完整 Editor／Play | 155/155，一次完整运行，非拼接统计 | `artifacts/m5-results/editor-tests.xml` |
| Mono 构建 | 一次调用、成功、退出 0 | `artifacts/m5-results/build-provenance.json` |
| 终局 UI／生命周期 | 21/21 | `artifacts/migration/result-ui-20260914-032139-601/result.json` |
| 启动 | 6/6 | `artifacts/migration/run-20260914-032154-744-mono/result.json` |
| Host＋独立来宾会话 | 13/13 | `artifacts/migration/session-20260914-032158-906/result.json` |
| 构建输入保护 | 2,603 个原始文件哈希全部一致 | `artifacts/m5-results/source-after-diagnostic.json` |
| Player 测试前后 | 408 个文件，无新增、丢失或改写 | `artifacts/m5-results/player-integrity.json` |

当前结果页修复 Player 为 `artifacts/m5-results/player-mono/DarkNights.exe`，**408 个文件、199,006,273 字节**。Entry DLL 的 SHA-256 为 `3042846E74463BD523517B1158B85EBBDC1F8DF7DC84B7A7DFFCEC1C4EBC0776`。此前 UI 校准批次的产物及数据继续保留为历史证据，不将其 31 项重复计入本批 40 项。

隔离源码位于 `artifacts/yygc-unified/u6/source`。原有五个 embedded 包目录与派生 `packages-lock.json` 差异未变，不能称为无差异 checkout。主工作区与隔离源码有 161 个文本文件仅 CRLF／LF 不同；跨目录的原始字节差异不是本次输入修改。2,603 项保护比较使用隔离源码自己的构建前哈希，主工作区 Game 的 Git 内容也未变化。

Mono 构建日志包含启动阶段许可证握手及 access token 错误，随后同一次调用恢复并完成构建。最终退出码、实际 Player 验收与输入哈希均已核实；不把日志描述为零错误，也未因此再次构建。

## 测试夹具与观察边界

测试先通过游戏保存自己的 v2 初始档，再向本批隔离槽位写入合成终局。Won 改变 `world.mode`；Lost 还移除酒馆及对应身份，以满足现有终局存档合同。初版 Lost 夹具只改状态，触发“营地必须拥有一座酒馆”的校验异常；完整诊断堆栈已保存于 `artifacts/m5-results/terminal-load-diagnostic.json`，确认失败发生在解析阶段，未进入对象恢复。修正夹具后复用同一 Player 通过，无需放宽游戏校验或修改 YYGC。

另一次退出检查读到了按钮执行当帧：主菜单已打开，展示缓存尚未进入下一次 Update。驱动改为有界等待菜单、断开与零活动展示同时成立；最终两端的退出报告均已保存。旧失败报告保留，不计为通过，也未为测试改变生产生命周期。

这些合成状态只验证结果页呈现、权限与重开／退出，**不证明完成三夜或胜负计算**。两进程使用 `-batchmode` 保留图形，没有 `-nographics`；不显示游戏窗口、不作为前台性能验收。实际复核四张截图确认：标题、说明、统计及按钮没有截断；Host 的“再次守夜”可用，来宾同按钮呈禁用态且“返回主界面”可用。后台世界画面与钟表没有冻结，不能据此签署同状态世界画面对照。

## 复跑与后续

```powershell
$player = 'D:\Developer\MiniGames\Dark Nights\unity-projects\artifacts\m5-results\player-mono\DarkNights.exe'
./tools/test-game-result-ui.ps1 -PlayerPath $player
./tools/test-game-startup.ps1 -Backend mono -PlayerPath $player
./tools/test-game-session.ps1 -Backend mono -Port 28412 -PlayerPath $player
```

后续按 [M5 当前接续点](M5_EXECUTION.md)继续完整同状态世界画面对照；前台三夜／容量性能、另行确认后的 IL2CPP、第二台物理 Windows 主机 LAN 分别保留待办。既有规则、网络完整矩阵与性能记录按其实际输入保留，不因本切片重复测试。

## 阶段空间收尾

本切片复用既有导入缓存，未运行新的 .NET 构建；前一 UI 切片 `dotnet clean` 释放的 32,992,552 字节不重复计入本批。本批新增 Mono 约 190 MiB，作为已验收可运行产物保留。诊断 Editor 已退出并恢复原场景，所有测试 Player 已退出。

已核对本批 `artifacts/m5-results/player-mono/DNights_BurstDebugInformation_DoNotShip` 的绝对路径、全部父目录、链接、文件哈希及无活动进程。自动审批在执行前以 `blocked by policy` 拒绝删除，未给出更具体原因；**未执行、未重试，245,875 字节保留，本批确认释放量为 0**。证据为 `debug-cleanup-preflight.json` 和 `debug-cleanup.json`。此前六个已被拒绝的清理目标也未重试，包括 U6 source／locked-archives；复用 source 不代表获得删除授权。

本批 03:35（UTC+08:00）的收尾空间快照为 C 盘 14,283,767,808 字节、D 盘 34,911,698,944 字节可用。其他进程会改变可用空间，不能把盘符余量变化当成本任务清理量。清理限制未解除前，本阶段的空间清理保留未完成状态。
