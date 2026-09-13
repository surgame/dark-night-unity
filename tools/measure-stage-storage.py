"""只读盘点阶段产物；跳过重解析点，不执行清理，不把父子目录重复计总。"""

import argparse
import csv
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
from datetime import datetime, timezone


DENIED = [
    "Game/Library/Bee/artifacts/WinPlayerBuildProgram",
    "artifacts/migration/player-mono/DNights_BurstDebugInformation_DoNotShip",
    "artifacts/yygc-unified/u6/source",
    "artifacts/yygc-unified/u6/locked-archives",
    "artifacts/yygc-unified/u6/player-mono-compressed/DNights_BurstDebugInformation_DoNotShip",
    "artifacts/m5-ui/player-mono/DNights_BurstDebugInformation_DoNotShip",
    "artifacts/m5-results/player-mono/DNights_BurstDebugInformation_DoNotShip",
    "artifacts/m5-world/player-mono/DNights_BurstDebugInformation_DoNotShip",
]
KEEP_PLAYERS = {
    "artifacts/m5-world/player-mono",
    "artifacts/yygc-unified/u6/player-mono-compressed",
}
CACHE_NAMES = {
    "bin", "obj", "__pycache__", "temp", "logs", "library", "build", "builds",
    "packagecache", "shadercache", "burstcache", "il2cppbuildcache",
    "artifacts", "scriptassemblies", "bee", "buildplayerdata",
}


def inside(path, parent):
    return path == parent or path.startswith(parent.rstrip("/") + "/")


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def native_path(path):
    value = str(path)
    if os.name == "nt":
        return "\\\\?\\UNC\\" + value[2:] if value.startswith("\\\\") else "\\\\?\\" + value
    return value


def measure(root, nodes, skipped, errors):
    """单次遍历汇总逻辑文件大小；缓存子目录结果供后续所有表复用。"""
    total = {"bytes": 0, "files": 0, "directBytes": 0, "directFiles": 0, "directories": 0,
             "readErrors": 0, "skippedReparsePoints": 0}
    try:
        info = os.lstat(native_path(root))
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            skipped.append(str(root))
            total["skippedReparsePoints"] += 1
            return total
        with os.scandir(native_path(root)) as entries:
            for entry in entries:
                entry_path = root / entry.name
                try:
                    info = entry.stat(follow_symlinks=False)
                    if entry.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400:
                        skipped.append(str(entry_path))
                        total["skippedReparsePoints"] += 1
                    elif entry.is_dir(follow_symlinks=False):
                        child = measure(entry_path, nodes, skipped, errors)
                        for key in ("bytes", "files", "directories", "readErrors", "skippedReparsePoints"):
                            total[key] += child[key]
                        total["directories"] += 1
                    elif stat.S_ISREG(info.st_mode):
                        total["bytes"] += info.st_size
                        total["directBytes"] += info.st_size
                        total["files"] += 1
                        total["directFiles"] += 1
                except OSError as error:
                    errors.append({"path": str(entry_path), "error": str(error)})
                    total["readErrors"] += 1
    except OSError as error:
        errors.append({"path": str(root), "error": str(error)})
        total["readErrors"] += 1
    nodes[root.as_posix()] = total
    return total


