# 氧气移除验证记录

2026-10-03。按用户恢复验证并允许转用 Local 的指示，在 `ref-20261003-remove-oxygen` 串行使用现有 Unity 6000.4.9f1 缓存完成后台验证。协议 **23**／存档 **v16**／AMP1 schema **2**。**验证已执行，整批未通过**：氧气专项、资源和常规双进程链路通过，弱网与两项旧远征路线失败，详见[失败分析](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)。YYGC未修改，未合并或推送。

## 结果

| 检查 | 结果 | 证据及边界 |
| --- | --- | --- |
| 独立 C# 编译 | 6/6 | Core、Runtime、View、Entry、Editor、Tests；之后 Local 正常导入、生成及 Mono 构建也通过 |
| 纯航程／冻结快照 | 1903/1903 | 不代替角色通行或联机 |
| Core 规则 | 1046/1048 | 两项软岩／保护格旧断言失败，基线[工具 Definition 说明](TOOL_DEFINITION_HARVESTING.md)已记录；未改变规则或冻结期望 |
| 受影响 Editor 用例 | 119/121 | r1与修正后r3按测试名取最新结果，不累加重复次数；失败为旧矿房行走和矿工交货用例 |
| 氧气专项 | 2/2 | 两名角色通过可信输入离船220单位，隔离敌人后推进18000 tick／五分钟；HP、位置、登船标记、租约保持。relay、协议22、v15拒绝且世界不改变 |
| 战斗死亡及结算 | 通过 | 更新后的死亡、失败结算回滚、复活后移动用例通过；保留战斗死亡返船语义 |
| 三设备及货物结算 | 通过 | CargoSettlesOnceAndUpgradeChangesSecondDeparture：ship＋storage＋turret＋lamp，三设备实际展开／撤收、保存恢复、原子结算及下一轮升级 |
| 当前主角采矿输入 | 通过 | MiningInputTests／MiningToolDefinitionTests与SessionRegression合批28/28；不能据此宣称旧矿工路线通过 |
| 原生资源探针 | 12/12 | 43个正式Definition，无氧气／空条目；Addressables无氧气，两套Prefab 11／16项命令，无中继、重复命令或MissingScript；保存重开和GUID保持 |
| Mono构建 | 普通、静音配置各一次成功 | 静音构建用于避免干扰用户，构建后AudioManager设置及字节恢复。两份产物分别保留，静音运行不计音频验收 |
| 正常独立Host＋Client | 130/130 | 同一静音Mono：权限、目的地竞争、航程、到达、下降／着陆／下船、晚加入／重连、暂停、epoch、v16写盘并重启恢复Orbit／Descent／Landed |
| 弱网独立Host＋Client | 失败 | 200 ms RTT＋5% loss＋25 ms jitter；一轮完成22项后到达Ready超时，另一轮初始Ready超时；只读AMP1诊断确认反复整图重同步 |

正常联机未覆盖不同目的地竞争（当前仅一颗可选星球）及ArrivalSync阶段网络挂起（阶段短于暂停钩子生效时间）。同目的地竞争通过不能替代前者。

## 验证中修正

- UnifiedSessionScope.Codec复用真实会话SaveCodec，避免测试独立构造器遗漏装备、矿床、投射物等指纹；生产存档严格校验保持，五项受影响Session回归恢复通过。
- 测试存档改在仓库artifacts/session-storage-tests生成，校验独占目录和链接后移至同盘artifacts/待清理，解决C→D的Directory.Move失败；不再删除测试存档。
- 原生资源探针按磁盘实际资产及OnlyExistingAssets检查退役资产，避免Unity最近删除GUID缓存误报。正式资源无额外变更。
- 新测试.meta由Unity正常生成。后台工具以隐藏父窗口与原有真实camera.Render推进Ready，不直接写Ready或权威状态。静音配置不写入最终工程设置。

首轮失败和修正前报告全部保留。Editor r1为101/109；r2尚未刷新新测试输入，不作为修正后的证据；刷新后r3为28/28。后台早期存在窗口嵌入、空mapEpoch、只渲染一个参与者等工具问题；修正后同一Player常规矩阵通过。最终弱网诊断隐藏窗口不变量通过，仍无法完成地图基线，不能将其归为同类工具失败。

## 已知失败与待验收

1. 弱网重同步循环及旧矿工路线未修复。报告给出代码链、数值及输入；不能宣称全部远征玩法或弱网通过。涉及YYGC的修改须先取得具体范围的用户同意。
2. 结算后ExpeditionPhase=4／Journey=Landed的再次完整航程入口问题仍未修复。本次通过的是货物升级后的旧无航程第二次depart，不等于第二次完整星球旅程。
3. 人工前台画面、HUD排版、音频、性能、四人本批矩阵、双机器尚未验收。此次不激活窗口、不操纵键鼠，隐藏截图只作诊断；Editor实际打开／重开不计前台视觉通过。
4. IL2CPP未构建／未验证，须另行明确授权；Mono不能替代。

## 工作区与证据

Local保持氧气候选分支。之前的ft-20261003-art-layer-cave-entrance已有HEAD d265421，另建保护分支chore-20261003-before-oxygen-local；26项未提交文件逐字节备份于artifacts/oxygen-removal-20261003/local-before，Git stash为e33cf30dc062b13caf877763b10b2f3488bda1d1。没有清空或删除旧任务文件。薄worktree源码与验证工具均已提交，未生成第二套Unity缓存。用户随后要求推送分支并归档移除该worktree；主项目已经在该分支。

机器摘要与原始报告路径／SHA-256见[证据JSON](evidence/oxygen-removal-20261003.json)。Local原始报告和两份Mono保留；薄worktree编译中间输出14,594,145字节已归档至artifacts/待清理/20261003-oxygen-validation。测试存档及本轮Python中间缓存的清单也保留在统一待清理目录。归档不计空间释放，未永久删除任何产物。

2026-10-03 worktree收尾：132项忽略文件共14,938,743字节已移至主项目并逐文件校验SHA-256。最终纯规则证据保存在artifacts/oxygen-removal-20261003/worktree-evidence/；原薄worktree的artifacts/待清理保存在主项目artifacts/待清理/20261003-oxygen-worktree-retirement/from-worktree/artifacts/待清理/。迁移清单为该主项目待清理目录的preservation.json和清单.md；原报告中的编译时绝对路径保留追溯含义，不要求已移除worktree继续存在。两份Player、失败日志、存档、旧任务备份及其他worktree均未清理。
