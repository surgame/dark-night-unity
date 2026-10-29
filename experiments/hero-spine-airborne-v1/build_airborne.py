"""Create leg-only animation input from the author's exported Spine skeleton."""

import json
from pathlib import Path


def build(source: Path, destination: Path) -> None:
    data = json.loads(source.read_text(encoding="utf-8-sig"))
    poses = {
        "air_move_legs": {
            "left-leg1": 18,
            "left-leg2": -28,
            "right-leg1": -20,
            "right-leg2": -24,
        },
        "air_idle_legs": {
            "left-leg1": 6,
            "left-leg2": -18,
            "right-leg1": -6,
            "right-leg2": -24,
        },
    }
    data["animations"] = {
        name: {
            "bones": {
                bone: {"rotate": [{"value": angle}, {"time": 0.4, "value": angle}]}
                for bone, angle in rotations.items()
            }
        }
        for name, rotations in poses.items()
    }
    data["skeleton"]["images"] = "../../美术素材/角色/主角/images/"
    destination.write_text(
        json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8"
    )


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    build(args.source, args.destination)
