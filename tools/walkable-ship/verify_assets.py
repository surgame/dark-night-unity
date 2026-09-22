"""Check imported native art against the preserved manifest and Unity sampling contract."""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[2]
source = json.loads((Path(__file__).parent / 'art-source/manifest.json').read_text(encoding='utf-8-sig'))
art = repo / 'Game/Assets/DarkNights/Res/Objects/ExpeditionShip/Art'
checks = {}
for name, entry in source['assets'].items():
    path = art / Path(entry['file']).name
    with Image.open(path) as image:
        checks[name + ':sha256'] = hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256']
        checks[name + ':native-size'] = list(image.size) == entry['size']
        pixels = list(image.convert('RGBA').getdata())
        checks[name + ':straight-binary-alpha'] = all(p[3] in (0, 255) and (p[3] or p[:3] == (0, 0, 0)) for p in pixels)
    meta = path.with_suffix('.png.meta').read_text()
    ppu = 100 if name.startswith(('crew_walk_', 'robot_walk_', 'drone_hover_')) else 50
    required = {'enableMipMap': 0, 'sRGBTexture': 1, 'filterMode': 0, 'spriteMeshType': 0,
                'spriteMode': 1, 'spritePixelsToUnits': ppu, 'textureCompression': 0}
    checks[name + ':import-contract'] = all(re.search(r'^\s*' + key + r': ' + str(value) + r'\s*$', meta, re.M) for key, value in required.items())
output = repo / 'artifacts/walkable-ship/art-verification.json'
output.parent.mkdir(parents=True, exist_ok=True)
result = dict(assets=len(source['assets']), passed=sum(checks.values()), total=len(checks), checks=checks)
output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'checks'}))
raise SystemExit(not all(checks.values()))
