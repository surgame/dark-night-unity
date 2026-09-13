# Core regression

从仓库根目录运行：

```powershell
dotnet run --project tools/CoreRegression -- .
```

U5 起，此 .NET 8 工具只引用真实 C# 9／netstandard2.1 Core、只读配置解析和纯计算／冻结 RNG 断言，不再编入 Runtime/Session 或旧世界。当前 1,043 项通过，报告为 `artifacts/migration/core-regression.json`。Unity 中的 `CoreRegressionTests` 复用相同场景源码。

命令、权限、工位、施工、训练、时钟、投影、v2 存档和文件原子性已迁到 `SessionRegressionTests`，由 `UnifiedSessionScope` 预加载真实正式 Prefab、装配 YYGC 上下文并统一释放。完整三夜由 `UnifiedCampaignTests` 对照冻结 Godot 报告，装配／池化及真实 Play 分别由对应 Unity 测试和独立 Player 验收。这些检查必须在 Unity 执行；本工具通过不等于对象、联机或 Player 通过。

`Fixtures` 保留原 Godot 的只读证据：布局／内容、完整防守和无人照料三夜、RNG 向量以及旧档。旧档导入成功不再是产品要求；旧格式用于明确拒绝测试，不能恢复旧读取分支。不得从当前实现重生成任何旧期望。来源提交和哈希见 `docs/evidence/core-migration-2026-09-11.json`。

`godot-rng-vectors.json` 来自独立 Godot 4.7.2 进程。复查研究时可将 `GodotProbe` 复制到指定空产物目录运行，普通回归不启动原引擎，不改写冻结向量。实际运行也不依赖 `../projects` 或参考工程。

完整迁移、覆盖映射和证据见 `docs/YYGC_UNIFIED_TEST_COVERAGE.md` 与 `docs/YYGC_UNIFIED_IMPLEMENTATION.md`。
