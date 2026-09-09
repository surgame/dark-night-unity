# 人工开发 Quick start

当前仓库是评估与开发准备，尚不能从Unity Hub打开运行。实际工程创建始于M0；先按[依赖文档](DEPENDENCIES.md)取得可构建的YYGC版本和依赖，再建立Unity场景。

## 先读什么

1. 读[README](../README.md)确认当前状态、范围与联机假设。
2. 读[框架评估](FRAMEWORK_REVIEW.md)的F01–F07，理解编译依赖、身份和状态归属。
3. 读[技术架构](ARCHITECTURE.md)的程序集表，再读[联机设计](MULTIPLAYER.md)的权限和请求流水线。
4. 按[开发执行计划](DEVELOPMENT.md)从M0推进，每阶段完成出口后再扩展内容。

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

下面是设计定位，不是已经生成的文件：

| 开发任务 | 归属 |
|---|---|
| 改成本、伤害、建造时间 | Content/Rules的JSON；Core只读 |
| 改采集、训练、攻击规则 | Core/Simulation与规则回归 |
| 新增一种玩家命令 | Core/Commands、Runtime/Networking/Commands与权限／去重测试 |
| 改同步频率、加入或重连 | Runtime/Networking，不能改客户端HP算法 |
| 换图、动画、角色锚点 | Prefabs/Visuals、Content/Visuals和ArtReview |
| 调HUD布局 | Prefabs/UI；动态显示在Presentation/UI |
| 改初始摆放 | Pinewatch.unity的LevelAuthoring标记；不另写一份坐标JSON |
| 改保存格式 | Core/Persistence＋Runtime/Persistence，增加显式版本迁移 |
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
