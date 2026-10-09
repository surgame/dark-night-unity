# Smart Console 与游戏日志

2026-10-07地面玩法验证发现F10中文缺字警告被插件再次收录，造成无界文本对象生成和两次OOM。正式装配现在关闭插件 `ShowApplicationLogs`，由游戏侧 `SmartConsoleLogBridge` 有界转入Unity日志，并添加运行时中文字体回退；日志等级和YYLogger入口保持。完整根因、内存监控及实际验证见[事故记录](UNITY_MEMORY_INCIDENT_20261007.md)。下文开启原订阅的描述只代表历史版本。

2026-10-07：消息和横幅继续只写入Console，但统一使用Gameplay／Info。`PresentationEvent.Warning`表示玩家需要注意的游戏事件，只选择橙色`#F2B66D`；普通消息和横幅为青蓝色`#83CBEA`。例如“开采声引来了洞穴游荡者。”是正常玩法提醒，不再触发Unity／Smart Console的Warning分类。颜色通过现有富文本显示，不修改YYGC或插件分类配置；寻路诊断等真正运行警告保留原等级。本次编译0错误／0警告，临时Editor预览场景中核验真实HUD入口的三种消息均为Log、Smart Console消息Prefab的实际字形颜色正确、HUD容器隐藏；运行Play探针未完成，不能计作正式游戏Play或Player验收。结果在`artifacts/game-notice-logging-20261007/`，中间探针统一归档到待清理目录。

2026-09-29 后续调整：远征常驻按钮块已移入 **F1 → 远征** 调试页，正式驾驶台改为 E 交互；Smart Console 仍用 F10。[最新入口](archive/RUNTIME_DEBUG_HUB.md)。

2026-09-29 更新：消息和横幅仅写入 YYLogger → Smart Console，不再同时弹出 HUD。连接状态、Session 提示、航程确认／超时、主角操作反馈和改键结果也写入日志；按值变化记录的状态不逐帧刷屏。F10 开关不变。沿用 `SC.Prefs.asset` 的分类色：普通日志灰白，Warning 黄色，Error／Exception 红色，命令蓝色。正式远征操作面板及库存、阶段、操作说明仍是主流程界面，不属于消息日志。后续选择性恢复横幅时，应在现有冻结 `PresentationEvent` 消费入口实施筛选。本批验证见[修复记录](archive/SESSION_INPUT_FEEDBACK.md)。

2026-09-28：Bootstrap 装配 Smart Console 2.4.0 Prefab，开启 `ShowApplicationLogs`，用 **F10** 打开或关闭；避开游戏菜单使用的 Escape。Prefab 保持插件原样，配置写在场景实例上。插件本身由用户导入到 `Game/Assets/Plugins/EdgarDev/Smart Console/`。

控制台打开时借用 YYGC Interaction Session 的高优先级模态租约，阻止游戏键盘操作穿透到人物和菜单；关闭时释放租约。Smart Console 自己的输入仍由插件处理。

## 信息路径

- YYGC 的 `GameCore.Logging.YYLogger` 是现有统一日志入口，支持 General、Runner、Network、Gameplay 通道与 Info、Warning、Error 等级。它最终调用 Unity `Debug.Log*`；Smart Console 通过 `Application.logMessageReceived` 显示这些日志，也显示项目原有的 Unity 日志与异常。没有另建日志系统，也没有修改 YYGC。
- 权威会话的 `SessionFeedback` 继续负责消息、横幅、音效与视觉事件，经 `SessionEventJournal` 投影给各客户端。客户端 `CampHudBehaviour` 消费消息和横幅时写入 YYLogger，保持原 HUD 消息／横幅容器隐藏；存档状态和命令拒绝等本地提示也走同一入口。音效和粒子属于表现事件，不逐项打印日志。
- 远征寻路停滞属于调试诊断。`ExpeditionNavigation` 不再向玩家发送通用警告，而是在服务端通过 YYLogger 的 Gameplay/Warning 记录单位规则键、实体 ID、当前位置、目标、飞行标记及地图提交号。约三分钟后出现时，可据此分辨游荡者与友方设备。
- YYLogger 默认配置在找不到 `Resources/LoggerSettings` 时允许输出；项目目前没有该配置资产。以后若设置了通道或最低级别过滤，Smart Console 只会看到 YYLogger 实际发出的内容。Smart Console 的 `Log` API 不作为第二个业务日志入口。

## 2026-09-28 首批验证边界（历史记录）

静态检查：`git diff --check` 通过；ArchitectureGuard 运行了 556 个源文件和 12 项自检，报告 14 项既有的多类型文件违规，均不在本批修改文件中。报告位于 `artifacts/smart-console/architecture.json`。当前 Editor 自动刷新尚未完成本批源码的重新编译，因此 F10、场景重开、HUD、YYLogger 日志及寻路警告仍需实际 Play 核验。独立 Player 构建与多进程联机未因这次接入自动视为通过。Smart Console Prefab 被正式 Bootstrap 场景引用，后续发布打包前需决定是否对正式非开发构建开放命令输入。
