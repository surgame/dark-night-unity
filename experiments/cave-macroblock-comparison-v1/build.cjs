const fs=require("fs"),path=require("path"),crypto=require("crypto");
const root=__dirname,base=path.resolve(root,"../dualgrid-cave-exploration-v1");
const baseRecord="records/2026-11-01T10-02-10-373Z-build.json";
const pinned=JSON.parse(fs.readFileSync(path.join(base,baseRecord),"utf8"));
const sha=b=>crypto.createHash("sha256").update(b).digest("hex");
const imported=["dual-grid.js","player.js"];
for(const file of imported) {
  const expected=pinned.sources.find(s=>s.file===file);
  if(sha(fs.readFileSync(path.join(base,file)))!==expected.sha256) throw Error("Pinned source changed: "+file);
}
const local=["geometry.js","modules.js","solver.js","renderer.js","app.js"];
const scriptFiles=[...imported.map(file=>({dir:base,file})),...local.map(file=>({dir:root,file}))];
const fragment=fs.readFileSync(path.join(root,"view.html"),"utf8")+"\n"+
  scriptFiles.map(s=>"<script>\n"+fs.readFileSync(path.join(s.dir,s.file),"utf8")+"\n</script>").join("\n")+"\n";
if(Buffer.byteLength(fragment)>=1000000) throw Error("Fragment exceeds 1 MB");
const shell=fs.readFileSync(path.join(base,"index.html"),"utf8");
if(sha(shell)!==pinned.standalone.sha256) throw Error("Pinned standalone wrapper changed");
const start=shell.indexOf("<main>")+6,end=shell.lastIndexOf("</main>");
let standalone=shell.slice(0,start)+fragment+shell.slice(end);
standalone=standalone.replace("地下探索 · Dual Grid圆角白模","洞穴轮廓 · 六种构造");
standalone=standalone.replace("--ring:light-dark","--font-size-base:14px;--border:light-dark(#bbc0c3,#4b5258);--ring:light-dark");
fs.writeFileSync(path.join(root,"index.html"),standalone);
const destination=process.argv[2];
if(destination) fs.writeFileSync(destination,fragment);
const sources=[{file:"view.html"},{file:"build.cjs"},
  ...scriptFiles.map(s=>({file:path.relative(root,path.join(s.dir,s.file)).replaceAll("\\","/")}))]
  .map(s=>({...s,sha256:sha(fs.readFileSync(path.join(root,s.file)))}));
const record={date:"2026-11-01",scope:"Six fixed-terrain contour and macroblock whiteboxes; no production or destruction changes.",
  pinnedBase:"../dualgrid-cave-exploration-v1/"+baseRecord,sources,
  standalone:{file:"index.html",bytes:Buffer.byteLength(standalone),sha256:sha(standalone)},
  fragment:destination?{file:path.resolve(destination),bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}:null};
fs.mkdirSync(path.join(root,"records"),{recursive:true});
const receipt=path.join(root,"records",new Date().toISOString().replace(/[:.]/g,"-")+"-build.json");
fs.writeFileSync(receipt,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({record:receipt,bytes:record.standalone.bytes,fragmentBytes:record.fragment?.bytes}));
