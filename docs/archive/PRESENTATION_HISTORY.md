# 原生表现、输入与 M5 历史摘要

2026-10-07 整合原生外观、UI、效果、主视图、主角输入及M5校准过程。日常操作以[Player指南](../PLAYER_GUIDE.md)为准，状态和输入边界以[技术架构](../ARCHITECTURE.md)、[联机设计](../MULTIPLAYER.md)为准。

## 原生资源与制作来源

最初迁移保留551项原素材、75组精灵、9项声音和32段AnimationClip的来源、字节及哈希。原始素材位于 `Game/Assets/DarkNights/Res/Art/Original`；冻结艺术、UI和效果输入分别保存在Editor的 `ArtInput.json`、`UiLayoutInput.json`／`UiSourceHashes.json`、`EffectInput.json`。

`tools/prepare-art.py`、`prepare-ui.py`、`prepare-effects.py` 及首版Install入口仅向指定空目录初始化，不能用普通导入或构建覆盖人工编辑。已有Prefab、动画、场景、字体引用及GUID继续作为制作来源；坐标转换遵循玩法像素和Unity坐标合同。

15类原生对象统一为6类 `ActorView`、5类 `BuildingView`、4类 `WorksiteView`，每个Prefab只有一个继承YYGC的主视图。表现Behaviour从所属对象取得视图，不通过 `visual` 自绑定或另一套外观状态兜底。被动预览、尸体、废墟沿同一定义工厂创建；资源、绑定、异步取消与退休均须释放。

## UI、效果与 Linear

| 已收敛合同 | 用途 |
| --- | --- |
| 原生UGUI面板及显式绑定 | 原RectTransform／文字为作者来源，新增联机和槽位控件由有限Prefab编辑维护 |
| 本地交互与YYGC Interaction Sessions | 选择、镜头、帮助、建造预览互不覆盖；菜单阻塞本机输入，房主暂停通过权威命令执行 |
| 冻结表现事件与去重 | 音效、浮字和残骸按epoch／事件序号消费；晚加入不重播历史一次性通知，未过期残骸另有冻结集合 |
| 右键指令圈本地化 | 各端预热复用，保留原外观和0.8秒寿命；不再进入网络投影；显示反馈不代表服务端已接受指令 |
| 原生动画和箭矢 | 动作采样不结算伤害；服务端保留攻击时序，客户端只读冻结帧 |
| Linear色彩空间 | 纹理与作者颜色进入线性照明／混合；Godot Gamma差异单列，不为逐像素追平修改项目色彩空间 |

2026-09-14 M5校准包含按钮文字四态、字排／多行间距、来源阴影及新增菜单区域；UIFont采用实际可解析的Microsoft YaHei／HintedSmooth，Georgia标题保留，根Canvas启用像素对齐。终局重开需清理旧状态、恢复会话并重新Ready，不能仅重置面板。

固定世界的六种局面及两种分辨率用于区分几何、文字、状态和色彩差异。冻结来源及仍需追溯的输入见[世界表现摘要](evidence/m5-world-presentation-2026-09-14.json)；各历史Player身份仍只说明原批次。

## 主角输入的已收敛语义

默认主角由服务端在玩家首次有效Ready后新建专属worker，重复Ready不增员、不接管场景闲置村民。加载优先恢复仍标记为手动主角的保存对象；真正重连按当前产品合同生成新人。SharedCamp／HostOnly及控制租约由可信会话统一校验。

`GameInputActions` 为正式本地玩家唯一Unity输入入口，YYGC动作组和Interaction Sessions控制许可；采样保存短按边沿、发送限速和保活，服务器检查连接、epoch、策略、租约及序号，过期输入归零。相机、动画、选装反馈只读展示副本。

后续手持素材、原生挂点、装备状态及池化仍有独立可操作说明，保留[手持装备交接](HERO_HANDHELD_EQUIPMENT.md)和[人工／回归清单](HERO_HANDHELD_TEST_PLAN.md)。当前交易／装备合同另见[交易设计](SHIP_TRADE_EQUIPMENT_DESIGN.md)与[验收状态](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)，早期四槽和燃料规则不能覆盖后续版本。

2026-10-05跳跃边沿立即发送及Host本地主角即时冻结展示是新的修复批次，其编译与69项定向检查见[当前修复记录](../evidence/hero-input-presentation-20261005.json)。旧Mono早于该修复，不作为当前操控、远端客户端或坡沿手感通过依据。

## 保留的验收边界

静态截图不证明动画时序、真实操作和前台性能；后台容量与启动结果不替代普通前台帧时。原生主视图历史完整批次155/156中的按钮主题失败保留其历史身份，不转写为全绿，也不直接认定当前版本仍失败。

人工手感、改键体验、音频、最终世界组合画面及跨机器门槛按[当前执行状态](../DEVELOPMENT.md)验收。框架修改的范围和来源继续保留在[YYGC账本](../YYGC_CHANGES.md)。被移除的重复实施过程、旧文件清单和机器摘要可通过[整理映射](DOCUMENT_CONSOLIDATION_20261007.md)从Git恢复。
