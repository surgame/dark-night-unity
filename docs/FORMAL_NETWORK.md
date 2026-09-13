# 正式会话网络接线

2026-09-13 当前入口已接完整 YYGC ObjectSession，使用协议 6／v2；场景对象按放置键接管原实例，动态对象经同步准备工厂创建。U4、U5 已完成正式接线与集成回归；U6 最终同一 Mono 产物通过完整多进程功能矩阵，包含九组弱网各 24/24，性能仍未签署，见[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)。下文按日期保留旧接线记录，旧 Core 世界和协议 5 不再作为当前合同。

2026-09-12 C 重构已实施，按用户要求收尾并先交付架构验收。Core 1361、最终 Editor 95、Play 生命周期 20、Mono 启动 6 和双进程 13 项通过；容量检查未签署通过，Ready 修复后的完整弱网矩阵及性能对比留待下次。实际合同、此前通过记录和待办见[C 实施记录](C_REFACTOR_IMPLEMENTATION.md)。M5 既有待验收项保持，下文历史批次的当时边界保留。

2026-09-11，M5 执行路线 A2 已接通，A3 可操作画面仍待完成。正式 Mono Host＋独立客户端通过 12 项实际规则／权限／完整投影检查，不能据此标为 M2 全部完成。

GameSessionStartupModule 在既有 AppStartup 中加载正式 Pinewatch 场景并从布局标记导出 LevelLayout，登记 SessionNetwork。单人也使用同一 Host 连接。WorldSession 和新增 PlayerConnection 均通过 GuidFirst／GuidV2 定义工厂与 Addressables 创建，正式代码不引用 Sample。已有 WorldSession Prefab 仅定向添加生命周期链接；Bootstrap 没有改写。

DefinitionNetworkAuthenticator 复用固定格式握手，其内容版本加入规则与布局摘要，框架继续核验目录和注册表。游戏会话协议升级为 2；旧 Ready tag 0 保留，新增 SessionCommand tag 1，状态 tag 0 保留。具体 MemoryPack wire 由类型生成器处理，命令注册源由 YYGC 生成菜单更新，不能手改生成结果。

SessionCommand 走 Gateway／owned Sender／Processor，SessionServer 仅接受服务端上下文映射的当前连接，按每秒60请求预算和原业务队列处理；异常频率断开该连接。Host 槽位只由服务端本地连接判断，远端不能传入槽位。TargetRpc 仅向 owned endpoint 返回确认，不执行业务。Ready 需匹配最近128份实际发布记录中的 publication／epoch／revision，再调用权威层 Ready；客户端会在未确认时重试。连接等待 Ready 超时30秒，加载与重连的产品生命周期仍待 M4 扩展验证。

每份完整投影先编码成只写一次的 MemoryPack 字节，再交给现有可靠 StatefulBehaviour／StateSynchronizer。发布复制调用者字节数组，订阅内立即解码、校验并冻结为 WorldReplica；异步表现不保留池对象。默认以60Hz服务tick、10Hz投影启动；有业务或连接结果时可立即发布。投影字段校验包含目录类别、有限值、数量、训练引用及实体身份，超限失败，不截断集合。

编码器上限262144字节；YYGC状态帧上限增加1024字节封装余量，业务命令上限8192字节。初始投影1363字节；256实体／1024箭矢／每名字256汉字的边界编码260570字节。10Hz向3客户端发送该极端载荷，仅净载荷估算约7.82MB/s，尚未做真实边界吞吐验收；不能把上限当作性能目标。正常初始净载荷同口径为40890B/s。后续扩展必须测实际正常三夜高峰与大载荷可靠队列，再决定分块或频率调整。

`tools/test-game-session.ps1` 复用已构建的正式 Player，创建两个独立进程和独立临时命令／报告目录，操作经 SessionClient 与网络进入真实权威服务。首次检查覆盖双方Ready与完整初始实体、暂停、共享建造一次支付、来宾时间拒绝、HostOnly切换、明确实体移动及暂停后完整世界一致。新增 SessionAutomation 只有显式 `--dn-role` 参数时启用；不访问玩家存档。该驱动可继续扩展四人／恢复场景，不能用它替代 UI 操作和画面验收。

独立规则回归1316项通过；架构守卫146手写文件／10自测／0错误。Editor首轮43项中42项通过，唯一失败是测试误用未启动AppStartup的全局注册表；调整为生成源检查后重跑受影响3项均通过，其余通过项复用。Mono构建一次，双进程12项通过，日志没有实际异常行。没有运行IL2CPP、四进程、弱网、重连或画面检查。证据见[正式网络记录](evidence/formal-network-2026-09-11.json)。
