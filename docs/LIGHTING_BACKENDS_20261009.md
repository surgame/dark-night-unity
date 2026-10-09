# 手电灯口截面与可切换照明后端

2026-10-09，开发分支 `ft-20261009-aperture-lighting-backends`，输入为 `fix-20261009-debug-hub-icons` 的 `a00508b`。本批只改变本地照明表现与制作资源；游戏协议31、存档v23、AMP1 schema2、Unity6000.4.9f1、URP17.4.0、Linear及锁定YYGC不变。没有修改框架源码、依赖锁或原生素材字节。

## 形状与挂载

共用 `LightEffect.prefab` 的 `LightEnvironmentEmitter` 增加 `Aperture Width`，默认0.375格，相当于当前岩壁原生密度的3像素。灯口是与照射方向垂直的有限截面；光束边界从截面两端展开，距离仍从真实灯口中心计算，远端保留圆弧衰减。原14格距离、90度锥角、1.35强度与暖色保持。

冻结参数 `LightEmissionRules` 加入灯口宽度；权威开关、照明身份与库存仍走原YYGC对象，没有第二份装备状态或新增网络字段。通用头部变体与手电继续继承同一效果资源。

私有后端的硬遮挡从灯口上接近目标的点采样，软遮挡保留有限5次取样；每个取样点额外检查从中心到截面的路径，防止灯口加宽后越过挡墙。没有直射贡献时跳过直射遮挡计算，近身散光和有限反射分别处理。

## 后端合同与切换

`HeroLightPresentation` 继续从冻结展示帧装配工具、方向与开关，并注入Core纯计算函数。`EnvironmentLighting` 仅管理本地参数与后端生命周期；`IEnvironmentLightBackend` 只消费相机、已加载地形和同一帧 `LightEmitterData`。

- `PrivateField`：现有384×216共享GPU光场，最多16个环境光源，保留局部地形遮挡、墙内羽化与有限反射。
- `Urp2D`：相同冻结光源映射为真实原生 `Light2D`；灯形纹理只在形状参数变化时更新，移动和转向复用。环境底光使用原生Global灯，模板开启Accurate法线受光。

切换先停用旧后端，再退休其纹理、灯实例与遮挡资源；不会自动改变选择。后端报错后仍可切到另一个后端。调参在地图更换时保留，不改变权威状态或存档。非游戏相机渲染时停用本任务的照明实例，避免污染其他相机。

在 `Dark Nights → Debug → 手电光照调试` 的“照明后端”选择 `PrivateField` 或 `Urp2D`；灯口宽度倍率可直接预览。Player启动参数为 `--dn-lighting-urp` 或 `--dn-lighting-private`，同时出现时最后一个生效。默认仍为私有后端。无需换渲染管线或重新制作地图；已有URP2D Renderer继续使用。

## 材质与原生资源

共用 `LightingSurface.hlsl` 是表面受光适配入口：私有模式读取现有光场；URP模式将真实表面颜色、Alpha和Mask交给锁定URP的 `CombinedShapeLightShared`，避免把原生加法照明错误地乘入底色。

CampSprite、CaveStrata、CaveBackgroundLayer和CavePixelRock均接入该入口及Universal2D/NormalsRendering通道。CampSprite增加可选Normal Map和Light Mask；默认贴图保留原有平面受光。URP模式下，其他标准Sprite Lit材质直接接收原生灯光；私有模式仍要求材质显式接入该表面入口。

原生模板为共享照明目录的 `UrpEnvironmentLight.prefab` 与 `UrpTerrainShadow.prefab`，由Unity Editor API首次制作，保留原GUID和组件引用。安装入口为 `Dark Nights → Tools → 安装照明后端资源`；普通启动不改写作者资源。模板绑定当前制作时的Sorting Layers；增加新的受光排序层时检查并补齐模板的目标层配置。

