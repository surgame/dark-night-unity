# 照明、光效复用与渲染后端

本页维护现行照明源码合同、原生资源复用和本地操作。业务持有／权限见[工具与装备](TOOL_DEFINITION_HARVESTING.md#装备与照明关系)、[联机](MULTIPLAYER.md)及[存档](SAVE_FORMAT.md)。当前源码集成及真实Unity／Shader／画面待验状态统一见[DEVELOPMENT](DEVELOPMENT.md)，本合同不新增验收结论。

## 光源与状态归属

手电仍是YYGC Definition／ObjectInstance／Behaviour道具，持有和开关唯一归ActorState，四格库存是引用依据。HeroLightPresentation只装配冻结展示值；光照方向与攻击方向独立，矿镐冻结挥击意图不冻结照明。客户端各自计算光照，不同步纹理、材质或阴影。

LightEffect是View子组件，组合LightEnvironmentEmitter与LocalLightFill。环境分支读取真实灯口、方向和已加载地形；补光只作用明确绑定SpriteRenderer，不写环境光场或作者颜色。两分支可分别禁用，总开关关闭两者。死亡、登船、移除、地图退休和会话退出按原生命周期撤除表现／资源，不留下幽灵工具。

## 预设、Definition 覆盖与原生挂载

| 资源／配置 | 归属 |
| --- | --- |
| `LightProfile` | 可复用单灯预设：光效模板引用、环境光与目标补光开关、方向性、灯形、颜色、强度、柔影和补光参数 |
| `FlashlightToolConfig.Profile`／`Overrides` | 物体 Definition 保存可选预设 Addressables 引用和逐项覆盖；Starter 仍只控制新角色配发 |
| `Flashlight.prefab` 的 `FlashlightView.Emitter` | 物体外观和光效安装位置、Rotation、Scale；原生 Prefab 为可编辑来源 |
| `LightEffect.prefab` | 环境光与局部补光的结构模板及明确组件绑定；数值不再由模板组件拥有 |
| `SceneLightingProfile`／`RandomLevelTemplate.Lighting` | 关卡引用默认光效模板、全局渲染资源、后端、底光、墙内补光、反射及设备灯预设 |
| `UrpEnvironmentLight.prefab`／`UrpTerrainShadow.prefab` | 原生 Light2D 及分块 ShadowCaster 模板 |

Definition 工坊的「照明道具能力」可选引用 LightProfile，所有参数均可直接编辑。未覆盖项显示并持续使用预设值；无预设时读取 Core 的 LightProfileDefaults 内置基线。实际修改某项时，一次操作同时保存该项的覆盖位与物体值，只写入当前 Definition；蓝点表示该项已覆盖，其他项继续跟随来源。点蓝点或在参数行右键选择恢复，立即回到当前预设或内置基线；换预设及清空预设均保留已覆盖项和 Starter。输入相同值和只读刷新不产生覆盖；把已有覆盖改成与来源相同的值仍保留覆盖，显式恢复才重新跟随。引用目标须是保存的项目资产，选择时加入既有 Addressables 分组，不以路径或物体名作为身份。

无预设时，光效结构取自 SceneLightingProfile.DefaultEffectTemplate，迁移绑定现有共用 LightEffect.prefab；内置基线、预设和物体覆盖使用同一冻结解析及实例生命周期，不建立第二套运行模型。工坊逐字段绘制，由 Overrides 字段的属性抽屉承接整块面板，避免 Addressables 的 Profile 字段抽屉接管。

运行链路为：Definition 配置 → `LightProfileResources` 预加载预设 → `LightProfileResolver` 合成冻结参数 → `HeroLightPresentation` 创建 YYGC 工具视图 → `FlashlightView.BindLight` → `LightEffectBinding` 从预设模板自动实例化到 Emitter → 环境光场及目标补光。手电 Prefab 不再手工内嵌 LightEffect；重复绑定复用一个实例，模板变化先退休旧实例，视图释放时撤除注册和受光引用。Runtime 配置用 AssetReference，具体 View 类型和合成由 Entry 桥接，不造成 Runtime 反向依赖 View。

Worker、Spearman、Archer继续明确绑定灯口、补光锚点和受光精灵，角色根及玩法占地保持。Rotation／Position／Scale 在物体 Prefab 的 Emitter 上制作；Scale 不作为照距的隐式倍乘，照距由 Range 控制。共享光效不保存某个角色的受光目标、开关或瞄准角。

手电原6×3像素确定性素材、100 PPU、Point、无mipmap／压缩及原字节保留。显式迁移菜单为 `Dark Nights → Tools → 迁移持久光照配置`：创建 `Profiles/FlashlightLighting.asset`，复用或补齐 `DeviceLighting.asset`、`PinewatchLighting.asset`，绑定 Definition 和含 RandomLevelTemplate 的关卡，保留原 GUID、灯口及人工资源。迁移读取保全的旧作者值；未绑定的既有手电候选、人工光效或未保存场景拒绝覆盖，失败记录和备份保留。先修复场景配置后仍可续接完整迁移。

关卡模板必须持久引用有效的 `SceneLightingProfile`，启动前校验默认光效、设备光预设和渲染资源。缺失时可显式执行 `Dark Nights → Tools → 修复关卡光照配置`：仅补缺失的设备／场景资产和关卡引用，保留已有有效配置、手电的可选预设及覆盖值；无引用变化的场景不保存。设备灯沿用原8.375格、0.45强度及暖色，场景参数使用既有配置合同默认值。未保存场景或 Prefab Stage 阻止修复，错误包含关卡路径。普通启动、调参及构建不创建或保存这些资产，新 meta 由 Unity 生成。

## 光照配置制作规范

1. Profile 为可选预设。无预设时，未覆盖项使用内置默认值；有预设时，未覆盖项持续读取该 Profile 的最新值，不把预设数值复制成长期独立配置。
2. 覆盖按参数独立产生。直接修改当前生效值即记录覆盖及作者值，保存在物体 Definition；其他参数继续跟随预设或内置默认值。蓝点可点击恢复，参数行也提供右键恢复。
3. 修改预设后，未覆盖项更新、已覆盖项保留；切换或清空预设同样保留覆盖位及作者值。取消某项覆盖后，该项立即恢复跟随当前来源。
4. 工坊的物体覆盖保存在 Definition，影响使用该 Definition 的实例。调试台修改独立运行时副本，只影响选中的本地手电；确认保存到 Definition 或共享预设后，才按该资产的引用范围生效。
5. 内置基线、预设和覆盖共用同一解析及光效生命周期。光效结构由预设模板或场景默认模板提供，物体无需额外手工挂光效；Position／Rotation／Scale 继续归物体灯口。
6. 工坊和调试台共用字段、范围、覆盖合同及冻结解析；工坊蓝点表示 Definition 覆盖，调试台蓝点表示临时覆盖。未覆盖参数显示当前来源值，刷新不制造历史或覆盖尚未提交的输入；覆盖位与数值一起撤销／重做，一次拖动保留一次历史。

## 灯形和贡献

原环境距离14格、锥角90度、强度1.35及暖色保持。Aperture Width默认0.375格，等于当前岩壁原生密度的3像素；灯口是垂直照射方向的有限截面，边界从两端展开，距离从真实中心计算，远端保留圆弧衰减。贴墙时按挂点至灯口真实坡形几何限制有效位置，取样点到中心的路径也验证遮挡。

PrivateField的环境底光、定向直射、近身散光和有限反射分开计算；空气柔影采用有限光源采样，墙内补光独立限深／限强。未知格和加载区域外保持遮挡；有限反射是有约束的艺术照明，不能声明真实全局光照。

原近身补光2.2格／0.55转入指定对象补光，环境近身散光默认0，避免重复计入光场。每精灵最多合成4个局部补光贡献，保留其他材质属性；关闭或退休清理贡献，MaterialPropertyBlock首次主线程合成时创建。

## 后端合同与切换

EnvironmentLighting读取关卡 SceneLightingProfile 并管理后端生命周期；IEnvironmentLightBackend消费相机、已加载地形和同帧LightEmitterData。Core纯计算由Entry通过有限委托注入View，照明后端不访问权威业务状态。

| 后端 | 能力与限制 |
| --- | --- |
| PrivateField | 每游戏相机共享384×216GPU光场及入射方向缓存，最多16个环境光源；局部遮挡、墙内羽化和有限反射；接收材质须显式接入表面函数 |
| Urp2D | 同一冻结光源映射原生Light2D，底光为Global灯；形状参数变化时更新灯形纹理，转向／移动复用；不复刻私有墙内深度、上限、反射和受光层次控制 |

打开 `Dark Nights → Debug → 手电光照调试`，从「选择当前运行中的手电」绑定一个真实实例，或选择 Definition／预设编辑临时草稿；没有运行实例时可在对应 Prefab Stage 查看独立灯口预览。选择场景配置后，后端及场景参数同样编辑临时副本，作用于本机引用该配置的活动会话。单灯柔影、柔度、光束边缘和灯口宽度使用同一冻结解析，两后端读取相同参数。Player参数 `--dn-lighting-urp`／`--dn-lighting-private` 同时存在时最后一个生效，覆盖该次启动的后端选择；未指定时跟随场景配置。切换先停旧后端，再退休纹理、灯和遮挡资源，不自动改变用户选择。

非游戏相机渲染时停用本任务灯实例，避免污染其他相机。URP阴影使用每16×16格区块一个模板，整格按连续行合并，坡形沿权威边界，未知格保守遮挡，几何变化只重建哈希变化区块。阴影用Collider为provider来源，Ignore Raycast、Trigger、排除碰撞层及关闭回调，不参与运动或伤害。

## 调试窗口持久化、撤销与重做

调试台所有参数首先写入独立运行时／预览副本，不标脏原 Definition、LightProfile、SceneLightingProfile 或 Prefab，也不因选择预设提前注册 Addressables。单灯参数按「预设 → Definition 覆盖 → 实例临时覆盖」合成，光效只消费冻结结果；取消临时覆盖恢复当前持久化基线。工坊继续直接编辑 Definition 资产，两入口复用 `LightOverrides`、字段范围与 `LightProfileResolver`，不共享可写配置对象。持有、开关、方向仍来自权威投影，调参不改变协议、存档或其他客户端。

「确认保存到 Definition」「确认保存到共享预设」「确认保存到场景光照资产」及灯口保存均先显示目标路径、修改项和影响范围，用户确认后才写回。Definition／Profile 只合并本轮临时修改的字段；场景只合并相对初始副本改变的 Settings 字段，保留其他当前作者值。取消确认保留草稿且不写资产。确认保存建立新的临时调参起点，结束该窗口本轮历史；其他尚未保存的单灯、灯口或场景调整仍保留。资产保存与玩法存档独立，普通关闭、换目标、脚本重载、退出 Play、实例回收或换世界不自动保存；临时效果撤除后恢复持久化配置。切换编辑目标时提示丢弃未保存草稿。

调试台没有主动另选预设时，工坊更新 Definition 的预设引用后，未临时覆盖项继续跟随新的持久化基线，已临时覆盖项保留。保存 Definition 只在调试台明确另选预设时写回引用，避免保存一个参数时恢复过期引用。

工坊继续使用原资产的 Unity 原生 Undo，在属性树结束当帧处理后合并拖动期间追加的记录；仍有鼠标捕获的 Ignore 不提前结束。调试台使用独立且有界的临时历史，保留最近128次修改；窗口按钮及 Ctrl+Z／Ctrl+Y／Ctrl+Shift+Z 只恢复副本，撤销到起点后停止，不进入工程资产历史。Unity 主菜单的原生历史继续归工坊及其他资产编辑。两者复用同一指针手势边界：一次按下到松手登记一条，重做恢复松手终值；连续两次拖动分开，取消或真实失去捕获时结束。数值输入框在 Enter 或失焦时提交，刷新无通知且不覆盖未提交文字；相同值不制造历史或丢弃重做分支。

灯口面板先编辑临时 Position／Rotation／Scale，作用于选中实例的明确 Emitter；没有运行实例时，在对应 Prefab Stage 旁用独立临时锚点预览，不修改 Stage 原对象。实例释放时自行恢复未保存姿态，避免池化后串到下一只手电。确认保存通过原生 Prefab API 仅写明确灯口，遇到该 Stage 未保存修改拒绝覆盖。临时光效和范围辅助线不启动业务、网络、RNG 或存档，使用 HideAndDontSave，退出面板即销毁；范围辅助线不替代正式墙体遮挡及双后端画面验收。

## 材质和异步资源

LightingSurface.hlsl是共用表面适配入口：私有模式读取光场，URP模式将真实颜色、Alpha和Mask交给锁定URP的CombinedShapeLightShared，避免把原生加法照明再乘入底色。CampSprite、CaveStrata、CaveBackgroundLayer及CavePixelRock接入Universal2D／NormalsRendering；CampSprite可选Normal Map／Light Mask，默认保留平面受光。

标准第三方Sprite Lit可接收URP原生灯，但不自动接收私有目标补光。屏幕HUD保持正常显示；世界交互提示可读性单独检查。模板的Sorting Layers需随新增受光排序层显式核对。

资源预加载沿ObjectSessionResources.Prepare／StartupResourceBatch，在事务外await，主线程同步装配／上传／GPU提交；不逐帧回读GPU。后台几何读取独占冻结局部格副本，每缓存只有一个运行任务；完成核验源、区域和修订，过期结果丢弃。退休先取消、等待任务退出再释放纹理，await前冻结旧矿层／源／控制器，避免迟到清理新世界。

## 人工检查

本次临时调参切片仍需按下表实际操作验收；源码完成及自动导入不能代替这些结果。

| 操作 | 预期结果与边界 |
| --- | --- |
| 运行时选手电 A，修改照距／颜色，观察同 Definition 的手电 B及源资产 | A 即时变化，B及源资产不变；蓝点表示临时覆盖，恢复该项后跟随持久化基线 |
| 从滑条中间按住拖到最左再松手；撤销、重做；再连续拖两次 | 一次撤销回到按下前，重做回到松手值；两次拖动分开，撤销到起点后停止；窗口按钮及快捷键不进入工坊资产历史 |
| 点保存后取消；关闭重开；另选目标时先取消再确认丢弃 | 取消保存保留草稿且不写资产；关闭重开恢复资产值；取消切换保留原目标，确认切换撤除未保存效果 |
| 分别确认保存到 Definition及共享预设，核对弹窗路径和字段，关闭重开 | 仅确认目标及改动字段持久化，引用该目标且未被更高层覆盖的道具跟随；成功保存结束临时历史，其他未保存范围继续保留 |
| 调试期间在工坊修改另一字段或预设引用，再保存调试字段 | 未临时覆盖项读取最新基线；保存保留其他作者字段和未在调试台另选的预设引用 |
| 改场景底光／后端，取消保存及确认保存各一次 | 临时值只作用本机引用该配置的会话；取消不写源资产，确认后仅变更的 Settings 字段持久化，双后端画面分别检查 |
| 临时移动灯口，关闭窗口；在对应 Prefab Stage 有未保存修改时保存灯口 | 未保存姿态撤除，Stage 原对象不被预览修改；脏 Stage 拒绝写回，处理后确认才保存明确 Emitter |
| 退出 Play、实例回收／池化复用及换世界 | 临时单灯、场景及灯口效果撤除，不写资产，不串到下一实例或新世界 |

| 项目 | 操作与检查 |
| --- | --- |
| 控制和持有 | F、改键、菜单、暂停；矿镐／枪同时照明；切换、移除、恢复及独立喷气能力不产生错引用 |
| 自身与灯形 | 前／上／下方向身体与脚边可读，挂点移动／旋转／镜像正确；有限灯口、硬光／柔影和圆弧边缘可辨 |
| 墙、坡形与动态地形 | 转角、顶棚、薄／厚墙；挖墙／爆破、快速换区后未知区不穿透，旧任务不回写 |
| 私有参数 | 对比墙内深度0／0.25／0.375／1与补光上限；1格为8原生岩壁像素，零深度关闭墙内动态补光 |
| 双后端 | Private→URP→Private、地图更换、错误后切换；分别记录效果及资源退休，不能用一个后端结论签署另一个 |
| 配置持久化和Undo／Redo | 拖动各滑条、数值提交、柔影开关、四个预设及后端切换；连续撤销／重做后核对光效与控件，撤销后新编辑不得返回旧重做分支；关闭重开、进入退出Play、目标及Settings实例更换不得回写旧会话 |
| 联机和文件 | 2人／4人、晚加入／重连／epoch的身份和开关一致；开／关分别保存重启恢复，临时缓存重建 |
| 长时与目标机器 | 多光源、矿物色、世界提示、帧时间及长期内存；前台性能、IL2CPP、双机器按各自授权和设备记录 |

检查记录保留当前源码／Player、种子、坐标、镜头倍率、后端及参数，填写接受／需调整／失败；未执行项仍待验。原始过程与失败见[初版](archive/FLASHLIGHT_IMPLEMENTATION_20261008.md)、[可复用光效](archive/REUSABLE_LIGHTING_20261009.md)、[后端实施](archive/LIGHTING_BACKENDS_20261009.md)。
