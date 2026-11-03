# 以原主角反推洞穴地图风格

本候选以用户指定的`../../角色/主角/`为风格起点，保留大圆头、浅青平涂、细小身体和手绘黑线的视觉特征，让环境减少细碎纹理、采用大岩块与分层剪影。实际状态及后续评审统一见[DEVELOPMENT](../../../docs/DEVELOPMENT.md)。

| 图像 | 设计方向 | 评审重点 |
| --- | --- | --- |
| [01-flat-cave.png](01-flat-cave.png) | 清爽平涂洞穴；灰褐岩面、浅砂路面、较亮蓝色远景 | 人物与环境轮廓统一程度、道路辨识度；整体气氛偏轻松 |
| [02-dark-cave.png](02-dark-cave.png) | 幽暗探险洞穴；炭褐岩面、赭黄路面、深蓝洞室 | 深度与压迫感、浅青人物显著性；黄色路面可继续降低亮度 |
| [03-open-mineral-hall.png](03-open-mineral-hall.png) | 第一套延展：宽阔矿厅，高拱顶、大岩柱、右侧露矿 | 开阔空间的层次、矿簇位置与人物辨识 |
| [04-winding-passage.png](04-winding-passage.png) | 第一套延展：低顶起伏洞道，局部收窄与右侧亮洞口 | 岩体占比增大后的道路辨识、封闭空间气氛 |
| [05-tiered-cavern.png](05-tiered-cavern.png) | 第一套延展：左右错层岩台、下层地面与对壁矿簇 | 平台厚度、落差与前后层次，不代表通行已验 |
| [06-near-wall-mining.png](06-near-wall-mining.png) | 玩法导向：近壁矿窝、嵌岩露矿、断续沙土与手电照明，两人同行 | 挖墙发现的表达、前景与后壁区分、局部照明；未知矿体不透视 |

两图原生尺寸均为1672×941，保持生成PNG原字节。使用内置`image_gen.imagegen`，提示目标尺寸1536×864与实际输出尺寸分别记录。完整提示见[prompts.json](prompts.json)，制作前用途、共边、透明、形状精度和方式理由见[brief.json](brief.json)，输入／输出身份及原生成路径见[manifest.json](manifest.json)。

用户认可第一套后，追加03～05三张效果图，同样使用内置生图并保持PNG原字节，原生尺寸依次为1672×941、1672×940、1672×941。第一套成图作为画法参考，原主角比例图继续作为人物身份参考。制作约束与完整提示见[expansion-prompts.json](expansion-prompts.json)，尺寸、来源及SHA-256见[expansion-manifest.json](expansion-manifest.json)。这批改变空间构图，未改美术基准；各图人物比例略有漂移，未统一真实镜头标尺。

生成参考1为既有WFC美术包的原主角组装比例图，参考2仅用于横版洞穴的大致构图；原作者`head.png`和`body.png`另作只读画法核对。没有采用另一聊天新设计的人物。原图只保留在原位置，本目录不重复保存来源。

人物是AI参考重绘，不能保证与原Spine精灵逐像素一致；场景也未严格锁定原几何。两图仍有少量柔和明暗，若推进正式平涂资产需继续约束。当前用于整体画法和气氛比较，不是无缝图块、WFC形状目录或正式地图。未导入Unity，未更改原Spine、原PNG、碰撞或游戏数值。

06按游戏设计评审改为近壁采掘场景，内置生图初稿保留为`06-near-wall-mining-draft.png`；初稿连续黄色路面和光束方向偏离目标，修正版将路面改为裸岩夹断续沙土，光束朝向采掘面，补局部凿痕。两图均为1672×941原生PNG。完整初稿提示、制作约束见[mining-concept-prompt.json](mining-concept-prompt.json)，修正提示见[mining-refinement-prompt.json](mining-refinement-prompt.json)，来源与哈希见[mining-concept-manifest.json](mining-concept-manifest.json)。

06只展示已经露出的少量矿物，不公开壁内矿量。两人姿势仅为同行场景，不规定必须由队友照明才能采矿。生成图未准确实现全部构图要求：侧向入口不明确、远景仍偏亮、后壁纹理尚可简化；工具距离、镜头尺度、格级破坏与人物遮光也未作实机核对。
