"""Lock the four tracked AnyRuleD packages from a clean YYGC checkout."""
import argparse
import hashlib
import json
import subprocess
import tempfile
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--framework", type=Path, required=True)
args = parser.parse_args()
framework = args.framework.resolve()
root = Path(__file__).resolve().parent
prefix = "AnyRuleD~/Packages/"
packages = (
    "com.tsgame.anyrules",
    "com.tsgame.anyrules.yygc",
    "com.tsgame.anyrules.networking",
    "com.tsgame.anyrules.networking.fishnet",
)


def git(*parts):
    return subprocess.check_output(["git", "-C", str(framework), *parts], text=True).strip()


commit = git("rev-parse", "HEAD")
if git("status", "--porcelain", "--untracked-files=no"):
    raise SystemExit("YYGC tracked worktree must be clean before locking a commit")
with tempfile.TemporaryDirectory() as temporary:
    archive = Path(temporary) / "packages.zip"
    subprocess.run(["git", "-C", str(framework), "archive", "--format=zip", f"--output={archive}",
                    commit, *(prefix + name for name in packages)], check=True)
    with zipfile.ZipFile(archive) as source:
        files = []
        for path in sorted(name for name in source.namelist() if not name.endswith("/")):
            if not path.startswith(prefix):
                raise SystemExit("Unexpected package path: " + path)
            files.append({"path": path[len(prefix):], "sha256": hashlib.sha256(source.read(path)).hexdigest()})
output = root / "source-lock-map-state.json"
output.write_text(json.dumps({"commit": commit, "files": files}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Locked {len(files)} AnyRuleD files from {commit}")
