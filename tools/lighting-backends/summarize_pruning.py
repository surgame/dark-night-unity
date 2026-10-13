"""汇总有限 ABBA 照明对照；拒绝不完整矩阵及重复回执，保留原始样本。"""
import argparse
import json
from collections import defaultdict
from pathlib import Path
from statistics import median
import random


def percentile(values, fraction):
    ordered = sorted(values)
    at = (len(ordered) - 1) * fraction
    lo = int(at)
    hi = min(lo + 1, len(ordered) - 1)
    return ordered[lo] + (ordered[hi] - ordered[lo]) * (at - lo)


def summarize(path):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    assert data["complete"], data.get("error")
    assert data.get("valid", True), "Batch explicitly invalid"
    assert not path.parent.joinpath("gate-invalid.json").exists(), "Batch excluded by its original gate"
    grouped = defaultdict(lambda: defaultdict(list))
    receipts = set()
    pixels = []
    for row in data["reports"]:
        if "gpuNs" not in row:
            pixels.append(row)
            continue
        assert row["receipt"] not in receipts and row["gpuNs"] >= 0
        receipts.add(row["receipt"])
        key = (row["shape"], row["lights"], row["motion"], row["variant"])
        grouped[key][row["backend"]].append(row)
    results = []
    for (shape, lights, motion, variant), pair in grouped.items():
        assert set(pair) == {"reference", "optimized"}
        assert len(pair["reference"]) == len(pair["optimized"])
        output = {"shape": shape, "lights": lights, "motion": motion, "variant": variant}
        lookup = {(r["block"], r["sample"]): r for rows in pair.values() for r in rows}
        assert len(lookup) == sum(len(rows) for rows in pair.values()), "Duplicate block/sample"
        assert all(r["backend"] == ("reference" if r["block"] in (0, 3) else "optimized") for r in lookup.values())
        matched = {"reference": [], "optimized": []}
        excluded = []
        for ref_block, opt_block in [(0, 1), (3, 2)]:
            for sample in range(len(pair["reference"]) // 2):
                a, b = lookup[ref_block, sample], lookup[opt_block, sample]
                if a["gpuNs"] > 0 and b["gpuNs"] > 0 and not a.get("missingGpuReason") and not b.get("missingGpuReason"):
                    matched["reference"].append(a)
                    matched["optimized"].append(b)
                else:
                    excluded.append({"reference": a["receipt"], "optimized": b["receipt"]})
        output["gpu_pairs"] = len(matched["reference"])
        output["attempted_pairs"] = len(pair["reference"])
        output["excluded_gpu_pairs"] = excluded
        assert output["gpu_pairs"] >= 2, f"Insufficient paired GPU receipts: {output}"
        for name, rows in pair.items():
            cpu = [r["cpuMs"] for r in matched[name]]
            gpu = [r["gpuNs"] / 1000000 for r in matched[name]]
            output[name] = {"samples": len(matched[name]), "attempted_samples": len(rows), "cpu_median_ms": median(cpu), "cpu_p95_ms": percentile(cpu, .95),
                            "gpu_median_ms": median(gpu), "gpu_p95_ms": percentile(gpu, .95),
                            "gc_bytes_min": min(r["gcBytes"] for r in rows), "gc_bytes_max": max(r["gcBytes"] for r in rows),
                            "uploaded_bytes": sorted(set(r["uploadedBytes"] for r in rows))}
            for field in ["terrainTickMs", "geometryTickMs", "freezeMs", "boundaryTickMs", "candidateMs", "prepareMs", "uploadMs", "submitMs"]:
                if all(field in r for r in matched[name]):
                    values = [r[field] for r in matched[name]]
                    output[name][field] = {"median": median(values), "p95": percentile(values, .95), "max": max(values)}
        # 配对差值的确定性 bootstrap 区间用于区分重复回退与采样噪声；绝对耗时仍报告原始分位数。
        for metric in ["cpuMs", "gpuNs"]:
            values = [b[metric] - a[metric] for a, b in zip(matched["reference"], matched["optimized"])]
            rng = random.Random(20261013)
            boot = [median(rng.choices(values, k=len(values))) for _ in range(3000)]
            divisor = 1000000 if metric == "gpuNs" else 1
            output[metric + "_paired_delta_ms"] = {"median": median(values) / divisor,
                "ci95": [percentile(boot, .025) / divisor, percentile(boot, .975) / divisor]}
        before, after = output["reference"], output["optimized"]
        for metric in ["cpu_median_ms", "gpu_median_ms", "cpu_p95_ms", "gpu_p95_ms"]:
            output[metric + "_reduction_percent"] = (1 - after[metric] / before[metric]) * 100
        results.append(output)
    summary = {"source": str(path.resolve()), "scope": data["scope"], "attempted_samples": len(receipts),
               "valid_samples": sum(r["gpu_pairs"] for r in results) * 2,
               "performance_accepted": all(r["gpu_pairs"] >= 30 for r in results),
               "cases": len(results), "missing_gpu_receipts": sum(bool(r.get("missingGpuReason")) for r in data["reports"]),
               "comparisons": pixels, "performance": results}
    out = path.with_name("summary.json")
    out.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    text = ["| 光形 | 灯数 | 状态／变体 | CPU中位数：原→新(ms) | GPU中位数：原→新(ms) | GPU下降 |",
            "| --- | --- | --- | --- | --- | --- |"]
    for row in results:
        before, after = row["reference"], row["optimized"]
        text.append(f'| {row["shape"]} | {row["lights"]} | {row["motion"]} / {row["variant"]} | '
                    f'{before["cpu_median_ms"]:.4f} → {after["cpu_median_ms"]:.4f} | '
                    f'{before["gpu_median_ms"]:.4f} → {after["gpu_median_ms"]:.4f} | '
                    f'{row["gpu_median_ms_reduction_percent"]:.1f}% |')
    path.with_name("performance.md").write_text("\n".join(text) + "\n", encoding="utf-8")
    print(json.dumps({"summary": str(out), "cases": len(results), "samples": len(receipts)}, ensure_ascii=False))
    print("\n".join(text))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("result", type=Path)
    summarize(parser.parse_args().result)
