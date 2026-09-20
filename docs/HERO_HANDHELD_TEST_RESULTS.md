# 主角手持装备测试执行结果

2026-09-20，测试输入 `9778a98613902d04dcf0ec595a4289ca57b8aa5d`，分支 `codex/hero-handheld-equipment`。执行依据为 [冒烟／回归计划](HERO_HANDHELD_TEST_PLAN.md)，逐项结果与未完成清单见 [本批证据](evidence/hero-handheld-tests-2026-09-20.json)。本批未修改游戏或测试代码，没有构建 Player。

## 本批结论

**验收未完成：自动化资源加载用例超时，按计划停止后续阶段。**

| 范围 | 实际结果 |
|---|---|
| AnyRules 锁定源文件校验 | 442/442 一致 |
| DarkNights.Tests EditMode 批次 | 选择 198 项；完成 78 项，其中 77 通过、1 超时失败；120 项没有最终结果 |
| HeroCombatTests／HeroBombTests | 新增 7/7 通过 |
| HeroControlTests／HeroRecoveryTests | 15/15 通过；主角相关合计 22/22 |
| 实际进入 Play 的生命周期用例 | 未执行到，不能以 EditMode 提交成功代替 Play 通过 |
| 素材编辑、Prefab 往返、真实输入及画面 | 本批未执行 |
| 新 Mono、双／四进程、弱网 | 自动化阶段未通过，未进入构建与联机阶段 |

主角通过项包括短按射击与重复序号拒绝、有限槽复用和过期回收、非法瞄准和越权拒绝、长短蓄力速度、取消／超时不投掷、黏附与引信存档恢复、切装／暂停取消，以及矿镐只播放动作、四玩家归属、租约、重连、燃料、冻结投影和恢复。具体断言范围以证据中的用例名称及现有测试源码为准；这些结果不能代替真实输入或独立进程联机。

## 阻断与诊断

`LocalCommandRingsTests.BurstsReuseNativeObjectsAndMeshesAndResetLifetime` 在 180.203 秒后失败，消息为 `Timeout value of 180000 ms was exceeded`。下一项 `NeverShownPoolReleasesEveryPrewarmedMesh` 同样停留在异步初始化阶段，取得运行器已完成结果后取消批次。该项取消没有通过或失败结论，计入 120 项没有最终结果的清单。

疑似原因是这两个旧用例未使用 `EditorAssetLoading`。该辅助类在其他真实对象夹具中关闭 EditMode 的 AssetDatabaseProvider 模拟加载延迟，避免后台 Editor 的 unscaledTime 停止导致等待不结束。本批尚未修改并复跑，因此这属于有源码依据的诊断，不能宣称已确认根因或正式运行时池存在缺陷。后续应先修复测试夹具并验证这两项，再补跑未完成批次，避免重复执行已通过的无关用例。

Console 中的 `BehaviourUpdateManager` 未创建错误来自 `FormalObjectContentTests` 的显式 `LogAssert.Expect`，对应测试通过，不另算失败。

另外，后续联机脚本 `tools/test-game-hero.ps1:150` 仍断言 `format_version -eq 3`，与当前 v5 合同不符。应在执行脚本前改为严格校验 v5，不能放宽或删去校验。本批尚未执行该脚本，不把静态发现计为联机失败。

## 环境、证据和收尾

- Unity 6000.4.9f1；协议 10、存档 v5；RTX 5060 Ti，驱动 32.0.16.1047。
- 使用已打开 Editor 的唯一 Pipeline 写入通道。最初 `all + async` 被工具拒绝且未启动测试，随后仅提交一次 EditMode 批次；没有重复运行正在执行的批次。
- 已完成结果在取消前从 Pipeline 运行器采集器中读取并落盘，完整结果写入提交的 JSON；没有将取消状态误算成完整成功。
- 原始执行、取消、测试清单和日志证据保留在 `artifacts/handheld/`；目录还含上轮编译证据，不混计为本轮编译或测试结果。
- 恢复测试前干净的 TerrainDebugBootstrap 场景。测试期间未修改游戏源码、正式资产、框架依赖或用户存档。
- 开始时 D 盘约 41.40 GiB 可用；结束余量见 JSON。无新增 Player、Library 副本或中间编译目录。保留本批诊断报告供后续复验，当前共享 Library／依赖缓存继续保留。

IL2CPP、双机器 LAN、前台性能与 M5 完成边界不变。
