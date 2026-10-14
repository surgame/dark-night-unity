"""Replay archived offline lighting versions into a fresh directory, without Unity."""
from pathlib import Path
import argparse
import importlib.util
import hashlib
import json
import shutil
import sys

sys.dont_write_bytecode=True


def module_from(path, name):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    folder=Path(__file__).resolve().parent
    parser=argparse.ArgumentParser()
    parser.add_argument("version",choices=["v1.0.0","v1.1.0","v1.2.0"])
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    output=args.output.resolve()
    if output.exists():
        raise RuntimeError("Choose a new output directory; archived and old results are never overwritten")
    baseline=folder/"v1.0.0"
    manifest=json.loads((baseline/"manifest.json").read_text(encoding="utf-8"))
    for entry in manifest["files"]:
        digest=hashlib.sha256((baseline/entry["path"]).read_bytes()).hexdigest()
        if digest!=entry["sha256"]:
            raise RuntimeError("Frozen source/image mismatch: "+entry["path"])
    if args.version=="v1.0.0":
        original=module_from(baseline/"generator-source/render_original_light_pipeline.py","archived_pipeline")
        # Source hashes in regenerated reports must describe the frozen inputs,
        # even if the current game's shaders have changed since the archive.
        source_root=output/"frozen-input"
        game=source_root/"Game/Assets/DarkNights/Res/Shared/Lighting"
        game.mkdir(parents=True)
        for source in (baseline/"game-source").iterdir():
            shutil.copy2(source,game/source.name)
        original.OUT=output
        original.ROOT=source_root
        original.main()
    else:
        generators={"v1.1.0":"generate_skip_step09_v1_1.py",
                    "v1.2.0":"generate_wall_mask_v1_2.py"}
        revised=module_from(folder/args.version/"generator-source"/generators[args.version],"archived_variant")
        previous=sys.argv
        try:
            sys.argv=[str(Path(__file__)),"--output",str(output)]
            revised.main()
        finally:
            sys.argv=previous


if __name__=="__main__":
    main()
