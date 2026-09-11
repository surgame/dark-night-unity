# ObjectDefinition 空装配定义盘点

2026-09-12。**已完成静态盘点与方案评估，提取尚未实施。** 本次没有修改游戏代码、Prefab、定义、Addressables、YYGC 或用户已有改动，也没有启动 Unity、运行测试或构建 Player。

盘点基于提交 `348b54d25770e80378eb05b7aea2495c9c958f67` 加当时工作区；用户已有的 `AGENTS.md`、`Farm.prefab` 修改和 `experiments/aseprite-pixel-crew/` 均保留。盘点期间 `Worker.asset` 的 `legacyIdAliases` 空列表序列化文本发生变化；本次分类所用字段未变，证据哈希已更新，原变化记录保留。完整清单、Prefab 脚本／绑定及源文件 SHA-256 见[机器可读记录](evidence/object-definition-audit-2026-09-12.json)。

“空”在本文中仅指 `BehaviourTypes = []` 且 `SharedConfigs = []`，并不代表资产无引用、没有 Prefab 组件或可直接删除。检查范围为 `Game/Assets` 下实际 ObjectDefinition 资产，并与正式数据库交叉核对；不统计 Library、构建输出和依赖缓存中的样板。项目内未发现 ObjectDefinition 子类。

## 盘点结论

| 范围／类别 | 数量 | 结论 |
|---|---:|---|
| Game/Assets 中全部定义资产 | 29 | 27 个正式定义，2 个独立 LAN Sample 定义 |
| 正式定义中行为与共享配置均为空 | 20 | 19 个 Local，1 个 Network |
| Command 同类的效果／音频 | 4 | 建议优先从定义体系提取 |
| 角色、建筑、工位外观 | 15 | 也都是纯本地表现；可提取，但需整体迁移共同的外观接入合同 |
| 玩家网络入口 | 1 | 虽为空列表，仍是有实际网络职责的定义，保留 |
| 其余正式定义 | 7 | 实际装配会话、UI 或输入互斥行为，保留 |
| Sample 空列表定义 | 1 | 独立模板的有意示范资产，保留作为回归基线 |

19 个本地候选的 Prefab 都含 `ObjectInstance + LocalObjectInstanceInitializer + ObjectView` 包装，实际展示由 `NativeEffect`、`CampAudio` 或 `NativeVisual` 承担，没有通过这些定义装配 YYGC Behaviour。移除包装具有结构简化依据；本次未测量帧耗时或分配，不能宣称已有性能提升。

建议先处理 4 个效果／音频，正式定义数将由 27 变为 23；若随后将 15 类实体外观也整体迁出，将剩 8 个正式定义。这是方案计数，当前资产仍为 27 个。

## 20 个正式空列表定义逐项评估

下表除 `connection.pinewatch` 为 Network 外，其余均为 Local，行为和共享配置列表均为空。“共同外观迁移”包含内容目录、创建入口、绑定、Editor 预览、建造幽灵和残骸；不能仅删除定义资产。

