# M5 世界表现与 Linear 验收

日期：2026-09-14。分支：`codex/yygc-unified-object-migration`。游戏源码：`a4a545017476cee87aa66b3ed2bb1674c26512cf`。本切片处理固定世界画面对照发现的圈线、浮字、图标和排序问题。用户明确要求 **Linear 色彩空间**；项目设置保持 Linear，不模拟 Godot 的 Gamma 乘色。YYGC `745f3d2`、FishNet `de19b5d` 均未修改。

本切片五个阶段已完成，完整 Editor／Play **155/155**、同一新 Mono **77/77** 通过。前台性能按用户选择继续暂缓；IL2CPP 尚未授权，双机器 LAN 缺第二台 Windows 主机，**M5 不签署全部完成**。受限清理按用户最新要求完成[清单交接](STAGE_CLEANUP_INVENTORY.md)，目录仍保留。

## 分阶段执行

| 阶段 | 输入与实际工作 | 验收和状态 |
|---|---|---|
| 1. 冻结与定位 | 读取 Godot 既有六张参考和 `ParityCaptureHost.cs`；在隔离 Unity Editor 中复现相同世界、镜头和表现时钟 | 首轮 `candidate-01` 保留；发现颜色处理、圈线、图标染色与残骸遮挡差异 |
| 2. Linear 与表现修正 | 校正进入 shader 的线性颜色；恢复圈线纵向压缩、点采样浮字、独立图标色和排序层 | 18 个 Prefab 的有限编辑、保存与重开；原 GUID、布局、数值和 551 项原素材保留 |
| 3. Editor 回归 | 复用既有隔离 checkout／Library；重新捕获六种局面，完整执行测试和架构守卫 | 最终六图与已复核候选逐文件相同；Editor／Play 155/155，架构 285 文件／12 自测通过；Mono 一次构建成功，退出 0，约 54.13 秒 |
| 4. 独立 Player | 同一 Mono 执行 Host＋来宾固定世界捕获、启动、会话和真实战斗晚加入 | 49＋6＋13＋9＝77/77 通过；七图已复核，五种活动世界的世界区域与最终 Editor 逐像素相同 |
| 5. 归档与空间 | 校验构建输入、产物哈希、引用和阶段清理 | 2,611 个输入和 408 个 Player 文件最终哈希一致；累计释放 67,000,747 字节。255 条清理盘点已归档，8 个审批拒绝目标未重试 |

## Linear 合同与色差范围

Unity 使用 `ProjectSettings.asset` 的 `m_ActiveColorSpace: 1`。实际 Editor 图形设备为 NVIDIA GeForce RTX 5060 Ti／Direct3D 12；冻结 Godot 参考为同型号 GPU／OpenGL Compatibility。当前证据不是不同物理 GPU 的兼容性验收。

离屏 GPU 探针确认：设置 `SpriteRenderer.color.r = 0.5` 后，shader 顶点色约为 `0.215686`；普通 Mesh 顶点色仍为 `0.5`。材质 Color 属性的 `0.4` 自动转换为约 `0.132868`，`Shader.SetGlobalColor` 的 `0.3` 则保持 `0.3`。因此静态网格、Sprite 与全局照明不能混用同一输入假设。

本次修改只进行正常的颜色输入解码：静态 Mesh 作者颜色经 `GammaToLinearSpace` 进入 shader，SpriteRenderer 继续使用引擎已转换的顶点色；全局环境光及灯色显式转为 Linear。纹理采样、光照和透明混合保持 Linear。世界 UGUI 在 CPU 中先合成线性照明，再编码为 UGUI API 接受的颜色值；普通菜单不参与世界光照。

原参考按 Gamma 空间乘色，因此天空、背景和透明混合的颜色不要求逐像素一致。用户已选择 Linear 后，没有执行纹理反向 Gamma 乘色方案，也没有修改项目颜色空间去追平旧图。`compare-world-parity.py` 的 RGB 差值只作定位资料，不能作为自动通过或失败门槛。

