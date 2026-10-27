import { readFile, writeFile, mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { browserFiles } from "./source-files.mjs";
import vm from "node:vm";

const root = path.dirname(fileURLToPath(import.meta.url));
const inputs = ["shell.html", "style.css", ...browserFiles];
const content = await Promise.all(inputs.map(name => readFile(path.join(root, "src", name), "utf8")));
const digest = createHash("sha256").update(content.join("\n")).digest("hex");
const identity = "dn-expedition-lab-v1-" + digest.slice(0, 12);
const sources = Object.fromEntries(inputs.map((name, index) => [name, content[index]]));
const catalogContext = vm.createContext({ window: {} });
for (const name of ["catalog.js", "scenes-weapons.js", "scenes-tools.js", "scenes-sites.js", "scenes-mixes.js", "reference-route.js", "scenes-features.js"]) {
  new vm.Script(sources[name], { filename: name }).runInContext(catalogContext);
}
const sceneCount = catalogContext.window.DN.catalog.length;
for (const name of browserFiles) new vm.Script(sources[name], { filename: name });
const code = browserFiles.map(name => "\n/* " + name + " */\n" + sources[name]).join("\n");
const preamble = "window.DN = { buildIdentity: " + JSON.stringify(identity) + " };";
const single = sources["shell.html"]
  .replace("<!-- LAB_STYLE -->", "<style>\n" + sources["style.css"] + "\n</style>")
  .replace("<!-- LAB_SCRIPTS -->", "<script>\n" + preamble + "\n" + code.replace(/<\/script/gi, "<\\/script") + "\n</script>");
const dev = sources["shell.html"]
  .replace("<!-- LAB_STYLE -->", '<link rel="stylesheet" href="src/style.css">')
  .replace("<!-- LAB_SCRIPTS -->", "<script>" + preamble + "</script>\n" +
    browserFiles.map(name => '<script src="src/' + name + '"></script>').join("\n"));
await mkdir(path.join(root, "dist"), { recursive: true });
await writeFile(path.join(root, "dist", "index.html"), single, "utf8");
await writeFile(path.join(root, "dev.html"), dev, "utf8");
await writeFile(path.join(root, "dist", "manifest.json"), JSON.stringify({
  schema: "darknights-independent-lab-build-v1", identity, sourceSha256: digest,
  sceneCount, featureCount: 10, networkDependencies: [], unityWrites: false, approval: "all-pending",
  inputs: inputs.map(name => ({ file: "src/" + name, sha256: createHash("sha256").update(sources[name]).digest("hex") }))
}, null, 2) + "\n", "utf8");
console.log(JSON.stringify({ identity, bytes: Buffer.byteLength(single), scenes: sceneCount,
  offline: path.join(root, "dist", "index.html") }));
