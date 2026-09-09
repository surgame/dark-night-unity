# Dark Nights Unity 适配方案

基线为现有工程 `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`。目标保持灰松谷的素材、布局、数值、操作意图和三夜玩法，建立原生 Unity Prefab 与合作会话。这里描述迁移工作，不表示 Unity 内容已经制作完成。

## 当前内容与代码量

| 模块 | 文件／物理行 | 迁移方式 | 难度 |
|---|---:|---|---|
| Simulation | 26 / 1,379 | 保留规则和处理顺序，替换少量 Godot 数学／随机 API，拆出客户端选择 | 中 |
| Content | 14 / 333 | 复用数据与定义语义，将 JSON／素材加载移到 Unity 适配层 | 低至中 |
| Persistence | 16 / 692 | 保留 DTO／校验／关系恢复思路，替换 IO、JSON 兼容和 RNG 边界 | 中 |
| Presentation | 54 / 2,814 | 在 Unity 中建立 Prefab、AnimationClip、UGUI、镜头和效果绑定 | 中高，工作量主体 |
| Bootstrap | 5 / 229 | 接入 YYGC 启动与单一会话装配 | 中 |
| 总计 | 115 / 5,447 | 不能把行数直接折算为可复制代码比例 | — |

现有 44 个 `.tscn`，包括 6 类角色、5 类建筑、4 类工位外观，以及 UI、效果、环境、关卡与预览场景。原始素材清单有 551 个文件、75 个 sprite 组、9 个 sound 条目；字节总量约 1.92 MiB，来源哈希已重新核验。

既有 Godot 记录包含 133 项游戏检查和 27 项架构工具自测。这些是迁移的验收输入，本轮没有重新运行，更不是 Unity 测试结果。

## 内容冻结边界

| 内容 | 原始唯一来源 | Unity 人工来源／派生关系 |
|---|---|---|
| 经济、单位、建筑、工位数值 | `projects/data/balance.json` | 保留 JSON；Unity 只读加载成 Core 定义 |
| 关卡 ID、seed、waves | `projects/data/levels/pinewatch.json` | 保留 JSON；仍为 `pinewatch`、seed 90127 |
| 初始摆放与边界 | `projects/scenes/levels/Pinewatch.tscn` 的 Layout | 一次迁移为 Pinewatch.unity 布局标记；派生定义不另作手填来源 |
| 外观、动作、偏移、锚点 | 原生 `.tscn/.tres` | 对应 Prefab、AnimationClip、外观目录资源 |
| 原图／音频／帧序／来源 | `projects/assets/manifest.json` | 原字节复制到 Art/Original，保留来源和导入映射 |
| 初始布局／旧档回归 | `projects/tests/fixtures` | 只读测试夹具；不可成为正式运行时依赖 |

冻结输入的精确哈希见[证据](evidence/assessment-2026-09-10.json)。新仓库运行不能依赖原游戏目录的绝对路径或 Godot 导入缓存。

需保持的代表性内容：初始食物60、木100、石80、铁40、金0；5工人、1长矛兵、1弓箭手，人口7/9；酒馆等4座初始建筑；5个显式自然工作点和由农田生成的食物工位。世界宽1100像素，地面Y=320，建设X=30–850，出生X=1020。

三夜敌人数量7/11/16，准备90/70/70秒，生成间隔3.8/3/2.7秒。费用、伤害、攻击前摇、训练和施工不因玩家人数调整。完整数值以 JSON 为准，本文件不维护第二套数值表。

## C# 核心迁移

1. 将 Content 规则定义、Simulation 和纯快照／校验迁到 Core，使用 Unity 支持的 C# 9 语法。移除主构造、required、文件级 namespace、集合表达式；显式构造／字段验证不能丢失不可变和必填语义。
2. 替换 Godot.Vector2 等基础类型为实际需要的小型值类型，例如 WorldPoint；移动、吸附与舍入保留原 double/float 边界，不引入完整数学框架。
3. 原 GameCatalog.Read 的 FileAccess、SaveRepository 的 user:// 和文件操作留在 Runtime 适配层；Core 接收已经验证的定义／快照。
4. 将 InteractionState、SelectGroup、Construction.Begin 等客户端意图移到本地交互层。Issue、Place、StartSelected 改收显式单位列表、职业、建筑种类和位置，返回业务结果而不是修改全局选择。
5. 保持有序 ActorIds 和 SpawnOrder。原 AI 时钟按 ID 派生、编队偏移、候选工人和训练顺序都不能被无意排序改变。
6. 保留 Economy.Pay、独占关系释放、施工受伤不回满、训练退款、箭矢延迟命中和波次结算的业务语义。

