# Runtime Debug Hub 接入规范

2026-10-09 物体页最新合同：48×48纯图标格、32px图标、6px间距、单格选择，按业务身份筛选真实物体；移除行ListView，由Hub统一滚动。名称／Key／能力状态放在悬停提示及选中信息区，搜索隐藏选中项时清空选择。游戏侧 `HubTheme.uss` 适配Hub专属Panel的内部控件，不修改YYGC共享源码。当前实际Host界面及验证见[物体面板记录](DEBUG_HUB_ICON_PANEL_20261009.md)，下方静态编译说明属于原候选。

2026-10-09：本候选已集成到 `ft-20261008-flashlight-lighting`，当前协议30／存档v22。下方日期及计数保留各自历史输入身份，当前结果见[集成记录](RUNTIME_DEBUG_HUB_MERGE_20261009.md)。

2026-10-08。本规范对应 `ref-20261008-runtime-debug-hub-uitk` 候选。Runtime Debug Hub 已从 IMGUI 改为 UI Toolkit；本批完成源码和静态编译，Unity 导入、实际画面和运行行为尚未验证。实现与验收边界见[开发记录](RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md)。

## 已确认的界面需求

最新用户变更：**上半部分采用 grid 物体网格，下半部分保持原预览的持有装备区**。这一需求替代之前的表格／逐行物体列表方案。

- 页面标题为“物体”；不使用“道具发放”，不写玩家式简介。
- 网格收录具备装备、单位、建筑、工位或矿床配置的有效定义，以及显式登记的真实投射物；排除UI、音频、会话、连接、服务、漂浮文字及指令圈。运行目录不按Key前缀猜测业务身份；名称／Key搜索忽略大小写与首尾空白，图标引用来自定义。
- 点击网格块选择定义，统一操作区显示目标、数量、明确的实例身份以及添加／移除按钮。
- 下方保留四个持有装备槽、独立喷气背包及最近操作；保持原开发者工具的深色、青色强调和危险按钮风格。
- 暂停玩法中的真实物体和受保护物体继续显示，能力不足时使用短状态。调试页不自动恢复 NPC、刷怪、物流、营地修改或出售等暂停业务。

## 容器和面板职责

| 所有者 | 职责 |
| --- | --- |
| Hub | 注册、排序、导航、F1、窗口拖动与尺寸、统一输入租约、共享主题、面板异常边界 |
| 面板 | 本模块控件、冻结副本展示、局部筛选和选择、显式操作参数、回执 |
| 游戏权威入口 | 可信连接、房主权限、Ready、epoch、协议、策略版本、序号去重 |
| 所属业务 Behaviour | 库存及物体可变状态、事务、不变量、资源释放 |

正式入口只有一个 F1 Hub。框架不引用 Dark Nights、玩家业务、网络适配或地图规则；业务依赖通过面板工厂传入。不新建 DI、事件框架、通用脚本执行器或第二套对象状态。

## 注册合同

| API | 内容 |
| --- | --- |
| `RuntimeDebugPanelDescriptor` | 不可变 `Id`、`Title`、`SortOrder`、`PreferredSize`；默认参考尺寸720×600 |
| `RuntimeDebugHub.RegisterPanel(descriptor, factory)` | 返回 `IDisposable` 注册句柄；首次激活才创建面板及视图 |
| `IRuntimeDebugPanel.CreateView(context)` | 返回唯一 VisualElement 根；成功后同一实例只创建一次 |
| `OnActivated(CancellationToken)` | 本次激活的订阅、调度和取消范围 |
| `OnDeactivated()` | 幂等停止订阅、调度和局部交互 |
| `Dispose()` | 最终释放；不得重新激活 |
| `RuntimeDebugPanelContext` | `Close()` 和有界 `Report(string)`；不授予业务权限 |

1. ID 使用稳定业务标识，例如 `dark_nights.objects`、`dark_nights.ship`；标题及类型不是身份。同类型可注册不同 ID。
2. 按 SortOrder、ID 排序；选中项按 ID 保持。重复 ID 明确抛错，调用者保存句柄并避免重复启动注册。
3. 句柄只注销本次注册，释放幂等；旧句柄不能注销同名新注册。
4. 沿用现有会话装配。禁用和卸载时释放句柄；不每帧扫描类型或自动发现面板。
5. 注销当前页先停用并切到有效页。创建或激活失败不会破坏导航、关闭和其他页；重新进入失败页可再次创建。
6. 正常顺序为注册、CreateView、激活、停用、再次激活、最终释放。未打开的页面不因注销而实例化。

接入示例：

```csharp
registration = RuntimeDebugHub.RegisterPanel(
    new RuntimeDebugPanelDescriptor("dark_nights.example", "诊断", 300),
    () => new ExampleDebugPanel(existingService));
// 模块禁用／退出时：registration?.Dispose(); registration = null;
```

## 生命周期和输入

Hub 独占一次 Interaction Session 租约，优先级150、SuspendLowerPriority、阻断玩法输入；关闭、停用或销毁均释放。各面板不重复取得 Hub 租约。F10 继续归已有 Smart Console。

切页先取消激活令牌并停用旧页，再激活新页；隐藏页停止150ms UI调度及回执订阅。关闭窗口不撤销已经提交的权威操作。异步回调须核验激活代次及 epoch，旧回执不能写入新页面状态。

