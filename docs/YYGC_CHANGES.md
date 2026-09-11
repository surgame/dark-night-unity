# YYGC 修改授权与改动账本

2026-09-12 用户授权：必要时可以更新 YYGC，并将这项约定加入项目知识；完成后必须一一列出 YYGC 改动。此授权允许为实际接入缺口修正框架，保留用户已有修改、隔离验证和锁定依赖的要求仍有效。`AGENTS.md` 已同步该约定。

| 本次变更 | 具体证据与原因 | 修改落点 | 当前验证 |
|---|---|---|---|
| 池化重入时重新登记本地／网络单例 Instance | 关闭 Domain Reload 再进入 Play，`CampInput.Initialize` 因 Interaction Sessions 服务为空而使正式会话模块失败；单例离场清空 Instance，但池化重入跳过首次 Initialize | 新增 `RestoreSingletonOnPooledReentry.patch`，将单例引用登记移至每次执行的 InitializeCore，首次 OnSingletonInitialize 仍只调用一次；同时覆盖两种单例基类，并接入准备脚本 | Editor 55/55；常规重载三次会话 9/9，关闭 Domain Reload 的连续两次 Play 各三次会话 10/10；协议 5 Mono 九组四进程恢复均 22/22 |
| 修正 UGUI 根组件的 Unity 空引用判断 | 空场景启动实际抛出 `MissingComponentException: Canvas`；`GetComponent<T>() ?? AddComponent<T>()` 未识别 Unity 的空组件包装对象 | 新增可复现补丁 `tools/lan-framework-patch/FixUguiRootUnityNull.patch`，修改隔离依赖 `UGUIRuntimeStartupModule` 中 Manager、Canvas、CanvasScaler 的三个判断；准备脚本验证并应用补丁，用户 YYGC 仓库未改动 | 修复后 Editor 实际创建五个正式面板和一个菜单 Interaction Session；字体及后续生命周期另行验证 |
| 为 `DarkNights.View` 增加 `InternalsVisibleTo` | UGUI Behaviour 的 YYGC 生成更新分派器读取 `CoreBehaviour._updateFlags`，Unity 编译报 CS1061；既有 Runtime／Sample 已有相同授权 | 本仓库 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，经核对旧文件等于已提交基线后更新 `.deps/YYGC/Runtime/NetworkCommands/SampleAssemblyAccess.cs`；用户维护的 YYGC 仓库尚未改动 | 依赖准备脚本通过；Unity 编译通过；原生 UGUI 首版资源创建完成，运行与生命周期验收继续执行 |

后两项已随原生 UI 批次 Mono 实际构建、启动 6/6、独立 Host＋客户端 13/13 和 Editor 鼠标 13/13 验证，见[实际证据](evidence/native-ui-2026-09-12.json)。用户维护的 `D:\Developer\YYGC` 未改动，修正位于可重现补丁与隔离依赖。

<a id="workshop-display"></a>

## 2026-09-12：Workshop 定义展示

原因：工坊平铺和树形均以 `[{Id}]` 开头、仅按旧 ID 排序并搜索，正式 GuidFirst 定义的零值无法区分对象，也不能按 Key 查找。用户确认原有资产文件名必须保留，因此采用名称／Key／文件名三行；名称 13 号加粗、Key 11 号偏蓝灰、文件名 10 号较淡，行高统一 60。长 Key 和文件名按宽度从中间省略，完整值保留在提示／复制和搜索中；有效旧 ID 只作为辅助标记。

落点：先在 `.deps/YYGC` 验证，提交 `516f76c4fe062fa82384f7b91ac46c453abbe80d`，再确认用户 YYGC 工作区干净且 HEAD 仍为 `10b8f0e`，将 `D:\Developer\YYGC` 的 `master` 快进至同一提交。没有推送远端。游戏通过 `tools/prepare-lan-sample.ps1` 锁定该完整提交；manifest／packages-lock 的隔离包路径未改变，既有运行补丁保留并通过精确校验。

以下为该 YYGC 提交的全部 10 个文件，路径相对于 `D:\Developer\YYGC`；隔离依赖包含相同提交。

| 文件 | 原因与修改 | 验证 |
|---|---|---|
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs` | 新增只读名称、身份及原文件名映射；按 Key／GUID／别名搜索，按名称及稳定身份排序 | 缺名称、零旧 ID、长 Key、GUID 与旧号搜索；实际窗口名称顺序通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs` | 新增共用三行视图、有效旧 ID 标记、完整提示与右键复制；复用时重置文件名和身份 | 两种栏宽／两种视图、完整 Key 复制、行复用通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopListPresenter.cs` | 平铺和树形共用三行视图及 60 像素行高；改名时整体隐藏／恢复三行 | 220／400 像素栏宽布局与搜索、F2／取消、文件名保留通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.uss` | 名称、Key、文件名的字号、配色、间距与省略；定义浅色主题对应颜色 | 当前深色 Editor 中实际布局无重叠和越界；浅色主题视觉复核未执行 |
| `Editor/Objects/Definition/Workshop/WorkshopAssetService.cs` | 去除按旧整数排序，使用共用名称比较器 | 实际 29 个定义名称顺序通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.cs` | 接入完整身份／文件名搜索，保存、项目变化和撤销后刷新列表并保持目标选择 | 实际窗口按大写完整 Key 搜索通过；关闭重开后 29 行均含文件名 |
| `Editor/Objects/Definition/Workshop/WorkshopInspectorPresenter.cs` | 基本属性显示可选中复制、自动换行的 Key／GUID；零旧 ID 不显示 | 实际 Worker 属性区绘制及窗口重开通过，Console 无错误 |
| `Documentation~/DEFINITION_IDENTITY.md` | 记录三行展示、长文本、旧号、搜索与身份制作边界 | 与本次实现和用户保留文件名的要求核对 |

验证：Unity `6000.4.9f1` 导入／编译通过，UI 检查 **47/47**；实际窗口搜索、名称排序与重开检查通过；2,457 个游戏资源、meta、Packages 和 ProjectSettings 输入哈希不变。当前依赖准备与全新隔离 clone 准备均通过。首次导入生成两份新 meta；用户提出三行要求后，仅对新增输入涉及的三份源码／样式统一再导入一次。未新增 Player、PlayMode 或联机验证，不改变 M5 状态。详见[本批证据](evidence/workshop-display-2026-09-12.json)。

已有的 Sample 注册排除、启动验证排除及 Sample／Runtime 友元声明是此前已提交补丁，本次没有改变这些行为。每次后续修复在本表新增独立行；最终交付逐项列出真实改动及各自通过／待验证状态，不把用户原有修改算作本次成果。