原数学行为需要特例检查：建造 `Mathf.Snapped(x, 4)` 的半格舍入、双精度 MoveToward、伤害的 AwayFromZero 舍入。不能仅把 API 名称替换成 Unity Mathf 后假定边界相同。

### 随机数与旧档

原会话用 Godot.RandomNumberGenerator，存档保留其 seed/state 的64位位表示。System.Random 或 UnityEngine.Random 使用同一个 seed 不会自然得到同样的序列。

M1 需要实现并验证该 Godot 版本的算法、播种、整数／浮点范围映射和状态恢复；首版冻结原调用顺序。用固定 seed 以及旧档继续20秒的夹具逐字段对照。不能只检查“能读出 JSON”就宣称兼容。显示效果不能消耗会话随机数。

如果无法达到旧序列兼容，应记录为未完成的兼容目标，并明确需要单独决定的存档／玩法变化；不能更换随机算法后继续声称内容结果一致。

## 场景与 Prefab 对应

| 现有职责 | Unity 对应 | 维护人可直接编辑 |
|---|---|---|
| ActorView / BuildingView / WorksiteView | YYGC ObjectView 实体容器 Prefab | 绑定引用、选区、状态条锚点 |
| 六角色 Visual 场景 | Worker、Spearman、Archer、Zombie、Ghoul、Armored 等 Visual Prefab | 身体／手臂分层、帧、颜色、ArtOffset |
| 五建筑 Visual 场景 | Tavern、House、Barracks、Farm、Watchtower Prefab | 成品图、施工阶段、锚点 |
| 四工位 Visual 场景 | Wood、Stone、Iron、Food Prefab | 变体与枯竭表现；农田工位避免重复绘制 |
| Arrow、尸体／废墟、飘字、指令圈 | 纯效果 Prefab | 显示与寿命；不结算命中 |
| Torch、CampLight、Backdrop | 环境 Prefab／SpriteRenderer／适量 URP 2D 灯光 | 布局、颜色、视差 |
| HUD、菜单、组件、Theme | UGUI Prefab、RectTransform、TMP 与主题资源 | 原静态布局、间距、字体、按钮状态 |
| Pinewatch/Layout | LevelAuthoring＋Placement 标记 | X、ContentId、SpawnOrder、边界 |
| ArtReview | ArtReview.unity | 一屏检查15类外观与 HUD 样本，无正式会话 |

建议的实体层次：

```text
ActorView                       脚底世界根；EntityId 绑定
  VisualSlot
    WorkerVisual                可独立编辑与替换的 Visual Prefab
      ArtOffset
        Facing
          Origin
            BackArm / Body / FrontArm
      StatusAnchor
      SelectionAnchor
  SelectionOverlay
  StatusDisplay
```

具体类和资源名在实施时可按职责调整；脚底、视觉偏移、朝向、状态锚点与玩法占地分离的合同必须保持。ObjectDefinitionLoader 遵循 YYGC 的场景／Prefab 保存规则，不把场景加载辅助器随意 Apply 到实体 Prefab。

### 坐标、像素和排序

模拟继续以原世界像素单位工作。建议展示尺度 PPU=100，统一在适配边界转换：

```text
UnityWorldX = GodotWorldX / 100
UnityWorldY = (320 - GodotWorldY) / 100
局部视觉偏移 = (GodotLocalX / 100, -GodotLocalY / 100)
```

因此1100像素地图显示为11 Unity单位，30像素/秒显示为0.3单位/秒；JSON 的速度和射程不改。地面基线320应来自关卡定义，实际代码不能散落这一常数。鼠标／镜头坐标需逆变换回模拟单位后再发请求。

