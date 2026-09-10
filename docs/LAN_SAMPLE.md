# LAN 合作联机 Sample

这是独立验证模板，不代表灰松谷玩法或 M2–M5 已完成。代码、原生 Prefab 和场景位于 `Game/Assets/Samples/LanCoop`，正式 Bootstrap 和构建场景列表不接入它。手动打开 `Content/LanCoop.unity`，点击 Host 或输入房主局域网 IP 后 Join。默认 UDP 17877，2–4 人共享一个营地；同机测试输入 `127.0.0.1`。初始暂停，网络、Ready、购买和工位操作继续运行。

本轮状态（2026-09-11）：Unity 编译、原生资产编辑往返、Windows Player 构建、四进程基础和真实 UDP 弱网测试均通过；完整证据见下方。

## 运行与可重现依赖

Editor 锁定 `6000.4.9f1`；样板手写代码兼容 C# 9，Core 不引用引擎，Player 使用 .NET Standard 2.1 / Mono。YYGC 使用提交 `10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4` 的隔离 checkout。UPM manifest/lock 指向仓库 `.deps/YYGC`，先执行：

```powershell
pwsh -NoProfile -File tools/prepare-lan-sample.ps1 -FrameworkPath D:/Developer/YYGC
```

脚本只从框架仓库读取 Git 数据，不改变其工作区、暂存或提交。独立 checkout 的 `SampleAssemblyAccess.cs` 为行为生成器授予本 Sample Runtime 的内部访问权，补丁源保存在 `tools/lan-framework-patch`。原因是本次 Unity 编译实际报出生成的 UpdateDispatcher 不能访问 `CoreBehaviour._updateFlags`（CS1061）；框架已对自带 MinimalNetwork 使用同样的友元程序集机制。遇到依赖或补丁内容不符时脚本失败，不覆盖本地修改。此补丁尚未上游合并，不能把 UPM lock 单独当作完整的可重现输入。

该 YYGC 提交还要求 VitalRouter 2.7.1 的 wait-all 修正。仓库中的运行 DLL 已按框架自带工具从固定源码构建，15 项完成／取消／异常／池生命周期回归通过；来源与 SHA-256 见 `docs/evidence/lan-sample-dependencies.json`。如果 NuGet 恢复将其替换为原版，应关闭 Unity 后执行 `.deps/YYGC/Tools/NetworkValidation~/Apply-VitalRouterFix.ps1 -ProjectPath Game`；工具校验原版哈希并在 Library 备份原 DLL，遇到未知二进制会拒绝覆盖。保留原有生成器，不能同时导入两个 VitalRouter DLL。

Unity 菜单 `Dark Nights/Samples/LAN/Build Windows Player` 只构建 Sample 场景，产物为 `artifacts/lan-sample/player/LanCoop.exe`。它不修改正式场景列表。已有 Content 可直接构建；`Create Initial Assets` 仅允许首次向空 Content 输出，已有资产时明确拒绝。

```powershell
dotnet run --project tools/LanSampleRules/LanSampleRules.csproj
pwsh -NoProfile -File tools/test-lan-sample.ps1
pwsh -NoProfile -File tools/test-lan-sample.ps1 -WeakNetwork -Port 17977
```

自动化打开独立 Host 和三个客户端，结果写入新的 `artifacts/lan-sample/run-*`。测试脚本负责断言，Player 仅报告观察值；任一进程错误、超时或意外退出都失败。`-sample-report` 显式启用本机文件控制，仅用于 Sample，不读取玩家存档。普通手动启动没有文件控制。

## 最少的框架用法

