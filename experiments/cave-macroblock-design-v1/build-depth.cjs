const fs=require("fs"),path=require("path"),crypto=require("crypto");
const base=__dirname,output=process.argv[2];
if(!output || !path.isAbsolute(output))throw new Error("Absolute fragment output required.");
const sha=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
const original=JSON.parse(fs.readFileSync(path.join(base,"records/2026-11-01T18-13-22-467Z-build.json"),"utf8"));
const elevation=JSON.parse(fs.readFileSync(path.join(base,"records/2026-11-01T19-02-48-440Z-elevation-build.json"),"utf8"));
for(const record of [original,elevation]){
  for(const [name,hash] of Object.entries(record.inputs)){
    if(sha(fs.readFileSync(path.join(base,name)))!==hash)throw new Error("Prior source changed: "+name);
  }
  if(sha(fs.readFileSync(record.output.path))!==record.output.sha256)throw new Error("Prior visualization changed.");
}
const inputs=["catalog.js","depth-layout.js","depth-view.html","depth-app.js","DEPTH.md"];
const fragment=fs.readFileSync(path.join(base,"depth-view.html"),"utf8")+
  "\n<script>\n"+["catalog.js","depth-layout.js","depth-app.js"].map(name=>fs.readFileSync(path.join(base,name),"utf8")).join("\n")+"\n</script>\n";
if(Buffer.byteLength(fragment)>=1000000)throw new Error("Fragment exceeds limit.");
fs.writeFileSync(output,fragment,"utf8");
const record={
  createdAt:new Date().toISOString(),scope:"Surface entrance and vertical design schematic; no game physics, solver, reachability or Unity validation.",
  inputs:Object.fromEntries(inputs.map(name=>[name,sha(fs.readFileSync(path.join(base,name)))])),
  priorVersionsPreserved:true,output:{path:output,bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}
};
const file=path.join(base,"records",record.createdAt.replace(/[:.]/g,"-")+"-depth-build.json");
fs.writeFileSync(file,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({ok:true,output,bytes:record.output.bytes,record:file}));
