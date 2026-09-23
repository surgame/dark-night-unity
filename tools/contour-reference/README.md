# H5 同源岩壁与背景验证工具

冻结输入为 `source-v16.1.html`（SHA-256 `0ba9423eaf1c4bc0acaf521d16f019d032ec6efd7b18038b18473c5d893c3de0`）。算法不依赖旧岩石 PNG 或旧 Unity 场景。原生 8px／格；新源不需 AI 生图；材质颜色、共边、透明轮廓由确定性代码计算。

## 夹具与纯计算

在仓库根运行 `dotnet run --project tools/TerrainRegression -c Release` 和 `dotnet run --project tools/ArchitectureGuard -c Release`。测试读取冻结字节，绝不调用 C# 烘焙器重写期望值。

- `export-golden.cjs`：真实 Chromium 运行冻结 HTML，导出原默认背景三层及 0/2/4px Alpha。
- `export-visual.cjs`：原默认 5px 岩块、宽波轮廓的 RGBA。
- `export-outline.cjs`：用户选择的岩块 4px、幅度 3px、波长 20px、阶梯 2px；六种独立轮廓及 HybridB 完整 RGBA。`profile-raw.bin` 是未扰动源，避免重复施加轮廓。

导出需要 Node、Playwright 与本机 Chrome；`NODE_PATH` 指向可用 Playwright 安装。三个脚本是显式更新源夹具用的入口，不是常规测试的一部分。RGBA 和占用文件使用原生 504×312、行向下坐标；H5 金样对齐算法，不代表 Unity 格形转换的布局逐像素相同。

## Unity 单通道

使用已有 Game Editor 和 `unity command --project-path Game run_script --file ../tools/contour-reference/<文件> --entry <类型>.Run`。编译完成后再调用；每个阶段完成后才运行下一阶段，不并发 Editor 写入。

- `SetupStrata` / `SetupContour` 只向空目录首建资产，禁止重跑覆盖人工资源。`RefineStrataSample`、`ApplyOutlineProfile`、`RefreshStrataPreview` 是本批拥有资产的显式修订入口，保留 GUID。
- `ContourEditorBatch` 提交 220 项 Editor 验证，等待 `artifacts/contour/editor-strata.json` 的 `status=completed`，不要在测试期间切场景或进入 Play。`BoundaryEditorBatch` 用于边界失败的定向重测。
- Play `Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` 或同目录 `RandomCave.unity` 后调用 `VerifyStrataPlay.Run`。地形资产仍在 `Res/Terrain/StrataCave/`。真实行走和爆破；比较静态背景计数与当前前景更新。输出近景、总览及爆破画面；停止 Play 后才能刷新派生 Editor 预览。
- `MeasureOutlineBake` 只测 Unity Mono CPU 单页生成，不证明前台性能达标。
- `ContourBuild` 单次构建 Mono r3，结果为 `build-result-r3.json`。构建返回或超时后先检查同一任务状态，不重复触发；此入口拒绝覆盖已有本批结果。

## 独立进程

`prepare-network-tests.py` 从远征原脚本派生当前协议／候选风格的验证，不修改原脚本。使用同一 `player-mono-r3` 运行 `test-network.ps1` 常规及 `-Weak`，再运行 `test-background-network.ps1` 两配置；各脚本有界等待并写独立目录。弱网为 RTT 200ms、loss 5%、jitter 25ms。

返船操作使用真实有限燃料喷气输入；早期 ground-only 跳跃脚本可能卡在未落地坡边，该失败保留在报告中；接近矿点时另校验实际 16px 手采范围，避免停止命令延迟影响测试。背景专项仅设置调试英雄位置和炸药装备，随后走正式输入、引信及权威地形修改；不直接清格代替爆破。

Unity `DarkNights.exe` 是通用启动壳，不能单独作为构建身份；交付证据同时保存 `DarkNights.*.dll`、数据文件和 StreamingAssets 的内容摘要。前台 GPU、IL2CPP、双机器、全路线与逐像素碰撞另列验收边界。
