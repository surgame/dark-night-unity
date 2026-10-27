import { readFile, writeFile, mkdir } from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";
import path from "node:path";
import vm from "node:vm";
import { runtimeFiles } from "./source-files.mjs";

const root = path.dirname(fileURLToPath(import.meta.url)), project = path.resolve(root, "../..");
const output = path.resolve(process.argv[2] || path.join(project, "artifacts/expedition-playground-polish-20261024/style-sampling"));
await mkdir(output, { recursive: true });
const context = vm.createContext({ window: {}, console });
for (const file of runtimeFiles) vm.runInContext(await readFile(path.join(root, "src", file), "utf8"), context);
const D = context.window.DN, inputs = [], used = new Set();
for (const scene of D.catalog) {
  const key = scene.layout === "nest" ? (scene.id === "w-foam" ? "nest-open" : "nest-covered") : scene.layout;
  if (scene.layout === "reference" || used.has(key)) continue;
  used.add(key);
  const world = new D.World(scene);
  inputs.push({ id: key, solids: world.solids.map(s => [s.x, s.y, s.w, s.h]) });
}
const inputFile = path.join(output, "inputs.json");
await writeFile(inputFile, JSON.stringify(inputs, null, 2) + "\n");
const assembly = path.join(project, "Game/Library/ScriptAssemblies/DarkNights.Core.dll");
const before = createHash("sha256").update(await readFile(assembly)).digest("hex");
execFileSync("dotnet", ["run", "--project", path.join(root, "style-sampler/StyleSampler.csproj"),
  "--", inputFile, output], { stdio: "inherit" });
execFileSync("python", [path.join(root, "style-sampler/encode.py"), output, path.join(root, "assets/cave-backdrops")],
  { stdio: "inherit" });
const after = createHash("sha256").update(await readFile(assembly)).digest("hex");
if (before !== after) throw new Error("Read-only Core assembly changed during sampling; review before distributing.");
await writeFile(path.join(root, "assets/cave-backdrops/source.json"), JSON.stringify({
  method: "BackgroundContourBaker + BackgroundPageBaker; frozen initial H5 room occupancy; native 2px scale",
  assembly, assemblySha256: before, inputFile,
  inputsSha256: createHash("sha256").update(await readFile(inputFile)).digest("hex"),
  unityStarted: false, originalSourceModified: false, layouts: inputs.length
}, null, 2) + "\n");