| 能力 | 本 Sample 的用途 |
|---|---|
| ObjectDefinition / ObjectInstance | 会话定义装配 CampBehaviour；工位 Prefab 是本地 Object 对象 |
| ObjectView | 原生工位 Prefab 的表现绑定根，场景未 Play 时即可看到；不保存金币或占用 |
| StatefulBehaviour / StateSynchronizer | 一个可靠完整投影；首次快照、后续变化及晚加入沿用框架 RPC |
| R3 | 订阅 ReactiveState，立即复制为不可变 CampReplica；断开时释放订阅 |
| Gateway / Sender / Processor | Host、远端共用可信上下文、ServerAuthoritative 和类型序列化链 |
| DefinitionNetworkAuthenticator | 连接前比较内容版本、定义目录和类型表；尚不是 Steam 账号认证 |
| MemoryPack / YYGC 类型注册 | 标量 DTO 的编解码与池；固定 Sample ID 29101 / 29102 |
| YYGC BehaviourUpdateManager | 复用对象生命周期运行器，独立样板显式装配，不建立第二个 DI 容器 |

调用栈刻意保持短：`SampleNetwork.Send → Gateway → Sender/Processor → SampleAuthority.Handle → CampSession.Apply`。`SampleAuthority.Handle` 是唯一业务订阅，拿到连接身份后同步调用普通 C# 方法；回执只给请求者，展示世界始终来自投影。新建网络栈、预测结算、每实体 NetworkTransform 均无必要。

VitalRouter **在当前 YYGC 命令链里是必要依赖**：INetworkCommand 继承 ICommand，Processor 发布到框架 Router。为了避免复制 Gateway，Sample 保留一个显式 `SubscribeAwait` 适配，不引入 `[Routes]`、业务路由树、过滤器链或跨多个 handler 的规则执行。后续模块只有出现具体多播／中间件需求、能展示调试收益时才新增 VitalRouter 用法。R3 同样只用于状态观察及订阅生命周期，不将经济规则改写为响应式流水线。

## 模板约束

- Core、Runtime、Presentation、Bootstrap 各一个程序集，Editor 单独隔离。正式游戏程序集不引用 Sample；删除整个 `Assets/Samples/LanCoop` 即移除模板代码与场景。共享 YYGC 依赖版本应按正式工程需求另行调整。
- `Assets/FishNet.Config.XML` 将 Sample 从默认 Prefab 自动收集目录排除，避免正式 Bootstrap 间接携带样板；样板使用独立 SpawnablePrefabs。若完整移除样板，可同时移除这一排除项。
- Sample 只以独立场景运行，不与正式 AppStartup 或另一个 NetworkManager 叠加加载。运行时目录和路由是框架全局资源，不能宣称本模板支持热插拔到活动对局。这里“可插拔”指独立导入、单独构建、完整删除及复用源码。
- 10 初始金币、购买费用 10、EntityId=1 的单工位是测试夹具；不接触正式 balance、素材、布局、波次和攻击时机。初始暂停是验证首次状态时便于观察的选择。
- CampSession 只有权威端创建。冻结 CampReplica 是所有界面读取源；池化 CampState 从不跨 await 或被 UI 长期保存。
- 身份由 FishNet 连接取得，服务端为每次加入分配新代次；重连恢复共享世界，不恢复私人身份／私有角色。断线释放工位，记录清理。世界重置增加 epoch 并重新 Ready。
- 每连接保留一个最高请求序号，重复及更旧请求返回 `DuplicateOrExpired`，不会再次扣费；不承诺重发获得原始成功回执。此简化适合有可靠完整投影的模板，正式业务若需原结果重取，应补有界结果缓存。
- Host 权限使用框架构造的 `IsHostInput`，玩家传来的 SenderObjectId 只用于本地选择入口，服务器覆盖它。命令是固定大小标量；每连接每秒最多 60 个请求进入规则。
- 模拟 60 Hz、可靠投影 10 Hz 起点，暂停不修改 Time.timeScale。这里只是一个小状态，尚无全关卡带宽或性能结论；不提前拆包／拆流。
- UI 是刻意局限在 Sample 的 IMGUI 调试面板；正式 UI 继续原有 YYGC UGUI。此面板没有建造／拖拽等多工具输入争用；增加这些功能时复用 Interaction Sessions。
- 原生 Prefab、场景和 Definition 由人类维护。首次创建后生成器不能覆盖，构建只读取；C# 保持中文职责注释、每类型一个主体、每文件最多 300 行。MemoryPack、StateData、NetworkCommand 的 partial 由已锁定包生成器在编译时生成，输入是带属性的两个 DTO；不手改 Library/Bee 生成结果。

