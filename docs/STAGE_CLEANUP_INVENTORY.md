# 阶段产物清理清单

2026-09-15 追加：[本地指令圈切片](LOCAL_COMMAND_RINGS.md#产物与受限清理)的清理命令被自动审批以 `blocked by policy` 整批拒绝。新临时编译文件、原生调试副本及 ArchitectureGuard 中间产物合计 35,842,798 字节，约 34.18 MiB，释放量 0，未重试；路径与保留条件已逐项列账。下文仍为 2026-09-14 的历史盘点，两批体积不直接相加。

盘点时间：2026-09-14 05:52:18 +08:00。工作区：`D:\Developer\MiniGames\Dark Nights\unity-projects`。用户要求无权限清理的内容列账，并尽量覆盖中间过程产生的产物；本清单按此交接，不重复尝试已被拒绝的删除。

**本阶段实际释放 67,000,747 字节（63.897 MiB）；8 个已被自动审批拒绝的目标仍保留，共 6,971,097,434 字节（6.492 GiB）。** 此外还有需先归档、确认依赖或保留作验收的目录，不能把清单总量当成可以立即释放的空间。

完整盘点含 **255 条目录记录**，覆盖构建、导入、旧 Player、生成器、测试副本、依赖解包、报告及 7 个系统共享缓存位置。见[完整 CSV](evidence/stage-cleanup-inventory-2026-09-14.csv)和[机器记录](evidence/stage-cleanup-inventory-2026-09-14.json)。CSV 每项都有绝对路径、文件数、体积、父项和清理条件。`trackedFiles` 只表示当前游戏仓库跟踪数，不能用其为 0 推断嵌套依赖或研究源码可删除。

## 统计口径

- 体积按文件逻辑字节计，不是磁盘实际分配的簇数；未扣除硬链接、压缩或稀疏文件，不能保证清理后的磁盘释放量相同。
- **父目录包含子目录，表格各行不能直接相加。** 只有上述 8 个互不包含的受限根目录使用去重合计。旧 Player 的调试备份体积也已包含在 Player 中。
- 最终遍历 23,828 个目录，读取错误 0、跳过重解析点 0。首次普通路径读取遇到 15 个长路径错误；改用 Windows 长路径只读访问后补齐，初次诊断单独保留。
- 本次没有测试文件系统删除 ACL。下方“审批拒绝”来自已发生的自动审批结果；“共享用途未确认”“先归档”是保留条件，不表示已尝试删除或无操作系统权限。
- 本批 Editor 和 Player 均已退出。三个仍存的 dotnet 进程属于 Unity 运行时或 Rider，命令行未指向本工作区，未停止；后续清理仍须在执行时重新核对占用。

## 已实际清理

| 工具 | 清理前 | 清理后 | 实际释放 |
|---|---:|---:|---:|
| ArchitectureGuard | 33,138,125 B | 145,565 B | 32,992,560 B |
| NetworkReviewProbe | 33,959,343 B | 151,629 B | 33,807,714 B |
| LanSampleRules | 345,906 B | 145,433 B | 200,473 B |

均通过原生 `dotnet clean` 完成，没有新增构建。剩余主要是恢复／项目元数据，不把残留 obj 文件写成已删除。ArchitectureGuard 首次使用了不支持的参数而未执行，随后正规命令成功；释放量只计算实际成功前后差值，不累计历史批次重复数字。

证据：`artifacts/m5-world/architecture-cleanup.json`、`storage/extra-tools-cleanup.json`（后者相对同一批次目录）；逐项目退出码均为 0。空间快照见[世界表现证据](evidence/m5-world-presentation-2026-09-14.json)，磁盘波动含其他进程活动，不能代替逐目标释放量。

## 已被自动审批拒绝的 8 项

自动审批仅返回 `blocked by policy`，未提供更细理由。命令未执行；本次没有重试这些目标、其子产物，或通过删除父目录绕过。用户已要求以清单交接，因此不再重复询问清理权限。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `Game/Library/Bee/artifacts/WinPlayerBuildProgram` | 3.392 GiB | 1,974 | 审批拒绝 |
| `artifacts/migration/player-mono/DNights_BurstDebugInformation_DoNotShip` | 0.234 MiB | 1 | 审批拒绝 |
| `artifacts/yygc-unified/u6/source` | 3.063 GiB | 61,573 | 审批拒绝 |
| `artifacts/yygc-unified/u6/locked-archives` | 37.383 MiB | 3,060 | 审批拒绝 |
| `artifacts/yygc-unified/u6/player-mono-compressed/DNights_BurstDebugInformation_DoNotShip` | 0.234 MiB | 1 | 审批拒绝 |
| `artifacts/m5-ui/player-mono/DNights_BurstDebugInformation_DoNotShip` | 0.234 MiB | 1 | 审批拒绝 |
| `artifacts/m5-results/player-mono/DNights_BurstDebugInformation_DoNotShip` | 0.234 MiB | 1 | 审批拒绝 |
| `artifacts/m5-world/player-mono/DNights_BurstDebugInformation_DoNotShip` | 0.234 MiB | 1 | 审批拒绝 |

`Game/Library`、`artifacts/migration`、`artifacts/yygc-unified`、M5 各 Player 等父目录包含上述受限项，不能整目录清理。隔离源码已复用作本次构建；其内的 Library、嵌入依赖和临时结果也属于受限范围。旧的约 2.82 GiB 只描述当时 U6 source／archives，不代表当前八项总量。

## 当前验收必须保留

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `artifacts/m5-world/player-mono` | 189.780 MiB | 408 | 验收保留 |
| `artifacts/yygc-unified/u6/player-mono-compressed` | 189.784 MiB | 408 | 验收保留 |

当前世界表现 Player 的 408 个文件／198,999,138 字节和哈希已冻结，后续前台性能仍可使用它。协议 7 压缩版保留作性能对照。旧 UI／结果页 Player 内也有受限调试目录，整包继续保留。删除隔离源的限制尚未解除，因此没有声称已完成“删除源码后再次启动”的独立性复验。

同时保留 `Game/Assets`、`.meta`、Packages／ProjectSettings、人工 Prefab／场景／动画、551 项原素材、真实用户存档、冻结 Godot 参考、当前验收报告及必要截图。`Game/Library/YYGCDependencyBackups` 的 44,098 字节可能含依赖恢复材料，不能按 Library 缓存批量删除。Git 对象和 worktree 登记、编辑器用户设置也不列为自动清理对象。

## 工作区各类过程目录

下表为目录范围总量，**同时包含证据和需保留的内容**。其中 `art-staging`、`effect-staging` 是迁移中转；角色试验输出可能含唯一可编辑源文件；`command-generator-output`、协议探针、Workshop 副本和 MCP 日志要先确认已有可重建输入或归档。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `artifacts/art-staging` | 2.267 MiB | 553 | 先归档再清理 |
| `artifacts/aseprite-pixel-crew` | 1.174 MiB | 168 | 先归档再清理 |
| `artifacts/blender-pixel-crew` | 9.349 MiB | 3,188 | 先归档再清理 |
| `artifacts/c-refactor` | 2.021 MiB | 41 | 先归档再清理 |
| `artifacts/command-generator-output` | 0.000 MiB | 0 | 先归档再清理 |
| `artifacts/effect-staging` | 0.083 MiB | 1 | 先归档再清理 |
| `artifacts/godot-rng-probe` | 0.064 MiB | 3 | 先归档再清理 |
| `artifacts/lan-sample` | 1.888 GiB | 5,275 | 先归档再清理 |
| `artifacts/legacy-command-generator-output` | 0.000 MiB | 1 | 先归档再清理 |
| `artifacts/m5-results` | 206.895 MiB | 436 | 包含受限子项 |
| `artifacts/m5-ui` | 358.050 MiB | 544 | 包含受限子项 |
| `artifacts/m5-world` | 228.733 MiB | 559 | 包含受限子项 |
| `artifacts/migration` | 3.179 GiB | 4,203 | 包含受限子项 |
| `artifacts/scene-definitions` | 0.002 MiB | 3 | 先归档再清理 |
| `artifacts/ui-reference` | 0.309 MiB | 51 | 先归档再清理 |
| `artifacts/unity-mcp` | 7.956 MiB | 19 | 先归档再清理 |
| `artifacts/upm-network-probe` | 0.407 MiB | 23 | 先归档再清理 |
| `artifacts/workshop-change` | 0.610 MiB | 26 | 先归档再清理 |
| `artifacts/workshop-dependency-516f76c` | 19.525 MiB | 5,837 | 先归档再清理 |
| `artifacts/yygc-unified` | 3.855 GiB | 66,581 | 包含受限子项 |

临时运行目录中的 `.commands`、周期报告、代理丢包日志和合成 `saves` 可在冻结最终输入／结论后清理；真实存档不在此范围。失败样本、旧回归夹具和作比较用的报告不是可直接丢弃的“重复文件”。

## 旧 Player 与 IL2CPP 的下一层产物

以下均为**现存历史产物**的只读盘点，没有生成、覆盖或验证新 IL2CPP Player。旧包先保留构建来源、文件清单、报告及仍需回溯的符号；确认无后续用途后才清理。两个 `BackUpThisFolder_ButDontShipItWithYourGame` 目录是主要的历史 IL2CPP 调试／生成副本，共约 2.261 GiB，已包含在其各自的 Player 体积中。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `artifacts/lan-sample/player` | 186.343 MiB | 409 | 先归档再清理 |
| `artifacts/lan-sample/player-il2cpp` | 1.138 GiB | 722 | 先归档再清理 |
| `artifacts/lan-sample/player-il2cpp/LanCoop_BackUpThisFolder_ButDontShipItWithYourGame` | 1.032 GiB | 690 | 先归档再清理 |
| `artifacts/migration/player-il2cpp` | 1.347 GiB | 833 | 先归档再清理 |
| `artifacts/migration/player-il2cpp/DarkNights_BackUpThisFolder_ButDontShipItWithYourGame` | 1.229 GiB | 793 | 先归档再清理 |
| `artifacts/migration/player-mono-before-m5-20260912-041810` | 189.711 MiB | 412 | 先归档再清理 |
| `artifacts/yygc-unified/u6/player-mono-performance` | 189.780 MiB | 408 | 先归档再清理 |
| `artifacts/yygc-unified/u6/sample-mono-performance` | 185.926 MiB | 404 | 先归档再清理 |

## Unity 缓存与缓存内部产物

主工程导入缓存可重建，但重新导入会再次消耗空间和时间，后续还需编辑时应复用；主工程 Bee 的受限目标不得清理。隔离工程整棵 source 已受限，下方子项仅解释体积来源。`BuildCache`、Addressables 构建缓存、脚本程序集、ShaderCache、BurstCache、PackageCache 和 Bee 的原生编译缓存均纳入盘点。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `artifacts/yygc-unified/u6/source/Game/Library` | 2.801 GiB | 48,798 | 受限目录内 |
| `artifacts/yygc-unified/u6/source/Game/Library/Bee` | 517.105 MiB | 4,744 | 受限目录内 |
| `artifacts/yygc-unified/u6/source/Game/Library/BurstCache` | 258.249 MiB | 1,693 | 受限目录内 |
| `artifacts/yygc-unified/u6/source/Game/Library/PackageCache` | 1.630 GiB | 32,408 | 受限目录内 |
| `artifacts/yygc-unified/u6/source/Game/Packages` | 3.755 MiB | 676 | 受限目录内 |
| `Game/Build` | 106.258 MiB | 237 | 可重建，先查占用 |
| `Game/Library` | 6.253 GiB | 52,947 | 包含受限子项 |
| `Game/Library/APIUpdater` | 3.022 MiB | 12 | 可重新导入，先查占用 |
| `Game/Library/Artifacts` | 204.348 MiB | 5,132 | 可重新导入，先查占用 |
| `Game/Library/Bee` | 3.833 GiB | 7,214 | 包含受限子项 |
| `Game/Library/BuildCache` | 41.027 MiB | 203 | 可重新导入，先查占用 |
| `Game/Library/BuildInstructions` | 0.000 MiB | 0 | 可重新导入，先查占用 |
| `Game/Library/BuildPlayerData` | 3.850 MiB | 3 | 可重新导入，先查占用 |
| `Game/Library/BuildProfiles` | 0.003 MiB | 2 | 可重新导入，先查占用 |
| `Game/Library/BurstCache` | 306.138 MiB | 2,610 | 可重新导入，先查占用 |
| `Game/Library/com.unity.addressables` | 13.389 MiB | 38 | 可重新导入，先查占用 |
| `Game/Library/com.unity.ide.rider` | 0.001 MiB | 1 | 可重新导入，先查占用 |
| `Game/Library/InputSystem` | 0.014 MiB | 1 | 可重新导入，先查占用 |
| `Game/Library/PackageCache` | 1.634 GiB | 33,082 | 可重新导入，先查占用 |
| `Game/Library/PackageManager` | 0.238 MiB | 3 | 可重新导入，先查占用 |
| `Game/Library/Pipeline` | 0.096 MiB | 1 | 可重新导入，先查占用 |
| `Game/Library/PlayerDataCache` | 13.001 MiB | 17 | 可重新导入，先查占用 |
| `Game/Library/ScriptAssemblies` | 41.085 MiB | 272 | 可重新导入，先查占用 |
| `Game/Library/Search` | 30.340 MiB | 9 | 可重新导入，先查占用 |
| `Game/Library/ShaderCache` | 16.165 MiB | 4,299 | 可重新导入，先查占用 |
| `Game/Library/SplashScreenCache` | 2.680 MiB | 1 | 可重新导入，先查占用 |
| `Game/Library/StateCache` | 0.010 MiB | 11 | 可重新导入，先查占用 |
| `Game/Library/TempArtifacts` | 0.000 MiB | 0 | 可重新导入，先查占用 |
| `Game/Library/UCBPBlobStorage` | 0.000 MiB | 0 | 可重新导入，先查占用 |
| `Game/Library/UIElements` | 0.001 MiB | 1 | 可重新导入，先查占用 |
| `Game/Library/YYGCDependencyBackups` | 0.042 MiB | 2 | 源码／备份保留 |
| `Game/Logs` | 57.108 MiB | 42 | 可重建，先查占用 |
| `Game/obj` | 21.094 MiB | 183 | 可重建，先查占用 |
| `Game/Temp` | 0.000 MiB | 0 | 可重建，先查占用 |

## 工具生成器及依赖的再中间产物

当前 `.deps/YYGC-unified`、`.deps/YYGC` 和 `.deps/FishNet` 均有既有本地适配，逐项状态已放入机器记录；本阶段未新增框架修改。依赖内名为 bin 的目录也可能是随源码附带的工具，不能仅根据名字删除。`.deps/YYGC` 还保存工作树登记，不能整体移除来保留另一个关联 checkout。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `.deps/FishNet` | 9.743 MiB | 2,057 | 依赖需核对 |
| `.deps/YYGC` | 34.822 MiB | 6,284 | 依赖需核对 |
| `.deps/YYGC-unified` | 11.209 MiB | 1,337 | 依赖需核对 |
| `.deps/YYGC/.artifacts/vitalrouter-fix/VitalRouter-5c6cd79aad43bb3f128230e89aa282b59cde4a69/src/VitalRouter.MRuby/bin` | 0.000 MiB | 1 | 依赖工具需核对 |
| `.deps/YYGC/tools/NetworkValidation~/VitalRouterFix.Tests/bin` | 4.413 MiB | 90 | 依赖工具需核对 |
| `.deps/YYGC/tools/NetworkValidation~/VitalRouterFix.Tests/obj` | 0.177 MiB | 19 | 依赖工具需核对 |
| `.deps/YYGC/tools/NetworkValidation~/VitalRouterFix/bin` | 0.083 MiB | 3 | 依赖工具需核对 |
| `.deps/YYGC/tools/NetworkValidation~/VitalRouterFix/obj` | 0.103 MiB | 15 | 依赖工具需核对 |
| `tools/ArchitectureGuard/bin` | 0.000 MiB | 0 | 可重建，先查占用 |
| `tools/ArchitectureGuard/obj` | 0.139 MiB | 9 | 可重建，先查占用 |
| `tools/CoreBuild/bin` | 0.000 MiB | 0 | 可重建，先查占用 |
| `tools/CoreBuild/obj` | 0.006 MiB | 8 | 可重建，先查占用 |
| `tools/CoreRegression/bin` | 0.000 MiB | 0 | 可重建，先查占用 |
| `tools/CoreRegression/obj` | 0.142 MiB | 9 | 可重建，先查占用 |
| `tools/LanSampleRules/bin` | 0.000 MiB | 0 | 可重建，先查占用 |
| `tools/LanSampleRules/obj` | 0.139 MiB | 9 | 可重建，先查占用 |
| `tools/NetworkReviewProbe/bin` | 0.000 MiB | 0 | 可重建，先查占用 |
| `tools/NetworkReviewProbe/obj` | 0.145 MiB | 9 | 可重建，先查占用 |

## 截图、比对、报告生成自身的产物

保留最终参考对应、修复前失败例和必要复跑夹具后，可整理中间候选图、并排图、差分图、裁切图、重复文本摘要及大日志。最终证据只有哈希或路径并不等于已备份文件；清理前必须确保仍可复核所需的原始报告／图片。

| 示例 | 当前逻辑体积 | 条件 |
|---|---:|---|
| `artifacts/m5-world/editor-tests.log` | 14.932 MiB | 保留最终 XML、必要失败上下文和日志归档后再压缩或清理 |
| `artifacts/m5-world/editor.log` | 3.150 MiB | 包含定位过程和曾经的探针错误，先保存问题与解决记录 |
| `artifacts/m5-world/build-mono.log` | 0.823 MiB | 构建证据，最终归档前保留 |
| `artifacts/m5-world/storage/storage-directories.json` | 6.482 MiB | 完整遍历的中间索引；本清单及 255 条 JSON／CSV 冻结后可清理 |

`candidate-01/02/03`、`comparison-01/02/03`、`player-editor-comparison`、截图驱动的合成存档、`ImportWorldInputs.cs`／颜色探针、临时归档脚本和空间初扫诊断也在这一类。当前批次证据与对照仍留作交付复核；不为节省这部分小文件而损失唯一失败或视觉证据。

## 系统共享缓存：仅列账

以下目录可能被其他项目、Editor 或包恢复进程使用。当前没有逐项确认归属或占用，不停止无关进程，也不把它们的全部体积算作本任务可释放量。四个 Temp 路径中体积为 0 的目录不是清理成功记录。

| 路径 | 逻辑体积 | 文件数 | 处理条件 |
|---|---:|---:|---|
| `C:/Users/Jobscn/.nuget/packages` | 2.144 GiB | 16,976 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/NuGet` | 876.522 MiB | 1,963 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/Temp/MSBuildTemp` | 0.000 MiB | 0 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/Temp/NuGetScratch` | 0.000 MiB | 0 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/Temp/Unity` | 151.089 MiB | 6 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/Temp/UnityCBMCrashes` | 0.000 MiB | 0 | 共享用途未确认 |
| `C:/Users/Jobscn/AppData/Local/Unity/cache` | 234.881 MiB | 1,061 | 共享用途未确认 |

未扫描整个系统 Temp、下载目录或用户文档，也没有清理 Unity 安装、Rider、NuGet 工具链、许可证、密钥或用户维护的 `D:\Developer\YYGC`。编译过程中遗留的锁文件、崩溃转储、包下载解压副本和日志，可在确认来源后按上述条件处理；不能按扩展名在磁盘上全局删除。

## 后续每阶段执行方式

1. 开始前及编译／构建前核对 C、D 可用空间，使用当前锁定依赖与已导入缓存，避免复制 Library。
2. 阶段结束先保存结果、失败上下文和继续验收所需的同一 Player，再清理已确认无占用、可重建的中间产物。
3. 逐目标记录清理前后字节；将审批拒绝、共享归属未确认、含本地修改、唯一证据和后续验收保留分别列账。已拒绝操作不换工具或删父目录重试。
4. 保留当前源码、稳定依赖输入和必要证据；目录清单完成不等于所有目录都已删除。

只读复盘入口（输出位于忽略的 artifacts 内）：

```powershell
python tools/measure-stage-storage.py --output artifacts/storage-review
```

本清单已完成用户要求的受限清理交接；M5 尚余前台性能、IL2CPP 和双机器 LAN 验收，见[当前执行状态](M5_EXECUTION.md)。
