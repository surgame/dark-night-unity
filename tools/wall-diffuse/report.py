"""Summarize saved Unity receipts; never runs Unity or changes author assets."""
import hashlib
import json
import math
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / "artifacts/wall-diffuse-20261012"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def p95(values):
    return sorted(values)[math.ceil(len(values) * .95) - 1]


def summarize(name):
    source = read(ARTIFACTS / name / "result.json")

    rows = [item for item in source["reports"] if "gpuBlocks" in item]
    groups = []
    for lights in [1, 2, 4, 16]:
        for motion in sorted({item["motion"] for item in rows}):
            selected = [item for item in rows if item["lights"] == lights and item["motion"] == motion]
            if len(selected) != 144:
                continue
            blocks = [selected[index:index + 12] for index in range(0, 144, 12)]
            deltas = {}
            for offset, reference in [(0, "A0"), (4, "A1"), (8, "Off")]:
                values = [(blocks[offset + 1][i]["gpuNs"] + blocks[offset + 2][i]["gpuNs"] -
                           blocks[offset][i]["gpuNs"] - blocks[offset + 3][i]["gpuNs"]) / 2e6 for i in range(12)]
                deltas[reference] = p95(values)
            b = [item for item in selected if item["mode"] == "B"]
            groups.append({"lights": lights, "motion": motion, "paired_gpu_delta_p95_ms": deltas,
                           "prepare_p95_ms": p95([item["prepareMs"] for item in b]),
                           "gc_method": b[0].get("gcMethod", "INVALID Mono byte counter; do not use"),
                           "gc_events_max": max((item.get("gcEvents", -1) for item in b)),
                           "gpu_valid": all(item["gpuBlocks"] > 0 and item["gpuNs"] > 0 for item in selected)})
    return {"complete": source["complete"], "error": source.get("error"), "source": str(ARTIFACTS / name / "result.json"), "samples": len(rows), "groups": groups,
            "cold_resource_frames": [item for item in source["reports"] if "coldResourceFrame" in item]}


def main():
    steady = summarize("acceptance-coherent")
    dynamic = summarize("acceptance-dynamic-delivery")
    tests = read(ARTIFACTS / "tests-delivery.json")
    compile_result = read(ARTIFACTS / "static-delivery/result.json")
    material = read(ARTIFACTS / "material-probe-d2/result.json")
    changed = subprocess.check_output(["git", "diff", "--name-only", "0c3aa772", "--", "Game", "docs", "tools/wall-diffuse"], cwd=ROOT, text=True).splitlines()
    changed += subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "Game", "docs", "tools/wall-diffuse"], cwd=ROOT, text=True).splitlines()
    hashes = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in sorted(set(changed)) if (ROOT / path).is_file() and path != "docs/evidence/wall-diffuse-20261012.json"}
    report = {"date": "2026-10-12", "baseline": "0c3aa7723194d3b35cf98dd3c4a4f2f9b9a98c1c",
              "branch": "ft-20261012-wall-diffuse-light", "unity": "6000.4.9f1", "gpu": "NVIDIA GeForce RTX 5060 Ti", "api": "Direct3D12",
              "implementation": "Complete; Pinewatch explicit Continuous migration", "unity_tests": tests["summary"],
              "implementation_checkpoint": "95eebb7", "static_compile": compile_result["checks"], "materials": material, "steady": steady, "dynamic": dynamic,
              "scope": "Actual Editor GPU, isolated terrain and Bootstrap; no Player or foreground performance signoff",
              "scene_profile_guid": "3a6e771a82d12b2429a3d3ea9d34b641", "new_gpu_buffer_bytes": 222860,
              "runtime": [read(path) for path in sorted((ARTIFACTS / "runtime").glob("*.json"))],
              "failed_evidence_retained": ["shader-d1-failed.json", "tests-d1-failed.json", "tests-d2.json", "tests-d4.json",
                                           "acceptance-d1", "acceptance-d2", "material-probe", "acceptance-gc", "acceptance-final", "acceptance-fenced", "acceptance-dynamic-fenced", "acceptance-dynamic-coherent", "acceptance-dynamic-delivery"],
              "pending": ["Foreground game performance and full-game visual judgment", "Dynamic paired GPU timing: valid receipt unavailable on some dispatches despite bounded wait; do not sign off budgets", "Independent Player/remote machine",
                          "Pointer gesture and save/cancel UI interactions", "Allocations during geometry/region cache rebuild; zero-GC budget only verified in steady state", "Cold Shader compilation timing; resource cold frames are recorded separately"],
              "protocol_save_dependency_changes": False, "source_hashes": hashes}
    output = ROOT / "docs/evidence/wall-diffuse-20261012.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"tests": tests["summary"], "four_lights": [item for item in steady["groups"] if item["lights"] == 4]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
