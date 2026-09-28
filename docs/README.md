# Dark Nights 文档结构

本目录保留当前合同、开发入口与进行中切片。已完成批次的记录见[历史文档索引](archive/README.md)，对应机器证据在 [`archive/evidence/`](archive/evidence/)；历史结论只适用于各自记录的构建与日期。

## 合同与架构（长期有效）

- [ARCHITECTURE.md](ARCHITECTURE.md) — 当前技术架构合同
- [MULTIPLAYER.md](MULTIPLAYER.md) — 联机协议与同步设计（原地着陆源码候选协议 16，验收状态见当前清单；旧切片按日期保留）
- [SAVE_FORMAT.md](SAVE_FORMAT.md) — 世界存档格式合同（航程 v11，验收状态见当前清单；下文含历史版本）
- [YYGC_CHANGES.md](YYGC_CHANGES.md) — YYGC 修改授权与改动账本（持续维护）

## 开发入口

- [DEVELOPMENT.md](DEVELOPMENT.md) — 开发执行入口与当前状态
- [QUICK_START.md](QUICK_START.md) — 人工开发快速上手
- [PLAYER_GUIDE.md](PLAYER_GUIDE.md) — 操作说明与本机验收入口
- [DEPENDENCIES.md](DEPENDENCIES.md) — YYGC／AnyRules 依赖准备与锁定
- [SCENES.md](SCENES.md) — Unity 场景目录、入口及工作台／测试用途

## 使用说明

- [RUNTIME_TERRAIN_TUNER.md](RUNTIME_TERRAIN_TUNER.md) — 新版工作台运行时左栏、分 Tab 调参、拆填与 UI 缩放
- [LAN_SAMPLE.md](LAN_SAMPLE.md) — 独立 LAN 合作联机模板
- [SCENES.md](SCENES.md) — 新版 ReferenceChamber／RandomCave、Cave Wall Tuner 与正式远征入口
- [TERRAIN_GENERATION.md](TERRAIN_GENERATION.md) — 可破坏地形与地图生成器

## 近期切片与候选（2026-09-26）

- [太空到星球流程](SPACE_TO_PLANET_IMPLEMENTATION.md) / [验收清单](SPACE_TO_PLANET_ACCEPTANCE.md) / [设计](SPACE_TO_PLANET_FLOW_DESIGN.md) — 主分支 `31aaecf` 基线；YYGC 航程、UI Toolkit 星球表格、会话内切图和驾驶降落；Local 编译、Mono r2、纯回归及实际 Play 主流程已执行；多人／弱网等结果持续记入清单
- [主角控制链路前后对照](HERO_INPUT_ARCHITECTURE.html) — 输入采样、命令、服务端授权、ObjectsV2 模拟与镜头表现的交互式职责图；重构后的人工验证待完成
- [输入平台实施前后对照](INPUT_PLATFORM_COMPARISON.html) — 可切换输入流、通用改键和驾驶键扩展的交互式对照；本轮代码已实现，按用户要求未运行 Unity 回归
- [MAP_STATE_NETWORKING.md](MAP_STATE_NETWORKING.md) — AnyRuleD 地图联网重构与本轮验证边界

## 其他

- [项目周报与进度分析 · 2026.09.21–09.27](reports/weekly-2026-09-27.html) — 对照远征营地开发方案的离线交互周报，附 09.28 最新变更、构建证据、差距与下周建议
- `evidence/` — 近期批次的机器证据；历史证据从[归档索引](archive/README.md)进入
- `third-party/` — 第三方许可与声明
