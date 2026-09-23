# M5 原生 UI 校准

后续接续：[终局页面与重开修复](M5_RESULT_UI.md)已完成新源码 `af29950` 的 155 项 Editor／Play 与 40 项 Mono 检查，修复版 Player 位于 `artifacts/m5-results/player-mono`。本页以下保留字体／菜单校准批次自身的输入、产物和结果。

日期：2026-09-14。分支：`codex/yygc-unified-object-migration`。源码提交：`2fff198cb220d3be055a1389dbdceb49de3e1a01`。本切片已完成五页原生 UGUI 校准及相关 Editor／Mono 验收，保留原文、字号、原控件几何、Prefab GUID、生成绑定与原素材。YYGC 继续锁定 `745f3d2`，FishNet 锁定 `de19b5d`；本切片没有框架修改。**UI 切片通过不代表 M5 整体完成；前台性能按用户选择继续暂缓，部分产物清理受自动审批限制。**

[总重构计划](YYGC_UNIFIED_REFACTOR_PLAN.md)记录 U0–U6 的状态归属、框架升级和旧模型退出；本页记录后续 UI 切片。完整结果见[本批机器证据](evidence/m5-ui-calibration-2026-09-14.json)及[408 个 Player 文件与 SHA-256](evidence/m5-ui-player-mono-files-2026-09-14.json)。

## 实施与验收顺序

| 阶段 | 输入与实际变更 | 验收及状态 |
|---|---|---|
| 1. 冻结与定位 | 15 份 Godot UI／Theme 源哈希、两种分辨率的冻结布局、32 份既有 UI 文件；已有十张原 UI 参考图 | 来源哈希全部一致；确认缺失按钮文字状态、顶部字排、多行间距及新增控件遮挡 |
| 2. 代码与 Prefab | `NativePanelTheme` 显式绑定 Text，共用四种指针／禁用状态；重入清除按压。五页启用像素对齐，正式 YYGC 根 Canvas 同步设置；UIFont 使用已解析的 Microsoft YaHei 和 HintedSmooth | 编译和结构守卫通过；原主标题保持 Georgia Bold，未复制字体字节或添加文本渲染框架 |
| 3. 排版与重开 | 原 Label 恢复顶部对齐；四处原多行内容按实测基线间距 170／42／42／56 px 校准；恢复原主题五处文字阴影 | 两分辨率各 162 个原控件几何／文字／绑定通过；30 个按钮状态、禁用中断、CanvasGroup 与重入通过；UI 相关 22/22 通过 |
| 4. 菜单新增区域 | 主菜单槽位、加入、地址、状态归入底部独立面板；暂停页的槽位／权限按钮归入随主卡片定位的附属面板 | 1280×800、1600×900 的可见区域、控件间隔、原说明不被遮挡检查通过；真实鼠标 19/19，包含槽位、地址聚焦、权限切换一次、重开与退出解绑 |
| 5. Player 与归档 | 复用 U6 隔离 checkout 和导入缓存，串行构建一次 Mono；复用同一产物执行启动、会话和两分辨率画面检查 | 构建成功；启动 6/6、Host＋独立客户端 13/13、两分辨率捕获各 6/6，共 31 项；十张图已复核。408 个文件测试前后哈希一致；清理结果单独记录在下方 |

正式根 Canvas 由 `SessionUiController` 通过既有 YYGC UI 入口设置 `pixelPerfect`。仅修改 Prefab 的 authoring Canvas 不足以生效，因为 YYGC 装配时会移除它。按钮文字由 `NativePanelTheme` 的显式序列化引用驱动，未增加运行时查找 Text 的兜底。

## 验收输入、结果与产物