## 已修正的具体表现

| 问题 | 最终行为 | 主要落点 |
|---|---|---|
| 选择圈及命令圈上下边缘过厚 | 半径、线宽一起按原作的 0.3／0.35 纵向比例压缩；20 个弧采样点组成 19 段 | `UiShapeGraphic`、`NativeEffect`、Command Prefab／原生 Mesh |
| 世界指示在夜间过亮 | 生命条、选择等世界叠层采样同一线性环境与径向照明；菜单保持自身配色 | `CampOverlay`、`NativeEnvironment`、`CampLight` |
| 浮字模糊、图标误染色 | 世界浮字使用独立七像素字体与点采样；资源图标使用白色及透明度，不乘文字颜色 | Floating Prefab／FloatingFont、`NativeEffect` |
| 废墟遮住前方工位 | 原生 SortingGroup 显式绑定；建筑 0、工位 10、角色 100，残骸 -50 | 15 类对象 Prefab、`NativeVisual`、只读制作合同 |
| 固定截图无法在 Player 复跑 | 显式 `--dn-role` 驱动增加 `capture-sample`，采样暂停世界、镜头、时钟、选择和有界表现通知 | `SessionPresentationCapture`、`test-game-world-parity.ps1` |

命令圈运行 Mesh 由效果实例拥有，在销毁时释放；字体图集重建订阅在停用时移除。新字体不替换已校准的普通 UI 字体。三项新原生资产和一个新脚本的 `.meta` 均由 Unity 生成，已有 `.meta` 未重建。

资源编辑通过一次显式 `WorldPresentationCalibration.cs` 完成。它预检新增路径为空、Editor 非 Play、场景无未保存修改，然后编辑已有资源。普通导入／构建不会自动调用；重复执行会拒绝覆盖新输出。初始化脚本同步维护同一合同。

## 六种冻结局面

| 局面 | 分辨率 | 镜头 X／缩放 | 夜色／表现时刻 | 活动实体／瞬态效果 |
|---|---|---|---|---|
| menu | 1280×800 | 255／2.8 | 0.16／2 秒 | 0／0 |
| camp | 1280×800 | 255／2.8 | 0.16／2 秒 | 17／0 |
| night | 1280×800 | 730／2.8 | 1／2 秒 | 20／0，选择弓箭手 17 |
| remnants | 1280×800 | 730／2.8 | 1／2 秒 | 20／6，选择弓箭手 17 |
| zoom_out | 1280×800 | 550／1.8 | 0.16／2 秒 | 17／0 |
| wide | 1600×900 | 550／1.8 | 0.16／2 秒 | 17／0 |

有世界时模拟均暂停且 `Elapsed = 0`。三类敌人及六个效果对应冻结参考的显式采样局面，不改变正式出生波次。Unity 当前 v2 快照作为本批 Player 输入另存；没有重生成或修改 Godot 冻结参考。快照校验、角色装配和网络 Ready 继续经过生产路径。

Editor 最终输出为 `artifacts/m5-world/candidate-03`。`candidate-02` 的六图已逐张复核，最终 PNG 的 SHA-256 全部与它相同；`candidate-01` 的修复前图片保留。布局、脚底、残骸遮挡、选择圈、资源图标、字形采样和两种视口均已检查。系统字体的跨引擎栅格化和 Linear／Gamma 固有色差按上述边界记录，不声称整幅图像逐像素相等。

## 输入、构建与复跑

隔离源码仍为 `artifacts/yygc-unified/u6/source`，没有复制 Library。该目录在保留文件的前提下对齐源码提交；原有五个 embedded 包目录和派生 `packages-lock.json` 差异单独记录。2,611 个构建输入在 Editor 测试和 Mono 构建后均与本批输入哈希一致。

