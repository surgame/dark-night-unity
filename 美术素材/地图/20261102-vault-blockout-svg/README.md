# 宽洞地形白模 · SVG风格候选

2026-11-02，CL-01／08，本聊天／Local。用户选择宽口洞厅方向，并明确方正岩层／长顶板／错台也属于宽洞；要求白模具有基本观感，允许SVG转资源，继续不放物品或过渡模块。

## 规格与分层

原生SVG位于`Custom/`，四种地形各含Terrain与Backdrop两份。PNG输出1920×1200，SVG逻辑画布1536×960；Unity显示密度125px／单位，世界范围X=-0.08～15.28、Y=-4.8～4.8。全部使用同一密度和中心原点，不缩放地形或主角来填满镜头。

Terrain为透明前景：岩层截面、色块、裂隙和内收边缘都裁在同一作者轮廓内。Backdrop为独立底图：洞外天色、远处地质轮廓和洞内平面后壁，不含可采矿物、物品、树、水、植物、机关或人物。颜色按sRGB纹理进入现有Linear工程，材质保持白色，不额外反向编码或改变工程色彩空间。

风格分别为暖棕层岩、青绿岩拱、冷蓝裂岩、赤赭断层。第一种采用真正的垂直格边和水平台地，其他形态组合既有全坡、半坡与平面；不是将所有入口都做成同一种圆拱。

## 来源与重建

形状唯一编辑来源为新Prefab的WhiteboxTerrainAuthoring.Floor／Ceiling；当前制作输入保留于`artifacts/vault-svg-styles-20261102/design-inputs.json`。SVG的实空裁切由相同点列确定，岩层色块和裂纹是确定性矢量绘制，不使用生图、外来素材或像素滤镜。

`tools/entrance-whitebox/make-vault-styles.py`记录首批设计及SVG生成方式，`raster-vault-styles.cjs`使用已有浏览器将SVG透明导出PNG，不新增Unity SVG包或改依赖。初始化只允许空目标；手工修改过的SVG和Prefab不得被该初始化脚本覆盖。已有轮廓改动时先导出该Prefab的新点列，更新SVG裁切并重新导出对应PNG，再通过明确工作台动作重建网格和保存；本片没有自动覆盖作者SVG的工具。

PNG及四份原生Prefab位于`Game/Assets/DarkNights/Res/Scenes/Workbenches/Terrain/EntranceWhiteboxAssets/VaultStyles/`。场景为同级CaveVaultStrata／Moss／Blue／Ochre，工作台搜索“宽洞”或M01可打开。前景与背景Sprite显式引用，原生地形Mesh保留作同源形状底稿；图层摘要与轮廓不符时运行入口拒绝启动，工作台也不单独重建碰撞留下旧图。

这是可装配的风格白模候选，不是最终美术、随机拼接图集或正式地图替换。原四形态与旧地图保留。编译、编辑态画面、来源核对及Play待验分别见DEVELOPMENT；本片没有人物实际通行、生态动画、采集或性能验收。
