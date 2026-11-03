const fs=require("fs"),path=require("path"),crypto=require("crypto");
const base=__dirname,output=process.argv[2];
if(!output||!path.isAbsolute(output))throw new Error("An absolute fragment output path is required.");
const sha=data=>crypto.createHash("sha256").update(data).digest("hex");
const priorBase=path.resolve(base,"../compact-natural-mine-v1");
const priorFile=path.join(priorBase,"records/2026-11-01T20-32-35-376Z-build.json");
const prior=JSON.parse(fs.readFileSync(priorFile,"utf8"));
for(const [name,digest] of Object.entries(prior.inputs)){
  if(sha(fs.readFileSync(path.join(priorBase,name)))!==digest)throw new Error("Prior source changed: "+name);
}
if(sha(fs.readFileSync(prior.output.path))!==prior.output.sha256)throw new Error("Prior fragment changed.");
const inputs=["data.js","geometry.js","view.html","app.js","README.md"];
const fragment=fs.readFileSync(path.join(base,"view.html"),"utf8")+"\n<script>\n"+
  ["data.js","geometry.js","app.js"].map(name=>fs.readFileSync(path.join(base,name),"utf8")).join("\n")+"\n</script>\n";
if(Buffer.byteLength(fragment)>=1000000||/<!doctype|<html|<head>|<body>|fetch\(|XMLHttpRequest|WebSocket/i.test(fragment))throw new Error("Fragment contract failed.");
fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,fragment,"utf8");
const record={
  createdAt:new Date().toISOString(),scope:"Two authored cave space proposals, no gameplay physics or Unity validation.",
  inputs:Object.fromEntries(inputs.map(name=>[name,sha(fs.readFileSync(path.join(base,name)))])),
  priorPreserved:{record:priorFile,fragment:prior.output.path,sha256:prior.output.sha256},
  output:{path:output,bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}
};
fs.mkdirSync(path.join(base,"records"),{recursive:true});
const file=path.join(base,"records",record.createdAt.replace(/[:.]/g,"-")+"-build.json");
fs.writeFileSync(file,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({ok:true,output,bytes:record.output.bytes,record:file}));
