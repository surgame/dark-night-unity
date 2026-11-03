const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const vm = require("node:vm");
const { variants } = require("../cave-entry-variants-v1/variants.cjs");
const destination = process.argv[2], evidence = process.argv[3];
if (!destination || !evidence) throw new Error("Provide visualization directory and evidence directory.");
const root = path.resolve(__dirname, "../..");
const hero = path.join(root, "Game/Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png");
const read = name => fs.readFileSync(path.join(__dirname, name), "utf8");
const identity = file => {
  const bytes = fs.readFileSync(file);
  return { path: path.resolve(file), bytes: bytes.length, sha256: crypto.createHash("sha256").update(bytes).digest("hex") };
};
let fragment = read("console.fragment.html");
for (const [token, value] of Object.entries({
  __STYLE__: read("console.css"), __SCHEMES__: JSON.stringify(variants),
  __HERO__: JSON.stringify("data:image/png;base64," + fs.readFileSync(hero).toString("base64")),
  __LOGIC__: read("console.js")
})) fragment = fragment.split(token).join(value);
if (/__[A-Z_]+__/.test(fragment)) throw new Error("Unexpanded placeholder.");
if (Buffer.byteLength(fragment) >= 1000000) throw new Error("Visualization exceeds size contract.");
for (const match of fragment.matchAll(/<script>([\s\S]*?)<\/script>/g)) new vm.Script(match[1]);
fs.mkdirSync(destination, { recursive: true });
fs.mkdirSync(evidence, { recursive: true });
const output = path.join(destination, "mapmodule-runtime-debug.html");
if (fs.existsSync(output)) fs.copyFileSync(output, path.join(evidence, "before-fragment-" + Date.now() + ".html"));
fs.writeFileSync(output, fragment, "utf8");
const report = {
  date: "2026-11-03", scope: "In-conversation UI proposal only. No Unity/network/game execution or asset writes.",
  inputs: ["console.fragment.html", "console.css", "console.js", "build.cjs"].map(name => identity(path.join(__dirname, name))),
  hero: identity(hero), schemes: identity(path.join(__dirname, "../cave-entry-variants-v1/variants.cjs")), output: identity(output)
};
const file = path.join(evidence, "build-" + Date.now() + ".json");
fs.writeFileSync(file, JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ output: report.output, report: file }));