| Definition Key／资产 | 实际角色 | 评估 | 建议落点 | 必须保留的依赖与语义 |
|---|---|---|---|---|
| [effect.command](../Game/Assets/DarkNights/Res/Effects/Command/Command.asset) | 指令落点圈 | 优先提取；改动面小 | 本地可复用指令圈绘制组件 | 多个圈并存、0.8 秒淡出、未缩放时钟、世界排序 160 |
| [effect.floating](../Game/Assets/DarkNights/Res/Effects/Floating/Floating.asset) | 伤害／资源浮字 | 优先提取；改动面中 | 保留原生浮字 Prefab，由本地展示集合复用 | 文字／资源图标、1.6 秒寿命、世界 Canvas 排序 170、字体与布局 |
| [effect.arrow](../Game/Assets/DarkNights/Res/Effects/Arrow/Arrow.asset) | 在飞箭矢外观 | 优先提取；改动面中 | 按 ProjectileViewData.ViewId 绑定的本地箭矢视图集合 | 晚加入的在飞箭矢、轨迹与朝向、暂停／倍速、epoch 清理、排序 150 |
| [audio.camp](../Game/Assets/DarkNights/Res/Effects/Audio/Audio.asset) | 背景音乐与音效 | 优先提取；改动面小 | 本地表现宿主持有 CampAudio／音频 Prefab | 现状随应用装配创建一次；断线不销毁。保留静音、音乐循环、命中音冷却及原音量 |
| [unit.worker](../Game/Assets/DarkNights/Res/Objects/Worker/Worker.asset) | 工人外观 | 可提取；需共同外观迁移 | ContentId → 原生 NativeVisual Prefab 映射 | 采集／施工动作、头像与四类锚点；FormalObjectCatalog 明确要求 worker 定义 |
| [unit.spearman](../Game/Assets/DarkNights/Res/Objects/Spearman/Spearman.asset) | 长矛兵外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 动作、朝向、插值和死亡残影；战斗仍由 Core 结算 |
| [unit.archer](../Game/Assets/DarkNights/Res/Objects/Archer/Archer.asset) | 弓箭手外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 分层动作、朝向、死亡残影；与 effect.arrow 的展示职责分开 |
| [unit.zombie](../Game/Assets/DarkNights/Res/Objects/Zombie/Zombie.asset) | Zombie 外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 敌方动作、插值、受击与死亡残影 |
| [unit.ghoul](../Game/Assets/DarkNights/Res/Objects/Ghoul/Ghoul.asset) | Ghoul 外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 敌方动作、插值、受击与死亡残影 |
| [unit.armored](../Game/Assets/DarkNights/Res/Objects/Armored/Armored.asset) | Armored 外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 敌方动作、插值、受击与死亡残影 |
| [building.tavern](../Game/Assets/DarkNights/Res/Objects/Tavern/Tavern.asset) | 酒馆外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 场景初始布局、施工／成品、头像与废墟 |
| [building.house](../Game/Assets/DarkNights/Res/Objects/House/House.asset) | 住宅外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 建造幽灵、施工／成品、头像与废墟 |
| [building.barracks](../Game/Assets/DarkNights/Res/Objects/Barracks/Barracks.asset) | 兵营外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 建造幽灵、施工／成品、训练状态锚点与废墟 |
| [building.farm](../Game/Assets/DarkNights/Res/Objects/Farm/Farm.asset) | 农田建筑外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 建造幽灵、施工和派生食物工位；当前工作区绑定差异见下文 |
| [building.tower](../Game/Assets/DarkNights/Res/Objects/Tower/Tower.asset) | 守望塔外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 建造幽灵、施工／成品、范围预览与废墟 |
| [worksite.wood](../Game/Assets/DarkNights/Res/Objects/Trees/Trees.asset) | 木材工位外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 变体、耗尽外观、选择边界与状态锚点 |
| [worksite.stone](../Game/Assets/DarkNights/Res/Objects/Stone/Stone.asset) | 石料工位外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 变体、耗尽外观、选择边界与状态锚点 |
| [worksite.iron](../Game/Assets/DarkNights/Res/Objects/Iron/Iron.asset) | 铁矿工位外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 变体、耗尽外观、选择边界与状态锚点 |
| [worksite.food](../Game/Assets/DarkNights/Res/Objects/Farmland/Farmland.asset) | 食物工位外观 | 可提取；需共同外观迁移 | 同一外观内容目录 | 自然工位与农田派生工位的显示差异、变体与耗尽状态 |
| [connection.pinewatch](../Game/Assets/DarkNights/Res/Objects/PlayerConnection/PlayerConnection.asset) | 玩家网络连接入口 | 保留；不属于纯本地表现 | 现有 YYGC／FishNet 网络定义 | Prefab 实际包含 PlayerEndpoint、NetworkCommandSender、NetworkObject、StateSynchronizer |

这 15 类实体视图没有独立权威状态；空装配列表符合目前 Core 集中模拟、客户端只读展示的实现。继续使用 ObjectDefinition 的实际用途是内容身份／Prefab 寻址和 ObjectView 装配。它们并非技术上必须保留，也不能仅以“没有 Behaviour”判定现有内容合同可以被删掉。

## 7 个非空正式定义

