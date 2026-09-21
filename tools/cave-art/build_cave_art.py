"""Build the deterministic 8 px tonal cave material and the existing 32 px Unity sprite contract."""
from pathlib import Path
import argparse
import hashlib
import json
import math

from PIL import Image


OUT = Path("Game/Assets/DarkNights/Res/Art/Custom/CaveExploration")
MANIFEST = OUT / "cave-art-manifest.json"
OUTPUTS = ("cave-rock.png", "cave-dualgrid.png")
SIZE = 256
NATIVE_TILE = 8
UNITY_TILE = 32
TEXTURE_SEED = 17
FACET_CELLS = 51
STONE_GROUPING = 0.55
PALETTES = {
    "warm": ("141316", "201d20", "30292c", "413637", "57453f", "705747", "907050", "b58c5d", "d0ab75"),
    "ash": ("14171a", "202528", "303637", "414a49", "56605d", "6b7770", "879086", "a7ae95", "c4caae"),
    "rust": ("181315", "251d1d", "392a27", "4c3630", "674637", "815840", "a3714b", "c7935d", "dbb87e"),
}
BASES = {"warm": (138, 119, 98), "ash": (121, 124, 119), "rust": (149, 109, 82)}
MATERIAL_TINTS = (
    (1.08, 0.98, 0.86), (0.92, 0.97, 1.03), (0.76, 0.80, 0.88), (1.10, 0.88, 0.72),
    (0.86, 0.93, 1.00), (1.14, 1.02, 0.76), (0.88, 0.98, 0.80), (0.58, 0.61, 0.68),
)
REFERENCE_INPUTS = {
    "dark_nights_8x8_design.html": "4b74ff5b3fa931856dd68acd895133e43d8a57e14922fec8ec05c8545eef8501",
    "dark_nights_8x8_final.html": "010d86029c5f7eb2f22a3d21932bf90e5e74a720b3790f8cb6a608b04ddf228a",
    "dark_nights_final_validation.json": "d46abc3c980d716ee93a465c7391be3dee395bb2cc978e0d1bcd147d47b90095",
}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hash01(x, y, seed=TEXTURE_SEED):
    value = ((x & 0xFFFFFFFF) * 374761393) ^ ((y & 0xFFFFFFFF) * 668265263) ^ (seed * 1442695041)
    value &= 0xFFFFFFFF
    value = ((value ^ (value >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((value ^ (value >> 16)) & 0xFFFFFFFF) / 4294967296.0


def lerp(a, b, amount):
    return a + (b - a) * amount


def rgb(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def tonal_palette(name):
    base = BASES[name]
    base_luma = base[0] * 0.2126 + base[1] * 0.7152 + base[2] * 0.0722
    result = []
    for value in PALETTES[name]:
        color = rgb(value)
        luma = color[0] * 0.2126 + color[1] * 0.7152 + color[2] * 0.0722
        result.append(tuple(max(0, min(255, round(channel * luma / base_luma))) for channel in base))
    return tuple(result)


def periodic_noise(x, y, seed, period=8):
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = x - ix, y - iy
    ux, uy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = lerp(hash01(ix % period, iy % period, seed), hash01((ix + 1) % period, iy % period, seed), ux)
    b = lerp(hash01(ix % period, (iy + 1) % period, seed), hash01((ix + 1) % period, (iy + 1) % period, seed), ux)
    return lerp(a, b, uy)


def facet_tone(x, y):
    step = SIZE / FACET_CELLS
    gx, gy = math.floor(x / step), math.floor(y / step)
    nearest, second, best = 1e9, 1e9, None
    for dy in range(-1, 2):
        for dx in range(-1, 2):
            cell_x, cell_y = gx + dx, gy + dy
            wrapped_x, wrapped_y = cell_x % FACET_CELLS, cell_y % FACET_CELLS
            variation = hash01(wrapped_x, wrapped_y, 316)
            variation_y = hash01(wrapped_x, wrapped_y, 471)
            center_x = (cell_x + 0.2 + variation * 0.62) * step
            center_y = (cell_y + 0.2 + variation_y * 0.64) * step
            distance = (x - center_x) ** 2 + (y - center_y) ** 2 * 0.88
            if distance < nearest:
                second, nearest = nearest, distance
                best = (wrapped_x, wrapped_y, center_y)
            elif distance < second:
                second = distance
    cell_x, cell_y, center_y = best
    individual = 2 + math.floor(hash01(cell_x, cell_y, 39) * 4)
    regional = 2.6 + periodic_noise((x + 0.5) / 32, (y + 0.5) / 32, 981) * 1.6
    tone = round(lerp(individual, regional, STONE_GROUPING))
    if y < center_y - 1.5:
        tone += 1
    elif y > center_y + 1.5:
        tone -= 1
    if math.sqrt(second) - math.sqrt(nearest) < 0.24:
        tone = 1
    return max(0, min(7, tone))


def build_rock():
    palette = tonal_palette("warm")
    image = Image.new("RGB", (SIZE, SIZE))
    pixels = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            pixels[x, y] = palette[facet_tone(x, y)]
    return image


def field(mask, u, v):
    nw, ne, sw, se = (1 if mask & bit else 0 for bit in (1, 2, 4, 8))
    if mask in (6, 9):
        return nw * (1 - u) + ne * (u - v) + se * v if u >= v else nw * (1 - v) + sw * (v - u) + se * u
    return nw * (1 - u) * (1 - v) + ne * u * (1 - v) + sw * (1 - u) * v + se * u * v


def anchored_alpha(mask):
    alpha = []
    for y in range(NATIVE_TILE):
        for x in range(NATIVE_TILE):
            fade = 0 if x in (0, 7) or y in (0, 7) else 1
            wave = (hash01(x // 2, y // 2, TEXTURE_SEED) - 0.5) * 0.25 * fade
            alpha.append(1 if mask and (mask == 15 or field(mask, x / 7, y / 7) + wave >= 0.5) else 0)
    corners = ((0, 1), (7, 2), (56, 4), (63, 8))
    for wanted in (1, 0):
        seen = set(index for index, bit in corners if int(bool(mask & bit)) == wanted)
        queue = list(seen)
        while queue:
            index = queue.pop(0)
            x, y = index % 8, index // 8
            neighbours = ([index - 1] if x else []) + ([index + 1] if x < 7 else []) + ([index - 8] if y else []) + ([index + 8] if y < 7 else [])
            for neighbour in neighbours:
                if neighbour not in seen and alpha[neighbour] == wanted:
                    seen.add(neighbour)
                    queue.append(neighbour)
        for index in range(64):
            if alpha[index] == wanted and index not in seen:
                alpha[index] = 1 - wanted
    return alpha


def rock_pattern(x, y, seed):
    motif = (
        (1, 1, 0, 1, 2, 2, 1, 0), (2, 3, 1, 2, 3, 2, 1, 1),
        (3, 4, 2, 2, 2, 1, 0, 2), (2, 3, 2, 1, 0, 0, 1, 2),
        (1, 1, 0, 0, 1, 2, 1, 1), (0, 0, 1, 2, 3, 3, 2, 0),
        (1, 1, 2, 3, 4, 2, 1, 0), (1, 2, 2, 2, 2, 1, 0, 1),
    )
    return motif[(y + ((seed >> 2) % 3)) % 8][(x + (seed % 3)) % 8]


def native_tile(mask, material, variant):
    palette = tonal_palette("warm")
    alpha = anchored_alpha(mask)
    image = Image.new("RGBA", (NATIVE_TILE, NATIVE_TILE))
    pixels = image.load()
    exposed = [(x, y) for y in range(8) for x in range(8) if not alpha[y * 8 + x]]
    for y in range(8):
        for x in range(8):
            if not alpha[y * 8 + x]:
                continue
            if exposed:
                ox, oy = min(exposed, key=lambda q: (q[0] - x) ** 2 + (q[1] - y) ** 2)
                distance = (ox - x) ** 2 + (oy - y) ** 2
                oy -= y
            else:
                distance, oy = 999, -1
            pattern = rock_pattern(x + variant * 2, y + variant, TEXTURE_SEED)
            grain = hash01(x + variant * 11, y + material * 7, TEXTURE_SEED + 91)
            tone = 1 if pattern >= 3 else 0
            if distance <= 2:
                tone = (4 + int(pattern > 1) + int(grain > 0.89)) if oy < 0 else (3 + int(pattern > 1))
                if pattern == 0 and grain < 0.55:
                    tone = max(1, tone - 2)
            elif distance <= 5:
                tone = 1 + int(pattern > 0) + int(pattern > 2) + int(oy < 0)
            elif distance <= 10:
                tone = 2 if pattern > 2 else (1 if pattern > 0 else 0)
            color = palette[max(0, min(8, tone))]
            tint = MATERIAL_TINTS[material]
            pixels[x, y] = tuple(min(255, round(color[channel] * tint[channel])) for channel in range(3)) + (255,)
    return image


def build_atlas():
    atlas = Image.new("RGBA", (512, 1024))
    for material in range(8):
        for variant in range(4):
            for mask in range(16):
                tile = native_tile(mask, material, variant).resize((UNITY_TILE, UNITY_TILE), Image.Resampling.NEAREST)
                atlas.paste(tile, (variant * 128 + mask % 4 * 32, material * 128 + mask // 4 * 32))
    return atlas


def validate(rock, atlas):
    if rock.size != (256, 256) or atlas.size != (512, 1024):
        raise SystemExit("Unexpected cave asset dimensions.")
    for material in range(8):
        for variant in range(4):
            for mask in range(16):
                left, top = variant * 128 + mask % 4 * 32, material * 128 + mask // 4 * 32
                for y in range(8):
                    for x in range(8):
                        block = {atlas.getpixel((left + x * 4 + dx, top + y * 4 + dy)) for dy in range(4) for dx in range(4)}
                        if len(block) != 1:
                            raise SystemExit("Atlas no longer preserves native 8 px blocks.")
    masks = [anchored_alpha(mask) for mask in range(16)]
    edge_checks = 0
    for first in range(16):
        for second in range(16):
            if bool(first & 2) == bool(second & 1) and bool(first & 8) == bool(second & 4):
                for y in range(8):
                    if masks[first][y * 8 + 7] != masks[second][y * 8]:
                        raise SystemExit("Horizontal DualGrid edge mismatch.")
                    edge_checks += 1
            if bool(first & 4) == bool(second & 1) and bool(first & 8) == bool(second & 2):
                for x in range(8):
                    if masks[first][56 + x] != masks[second][x]:
                        raise SystemExit("Vertical DualGrid edge mismatch.")
                    edge_checks += 1
    return {
        "nativeTilePixels": NATIVE_TILE,
        "unitySpritePixels": UNITY_TILE,
        "rockPaletteColors": len(set(rock.getdata())),
        "atlasEdgeChecks": edge_checks,
        "scaledBlockChecks": 8 * 4 * 16 * 64,
    }


def verify_existing_outputs():
    if not MANIFEST.exists():
        raise SystemExit("Existing assets require their manifest.")
    previous = json.loads(MANIFEST.read_text(encoding="utf-8"))["outputs"]
    for name in OUTPUTS:
        if sha256(OUT / name) != previous[name]:
            raise SystemExit("Manual changes detected; refusing to overwrite " + name)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--update-generated", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    if args.validate_only:
        report = validate(Image.open(OUT / OUTPUTS[0]).convert("RGB"), Image.open(OUT / OUTPUTS[1]).convert("RGBA"))
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return
    if any((OUT / name).exists() for name in OUTPUTS):
        if not args.update_generated:
            raise SystemExit("Existing assets: pass --update-generated after reviewing the current manifest.")
        verify_existing_outputs()
    rock, atlas = build_rock(), build_atlas()
    report = validate(rock, atlas)
    rock.save(OUT / OUTPUTS[0])
    atlas.save(OUT / OUTPUTS[1])
    source = OUT / "rock-source-gpt-image-2.5.png"
    manifest = {
        "schema": "dark-nights-cave-art/v2",
        "previousResearchSource": source.name,
        "previousResearchModel": "gpt-image-2.5",
        "previousResearchSourceSha256": sha256(source),
        "referenceInputs": REFERENCE_INPUTS,
        "generator": "tools/cave-art/build_cave_art.py",
        "design": {
            "palette": "warm-tonal",
            "coherence": 1.0,
            "toneGrouping": STONE_GROUPING,
            "rockPixelsPerWorldUnit": 8,
            "fixedEdgeShadingPixels": {"start": 2, "decay": 7, "coreAfter": 25},
            "note": "The provider image remains provenance for the prior study; v2 output is deterministic pixel construction from the supplied 8x8 direction.",
        },
        "validation": report,
        "outputs": {name: sha256(OUT / name) for name in OUTPUTS},
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"outputs": manifest["outputs"], "validation": report}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
