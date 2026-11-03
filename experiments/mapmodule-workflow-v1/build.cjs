const fs=require("node:fs"),path=require("node:path"),crypto=require("node:crypto"),vm=require("node:vm");
const {variants}=require("../cave-entry-variants-v1/variants.cjs");
const root=path.resolve(__dirname,"../..");
const destination=process.argv[2],records=process.argv[3];
if(!destination||!records)throw new Error("Provide output directory and evidence directory.");
fs.mkdirSync(destination,{recursive:true});fs.mkdirSync(records,{recursive:true});
const identity=file=>{const bytes=fs.readFileSync(file);return {path:path.resolve(file),bytes:bytes.length,sha256:crypto.createHash("sha256").update(bytes).digest("hex")};};
const read=name=>fs.readFileSync(path.join(__dirname,name),"utf8");
const hero=path.join(root,"Game/Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png");
const outputs=[];
for(const kind of ["workbench","lab"]) {
  let fragment=read(kind+".fragment.html");
  const bindings={__PRODUCT_CSS__:read("product.css"),__SCHEMES__:JSON.stringify(variants),__HERO__:JSON.stringify("data:image/png;base64,"+fs.readFileSync(hero).toString("base64")),__SCENE_JS__:read("scene.js"),__WORKBENCH_JS__:read("workbench.js"),__LAB_JS__:read("lab.js")};
  for(const [token,value] of Object.entries(bindings))fragment=fragment.split(token).join(value);
  if(/__[A-Z_]+__/.test(fragment))throw new Error("Unexpanded placeholder.");
  if(Buffer.byteLength(fragment)>1000000)throw new Error("Fragment too large.");
  for(const match of fragment.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(match[1],{filename:kind+".fragment.js"});
  const file=path.join(destination,"mapmodule-"+kind+"-ui.html");
  if(fs.existsSync(file))fs.copyFileSync(file,path.join(records,kind+"-before-"+Date.now()+".html"));
  fs.writeFileSync(file,fragment,"utf8");outputs.push(identity(file));
}
const files=fs.readdirSync(__dirname).filter(name=>fs.statSync(path.join(__dirname,name)).isFile());
const report={date:"2026-11-03",scope:"Two UI design prototypes only; all product actions are local demonstrations, not Unity/network calls.",inputs:[...files.map(name=>identity(path.join(__dirname,name))),identity(hero),identity(path.join(__dirname,"../cave-entry-variants-v1/variants.cjs"))],outputs,completed:true};
const reportPath=path.join(records,"build-"+Date.now()+".json");
fs.writeFileSync(reportPath,JSON.stringify(report,null,2)+"\n");
console.log(JSON.stringify({outputs,report:reportPath}));
