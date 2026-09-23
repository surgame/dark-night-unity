# 原生对象主视图统一

日期：2026-09-14。分支：`codex/native-object-views`。本切片继续 YYGC 统一对象路线，消除正式实体 Prefab 上泛型 `ObjectView` 与 `NativeVisual` 并列存在、再用 `"visual"` 绑定回自身的重复结构。权威 Behaviour／State、数值、规则、网络协议、资源 GUID、场景布局和原素材均未改变。

## 最终合同

- `EntityView` 直接继承 YYGC `ObjectView`，集中持有拾取范围、头像、排序、状态／选择锚点和环境染色条目。
- 6 类单位使用 `ActorView`，持有影子、朝向、姿态根、服装、动画及尸体表现；5 类建筑使用 `BuildingView`，持有完成体、地基、动画及废墟表现；4 类工位使用 `WorksiteView`，持有资源变体和枯竭表现。
- 每个正式实体 Prefab 只有一个上述类别主视图。`ObjectInstance._view` 直接指向它，`EntityPresentationBehaviour` 从所属对象取得主视图并校验类别，不保留 `"visual"` 自绑定。
- `ObjectView.Bindings` 只用于主视图之外的真实依赖。15 个正式实体的绑定集合为空；Worker 过去重复保存的朝向、锚点和外观绑定也已删除。
- `EntityViewFactory` 继续通过正式 `ObjectDefinition` 创建被动建造预览、尸体和废墟。残骸能力以 `IRemnantView` 限定在角色和建筑主视图，不引入第二套对象或状态生命周期。
- `SpriteTintGroup` 将渲染器和作者基础色保存为配对的 `SpriteTintTarget`，避免两个平行数组在 Prefab 编辑后错位；每帧从基础色重算，不累计环境光或受击染色。

## 资源迁移

一次性 Editor 迁移在写入前核对全部 15 个旧 Prefab，复制 YYGC 主视图基础字段及原生表现引用，再重连 `ObjectInstance`、Pinewatch 标记和预览引用。实际结果为 15 个 Prefab、16 个场景放置、19 个旧 Prefab 绑定移除；差异复核又通过 Unity Prefab override API 删除了 5 条指向旧 Worker 组件的场景 override。迁移脚本和审计场景在成功后删除，不成为日常导入或构建入口。

最终静态结果：`ActorView` 6 个、`BuildingView` 5 个、`WorksiteView` 4 个；正式 Prefab 中旧 `NativeVisual`、泛型主视图、旧绑定、缺失脚本、空 `_view` 均为 0。Pinewatch 未改变放置参数或布局。

## 验证与边界

| 检查 | 结果 |
|---|---:|
| Unity 最终编译 | 通过，0 个编译错误 |
| `NativeArtTests` 最终资源保存／重开／装配 | 3/3 |
| `UnifiedObjectAssemblyTests` 新主视图装配合同 | 9/9 |
| `EntityPresentationTests` | 28/28 |
| `FormalObjectContentTests` | 3/3 |
| 完整 Editor／Play 批次 | 155/156 |
| 架构守卫 | 291 个手写文件、12 自测、0 错误 |

完整批次的唯一失败是未修改的 `NativeButtonThemeTests.InteractableChangesUpdateWithoutPointerMovement`：首次禁用按钮后，EditMode 的一帧等待没有调用 `NativePanelTheme.Update`，文字仍为正常色；隔离复跑为 5/6。该问题不读取对象主视图，也不影响上述相关测试通过结论，但本页不把完整批次写成全绿。

本切片没有修改 `D:\Developer\YYGC` 或锁定依赖，没有构建或覆盖 Mono Player，也没有执行 IL2CPP、双机器 LAN 或前台性能验收。此前 Player 和 155/155、77/77 证据仍只对应各自历史源码；M5 未完成状态保持。