| Definition Key／资产 | 类型 | 实际装配 | 评估 |
|---|---|---|---|
| [session.pinewatch](../Game/Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset) | Network | WorldSessionBehaviour + ContentDefinitionMap | 保留会话定义；15 类外观若迁出，内容映射字段另行迁移 |
| [ui.chrome](../Game/Assets/DarkNights/Res/UI/Chrome/Chrome.asset) | Local | CampHudBehaviour | 保留；UGUIManager、生成绑定与 HUD 行为正在使用 |
| [ui.mainmenu](../Game/Assets/DarkNights/Res/UI/MainMenu/MainMenu.asset) | Local | MainMenuBehaviour | 保留；UGUI 面板生命周期及输入事件 |
| [ui.pausemenu](../Game/Assets/DarkNights/Res/UI/PauseMenu/PauseMenu.asset) | Local | PauseMenuBehaviour | 保留；UGUI 面板生命周期及输入事件 |
| [ui.help](../Game/Assets/DarkNights/Res/UI/Help/Help.asset) | Local | HelpMenuBehaviour | 保留；UGUI 面板生命周期及输入事件 |
| [ui.result](../Game/Assets/DarkNights/Res/UI/Result/Result.asset) | Local | ResultMenuBehaviour | 保留；UGUI 面板生命周期及输入事件 |
| [service.interaction_sessions](../Game/Assets/DarkNights/Res/UI/Shared/InteractionSessions.asset) | Local | YYInteractionSessionService | 保留；通过 ObjectSingletonDatabase 装配输入互斥服务，无 Prefab 是现有服务接法 |

UI 定义虽然没有 SharedConfigs，但每个都有实际 MenuBehaviour 派生行为和 UGUIManager 创建入口。InteractionSessions 通过单例数据库以定义 GUID 装配 YYInteractionSessionService；不能因为没有 Prefab 将其视为遗留空壳。

## 独立 Sample 与其他相似表现

| 项目 | 实际状态 | 评估 |
|---|---|---|
| [lan_sample.worksite](../Game/Assets/Samples/LanCoop/Content/Worksite.asset) | Local，行为／配置双空；SampleComposition 显式将场景中的 Worksite ObjectInstance 与此定义初始化绑定 | 保留。它用于演示 YYGC 本地对象接法，不在正式数据库中 |
| [lan_sample.session](../Game/Assets/Samples/LanCoop/Content/Session.asset) | Network，装配 CampBehaviour；由独立 Sample 自建目录 | 非空，保留。正式游戏不反向引用 Sample |
| 选择圈、悬停、建筑／工位底线、建造占地／范围线、框选 | CampOverlay 已用 UGUI 网格集中绘制 | 没有各自的 ObjectDefinition，不存在额外定义可移除 |
| 尸体／废墟 | SessionEffects 复用角色／建筑的内容映射和 NativeVisual，未建立独立尸体／废墟定义 | 归入 15 类外观迁移的依赖，避免为残骸再建一套定义 |
| 建造幽灵 | SessionPlacementView 复用相应建筑外观定义 | 归入建筑外观迁移；支付和合法性仍走现有权威链 |
| 火把、灯位、月亮、萤火、背景、地表、角色阴影 | 原生环境／角色 Prefab 和表现组件 | 本次资产普查未发现对应的独立 ObjectDefinition；无需按本清单做定义迁出 |

## 建议实施边界

第一批：提取 4 个效果／音频。SessionEffects 继续消费现有冻结事件和箭矢投影，向本地表现组件提供数据；PresentationCursor 保留去重与过期判定。指令圈管理多个绘制记录，箭矢按 ProjectileViewData.ViewId 绑定，浮字保持原生字体／图标 Prefab，音频保留 CampAudio 与两个 AudioSource。它们的生命周期不同，不需要建立一个同时管理高亮、箭矢、UI 和声音的通用 Manager。

音频现状是在 GameSessionStartupModule 装配 SessionEffects 时创建一次；SessionEffects.Clear 只清理箭矢和短时效果，音频到宿主销毁才释放。因此提取音频时应保留其跨连接的本地表现生命周期，不能改成每次断线重新播放音乐。

当前排序为箭矢 150、指令圈 160、世界空间浮字 Canvas 170，而选择叠层属于屏幕空间 UGUI。将指令圈并入 CampOverlay 会改变相对于浮字的排序。可复用几何和管理方式，具体绘制层级需按原画面保留并验证。

第二批：若决定迁出 15 类外观，统一建立 ContentId 到原生外观资源的映射，接续现有资源加载能力。共同改动包括：

