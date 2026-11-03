const fs=require("fs"),path=require("path"),crypto=require("crypto");
const deps="C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const sharp=require(path.join(deps,"sharp"));
const root=__dirname;
const out="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a18896-3db9-7340-b606-b6184a07f49c";
const names={rock:"sources/rock.png",backdrop:"sources/backdrop.png",ore:"sources/ore.png",
  hero:"../20261029-wfc-cave-kit/sources/hero-scale-reference.png"};
(async()=>{
  const sources={},records=[];
  for(const [key,file] of Object.entries(names)){
    const input=path.join(root,file),bytes=fs.readFileSync(input),meta=await sharp(bytes).metadata();
    const encoded=await sharp(bytes).webp({quality:80,alphaQuality:100}).toBuffer();
    fs.mkdirSync(path.join(root,"preview-cache"),{recursive:true});
    fs.writeFileSync(path.join(root,"preview-cache",key+".webp"),encoded);
    sources[key]="data:image/webp;base64,"+encoded.toString("base64");
    const stats=await sharp(bytes).stats();
    records.push({key,path:file,width:meta.width,height:meta.height,hasAlpha:meta.hasAlpha,
      alphaRange:meta.hasAlpha?[stats.channels.at(-1).min,stats.channels.at(-1).max]:null,
      bytes:bytes.length,sha256:crypto.createHash("sha256").update(bytes).digest("hex")});
  }
  const files=["geometry-snapshot/catalog.js","geometry-snapshot/plan.js","geometry-snapshot/solver.js",
    "renderer/geometry.js","renderer/materials.js","renderer/mining.js","renderer/app.js"];
  const code=files.map(f=>fs.readFileSync(path.join(root,f),"utf8")).join("\n");
  const fragment=fs.readFileSync(path.join(root,"renderer/preview.html"),"utf8")
    .replace("/*__SOURCES__*/","const FLAT_SOURCES="+JSON.stringify(sources)+";")
    .replace("/*__CODE__*/",code);
  if(Buffer.byteLength(fragment)>1000000)throw Error("Preview exceeds 1 MB");
  fs.mkdirSync(out,{recursive:true});
  fs.writeFileSync(path.join(out,"flat-wfc-cave.html"),fragment);
  fs.writeFileSync(path.join(root,"sources-manifest.json"),JSON.stringify(records,null,2));
  console.log(JSON.stringify({preview:path.join(out,"flat-wfc-cave.html"),bytes:Buffer.byteLength(fragment),sources:records}));
})().catch(e=>{console.error(e);process.exit(1);});
