# 矿洞地图扩展方案包

**当前执行目标已按用户要求转为正式 Unity。** 五张扩展地图、现有采集与手电进入正式生成入口；详细完成边界见 [STATUS](STATUS.md)。原 H5 方案保留追溯，`prototype` 旧源码复制未继续开发。Unity 作者源位于 `Game/Assets/DarkNights/Res/Terrain/MineMaps`，在 Tuner 的地图输入中选择关卡矿洞资源。

本包位于新worktree的专属artifacts目录，继续独立H5实验。以06–10号地图为基础，把宽度与总高度分别扩展到原来两倍，同时保持24像素格、角色、跳跃、梯长和手电尺度。

- 阅读 [开发执行方案](开发执行方案.md) 或 [离线HTML](开发执行方案.html)。
- 字段与实施合同见 [地图扩展合同](地图扩展合同.md) 或 [离线HTML](地图扩展合同.html)。
- 当前进度见 [STATUS](STATUS.md)；输入身份见 [基线清单](references/baseline-manifest.json)。
- 原v0.4试玩的只读副本为 [基线试玩](references/h5-v0.4/dist/index.html)，它尚未包含扩展地图。
- 文档与源快照核验见 [本轮回执](evidence/plan-verification.json)。

方案与只读资料已经交付；prototype为后续开发的计划位置，本轮没有创建扩展版玩法代码或执行H5／Unity验收。五张图的开发顺序为08、09／10、06／07。

新worktree的分支是docs-20261011-mine-map-expansion-plan。原D盘实验的未提交源码不随Git worktree自动复制，本包通过文件SHA固定实际输入。references只读；后续源码、构建和新报告都留在本目录。

artifacts受仓库忽略规则管理，本包当前为本地成果，未提交、推送或归档。后续checkpoint应记录被忽略成果的独立保全方式与清单，不能仅以Git快照宣称整个包已经保全。
