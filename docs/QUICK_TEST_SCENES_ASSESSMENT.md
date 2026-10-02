# 主菜单快速测试与场景整理复评

2026-10-02。用户要求在主菜单的 Debug Hub 中提供 GUI 快速测试入口，记住上次选择，方便频繁进入已着陆、持矿镐的测试状态，并把现有场景整理清晰。本轮为再次评估，未实现按钮、预设或场景迁移；结论来自当前源码、锁定依赖及场景引用核对，未运行 Unity 编译、Play 或 Player 构建。

## 建议结论与日常操作

采用 **Debug Hub 的“快速测试”页＋本机记忆的测试预设**，首项为“已着陆 · 矿镐”。复用正式 Bootstrap、Expedition、地图生成、装备 Definition 和采集代码；新增少量测试初始条件装配，不复制另一份远征场景。正式开局、联机加入及存档继续沿用原入口。

实现后的日常流程：

1. 打开 Bootstrap 并 Play，停在主菜单。
2. 按 **F1 或反引号**打开现有 Debug Hub，进入“快速测试”页。
3. 选择“已着陆 · 矿镐”，点击 **启动所选测试**。以后这个选择自动恢复，直接点击即可。
4. 正式地图与表现就绪后，进入船已着陆、主角已在舱外安全位置并选中矿镐的状态。
5. 要重测时返回主菜单，再点相同按钮；每次生成干净的初始测试局，不继承上一局挖掘、货袋或装备修改。

拟议面板只保留“测试项选择／用途说明／启动所选测试／生成及失败状态”。记忆的是稳定测试 ID，不是场景路径、列表下标、运行中的世界或整份存档。第一次默认选矿镐测试；记录的项目已退役时回退到有效默认项。退出 Play、重启 Editor 后仍记住选择，但不自动开局，也不默认改变普通“新游戏”。

## Debug Hub 接入与显示边界

当前 `Game/Packages/manifest.json` 使用 `.deps/YYGC-grid-business`；其 `RuntimeDebugHub` 已提供 `RegisterPanel`、`UnregisterPanel`、`IRuntimeDebugPanel` 及激活／停用生命周期。现有 `ExpeditionHud` 已经接入远征页。Hub 在 **Editor 或 Development Build** 中编译，F1／反引号切换，普通发行 Player 不包含它。

Hub 当前没有按游戏页面过滤面板的接口。游戏自己的快速测试面板仅在“`SessionUiController.Page == MainMenu`、无活动客户端／服务端会话、无连接或生成任务”的状态注册；离开主菜单、开始启动、断开清理未完成或宿主销毁时撤下。不能只把按钮变灰并长期保留标签，也不能仅凭 `Replica.Current == null` 判断主菜单空闲，因为连接期间它也可能为空。

GUI 的 Draw 只提交一次启动意图，后续按现有主线程异步流程处理，避免在绘制调用中修改面板集合、同步生成地图或重入连接。启动按钮立即锁定，关闭 Hub，显示生成状态；失败回到可重试的主菜单，释放输入和任务资源。注册／撤销依据状态变化执行，不能每帧重复注册或清空整个 Hub 的其他面板。

主菜单已有优先级 100 的本地模态会话。快速页复用这层菜单状态，不照搬远征页的低优先级输入租约造成冲突；启动和撤销时必须检查鼠标／主角输入恢复。记忆在 Editor 可用项目隔离的 `EditorPrefs`，开发 Player 可用 `PlayerPrefs`；只存一个测试 ID，不写入游戏权威状态或正式存档。

此接法复用当前 YYGC 能力，**不需要修改框架、升级依赖或修改 MainMenu 原生 Prefab**。如果以后要求普通发行包也提供该入口，那是另一项明确的产品需求，不应顺带把 Debug Hub 带进发行包。

## 快速开局真正需要处理的状态

“快速测试场景”在 GUI 上是一个测试项，在实现上是**同一正式场景的开局预设**。不能直接 `LoadScene(Expedition)`：主菜单时这个内容场景已经由 Bootstrap 加载，重复加载既不能完成着陆，又可能重复建立会话与 UI。地形工作台的点击破坏也不能作为矿镐测试后端。

