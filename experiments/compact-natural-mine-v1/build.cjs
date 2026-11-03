const fs=require("fs"),path=require("path"),crypto=require("crypto");
const output=process.argv[2],base=__dirname;
if(!output || !path.isAbsolute(output))throw new Error("An absolute fragment output path is required.");
const sha=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
const priorBase=path.resolve(base,"../cave-macroblock-design-v1");
const priorFile=path.join(priorBase,"records/2026-11-01T19-32-56-128Z-depth-build.json");
const prior=JSON.parse(fs.readFileSync(priorFile,"utf8"));
for(const [name,digest] of Object.entries(prior.inputs)){
  if(sha(fs.readFileSync(path.join(priorBase,name)))!==digest)throw new Error("Prior source changed: "+name);
}
if(sha(fs.readFileSync(prior.output.path))!==prior.output.sha256)throw new Error("Prior fragment changed.");
const inputs=["data.js","view.html","app.js","README.md"];
const fragment=fs.readFileSync(path.join(base,"view.html"),"utf8")+"\n<script>\n"+
  ["data.js","app.js"].map(name=>fs.readFileSync(path.join(base,name),"utf8")).join("\n")+"\n</script>\n";
if(Buffer.byteLength(fragment)>=1000000)throw new Error("Fragment too large.");
fs.writeFileSync(output,fragment,"utf8");
fs.mkdirSync(path.join(base,"records"),{recursive:true});
const record={
  createdAt:new Date().toISOString(),scope:"Compact natural mine design; suggested geometric route only, no actual traversal, timing or Unity validation.",
  inputs:Object.fromEntries(inputs.map(name=>[name,sha(fs.readFileSync(path.join(base,name)))])),
  priorPreserved:{record:priorFile,fragment:prior.output.path,sha256:prior.output.sha256},
  output:{path:output,bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}
};
const recordPath=path.join(base,"records",record.createdAt.replace(/[:.]/g,"-")+"-build.json");
fs.writeFileSync(recordPath,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({ok:true,output,bytes:record.output.bytes,record:recordPath}));
