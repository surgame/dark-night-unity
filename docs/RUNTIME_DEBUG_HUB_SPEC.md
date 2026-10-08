# Runtime Debug Hub 重构与面板接入规范

2026-10-08。目标是将运行时 Debug Hub 统一为 UI Toolkit 容器，让物体、飞船、地图、会话及后续诊断模块通过同一契约接入。容器管理窗口、导航、输入和生命周期；面板提供视图及本模块的操作。本文定义目标合同和实施范围，框架迁移尚未执行。

## 现有实现与迁移边界

游戏 `Packages/manifest.json` 当前将 GameCore 指向 `.deps/YYGC-grid-business`。其中 `Runtime/Debugging/RuntimeDebugHub.cs` 使用 `OnGUI`、`GUILayout.Window` 和 `IRuntimeDebugPanel.Draw`；`RuntimeDebugPanelContext` 只提供 `GUIStyle`。它是 IMGUI 容器。

游戏船内商店已使用 `UIDocument`、`GameCore.UI.UIManager`、`UIPanel`、UXML 和 USS，可复用已有资源装配、字体和 UI 管理能力。当前游戏 Hub 提供者为 `QuickTestPanel` 和 `ExpeditionHud.ExpeditionDebugPanel`，实施前应再次核对全部提供者和依赖补丁。

Unity 6.4 的 `IMGUIContainer` 用于 Editor IMGUI 内容；运行时 Hub 的迁移不能依赖它包裹旧 `Draw` 面板。当前提供者必须转换为保留式 VisualElement 视图后，才能切换运行时容器。[Unity API](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/UIElements.IMGUIContainer.html)

## 容器和面板职责

| 归属 | 必须承担的职责 |
| --- | --- |
| Hub 容器 | 面板注册、排序、导航、打开关闭、窗口位置与尺寸、焦点、输入租约、共享主题、异常边界 |
| 面板 | 本模块控件、冻结数据展示、操作参数、局部选择与筛选、业务请求与回执显示 |
| 游戏权威入口 | 可信连接、开发者授权、Ready、epoch、版本、去重、参数与事务校验 |
| 所属业务 Behaviour | 库存、对象、地图等可变状态；生成、移除和恢复的业务不变量 |

Dark Nights 的运行时入口保持一个 F1 Hub。各功能通过面板接入，复用同一窗口和样式。框架容器不引用 Dark Nights、FishNet 玩家业务、地图规则或装备状态；游戏依赖在面板工厂的构造参数中注入。

后续项目可采用相同容器合同及自己的面板集合。另一个独立 Hub 宿主必须有明确的窗口用途、独立的注册作用域与视图实例；本批不预建多窗口调度框架。Editor 工具可复用主题和可复用的视图部分，其入口与生命周期继续由 EditorWindow 管理。

## 注册合同

目标 API 以一个描述类型、一个面板接口和一个可释放的注册句柄为主，继续沿用 GameCore 的启动与 DI 路线。

| 契约 | 内容 |
| --- | --- |
| `RuntimeDebugPanelDescriptor` | `Id`、`Title`、`SortOrder`、`MinimumSize`、`PreferredSize` |
| `RuntimeDebugHub.RegisterPanel` | 接收描述和面板工厂，返回注册句柄；首次打开该页才创建视图 |
| `IRuntimeDebugPanel.CreateView` | 返回该面板唯一的 `VisualElement` 根；同一实例只创建一次 |
| `IRuntimeDebugPanel.OnActivated` | 接收本次激活的取消令牌，订阅展示数据并呈现当前状态 |
| `IRuntimeDebugPanel.OnDeactivated` | 停止订阅、调度和局部交互；必须可重复安全调用 |
| `IRuntimeDebugPanel.Dispose` | 最终释放控件回调、资源租约和任务；释放后不可再次激活 |
| `RuntimeDebugPanelContext` | 宿主生命周期和必要的 UI 上下文；不提供全局业务状态或任意命令执行器 |

接入规则：

