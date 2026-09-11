# Blender 像素角色流程试验

2026-09-12 收尾。**技术管线通过，美术风格验证未通过。** 用户明确认为生成角色与参考图差距较大，因此停止继续扩展造型。这不是正式 Dark Nights 素材，也不计入 M3/M5 美术验收。

## 能力判断

Blender 可以复用骨架、姿势和服饰绑定，适合多动作、多换装的批量出帧。本试验不能证明自动建模能达到参考图的美术质量；MCP 和 Python 只是不同的操作入口，不能代替轮廓、比例、像素块、遮挡与动作设计。

纯 2D 也不意味着每套衣服都从零画完整角色。分层、局部替换和少量部件变形可以复用，但每个姿态仍需处理轮廓和遮挡。若参考风格优先，先制作认可的 2D 样稿，再选择 2D 分层或 3D 辅助流程更稳妥。用户随后要求单独试验 Aseprite 的空闲、行走、跳跃与 H5 预览。

## 已交付

- [可编辑模型](model/pixel_crew.blend)：一个 16 骨骼骨架，113 个有明确绑定的网格，九个共用 Action。
- 四套穿搭：工人、机械师、旅人、兔帽。头部、帽子、衣服、包、工具按部件集合切换。
- 空闲、行走、奔跑、跳跃、交互、欢呼、坐下、睡眠、受击；均为动作样机。
- [离线交互预览](preview/index.html)、透明 PNG、精灵表和切片元数据。共 584 帧：64 像素 576 动画帧，加 32/48 像素 8 张站立样本。
- Blender MCP 1.9.1 已安装、启用、登记，并完成真实 stdio 初始化、28 个工具发现、场景读取、临时对象增删与视口截图。

## 使用

环境：Windows、Blender 4.4.3、Python 3.13、Pillow 12.0.0、PowerShell 7。默认 Blender 路径为 D:\Program Files\Blender\blender.exe，可通过参数覆盖。

在本目录执行：

~~~powershell
.\open-blender.ps1
~~~

Blender 右侧 N 面板的 Pixel Crew 可切换穿搭、动作、视角和分辨率；播放按钮检查模型动画，F12 查看原始渲染。最终色板、硬透明和外轮廓在导出后处理阶段加入。

编辑模型、材质或 Action 后先在 Blender 保存，然后：

~~~powershell
.\run.ps1
.\run.ps1 -Presets worker -Actions idle,walk,jump -Directions right -Sizes 48
.\run.ps1 -ComparisonSizes 32,48
~~~

结果写入新的时间戳目录 renders/。不裁切帧，不逐帧重心对齐，脚底原点固定；左右分别渲染，不镜像替换左右手工具。atlas.json 的 rect 使用左上角像素坐标，pivot_unity 使用左下角归一化坐标。

日常导出读取已保存的模型，不重建它。只有明确指定 -Initialize 和一个新模型路径，才执行一次性生成器；模型目标目录必须为空。

~~~powershell
.\run.ps1 -Initialize -Model .\local\new-model\pixel_crew.blend
~~~

pipeline.json 是初始化配方，生成时嵌入 .blend；修改配方不会自动覆盖已经手工编辑的模型。运行时相机和部件切换读取模型内的配置。

## MCP

~~~powershell
.\setup-mcp.ps1 -CheckOnly
.\setup-mcp.ps1
~~~

setup-mcp 使用已安装的 uv 和 Codex CLI，锁定包和插件哈希，保留配置与 Blender 偏好备份。遥测关闭，监听仅使用 127.0.0.1:9876；不启用云端模型、素材下载或任何收费服务。

配置已写入用户 Codex config.toml，使用 MCP 可执行文件的绝对路径。当前 Codex 工具目录可能需要在 MCP 设置中重载，或下次启动时加载新服务器；本次没有为了加载工具而重启正在运行的 Codex。

MCP 需要交互式 Blender 主循环。关闭 Blender 后连接随之结束；重新运行 open-blender.ps1 即可。后台批量导出使用 Blender CLI，不依赖 MCP。一次只启动一个监听 9876 的 Blender 实例。

来源：[Blender MCP](https://github.com/ahujasid/blender-mcp)、[Codex 官方 MCP 配置](https://developers.openai.com/codex/mcp/)。
MCP 为第三方集成。Python 包 1.9.1、插件声明 1.6/协议 5、MCP SDK 1.30.0 是不同版本字段。

## 验证与剩余边界

- [像素检查](preview/verification.json)：584 帧固定画布、无越界、非空、36 色固定色板、透明 alpha 为 0/255，全部通过。
- 首轮睡眠的 64 帧触及底边，修正睡眠 Root 高度后仅补渲染这 64 帧，其余 520 帧复用。frames.json 逐帧记录来源模型哈希，避免把混合验证来源写成同一次全量渲染。
- [模型检查](preview/model-verification.json)：所有网格绑定同一骨架、归一化刚性权重、九组动作实际变化、循环闭合、在隔离副本中编辑/保存/重开通过。
- [MCP 检查](preview/mcp-verification.json)：真实协议调用通过；不是仅写入配置文件。
- 轮廓和动作质量仍未达到参考要求。刚性分段权重没有布料或软变形；脚步锁定、穿插、表情和逐帧像素稳定性尚未完成美术验收。
- 使用法线分档平涂来减少低采样杂色。当前材料不以场景灯光驱动色块；美术可编辑材质内的方向与色板。
- 未导入正式 Unity 内容、未改原始素材、未改变游戏规则，也没有运行 Unity 构建。

重跑日志与中间版本保留在仓库忽略的 artifacts/blender-pixel-crew/；验证后的模型、脚本和预览在本目录。
