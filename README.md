# Dark Nights · Unity

2026-09-24 [AnyRuleD 地图联网重构](docs/MAP_STATE_NETWORKING.md)在 `ref-20260924-map-state-networking` 开发：纯协议与 FishNet 传输已从 YYGC 业务包拆出，单格 Delta 改为 1 个最终格记录，游戏副本通知已接入范围刷新。现有 Editor 编译通过；本轮 Editor、Mono 联机及独立样板的具体结果以进度文档为准，不沿用旧 Player 数字。

2026-09-24 [地形 Modifier 与可插拔点缀层进度](docs/TERRAIN_MODIFIERS_PROGRESS.md)：下坠岩齿／花菜圆簇已接入主工程，前景及三个背景层可分别配置，点缀生成器可替换。`Cave Wall Tuner` 保留样式与地图草稿 Apply／Cancel、拆填和网格；预览仅在空白离屏宿主中渲染当前地图，共用正式 `TerrainPreview`、AnyRuleD、`CaveVisualSource` 和 Cave shader，不加载正式游戏场景或角色。拆填微基准发现活动 RoundedRock 下单格全图轮廓烘焙约 281–286 ms，之后全量差异比较约 39–132 ms，是画面滞后的主要已测阶段；Unity 端到端及 Player 前台帧时仍待实测，不据微基准宣称通过。

2026-09-22 本分支已接入[可步入远征飞船](docs/WALKABLE_EXPEDITION_SHIP.md)，`ft-20260922-walkable-expedition-ship`，协议 **14**／存档 **v10**。支持船内步行、唯一驾驶席、泊位附近试飞、搬运机器人与侦察机出舱归队；远征默认应用当前 StrataCave 岩层与三层背景。50 张原生素材导入检查 200/200，新增飞船／远征用例 24/24，同一 Mono 正常／弱网三进程各 28/28，Core 1048/1048；Editor 按影响合并 235/236，既有按钮主题 1 项失败留账。实际画面和本批证据见实现说明，不宣称全洞穴航行、异地降落、前台性能、IL2CPP 或双机器通过。

2026-09-22 当前候选为[独立洞穴材质与三层背景](docs/STATIC_CAVE_BACKGROUND_EXECUTION.md)，分支 `codex/static-cave-background`，协议 **13**／存档 **v9**。`Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` 是 HTML 同源固定样板，同目录 `RandomCave.unity` 验证随机地图（2026-09-23 仅整理场景路径，资产仍在 `Res/Terrain/StrataCave/`）；独立 AnyRuleD 规则、原生 8px 岩壁、圆形柔光和角色比例已接入。矿粒及矿光按用户要求暂时隐藏。正式远征以 `--dn-contour-static` 试用，原风格保持默认；结果及前台性能边界见本批记录，下方为历史切片。 外轮廓为独立可配置的六方案，当前 HybridB／OUTLINE-0921／3px／20px／2px、岩块 4px；仅修饰表现，权威碰撞保持原坡形。

2026-09-21 当前切片为[远征营地 Demo 实施与快速验收](docs/archive/EXPEDITION_CAMP_DELIVERY.md)，分支 `codex/expedition-camp-plan`，协议 **12**／存档 **v8**。正式入口接入紧凑洞穴、背景墙矿物和透岩矿光、氧气与货袋、机器人四设备展开、矿工交货、风险撤收、原子结算与三种舱段成长。Editor 按影响合并 213/214 通过，1 项通用按钮主题用例留账，Mono 常规／弱网四进程各 35/35；完整证据、玩法简化和待验边界以交付记录为准，不将原[执行方案](docs/archive/EXPEDITION_CAMP_EXECUTION.md)中的完整性能及全路线门槛写成通过。

当前默认玩法为远征。地图调试仍可通过[洞穴地图工作台](docs/archive/CAVE_WORKSHOP.md)进入；旧营地仅作为 `--dn-camp-mode` 的兼容回归入口。各旧版本的验收数字留在对应切片文档中，不作为当前构建结论。过时的指令圈运行链和专用回归已删除。

