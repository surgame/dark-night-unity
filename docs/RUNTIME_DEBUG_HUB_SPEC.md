# Runtime Debug Hub接入规范

本页维护当前F1面板注册、生命周期、界面和房主调试操作。实际完成情况见[执行状态](DEVELOPMENT.md)，框架具体授权／补丁见[YYGC账本](YYGC_CHANGES.md)。历史候选中的ListView行网格及隔离静态编译声明不作为现行合同。

## 容器和面板职责

| 所有者 | 唯一职责 |
| --- | --- |
| Hub | 注册／排序／导航、F1、窗口拖动／尺寸、统一输入租约、共享主题及异常边界 |
| 面板 | 本模块控件、冻结副本、局部搜索／选择、明确参数与回执 |
| 游戏权威入口 | 可信连接、房主开发权限、Ready、epoch、协议、策略和去重 |
| 所属Behaviour | 库存／对象可变状态、事务及资源释放 |

正式F1只有一个Hub；框架不引用Dark Nights业务／地图／网络适配。面板工厂接收现有依赖，不新建DI、事件总线、通用脚本执行器或第二套对象状态。F10继续归已有Smart Console，日志链见[架构](ARCHITECTURE.md#本地照明与有界日志)。

## 注册合同

| API | 合同 |
| --- | --- |
| RuntimeDebugPanelDescriptor | 不可变Id、Title、SortOrder、PreferredSize，默认参考720×600 |
| RuntimeDebugHub.RegisterPanel(descriptor, factory) | 返回IDisposable句柄，首次激活才创建面板／视图 |
| IRuntimeDebugPanel.CreateView(context) | 唯一VisualElement根，同实例成功后只创建一次 |
| OnActivated(CancellationToken) | 本次激活订阅／调度和取消范围 |
| OnDeactivated() | 幂等停止订阅、调度和局部交互 |
| Dispose() | 最终释放后不再激活 |
| RuntimeDebugPanelContext | Close与有界Report，不授予业务权限 |

稳定Id如dark_nights.objects／ship，与标题、类型分开；同类型可有多个Id。按SortOrder再Id排序，选择按Id保持；重复Id明确失败。旧句柄不能注销同名新注册，未打开页面不因注销而实例化。

正常顺序注册→CreateView→激活→停用→再激活→最终释放。注销活动页先停用并切有效页；创建／激活失败保留导航与关闭，重新进入失败页可重新创建。SubsystemRegistration清理旧注册及宿主，覆盖关闭Domain Reload的再次进入；具体运行回归仍按待验队列记录。

```csharp
registration = RuntimeDebugHub.RegisterPanel(
    new RuntimeDebugPanelDescriptor("dark_nights.example", "诊断", 300),
    () => new ExampleDebugPanel(existingService));
// 所属模块退出时幂等释放registration。
```

## 生命周期和输入

Hub独占一次Interaction Session租约，优先级150、SuspendLowerPriority并阻断玩法输入；面板不重复取得Hub租约。关闭、禁用、异常或销毁释放。切页先取消旧激活令牌并停用，隐藏页停止150 ms调度及回执订阅；关闭窗口不撤销已经提交的权威操作。

搜索、选择和滚动属于本地面板。异步回调核验页面代次、连接代次及epoch，旧回执不写新页。上帝授权只归当前服务端会话epoch，不写EditorPrefs或玩法存档。状态文本最多256字符、最近操作最多4条，不捕获自身渲染警告。

## 物体页界面

上方为纯图标物体网格，下方保留四槽装备、独立喷气背包和最近操作。页面名“物体”，名称／Key／能力放悬停提示和选中信息区。

- 单格48×48，图标32 px，间距6 px，按宽度Flex换行，单格选择；不存在86高的虚拟化ListView行。Hub负责唯一滚动，面板不叠第二层ScrollView。
- 收录有效装备、单位、建筑、工位、矿床及明确登记的真实投射物；排除UI、音频、连接、服务、漂浮文字和指令圈。不按Key前缀猜业务身份。
- 搜索名称／Key忽略大小写及首尾空白；筛选隐藏所选项就清选择。操作始终读取当前绑定GUID／角色／明确实例，不能复用旧下标。
- 图标来自定义Sprite，复用已有静态帧及作者来源，不预加载全部Prefab。缺能力／暂停／受保护对象仍可查看，以短状态说明限制。

```text
UIDocument
└── Hub窗口
    ├── header / navigation
    ├── contentHost
    │   └── objectsRoot
    │       ├── 搜索、角色目标、上帝模式
    │       ├── VisualElement objectGrid：48×48图标格
    │       ├── 选中信息和明确操作参数
    │       ├── 四个装备槽、独立喷气背包
    │       └── 最近操作
    └── status
```

## UXML、USS与资源

UXML声明UIElements，引用相邻USS，单根容器，不写行内style；name为camelCase，class为kebab-case。共享样式rdh-，游戏物体样式dn-objects-。Flex、margin和几何事件负责布局，不用CSS Grid、gap、阴影、filter、calc、媒体查询或结构伪类。

游戏HubTheme.uss限定Hub专属Panel，适配Button、输入框、Foldout、Dropdown和ScrollView内部控件，不修改YYGC共享源码。窗口位置／尺寸由Hub管理，颜色和正常布局由USS负责；上限受屏幕百分比约束。PanelSettings克隆有效主题或使用独立设置，中文使用TextCore及已有船内UI的Microsoft YaHei装配方式。

框架模板在包内 `Runtime/Debugging/Resources/YYGC/Debugging/`，游戏模板在 `Res/UI/DebugHub/Resources/DarkNights/Debugging/`；这是既有开发工具Resources模板，非Addressable，不重复登记。视图沿UIPanel.Bind和DIContainer.Root，不另建加载框架。新meta交Unity，人工UXML／USS／Theme／Prefab不由普通启动覆盖。

## 物体与权威操作

当前允许合法装备免付费添加／移除，手持子弹生成及明确实例移除。手枪、矿镐、炸弹、手电走原四槽，喷气背包独立；炸弹批量1–100、库存总数1000上限，普通装备一次一个。不会新增炸弹／手电商品、给钱或改变普通商店扣款。

可信房主在开发会话启用上帝模式且取得回执后操作。命令编号1000–1004与普通玩法分开，非开发构建参数入口拒绝调试请求。沿SessionClient／SessionAuthority、YYGC命令及同一初始化事务，核验Ready、协议、策略、epoch、序号和库存版本。

移除装备清所选动作、矿镐缓存、炸弹蓄力／数量或喷气燃料；移除手电另清照明引用／开关。投射物从原权威数组移除，不直接销毁表现。多实例必须明确选择；目标消失或版本过期拒绝。未确认请求锁定操作，12秒超时提示状态，不自动重发。

其他定义仍只读／暂停；世界对象开放需所属模块准备、创建、移除及关系清理入口，不直接InstantiatePrefab或解除暂停范围。唯一飞船、当前主角及业务服务对象受保护。

## 验证要求

核对注册／旧句柄／Domain Reload、失败页和租约释放；宽度改变／筛选／换主角的绑定身份；动作中移除、满槽／重复／批量限制；普通Client、旧epoch／版本和重复序号；Host→Client、晚加入／重连及保存恢复。实际UXML／USS导入、中文、弹出控件和布局需Editor检查，XML静态语法不代替运行画面。

原候选、合并和图标批次证据分别保留在[重构记录](archive/RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md)、[集成记录](archive/RUNTIME_DEBUG_HUB_MERGE_20261009.md)和[图标记录](archive/DEBUG_HUB_ICON_PANEL_20261009.md)。仅当前执行状态核销未完成验收，不借用旧测试数字。
