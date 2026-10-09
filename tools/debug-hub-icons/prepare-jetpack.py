"""从保留的 AI 源图派生独立 32px 图标；不改源图或既有手工素材。"""
import hashlib
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = root / "experiments/imagegen-debug-hub-20261009/jetpack-source.png"
output = root / "Game/Assets/DarkNights/Res/UI/DebugHub/Icons/Jetpack.png"
image = Image.open(source).convert("RGBA")
if image.getchannel("A").getextrema()[0] == 255:
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if r > 170 and b > 170 and g < 100:
                pixels[x, y] = (0, 0, 0, 0)
image.putalpha(image.getchannel("A").point(lambda value: 255 if value >= 128 else 0))
bounds = image.getchannel("A").getbbox()
if bounds is None:
    raise RuntimeError("Source contains no object")
icon = image.crop(bounds)
ratio = min(28 / icon.width, 28 / icon.height)
icon = icon.resize((round(icon.width * ratio), round(icon.height * ratio)), Image.Resampling.NEAREST)
alpha = icon.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
icon = icon.convert("RGB").quantize(colors=24, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
icon.putalpha(alpha)
final = Image.new("RGBA", (32, 32))
final.alpha_composite(icon, ((32 - icon.width) // 2, (32 - icon.height) // 2))
output.parent.mkdir(parents=True, exist_ok=True)
final.save(output)
report = {"model": "gpt-image-2.5", "provider": "1qq", "base_url": "https://sub.1qq.xyz/v1",
          "requested_size": "1024x1024", "actual_source_size": list(image.size),
          "source": str(source.relative_to(root)), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
          "derived": str(output.relative_to(root)), "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
          "size": [32, 32], "alpha": "binary", "palette_limit": 24, "resize": "nearest", "source_bounds": bounds}
(source.parent / "source.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
final.resize((256, 256), Image.Resampling.NEAREST).save(root / "artifacts/debug-hub-icons-20261009/jetpack-native-preview.png")
print(json.dumps(report))