1. `Id` 是稳定业务标识，例如 `dark_nights.objects`、`dark_nights.ship`、`dark_nights.quick_test`。标题、本地化文本和类型名不承担身份职责。同一实现类型可注册多个不同 ID。
2. 排序使用 `SortOrder`，同序按 `Id` 排序。面板选择和本地偏好都按 ID 保存，注册顺序改变不导致选中另一页。
3. 重复 ID 必须明确报告注册错误；不能静默覆盖另一模块或按类型吞掉注册。调用者保存句柄，重复启动回调先检查自身是否已经注册。
4. 句柄 `Dispose` 幂等，只注销本次注册。旧句柄不能注销相同 ID 的后续新注册，必须比较注册代次。
5. 注册到现有启动／会话装配流程；禁用、退出场景或会话时释放句柄。不开新 DI 容器，不建立每帧扫描类型的自动注册器。
6. 注销当前页面先停用和释放，再切到仍可用的页面；无页面时显示必要状态。面板创建失败只标记该页失败，Hub 的导航与关闭继续可用。

实施时新增手写类型遵守 C# 9、中文 XML summary 和单文件上限。这里的接口名与方法名是目标合同，现有调用者在同一迁移批次中适配。

## 生命周期和输入

正常顺序为 `注册 → 首次 CreateView → OnActivated → OnDeactivated → 再次 OnActivated → Dispose`。未打开过的面板注销时只释放注册，不要求先实例化。

- Hub 打开后，容器统一取得 YYGC Interaction Session 的输入租约；关闭、失效、卸载、异常或销毁时释放。面板不再各自竞争同一 Hub 的键盘／鼠标租约。
- 切页先取消旧页激活令牌并停用，再激活新页。关闭 Hub 不清空世界物体，也不撤销已经提交的权威操作。
- 所有数据订阅和 UI 定时刷新受激活生命周期约束。隐藏页不继续查询场景、刷新列表或输出日志。
- 异步读取在激活取消、连接变化或 epoch 变化时失效。UI 回调校验激活代次，过期回执不覆盖新会话；后台结果拥有的资源仍需释放。
- 界面保存搜索、选择、滚动位置等本地偏好。控制权限和上帝模式授权归服务端当前会话，不能由 EditorPrefs、客户端复选框或玩法存档授予。
- 输入框编辑期间保护文本与数值操作；Hub 的 F1 路由只有一个所有者。F10 继续使用现有 Smart Console，重构不新建日志控制台。
- 注册表、静态快捷入口和资源在退出／重新进入 Play 后恢复到正确初态，关闭 Domain Reload 时也不得累计旧面板。

## UXML 和 USS 规范

容器结构统一为窗口标题、面板导航、内容挂载区、必要状态区。面板在内容区提供一个根元素。物体页使用一个列表，没有产品简介、功能宣传或装备详情卡片。

```text
UIDocument
└── VisualElement name="debugHubRoot" class="rdh-root"
    └── VisualElement name="window" class="rdh-window"
        ├── VisualElement name="header" class="rdh-header"
        ├── VisualElement name="navigation" class="rdh-navigation"
        ├── VisualElement name="contentHost" class="rdh-content"
        │   └── VisualElement class="dn-debug-objects"
        │       ├── VisualElement class="dn-debug-objects-toolbar"
        │       └── ListView name="objectList" class="rdh-list"
        └── VisualElement name="status" class="rdh-status"
```

- UXML 声明 `UnityEngine.UIElements` 命名空间并引用 USS。每个模板有一个顶层容器，不写行内 `style`。
- `name` 使用 camelCase；类名使用 kebab-case。共享类采用 `rdh-` 前缀，业务样式采用模块前缀，例如 `dn-debug-objects-`，只作用于本面板根内。
- 容器 USS 提供背景、文本、分隔线、强调色、危险色、圆角、间距和控件状态。面板复用控件类，不重复定义全局 `Button`、`Label` 或所有输入框的样式。
- 主题使用 USS 自定义属性和明确的主题类。网页原型中的 `light-dark()` 只用于预览跟随宿主主题，不能直接复制到 USS。
- 布局采用 Flexbox 子集及子元素 margin；不依赖 CSS Grid、`gap`、`box-shadow`、`filter`、浏览器伪元素、属性选择器、`calc()` 或 CSS 媒体查询。边框用 `border-width` 和 `border-color`，圆角及纯色填充由 USS 实现。
- 窗口受屏幕可用区域和已有 PanelSettings 缩放约束。最低可用宽度暂定 640 UI 单位，首版参考尺寸 720×600；实际导入时按字体和目标分辨率确认。紧凑布局由容器的几何变化事件切换明确类，保留关闭和操作按钮。
- 列表由 `ListView` 的虚拟化管理滚动和复用；首版固定行高 54 UI 单位。每行复用 `Label`、`DropdownField` 和 `Button`，禁止每次数据变化重建整个 VisualTree。[Unity ListView](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-uxml-element-ListView.html)
- 使用 `makeItem`、`bindItem`、`unbindItem`／`destroyItem` 分别构建、绑定和释放行；绑定不得反复叠加按钮回调。操作读取本行当前绑定的稳定身份，不能捕获首次绑定的列表下标。
- 图标复用本地项目资源。UI Toolkit 使用 TextCore 字体资产和既有中文字体装配，不能把 TMP 字体资产当作 UITK 字体。
- `:hover`、`:active`、`:disabled` 的文本与背景作为完整配对维护。运行时禁用原因直接显示短状态或由统一的运行时提示控件展示，不假定 Editor 的 `VisualElement.tooltip` 会在 Player 自动弹出。