```powershell
$player = 'D:\Developer\MiniGames\Dark Nights\unity-projects\artifacts\m5-world\player-mono\DarkNights.exe'
./tools/test-game-world-parity.ps1 -PlayerPath $player -FixtureDirectory 'artifacts/m5-world/candidate-03'
./tools/test-game-startup.ps1 -Backend mono -PlayerPath $player
./tools/test-game-session.ps1 -Backend mono -Port 28431 -PlayerPath $player
./tools/test-game-battle.ps1 -Port 28432 -PlayerPath $player -BatchMode
```

所有本批运行均保留图形的后台模式或无图形启动检查，不显示游戏窗口，不计作前台性能验收。不同输入／后端的历史数字不合并到本批。`test-game-battle.ps1` 新增的可选 `-BatchMode` 已用于本次真实战斗晚加入检查；归档时仅统一文本换行，没有重新构建或运行测试。

## 最终验收证据

| 检查 | 实际结果 | 报告路径 |
|---|---:|---|
| 完整 Editor／Play | 155/155，52.1548 秒 | `artifacts/m5-world/editor-tests.xml` |
| 架构守卫 | 285 个手写文件、12 自测，0 错误 | `artifacts/m5-world/architecture.json` |
| Host＋来宾固定世界 | 49/49，七张图 | `artifacts/m5-world/player-parity-20260914-052236-126/result.json` |
| Mono 启动 | 6/6 | `artifacts/migration/run-20260914-052247-286-mono/result.json` |
| 独立双进程会话 | 13/13 | `artifacts/migration/session-20260914-052251-464/result.json` |
| 真实战斗晚加入 | 9/9 | `artifacts/migration/battle-20260914-052302-283/result.json` |

本次 Mono 的 408 个文件共 **198,999,138 字节**，测试前后及最终归档时一致；Entry DLL SHA-256 为 `3EDBC8AD0BCDD12ECF4848086D3B0326E50C2BB86B25614CE25F2D5C98C1552B`。完整[文件清单](evidence/m5-world-player-mono-files-2026-09-14.json)和[机器证据](evidence/m5-world-presentation-2026-09-14.json)已冻结，构建来源为 `a4a5450`，后续文档提交不改变这份产物来源。

camp、night、remnants、zoom_out、wide 的世界区域与最终 Editor 均无差异，night／remnants 整幅图像也相同。menu 的差异只位于 Continue 和存档／连接栏两个已知 UI 区域，区域外差异像素为 0；原因是 Player 已创建合成槽位并离开过房间，Editor 在加入前捕获。Godot 对照按本页 Linear 合同检查布局和表现，不要求 Gamma 参考颜色相同。15 类外观、32 段动画、583 个采样关键帧也已通过本批检查。

所有本批 Editor／Player 已退出。测试 Editor 仅保存了 Passed XML 和进程已退出事实，未取得进程退出码；不推断为退出 0。Mono 构建的退出 0 则有真实进程记录。临时颜色诊断曾出现并修正错误，正式 155／77 项通过不表示全部诊断日志从未报错。

## 空间与后续边界

本阶段通过 `dotnet clean` 清理 ArchitectureGuard、NetworkReviewProbe、LanSampleRules，共释放 **67,000,747 字节（约 63.9 MiB）**。当前 8 个已被自动审批拒绝的目标合计 **6,971,097,434 字节（约 6.49 GiB）**，没有重试或经父目录绕过；完整清单还覆盖旧 IL2CPP 调试副本、依赖生成器、缓存中的缓存、截图／报告的中间产物和系统共享缓存。详见[清理交接](STAGE_CLEANUP_INVENTORY.md)。

归档空间快照为 C 约 13.25 GiB、D 约 32.45 GiB；外部进程会使可用空间变化，不用磁盘差值计算本任务释放量。当前 Mono、必要报告／截图、人工资源、真实存档、冻结基线和带既有修改的依赖保留。

本机世界表现切片已完成，剩余验收为前台三夜／容量性能、单独获准后的 IL2CPP、第二台物理 Windows 主机的 LAN。跨物理 GPU 兼容性不在本次同型号 GPU 对照的证据范围内；没有据此扩张通过结论。
