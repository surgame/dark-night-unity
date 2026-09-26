# Cave Wall Tuner 延迟：独立修复与验证

日期：2026-09-25。状态：隔离候选，尚未移入 Local Unity 或合并主链路。

## 冻结基线

游戏：6964f57106d2fcfd58bcdf81fdfcd2dbba48d8bb；YYGC：5ad6f0c63185e09442bf21b2e165e9c5a48b1a61。两者原分支均为 fix-20260925-terrain-refresh-review。
当前 manifest 的四个地图包直接引用 D:/Developer/YYGC，不是来源锁文件记录的 d1a6c14。来源锁与直接引用的可重现性问题另列，不在本次隔离修复中修改包配置。

## 原因

1. AnyRuleD 的 GridPreviewSession.Tick 每次连续推进最多八轮 Map.Tick，每轮声明 50 ms 的处理预算。默认工作台地图较小，且不包含游戏程序化岩壁、光照和冻结背景。这不是游戏实际运行时的帧预算，也不是实际耗时 400 ms。
2. Cave Wall Tuner 的填拆只提交稀疏输入，随后等待 Editor update 驱动共用 TerrainPreview；0.4 秒防抖只用于样式、配置重建，不用于普通填拆。30 Hz 的主动 Repaint 也不是 stage.Tick 的固定执行频率。
3. 同一 TerrainPreview / CaveLocalRockSurface / CaveLocalRockGeometry 用于正式运行时。因此岩壁计算延迟可以影响运行时，不能凭 AnyRuleD 工作台即时刷新就认定游戏无影响。
4. 当前默认样式的单格失效像素半径为 105；输出区域在边界裁剪前达 218×218 像素，并需冻结更大 Halo。4 个 Dual Grid 规则输出不等于只计算 4 个岩壁像素。
5. 离屏 Stage 只在 VisualRevision 变化时绘制；作业清理可以使稳定条件变化而不产生新画面版本，存在遗漏最终确认绘制的风险。此项是源码可达性分析，并非声称已经实跑复现用户现场。
6. 左下角原来的等待提示合并了装载、输入、规则、岩壁/背景、最终相机回执；原“相机耗时”实际包括整个 Stage.Tick，不是纯 Camera.Render 时间。

## 可分别集成的两个提交

A：fd100f438b1a5394cf7ea18cc350f08523d6fb2b。
只修改 Editor 的即时输入泵送、最终相机确认、等待原因提示和相机单独计时。TerrainPreview 仅增加两个只读诊断属性；不修改 Ready 判定、权威、网络或运行时预算。不复制工作台八轮大预算刷新，不直接把 Presented 标成成功。

B：6f3fa2c7115d23d4364b24ec688274e5670c62a0。
纯计算优化：远离轮廓的像素跳过不可能改变符号的噪声计算；每像素九邻域复用相同的整数坐标；圆簇只遍历可能覆盖当前行的簇，并保持原融合次序。不改变随机数、Halo、像素合同、Modifier 版本、业务格或碰撞。

工作分支：fix-20260925-cave-tuner-isolated。工作目录为 ../cave-tuner-isolated。原 Local 工作树的注册表/生成代码修改保持不动；YYGC、Packages、ProjectSettings、素材、GUID、网络及业务命令均不修改。未启动第二个 Unity，未复制/删除 Library。

## 本轮实际验证

- 56 组旧版与候选像素摘要完全相同：36 组外轮廓参数组合，20 组真实固定地图位置/拆填/岩粒开关组合。
- 现有 TerrainRepairTests：7/7 通过，使用真实候选 Core 源文件，运行器为 .NET 8。
- Core、View、Editor 三个程序集使用当前 Local Unity 6000.4.9f1 的响应文件和实际引擎引用，在隔离输出目录独立编译成功；最后一次 Editor 修改也单独复编译成功。不是 Unity 导入、生成器全周期或 Player 验收。
- 同机 .NET 8 Release CPU 内核微基准：3 次预热，11 次测量。单格中位数 30.5622→15.4186 ms；本批样本 P95 33.0539→18.0975 ms。8×8 合成压力矩形中位数 36.2462→20.7284 ms，P95 37.3464→25.8530 ms。样本较少且 Unity 仍开着，只证明本次 CPU 内核改善；不含输入排队、GPU、相机、网络，不等于游戏端到端延迟。
- 优化后仍可能超过 4 ms 同步预算，不能宣称每次都在同一帧完成。

证据：evidence/cave-tuner-isolated-20260925.json。原始输出保留在隔离目录 artifacts/cave-tuner-probe，不自动删除。

## 待验证与独立后续路线

先按 A 验证 Tuner 单格、持续拖动、Undo/Redo/Cancel、配置防抖、最终确认只补一次、异常可见、不无限重绘。再按 B 比较同一输入的最终岩壁与全量结果、首图、边界/跨页、64 格压力事务和实际相机端到端耗时。
使用同一 Mono 构建检验 Host/Client、弱网、断线重连、旧代次不能回闪；确认普通 Delta 的区块装卸、冻结背景重烘焙为零。以上 Unity/GPU/碰撞/联机项目本轮均 NOT_RUN。

若目标要求严格一帧的最终材质完成，应另开纯表现缓存路线：缓存不随地形变化的世界坐标岩面、局部轮廓/距离场增量计算，并保留全量算法作像素比较基准。不可简单提高主线程同步预算、缩小 Halo、换一个更简化的假预览或提前置 Ready。

既有跨资源限制仍保留：源输入是原子事务，不代表每个中间帧的规则 Mesh、岩壁及光照已经统一原子切换。本次没有借 Editor 修复改造该主链路。
