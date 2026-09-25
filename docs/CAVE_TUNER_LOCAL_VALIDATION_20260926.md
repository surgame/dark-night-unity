# 地图即时刷新：Local 接入与验收记录

状态：部分完成。不得宣称全量修复或全量验收通过。

## 本次已经执行
- 在两个仓库创建 `fix-20260926-terrain-final-validation`，原分支不覆盖。
- Local 游戏接入此前隔离的 Tuner 输入泵送、最终相机确认和等价 CPU 优化。
- 原有未提交内容完整备份在 `artifacts/terrain-final-20260926/preexisting`。
- 修复 YYGC 注册表自动发现的程序集边界：独立 Sample 不再进入正式生成代码。
- 错误生成文件归档后，由 Unity 原生成器重新生成；保留原 `.meta` GUID。
- 使用已经运行的 Unity 6000.4.9f1 导入并执行真实 EditMode Test Runner。
- 首轮 34 项实际执行：32 通过、2 失败。原始 XML 保留，不用旧测试数字替代。

## 两项失败及修改
- `ActualPagesStayStaticAndDestructionIsLocal`：旧测试直接编辑只读块；改为冻结基线和源输入安装。
- `PreviewKeepsCameraPagesVisibleAcrossDirectionChanges`：旧测试共用相机和地形移动节点；改为独立宿主。
- 两项修改已写入，尚未复测。

## 已新增但未执行的测试
- `TerrainVisualAcceptanceTests`：真实 GPU 补丁、单格四输出、64 格 81 输出、连续绘制、撤销、真实相机回执和性能采样。
- `TerrainVisualTestScope`：隔离临时地图、GPU 读回和人工资源保护。
- 后续测试触发请求被工具安全检查阻止，未执行。新测试不计入通过数量。

## 明确保留的未完成项
- 跨资源 Mesh/岩壁/光照整批可见切换写入受阻；已经撤回未接线的前置改动。
- 当前保持原来的独立资源发布和最终稳定相机确认，不保证所有中间帧原子切换。
- PlayMode、Mono 构建、实际 Host/Client、弱网/重连、实际 P95 门槛本轮均未执行。
- 源包仍按用户当前直接引用 YYGC 工作区；旧来源锁与该引用的差异尚未收口。

证据：`docs/evidence/terrain-local-validation-20260926.json`；原始结果：`artifacts/terrain-final-20260926/terrain-baseline.xml`。
