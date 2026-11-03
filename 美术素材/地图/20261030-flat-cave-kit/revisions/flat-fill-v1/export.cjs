const fs=require("fs"),path=require("path"),crypto=require("crypto"),assert=require("assert");
const deps="C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const {chromium}=require(path.join(deps,"playwright")),sharp=require(path.join(deps,"sharp"));
const root=__dirname,out=path.join(root,"exports");
const preview="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a18896-3db9-7340-b606-b6184a07f49c";
const hash=b=>crypto.createHash("sha256").update(b).digest("hex"),files=[],maps=[],checks=[],errors=[];
const sampleOnly=process.argv.includes("--sample-only");
if(sampleOnly) {
  const previous=JSON.parse(fs.readFileSync(path.join(root,"verification.json"),"utf8"));
  files.push(...previous.files.filter(f=>!f.file.startsWith("mining/")));
  maps.push(...previous.maps);
}
function save(name,bytes) {
  const target=path.join(out,name);fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,bytes);
  files.push({file:name,bytes:bytes.length,sha256:hash(bytes)});return bytes;
}
const image=(name,url)=>save(name,Buffer.from(url.split(",")[1],"base64"));
let browser;
(async()=>{
  browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:1056,height:1000}});
  page.on("pageerror",e=>errors.push(e.message));
  await page.route("**/*",r=>r.request().url().startsWith("http")?r.abort():r.continue());
  await page.goto("file:///"+preview+"/flat-wfc-browser.html");
  const frame=page.frames()[1];
  await frame.waitForFunction(()=>document.getElementById("flat-cave-kit")?.dataset.ready==="true");
  const sourceRecords=JSON.parse(fs.readFileSync(path.join(root,"sources-manifest.json"),"utf8"));
  const raw=Object.fromEntries(sourceRecords.map(s=>[s.key,"data:image/png;base64,"+fs.readFileSync(path.join(root,s.path)).toString("base64")]));
  await frame.evaluate(async sources=>{
    const images={};await Promise.all(Object.entries(sources).map(async([key,url])=>{
      const im=new Image();im.src=url;await im.decode();images[key]=im;
    }));window.nativeFlatArt=FlatCave.prepare(images);
  },raw);
  if(!sampleOnly)image("materials/rock-periodic.png",await frame.evaluate(()=>nativeFlatArt.rock.toDataURL()));
  for(const seed of sampleOnly?[]:[271,902,1643]) {
    const result=await frame.evaluate(async seed=>{
      const map=await document.getElementById("flat-cave-kit")._art.ensure(seed),a=FlatCave,U=a.U;
      const c=a.canvas(5120,3072),ctx=c.getContext("2d"),box={x:0,y:0,w:80*U,h:48*U};
      a.draw(ctx,nativeFlatArt,map,box,5120,3072);const painted=c.toDataURL();
      a.draw(ctx,nativeFlatArt,map,box,5120,3072,{transparent:true});const foreground=c.toDataURL();
      const local=a.canvas(1536,864),node=map.plan.nodes[3];
      a.draw(local.getContext("2d"),nativeFlatArt,map,{x:node.x*U-768,y:node.y*U-400,w:1536,h:864},1536,864,{hero:true,room:3});
      return {painted,foreground,local:local.toDataURL(),data:{
        seed,width:80,height:48,terrainCells:[320,192],cellsPerModule:4,pixelsPerCell:16,artUnitsPerModule:U,outputPixels:[5120,3072],
        tiles:Array.from(map.tiles),rawContours:map.geometry.contours,paintedContours:map.visual.contours,roomMarkers:map.plan.nodes,
        check:{comparisons:map.check.comparisons,mismatches:map.check.mismatches,cornerConflicts:map.check.cornerConflicts,nonManifold:map.geometry.nonManifold},
        visualContract:"Global material phase and shared rounded contours. Rounded art differs from raw WFC sockets; not current Unity collision."}};
    },seed);
    assert.equal(result.data.check.mismatches,0);assert.equal(result.data.check.cornerConflicts,0);assert.equal(result.data.check.nonManifold,0);
    image(`maps/${seed}-painted.png`,result.painted);const fg=image(`maps/${seed}-foreground.png`,result.foreground);
    image(`maps/${seed}-local.png`,result.local);save(`maps/${seed}-layout.json`,JSON.stringify(result.data));
    const original=await sharp(fg).ensureAlpha().raw().toBuffer(),rebuilt=Buffer.alloc(original.length),chunks=[];
    for(let y=0;y<6;y++)for(let x=0;x<10;x++){
      const bytes=await sharp(fg).extract({left:x*512,top:y*512,width:512,height:512}).png().toBuffer();
      const name=`chunks/${seed}/${x}-${y}.png`;save(name,bytes);
      const raw=await sharp(bytes).ensureAlpha().raw().toBuffer();
      for(let row=0;row<512;row++)raw.copy(rebuilt,((y*512+row)*5120+x*512)*4,row*2048,(row+1)*2048);
      chunks.push({file:name,x:x*512,y:y*512,width:512,height:512});
    }
    assert(original.equals(rebuilt));
    save(`maps/${seed}-chunks.json`,JSON.stringify({seed,width:5120,height:3072,chunks},null,2));
    maps.push({seed,...result.data.check,chunks:chunks.length,reassemblyExact:true});
  }
  if(!sampleOnly) {
  const atlas=await frame.evaluate(()=>{
    const c=FlatCave.canvas(2048,2048),m=FlatCave.canvas(2048,2048),ctx=c.getContext("2d"),mc=m.getContext("2d"),entries=[];
    for(let id=0;id<256;id++){
      const x=id%16*128,y=Math.floor(id/16)*128;
      ctx.drawImage(FlatCave.tileCanvas(nativeFlatArt,id,128,true),x,y);
      mc.drawImage(FlatCave.tileCanvas(nativeFlatArt,id,128,false),x,y);
      const tile=CaveWB.catalog[id];entries.push({id,mask:tile.mask,values:tile.values,edges:tile.edges,rect:[x,y,128,128],polygons:FlatCave.tilePolygons(tile)});
    }
    return {painted:c.toDataURL(),mask:m.toDataURL(),entries};
  });
  image("geometry-painted-reference.png",atlas.painted);image("geometry-alpha-atlas.png",atlas.mask);
  save("socket-catalog.json",JSON.stringify({version:1,levels:[-3,-1,1,3],cornerOrder:["TL","TR","BR","BL"],edgeOrder:["N","E","S","W"],
    cellsPerModule:4,entries:atlas.entries,contract:"Painted atlas is phase-zero reference only. For seamless use world-coordinate material with shared contours, or the baked map chunks."},null,2));
  }
  const stages=[];
  for(const stage of [0,1,2]){
    const result=await frame.evaluate(stage=>{
      const sample=FlatCave.mining.render(nativeFlatArt,stage,false),bare=FlatCave.mining.render(nativeFlatArt,stage,false,false);
      return {scene:sample.canvas.toDataURL(),lit:FlatCave.mining.render(nativeFlatArt,stage,true).canvas.toDataURL(),
        foreground:sample.foreground.toDataURL(),bare:bare.canvas.toDataURL(),cells:sample.cells,bounds:sample.bounds};
    },stage);
    image(`mining/stage-${stage}.png`,result.scene);image(`mining/stage-${stage}-lit.png`,result.lit);
    const fg=image(`mining/stage-${stage}-foreground.png`,result.foreground);
    stages.push({stage,cells:result.cells,bounds:result.bounds,fg,raw:await sharp(Buffer.from(result.bare.split(",")[1],"base64")).ensureAlpha().raw().toBuffer()});
  }
  assert.deepEqual(stages.map(s=>s.cells.length),[0,4,15]);
  for(let i=1;i<3;i++) {
    let changed=0,outside=0;const a=stages[0].raw,b=stages[i].raw;
    for(let p=0;p<a.length;p+=4)if(!a.subarray(p,p+4).equals(b.subarray(p,p+4))){
      changed++;const x=(p/4)%1600,y=Math.floor(p/4/1600);
      if(x<950||x>1180||y<565||y>715)outside++;
    }
    assert(changed>0);assert.equal(outside,0,"Excavation must not repaint distant terrain or background");
    const alpha=await sharp(stages[i].fg).ensureAlpha().raw().toBuffer();
    for(const [x,y] of stages[i].cells)assert.equal(alpha[(((y*32+16)*1600+x*32+16)*4)+3],0);
    checks.push({stage:i,changedPixels:changed,changesOutsideLocalRegion:outside,cutCentersTransparent:true});
  }
  save("mining/stages.json",JSON.stringify({source:"Independent authored visual sample, not a WFC map or game simulation.",
    worldCellArtUnits:32,equivalentGameCellPixels:16,stages:stages.map(({stage,cells,bounds})=>({stage,cells,bounds})),
    light:"Illustrative local shading only, no gameplay visibility, shadow or shared-light claim."},null,2));
  for(const mode of ["mining","local","overview","sources"]){
    await frame.locator("#flat-view").selectOption(mode);
    assert.equal((await frame.evaluate(()=>document.getElementById("flat-cave-kit")._inspect())).state.view,mode);
    await frame.locator("#flat-cave-kit").screenshot({path:preview+`/flat-final-${mode}.png`});
  }
  await frame.locator("#flat-view").selectOption("mining");
  for(const stage of ["0","1","2"]){
    await frame.locator("#flat-stage").selectOption(stage);
    assert.equal((await frame.evaluate(()=>document.getElementById("flat-cave-kit")._inspect())).state.stage,+stage);
  }
  await frame.locator("#flat-light").check();
  assert.equal((await frame.evaluate(()=>document.getElementById("flat-cave-kit")._inspect())).state.light,true);
  await frame.locator("#flat-cave-kit").screenshot({path:preview+"/flat-final-light.png"});
  for(const width of [736,360,320]){
    await page.setViewportSize({width,height:1000});
    for(const theme of ["light","dark"]){
      await page.emulateMedia({colorScheme:theme});
      const layout=await frame.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,canvas:document.getElementById("flat-scene").clientWidth}));
      assert(layout.scroll<=layout.width+1);assert(layout.canvas>200);
      checks.push({width,theme,...layout});
      await frame.locator("#flat-cave-kit").screenshot({path:preview+`/flat-${width}-${theme}.png`});
    }
  }
  await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:
    {version:1,view:"local",stage:1,light:false,seed:902,room:3,cx:29,cy:20,span:9}}}}})));
  await frame.waitForFunction(()=>document.getElementById("flat-cave-kit")._inspect().seed===902&&!document.getElementById("flat-cave-kit")._inspect().busy);
  assert.equal((await frame.evaluate(()=>document.getElementById("flat-cave-kit")._inspect())).state.span,9);checks.push({stateRestore:true});
  assert.equal(errors.length,0,errors.join("\n"));
  const provenance=JSON.parse(fs.readFileSync(path.join(root,"geometry-provenance.json"),"utf8").replace(/^\uFEFF/,""));
  for(const entry of provenance){
    assert.equal(hash(fs.readFileSync(entry.source)),entry.sha256);
    assert.equal(hash(fs.readFileSync(path.join(root,entry.file))),entry.sha256);
  }
  const prompts=JSON.parse(fs.readFileSync(path.join(root,"prompts.json"),"utf8"));
  for(const call of prompts.calls)assert(fs.readFileSync(path.join(root,call.output)).equals(fs.readFileSync(call.generatedSource)));
  const report={ok:true,scope:"Offline art output, socket invariants, chunk integrity and preview controls only. No Unity, traversal, physics or gameplay test.",
    sources:sourceRecords,geometry:provenance,maps,checks,files,errors,sampleOnly,
    code:fs.readdirSync(path.join(root,"renderer")).map(name=>({name,sha256:hash(fs.readFileSync(path.join(root,"renderer",name)))})),
    previewBytes:fs.statSync(path.join(preview,"flat-wfc-cave.html")).size};
  fs.writeFileSync(path.join(root,"verification.json"),JSON.stringify(report,null,2));
  console.log(JSON.stringify({ok:true,maps,checks:checks.length,files:files.length,bytes:files.reduce((n,f)=>n+f.bytes,0)}));
})().catch(error=>{
  fs.mkdirSync(out,{recursive:true});
  const stamp=new Date().toISOString().replace(/[:.]/g,"-");
  fs.writeFileSync(path.join(out,"failure-"+stamp+".json"),JSON.stringify({error:error.stack,maps,checks,errors},null,2));
  console.error(error);process.exitCode=1;
}).finally(async()=>{if(browser)await browser.close();});