| 检查 | 实际结果 | 证据 |
|---|---|---|
| 首轮完整 Editor／Play | 154/155；唯一失败是新增的原多行基线测试 | `artifacts/m5-ui/editor-tests-initial.xml` |
| 受影响 Native 复验 | 22/22，覆盖原控件合同、新增区域、多行排版和按钮状态 | `artifacts/m5-ui/editor-ui-tests-final.json` |
| 按影响合并的 Editor／Play 结果 | 155 项通过：复用未受影响的 133 项，替换为最新 22 项结果；没有再运行一次完整 155 项 | `artifacts/m5-ui/editor-merged-results.json` |
| 真实鼠标 Play | 19/19；原场景外观退出后保留且无活动绑定，再开局恢复新世界 | `artifacts/m5-ui/runtime-final.json` |
| 架构守卫 | 284 个手写文件、12 个守卫自测、0 错误 | `artifacts/m5-ui/architecture-final.json` |
| 构建输入完整性 | 2,603 个受保护输入前后哈希一致；15 份 Godot UI／Theme 来源哈希一致 | `artifacts/m5-ui/build-source-integrity.json`、`source-integrity.json` |
| Mono 启动 | 6/6 | `artifacts/migration/run-20260914-013908-663-mono/result.json` |
| Mono 独立双进程 | 13/13，包含单次支付、来宾权限、HostOnly、运动和暂停收敛 | `artifacts/migration/session-20260914-014020-890/result.json` |
| 1280×800 捕获 | 6/6；昼／夜、暂停、帮助、主菜单五张图人工复核完成 | `artifacts/migration/visual-1280x800-20260914-014032-569/result.json` |
| 1600×900 捕获 | 6/6；同五种画面人工复核完成 | `artifacts/migration/visual-1600x900-20260914-014056-023/result.json` |

新 Player 位于 `artifacts/m5-ui/player-mono/DarkNights.exe`，共 **408 个文件、199,006,260 字节**，包含尚未获准清理的 245,875 字节 Burst 调试文本。归档文件清单按仓库约定使用 UTF-8／LF，SHA-256 为 `ACAB29E804DA1BBA709477CB3A1B7D7186184C1874B9DFA254EDC4A8844DEE24`；清单条目与原始记录相同，Player 文件测试前后逐项比较无增加、删除或改写。

| 产物 | SHA-256 |
|---|---|
| `DarkNights_Data/Managed/DarkNights.Entry.dll` | `2A66107EDB8E649EE084B671AA3CEABE0C796EFA8E136293421DDA635D0F8081` |
| `DarkNights_Data/Managed/DarkNights.View.dll` | `D4806FDE0D8B951F01216BF42F831D507082E3523BD0B18F2C7E214C230558FA` |
| `DarkNights_Data/StreamingAssets/aa/catalog.bin` | `4A9128B87DE2F7AABF2B5562F652542DFD1DE654B6435A99596B7C4B725FBC40` |
| `defaultlocalgroup_assets_all_a466d1cc065883734bac3cfd8160bbcd.bundle` | `B2D0D1EC710C7370E090550D601EEA949512DD842FAB62AC4155EDD1AC05B286` |

构建来源为 `artifacts/yygc-unified/u6/source`，已快进到本切片源码提交；复用原导入缓存，未复制 Library。来源仍有原有五个 embedded 包目录与派生 `packages-lock.json` 差异，已记录完整状态，不能称为无差异的干净 checkout。构建 provenance、日志与受保护输入列表在 `artifacts/m5-ui/build-provenance.json`、`build-mono.log`、`build-input.json`。

`tools/test-game-visual.ps1` 增加可选 `-BatchMode`，以 `-batchmode` 保留图形渲染并离屏捕获，不添加 `-nographics`。两次新截图均使用该模式，结果记录 `renderedBatchMode=true`。尺寸／异常检查与人工图像复核分别记录；Result 页保留两分辨率的 Editor 检查，本批 Player 的五种捕获状态不包含胜负结果页。

旧游戏 `4e3798f` 的 144 项 Editor／Play、350 项完整 Mono 矩阵和 240 秒容量 21 项仍是[协议 7 批次的证据](YYGC_UNIFIED_PERFORMANCE.md)。本切片没有改规则和网络，没有重跑整个 350 项矩阵；上述新 Mono 31 项是本次 UI 改动的相关验收。保留旧性能 Player 供历史数据对照，不把旧帧时作为新 UI 产物的前台结果。