搜索、选择及滚动位置是面板本地状态。上帝模式授权只归当前服务端会话 epoch，不写 EditorPrefs 或玩法存档。SubsystemRegistration 清理旧注册与宿主，覆盖关闭 Domain Reload 的重新进入场景；真实行为仍待人工验证。

## UXML、USS 与资源

```text
UIDocument
└── debugHubRoot / rdh-root
    └── window / rdh-window
        ├── header
        ├── navigation
        ├── contentHost
        │   └── objectsRoot / dn-objects-panel
        │       ├── 搜索、目标、上帝模式
        │       ├── ListView objectGrid（虚拟化网格行）
        │       ├── 选中定义和操作
        │       ├── 四个持有装备槽
        │       ├── 独立喷气背包
        │       └── 最近操作
        └── status
```

- UXML 声明 UnityEngine.UIElements、引用相邻 USS、只有一个顶层容器，不写行内 style；name 使用 camelCase，class 使用 kebab-case。
- 共享主题使用 `rdh-`，游戏物体样式使用 `dn-objects-`；原生 Button、输入框、Foldout 的子控件也按明确作用域适配。
- 使用 Flexbox、子元素 margin 和几何事件；不使用 CSS Grid、gap、阴影、filter、calc、媒体查询或结构伪类。几何尺寸和拖动位置由宿主管理，颜色及正常布局由 USS 管理。
- 网格通过 ListView 固定86单位行高虚拟化，每行用 Flex 排列1–6个块。宽度改变才重建列结构；筛选与选择刷新绑定。按钮始终读取当前绑定 GUID，解绑清除身份及图标，回调只在构建时添加。
- 图标复用现有定义的 Sprite，不预加载所有对象 Prefab。使用 TextCore FontAsset；中文字体与既有船内 UI 一样采用 Microsoft YaHei 动态装配。
- 宿主克隆已加载的有效 PanelSettings 主题；没有现有主题时使用独立设置及容器 USS。窗口上限受屏幕百分比约束。
- 通用 UXML/USS 位于包内 `Runtime/Debugging/Resources/YYGC/Debugging/`，游戏模板位于 `Res/UI/DebugHub/Resources/DarkNights/Debugging/`。这些模板是随开发工具打包的 Resources 资源，**不是 Addressable 资源**；不重复登记 Addressables。Hub 视图沿用 UIPanel.Bind 和 DIContainer.Root，不新增资产加载框架。
- 静态 XML／USS 子集检查仅核验源码结构；Unity 导入、中文、主题、弹出控件和实际布局仍需 Editor 查看。新增资产与脚本的 .meta 留待该候选首次正常 Unity 导入生成；不人工分配 GUID，已有 GUID 保留。

## 物体与权威操作

当前开放装备免付费添加／移除及手持子弹生成／明确实例移除。手枪、矿镐和炸弹进入原四槽库存；喷气背包是独立能力。炸弹支持1–100批量，库存仍受1000总数上限约束；普通装备只添加一个。并未新增炸弹商品或售价。

其他定义仍列在网格，当前操作为只读／暂停。后续开放世界对象生成时，需提供该模块的准备资源、创建、移除和引用清理处理器，不能直接 Instantiate 表现 Prefab 或自动解除玩法范围限制。唯一飞船、当前主角及业务服务对象必须保护。

- 只允许可信房主连接；SharedCamp 不授予开发者权限。非开发构建参数入口直接拒绝调试请求。
- 使用原 SessionClient、SessionAuthority 和 YYGC 命令通道；开发命令编号1000–1004与普通玩法分开。
- 切换上帝模式需服务端回执；授权不越过 epoch。请求携带明确 GUID、ActorId／实例ID、数量及库存版本，不使用全局选中对象代替请求参数。
- 商店和调试共用库存初始化事务；普通商店继续验证距离、价格、余额并扣款。调试不给钱，也不经过经济扣款。
- 装备移除清理所选动作、蓄力和采矿工具引用；炸弹清数量，喷气背包清燃料。投射物通过原权威数组移除，不单独销毁表现。
- 多实例必须明确选择。目标消失或库存版本过期由服务端拒绝；未确认请求锁定操作，12秒超时后给出状态且不自动重发。

## 反馈、验证与范围

状态文本至多256字符，最近操作至多4条。隐藏页无调度，不捕获自己的渲染警告；日志继续使用已有有界链。面板只读取冻结副本，不复制另一份世界。

本批用户明确要求仅静态编译。使用隔离分支和只读 Local 引用，不启动 Unity、Play、Editor测试或Player；不切换、合并或覆盖另一个开发对话及 YYGC 主仓库。正式非开发源码、开发源码及 Editor 源码分别编译；这不等同于对应 Player 已构建或运行。

后续验证重点为注册代次／Domain Reload、网格复用身份、装备动作取消、房主与旧 epoch 拒绝、去重和联机副本一致性。关键人工项目及逐项框架差异见实现记录和 [YYGC账本](YYGC_CHANGES.md)。框架范围限定 Debugging 内容器、契约、描述、注册表、视图、资源及对应可重建补丁；既有其他补丁继续保留。
