# 航程独立进程验收驱动

`test_network.py` 使用正式 development Player 的命令文件入口和可信客户端命令链，不改运行状态，不写玩家原始存档。日志、报告、存档与截图默认放在独立 `artifacts/space-planet-flow/network-*` 目录，也可通过 `--output-root` 指定本轮证据目录。协议取实际 Player；2026-10-05 当前存档为 v19，`--save-version` 默认19，历史构建必须显式指定其版本，避免误用存档路径。跨进程一致性包含航程内容指纹、地图格子与背景哈希，真实进程重启另核验保存前的生成指纹、地图身份及种子。

```powershell
python tools/space-planet-flow/test_network.py --player <当前构建绝对路径> --backend mono --save-version 19 --clients 1 --driver client --phase-hook --background
python tools/space-planet-flow/test_network.py --player <同一构建绝对路径> --backend mono --save-version 19 --clients 3 --driver host --phase-hook --background --port 29270
python tools/space-planet-flow/test_network.py --player <同一构建绝对路径> --backend mono --save-version 19 --clients 1 --driver client --phase-hook --background --weak --port 29280
python tools/space-planet-flow/test_network.py --player <同一构建绝对路径> --backend mono --save-version 19 --clients 3 --driver host --phase-hook --background --weak --port 29290
```

四进程组还应交换 `--driver host/client` 补齐房主与来宾担任驾驶者。经单独授权并完成 IL2CPP development 构建后，使用相同脚本与 `--backend il2cpp`；标记不替代真实后端构建身份。每组串行启动，端口不重用正在运行的组。

启动屏障除地图 Ready 与可见页外，还等待当前 slot 的受控主角出现在冻结投影中；弱网下 Ready 和新主角投影可能相差一个发布帧。当前较快步速的定位通过可信输入、命令消费回执、服务端位置和自适应提前制动完成；最终本地与权威位置均须在目标 12 单位内且已经停止，不修改游戏步速或直接写角色状态。回执严格匹配请求序号；世界已切换时，只允许当前新 epoch 的 `EpochChanged` 拒绝跨代次匹配，不接受新 epoch 的 `Applied` 或其他请求回执。新增报告同时冻结驱动脚本 SHA-256。

多 Player 组与 Unity／IL2CPP 构建严格分时执行。本轮曾在并发 IL2CPP 编译期间遇到第四个 Player 的 D3D12 `0x8007000e` 内存分配失败；该组保留原始崩溃日志和独立环境中止说明，不计产品验收失败或通过，待构建结束使用同一产物重跑受影响组。

正常组预计约 6–12 分钟，弱网约 10–20 分钟，取决于导入后 Player 启动和实际地图可见页完成速度；这是运行前估计。驱动保留图形窗口，不能添加 `-batchmode` 或 `-nographics` 绕过实际相机 Ready。本轮 Mono r1 有界对照已确认 batchmode 可收到完整地图，但无法取得星空相机回执；相同产物的窗口回放模式正常 Ready，因此自动化也须使用窗口模式。`status.json` 是极小进度文件，`result.json` 为最终结果，`transitions.jsonl` 保存阶段变化。报表明确记录 `not_covered`：例如正式目录只有一个启用星球时不能宣称不同目的地竞争，自动暂停没赶上短暂阶段也不能算通过。

当前脚本遵循松手缓降和自动着陆：驾驶者松开垂直输入后，实际高度继续下降；离座、断线、权限撤销或存档恢复后没有驾驶者时，飞船保持原位。降落段通过 S 推力接近平台后停止输入，等待权威安全着陆、驾驶租约更新、开舱倒计时结束，再实际走下坡道检查地面支撑；不发送手动着陆来推进流程。旧 `land` 请求仅作为着陆后的拒绝回归。

## Ready 失败的有界启动诊断

`diagnose_startup.py` 可复用当前 development Player 已有的只读 AMP1 TCP 调试入口。它仅开一个 Host，默认总预算 25 秒，采集命令报告和地图副本摘要后正常退出；该诊断不计为独立联机验收。新 `startup-时间-模式` 目录记录二进制身份、各时点副本和进程退出原因。

```powershell
python tools/space-planet-flow/diagnose_startup.py --player <同一本轮构建绝对路径> --mode batch-replay
```

