"""Prepare unmodified official Spine packages at the committed, checked identity."""

import argparse
import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    lock = json.loads(Path(__file__).with_name("dependency.lock.json").read_text())
    archive = args.archive or root / "artifacts/spine-dependency" / (lock["commit"] + ".tar.gz")
    if not archive.exists():
        archive.parent.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(
            "https://codeload.github.com/EsotericSoftware/spine-runtimes/tar.gz/" + lock["commit"],
            archive,
        )
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    if digest != lock["archiveSha256"]:
        raise SystemExit("Official archive SHA-256 differs from the locked source.")
    target = root / lock["localRoot"]
    if target.exists():
        receipt = target / "source-receipt.json"
        if not receipt.exists() or json.loads(receipt.read_text())["archiveSha256"] != digest:
            raise SystemExit("Existing dependency is not the locked prepared package; preserve it.")
        expected = json.loads(receipt.read_text())["files"]
        for relative, checksum in expected.items():
            path = target / relative
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != checksum:
                raise SystemExit("Prepared dependency was modified: " + relative)
        print("Existing unmodified Spine package identity verified.")
        return
    files = {}
    prefix = "spine-runtimes-" + lock["commit"] + "/"
    with tarfile.open(archive, "r:gz") as source:
        for item in source:
            if not item.isfile() or not item.name.startswith(prefix):
                continue
            relative = item.name[len(prefix):]
            if not any(relative.startswith(package + "/") for package in lock["packages"]):
                continue
            path = (target / relative).resolve()
            if target.resolve() not in path.parents:
                raise SystemExit("Archive path escapes the dependency directory.")
            content = source.extractfile(item).read()
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
            files[relative] = hashlib.sha256(content).hexdigest()
    receipt = dict(lock, files=files)
    (target / "source-receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps({"commit": lock["commit"], "files": len(files), "target": str(target)}))


if __name__ == "__main__":
    main()
