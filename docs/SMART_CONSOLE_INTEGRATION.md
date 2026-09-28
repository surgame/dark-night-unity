# Smart Console 与游戏日志

2026-09-28：Bootstrap 装配 Smart Console 2.4.0 Prefab，开启 `ShowApplicationLogs`，用 **F10** 打开或关闭；避开游戏菜单使用的 Escape。Prefab 保持插件原样，配置写在场景实例上。插件本身由用户导入到 `Game/Assets/Plugins/EdgarDev/Smart Console/`。

控制台打开时借用 YYGC Interaction Session 的高优先级模态租约，阻止游戏键盘操作穿透到人物和菜单；关闭时释放租约。Smart Console 自己的输入仍由插件处理。

## 信息路径

- YYGC 的 `GameCore.Logging.YYLogger` 是现有统一日志入口，支持 General、Runner、Network、Gameplay 通道与 Info、Warning、Error 等级。它最终调用 Unity `Debug.Log*`；Smart Console 通过 `Application.logMessageReceived` 显示这些日志，也显示项目原有的 Unity 日志与异常。没有另建日志系统，也没有修改 YYGC。
- 权威会话的 `SessionFeedback` 继续负责消息、横幅、音效与视觉事件，经 `SessionEventJournal` 投影给各客户端。客户端 HUD 在 `CampHudBehaviour` 显示消息和横幅时同步写入 YYLogger；存档状态和命令拒绝等本地 HUD 提示也走同一入口。音效和粒子属于表现事件，不逐项打印日志。
- 远征寻路停滞属于调试诊断。`ExpeditionNavigation` 不再向玩家发送通用警告，而是在服务端通过 YYLogger 的 Gameplay/Warning 记录单位规则键、实体 ID、当前位置、目标、飞行标记及地图提交号。约三分钟后出现时，可据此分辨游荡者与友方设备。
- YYLogger 默认配置在找不到 `Resources/LoggerSettings` 时允许输出；项目目前没有该配置资产。以后若设置了通道或最低级别过滤，Smart Console 只会看到 YYLogger 实际发出的内容。Smart Console 的 `Log` API 不作为第二个业务日志入口。

## 验证边界

静态检查：`git diff --check` 通过；ArchitectureGuard 运行了 556 个源文件和 12 项自检，报告 14 项既有的多类型文件违规，均不在本批修改文件中。报告位于 `artifacts/smart-console/architecture.json`。当前 Editor 自动刷新尚未完成本批源码的重新编译，因此 F10、场景重开、HUD、YYLogger 日志及寻路警告仍需实际 Play 核验。独立 Player 构建与多进程联机未因这次接入自动视为通过。Smart Console Prefab 被正式 Bootstrap 场景引用，后续发布打包前需决定是否对正式非开发构建开放命令输入。