## 真实边界与诊断记录

首轮完整 Editor／Play 为 154/155。新增行距测试使用了当前 Canvas 的缩放，导致 1:1 基线期望与缩放后的结果混比（170 px 被测成 64 px）；固定 `TextGenerationSettings.scaleFactor=1` 后复验。同时将中文 13 号字体的行距由 43 px 校准为参考的 42 px。随后 UI 相关 22/22 通过，其他已通过且输入未变的项目复用。合并结果按完整测试名核对，没有缺项或新增未对应的测试。

字体校准先比较临时克隆，确认半像素位置和未提示字形对小字清晰度的影响后才保存。离屏 Editor 预览使用 WorldSpace Canvas，像素吸附在临时克隆中模拟；它不能替代正式 ScreenSpace Canvas 的 Player 检查。跨引擎抗锯齿仍以近似视觉复核为准，不宣称像素完全一致。

一次临时预览尝试把 CanvasRenderer 返回的网格写回自身，触发 Unity 原生崩溃。该操作已移除，未写入正式 Prefab，之前已确认没有未保存场景；崩溃日志保存在 `artifacts/m5-ui/preview-mesh-crash.log`。后台恢复后的首次编译遇到探针中的 Color32 比较错误，修正后通过。鼠标探针另保留默认 640×480 的失败；固定 1280×800 后确认旧探针对单一实体父节点的计数假设过期，按统一对象的实体／预览／解绑合同更新。最终鼠标检查 19/19；临时 GameView 尺寸已移除并恢复原选择，后台 Editor 已正常退出。

Mono 构建日志保留早期许可证握手／access token 错误，之后自动恢复并完成构建，最终退出码为 0。实际新 Player 已完成上述 31 项检查；不把该构建日志描述为“零错误”，也未因此重复构建。

此前 Godot 参考截图进程虽以 Hidden 启动，但末尾日志没有确认全程隐藏；进程已退出，没有再次启动。这批参考图不作为前台验收。用户选择的“暂不显示，保留前台验收待办”持续有效。

## 后续边界

前台三夜／容量性能、完整同状态世界画面对照、另行确认后的 IL2CPP，以及第二台物理 Windows 主机的 LAN 验收分别保留；本切片不签署整个 M5 完成。字体抗锯齿仍是跨引擎近似，不以 UI 检查代替完整世界画面的同状态验收。

## 阶段空间收尾

构建前可用空间为 C 盘 14,335,315,968 字节、D 盘 34,294,796,288 字节。01:54（UTC+08:00）的收尾快照为 C 盘 **14,289,866,752 字节**、D 盘 **35,361,787,904 字节**；盘上其他进程也会改变余量，不能把可用空间差值当成本任务释放量。

| 范围 | 实际结果 |
|---|---|
| 本轮 ArchitectureGuard 中间产物 | `dotnet clean` 从 33,138,117 降至 145,565 字节，释放 **32,992,552 字节（约 31.5 MiB）**；见 `artifacts/m5-ui/tools-cleanup.json` |
| 本轮新 Player 的 `DNights_BurstDebugInformation_DoNotShip` | 已检查绝对路径、所有父目录、链接、文件清单及无活动 Editor／Player；自动审批在执行前以 `blocked by policy` 拒绝，245,875 字节保留。没有提供更具体原因，未重试；见 `debug-cleanup-preflight.json`、`debug-cleanup.json` |
| 此前被拒绝的 Bee 构建目录、旧 Player 调试目录、U6 source／locked-archives | 本轮未再次删除，也未改换方式清理其内容。U6 source 仅复用构建；原有约 2.82 GiB source／archives 的待办保持 |
| 当前与历史性能 Player、当前 Editor 导入缓存 | 按后续验收需要保留；没有复制 Library。截图、失败日志、输入／产物哈希作为证据保留 |

仅文档、机器证据及截图驱动的归档不触发额外编译／构建。上述清理限制解除前保留待办，不将阶段清理写成全部完成。
