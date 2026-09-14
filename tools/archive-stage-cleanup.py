"""2026-09-14 阶段清理：限定输入、生成 ZIP 和 SHA-256 清单；本脚本不删除源文件。"""

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import zipfile


ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "artifacts/_archives/stage-cleanup-2026-09-14"
SOURCE = "artifacts/yygc-unified/u6/source"
PLAYERS = ["artifacts/m5-world/player-mono",
           "artifacts/yygc-unified/u6/player-mono-compressed"]
REIMPORTABLE = [SOURCE + "/Game/Library/" + name for name in (
    "APIUpdater", "Artifacts", "Bee", "BuildCache", "BuildPlayerData",
    "BurstCache", "PackageCache", "PlayerDataCache", "ScriptAssemblies",
    "Search", "ShaderCache", "SplashScreenCache", "TempArtifacts", "UCBPBlobStorage")]


def inside(path, parent):
    return path == parent or path.startswith(parent.rstrip("/") + "/")


def native(path):
    return "\\\\?\\" + str(path) if os.name == "nt" else str(path)


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha_stream(stream):
    result = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        result.update(chunk)
    return result.hexdigest()


def sha(path):
    with open(native(path), "rb") as stream:
        return sha_stream(stream)


def entries(relative):
    """拒绝越界或重解析点；使用长路径枚举，不漏掉隐藏文件。"""
    path = ROOT / relative
    if not path.resolve().is_relative_to(ROOT) or path.resolve() == ROOT:
        raise RuntimeError("Invalid target: " + relative)
    for parent in [path, *path.parents]:
        info = os.lstat(native(parent))
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise RuntimeError("Reparse point: " + str(parent))
        if parent == ROOT:
            break
    pending = [path]
    files, directories = [], []
    while pending:
        current = pending.pop()
        info = os.lstat(native(current))
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise RuntimeError("Reparse point: " + str(current))
        rel = current.relative_to(ROOT).as_posix()
        if stat.S_ISDIR(info.st_mode):
            directories.append(rel)
            with os.scandir(native(current)) as children:
                pending.extend(current / child.name for child in children)
        elif stat.S_ISREG(info.st_mode):
            files.append({"path": rel, "bytes": info.st_size, "mtimeNs": info.st_mtime_ns})
        else:
            raise RuntimeError("Unsupported file: " + rel)
    return sorted(files, key=lambda row: row["path"]), sorted(directories)


def git_state(path):
    def run(*args):
        return subprocess.check_output(["git", "-C", str(path), *args], encoding="utf-8")
    return {"head": run("rev-parse", "HEAD").strip(),
            "status": run("status", "--porcelain=v1"),
            "diffSha256": hashlib.sha256(run("diff", "--binary", "HEAD").encode()).hexdigest()}


