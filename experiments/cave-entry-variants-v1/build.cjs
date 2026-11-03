const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const {variants} = require("./variants.cjs");

const root = path.resolve(__dirname, "../..");
const destination = process.argv[2];
const records = process.argv[3];
if (!destination || !records) throw new Error("Provide fragment output and record directory.");
const hero = path.join(root,"Game/Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png");
const source = path.join(__dirname,"source.fragment.html");
const identity = file => {
  const bytes = fs.readFileSync(file);
  return {path:path.resolve(file),bytes:bytes.length,sha256:crypto.createHash("sha256").update(bytes).digest("hex")};
};
const input = fs.readFileSync(source,"utf8");
const output = input.replace("__SCHEMES__",JSON.stringify(variants)).replace("__HERO__",JSON.stringify("data:image/png;base64,"+fs.readFileSync(hero).toString("base64")));
if (/__SCHEMES__|__HERO__/.test(output)) throw new Error("Unexpanded source placeholder.");
fs.mkdirSync(path.dirname(destination),{recursive:true});
fs.mkdirSync(records,{recursive:true});
if (fs.existsSync(destination)) {
  const backup=path.join(records,`fragment-before-${Date.now()}.html`);
  fs.copyFileSync(destination,backup);
}
fs.writeFileSync(destination,output,"utf8");
const record = {
  createdLocalDate:"2026-11-03",
  sources:[source,path.join(__dirname,"variants.cjs"),__filename,hero].map(identity),
  output:identity(destination),
  design:{count:variants.length,unitWorld:3.2,moduleDimensions:[6.4,3.2],modules:2,heroHeight:.48,strictSideView:true,sizeDraftUserConfirmed:false},
  scope:"Design and finite ecology motion preview only; no Game writes, native assets, WFC solve, collision or actor tests."
};
fs.writeFileSync(path.join(records,`build-${Date.now()}.json`),JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({output:record.output,variants:variants.length}));
