# 评估状态与验证边界

日期：2026-09-10。本文件保留 YYGC 静态评估与 Unity／合作联机方案的历史边界；M0/M1 执行结果已追加在下方。

## M0/M1 执行增补

在评估后已对 `DNights` 执行 Unity 6000.4.9f1 导入：

- `Packages/manifest.json`、`packages-lock.json` 已锁定本地 YYGC、NuGetForUnity、FishNet 4.7.2、UniTask 2.5.11、R3 1.3.1、MemoryPack 1.21.4、ZLinq 1.5.6 与 Addressables 2.10.1。
- `Assets/NuGet.config`、`Assets/packages.config` 和 `Assets/Packages` 已就位；NuGet 包缓存不再被 Git 忽略，以避免新 checkout 在脚本编译前缺少 R3/MemoryPack 核心 DLL。
- 从本机已有插件补齐 Odin Inspector（含 Addressables 模块）和 DOTween，YYGC 的 Runtime/Editor 程序集均可编译。
- YYGC 自动创建了 6 个空的全局 ScriptableObject 配置资产；`ObjectDefinitionDatabase` 保持空列表，不带入参考工程的对象定义。
- 已创建 `AppStartupSettings`、`GameCore`、`NetworkManager`、`DefaultPrefabObjects`、Addressables 默认组和 `Bootstrap` 首场景；启动场景已放在 Build Settings 第一位。
- 已执行 StateData/NetworkCommand 生成器；当前没有项目专属类型，因此生成的是空但可编译的注册入口。
- 为适配项目已启用的 Input System，YYGC 的 UGUI 启动和两个 Editor 创建器改用 `InputSystemUIInputModule`，Editor asmdef 补充 `Unity.InputSystem` 引用。
- Unity 编译、Addressables 内容构建、Windows Player 构建和最终 Player 冒烟均通过；Player 日志达到 `AppStartup` Ready，且无 Input System 异常。联机、完整玩法、AOT 和真实多进程资源流程仍未验收。

YYGC 原有暂存的 `UGUIManager.cs` 和未跟踪 IDRegistry 备份保持原状；本轮仅新增上述 Input System 兼容改动，仍未提交 YYGC。

## 第二轮快速复评

新增[YYGC能力复评](YYGC_REASSESSMENT.md)与[冻结证据](evidence/yygc-network-review-2026-09-10.json)。用户补充联机尚未正式生产使用；设计改为优先修正并验证YYGC完整命令/会话状态链，游戏补营地合同，分块和拆流由测量触发。

本轮实际执行：用真实命令生成器DLL对当前/旧命名空间分别运行Roslyn，输出0/1份文件；R3 1.3.0空初值订阅收到[2]，已有初值对照收到[1,2]；MemoryPack 1.21.4具体类型往返得到42，未注册接口调用抛出异常。工具源码和依赖锁文件已保存。它们是隔离生成器和依赖语义观察，未运行Unity、FishNet RPC或完整框架测试；M0/M1 环境基线已完成，M2–M5仍未完成。

补充盘点了Interaction Sessions、工厂Local/Network分流、会话Behaviour的复用位置；这些静态复评仍以 YYGC/Godot 只读为口径。随后执行阶段只对 YYGC 加入 Input System 兼容改动，未改 Godot。下文的文档数量、链接数量和全部测试未执行等记录是首轮历史口径；第二轮新增工具的运行范围以本节和复评报告为准。

第二轮验证：依赖锁定恢复及探针运行成功，原始551项素材哈希通过；10组源码/仓库/生成器/素材证据与首轮冻结值一致。随后 M0/M1 已验收 `Game/` 宿主（ProjectVersion 为 6000.4.9f1），因此旧的“只读取、未修改宿主”描述仅适用于 M0 之前的评估阶段。

## 本轮完成

