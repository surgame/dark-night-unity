"""通过真正的 MCP stdio 会话检查工具发现、读取、可逆编辑和视口截图。"""

import argparse
import asyncio
import base64
import json
import os
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


async def check(args):
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    parameters = StdioServerParameters(
        command=args.server, args=[],
        env=dict(os.environ, DISABLE_TELEMETRY="true", BLENDER_HOST="127.0.0.1",
                 BLENDER_PORT="9876"),
    )
    async with stdio_client(parameters) as streams:
        async with ClientSession(*streams) as session:
            initialized = await session.initialize()
            listing = await session.list_tools()
            names = sorted(tool.name for tool in listing.tools)
            required = {"get_scene_info", "execute_blender_code", "get_viewport_screenshot"}
            if not required.issubset(names):
                raise AssertionError("Missing MCP tools: " + str(required - set(names)))
            info = await session.call_tool("get_scene_info", {
                "user_prompt": "Read the Pixel Crew prototype scene to verify MCP installation."})
            if info.isError:
                raise AssertionError(str(info))
            code = "\n".join([
                "import bpy, json",
                "before = len(bpy.data.objects)",
                "probe = bpy.data.objects.new('CREW_MCP_TEMP_PROBE', None)",
                "bpy.context.scene.collection.objects.link(probe)",
                "bpy.data.objects.remove(probe, do_unlink=True)",
                "rig = bpy.data.objects.get('CrewRig')",
                "print(json.dumps({'mcp_round_trip': len(bpy.data.objects) == before,",
                "'objects': before, 'rig': rig.name if rig else None,",
                "'bones': len(rig.data.bones) if rig else 0,",
                "'actions': sorted(a.name for a in bpy.data.actions),",
                "'model': bpy.data.filepath,",
                "'telemetry': bpy.context.preferences.addons['blender_mcp'].preferences.telemetry_consent}))",
            ])
            result = await session.call_tool("execute_blender_code", {
                "code": code, "user_prompt": "Create and remove one temporary empty to verify local editing."})
            if result.isError:
                raise AssertionError(str(result))
            text = "\n".join(item.text for item in result.content if item.type == "text")
            if '"mcp_round_trip": true' not in text and '\\"mcp_round_trip\\": true' not in text:
                raise AssertionError(text)
            screenshot = await session.call_tool("get_viewport_screenshot", {
                "max_size": 1400, "user_prompt": "Capture the editable Pixel Crew model viewport."})
            images = [item for item in screenshot.content if item.type == "image"]
            if screenshot.isError or not images:
                raise AssertionError(str(screenshot))
            suffix = ".png" if images[0].mimeType == "image/png" else ".jpg"
            screenshot_path = output / ("blender-viewport" + suffix)
            screenshot_path.write_bytes(base64.b64decode(images[0].data))
            report = {
                "status": "passed", "protocol": initialized.protocolVersion,
                "server": initialized.serverInfo.model_dump(), "tool_count": len(names),
                "checks": ["initialize", "list_tools", "get_scene_info",
                           "execute_code_create_remove", "get_viewport_screenshot"],
                "probe_result": text, "screenshot": screenshot_path.name,
                "client_catalog": "Config registered; active Codex task may need MCP reload.",
            }
            (output / "mcp-verification.json").write_text(
                json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps(report, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    asyncio.run(asyncio.wait_for(check(args), timeout=90))


if __name__ == "__main__":
    main()
