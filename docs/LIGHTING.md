# 照明、光效复用与渲染后端

本页维护现行照明源码合同、原生资源复用和本地操作。业务持有／权限见[工具与装备](TOOL_DEFINITION_HARVESTING.md#装备与照明关系)、[联机](MULTIPLAYER.md)及[存档](SAVE_FORMAT.md)。当前源码集成及真实Unity／Shader／画面待验状态统一见[DEVELOPMENT](DEVELOPMENT.md)，本合同不新增验收结论。

## 光源与状态归属

手电仍是YYGC Definition／ObjectInstance／Behaviour道具，持有和开关唯一归ActorState，四格库存是引用依据。HeroLightPresentation只装配冻结展示值；光照方向与攻击方向独立，矿镐冻结挥击意图不冻结照明。客户端各自计算光照，不同步纹理、材质或阴影。

LightEffect是View子组件，组合LightEnvironmentEmitter与LocalLightFill。环境分支读取真实灯口、方向和已加载地形；补光只作用明确绑定SpriteRenderer，不写环境光场或作者颜色。两分支可分别禁用，总开关关闭两者。死亡、登船、移除、地图退休和会话退出按原生命周期撤除表现／资源，不留下幽灵工具。

## 原生资源与挂载

| 资源 | 用途 |
| --- | --- |
| `Res/Shared/Lighting/LightEffect.prefab` | 共用环境照明和指定对象补光 |
| `Res/Shared/Lighting/HeadMountedLightEffect.prefab` | 继承共用效果，仅覆盖头部灯口位置 |
| `Res/Objects/Flashlight/Flashlight.asset`／`Flashlight.prefab` | 正式手电定义、外观及内嵌共用光效，保留原GUID |
| 共享目录的 `UrpEnvironmentLight.prefab`／`UrpTerrainShadow.prefab` | 原生Light2D及分块ShadowCaster模板 |

帽子／固定灯具复用光效而不新增产品：Environment Origin放在真实灯口，Local Fill Origin放补光位置，Targets明确绑定受光精灵。空Targets没有局部补光，固定灯可只启用环境分支。运行挂载使用 `LightEffect.Bind(context, occlusionAnchor, fillAnchor, receivers)`；业务对象判断持有、权限或供电后传入SetOn。静态场景光源可保留引用并登记到同场景正式相机。

Worker、Spearman、Archer显式绑定灯口、补光锚点及受光精灵，角色根／玩法占地保持。移动、旋转、左右镜像与地形非等比缩放均由表现变换处理。视觉参数唯一归共用效果Prefab，Runtime配置只声明照明能力和新角色配发标记。

手电原6×3像素确定性素材、100 PPU、Point、无mipmap／压缩及原字节保留。安装菜单仅用于首次指定资源制作：`Dark Nights → Tools → 安装可复用照明与手电道具`、`安装照明后端资源`。普通启动／构建不重建人工Prefab；新meta由正常Unity导入生成。

## 灯形和贡献

原环境距离14格、锥角90度、强度1.35及暖色保持。Aperture Width默认0.375格，等于当前岩壁原生密度的3像素；灯口是垂直照射方向的有限截面，边界从两端展开，距离从真实中心计算，远端保留圆弧衰减。贴墙时按挂点至灯口真实坡形几何限制有效位置，取样点到中心的路径也验证遮挡。

PrivateField的环境底光、定向直射、近身散光和有限反射分开计算；空气柔影采用有限光源采样，墙内补光独立限深／限强。未知格和加载区域外保持遮挡；有限反射是有约束的艺术照明，不能声明真实全局光照。

原近身补光2.2格／0.55转入指定对象补光，环境近身散光默认0，避免重复计入光场。每精灵最多合成4个局部补光贡献，保留其他材质属性；关闭或退休清理贡献，MaterialPropertyBlock首次主线程合成时创建。

## 后端合同与切换

EnvironmentLighting管理本地参数及生命周期；IEnvironmentLightBackend消费相机、已加载地形和同帧LightEmitterData。Core纯计算由Entry通过有限委托注入View，照明后端不访问权威业务状态。

| 后端 | 能力与限制 |
| --- | --- |
| PrivateField | 每游戏相机共享384×216GPU光场及入射方向缓存，最多16个环境光源；局部遮挡、墙内羽化和有限反射；接收材质须显式接入表面函数 |
| Urp2D | 同一冻结光源映射原生Light2D，底光为Global灯；形状参数变化时更新灯形纹理，转向／移动复用；不复刻私有墙内深度、上限、反射和受光层次控制 |

打开 `Dark Nights → Debug → 手电光照调试`，选择PrivateField或Urp2D并调整灯口宽度倍率。Player参数 `--dn-lighting-urp`／`--dn-lighting-private` 同时存在时最后一个生效；默认PrivateField。参数在地图更换时保留，不写玩法存档；切换先停旧后端，再退休纹理、灯和遮挡资源，不自动改变用户选择。后端报错后仍可手动切换。

非游戏相机渲染时停用本任务灯实例，避免污染其他相机。URP阴影使用每16×16格区块一个模板，整格按连续行合并，坡形沿权威边界，未知格保守遮挡，几何变化只重建哈希变化区块。阴影用Collider为provider来源，Ignore Raycast、Trigger、排除碰撞层及关闭回调，不参与运动或伤害。

## 材质和异步资源

LightingSurface.hlsl是共用表面适配入口：私有模式读取光场，URP模式将真实颜色、Alpha和Mask交给锁定URP的CombinedShapeLightShared，避免把原生加法照明再乘入底色。CampSprite、CaveStrata、CaveBackgroundLayer及CavePixelRock接入Universal2D／NormalsRendering；CampSprite可选Normal Map／Light Mask，默认保留平面受光。

标准第三方Sprite Lit可接收URP原生灯，但不自动接收私有目标补光。屏幕HUD保持正常显示；世界交互提示可读性单独检查。模板的Sorting Layers需随新增受光排序层显式核对。

资源预加载沿ObjectSessionResources.Prepare／StartupResourceBatch，在事务外await，主线程同步装配／上传／GPU提交；不逐帧回读GPU。后台几何读取独占冻结局部格副本，每缓存只有一个运行任务；完成核验源、区域和修订，过期结果丢弃。退休先取消、等待任务退出再释放纹理，await前冻结旧矿层／源／控制器，避免迟到清理新世界。

## 人工检查

| 项目 | 操作与检查 |
| --- | --- |
| 控制和持有 | F、改键、菜单、暂停；矿镐／枪同时照明；切换、移除、恢复及独立喷气能力不产生错引用 |
| 自身与灯形 | 前／上／下方向身体与脚边可读，挂点移动／旋转／镜像正确；有限灯口、硬光／柔影和圆弧边缘可辨 |
| 墙、坡形与动态地形 | 转角、顶棚、薄／厚墙；挖墙／爆破、快速换区后未知区不穿透，旧任务不回写 |
| 私有参数 | 对比墙内深度0／0.25／0.375／1与补光上限；1格为8原生岩壁像素，零深度关闭墙内动态补光 |
| 双后端 | Private→URP→Private、地图更换、错误后切换；分别记录效果及资源退休，不能用一个后端结论签署另一个 |
| 联机和文件 | 2人／4人、晚加入／重连／epoch的身份和开关一致；开／关分别保存重启恢复，临时缓存重建 |
| 长时与目标机器 | 多光源、矿物色、世界提示、帧时间及长期内存；前台性能、IL2CPP、双机器按各自授权和设备记录 |

检查记录保留当前源码／Player、种子、坐标、镜头倍率、后端及参数，填写接受／需调整／失败；未执行项仍待验。原始过程与失败见[初版](archive/FLASHLIGHT_IMPLEMENTATION_20261008.md)、[可复用光效](archive/REUSABLE_LIGHTING_20261009.md)、[后端实施](archive/LIGHTING_BACKENDS_20261009.md)。
