"""启用已安装的 Blender MCP；只修改该插件的启用项和遥测偏好。"""

import json

import addon_utils
import bpy

addon_utils.enable("blender_mcp", default_set=True, persistent=True)
addon = bpy.context.preferences.addons.get("blender_mcp")
if addon is None:
    raise RuntimeError("blender_mcp 未成功启用。")
addon.preferences.telemetry_consent = False
bpy.ops.wm.save_userpref()
print("CREW_MCP_ADDON " + json.dumps({
    "status": "passed", "blender": bpy.app.version_string,
    "addon_enabled": True, "telemetry_consent": False,
}), flush=True)