URP地形遮挡使用每16×16格区块一个原生ShadowCaster模板。整格按连续行合并路径，坡形沿用原权威边界，未知格保守遮挡；几何变化后只重建哈希变化的区块。Collider只作为原生阴影provider的几何来源，使用Ignore Raycast层、Trigger、排除全部碰撞层和关闭回调，不参与权威运动或伤害。

URP是功能回退，不保证逐像素复刻。私有墙内深度、墙内上限、有限反射与受光层次控制在URP模式下不生效；指定对象补光仍由共用精灵材质提供，标准第三方Sprite Lit不会自动接收这项私有补光。不得把URP的原生阴影效果或性能写成与私有模式相同。

## 验证状态

Core灯口独立探针12/12通过，执行真实Core实现，覆盖非零截面、背光、对称性、圆弧距离及非法参数。首轮静态编译定位并修复View中`System.Environment`的命名冲突；修复后Core/Runtime/View/Entry/Editor/Tests六个程序集0错误，Entry1、Editor7、Tests7个既有代码警告保留，新增照明文件没有这些警告。

后续架构检查指出View不能直接依赖Core.Logic.Lighting；现已将距离计算和灯形纯函数的装配放回Entry，通过有限委托注入View。最新候选的六程序集Editor静态编译0错误，Development和Release各四个游戏运行程序集0错误；三个配置的Entry各保留1个既有警告，Editor配置另有Editor7／Tests7个既有警告。受影响17个手写文件及三个asmdef的架构复验通过，守卫自身16项自测通过；这不是全仓架构重跑。

新增 `LightingBackendTests` 核对灯口、未知区、连续行遮挡合并、原生模板持久化provider、法线设置和灯形资源释放。原生模板的首次Editor制作与持久化provider检查已完成；其余新脚本／HLSL的首次导入meta、真实Unity编译、Shader编译、这些Editor测试、正式Bootstrap双后端切换、地图更换与画面仍待验。离屏Shader探针脚本已准备，但在4.342GiB余量时因缺少额外编译余量而未启动。没有本批Player、联机、IL2CPP或性能对照结论。

开发阶段本机初始提交余量1.87GiB，后续恢复至约4.4–4.6GiB。独立静态编译监控在完整架构分析期间观察到3.973GiB并停止本任务进程；没有启动Play或Player。减少为受影响文件的架构复验，并在新监控下完成最终静态编译；保留启动前被门控的请求和首轮编译失败。这些记录遵守当时Editor私有8GiB、可用RAM6GiB及提交余量4GiB边界，不关闭其他应用。开发阶段Local保持原分支与暖缓存，没有为导入候选切换源码。

## 主分支集成与门控更新

2026-10-09按用户要求把最低系统提交余量提高到8 GiB；监控和静态编译驱动共用同一判定，Editor上限8 GiB、可用RAM下限6 GiB保持。合并前提交余量3.809 GiB，Local Editor未Play、未编译、场景无脏标记且原有自动刷新关闭，因此仅执行Git集成，不主动刷新或启动验证。上述真实Unity／Shader／双后端画面待验项保持。

照明候选保留 `59ff941` 的协作约定整理及共同基线的Debug Hub／可复用手电成果后合入本地 `main`。Shader探针改为从当前Editor项目读取源文件，后续验证不再依赖该worktree路径；探针尚未执行。薄worktree在确认提交完整进入main、无未提交或忽略内容后通过应用可恢复归档移除，Git集成不构成候选视觉或性能验收。

## 产物保全

代码在薄worktree，未创建Library、Temp、Logs、obj或Player缓存；编译输出及日志位于Local `artifacts/lighting-backends-20261009/`，失败和门控记录保留。Local的两个原生模板临时导入副本已复制至候选资源并统一归档到 `artifacts/待清理/20261009-lighting-native-templates/`，原生暂存内容6781 bytes，空目录和其meta另行保留；清单记录全部绝对路径。没有永久删除或释放空间声明。
