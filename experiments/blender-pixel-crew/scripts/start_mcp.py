"""供交互式 Blender 启动：载入样机面板，在回环地址开启已安装的 MCP。"""

import json
import os
import sys
from pathlib import Path

import addon_utils
import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
os.environ["DISABLE_TELEMETRY"] = "true"

if bpy.app.background:
    raise RuntimeError("MCP 依赖交互式 Blender 主循环；后台渲染请运行 run.ps1。")
addon_utils.enable("blender_mcp", default_set=False, persistent=True)
import blender_mcp
import control_panel

addon = bpy.context.preferences.addons["blender_mcp"]
addon.preferences.telemetry_consent = False
scene = bpy.context.scene
for option in ("polyhaven", "hyper3d", "sketchfab", "polypizza", "hunyuan3d"):
    name = "blendermcp_use_" + option
    if hasattr(scene, name):
        setattr(scene, name, False)
scene.blendermcp_port = 9876
server = getattr(bpy.types, "blendermcp_server", None)
if not server or not server.running:
    server = blender_mcp.BlenderMCPServer(host="127.0.0.1", port=9876)
    bpy.types.blendermcp_server = server
    server.start()
scene.blendermcp_server_running = server.running
if not server.running:
    raise RuntimeError("无法启动 MCP 9876 端口；检查是否已有另一个 Blender 实例。")
if "CrewRig" in bpy.data.objects:
    control_panel.register()
print("CREW_MCP_READY " + json.dumps({
    "host": server.host, "port": server.port, "model": bpy.data.filepath,
    "telemetry": False, "running": server.running,
}), flush=True)
