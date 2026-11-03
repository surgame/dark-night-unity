const fs=require("fs"),path=require("path"),crypto=require("crypto");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const root=__dirname;
const out="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785";
const hash=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
(async()=>{
  const profiles=JSON.parse(fs.readFileSync(path.join(root,"art-profiles.json"),"utf8"));
  const inputs=[
    ["atlas","sources/cave-modules-generated.png"],
    ["hero","../20261029-wfc-cave-kit/sources/hero-scale-reference.png"]
  ];
  const art={},records=[];
  fs.mkdirSync(path.join(root,"preview-cache"),{recursive:true});
  for(const [key,file] of inputs) {
    const bytes=fs.readFileSync(path.join(root,file)),meta=await sharp(bytes).metadata();
    const encoded=await sharp(bytes).webp({quality:key==="atlas"?88:90,alphaQuality:100}).toBuffer();
    fs.writeFileSync(path.join(root,"preview-cache",key+".webp"),encoded);
    art[key]="data:image/webp;base64,"+encoded.toString("base64");
    records.push({key,file,width:meta.width,height:meta.height,bytes:bytes.length,sha256:hash(bytes),previewBytes:encoded.length});
  }
  const files=["solver.js","physics.js","painter.js","app.js"];
  const code=files.map(file=>fs.readFileSync(path.join(root,file),"utf8")).join("\n");
  const fragment=fs.readFileSync(path.join(root,"preview.template.html"),"utf8")
    .replace("/*__DATA__*/","const MACRO_PROFILES="+JSON.stringify(profiles)+";\nconst MACRO_ART="+JSON.stringify(art)+";")
    .replace("/*__CODE__*/",code);
  if(Buffer.byteLength(fragment)>1000000)throw Error("Fragment exceeds one megabyte.");
  fs.mkdirSync(out,{recursive:true});
  fs.writeFileSync(path.join(out,"modular-cave-roam.html"),fragment);
  fs.writeFileSync(path.join(root,"manifest.json"),JSON.stringify({
    tool:"Built-in image_gen.imagegen",sources:records,
    inputIdentity:["sources/module-layout-guide.png","sources/user-style-reference.png",
      "art-profiles.json","preview.template.html","generation-prompt.json"].map(file=>({
        file,sha256:hash(fs.readFileSync(path.join(root,file)))
      })),
    generatedSource:"C:/Users/Jobscn/.codex/generated_images/01a1891b-2542-7ea1-a69e-12cf28b0c785/call_ttmji1sdeUmKyNAh3PuGbnMi.png",
    code:files.map(file=>({file,sha256:hash(fs.readFileSync(path.join(root,file)))})),
    preview:path.join(out,"modular-cave-roam.html"),fragmentBytes:Buffer.byteLength(fragment),
    caveat:"Generated source is 1254x1254 and did not preserve the requested layout geometry. Native 623x414 crops exclude divider lines; collision profiles are manually traced from these actual crops. Six source panels plus six explicit horizontal reflections. Fixed native crop overlays bridge joints. Browser-only one-dimensional strip, not Unity or Spine."
  },null,2));
  console.log(JSON.stringify({ok:true,bytes:Buffer.byteLength(fragment),sources:records,preview:path.join(out,"modular-cave-roam.html")}));
})().catch(error=>{console.error(error);process.exit(1);});
