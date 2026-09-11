# 权威规则核心迁移记录

2026-09-11。正式 `Core/Logic` 已实现经济、施工、训练、单位 AI、攻击前摇、箭矢、夜袭、胜负与可恢复随机数；`Core/Save` 和 `Runtime/Save` 已实现冻结旧档、严格 JSON 映射及临时世界恢复。这些实现已在独立 .NET 进程和 Unity Editor 检查，尚未接入可游玩的正式场景、YYGC 会话或正式联机。

## 输入与实际边界

- 规则移植来源为 `projects` 提交 `91cb09ff0894f26134f07fd544f1273d9fe7ffaa` 的 `src/Simulation`、纯存档模型及校验。原目录只读；现有 balance／波次 JSON 保持不变。
- 旧档、旧档继续 20 秒、初始布局和内容摘要复制到 `tools/CoreRegression/Fixtures`，字节与来源一致。正常策略与无人照料结果来自原 `artifacts/gameplay-validation.json`，没有以 Unity 输出重写期望值。
- `GameSession(catalog, layout)` 要求显式的不可变 `LevelLayout`，不再从仅有波次的 JSON 推断零坐标布局。正式 `Pinewatch.unity` 已保存边界和 16 个有序标记，View 导出器在创建世界前执行结构、内容和占地校验；生产启动仍未装配会话，也不读取测试夹具。
- Core 不引用引擎、JSON、文件系统或 YYGC。Runtime 的 JSON 映射逐字段调用不可变构造函数，不依赖反射创建存档类型。测试程序集不进入 Player。

## 规则与操作合同

处理顺序保持经济 → 建筑 → 单位 → 工位 → 箭矢 → 夜袭。`Advance(1/60)` 内部只乘一次 Speed，2× 保留 `2/60` 步长；暂停不推进规则。Unity 的实际调度器、网络心跳和服务端 tick 尚未接入。

`Construction.Place(kind, x, actorIds)`、`Training.Start(kind, actorIds)`、`Orders.Issue(actorIds, targetId, x)` 和 `Camp.Repair(buildingId)` 使用明确参数，GameSession 不再保存选择、悬停、建造预览或镜头。命令解析先拒绝超大列表、重复 ID、敌军和失效居民，再修改世界；无效目标及非有限坐标不支付。建造仍保留原有最近可用工人自动派工规则。

这里的参数校验不等于联机授权。可信连接身份、HostOnly／SharedCamp、业务序号去重、epoch、策略版本和 Ready 都仍归后续 Runtime 会话入口实现；Host 必须走同一入口。Core 命令按调用次序执行，尚未建立网络队列。

## 随机与浮点

算法标识为 `godot-pcg32-clz-f32-v1`。只实现实际使用的 PCG32、播种、整数闭区间和 float 范围采样；数学适配仅有移动、格点吸附和箭矢坐标。算法核对使用 Godot 的 [RandomPCG](https://github.com/godotengine/godot/blob/4.6/core/math/random_pcg.h)、[范围映射](https://github.com/godotengine/godot/blob/4.6/core/math/random_pcg.cpp)及 [PCG32](https://github.com/godotengine/godot/blob/4.6/thirdparty/misc/pcg.cpp)；这些源码链接是算法参考，实际兼容版本由下述运行结果确认。

独立探针运行本机 `4.7.2.stable.mono.official.ed1daf0bf`，完整引擎 hash 保存在 `godot-rng-vectors.json`。种子为 90127、全 1 位、最高位 1 和 0；每种种子采样 128 次整数与浮点交错调用，包含完整 Int32 区间，保存初始状态及每一步状态。探针只在隔离目录运行，不加载原游戏或玩家存档。复核探针源在 `tools/CoreRegression/GodotProbe`，日常回归只读取已冻结向量。

首次独立 .NET 回归通过，但 Unity Mono 在种子 90127 第 3 次浮点采样出现一位差异；整数和 RNG 状态一致。原因是 Mono 保留更宽的浮点中间值。现已在整数转 float、缩放与范围映射各步通过无分配位往返固定 binary32 舍入，保留原算法和调用顺序。不能用放大误差容限替代此修正。

## 旧档与所有权

存档快照的所有属性只读；实体、训练及箭矢集合在构造时复制，嵌套记录也不可变。模拟继续运行不会改写已捕获数据。`LegacySnapshotJson` 是明确的 v1 入口，拒绝缺字段、null、类型错误、数值字符串、非有限数、非法枚举、重复键、尾随内容、超过 4,000,000 UTF-8 字节或 32 层深度的输入。Core 再检查实体数量、唯一身份、工位／施工／训练双向关系、目标阵营、波次与箭矢计时。

`SnapshotMapper.Restore` 验证输入后返回一个新建的完整会话，不修改调用方持有的活动世界。只有未来的会话协调器可以在成功后原子替换它。旧 seed/state 仍是有符号十进制字符串的 64 位位表示；不经过浮点转换。

旧相机／选择只通过单独的 `LegacyDisplayState` 提供给导入方，恢复不会写入 GameSession，也不覆盖其他客户端。Unity 新存档格式、规则摘要封装、原子文件写入、UI 和加载 epoch 尚未实施，不能把现有 v1 JSON 编解码宣称为完整存档产品入口。

## 验证与复跑

独立回归 1102 项：70 项规则／旧档／输入检查，以及 1032 项随机播种、精确值、状态恢复与等边界检查。固定三夜策略仍在约 377.6667 秒胜利，击败 34 个敌人，9 名幸存者、3 名损失，全部库存与酒馆 HP 对照一致；无人照料约 452.1 秒失败并击杀 6 个敌人。旧档恢复及继续 20 秒按每个字段比较，绝对数值容限 0.0001；随机向量要求 float 和整数精确一致、64 位状态逐位一致。

```powershell
dotnet build tools/CoreBuild/CoreBuild.csproj
dotnet run --project tools/CoreRegression -- .
dotnet run --project tools/ArchitectureGuard -- .
unity command run_tests --mode editor --filter DarkNights.Tests --filter_type assembly --project-path Game --detach
```

`CoreBuild` 独立编译实际 Core 为 C#9／netstandard2.1。`CoreRegression` 使用 .NET 8 作为测试宿主，引用上述 Core 产物、实际 Runtime JSON 源码及锁定 Unity 包中的 Newtonsoft DLL；首次运行需按仓库依赖准备流程完成 Unity 导入。它不证明 IL2CPP 通过。

Editor 首次 28 项中 27 项通过、随机向量失败；修正后重跑受 RNG 影响的 6 组核心检查，结果单独保存，之前通过的 22 项环境／配置检查复用。本轮未运行新的 Mono／IL2CPP Player 构建、独立联机进程、美术或双机器验收。详细结果及输入摘要见 [核心迁移证据](evidence/core-migration-2026-09-11.json)。

后续依赖：M0 正式定义、Prefab 与生成访问收口；M1 新存档／文件适配；随后才是 M2 正式网络切片。正式场景布局来源已在后续批次完成并通过 Editor 对照，但 M3–M5 的完整表现、会话恢复、性能及交付仍待实施。

## 第三方算法来源

PCG32 算法来自 M. E. O'Neill / pcg-random.org（2014），参考实现使用 Apache License 2.0；Godot RandomPCG 的浮点映射为 Godot Engine contributors / Juan Linietsky、Ariel Manzur，使用 MIT License。对应许可和来源见 [算法许可说明](third-party/PCG-NOTICES.md)。本项目只保留玩法需要的最小实现，未复制 Godot 数学库。
