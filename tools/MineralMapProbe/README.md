# 原生双层矿图探针

`Program.cs`只读编译锁定AnyRules原生内核及协议，检查两层目录、业务状态、显式空格、区域代次和重进后的采空状态。输入来自 `.deps/YYGC-grid-business`，不修改依赖。

```powershell
dotnet run --project tools/MineralMapProbe/MineralMapProbe.csproj -- artifacts/mineral-map-migration-20261005/protocol-probe.json
```

首次8/8结果保留在上述证据路径。`.cs.fixture`是P0开发构建的显式双层FishNet入口备份，不编入正式游戏；旧P0 Player的9/9只证明传输接线前置。`test_transport.py`须配合含该入口的旧探针Player，不能针对当前正式Player运行或作为正式采集验收。

正式Player使用 `tools/embedded-minerals/test_network.py`，当前Editor回归与玩法见[矿层实现记录](../../docs/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)。工具bin／obj是可重建中间产物，结束后按AGENTS.md归档，不提交。