def decision(relative, external=False):
    if external:
        return "shared_unconfirmed", "其他项目可能共用；仅列账，确认归属与占用后再由维护者清理"
    if relative in DENIED:
        return "policy_denied", "自动审批已拒绝；不重试、不经父目录或其他工具绕过"
    if any(inside(relative, target) for target in DENIED):
        return "inside_denied", "属于已拒绝目录的子产物；只列细项，不单独删除"
    if relative in KEEP_PLAYERS:
        return "retain_acceptance", "保留当前验收 Player 或协议 7 性能对照；内含拒绝清理的调试目录"
    if any(inside(relative, target) for target in KEEP_PLAYERS):
        return "retain_acceptance", "属于后续验收所需 Player，保持完整文件哈希"
    if any(inside(target, relative) for target in DENIED):
        return "contains_denied", "含已拒绝子项，不能整体删除；非受限子项另按条件列账"
    parts = Path(relative).parts
    name = parts[-1].lower() if parts else ""
    if ".git" in parts:
        return "retain_source", "Git 对象／工作树登记属于恢复来源；不作为普通缓存清理"
    if any(inside(relative, target) for target in ("Game/Assets", "Game/Packages", "Game/ProjectSettings", "Game/UserSettings", "Game/.idea")):
        return "retain_source", "正式资源、依赖配置或用户设置；不能按内部 bin／build 等目录名判断可清理"
    if "backup" in name and "dontship" not in name:
        return "retain_source", "可能含依赖补丁或场景恢复备份；先确认有另一份完整来源，不按缓存清理"
    if relative.startswith(".deps/") and name in {"bin", "obj", "__pycache__"}:
        return "dependency_intermediate", "依赖自带工具／生成器产物；先核对源文件、输出 DLL 依赖和本地修改，不自动清理"
    if name in {"bin", "obj", "__pycache__"}:
        return "rebuildable", "编译／Python 缓存；确认无占用及所需工具输出已保存后可清理"
    if relative.startswith(".deps/") and len(parts) == 2:
        return "dependency_checkout", "锁定依赖；先确认未提交修改、本地提交及生成 DLL，后续构建仍需重建"
    if name == "library" or "/Library/" in relative:
        return "reimportable", "Unity 导入／编译缓存；Editor 关闭后可重建，后续导入有时间与空间代价"
    if relative.startswith("Game/") and name in {"temp", "obj", "logs", "build", "builds"}:
        return "rebuildable", "本机 Unity 中间产物；先保存日志与场景恢复备份，确认无活动进程"
    if relative.startswith("Game/"):
        return "retain_source", "正式资源、配置、用户编辑器设置或生成输入；不可按目录名整批清理"
    if relative.startswith("experiments/") or relative.startswith("tools/"):
        return "retain_source", "研究／工具源文件；仅其明确的 bin、obj、缓存等派生子项可清理"
    if relative.startswith("artifacts/"):
        if name.startswith(("player-", "sample-mono")):
            return "archive_then_clean", "历史构建；确认无后续对照需求并保留输入、文件清单和报告后可清理"
        return "archive_then_clean", "含诊断、截图、日志、测试存档或临时源码；先归档必要证据和唯一源文件"
    return "retain_source", "仓库源码／配置；不属于自动清理范围"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    if not output.is_relative_to(repo):
        raise SystemExit("盘点输出必须位于当前工作区内")
    output.mkdir(parents=True, exist_ok=True)
    local = Path(os.environ["LOCALAPPDATA"])
    profile = Path(os.environ["USERPROFILE"])
    external = [local / "Temp" / name for name in ("MSBuildTemp", "NuGetScratch", "Unity", "UnityCBMCrashes")]
    external += [local / "Unity/cache", local / "NuGet", profile / ".nuget/packages"]
    nodes, skipped, errors, roots = {}, [], [], []
    for root in [repo] + external:
        roots.append({"path": str(root), "exists": root.exists()})
        if root.is_dir():
            measure(root, nodes, skipped, errors)
    tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=repo).decode("utf-8").split("\0")
    selected = set()
    for key in nodes:
        path = Path(key)
        if not path.is_relative_to(repo):
            if path in external:
                selected.add(key)
            continue
        relative = path.relative_to(repo).as_posix()
        parts = path.relative_to(repo).parts
        name = path.name.lower()
        depth = len(parts)
        top_row = depth <= 1 or (depth == 2 and parts[0] in {"artifacts", ".deps", "Game", "experiments"})
        selected_cache = name in CACHE_NAMES and name != "artifacts"
        selected_build = name == "player" or name.startswith(("player-", "sample-mono")) or "donotship" in name or "dontship" in name
        selected_intermediate = name in {"source", "source-clean", "clean-source", "locked-archives", "saves"}
        library_detail = (depth == 3 and parts[:2] == ("Game", "Library")) or relative in DENIED
        isolated_detail = relative.startswith("artifacts/yygc-unified/u6/source/") and depth in {6, 7} and name in {"game", "library", "packages", ".git"}
        if top_row or selected_cache or selected_build or selected_intermediate or library_detail or isolated_detail:
            selected.add(key)
    rows = []
    for key in sorted(selected, key=str.casefold):
        path = Path(key)
        is_external = not path.is_relative_to(repo)
        relative = path.relative_to(repo).as_posix() if not is_external else path.as_posix()
        status, condition = decision(relative, is_external)
        ancestors = [p for p in path.parents if p.as_posix() in selected]
        parent = ancestors[0] if ancestors else None
        rows.append({"path": relative, "absolutePath": str(path), **nodes[key],
                     "status": status, "condition": condition,
                     "trackedFiles": sum(1 for item in tracked if item and (relative == "." or inside(item, relative))) if not is_external else None,
                     "parentRow": (parent.relative_to(repo).as_posix() if parent.is_relative_to(repo) else parent.as_posix()) if parent else None,
                     "external": is_external})
    denied = [{"path": item, **nodes.get((repo / item).as_posix(), {"bytes": 0, "files": 0}),
               "exists": (repo / item).is_dir()} for item in DENIED]
    by_status = {}
    # 同一状态的父子项只取最外层；不同状态之间也不可直接相加。
    for status in sorted({row["status"] for row in rows}):
        group = [row for row in rows if row["status"] == status]
        unique = [row for row in group if not any(other is not row and inside(row["path"], other["path"]) for other in group)]
        by_status[status] = {"rows": len(group), "outermostBytes": sum(row["bytes"] for row in unique),
                             "note": "状态之间可能重叠；不可跨状态相加作为预计释放量"}
    record = {"schemaVersion": 1, "utc": datetime.now(timezone.utc).isoformat(), "readOnly": True,
              "workspace": str(repo), "roots": roots, "rows": rows, "deniedTargets": denied,
              "deniedDistinctBytes": sum(item["bytes"] for item in denied), "statusSummary": by_status,
              "reparsePointsSkipped": skipped, "errors": errors,
              "space": {drive: shutil.disk_usage(drive + ":/")._asdict() for drive in ("C", "D")},
              "measurement": "Logical file bytes, not allocated disk clusters. Reparse points excluded; parent/child rows overlap. No deletes or process termination."}
    write_json(output / "storage-inventory.json", record)
    write_json(output / "storage-directories.json", nodes)
    with (output / "storage-inventory.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    largest = sorted([row for row in rows if row["path"] in {"Game/Library", "artifacts", ".deps"} or row["external"]], key=lambda row: row["bytes"], reverse=True)
    print(json.dumps({"rows": len(rows), "scannedDirectories": len(nodes), "errors": len(errors),
                      "skippedReparsePoints": len(skipped), "deniedBytes": record["deniedDistinctBytes"],
                      "largest": [{"path": row["path"], "bytes": row["bytes"]} for row in largest]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
