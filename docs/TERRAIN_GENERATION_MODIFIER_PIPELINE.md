# 地形生成阶段 Modifier

2026-09-30，分支 `ft-20260930-terrain-modifier-pipeline`。本批实施源码与文档，并仅为新增脚本 `.meta` 执行一次 Unity Editor 导入；按用户要求不运行测试、Play 或 Player 构建。导入成功不代表功能验收。

## 产生截图中斜向阻断的链路

`TerrainGenerator.GenerateCave` 产出天然洞穴后，`PlanetTerrainGenerator` 清理天空、铺设保护泊位，再执行下洞步道。旧 `CarveWalkway` 逐列清出头部空间，同时把脚下两格写成材料 `2`。斜线跨过已有空腔时，就可能形成截图中的石带。此前删除的是天然洞穴里的塌方 `Cover` 与竖井 `Shelves` 回填；它们和泊位后的步道属于不同生成阶段。

## 当前结构

权威地形阶段固定为 `AfterCave → AfterSky → AfterDock → BeforeGeometry`。每阶段按 `WorldSession.asset → ExpeditionFlowConfig.Modifiers` 中的原始顺序执行适用步骤；完成后统一生成坡形、筛选矿床并捕获背景。`ITerrainGenerationModifierConfig` 是可序列化的作者接口，使用 Unity `[SerializeReference]` 保存具体类型；Editor 用 YYGC Objects 同样的 Odin `PropertyTree` 绘制类型选择与字段。作者对象只在主线程读取，进入后台前由 `FreezeModifiers()` 转为不引用 Unity 的 `ITerrainGenerationModifier`。业务实现可声明阶段并在该阶段访问 `TerrainGenerationContext` 的权威材料、保护位和软岩。

原步道已迁为 `EntranceWalkwayModifierConfig → EntranceWalkwayModifier`，默认启用，位于 `AfterDock`。可调净空 3–7 格、最多 1–256 个候选支撑点；逐个尝试并比较修改前后的空腔分量，避免把原本连通的区域切成两块。若没有安全候选，生成会明确失败；关闭此项可直接观察天然洞穴与泊位的结果，但不保证存在从泊位步行进入洞穴的路。旧资源缺少新列表时仍视为默认步道，首次 Apply 会把显式步骤写入正式配置。显式空列表代表没有附加权威步骤。

现有 `ICaveMaskModifier` / `CaveModifierAsset` 仍处理岩壁外观和点缀；其输入是表现遮罩，不能改权威格子。它与权威生成步骤属于不同类型的阶段，不再把所有 modifier 限制为点缀层。新增需要改权威通行的业务步骤实现 Core 接口及 Runtime 作者配置；只改外观的步骤继续使用现有遮罩链。

## 编辑入口与身份

- `Dark Nights / Terrain / Cave Wall Tuner`：在正式星球与预览种子下展开权威步骤列表，选类型、启停和调参；重建后显示各步骤的阶段、变更格数和包围范围。保存正式生成配置才写 `WorldSession.asset`，取消恢复草稿。临时拆填依旧只作用于当前预览。
- `Dark Nights / 配置 / 星球与航程`：同一列表与星球目录共用 Apply／Cancel，蓝图缩略图使用冻结步骤。运行时工作台只读取正式配置，不维护第二份接口列表。
- 草稿深拷贝逐项调用具体配置的 `Copy()`，避免依赖 `JsonUtility` 对嵌套接口引用的复制行为。内容身份和冲突检查显式包含类型、顺序、启停及参数；生成器版本升至 3。现有地图与存档不自动重生，需生成新地图观察变化。

## 待验证

Unity 6000.4.9f1 批处理导入已生成 10 个新增脚本的 `.meta`，日志无编译错误，退出码 0；完整日志归档在 `artifacts/待清理/20260930-terrain-modifier-pipeline/terrain-modifier-import-20260930.log`。本批未验证 Odin 嵌套列表的类型选择、`[SerializeReference]` 持久化、`STRATA-0922` 及其他种子的安全步道候选、完整洞室可达性、联机内容身份和已有存档行为。关闭步道仅用于对照或业务主动选择；不应据此声称玩家能够进出洞穴。
