# 评估状态与验证边界

日期：2026-09-10。本轮交付的是YYGC静态评估、Unity／合作联机方案及独立Git筹备仓库。

## 第二轮快速复评

新增[YYGC能力复评](YYGC_REASSESSMENT.md)与[冻结证据](evidence/yygc-network-review-2026-09-10.json)。用户补充联机尚未正式生产使用；设计改为优先修正并验证YYGC完整命令/会话状态链，游戏补营地合同，分块和拆流由测量触发。

本轮实际执行：用真实命令生成器DLL对当前/旧命名空间分别运行Roslyn，输出0/1份文件；R3 1.3.0空初值订阅收到[2]，已有初值对照收到[1,2]；MemoryPack 1.21.4具体类型往返得到42，未注册接口调用抛出异常。工具源码和依赖锁文件已保存。它们是隔离生成器和依赖语义观察，未运行Unity、FishNet RPC或完整框架测试；M0–M5仍未完成。

补充盘点了Interaction Sessions、工厂Local/Network分流、会话Behaviour的复用位置。YYGC及Godot保持只读。下文的文档数量、链接数量和全部测试未执行等记录是首轮历史口径；第二轮新增工具的运行范围以本节和复评报告为准。

第二轮验证：依赖锁定恢复及探针运行成功，原始551项素材哈希通过；10组源码/仓库/生成器/素材证据与首轮冻结值一致。复核结束时出现本轮之外的 `DNights/` Unity宿主（ProjectVersion为6000.4.9f1）；只读取版本和目录信息，未修改、暂存或验收该宿主，不能沿用“整个仓库尚无Unity项目”的旧描述。原6.2目标需与实际宿主在M0核对。

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

机器可复核数据见[冻结证据](evidence/assessment-2026-09-10.json)。采集器排除被忽略的生成器bin/obj缓存；目录物理代码量与Player实际程序集体积不同。

## 本轮没有执行

| 项目 | 当前状态／原因 |
|---|---|
| Unity工程导入／Play／Player构建 | 未执行；依赖未锁定，框架声明Editor补丁与已找到安装不一致 |
| YYGC测试与生成器重建 | 未执行；避免触发会回写原框架的构建目标，且缺少可验证宿主依赖 |
| FishNet Host＋独立客户端 | 未执行；没有构建出的Unity宿主 |
| 旧档／完整三夜Unity规则回归 | 未执行；Core尚未迁移 |
| Unity Prefab制作与美术人工流程 | 未执行；文档为设计合同 |
| Unity性能／带宽／弱网测试 | 未执行；频率和缓存值只是待测起点 |
| 当前Godot游戏全套测试 | 未重跑；133游戏检查／27架构自测来自其既有报告 |
| YYGC或Godot代码修改／提交 | 未进行；本轮只在新Unity仓库写入成果 |

## 已区分的事实与推断

StateSynchronizer确实通过OnSpawnServer＋TargetRpc实现了单对象首次快照；“没有初始快照”不是本次结论。需要新建的是世界切点、Ready、epoch和合作业务的一致性流程。

Runtime中的无条件UnityEditor引用、IsExternalInit命名空间、缺少包依赖声明属于已看到的源码事实；Player具体报错和依赖补齐后的行为仍需实测。广播类型路由、初次Owner注册和Domain Reload关闭后的重复filter列为探针，不伪装成已复现故障。

框架README的性能描述未经过本轮基准验证。源码中的byte[]序列化和状态变化广播说明需要测量分配与消息量，不能直接宣称已证明性能不达标。

## 本仓库完成检查

已核验10份Markdown的UTF-8与62个本地链接（含源码行号），没有失效链接或项目用语问题。阶段加总24–40人日，加入25%余量后为30–50人日；证据规模与文档一致。

采集器对相同行数的文件按路径稳定排序，使用默认输出连续重跑两次，10组核心报告数据均与冻结证据完全相同；原素材551项哈希通过。源仓库HEAD与Git差异不变，21项YYGC关键文件和17项Godot证据文件哈希一致。六个生成器DLL及其六份.meta均已记录。

Git忽略规则覆盖Library、Temp、Obj、Logs、artifacts、本机依赖与UserSettings；README、AGENTS、Unity.meta、Packages锁文件、ProjectSettings与冻结证据未被误忽略。实际Unity开发仍从[执行计划M0](DEVELOPMENT.md)开始。

新增实施结果需附实际命令和证据，在此更新状态。不要把计划中的Prefab、API、守卫或测试写成已经存在。
