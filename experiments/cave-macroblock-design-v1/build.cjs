const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const base = __dirname;
const output = process.argv[2];
if (!output || !path.isAbsolute(output)) throw new Error("An absolute fragment output path is required.");
const inputs = ["catalog.js", "view.html", "app.js", "README.md"];
const hash = data => crypto.createHash("sha256").update(data).digest("hex");
const fragment = fs.readFileSync(path.join(base, "view.html"), "utf8") +
  "\n<script>\n" + fs.readFileSync(path.join(base, "catalog.js"), "utf8") +
  "\n" + fs.readFileSync(path.join(base, "app.js"), "utf8") + "\n</script>\n";
if (Buffer.byteLength(fragment) >= 1000000) throw new Error("Fragment exceeds size contract.");
fs.writeFileSync(output, fragment, "utf8");
const folder = path.join(base, "records");
fs.mkdirSync(folder, {recursive: true});
const record = {
  createdAt: new Date().toISOString(),
  scope: "Design proposal and schematic only; no physics, solver, Unity, gameplay or performance verification.",
  inputs: Object.fromEntries(inputs.map(name => [name, hash(fs.readFileSync(path.join(base, name)))])),
  output: {path: output, bytes: Buffer.byteLength(fragment), sha256: hash(fragment)}
};
const file = path.join(folder, record.createdAt.replace(/[:.]/g, "-") + "-build.json");
fs.writeFileSync(file, JSON.stringify(record, null, 2) + "\n");
console.log(JSON.stringify({ok: true, output, bytes: record.output.bytes, record: file}));