`window-replay` 仅去掉 batchmode，`window-input` 再去掉输入回放标记；按实际失败现象逐组串行隔离，不能在另一组 Player 或 Editor Play 未退出时启动。诊断端口默认 29360，游戏端口 29260。每组生成临时回环访问令牌，仅用于本次既有只读桥，不输出令牌。`readyObserved=false` 必须保留为真实结果，不能放宽正式 DataReady／可见完成门控。

## 独立 60 秒性能采样

待所有构建及其他 Editor Play 完全停止后，才运行 `capture_performance.py`，不把功能矩阵期间的争用环境作为性能证据：

```powershell
python tools/space-planet-flow/capture_performance.py --player <同一本轮构建绝对路径> --backend mono --phase orbit --workload walk --seconds 60
```

默认一个窗口 Host，`--clients 1/3` 可增加真实客户端。`--phase descent` 用正式导航到达后离座悬停，`--phase landed` 用正式驾驶输入和自动着陆准备环境；`--workload idle/walk` 明确区分静止和船内往返。所有实例启用 `--dn-metrics`，通过实际命令 `metrics-reset`、`metrics` 输出；记录器先排除两秒暖机，再采至少 60 秒。结果独立保存在 `artifacts/space-planet-flow/performance/network-*`，包含外部 Windows WorkingSet、原始 Player 指标、相位时间线及构建哈希。

原有记录器通过 `GetForegroundWindow/GetWindowThreadProcessId` 核验帧区间两端属于本进程，`foregroundFrameMilliseconds` 才是实际前台样本。脚本不强制 OS 焦点，不改此判据；前台数量为零或覆盖不足时只能记录未覆盖，不能拿 `Application.isFocused` 或全部后台帧替代。`performanceAcceptance=false` 表示此工具只采样，性能通过结论仍需依实际覆盖和指标评估。

## 前台实景操作

```powershell
python tools/space-planet-flow/test_network.py --player <当前构建绝对路径> --backend mono --clients 1 --keep-open orbit --interactive --port 29310
```

这一独立模式不执行完整自动化矩阵，也不禁用原生键盘输入。它打开真实可操作的 Player，完成开房和加入，然后打印 `manual-ready` 及本次目录。通过 Computer Use 在所需窗口执行原生 UI／键盘操作：

1. 两个窗口分别确认太空船舱、自由移动和关闭坡道。
2. 走到驾驶台，点击“选择目的地”；检查星球名称、说明、翻页、确认／关闭，以及打开选择页期间角色停止移动／使用装备。
3. 确认目的地，记录本地星点变化、等待提示、到达星球半空；观察另一位乘员可以继续走动。
4. 实际按 A/D、空格、S 驾驶，松开垂直输入观察缓降；离开驾驶位确认悬停，重新接管后在安全平台上方松手，等待自动着陆、自动释放驾驶席及坡道展开。直接走下坡道，再走回船内。
5. 分别在 1280×720、较小窗口及目标高 DPI 记录文字、按钮、等待／失败提示；同一镜头观察天空分界、船内比例与地下背景。

结束时在打印的本次目录创建 `stop.flag`，驱动保存最终冻结报告并正常退出所有本次 Player。默认最多保持一小时，可用 `--keep-seconds` 调整。真实画面及人工操作证据应另行保存和记账；本脚本的隐藏截图及状态报告不计作前台视觉通过。

`--keep-open descent` 会在到达后通过可信命令离座，保留无驾驶者的安全悬停画面；`--keep-open landed` 会实际驾驶降落并等待自动开舱。这两种方式仍使用输入回放模式，不支持原生键盘驾驶；需要真实操控时使用上面的 `orbit --interactive` 路径，由人工走完整流程。

## 与 117 项清单的对应边界

- 本脚本主要消费清单第 3、4、6、7、8、9、10 节中的具体权威状态、身份、命令、地图 Ready 和稳定状态恢复断言；每个结果只证明相应命名检查。
- 制作面板（第 1 节）、原生点击与可理解错误提示（第 4 节）、策略可见效果（第 6 节）、输入焦点互斥（第 9 节）及分辨率／前台七时点画面（第 11 节）需真实 Editor/UI/Computer Use 证据，不能从网络状态报告推断。
- 新船内走动／真实降落有命令链输入检查，但图像比例、动画、前台帧时、任意种子角色下洞路线、矿床开采后写盘及设备全链不因这些检查自动通过。
- 所有冻结旧证据、正式配置或构建仅作为输入身份。本驱动不修改旧证据，不以源码实现或旧测试数充当新批通过数。
