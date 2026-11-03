# 主角白模导入

2026-11-03起，当前动作选择唯一读取`AnimationSelection.json`，正式播放器使用`Spine/`内原生骨骼二进制与图集，来源读取`Spine/RuntimeManifest.json`。官方运行库按`tools/spine/dependency.lock.json`锁定。`SelectedFrames/`、旧Clip和各版Manifest保留为历史证据，已选主角不再用12fps整身图片播放。

当前站立为`ai_idel1_ok`，移动为用户在《整理主角 Spine 素材并制作预览》中选择的`ai_mie_run5_ok`（run5，完整2.4秒），跳跃为`ai_jump2_ok`并叠加`air_move_legs`／`air_idle_legs`。作者源为`美术素材/角色/主角/主角-腾空腿部.spine`；不得因旧脚本或缺图改回`ai_walk`及单张跳跃定格。确切用户消息与后续未定方案保存在清单和作者目录README中。

`SelectedFrames/`是旧95帧烘焙：站立46、移动29、两套腾空各10。当前骨骼版保留相同作者动作、去除jump根平移和0.12～0.6秒选定区间，逐渲染帧计算连续姿态，并由AnimationState做动作过渡；真实接地立即退出腾空，步频按实际位移／基础步速映射。此版尚未制作完整新攻击／死亡动作，也未启用ragdoll。

作者目录新增两张89×19透明眨眼附件，原spine引用可直接解析；当前派生idle的原第29／30帧不再含缺图框。其余尚缺的工具等附件沿原预览透明隐藏，仅处理派生播放数据；原作者工程、原10张图及旧导出保持。补图仍待用户视觉确认。

当前骨骼重建：先从上述作者源向新空批次`native/`导出JSON，再执行`tools/hero-whitebox/prepare_runtime_assets.py <批次绝对路径>`；等导入／编译屏障就绪后，通过单一Local Editor显式执行`HeroSpineAssetSetup.Migrate()`。旧PNG脚本只保留历史用途，不能覆盖当前骨骼绑定。

## 首版导入来源（保留）

作者唯一来源为仓库根目录的`美术素材/角色/主角/主角.spine`及其`images/`。本目录只保存该原版工程的运行导出，没有复制或修改两份`.spine`作者文件，也没有采用另一套人物概念图。

- `Export/Hero.json`、`Hero.atlas.txt`、`Hero.png`由本机Spine 4.3.26 Professional原生导出；骨架JSON保留原27段动画，图集页名统一为Hero.png。运行库接入时前两者仅改为`Hero.json.txt`／`Hero.atlas.source.txt`，字节与GUID保持，防止旧PMA／缺图数据触发自动运行资源装配；原Manifest中的名称保留为历史来源。
- `AnimationFrames/idle`为`ai_idel1_ok`的46帧，`walk`为`ai_walk`的10帧，均按12fps原生导出；`air`只取`ai_jump2_ok`的第3帧作腾空保持姿态，位置由正式运动状态推进，不重复播放作者跳跃位移。
- PNG只对原IDAT过滤流重新做无损压缩，不重绘、缩小、改色或生成替代图片。Unity使用Point、无mipmap、无有损压缩及sRGB输入，显示尺度沿用48逻辑像素，脚底按各帧实际透明边界对齐。
- `AnimationClips`为原生Unity sprite动画，使用非legacy、PPtrCurve精灵绑定和显式源时长，idle／move循环、jump保持腾空帧。CaveWhitebox及由其派生的MapModule HeroRig显式引用这些动画，原Worker Prefab／Definition不改；此导入没有新增Spine Unity运行依赖或修改锁定包。原有legacy片段缺少运行用绑定表，已通过Editor API保留GUID修正；共用表现按实际支撑选择腾空，不只在旧白模入口追加覆盖。
- `SourceManifest.json`记录作者12文件、原生导出、57帧及各自SHA-256、导出设置、脚底pivot与密度。作者源仍有15个缺失附件，主要为工具、背面采掘及表情，原生导出日志保留；未为此补画。

重建方式见仓库`tools/hero-whitebox/README.md`。外观只接入上述白模／模块研究入口，正式新局其他场景没有随之替换；动画机方案见`docs/ARCHITECTURE.md`的“主角与输入”，运行和人工验收状态以DEVELOPMENT为准。
