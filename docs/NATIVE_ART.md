# 原生外观首批实施

2026-09-12，基于正式网络提交 `ccfcd3d`。这是 A3／B 的外观基础批次，M2 可操作闭环与 M3 全关卡验收仍未完成。

已实施：

- 原始清单的 551 项素材复制到 `Res/Art/Original`，原清单字节及每项 SHA-256 均保留；75 组精灵、9 项声音已导入。声音播放尚未接线。
- 6 类角色、5 类建筑、4 类工位拥有原生 Prefab、15 项内容映射及 DefinitionReference；总定义数 17（另含会话和连接）。Worker 原有 GUID 和四项锚点绑定保留，增加显式 `visual` 绑定。
- 32 段原生 AnimationClip 保留源帧序、精灵偏移、可见性、循环及精确时长。使用 Animator 支持精灵引用轨道，按权威 ActionTime 采样；攻击只改变姿势，不产生伤害事件。
- Pinewatch 保存了 16 个布局预览实例、五层视差背景、天空、土壤、草地与墓碑。未进入 Play 即可查看；进入 Play 关闭编辑预览，由 YYGC 定义工厂按客户端副本创建全部 17 个初始实体（含农田派生工位）。火把、灯光、月亮、萤火虫和地面细节仍待接入。
- 客户端异步创建同时检查本地连接代次、epoch、实体需求及种类；断开释放全部对象，再连接重新装配。当前按完整投影更新位置，平滑插值、死亡效果、箭矢表现、选择及操作 UI 尚待完成。

坐标统一为 100 像素／米，精灵左上原点，Godot Y 向下偏移转换为 Unity Y 向上。场景布局根缩放只影响编辑显示，标记局部像素坐标不变；导出沿父链计算，避免世界矩阵浮点消去造成冻结布局或握手摘要变化。

验证：551 项源与目标哈希、像素导入参数、15 项定义绑定通过；32 段动画实际采样 583 个关键帧通过。首轮完整 Editor 45 项中 44 项通过，发现缺少 Animator 且 legacy Clip 不更新精灵轨道；修正该批新资源后重跑 NativeArt 两项均通过，其余 43 项复用。场景装配后冻结布局测试另行通过。Editor 实际 Host Ready、17 个视图、断开归零、再连恢复 17 个已验证。截图在本机 `artifacts/migration/native-preview-1280.png` 与 `native-host-1280.png`；后者是指定尺寸摄像机渲染，未固定实际 Game View 视口，不用于跨引擎像素签署。本批尚未重建 Player。

首版迁移输入由 `python tools/prepare-art.py --source ../projects --output <空目录>` 导出；工具先验证全部源，输出 Original 和视觉输入。已冻结的视觉输入保存在 `Scripts/Editor/ArtInput.json`，含源场景和动画哈希。首次安装通过 `Dark Nights/Content/Install Initial Native Art` 和 `Install Initial Pinewatch Visuals` 完成，已安装项目不要重跑。普通导入／构建不读取旧工程，不调用初始化器；后续直接编辑原生资产，生成脚本只用于新空目录首版迁移。
