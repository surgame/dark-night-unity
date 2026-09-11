# 人工开发 Quick start

`Game/` 使用 `6000.4.9f1`。依次运行 `pwsh -File tools/prepare-lan-sample.ps1` 与 `pwsh -File tools/prepare-fishnet.ps1` 准备锁定的隔离依赖；核心 NuGet 包在 `Game/Assets/Packages`。正式 `Bootstrap` 仍为原首场景，游戏操作与复跑入口见 [Player 指南](PLAYER_GUIDE.md)。独立模板位于 `Assets/Samples/LanCoop/Content/LanCoop.unity`，只作为 [LAN Sample](LAN_SAMPLE.md) 对照。

正式联机、灰松谷三夜、原生对象／UI 与存档恢复主体已实现；干净 Mono、活跃恢复与本机完整进程矩阵已在 2026-09-12 验收续跑通过。当前继续字体／新增菜单控件校准、完整同状态画面对照及性能采样修正，见 [Mono 验收记录](MONO_ACCEPTANCE.md)与[M5 当前清单](M5_EXECUTION.md#mono-acceptance)。不要重复运行历史环境或资源初始化，已有资产会被保护检查拒绝。

## 先读什么

1. 读[README](../README.md)确认当前状态、范围与联机假设。
2. 读最新[移植方案](MIGRATION_PLAN.md)和[LAN Sample](LAN_SAMPLE.md)，区分已验证路径与正式游戏待补内容；早期[YYGC 能力复评](YYGC_REASSESSMENT.md)用于了解历史修正缘由。
3. 读[技术架构](ARCHITECTURE.md)的程序集表，再读[联机设计](MULTIPLAYER.md)的权限和请求流水线。
4. 按 [M5 当前清单](M5_EXECUTION.md) 继续未验收项；[开发执行计划](DEVELOPMENT.md)保留阶段合同和历史记录，不重新实施已完成的 M0／M1。

## M0/M1 已执行

1. 用 Unity 6000.4.9f1 打开 `DNights`，等待 UPM 解析并生成 `Packages/packages-lock.json`。
2. 通过 NuGetForUnity 配置还原 R3 1.3.1、MemoryPack 1.21.4、VitalRouter 2.7.1、ZLinq 1.5.6 及其 Unity 所需依赖。
3. 导入 FishNet 4.7.2、UniTask 2.5.11、Addressables 2.10.1；补齐 YYGC 所需本机 Odin Inspector/DOTween。
4. 验证 Unity 编译无 CS 错误，YYGC 自动创建全局 ScriptableObject 空配置。
5. 运行 `YY/Dark Nights/Initialize Environment` 等价批处理，创建 AppStartup、Addressables、GameCore、NetworkManager 和 Bootstrap；运行 StateData/NetworkCommand 生成器，生成空注册入口。
6. 用 `-buildWindows64Player` 完成 Bootstrap 首场景的 Windows Player 构建，并启动 Player 冒烟；日志达到 `[AppStartup] Startup completed. Application is ready.`。

上面列出的是历史 M0/M1 操作。当前不要直接改用户 YYGC 工作区；隔离补丁与恢复方法见 Sample 文档。NuGet 恢复后需要核对并重新应用 VitalRouter wait-all 修正。普通 Sample 构建不运行环境生成器，不重写正式 Bootstrap 或 Addressables。

## 从现有代码理解规则

以下是实际存在的Godot基线文件，可在Unity尚未建立时阅读：

| 顺序 | 入口 | 重点 |
|---|---|---|
| 1 | [GameSession](<D:/Developer/MiniGames/Dark Nights/projects/src/Simulation/GameSession.cs>) | NewGame、Advance、单一WorldState和服务调用顺序 |
| 2 | [WorldState](<D:/Developer/MiniGames/Dark Nights/projects/src/Simulation/State/WorldState.cs>) | 稳定ID、单位／建筑／工位及在飞箭矢 |
| 3 | [ConstructionService](<D:/Developer/MiniGames/Dark Nights/projects/src/Simulation/Commands/ConstructionService.cs>) | 位置验证、选工人、扣款、生成；迁移时拆出全局选择 |
| 4 | [WorkOrders](<D:/Developer/MiniGames/Dark Nights/projects/src/Simulation/Commands/WorkOrders.cs>) | 双向独占、换命令与死亡释放 |
| 5 | [CombatService](<D:/Developer/MiniGames/Dark Nights/projects/src/Simulation/Systems/CombatService.cs>) | 前摇、射程、随机伤害、死亡与箭矢分工 |
| 6 | [SnapshotMapper](<D:/Developer/MiniGames/Dark Nights/projects/src/Persistence/SnapshotMapper.cs>) | 全世界捕获与恢复，不能把展示快照当存档 |

建议跟一次“选中工人→放置住宅”的链路。找出哪些步骤只是本地预览，哪些需要最新世界状态，哪些只能由服务器执行；再把请求改成BuildingKind、X和候选ActorIds，返回结果而不是直接改变共享选择。

## 根据任务找到未来Unity目录

下面是设计定位，不是已经生成的文件；路径相对于 `Assets/DarkNights`。Scripts 为代码区，Res 为资源区，完整结构见[技术架构](ARCHITECTURE.md)。

| 开发任务 | 归属 |
|---|---|
| 改成本、伤害、建造时间 | Res/Config 的 JSON；Scripts/Core 只读 |
| 改采集、训练、攻击规则 | Scripts/Core/Logic 与规则回归 |
| 新增一种玩家命令 | Scripts/Core/Logic、Scripts/Runtime/Network 与权限／去重测试 |
| 关闭共同操作／调整房间控制权限 | Scripts/Runtime/Session 的 CampControlMode 与 PolicyRevision；同步到 UI；直接命令、自动派工和训练共用校验 |
| 改同步频率、加入或重连 | Scripts/Runtime/Network，不能改客户端 HP 算法 |
| 换图、动画、角色锚点 | Res/Objects 下所属对象目录；原图引用 Res/Art/Original，改图放 Res/Art/Custom；在 ArtReview 检查 |
| 调 HUD 布局 | Res/UI/HUD；动态显示代码在 Scripts/View |
| 排查资源加载、对象装配与组件绑定 | Scripts/Runtime/Framework、Scripts/View 及 Res 中所属对象的 Definition／Prefab；不靠 GetComponent 兜底缺失绑定 |
| 改初始摆放 | Res/Scenes/Pinewatch.unity 的布局标记；不另写一份坐标 JSON |
| 改保存格式 | Scripts/Core/Save＋Scripts/Runtime/Save，增加显式版本迁移 |
| 改YYGC通用代码 | 独立框架checkout，先确认必要范围与工作区状态 |

## 当前可以运行的命令

在 `unity-projects` 下：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
git log -1 --oneline
```

报告写到 `artifacts/assessment-current.json`，不覆盖已提交的[冻结证据](evidence/assessment-2026-09-10.json)。该命令不运行游戏测试或Unity构建。

开始写Unity代码时，将实际Editor导入、测试和Player构建命令补入执行文档；路径由参数指定，不写死个人机器安装目录。每个手写类型补中等详尽XML说明，300行上限和层次限制从第一批代码就执行。
