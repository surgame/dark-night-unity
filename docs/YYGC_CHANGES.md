# YYGC 修改授权与改动账本

2026-09-12 用户授权：必要时可以更新 YYGC，并将这项约定加入项目知识；完成后必须一一列出 YYGC 改动。此授权允许为实际接入缺口修正框架，保留用户已有修改、隔离验证和锁定依赖的要求仍有效。`AGENTS.md` 已同步该约定。

| 本次变更 | 具体证据与原因 | 修改落点 | 当前验证 |
|---|---|---|---|
| 池化重入时重新登记本地／网络单例 Instance | 关闭 Domain Reload 再进入 Play，`CampInput.Initialize` 因 Interaction Sessions 服务为空而使正式会话模块失败；单例离场清空 Instance，但池化重入跳过首次 Initialize | 新增 `RestoreSingletonOnPooledReentry.patch`，将单例引用登记移至每次执行的 InitializeCore，首次 OnSingletonInitialize 仍只调用一次；同时覆盖两种单例基类，并接入准备脚本 | Editor 55/55；常规重载三次会话 9/9，关闭 Domain Reload 的连续两次 Play 各三次会话 10/10；协议 5 Mono 九组四进程恢复均 22/22 |
| 修正 UGUI 根组件的 Unity 空引用判断 | 空场景启动实际抛出 `MissingComponentException: Canvas`；`GetComponent<T>() ?? AddComponent<T>()` 未识别 Unity 的空组件包装对象 | 新增可复现补丁 `tools/lan-framework-patch/FixUguiRootUnityNull.patch`，修改隔离依赖 `UGUIRuntimeStartupModule` 中 Manager、Canvas、CanvasScaler 的三个判断；准备脚本验证并应用补丁，用户 YYGC 仓库未改动 | 修复后 Editor 实际创建五个正式面板和一个菜单 Interaction Session；字体及后续生命周期另行验证 |
| 为 `DarkNights.View` 增加 `InternalsVisibleTo` | UGUI Behaviour 的 YYGC 生成更新分派器读取 `CoreBehaviour._updateFlags`，Unity 编译报 CS1061；既有 Runtime／Sample 已有相同授权 | 本仓库 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，经核对旧文件等于已提交基线后更新 `.deps/YYGC/Runtime/NetworkCommands/SampleAssemblyAccess.cs`；用户维护的 YYGC 仓库尚未改动 | 依赖准备脚本通过；Unity 编译通过；原生 UGUI 首版资源创建完成，运行与生命周期验收继续执行 |

后两项已随原生 UI 批次 Mono 实际构建、启动 6/6、独立 Host＋客户端 13/13 和 Editor 鼠标 13/13 验证，见[实际证据](evidence/native-ui-2026-09-12.json)。用户维护的 `D:\Developer\YYGC` 未改动，修正位于可重现补丁与隔离依赖。

已有的 Sample 注册排除、启动验证排除及 Sample／Runtime 友元声明是此前已提交补丁，本次没有改变这些行为。每次后续修复在本表新增独立行；最终交付逐项列出真实改动及各自通过／待验证状态，不把用户原有修改算作本次成果。
