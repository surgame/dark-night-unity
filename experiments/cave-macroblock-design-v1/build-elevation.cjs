const fs=require("fs"),path=require("path"),crypto=require("crypto");
const output=process.argv[2],base=__dirname;
if(!output || !path.isAbsolute(output))throw new Error("Absolute fragment output required.");
const sha=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
const inputs=["catalog.js","elevation.js","elevation-view.html","elevation-app.js","ELEVATION.md"];
const original=["catalog.js","app.js","view.html","README.md"];
const prior=JSON.parse(fs.readFileSync(path.join(base,"records/2026-11-01T18-13-22-467Z-build.json"),"utf8"));
for(const file of original){
  if(sha(fs.readFileSync(path.join(base,file)))!==prior.inputs[file])throw new Error("Original design changed: "+file);
}
const fragment=fs.readFileSync(path.join(base,"elevation-view.html"),"utf8")+
  "\n<script>\n"+["catalog.js","elevation.js","elevation-app.js"].map(file=>fs.readFileSync(path.join(base,file),"utf8")).join("\n")+"\n</script>\n";
if(Buffer.byteLength(fragment)>=1000000)throw new Error("Fragment too large.");
fs.writeFileSync(output,fragment,"utf8");
const record={
  createdAt:new Date().toISOString(),scope:"Elevation design display only; no game physics, traversal, solver or Unity validation.",
  inputs:Object.fromEntries(inputs.map(file=>[file,sha(fs.readFileSync(path.join(base,file)))])),
  originalPreserved:original,output:{path:output,bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}
};
const recordPath=path.join(base,"records",record.createdAt.replace(/[:.]/g,"-")+"-elevation-build.json");
fs.writeFileSync(recordPath,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({ok:true,output,bytes:record.output.bytes,record:recordPath}));