def prepare():
    if OUTPUT.exists():
        raise RuntimeError("Existing output; inspect it before resuming: " + str(OUTPUT))
    OUTPUT.mkdir(parents=True)
    inventory = json.loads((ROOT / "docs/evidence/stage-cleanup-inventory-2026-09-14.json").read_text(encoding="utf-8-sig"))
    names = [row["path"] for row in inventory["scan"]["rows"]
             if row["path"].startswith("artifacts/") and row["path"].count("/") == 1]
    major = ["lan-sample", "migration", "m5-ui", "m5-results", "m5-world", "yygc-unified"]
    groups = [{"name": name, "roots": ["artifacts/" + name]} for name in major]
    groups[4]["exclude"] = [PLAYERS[0]]
    groups[5]["exclude"] = [PLAYERS[1], SOURCE]
    misc = [path for path in names if path.split("/")[1] not in major]
    misc += [path.relative_to(ROOT).as_posix() for path in (ROOT / "artifacts").iterdir() if path.is_file()]
    groups.insert(0, {"name": "art-and-probes", "roots": sorted(misc)})
    groups.append({"name": "isolated-source", "roots": [SOURCE], "discard": REIMPORTABLE})
    tools = [row["path"] for row in inventory["scan"]["rows"]
             if row["status"] == "rebuildable" and row["path"].startswith(("tools/", "experiments/"))]
    tools += [".deps/YYGC/.artifacts"]
    tools += [".deps/YYGC/tools/NetworkValidation~/" + project + "/" + folder
              for project in ("VitalRouterFix", "VitalRouterFix.Tests") for folder in ("bin", "obj")]
    groups.append({"name": "tool-intermediates", "roots": [path for path in tools if (ROOT / path).exists()]})
    tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode().split("\0")
    baseline = [{"path": path, "sha256": sha(ROOT / path)} for path in tracked if path]
    save(OUTPUT / "protected-inputs-before.json", baseline)
    dependencies = {path: git_state(ROOT / path) for path in (".deps/YYGC", ".deps/YYGC-unified", ".deps/FishNet", SOURCE)}
    save(OUTPUT / "dependency-state-before.json", dependencies)
    spec = importlib.util.spec_from_file_location("processes", ROOT / "tools/stage-cleanup-processes.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    save(OUTPUT / "processes-before.json", module.snapshot())
    for group in groups:
        all_files, directories = [], []
        for path in group["roots"]:
            files, dirs = entries(path)
            all_files.extend(files)
            directories.extend(dirs)
        excluded = group.get("exclude", [])
        discarded = group.get("discard", [])
        selected = [row for row in all_files if not any(inside(row["path"], path) for path in excluded)]
        archived = [row for row in selected if not any(inside(row["path"], path) for path in discarded)]
        for row in archived:
            row["sha256"] = sha(ROOT / row["path"])
        group.update(files=len(selected), bytes=sum(row["bytes"] for row in selected),
                     archivedFiles=len(archived), archivedBytes=sum(row["bytes"] for row in archived))
        archived_paths = {row["path"] for row in archived}
        manifest = {"roots": group["roots"], "files": archived,
                    "directories": [path for path in directories if not any(inside(path, root) for root in excluded + discarded)],
                    "discardedReimportable": [row for row in selected if row["path"] not in archived_paths]}
        save(OUTPUT / (group["name"] + ".manifest.json"), manifest)
        print(json.dumps({"prepared": group["name"], "files": group["files"], "bytes": group["bytes"],
                          "archivedBytes": group["archivedBytes"]}), flush=True)
    plan = {"schemaVersion": 1, "utc": datetime.now(timezone.utc).isoformat(),
            "workspace": str(ROOT), "userInstruction": "完成清单中的归档和清理；本次保留主工程缓存",
            "keepPlayers": PLAYERS, "keepMainProjectCaches": True,
            "groups": groups, "spaceBefore": {drive: shutil.disk_usage(drive + ":/")._asdict() for drive in ("C", "D")}}
    save(OUTPUT / "plan.json", plan)


def verify(group, archive):
    manifest = json.loads((OUTPUT / (group["name"] + ".manifest.json")).read_text(encoding="utf-8"))
    expected = {row["path"]: row for row in manifest["files"]}
    with zipfile.ZipFile(archive) as saved:
        actual = [item.filename for item in saved.infolist() if not item.is_dir() and not item.filename.startswith("_stage_cleanup/")]
        if len(actual) != len(expected) or set(actual) != set(expected):
            raise RuntimeError("Archive entries mismatch: " + group["name"])
        for path, row in expected.items():
            info = saved.getinfo(path)
            with saved.open(info) as stream:
                digest = sha_stream(stream)
            if info.file_size != row["bytes"] or digest != row["sha256"]:
                raise RuntimeError("Archive content mismatch: " + path)
    return {"name": group["name"], "archive": archive.relative_to(ROOT).as_posix(),
            "archiveBytes": archive.stat().st_size, "sha256": sha(archive),
            "verifiedFiles": len(expected), "verifiedBytes": sum(row["bytes"] for row in expected.values()),
            "verifiedUtc": datetime.now(timezone.utc).isoformat(), "passed": True}


def archive_all():
    plan = json.loads((OUTPUT / "plan.json").read_text(encoding="utf-8"))
    results = []
    for group in plan["groups"]:
        archive = OUTPUT / (group["name"] + ".utf8.zip")
        completed = OUTPUT / (group["name"] + ".verified.json")
        if completed.exists():
            record = json.loads(completed.read_text(encoding="utf-8"))
            if sha(archive) != record["sha256"]:
                raise RuntimeError("Previously verified archive changed")
            results.append(record)
            continue
        if archive.exists():
            raise RuntimeError("Unverified archive already exists: " + str(archive))
        manifest_path = OUTPUT / (group["name"] + ".manifest.json")
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        list_path = OUTPUT / (group["name"] + ".inputs.txt")
        names = [row["path"] for row in manifest["files"]]
        if any("\n" in path or "\r" in path for path in names):
            raise RuntimeError("Newline in filename")
        list_path.write_text("\n".join(names) + "\n", encoding="utf-8")
        if shutil.disk_usage(ROOT).free < group["archivedBytes"] + 1024 ** 3:
            raise RuntimeError("Insufficient worst-case archive headroom")
        log = OUTPUT / (group["name"] + ".7z.log")
        print("Archiving " + group["name"], flush=True)
        with log.open("w", encoding="utf-8") as stream:
            subprocess.run(["7z", "a", "-tzip", "-mx=5", "-mmt=2", "-mcu=on", "-scsUTF-8", "-bd", "-y",
                            str(archive), "@" + str(list_path)], cwd=ROOT, stdout=stream,
                           stderr=subprocess.STDOUT, check=True)
        with zipfile.ZipFile(archive, "a", compression=zipfile.ZIP_DEFLATED) as saved:
            saved.writestr("_stage_cleanup/manifest.json", manifest_path.read_bytes())
        record = verify(group, archive)
        save(completed, record)
        results.append(record)
        print(json.dumps(record, ensure_ascii=False), flush=True)
    save(OUTPUT / "archives-verified.json", results)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["prepare", "archive", "verify"])
    args = parser.parse_args()
    if args.action == "prepare":
        prepare()
    elif args.action == "archive":
        archive_all()
    else:
        plan = json.loads((OUTPUT / "plan.json").read_text(encoding="utf-8"))
        for group in plan["groups"]:
            print(json.dumps(verify(group, OUTPUT / (group["name"] + ".utf8.zip")), ensure_ascii=False), flush=True)
