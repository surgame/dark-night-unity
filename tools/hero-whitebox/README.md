# 主角白模资源重建

当前选择由`Game/Assets/DarkNights/Res/Objects/Hero/AnimationSelection.json`锁定，用户明确选择run5／jump2及后续腾空腿姿，不能再用初版ai_walk／跳跃定格。

2026-11-03起，已选主角正式播放链改为Spine runtime；下述PNG／Clip流程仅保留历史重建。骨骼资源重建先按`tools/spine/dependency.lock.json`准备官方包，再从原作者工程向新的空批次`native/`导出JSON，执行`python tools/hero-whitebox/prepare_runtime_assets.py <批次绝对路径>`。脚本保留完整骨骼／IK／网格，把已选动作重命名为idle／move／jump_move／jump_idle，只在派生输入隐藏缺图并去除jump根平移，调用本机Spine原生binary导出和straight-alpha／Bleed图集打包；输出`Res/Objects/Hero/Spine`，已有目录拒绝覆盖。RuntimeManifest记录源图、选择和作者SHA-256，派生JSON以`.json.txt`保留以避免重复触发骨骼导入。新资源meta由Unity生成。

导入／编译就绪后，单一Local Editor显式执行`DarkNights.Editor.HeroSpineAssetSetup.Migrate()`，迁移前拒绝脏场景，保存本批before-binding副本；已迁移对象跳过或清理继承的旧播放器绑定，保留Prefab／Scene GUID、旧图片和Clip。新主角只使用SpineActorAnimation；未来物理不另接独立动画／业务时钟，具体控制权与同步边界见ARCHITECTURE。

1. 从`美术素材/角色/主角/主角-腾空腿部.spine`向本次新的空批次`author29/`执行Spine原生JSON导出。
2. 执行`python tools/hero-whitebox/prepare_selected_assets.py <批次绝对路径>`。脚本读取选择清单，保留原作者数据，只在派生输入隐藏缺图、去除跳跃根位移并合成已有腿姿，调用Spine原生PNG导出；按源时间线取时长，run5为2.4秒。已有SelectedFrames拒绝覆盖，重制先按工程保全流程保留现有帧、manifest与GUID。
3. 导入／编译就绪后，以Unity CLI eval执行`bind_selected.cs.txt`，批量设置原生Sprite、更新原三个clip而保留GUID，并增加两套腾空clip和HeroRig显式绑定。旧白模Scene绑定迁移只在非Play状态执行，已保存回执与脚本在`artifacts/hero-animation-selection-20261103/`。

下述为首版历史重建流程。旧脚本检测到选择清单时拒绝执行，避免回退到错误素材。

原作者工程位于`美术素材/角色/主角/主角.spine`，保留原字节。当前导入输出为`Game/Assets/DarkNights/Res/Objects/Hero/`，来源与设置由该目录SourceManifest.json记录。

1. 使用已安装的Spine 4.3.26 Professional，以绝对路径读取原作者工程，向本次新的空批次目录执行`-i <主角.spine> -o <批次>/hero-native-export -e json+pack`。
2. 从SourceManifest的exportSettings分别写出`hero-idle-png-settings.json`、`hero-walk-png-settings.json`、`hero-jump-png-settings.json`。按同样的`-i <主角.spine> -o <批次>/hero-<idle|walk|jump>-png -e <对应设置>`导出PNG；Spine把该output视为文件前缀，帧直接位于批次目录。
3. 执行`python tools/hero-whitebox/prepare_assets.py <批次绝对路径>`，只向尚不存在的Hero目录打包原生导出、57张无损压缩帧和来源清单。已有目录时脚本拒绝覆盖；更新必须另行保全并审查原资产与GUID，不能先删除旧资源绕过检查。
4. 等同批资源导入和脚本编译就绪后，在单一Local Editor以Unity CLI eval执行`bind_scene.cs.txt`。该有限脚本只处理已保存的CaveWhitebox，拒绝重复建立动画目录或覆盖已有Author Hero节点，生成三份非legacy原生动画，以PPtrCurve绑定精灵并设置源时长与循环，再保存场景内显式绑定。源Worker资源不改。

生成输入是原作者工程和10张共用PNG。输出不使用生图，也不增加骨骼运行库；平地、角色位置和相机布局仍归CaveWhitebox原生场景。本片原始导出、首次未导入脚本／命名空间诊断、旧Pipeline五秒超时及实际完成回执均保留于`artifacts/playable-cave-whitebox-20261102/`，不因超时重复生成。
