"""Rebuild the approved mixed-renderer YYGC candidate without changing older checkouts."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def git(repository, *arguments):
    return subprocess.check_output(
        ["git", "-C", str(repository), *arguments], stderr=subprocess.STDOUT
    )


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def changed_files(repository):
    tracked = git(repository, "diff", "--name-only", "-z", "HEAD").split(b"\0")
    untracked = git(repository, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0")
    return {item.decode("utf-8") for item in tracked + untracked if item}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--framework-repository", type=Path, default=Path("D:/Developer/YYGC"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    lock = json.loads(Path(__file__).with_name("framework.lock.json").read_text("utf-8"))
    target = (root / lock["destination"]).resolve()
    if target.parent != (root / ".deps").resolve():
        raise SystemExit("Dependency target must be directly inside this workspace's .deps.")
    patches = []
    for entry in lock["patches"]:
        patch = root / entry["path"]
        if sha256(patch) != entry["sha256"]:
            raise SystemExit("Patch identity differs from lock: " + entry["path"])
        patches.append(patch)
    if not target.exists():
        subprocess.run(
            ["git", "clone", "--shared", "--no-checkout", str(args.framework_repository), str(target)],
            check=True,
        )
        git(target, "config", "core.autocrlf", "false")
        git(target, "checkout", "--detach", lock["base_commit"])
        for patch in patches:
            git(target, "apply", "--check", str(patch))
            git(target, "apply", str(patch))
    if git(target, "rev-parse", "HEAD").decode().strip() != lock["base_commit"]:
        raise SystemExit("Existing dependency has a different baseline; preserved unchanged.")
    expected = lock["files"]
    changed = changed_files(target)
    if changed != set(expected):
        raise SystemExit("Existing dependency has unknown or missing changes; preserved unchanged.")
    for relative, checksum in expected.items():
        path = (target / relative).resolve()
        if target not in path.parents or not path.is_file() or sha256(path) != checksum:
            raise SystemExit("Dependency payload differs from lock: " + relative)
    print(json.dumps({
        "target": str(target), "base_commit": lock["base_commit"],
        "verified_changed_files": len(expected), "unity_started": False,
    }))


if __name__ == "__main__":
    main()
