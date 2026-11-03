# M01入口白模制作来源

2026-11-02，CL-01／08，本聊天／Local。用户指定在已恢复的干净角色白模上先制作入口，多种地形形态，无物品、无过渡模块。

## 原生来源

`Game/Assets/DarkNights/Res/Scenes/Workbenches/Terrain/EntranceWhiteboxAssets/`保存四份入口Prefab。各Prefab的WhiteboxTerrainAuthoring内Floor／Ceiling是唯一作者轮廓；地形Mesh、空腔底色和边线由明确工作台动作派生。修改Prefab后运行“重建选中入口白模”，再保存Prefab；旧GUID沿用，不自动覆盖作者网格。

首批折线制作输入在`artifacts/entrance-whitebox-20261102/variants.json`，`create-modules.csx`通过单一Unity Editor API创建空目标目录下的材质、Mesh、Prefab和四个原生场景。该初始化脚本拒绝已有目标，不用于重复覆盖后续作者修改。原平地场景仅由AssetDatabase复制，字节保留。

原生场景位于同级的CaveEntranceGentle／Shelf／Vault／Fissure，工作台搜索M01可打开。严格正侧视；场景使用已指定的原主角，不重新导出或修改Spine、美术、Worker原Prefab或Definition。新增入口Prefab只有地形截面、空腔底色和两条轮廓线。

## 尺度与地形合同

首片宽15.28 Unity单位，左侧出生平地顶面Y=0，角色出生X=2；形态含不同的坡势、岩肩、拱厅或窄檐。坐标只是当前作者候选，不是正式地图数值或通行承诺。四图出口不通过独立过渡模块衔接，本片不建立整图拼接／WFC规则。

逻辑坐标继续使用EntityView.PixelsPerUnit。PlayableTerrain每格16逻辑像素，形状沿用Full加12种地面／洞顶全坡、半坡。64位格内掩码匹配仅用于确定性白模折线转换，无法匹配则拒绝；不以微弱透视、绘画圆角或视觉台阶冒充可运动斜坡。运行与原生显示网格均由同一冻结数据生成；运行唯一地图归SessionTerrain，移动仍由ActorState／MovementBehaviour负责。

当前模块无矿脉、巢位、地质事件、采集入口、生态、网络或玩家文件存档。不会修改正式地图、生成器、运动数值、YYGC或依赖。原CaveWhitebox平地、旧地图及全部原素材保留。

## 本轮核对

`capture-modules.csx`仅在保存的编辑态场景逐图读取绑定、重开并输出原生相机全景／近景。实际创建、编译、展示记录及保护输入摘要位于同任务artifacts目录，当前状态归docs/DEVELOPMENT。

本片没有获准执行Play、人物物理／往返、游戏测试或Player，编辑态画面不能证明这些项目通过。用户按Play后可沿现行绑定使用A／D、空格、Shift、Esc，实际体验仍待运行核对。

## 宽洞SVG风格续片

在原四形态之外，新增CaveVaultStrata／Moss／Blue／Ochre四个独立场景，对应暖棕层岩、青绿岩拱、冷蓝裂岩和赤赭断层。它们同时变化轮廓、色块与分层，方正版使用同一X坐标的上下端点表达真正垂直壁；其他版沿用全坡、半坡和平面。原四场景及Prefab不覆盖。

可编辑SVG和规格见[源图说明](../../美术素材/地图/20261102-vault-blockout-svg/README.md)。`make-vault-styles.py`只负责空目录首批设计和SVG绘制，`raster-vault-styles.cjs`转成1920×1200 PNG；`create-vault-styles.csx`通过单一Editor创建原生Prefab／场景并设定125px／单位、Point、FullRect、无压缩和sRGB导入。图层显式绑定在WhiteboxTerrainAuthoring，ArtworkFingerprint与作者轮廓摘要一致才允许启动或重建；后续手改不通过初始化脚本覆盖。

首次第四图暴露了全坡对角线上采样浮点歧义：旧采样偏移的X、Y之和为1，落在边界时来源和掩码判定不一致。本片将两处采样统一移出全坡／半坡边界，不修改形状合同或运动参数；前三图已保存，失败现场以原生场景副本保留，再用`resume-vault-ochre.csx`只完成缺失的第四图。原失败回执没有覆盖，完整记录归`artifacts/vault-svg-styles-20261102/`。

`capture-vault-styles.csx`输出四图实际相机全景／近景；`inspect-vault-styles.csx`核对保存重开、Sprite导入、主角与Prefab绑定、图形／轮廓摘要，并确认原四形态的形状编码计数未改变。像素透明边界抽查只核对PNG与作者轮廓，不是人物通行或运行碰撞验收；本片仍未执行Play、人物测试或Player。

## 纯色天然轮廓与尺寸草案

`make-natural-contours.py`绘制不对称贝塞尔轮廓及SVG，`create-natural-contours.csx`在独立PreviewScene创建纯色原生Mesh Prefab，地形不使用PNG贴图。目标为`EntranceWhiteboxAssets/NaturalContours/`；角色只在预览中作比例参考，不烘进地形。该批只写新增资源，不全局SaveAssets、不改Game脚本、不打断用户Play。

9.6单位试做体量已被否定，首轮预览还漏继承了主角父级缩放4；错误6图及native-preview-review保留于`artifacts/natural-contour-blockout-20261102/`。创建脚本已补回缩放；`capture-natural-contours.csx`与`make-compact-contours.py`仅为尚未执行的后续准备，不据此声称比例正确或用户批准7.2单位方案。

用户要求先定规格，当前建议草案1U=3.2×3.2 Unity单位，即20×20个16逻辑像素格；主角H≈0.48，保持现有4倍作者根缩放。模块宽高为U整数倍，首批可用1×1、2×1、1×2、2×2。M01优先2×1；2×2内部安排两层、错层或复合空间，不把全部包围框挖成一个大空洞。常规局部净高约2.5～3.5H、小厅约3.5～4.5H为视觉比例建议，未成为人物通行门槛或正式数值。

同尺度图为`C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55/cave-module-scale-standard.html`。2×1和2×2使用同一尺度及真实主角，占格开关仅隐藏边界线；不是新洞形、正式碰撞或WFC验收。草案尚待讨论，停止按任意尺寸批制；实际状态归DEVELOPMENT。

用户补充入口必须表达地面进入洞穴后，同图M01改为露天地表直接经过洞檐，下行进入浅厅，再回升对接地下道路。露天部分直接包含在入口2×1内，上方局部空旷，不新增独立过渡块；6.4×3.2外框、角色尺寸及两图显示比例保持。普通地下2×2留作对照。修订前来源和新展示检查另存`artifacts/cave-entry-surface-20261102/`，不以旧图的回执证明新图通过。
