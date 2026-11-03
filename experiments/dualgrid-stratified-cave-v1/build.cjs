const fs=require("fs"),path=require("path"),crypto=require("crypto");
const root=__dirname,base=path.resolve(root,"../dualgrid-cave-exploration-v1");
const baseBuild="records/2026-11-01T10-02-10-373Z-build.json";
const baseline=JSON.parse(fs.readFileSync(path.join(base,baseBuild),"utf8"));
const sha=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
for (const source of baseline.sources)
  if (sha(fs.readFileSync(path.join(base,source.file)))!==source.sha256)
    throw Error("Pinned base changed; review before adapting: "+source.file);
if (sha(fs.readFileSync(path.join(base,"index.html")))!==baseline.standalone.sha256)
  throw Error("Pinned base standalone changed");
const read=file=>fs.readFileSync(path.join(base,file),"utf8");
function replace(text,from,to) {
  if (!text.includes(from)) throw Error("Missing pinned adapter anchor: "+from);
  return text.replaceAll(from,to);
}
let view=replace(read("view.html"),"dg-cave-whitebox","dg-stratified-cave");
view=replace(view,"地下探索 · 圆角白模","天然洞穴 · 层状弯折");
view=replace(view,"固定洞形 / Dual Grid 16型","方整岩层 / 自然转折 / Dual Grid");
let renderer=read("renderer.js");
renderer=replace(renderer,"      ctx.fillStyle=palette.air;ctx.fillRect(0,0,width,height);",
  "      ctx.fillStyle=palette.rock;ctx.fillRect(0,0,width,height);\n"+
  "      const corner=screen(state.camera,0,0);\n"+
  "      ctx.fillStyle=palette.air;ctx.fillRect(corner.x,corner.y,world.width*state.camera.scale,world.height*state.camera.scale);");
let state=replace(read("state.js"),"dark-nights-dualgrid-cave-whitebox-v1",
  "dark-nights-dualgrid-stratified-cave-v1");
state=replace(state,"dg-cave-v1","dg-stratified-v1");
state=replace(state,"dualgrid-cave-exploration","dualgrid-stratified-cave");
const app=replace(read("app.js"),"dg-cave-whitebox","dg-stratified-cave");
const scripts=[read("dual-grid.js"),fs.readFileSync(path.join(root,"layout.js"),"utf8"),
  read("player.js"),renderer,state,app];
const fragment=view+"\n"+scripts.map(text=>"<script>\n"+text+"\n</script>").join("\n")+"\n";
if (Buffer.byteLength(fragment)>=1000000) throw Error("Fragment exceeds 1 MB");
const previous=read("index.html"),start=previous.indexOf("<main>")+6,end=previous.lastIndexOf("</main>");
if (start<6||end<=start) throw Error("Standalone wrapper anchors changed");
let standalone=previous.slice(0,start)+fragment+previous.slice(end);
standalone=replace(standalone,"地下探索 · Dual Grid圆角白模","天然洞穴 · 层状弯折白模");
fs.writeFileSync(path.join(root,"index.html"),standalone);
const destination=process.argv[2];
if (destination) {
  fs.mkdirSync(path.dirname(path.resolve(destination)),{recursive:true});
  fs.writeFileSync(destination,fragment);
}
const inputs=[...baseline.sources.map(r=>({
  file:path.relative(root,path.join(base,r.file)).replaceAll("\\","/"),sha256:r.sha256})),
  {file:"../dualgrid-cave-exploration-v1/index.html",sha256:baseline.standalone.sha256},
  ...["layout.js","build.cjs"].map(file=>({file,sha256:sha(fs.readFileSync(path.join(root,file)))}))];
const record={date:"2026-11-01",baseBuild:"../dualgrid-cave-exploration-v1/"+baseBuild,
  scope:"Separate stratified natural cave; earlier cave and mine sources, outputs and state keys preserved.",
  adapters:["Unique component ID and visible names","Independent storage key and state kind",
    "New stratified profiles and irregular cleft layout","Solid backdrop outside finite world bounds"],
  standalone:{file:"index.html",bytes:Buffer.byteLength(standalone),sha256:sha(standalone)},
  fragment:destination?{file:path.resolve(destination),bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}:null,
  sources:inputs};
fs.mkdirSync(path.join(root,"records"),{recursive:true});
const receipt=path.join(root,"records",new Date().toISOString().replace(/[:.]/g,"-")+"-build.json");
fs.writeFileSync(receipt,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({standalone:path.join(root,"index.html"),fragment:destination||null,
  bytes:record.standalone.bytes,record:receipt}));