上述布局、边框和圆角属于 USS 支持的能力。[Unity USS 属性](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-USS-SupportedProperties.html) HTML 原型通过不等于 Unity 的 USS 导入、字体和实际 Player 布局通过。

## 物体面板合同

列表从当前 `ObjectDefinitionDatabase` 生成，显示名称、Key、类型、当前实例／槽位和直接操作。当前资产定义数量仅为预览输入，正式实现不得硬编码名称列表或数量；新增定义按相同目录与能力规则自动出现。

| 对象类别 | 添加 | 移除 |
| --- | --- | --- |
| 装备 | 指定目标角色，复用库存校验及初始化，免支付；保留槽位与数量约束 | 指定角色和槽位／能力身份，取消动作并更新库存版本 |
| 世界实体 | 已实现调试处理器的定义按指定位置生成；资源在事务外准备，回到权威入口再次校验 | 明确选中 EntityId，处理引用、索引和资源生命周期 |
| 手持投射物 | 需要种类和初始参数的专用处理器，不能直接生成表现 Prefab 代替权威投射物 | 通过所属投射物业务状态移除具体实例 |
| 暂停玩法定义 | 继续列出并显示暂停状态；不能自动恢复 NPC、物流或营地业务 | 随对应玩法范围与处理器定义 |
| 系统、基础及旧定义 | 继续列出，默认保护或只读 | 当前主角、唯一飞船、会话、连接、作者服务点等明确保护 |

所有目录展示和能力判断基于定义身份及配置能力；Key 是搜索与诊断信息。服务端处理不得仅凭名称前缀、客户端分类或按钮可用状态确定操作权限。

多实例时显示下拉选择，每行删除按钮必须指向当前选定的实例；单实例或唯一装备槽直接显示身份。多实例场景不能默默删除列表中的第一项或最近生成的一项；零实例时禁用移除。被选实例消失时清空或明确更新选择，过期请求由服务端拒绝。

上帝模式默认仅房主可执行。后续授权其他连接需要明确的服务端开发者权限；普通 SharedCamp 权限不自动升级。面板发送业务意图，继续复用 YYGC Gateway／Sender／Processor 和游戏 `SessionAuthority`；面板不直接编辑 State、发任意脚本或操作客户端表现物体。

公共的 Hub 不提供一个无限扩张的字符串命令中心。新增调试业务接入本模块的有类型入口，复用所属业务事务。调试命令仍携带 epoch、序号、目标身份和相关版本；重复请求只执行一次。

## 内存和反馈

Hub 只显示必要的执行结果和拒绝原因，不添加玩家用简介。反馈文本和队列必须有界；默认状态区只保留最近一次结果。详细日志继续进入已有有界日志链，不收录自身渲染警告形成反馈循环。

对象资源按本次操作和会话生命周期持有，面板不预加载所有定义的 Prefab。列表先读取静态元数据；生成时按批加载所需资源，取消或失败时收尾释放。不得为了调试窗口复制整套世界状态。

每批 Unity 验证沿用 `AGENTS.md` 的磁盘、Editor 私有内存、可用 RAM 和提交余量门控。先短时探针，再扩大批次；越界停止本任务 Play，不结束其他应用。

## 框架修改范围与落点

根据 `AGENTS.md`，YYGC 的源码、隔离工作树、本机 `.deps`、补丁和锁定版本修改都需要用户事先对具体范围明确同意。当前规范和预览不执行这些修改。

拟议范围如下，路径相对对应仓库：

