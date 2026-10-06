# 工作区目录用途

2026-10-07文档再次精简后的日常入口。目录归属按源码、作者来源、实际引用与验证任务确认；未导入 Unity 或年代较早不等于弃用。目录移动见[10-05回执](archive/FOLDER_ORGANIZATION_20261005.md)，本次文档整合和证据取舍见[10-07回执](archive/DOCUMENT_CONSOLIDATION_20261007.md)。

## 仓库根目录

| 目录 | 用途与保留依据 |
| --- | --- |
| `Game/` | 唯一日常 Unity 宿主；日常导入与构建独立于外部基线和参考项目 |
| `tools/` | 依赖准备、纯规则检查、联机矩阵与美术重建入口；分类见[工具索引](../tools/README.md) |
| `experiments/` | 独立实验的唯一可编辑来源、图集重建来源和用户成果；见[实验索引](../experiments/README.md) |
| `docs/` | 当前合同、配置和操作说明；旧方案、过程、审查和周报归 `archive/` |
| `docs/evidence/` | 当前矿层、主角及地形／工作台机器摘要；新批次按实际用途写入 |
| `docs/archive/evidence/` | 仍有用途的历史来源、框架和待验记录；保留文件字节保持，重复旧证据按整理范围移除 |
| `docs/archive/reports/` | 按原日期保存的周报，旧版本结论不作为当前验收 |
| `docs/third-party/` | 许可、第三方来源与声明 |
| `artifacts/` | Player、原始验证报告、日志、截图、隔离测试存档及本机操作回执；不提交 |
| `artifacts/待清理/` | 已确认不用的中间产物集中保留；按日期／任务隔离并写清单，无永久删除授权 |
| `.deps/` | 当前及历史锁定依赖；未逐项批准版本退役，不移动、不修改源码或补丁 |
| `.github/` | 仓库自动化配置 |
| `.git/` | Git 索引与对象，不参与本批整理 |

## Unity 宿主

| 目录／位置 | 用途与处理 |
| --- | --- |
| `Game/Assets/DarkNights/Scripts/` | Core、Runtime、View、Entry、Editor 和 Tests 源码；不为目录整理改变职责或生成绑定 |
| `Game/Assets/DarkNights/Res/` | 作者维护的 Definition、Prefab、材质、动画、场景与唯一素材源；保留 `.meta`／GUID |
| `Game/Assets/Scenes/` | Bootstrap 与仍被设置引用的默认模板 |
| `Game/Assets/Samples/` | 独立 LAN／InputActions 样例；有现行验证及构建用途 |
| `Game/Assets/AddressableAssetsData/`、`Addressables/` | 现行加载与构建配置；物理位置不等同于游戏资源归属 |
| `Game/Assets/Editor/`、`Network/`、`Scripts/` | 宿主接线与现有网络／Editor 支持，保留已用引用 |
| `Game/Assets/Packages/`、`Plugins/` | 第三方组件与本机导入依赖；不作为普通缓存搬走 |
| `Game/Assets/Resources/`、`Settings/` | 已有框架／主题／渲染配置，保留现有特殊加载语义 |
| `Game/Packages/`、`ProjectSettings/` | 锁定包配置、Editor 版本、场景与平台配置，继续提交 |
| `Game/Library/`、`Temp/`、`Logs/`、`obj/` | Local Editor 仍在使用；本批保留，不能与其他 checkout 共享 Library |
| `Game/UserSettings/`、IDE 配置 | 用户本机状态，不提交、不冒充可丢弃来源 |
| `Game/Build/`、生成的项目／解决方案文件 | 宿主生成入口；来源与后续用途未逐项退役，原位保留 |

三个旧地形场景虽然已退出产品入口，仍在场景合同、菜单定位和测试中引用；两份营地场景仍供回归及显式 Player 构建。它们继续留在[场景索引](SCENES.md)列出的原位置。原生矿床 Prefab／Definition 还承担配置与素材引用，不按运行对象退出直接搬走。

## 本机产物

最终 Mono 与各历史构建仍关联报告、DLL 身份或失败复现；全部保留原路径。`artifacts/weak-network-completion-20261005/` 包含未完成矩阵，后续继续复用，其结果入口见[开发执行计划](DEVELOPMENT.md)。正式证据、测试失败、隔离存档和作者源备份继续保留。

工具的可重建 `bin/`、`obj/`、Python 字节缓存和本轮已使用完的测试生成资产移入 `artifacts/待清理/20261005-folder-organization/`。清单记录原／新绝对路径、体积、产生任务、归档原因、处理条件及文件哈希。归档释放空间为0；没有涉及 `.codex`、外部 YYGC、Godot 基线或其他聊天工作树。
