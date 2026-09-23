# 会话展示副本与时钟

2026-09-13 当前实现：SessionProjector 从 ObjectSession／YYGC 业务 State 冻结展示副本，Host 与远端复用同一应用路径；不再读取旧 GameSession。U5 的冻结、乱序、epoch、插值、时钟与真实序列化检查通过，见[当前架构](../ARCHITECTURE.md)和[回归迁移](YYGC_UNIFIED_TEST_COVERAGE.md)。下文为早期批次的实现与验收记录。

2026-09-12 C 重构已实施，按用户要求收尾并先交付架构验收。Core 1361、最终 Editor 95、Play 生命周期 20、Mono 启动 6 和双进程 13 项通过；容量检查未签署通过，Ready 修复后的完整弱网矩阵及性能对比留待下次。实际合同、此前通过记录和待办见[C 实施记录](C_REFACTOR_IMPLEMENTATION.md)。M5 既有待验收项保持，下文历史批次的当时边界保留。

2026-09-11，属于 [M5 连续执行路线](M5_EXECUTION.md)的 A1。代码已实现并通过独立及 Unity Editor 回归；没有新增网络 wire、Entry／Update 装配或可操作视图。当前正式网络仍只有 M0 元数据探针，不能将本页的内存投影写成网络已接通。

## 数据与状态归属

`SessionAuthority.CaptureProjection()` 限创建线程，在同一调用内读取唯一 GameSession 并通过 Runtime/Network 的 SessionProjector 建立 Core/ViewData 的不可变 SessionViewData。不通过 CaptureWorld，不生成完整存档，不读取或推进 RNG。Core 的经济、伤害、攻击时机与实体生命周期没有修改。

| 展示数据 | 内容 |
|---|---|
| SessionViewData | 发布序号、epoch、revision、serverTick、策略版本／HostOnly、人数／Ready 数、Loading、暂停、倍速、模拟时间和完整 WorldViewData |
| CampViewData | 库存、人口／容量、招募冷却、当前波次／昼夜／倒计时、敌人数、胜负、击杀／损失／累计采集 |
| ActorViewData | 稳定 ID、内容类别、名字、敌我、X、HP、任务／目标、朝向、Walking、动作时间／前摇／受击闪烁 |
| BuildingViewData | ID、类别、位置、HP、施工、占用者／农田关联、受击闪烁和有序冻结训练列表 |
| WorksiteViewData | ID、类别、位置、占用者、剩余数量、生产进度、外观变体、农田关联 |
| ProjectileViewData | epoch 内稳定 ViewId、起终点、Age／Duration；不含伤害，不执行命中 |

各记录为 sealed 类且只有 getter；列表复制到只读容器，训练项同样不可变。ResourceAmounts 本身为不可变值，可安全共享。既有 Activity／WavePhase／Mode 使用权威枚举名称的字符串展示，View 不必引用 Core/Logic；这些名称还不是冻结的网络编码合同。最大 HP、成本等仍从已匹配的只读内容读取，不维护第二份规则。

WorldViewData 在复制前检查合计最多 256 实体、1024 在飞箭矢，以及正数且跨类别唯一的实体 ID；训练列表单建筑最多 256 项，拒绝空元素。上限来自已支持存档的验证边界，不是 transport 字节上限或带宽验收。超过限制明确失败，不截断世界。构造器不是网络不可信输入解析器；后续接收适配还必须在解码／分配边界校验字节数、字段长度、有限数值、内容 ID、训练关系及协议／内容握手。

SessionProjector 用权威 Projectile 对象的引用身份分配递增 ViewId。已消失的引用每次采集移除；数组头部箭矢命中不会改变后续箭矢身份。新 epoch 清空身份表并从头分配，客户端绑定必须使用 `(epoch, ViewId)`。这份附属表不改变 Core ID、存档或伤害。加载提交／Dispose 会清理保留的箭矢引用。晚加入只取得仍在飞的轨迹，不补放历史箭矢。

## 发布与应用

Publication 是每次成功采集递增的 long，会话内跨加载持续增加；它不同于业务 Revision 和未来 YYGC 的 Sequence。连接变化、Ready 确认或加载取消可能在相同 tick／revision 发生，新的 Publication 允许 UI 更新这些状态。采集不会改变服务端 Ready、命令 revision 或模拟 tick。

WorldReplica 是 Host 与客户端共用的串行应用器，只持有一帧。可信连接装配调用 BeginConnection 获得本地递增代次；所有异步回调携带当时的代次。旧连接回调和旧断开通知不能污染新连接；EndConnection 清理副本。该代次是本地回调生命周期标识，不代替服务端 ConnectionGeneration 或恢复凭据。

同连接只接受更大的 Publication；拒绝旧 epoch、serverTick／PolicyRevision 回退，以及同 epoch 的 revision／模拟时间回退。新的 epoch 可以将 revision 和模拟时间复位，整个 WorldViewData 原子替换；旧首次快照或 Host 回环无法回退画面。HasApplied 只检查实际应用过的同 epoch revision，用于回执先到时等待世界；它不证明资源加载完成，不自动调用服务端 AcknowledgeReady。

## 时钟

SessionClock 接收未缩放真实秒数，以 60 Hz 调用已有 SessionAuthority.Tick。默认每次最多追 8 步，保留余量，可用 Advance(0) 继续追帧；返回本次执行点的有序回执列表。1×／2×仅在 Core 应用一次，暂停及 Loading 仍推进会话 tick；关闭后清理积压并停止。

负数、NaN、Infinity 和超过 86400 秒总积压的输入明确抛错并保留原累积值。这个防御上限避免极端 double 积压下单步减法失去精度，不以截断 delta 隐藏停顿；Entry 后续需把异常转成明确会话故障。创建和推进必须在权威所属线程，不从后台网络回调或文件任务直接驱动。

尚无 Unity Update／未缩放时间接线，没有 10 Hz 发布器或插值。本批验证了帧率无关的时间累计，未测量 Player 帧时、可靠队列或序列化分配。

## 验证

独立回归 **1316/1316**，比上一批新增 **56 项**；架构守卫 **123 个手写文件／10 项自测／0 错误**。一次 Unity `6000.4.9f1` 主动批量编译完成且无错误，完整 Editor 程序集 **40/40**。新增 session-projection、session-replica、session-clock 三组共享实际源码测试。

验证包括初始全部实体／HUD、12 秒采集、训练嵌套冻结、集合写入拒绝、数量边界、箭矢真实载入及命中后的身份、Host 重复／晚初始帧／回执先到、加载取消同版本更新、加载新 epoch、旧连接回调、30／60／144 FPS 下十秒完整世界与固定 60 Hz 一致、追帧保留、暂停／倍速／加载与跨线程输入。

没有 Player、PlayMode、MemoryPack 往返、YYGC 状态池、独立进程或美术验收。测试中的 JSON 仅比较对象值，不代表网络序列化或字节测量。冻结规则／夹具、正式资源、ProjectSettings、包和 Sample 未变。证据：[A1 展示与时钟](evidence/session-projection-2026-09-11.json)。下一执行点为 A2 的可靠 wire、握手及实际会话装配。