| 落点 | 拟改文件或新增职责 | 原因和影响 |
| --- | --- | --- |
| 隔离 YYGC | `Runtime/Debugging/RuntimeDebugHub.cs` | 将 IMGUI 容器转为 UITK 宿主，统一导航、输入与打开关闭 |
| 隔离 YYGC | `Runtime/Debugging/IRuntimeDebugPanel.cs` | 将 `Draw` 接入合同转换为 VisualElement 与显式生命周期，当前提供者需同步迁移 |
| 隔离 YYGC | `Runtime/Debugging/RuntimeDebugPanelContext.cs` | 退出 GUIStyle，提供最小 UI 和生命周期上下文 |
| 隔离 YYGC | 同目录新增描述、注册句柄、注册表和 Hub 视图类型 | 稳定 ID、代次校验、懒创建、统一销毁；按职责拆分，避免巨型 Hub 文件 |
| 隔离 YYGC | `Runtime/Debugging/UI/RuntimeDebugHub.uxml`、`RuntimeDebugHub.uss` 及必要资源登记 | 通用容器结构和主题；复用既有 AssetProvider／UIManager，不新建 UI 资产加载框架 |
| 游戏 | `Game/Assets/DarkNights/Scripts/Entry/QuickTestPanel.cs`、同目录 `QuickTestHub.cs`、`ExpeditionHud.cs` | 将现有页面迁移到新视图合同和注册句柄，复用原有业务入口 |
| 游戏 | 物体页的 Entry、Runtime 调试处理器及 Res/UI 模板 | 增加统一列表与权威调试操作；具体命令及恢复范围随实现列明 |
| 游戏 | 必要的依赖准备脚本、源码锁、锁文件及 `docs/YYGC_CHANGES.md` | 接入已验证的框架候选，保留已获批补丁，保证重新准备可复现 |

落地先在隔离 YYGC 检出形成可审查差异，再通过游戏的单一 Local Unity 验收通道验证。用户维护的 YYGC 主仓库及当前 Local 其他任务不被代为切换、合并或清理。是否集成、升级锁定依赖和合入主分支按明确授权执行。

## 迁移顺序与验收

1. 核对当前锁定依赖、已获批补丁、全部注册者和已有 Editor／Player 进程，确认框架修改范围。
2. 实施注册合同、UITK 容器和主题；迁移快速测试与飞船页后统一切换。运行时不留下永久的双 Hub 入口，也不采用 IMGUIContainer 兼容方案。
3. 接入物体列表及已授权的调试处理器。UI 重构与调试业务分别审查，协议／保存合同是否变化按实际命令与状态决定。
4. 按下表执行一次有限验证批次，失败只修复并重跑受影响项。完成后逐项维护 YYGC 修改文件、原因、落点与验证结果。

| 验收主题 | 必须证明的行为 |
| --- | --- |
| 注册 | 同类型不同 ID 可同时存在；重复 ID 明确失败；旧句柄不影响新注册 |
| 生命周期 | 打开关闭、切页、注销活动页、断线、重连、场景卸载和关闭 Domain Reload 均不泄漏订阅／租约 |
| UI 资源 | UXML／USS 导入无错误与未知属性；中文字体、主题、长名称和禁用状态可读 |
| 输入 | F1 只触发一次；F10、文本输入、鼠标与镜头互斥正确；关闭后角色控制恢复 |
| 列表 | 滚动／筛选／换目标后按钮仍操作正确 GUID、槽位或 EntityId；回调不叠加 |
| 调试事务 | 零支付、库存满、重复请求、旧 epoch／版本、保护对象和未授权连接正确处理 |
| 同步和恢复 | Host 与客户端结果一致；晚加入和重连读到最终状态；调试存档恢复通过严格校验 |
| 资源和内存 | 反复打开切页关闭与有限生成移除不累计对象、字体、订阅或队列；内存门控记录完整 |
| 构建边界 | Mono 开发构建验证新 Hub；普通非开发构建的调试注册和业务执行关闭；IL2CPP另行授权 |

## 后续面板接入清单

新模块只需提供稳定描述、一个实现新契约的面板、自己的 UXML／USS 和现有启动流程中的一次注册。需要业务写入时，再提供所属模块的权威处理器。

接入评审核对：ID 唯一、样式作用域正确、数据归属明确、激活订阅可释放、命令参数显式、权限在服务端、资源按需准备、反馈有界、退出不残留。新增面板不得为自己的导航、快捷键、窗口拖动或输入互斥修改 Hub 主体。