迁移已校准的 Sprite2D 时，首版可统一使用左上角 pivot `(0,1)`，将已有 offset 转成局部坐标。manifest 原点用于核验，不再重复叠加。若改用原点 pivot，公式为 `(originX/width, 1-originY/height)`，同时消除旧偏移的重复量；先用工人和分层弓箭手验证，再批量转换。

像素图采用 Point 过滤，检查压缩、mipmap、图集边缘和Pixel Perfect设置。角色使用 SortingGroup，组内明确后臂／身体／前臂层级；脚底朝向翻转不能带动状态条或让占地漂移。

### 动画、UI 与预览

采用原生 AnimationClip/Animator，帧图切换使用离散键。PosePresenter 根据模型动作阶段和时间采样，不通过 Animation Event 施加伤害或启动生产；关闭 root motion。身体11帧与静态手臂等不同轨道必须保留各自帧序。

施工阶段、受击闪烁、转职换装和死亡残骸按现有规则映射；AnimationClip 可以由美术编辑，但行为时机仍归模拟。帧时钟和网络插值分开，暂停时战斗动作不能继续推进到新的命中。

HUD 使用1280×800作为对照尺寸，同时验1600×900；建立独立的菜单、选择、命令、资源和小地图面板，不把布局堆进一个生成脚本。Godot 主题里的字体、回退与系统字体选择需单独核实，Unity/TMP 必须具备实际中文字符资源。

编辑器中的布局标记显示正式 Visual Prefab 样本，Play 时统一由本地视图绑定器管理，预览不额外产生游戏实体。拖动标记、修改动画帧、替换原点和主题后，保存重开仍生效。普通构建和导入不能重新生成正式 Prefab 覆盖美术修改。

## 数值与关卡验证

将现有测试的业务意图迁为 Core/NUnit 与 Unity 场景检查，重新建立覆盖关系；不为了沿用“133项”这个数字而制造空测试。

需要保留的基线：

- 初始 Layout 与世界实体快照；农田派生工位与ID顺序一致。
- 固定正常资源策略三夜胜利：原记录约377.67模拟秒、34击杀、酒馆320HP；无人照料约452.10秒失败。时间和完整状态按既有浮点容限检查，不只比较胜负。
- 旧 v1 恢复与继续20秒的逐字段结果，允许原基线定义的浮点容差，不覆盖旧夹具。
- 暂停、2×、支付原子性、工位独占、训练部分成功、死亡清理和在飞箭矢的保存／恢复。
- 正常控制所触发的规则一致；多人并发的次序由服务端明确定义，不能要求不同网络抵达顺序产生同一策略轨迹。

跨引擎画面采用固定场景、镜头、动作时间和窗口尺寸对照。纹理字节相同不代表渲染逐像素相同；记录颜色空间、灯光、后处理与文字排版差异，逐项检查角色脚底、分层、UI与昼夜观感。Godot 的旧 GPU 对照结果不自动适用于 Unity。

## 存档接入

Unity 新档应有独立格式标识与版本，并记录规则摘要和随机算法标识；具体字段在 M1 冻结。旧 v1 用独立导入入口，保留其严格字段和关系校验，不让两种格式被自动猜测混用。

世界快照与玩家显示设置分离。旧档的相机／选择可供房主本地恢复，其余客户端使用各自设置。epoch、连接ID和网络对象ID是本次会话状态，不把它们当作持久实体身份。

默认先保留 JSON 与原子文件写入边界；接 YYArchive 时将整个世界作为一个模块。任何恢复路径都先建立临时世界，完成全部校验后再替换正在运行的世界。

## 迁移执行方式

先迁移规则和少量工人／建筑 Prefab，验证两个独立进程的命令与快照；随后扩充为完整灰松谷。避免先制作全部界面才发现核心身份或同步入口不成立。逐阶段工作量与验收见[执行计划](DEVELOPMENT.md)。

一次性资产迁移工具可以读取原 manifest 和场景数据，输出到明确的空目录，并生成路径／GUID／原点／帧序对照；此后正式 Unity 场景归人工维护。原目录和原始素材不清理、不移动。
