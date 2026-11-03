const fs=require("fs"),path=require("path"),crypto=require("crypto");
const deps="C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const sharp=require(path.join(deps,"sharp"));
const root=__dirname;
const out="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1885a-d455-7a51-ad10-5e3b12d43271";
const sourceNames={rock:"rock.png",floor:"floor-wide-v2.png",ceiling:"ceiling.png",backdrop:"backdrop.png",ore:"ore.png",hero:"hero-scale-reference.png"};
(async()=>{
  fs.mkdirSync(out,{recursive:true});fs.mkdirSync(path.join(root,"preview-cache"),{recursive:true});
  const sources={},inputs=[];
  for(const [key,name] of Object.entries(sourceNames)) {
    const file=path.join(root,"sources",name), bytes=fs.readFileSync(file),meta=await sharp(bytes).metadata();
    const webp=await sharp(bytes).webp({quality:64,alphaQuality:90}).toBuffer();
    // Preview-only encoding. Native generated sources are preserved byte-for-byte.
    fs.writeFileSync(path.join(root,"preview-cache",key+".webp"),webp);
    sources[key]="data:image/webp;base64,"+webp.toString("base64");
    inputs.push({name,width:meta.width,height:meta.height,alpha:meta.hasAlpha,bytes:bytes.length,sha256:crypto.createHash("sha256").update(bytes).digest("hex")});
  }
  // Preview encodings preserve native dimensions and the brush coordinate system.
  const sourceCode="const CAVE_SOURCES="+JSON.stringify(sources)+";\n";
  const files=["geometry-snapshot/catalog.js","geometry-snapshot/plan.js","geometry-snapshot/solver.js","renderer/art-renderer.js","renderer/app.js"];
  const code=files.map(file=>fs.readFileSync(path.join(root,file),"utf8")).join("\n");
  const html=fs.readFileSync(path.join(root,"renderer/preview.html"),"utf8").replace("/*__SOURCES__*/",sourceCode).replace("/*__CODE__*/",code);
  if(Buffer.byteLength(html)>1000000)throw Error("Inline preview exceeds 1 MB");
  fs.writeFileSync(path.join(out,"painted-wfc-cave.html"),html);
  fs.writeFileSync(path.join(root,"sources-manifest.json"),JSON.stringify(inputs,null,2));
  console.log(JSON.stringify({preview:path.join(out,"painted-wfc-cave.html"),bytes:Buffer.byteLength(html),sources:inputs}));
})();
