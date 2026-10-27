import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";

const root = path.dirname(fileURLToPath(import.meta.url));
let result;
const context = vm.createContext({ window: { DN: { clamp: (n, a, b) => Math.max(a, Math.min(b, n)) } },
  document: { createElement: () => ({ getContext: () => ({
    createImageData: (w, h) => ({ data: new Uint8ClampedArray(w * h * 4) }),
    putImageData: image => { result = image.data; }
  }) }) } });
vm.runInContext(await readFile(path.join(root, "src/cave-pixels.js"), "utf8"), context);
const D = context.window.DN, mask = new Uint8Array(1024);
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  mask[y * 32 + x] = y >= 30 || x < 7 || y < 6 || x > 22 && x < 27 && y < 20 ? 1 : 0;
}
D.bakePixelRock(32, 32, mask, D.rockToneField(32, 32));
const reference = await readFile(process.argv[2]);
assert.equal(result.length, reference.length);
let differences = 0;
for (let i = 0; i < result.length; i++) if (result[i] !== reference[i]) differences++;
const provenance = JSON.parse(await readFile(path.join(root, "asset-provenance.json"), "utf8"));
for (const source of [...provenance.assets, ...provenance.styleSources]) {
  assert.equal(createHash("sha256").update(await readFile(source.source)).digest("hex"), source.sha256, source.source);
}
const report = { type: "native-style-source-audit", assets: provenance.assets.length,
  styleSources: provenance.styleSources.length, sourceHashesMatch: true,
  calibration: { reference: path.resolve(process.argv[2]), pixels: 1024, bytes: reference.length,
    differingBytes: differences, passed: differences === 0 },
  note: "Exact CPU calibration covers the raw unmodified rock bake; it does not verify Unity lighting, contour modifiers or human art approval." };
if (process.argv[3]) await writeFile(process.argv[3], JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report, null, 2));
assert.equal(differences, 0, "H5 rock pixels match the native Core calibration");