## LAN 与后续 Steam

当前使用 FishNet 默认 Tugboat（LiteNetLib），支持可靠和不可靠消息。LAN 的本质是房主监听、客户端连接可达的 IP/端口；这里不包含自动房间发现、NAT 穿透或公网中继。[Tugboat 官方说明](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/tugboat)

后续 Steam 在连接入口替换／配置平台 transport，并补 Lobby、邀请、Steam 身份及账号校验。FishNet 列出 FishySteamworks、FishyFacepunch 等 transport，但不能由此推断项目已经具备 Steam 房间功能。共享营地命令和完整投影应保持不变；只有账号到连接映射、入口与退出路径变化。暂不引入多 transport 管理层或房主迁移。[FishNet transport 列表](https://fish-networking.gitbook.io/docs/guides/high-level-overview/transports)

双机器时，房主窗口选择 Host，其他电脑连接其私网地址；系统若提示防火墙，由操作者选择是否允许专用网络。工具不会擅自修改防火墙规则。

## 验证证据与边界

| 已执行 | 实际结果／证据 |
|---|---|
| 纯规则 | 25 条固定预期断言通过，直接编译真实 Core 源码 |
| VitalRouter 完成语义 | 固定源码和补丁构建，15 项框架回归通过 |
| Unity／资源 | 编译通过；场景重开、ObjectInstance/ObjectView 引用及 Prefab 副本编辑／保存／重开通过 |
| Windows Mono Player | 实际构建成功；图形窗口的面板、工位及 Ready 状态已检查 |
| Host＋3 客户端基础 | 30 项断言通过；[冻结结果](evidence/lan-sample-baseline.json) |
| Host＋3 客户端弱网 | 30 项断言通过；实际接收 627 包、丢弃 33 包、重排 52 次；[冻结结果](evidence/lan-sample-weak-network.json) |
| 依赖与构建来源 | [提交、DLL、补丁和日志 SHA-256](evidence/lan-sample-dependencies.json) |

两组网络测试均覆盖首次 null 后状态、非法实体／伪造 SenderObjectId、非 Host 权限、协议版本、旧 epoch、并发资源扣款、重复请求、工位独占、四人晚加入、断开／重连、暂停及运行、加载式 epoch 重置、Host 退出和同进程重开。最终报告可能记录重开后的世界；各阶段成功由脚本按当时状态判断，未由最终状态倒推。第一次基础测试因脚本变量作用域错误失败，前两次弱网因 Windows UDP ICMP 导致中继退出失败，均已修正后完整重跑；没有将这些失败记录为成功。

本轮使用之前曾重命名的 Game 宿主，旧 Bee 缓存仍含 DNights 路径，首次构建失败；缓存被移动到忽略目录备份后重建成功。这是本机缓存处理，不是可重现依赖要求。Sample 构建临时跳过正式 Addressables 内容生成并恢复设置，正式 Bootstrap／Addressables 的当前新依赖组合未额外重新验收。

弱网脚本以独立 UDP 中继在真实 transport 外施加双向约 50ms 单程延迟、±25ms 抖动和 5% 随机丢包，并记录实际丢弃／重排计数。它影响可靠通道底层数据包，不把 FishNet 仅针对不可靠消息的丢包模拟当作可靠性验收。[TransportManager 官方说明](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/components/managers/transportmanager)

尚未验证：两台物理机器 LAN、Steam、IL2CPP/AOT、长时满载、正式玩法迁移、生产存档恢复及同进程多场景热切换。Sample 不作为正式联机全部验收通过的替代证据。
