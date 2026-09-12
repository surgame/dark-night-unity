# Definition 驱动的场景放置

2026-09-13：实现完成，测试回归待用户确认。Unity `6000.4.9f1` 编译已通过；没有运行 Editor/Play 测试、Player 构建、联机或弱网回归。历史 C 重构通过记录不能视为本次改动已验收。

## 当前职责

| 内容 | 唯一来源 |
|---|---|
| 对象身份、分类、Prefab | ObjectDefinition 的 GUID／Key、Type 与 PrefabRef |
| 场景静态初始化 | ObjectDefinitionLoader，沿用启动 Gate 和显式 Initializer |
| 顺序、名字、变体 | 同一 Prefab 实例上的薄 LevelPlacementMarker |
| 布局坐标 | Loader 的场景 Transform，由 LevelLayoutAuthoring 导出纯数据 |
| 旧规则与存档 Kind | DefinitionRuleIndex 从既有 `unit.*`／`building.*`／`worksite.*` Key 后缀导出 |
| 游戏状态与 EntityId | 房主 Core；表现只绑定权威投影给出的 `(epoch, EntityId)` |

`ContentDefinitionMap`、`ContentDefinitionEntry` 和 `LevelPlacementCategory` 已删除；WorldSession 不再保存独立手填映射。15 个实体 Definition 和 2 个网络 Definition 已补齐 Type。只读索引验证 Type／Key 一致、重复 Kind、PrefabRef，以及规则目录的缺失或多余项。Key 后缀现在是明确的旧格式兼容合同；改名不能只改资产 Key 而不迁移规则引用。

## 工作流与生命周期

在有 LevelLayoutAuthoring 的场景中拖入游戏 ObjectDefinition，YYGC 创建对应 Prefab＋Loader，游戏 Editor 扩展依据 Definition.Type 放入显式绑定的布局分组并添加实例参数。修改位置、顺序、名字、变体后保存。PrefabRef 与场景 Prefab 不一致会由放置验证报错，不自动覆盖手工外观。

GameSessionStartupModule 改为 Order 9100，并显式依赖 Order 9000 的 Loader 启动模块。场景已加载时先激活等待的 Loader；后加载场景通过已经打开的 Gate 同步激活。没有在 8600 阶段等待尚未执行的 9000 模块。

SceneEntityViews 接管已装配的本地视图，并作为按定义复用的场景外观集合。SessionEntityViews 根据实际投影中的 EntityId 借用并绑定外观，不根据初始化顺序推算实体 ID。重开、连接切换、加载 epoch、删除及转职先解绑；场景实例归还并隐藏，动态工厂实例销毁。新建建筑、招募、敌人和临时预览仍使用现有对象工厂。场景视图没有另一套 HP、经济或 AI。

当前关卡试玩通过 Editor 的 ScenePlaySelection 保存所选场景路径，在 Bootstrap 完成后加载它；支持另存的 `PineWatch#2` 等未加入 Player 构建列表的副本。未保存场景或有未保存修改时提示先保存；工具不自动进入 Play、不改 Bootstrap 资产或 Player 场景列表。

## 现有场景迁移

显式菜单 `Dark Nights/Content/Upgrade Scene Definition Loaders` 只改已有实例的组件及引用，不重建 Prefab。Pinewatch 的 16 个放置项已迁移：保留现有 Prefab 连接，把薄参数组件移到 Prefab 实例根，添加 Loader，移除旧 LayoutVisualPreview。原场景辅助父节点和全部变换保留。

导入新字段后，已打开场景曾被 Unity 标为 dirty。先保存副本到忽略目录 `Game/Temp/SceneDefinitionDrafts/Pinewatch.unity`；确认全部差异只有旧 category/contentId 替换为新的空组件引用后，才保存并迁移。没有丢弃用户未保存编辑。迁移明确跳过预览姿态重采样。

静态差异检查确认 144 个既有 Transform／Renderer／Camera 等组件以及全部 Prefab 属性覆盖不变；16 组 Loader／ObjectView／Initializer 引用均非悬空。本次未改规则 JSON、原始美术、Prefab 或冻结夹具。摘要见 [证据](evidence/scene-definitions-2026-09-13.json)。

## 确认后执行的回归

1. 冻结布局与初始 ID 顺序、Definition／Prefab 一致性、拖拽 GUID 与单例判重。
2. 保存重开、当前场景副本试玩、重复 Play、开局／退出／重开、转职和加载 epoch 的实例归属。
3. 复用一次 Mono 构建，执行独立 Host＋客户端初始快照、晚加入、重连和重复实例检查；按受影响范围续跑既有联机矩阵。

以上均待确认，IL2CPP 未构建、未验证。YYGC 四个修改文件与锁定提交见 [改动账本](YYGC_CHANGES.md#scene-definitions)。
