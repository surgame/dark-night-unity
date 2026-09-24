# Dark Nights 文档结构

本目录保留当前合同、开发入口与进行中切片。已完成批次的记录见[历史文档索引](archive/README.md)，对应机器证据在 [`archive/evidence/`](archive/evidence/)；历史结论只适用于各自记录的构建与日期。

## 合同与架构（长期有效）

- [ARCHITECTURE.md](ARCHITECTURE.md) — 当前技术架构合同
- [MULTIPLAYER.md](MULTIPLAYER.md) — 联机协议与同步设计（协议 14；旧切片按日期保留）
- [SAVE_FORMAT.md](SAVE_FORMAT.md) — 世界存档格式合同（v10；下文含历史版本）
- [YYGC_CHANGES.md](YYGC_CHANGES.md) — YYGC 修改授权与改动账本（持续维护）

## 开发入口

- [DEVELOPMENT.md](DEVELOPMENT.md) — 开发执行入口与当前状态
- [QUICK_START.md](QUICK_START.md) — 人工开发快速上手
- [PLAYER_GUIDE.md](PLAYER_GUIDE.md) — 操作说明与本机验收入口
- [DEPENDENCIES.md](DEPENDENCIES.md) — YYGC／AnyRules 依赖准备与锁定
- [SCENES.md](SCENES.md) — Unity 场景目录、入口及工作台／测试用途

## 使用说明

- [LAN_SAMPLE.md](LAN_SAMPLE.md) — 独立 LAN 合作联机模板
- [TERRAIN_DEBUG_BOOTSTRAP.md](TERRAIN_DEBUG_BOOTSTRAP.md) — 随机地图 Debug Bootstrap
- [TERRAIN_GENERATION.md](TERRAIN_GENERATION.md) — 可破坏地形与地图生成器

## 近期切片与候选（2026-09-22）

- [MAP_STATE_NETWORKING.md](MAP_STATE_NETWORKING.md) — AnyRuleD 地图联网重构与本轮验证边界
- [STATIC_CAVE_BACKGROUND_PLAN.md](STATIC_CAVE_BACKGROUND_PLAN.md) / [STATIC_CAVE_BACKGROUND_EXECUTION.md](STATIC_CAVE_BACKGROUND_EXECUTION.md) — 静态背景岩层与独立洞穴材质
- [TERRAIN_MODIFIERS_PROGRESS.md](TERRAIN_MODIFIERS_PROGRESS.md) — 地形 Modifier 与可插拔点缀层
- [WALKABLE_EXPEDITION_SHIP.md](WALKABLE_EXPEDITION_SHIP.md) — 可步入远征飞船
- [ORE_LAYER_ART.md](ORE_LAYER_ART.md) — 按格矿层美术候选（AnyRuleD 资产待导入）

## 其他

- `evidence/` — 近期批次的机器证据；历史证据从[归档索引](archive/README.md)进入
- `third-party/` — 第三方许可与声明