目录设计已收口为 `Assets/DarkNights/Scripts` 与 `Res` 分离；代码采用 Core、Runtime、View、Entry，资源按对象／面板集中维护定义、Prefab 和专用资源。Addressables 不要求游戏素材目录采用特殊名称，仍通过 ObjectDefinition 驱动加载与绑定；现有 AddressableAssetsData 配置位置保留。详见[目录及绑定要求](docs/archive/MIGRATION_PLAN.md#directory-and-assets)。目前已建立四个运行程序集、Editor/Tests、配置与 Pinewatch 场景、15 类原生对象、效果与五页 UI。

`Game/` 是 Unity 宿主，[ProjectVersion](Game/ProjectSettings/ProjectVersion.txt) 为 `6000.4.9f1`。先运行 `tools/prepare-lan-sample.ps1` 与 `tools/prepare-fishnet.ps1` 准备锁定提交的 `.deps/YYGC-unified`／`.deps/FishNet`，不直接引用用户维护的框架工作区。FishNet 4.7.2 带断线分片清理补丁，见[恢复接入](docs/archive/NETWORK_RECOVERY.md)。R3、MemoryPack、UniTask 等依赖已导入；VitalRouter 使用 YYGC 要求的完成语义修正版。内部产品名暂保留 `DNights`。

原评估日期：2026-09-10；Sample 验证日期：2026-09-11。当时分支为 `main`，当前迁移分支见上方。早期评估未修改 `D:\Developer\YYGC`、Godot 基线或参考素材；2026-09-12 的 Workshop 展示修复已同步 YYGC，逐文件记录见[改动账本](docs/YYGC_CHANGES.md#workshop-display)。下方评估输入表保留历史时点。

## 核心判断

YYGC 适合作为应用、表现与联机基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、UGUI、本地交互仲裁、网络命令、服务端状态发布和单对象首次快照。[早期能力复评](docs/archive/YYGC_REASSESSMENT.md)说明修正缘由，当前 Sample 记录了修正后可复用的命令与状态路径。

保留普通 C# 模拟，由房主唯一结算世界；游戏补营地权限、业务去重、投影、Ready、epoch 和恢复流程。首个切片用会话级 StatefulBehaviour/StateSynchronizer 同步可靠完整投影，测量后再决定分块与拆流。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab。各玩家独立选择，SharedCamp / HostOnly 由服务端统一校验，关闭共同操作不会改变模拟或单位所有权。

早期复评的生成器、首状态和序列化问题属于当时版本。新 Sample 已在 YYGC `10b8f0e` 上复用完整命令与状态链，通过真实多进程验证；独立程序集补丁、依赖和未验收边界见 [Sample 说明](docs/LAN_SAMPLE.md)。联机尚未正式生产使用。

早期 M0／M1 时点的剩余工作估算为 16–26 人日，预留后约 20–33 人日；该历史估算及 [M5 当时的收尾状态](docs/archive/M5_EXECUTION.md#closeout)不代表当前剩余工作。当前飞船切片与未验边界见[实现说明](docs/WALKABLE_EXPEDITION_SHIP.md)。

## 范围与假设

| 项目 | 状态 |
|---|---|
| 2–4 人合作，共享一个营地 | 用户已确认 |
| 灰松谷、三夜、既有素材与规则 | 本次内容基线 |
| Windows 桌面、房主主持、先局域网／直连 | 方案与估算假设；公网入口尚未确认 |
| 客户端独立镜头、选择与建造预览 | 联机设计要求 |
| 默认共享控制，可切仅房主操作 | 本次方案；关闭后普通玩家只观看／查看，服务端统一限制直接命令与自动派工 |
| 暂停、倍速、提前入夜、存档权限 | 已经统一权威入口接入产品与网络；活跃施工／训练／箭矢组合恢复仍待新 Player 验证 |
| 全新地图、PVP、锁步、回滚、房主迁移 | 本轮估算范围之外 |

## 阅读入口

| 要解决的问题 | 文档 |
|---|---|
| 当前合同、开发入口与近期切片 | [文档索引](docs/README.md) |
| 场景位置、用途与入口 | [场景索引](docs/SCENES.md) |
| 试玩与本机复跑 | [Player 指南](docs/PLAYER_GUIDE.md)、[Quick start](docs/QUICK_START.md) |
| 状态归属、权限、存档与依赖 | [技术架构](docs/ARCHITECTURE.md)、[联机设计](docs/MULTIPLAYER.md)、[存档格式](docs/SAVE_FORMAT.md)、[依赖说明](docs/DEPENDENCIES.md) |
| 历史迁移方案、框架评估、阶段验收与清理列账 | [历史文档索引](docs/archive/README.md) |
| 自动化协作与编码要求 | [AGENTS.md](AGENTS.md) |

## 评估输入

| 输入 | 基线 |
|---|---|
| `D:\Developer\YYGC` | HEAD `6c3e0ff96221ac4a9fdfe0db85bf8f2cdc8dabc9` 加评估时工作区 |
| `../projects` | HEAD `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`，工作区干净 |
| YYGC 用户未提交内容 | 已暂存 `Runtime/UI/UGUI/UGUIManager.cs`；未跟踪 `Tools/IDRegistry备份数据20260812` |
| 实际 Unity Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`；YYGC 包声明 `6000.2` + `35f1`，已在 6000.4.9f1 编译通过 |

精确规模、关键源文件 SHA-256、原始素材核验及历史 Git 状态见[冻结评估证据](docs/archive/evidence/assessment-2026-09-10.json)。Godot 基线保持该提交；评估后的早期 YYGC 锁定曾为 `ccd61e0` / `0.3.0-preview.1`，包含 Workshop 展示修复与 Definition 场景入口修复；该时点后者的回归待确认。当前隔离依赖、验证边界及补丁见[依赖说明](docs/DEPENDENCIES.md)，不将历史预览版本写成已发布稳定版本。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

正式 Bootstrap 场景保留，AppStartup 已接入规则加载模块；全局对象定义与命令／状态注册表仍保持环境基线。Sample 使用自己的定义、StateData、NetworkCommand、Prefab 和场景。`Library/`、构建输出、`.deps/` 和日志不提交，冻结验证摘要在 `docs/evidence/`。