首批使用固定、可复现的星球和种子，正常读取当前 WorldSession、地形 Profile、矿镐 Definition 和 balance。生成地图仍需真实等待；省掉的是购镐、走到驾驶台、过场、驾驶和人工下船步骤，不宣称地图能够瞬时生成。找出有安全支撑、净空及附近合法前景岩壁的舱外出生点；当前配置无法提供有效位置时报告失败，不能临时放宽碰撞或挖掘权限。

| 接入位置 | 需要完成的工作 | 保持的现有合同 |
| --- | --- | --- |
| Entry 的快速测试 GUI／启动入口 | 接收一次测试 ID、恢复上次选择、显示状态、空闲时启动本机 Host | 普通 NewGame／Join／Continue 不携带预设；不能用 UI 保存一份权威状态 |
| `SessionTerrainNetwork` 的地图选择 | 显式预设选择正式星球地图，避免仍生成太空地图；清理旧的未消费候选和失效异步结果 | 复用同一个正式生成器、冻结配置和地图发布链 |
| `SessionNetwork` 的会话装配 | 把仅此次启动有效的预设传给服务端准备步骤；串行生成、连接、发布和错误回收 | 继续使用现有唯一 YYGC ObjectSession 与网络命令入口 |
| ObjectSession／远征准备及默认主角创建 | 在发布前准备一致的着陆航程、地图身份、船体、舱门、乘员与出生点；默认主角使用真实矿镐定义身份并选中装备 | 状态仍归原业务 Behaviour／ActorState；后续挥镐、落镐伤害、奖励与货袋均走正式逻辑 |
| 重开与测试存储 | 快速局的重开基线也是已着陆测试初态；测试存档目录与正式槽位隔离；返回主菜单清空启动预设 | 不覆盖用户存档，普通开局不继承测试状态 |

当前 `ObjectSession.Prepare()` 末尾捕获 `initial`，`Restart()` 直接恢复它。预设必须在捕获基线前生效，或显式建立正确测试基线；如果只在 Ready 后零散改状态，重开就可能回到太空。默认主角又是在玩家 Ready 后分配，必须将人物位置／矿镐初始化接到对应生命周期，避免多生成一名人物或重复发装备。

当前地图 `Disconnect()` 释放副本与传输，但没有直接清空地图选择缓存。切换测试预设、快速局与普通局时，候选缓存及后台生成的归属必须明确，不能复用上一次的太空／星球候选。使用既有连接尝试代次、取消与释放模式即可，不新建通用任务调度框架。

这些属于局部装配改动，规模为小到中等；不能承诺只加一个 GUI 按钮就完整实现。预计改动集中在上述游戏入口及初始化文件，新增测试目录／面板也按 C# 9、单文件上限及中文 XML summary 约定执行。无需改变当前游戏数值、采矿规则、对外协议或存档格式；实现后须以实际差异确认此边界。

首批只提供矿镐预设，不把 16 个场景全部塞进运行时下拉框。离线工作台、第三方示例和完整网络会话各有不同生命周期；它们继续通过 Editor 工作台按分类打开。未来真正需要另一种频繁测试时，再登记一个有明确初始化与退出流程的测试项。

## 16 个现有场景的实际整理方案

完整原路径、用途和引用见 [场景索引](SCENES.md)。下面将上轮建议细化为物理位置和入口整理；所有拟议路径相对 `Game/Assets`。本轮没有执行迁移或删除。