- `ContentDefinitionMap / ContentDefinitionEntry`、WorldSession 上的映射、`FormalObjectCatalog` 和 Editor 守卫。
- `SessionEntityViews` 的创建、异步过期检查、EntityId 绑定、插值、转职替换与释放。
- `SessionPlacementView` 的建造幽灵，以及 `SessionEffects` 的尸体／废墟创建。
- `PinewatchVisualSetup`、布局标记预览和现有 Prefab 组件绑定／保存检查。
- 15 类原生 Prefab 的表现、动画、锚点与原始素材引用，以及对应的内容和绑定验证。

Prefab、AnimationClip、材质、音频和原始素材仍是人工可编辑资源。移出 ObjectDefinition 不等于删除 Prefab，也不必取消 Addressables；按新的引用／加载方式保留所需条目，继续复用既有资源 API，不另造资源管理器或 DI 容器。沿用保留资产的 .meta／GUID，不通过重建资产绕过引用迁移。

当前架构文档约定实体外观通过 DefinitionReference 接入。15 类迁出时需同步修订这个明确合同并记录新的外观映射方式；本次仅记录候选和依赖，没有把提案写成已完成实现。Core 的内容 ID、规则、可写世界与网络权威入口均无需为了显示资源迁出而重构。

## 资源、握手与验收影响

删除定义前，应先完成替代引用和展示接线，再清理正式数据库、必要的 Addressable 条目、旧包装组件及初始化工具。NativeEffectsSetup、NativeArtSetup 等首次初始化入口必须与新合同一致，但不得重跑来覆盖人工资源。生成绑定按已有入口更新，不手改生成输出。

SessionNetwork 实际安装 DefinitionNetworkAuthenticator，后者调用 DefinitionNetworkProfile.Create。摘要包含所有正式定义的 GUID、Key、Prefab GUID、网络类型和行为顺序，包括 Local 定义。移除这 19 项中的任意一项都会改变目录摘要；不能假定“只有本地表现”便无需检查 Host／客户端版本匹配。

| 后续实施检查 | 目的 |
|---|---|
| 编译、架构守卫、定义／Prefab／绑定引用检查 | 核对迁出的包装不再被创建，保留资产引用完整 |
| Prefab 编辑、保存、重开与 Editor 运行 | 保留人工资源、组件引用和生命周期 |
| 多个圈／浮字重叠，箭矢 150／圈 160／浮字 170，镜头与两分辨率 | 保留层级、原点、线宽、颜色、帧序与字体布局 |
| 暂停、倍速、重复事件、退出／重连／加载 epoch | 不重复播放、不延长已过期反馈、不残留旧世界视图 |
| 同一 Mono 产物的独立 Host＋客户端、晚加入在飞箭矢 | 检查新目录握手、各端一致呈现和生命周期；此项未执行 |
| 15 类外观整体迁移时，追加转职、施工、训练、耗尽、建造预览、尸体／废墟 | 覆盖共同外观目录的全部实际调用方 |
| 按实际需要采样正常交互下的创建次数、分配和帧时间 | 确认复用收益，不用结构推断性能验收通过 |

以上均为后续检查计划，不是本次通过记录。本次没有运行 Unity 或生成 Mono／IL2CPP 产物。

## 盘点中发现的现有差异

用户已有未提交修改的 [Farm.prefab](../Game/Assets/DarkNights/Res/Objects/Farm/Farm.prefab) 当前 ObjectView 绑定键为 `Farm`，而 [SessionEntityViews](../Game/Assets/DarkNights/Scripts/Entry/SessionEntityViews.cs) 与 [SessionPlacementView](../Game/Assets/DarkNights/Scripts/Entry/SessionPlacementView.cs) 查询 `visual`。这是序列化资源与源码的静态不一致，尚未在 Unity 运行复现；本次未改动或覆盖该 Prefab。后续核对或迁移这项时需基于用户现有编辑处理。

主要依据：[SessionEffects](../Game/Assets/DarkNights/Scripts/Entry/SessionEffects.cs)、[NativeEffect](../Game/Assets/DarkNights/Scripts/View/NativeEffect.cs)、[CampAudio](../Game/Assets/DarkNights/Scripts/View/CampAudio.cs)、[NativeVisual](../Game/Assets/DarkNights/Scripts/View/NativeVisual.cs)、[ContentDefinitionMap](../Game/Assets/DarkNights/Scripts/Runtime/Framework/ContentDefinitionMap.cs)、[正式目录守卫](../Game/Assets/DarkNights/Scripts/Runtime/Framework/FormalObjectCatalog.cs)、[正式数据库](../Game/Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset)和同批机器可读记录。
