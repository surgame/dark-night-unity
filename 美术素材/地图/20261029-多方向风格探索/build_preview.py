"""保存原生候选与来源身份，为聊天生成不改变画面内容的压缩对照预览。"""

import base64
import hashlib
import io
import json
import shutil
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
THREAD = "01a18820-685c-7ac2-a63a-caf825451d27"
GENERATED = Path("C:/Users/Jobscn/.codex/generated_images") / THREAD
VIS = Path("C:/Users/Jobscn/.codex/visualizations/2026/10/29") / THREAD
SOURCES = {
    "A": "call_pyeakPYNEBtwZfL3o87g85J4.png",
    "B": "call_SCdaDcHOObf5XQYIi8qZbDUa.png",
    "C": "call_wk2nDEOBbunELh4v2YgXWdvr.png",
    "D": "call_JYGeKus8GTrzMTrSprC9bYAF.png",
    "E": "call_zdGfnfXBN1xwxSfM9dfTgwq2.png",
    "F": "call_W9UnGua7uhXSPuYTE7tpsHmf.png",
}
DETAILS = {
    "A": "原版布局 · 像素团块、冷色后壁与少量暖矿",
    "B": "原版布局 · 大块轮廓与简化明暗，减少细碎纹理",
    "C": "原版布局改绘 · 厚路面与浓厚景深，局部坡形有生成偏差",
    "D": "新构图 · 松林天光、灰绿岩体与宽阔道路顶面",
    "E": "新构图 · 长条页岩与冷峻洞厅，画面更硬朗",
    "F": "新构图 · 连续暖土层与少量深色大石，留出安静区域",
}


def identity(path):
    content = path.read_bytes()
    with Image.open(path) as image:
        return {
            "path": str(path.resolve()),
            "bytes": len(content),
            "sha256": hashlib.sha256(content).hexdigest(),
            "nativeSize": list(image.size),
            "mode": image.mode,
        }


def preview_uri(path):
    with Image.open(path) as original:
        image = original.convert("RGB")
        image.thumbnail((1056, 595), Image.Resampling.LANCZOS)
        buffer = io.BytesIO()
        image.save(buffer, format="WEBP", quality=78, method=6)
    return "data:image/webp;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


brief = json.loads((ROOT / "brief.json").read_text(encoding="utf-8"))
VIS.mkdir(parents=True, exist_ok=True)
manifest = {"date": brief["date"], "method": brief["method"], "inputs": [], "outputs": []}
for item in brief["nativeInputs"]:
    source = Path(item["path"])
    if not source.is_absolute():
        source = REPO / source
    manifest["inputs"].append({**identity(source), "role": item["role"]})

variants = []
for item in brief["variants"]:
    source = GENERATED / SOURCES[item["id"]]
    target = ROOT / item["file"]
    if target.exists():
        if target.read_bytes() != source.read_bytes():
            raise RuntimeError("Refusing to replace an existing candidate: " + str(target))
    else:
        shutil.copyfile(source, target)
    manifest["outputs"].append({
        **identity(target),
        "id": item["id"],
        "name": item["name"],
        "generatedSource": str(source),
        "sourceSha256": identity(source)["sha256"],
        "prompt": item["prompt"],
    })
    variants.append({
        "id": item["id"],
        "name": item["name"],
        "detail": DETAILS[item["id"]],
        "src": preview_uri(target),
    })

runtime = REPO / brief["nativeInputs"][0]["path"]
data = {"variants": variants, "original": preview_uri(runtime)}
template = (ROOT / "preview.template.html").read_text(encoding="utf-8")
fragment = template.replace("/*__IMAGE_DATA__*/null", json.dumps(data, ensure_ascii=False))
output = VIS / "cave-style-comparison.html"
output.write_text(fragment, encoding="utf-8")
manifest["preview"] = {
    "path": str(output),
    "bytes": output.stat().st_size,
    "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
    "compression": "1056px maximum width, WebP quality 78; native PNG files unchanged",
}
assert manifest["preview"]["bytes"] < 1_000_000
assert all(item["sha256"] == item["sourceSha256"] for item in manifest["outputs"])
(ROOT / "manifest.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)
print(json.dumps({
    "nativeCandidates": len(variants),
    "nativeSizes": [item["nativeSize"] for item in manifest["outputs"]],
    "nativeBytes": sum(item["bytes"] for item in manifest["outputs"]),
    "preview": manifest["preview"],
}, ensure_ascii=False))