| 对象 | 具体建议 |
| --- | --- |
| `Scenes/Bootstrap.unity`、`Res/Scenes/Expedition/Expedition.unity` | 保留路径和 GUID；工作台中显示为“正式游戏入口”“正式远征内容”。快速测试继续使用这两项。 |
| `Res/Scenes/Workbenches/Terrain/RandomCave.unity` | 保留，日常名称为“地形预览”；与矿镐正式玩法测试明确区分。 |
| `Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` | 建议移入 `DarkNights/Res/Scenes/References/Terrain/ReferenceChamber.unity`，从日常地形入口移到“参考对照”；保留其 EditorOnly 静态参考画面和 GUID。它的 Play 仍走正式生成，不能标成另一套固定地图。 |
| `Res/Scenes/Pinewatch/Pinewatch.unity` | 建议归入 `DarkNights/Res/Scenes/Regression/Camp/StaticCampRegression.unity`，标“静态营地回归”。同步更新 camp-mode、构建和作者工具的路径引用。 |
| `Res/Scenes/RandomPinewatch/Pinewatch.unity` | 建议归入 `DarkNights/Res/Scenes/Regression/Camp/RandomCampRegression.unity`，标“随机营地回归”。保留其布局、GUID 和专用构建用途，解决两个 Pinewatch 同名混淆。 |
| `Res/Scenes/Tests/Terrain/TerrainTest.unity`、`TerrainNetworkTest.unity` | 保留 Tests 分组，显式标为单机／网络探针；不占用日常场景列表。 |
| `(old)/TerrainDebugBootstrap(old).unity`、`(old)/CaveExploration(old).unity`、`PendingDeletion/Terrain/CaveContourStatic.unity` | 建议统一到 `DarkNights/Res/Scenes/Archive/Terrain/` 的退役分组，沿用各自文件名与 GUID，并列明缺绑定／被替代情况。移除当前菜单中的旧创建／运行入口；迁移现行路径和 GUID 保留测试，不删历史证据或共用代码。 |
| `Scenes/SampleScene.unity` | 从 Build Settings 移除空模板项；它不在 `GamePlayerBuild` 的显式构建列表中。先保留场景文件与 `templateDefaultScene` 引用，后续单独决定是否替换模板并删除。 |
| `Settings/Scenes/URP2DSceneTemplate.unity` | 保留在 Settings，供现有 scenetemplate 使用。 |
| `Samples/LanCoop/Content/LanCoop.unity`、`Samples/YYGCInputActions/Content/InputActions.unity` | 保留在 Samples，Editor 工作台“独立样例”列清用途，不混入正式入口或快速局预设。 |
| `Plugins/EdgarDev/Smart Console/Demo/Demo.unity` | 保留插件原路径；仅按第三方示例标识，不修改插件内容。 |

导航分为：日常开发（正式游戏、地形预览）、快速测试、参考对照、旧玩法回归、专用测试、独立样例、已退役。已退役默认折叠，点击只定位资源，不继续提供会重新生成旧场景的按钮。可以为本轮完整盘点的已有资源登记目录，不为尚不存在的预设制造可用按钮。

这是整理 **6 个场景的位置／名称、清除 1 个构建残留、收敛导航** 的方案，首轮不靠永久删除降低数量。迁移时用 Unity AssetDatabase，保留 `.meta`／GUID 和人工场景字节，统一更新 `TerrainScenePaths`、启动／构建路径、菜单及测试；历史日期证据保留当时路径。不要直接文件系统改名后再让 Unity 重建 `.meta`。正式 Bootstrap 和 Expedition 不移动。

三个退役实验虽然退出产品，但仍有现行测试和旧工具引用；实际删除是下一项有明确文件清单的操作。关联纹理、样式、生成器和冻结样板资产的独占关系尚未核实，不应随场景一并删除。工作台合并也不授权删除 ReferenceChamber 的参考画面。

## 执行顺序与验收

建议同批先实现快速入口及有限预设，再集中整理场景路径／工作台目录，一次更新相关合同、编译和验证。实施前检查当前分支、未提交改动、Unity 活动与磁盘空间；当前已有其他地表／移动修改，本轮不能覆盖或混入提交。无需再开一套 Unity 缓存或构建 IL2CPP。

必要验证：

- 主菜单显示快速页，帮助页／暂停菜单／游戏中／连接中隐藏；记忆跨 Play 与 Editor 重启有效，无效 ID 安全回退。
- 双击按钮只启动一次；返回主菜单能再次启动；生成失败／取消／退出不遗留任务、候选、输入锁或测试存储设置。
- 新局真实着陆、人在安全舱外、装备真实矿镐；实际鼠标选取、连续挥镐、地形耐久／奖励／货袋工作，每次重测回到干净地图。测试局重开仍是测试初态。
- 普通 NewGame、Continue、Join 不受记忆影响；测试存档隔离；地图和表现仍经过完整 Ready。
- 全部 16 个场景归类完整，迁移的 6 个 GUID 及场景内容保持；现行路径、Build Settings、模板引用和专用构建列表正确；新路径可打开、保存、重开。
- 若交付范围包含多人快速测试，使用独立 Host＋Client 验证初始地图／着陆／装备及采集收敛；单个 Editor Host 不代替联机验收。若仅交付 Editor 快速入口，明确不宣称新 Player 或联机通过。

本轮只有评估文档与引用检查，没有执行上述验收。可按本方案继续实施，不需要为每个按钮、每个场景逐步请求“继续”。