- 创建 `unity-projects`，初始化独立Git `main`分支。
- 读取YYGC启动、对象、DI、视图、UGUI、命令、状态、序列化、存档与测试实现，并对照框架文档。
- 记录YYGC HEAD＋工作区状态；用户的UGUIManager暂存变更与IDRegistry备份保留。
- 统计框架232份C#／41,260物理行（不含SourceGenerators~），并记录六个随包提供的生成器DLL及导入设置哈希。
- 核对Godot基线115份运行C#／5,447行，以及可移植规则和需要重建的表现层。
- 重新核验551项原素材SHA-256，通过；记录balance、waves、Pinewatch布局和旧档夹具哈希。
- 读取本机一套Unity.exe版本，核对Unity6.2官方C#／API profile说明。
- 确认2–4人合作共享营地，形成服务端权限、同步、晚加入、重连、暂停／倍速与保存建议。
- 建立架构、迁移、美术接入、难度／执行计划、AGENTS与人工接手指南。
- 建立 Addressables、AppStartup、FishNet NetworkManager 和 Bootstrap 场景基线；执行两个空类型注册源生成器。
- 在本地 YYGC 中修正 UGUI/Input System 组件和 Editor asmdef，使 Player 启动不再触发 `StandaloneInputModule` 与 active Input System 冲突。

机器可复核数据见[冻结证据](evidence/assessment-2026-09-10.json)。采集器排除被忽略的生成器bin/obj缓存；目录物理代码量与Player实际程序集体积不同。

## 本轮没有执行

| 项目 | 当前状态／原因 |
|---|---|
| Unity工程导入／Play／Player构建 | M0/M1 已完成导入、脚本编译、Addressables 构建、Windows Player 构建和启动冒烟；编辑器 Play、场景功能仍未完整验收 |
| YYGC测试与生成器重建 | StateData/NetworkCommand 注册源已在宿主中生成；YYGC 全套测试与生成器工程重建仍未执行 |
| FishNet Host＋独立客户端 | 未执行；没有构建出的Unity宿主 |
| 旧档／完整三夜Unity规则回归 | 未执行；Core尚未迁移 |
| Unity Prefab制作与美术人工流程 | 未执行；文档为设计合同 |
| Unity性能／带宽／弱网测试 | 未执行；频率和缓存值只是待测起点 |
| 当前Godot游戏全套测试 | 未重跑；133游戏检查／27架构自测来自其既有报告 |
| YYGC或Godot代码修改／提交 | YYGC 仅有本轮 Input System 兼容改动，未提交；Godot 未修改；用户原有 YYGC 工作区改动保持不变 |

## 已区分的事实与推断

StateSynchronizer确实通过OnSpawnServer＋TargetRpc实现了单对象首次快照；“没有初始快照”不是本次结论。需要新建的是世界切点、Ready、epoch和合作业务的一致性流程。

Runtime中的无条件UnityEditor引用、IsExternalInit命名空间、缺少包依赖声明属于已看到的源码事实；Player具体报错和依赖补齐后的行为仍需实测。广播类型路由、初次Owner注册和Domain Reload关闭后的重复filter列为探针，不伪装成已复现故障。

框架README的性能描述未经过本轮基准验证。源码中的byte[]序列化和状态变化广播说明需要测量分配与消息量，不能直接宣称已证明性能不达标。

## 本仓库完成检查

已核验10份Markdown的UTF-8与62个本地链接（含源码行号），没有失效链接或项目用语问题。阶段加总24–40人日，加入25%余量后为30–50人日；证据规模与文档一致。

采集器对相同行数的文件按路径稳定排序，使用默认输出连续重跑两次，10组核心报告数据均与冻结证据完全相同；原素材551项哈希通过。源仓库HEAD与Git差异不变，21项YYGC关键文件和17项Godot证据文件哈希一致。六个生成器DLL及其六份.meta均已记录。

Git忽略规则覆盖 Library、Temp、Obj、Logs、Build、artifacts、本机依赖与 UserSettings；NuGet `Assets/Packages`、README、AGENTS、Unity.meta、Packages 锁文件、ProjectSettings 与冻结证据保持可版本化。实际 Unity 开发从 M1 的环境基线继续；生成注册表目前为空，双进程、序列化、AOT 和玩法对象仍属于后续验收。

新增实施结果需附实际命令和证据，在此更新状态。不要把计划中的Prefab、API、守卫或测试写成已经存在。
