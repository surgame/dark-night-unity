const fs=require("fs"),path=require("path"),crypto=require("crypto"),root=__dirname;
const base=path.resolve(root,"../dualgrid-cave-exploration-v1");
const sha=b=>crypto.createHash("sha256").update(b).digest("hex");
const pinned=JSON.parse(fs.readFileSync(path.join(base,"records/2026-11-01T10-02-10-373Z-build.json"),"utf8"));
const imported=fs.readFileSync(path.join(base,"dual-grid.js"),"utf8");
if(sha(imported)!==pinned.sources.find(s=>s.file==="dual-grid.js").sha256) throw Error("Pinned source changed");
const core=["plan.js","solver.js","field.js","mesh.js","gpu.js","benchmark.js"],local=[...core,"app.js"];
let reference=null;
if(fs.existsSync(path.join(root,"reference.json"))) {
  reference=JSON.parse(fs.readFileSync(path.join(root,"reference.json"),"utf8"));
  for(const source of reference.sourceHashes)
    if(sha(fs.readFileSync(path.join(root,source.file)))!==source.sha256) throw Error("Measurement source changed: "+source.file);
}
const scripts=[imported,...core.map(file=>fs.readFileSync(path.join(root,file),"utf8")),
  "globalThis.CavePerf.reference="+JSON.stringify(reference)+";",fs.readFileSync(path.join(root,"app.js"),"utf8")];
const fragment=fs.readFileSync(path.join(root,"view.html"),"utf8")+"\n"+
  scripts.map(s=>"<script>\n"+s+"\n</script>").join("\n")+"\n";
if(Buffer.byteLength(fragment)>=1000000) throw Error("Fragment exceeds 1 MB");
const shell=fs.readFileSync(path.join(base,"index.html"),"utf8");
if(sha(shell)!==pinned.standalone.sha256) throw Error("Pinned shell changed");
const at=shell.indexOf("<main>")+6,end=shell.lastIndexOf("</main>");
let standalone=shell.slice(0,at)+fragment+shell.slice(end);
standalone=standalone.replace("地下探索 · Dual Grid圆角白模","洞网性能 · 预设房间与随机连接");
standalone=standalone.replace("--ring:light-dark","--font-size-base:14px;--border:light-dark(#bbc0c3,#4b5258);--ring:light-dark");
standalone=standalone.replace("</style>"," .table{width:100%;border-collapse:collapse}.table th,.table td{text-align:left;padding:8px 7px;border-bottom:1px solid var(--border)}.table .text-end{text-align:right}.table-responsive{overflow-x:auto}.text-small{font-size:12px}.text-muted{color:var(--muted-foreground)}.table caption{text-align:left;padding:8px 0}.form-check{display:inline-flex;gap:5px}</style>");
fs.writeFileSync(path.join(root,"index.html"),standalone);
const output=process.argv[2];if(output)fs.writeFileSync(output,fragment);
const record={date:"2026-11-01",scope:"Independent WebGL2 benchmark; no Unity or production changes.",
  sources:[...local,"view.html","build.cjs",...(reference?["reference.json"]:[])].map(file=>({
    file,sha256:sha(fs.readFileSync(path.join(root,file)))})),
  imports:[{file:"../dualgrid-cave-exploration-v1/dual-grid.js",sha256:sha(imported)}],
  standalone:{file:"index.html",bytes:Buffer.byteLength(standalone),sha256:sha(standalone)},
  fragment:output?{file:path.resolve(output),bytes:Buffer.byteLength(fragment),sha256:sha(fragment)}:null};
fs.mkdirSync(path.join(root,"records"),{recursive:true});
const receipt=path.join(root,"records",new Date().toISOString().replace(/[:.]/g,"-")+"-build.json");
fs.writeFileSync(receipt,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({receipt,bytes:record.standalone.bytes,fragmentBytes:record.fragment?.bytes,reference:!!reference}));
