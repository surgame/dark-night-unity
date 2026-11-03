const fs=require("fs"),path=require("path"),crypto=require("crypto"),assert=require("assert");
const dependencies="C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const {chromium}=require(path.join(dependencies,"playwright"));
const sharp=require(path.join(dependencies,"sharp"));
const root=__dirname, output=path.join(root,"exports");
const preview="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1885a-d455-7a51-ad10-5e3b12d43271";
const hash=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
function saveData(name,url) {
  const bytes=Buffer.from(url.split(",")[1],"base64"),file=path.join(output,name);
  fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,bytes);
  return {name,bytes:bytes.length,sha256:hash(bytes)};
}
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:1056,height:1050}}),errors=[],checks=[],files=[],maps=[];
  page.on("pageerror",error=>errors.push(error.message));
  await page.route("**/*",route=>route.request().url().startsWith("http")?route.abort():route.continue());
  await page.goto("file:///"+preview+"/browser-preview.html");
  const frame=page.frames()[1];
  await frame.waitForFunction(()=>document.getElementById("painted-wfc-cave")?.dataset.ready==="true");
  await frame.locator("#painted-wfc-cave").screenshot({path:preview+"/final-local.png"});
  const rawSources={};
  for(const [key,file] of Object.entries({rock:"rock.png",floor:"floor-wide-v2.png",ceiling:"ceiling.png",backdrop:"backdrop.png",ore:"ore.png",hero:"hero-scale-reference.png"})) {
    rawSources[key]="data:image/png;base64,"+fs.readFileSync(path.join(root,"sources",file)).toString("base64");
  }
  await frame.evaluate(async sources=>{
    const images={};
    await Promise.all(Object.entries(sources).map(async([key,url])=>{const image=new Image();image.src=url;await image.decode();images[key]=image;}));
    window.nativeArt=CaveArt.prepare(images);
  },rawSources);
  for(const seed of [271,902,1643]) {
    await frame.evaluate(async seed=>{await document.getElementById("painted-wfc-cave")._art.select(seed);},seed);
    const result=await frame.evaluate(()=>{
      const r=document.getElementById("painted-wfc-cave"),m=r._art.map;
      const art=CaveArt,U=art.U,w=5120,h=3072,c=art.canvas(w,h),ctx=c.getContext("2d");
      art.draw(ctx,window.nativeArt,m,{x:0,y:0,w:80*U,h:48*U},w,h,{});
      const painted=c.toDataURL("image/png");
      art.draw(ctx,window.nativeArt,m,{x:0,y:0,w:80*U,h:48*U},w,h,{transparent:true});
      const foreground=c.toDataURL("image/png");
      const local=art.canvas(1536,864),node=m.plan.nodes[3],box={x:node.x*U-768,y:node.y*U-400,w:1536,h:864};
      art.draw(local.getContext("2d"),window.nativeArt,m,box,1536,864,{hero:true,room:3});
      let cornerDisplacement=0;
      m.geometry.contours.forEach((contour,c)=>{
        const points=contour.points.slice(0,-1);
        points.forEach((p,i)=>{const q=m.artContours[c].points[i*13+6];cornerDisplacement=Math.max(cornerDisplacement,Math.hypot(q.x-p.x,q.y-p.y));});
      });
      return {painted,foreground,local:local.toDataURL("image/png"),cornerDisplacement,
        data:{version:1,seed:m.seed,width:m.width,height:m.height,terrainCells:[320,192],
          terrainPixelsPerCell:16,cellsPerModule:4,artUnitsPerModule:U,mapExportPixels:[w,h],
          tiles:Array.from(m.tiles),stats:m.stats,
          check:{comparisons:m.check.comparisons,mismatches:m.check.mismatches,cornerConflicts:m.check.cornerConflicts,nonManifold:m.geometry.nonManifold},
          rawContours:m.geometry.contours,paintedContours:m.artContours,
          roomMarkers:m.plan.nodes,profileRule:"Shared whole-contour corner rounding: trim 40% of each adjacent segment and join through the old corner with a quadratic Bezier, sampled at 12 intervals. Raw WFC sockets remain logical constraints; final painted contours and baked chunk edges are the visual boundary contract."}};
    });
    assert(result.cornerDisplacement<=.3);assert.equal(result.data.check.mismatches,0);assert.equal(result.data.check.nonManifold,0);
    files.push(saveData(`maps/${seed}-painted.png`,result.painted));
    files.push(saveData(`maps/${seed}-foreground.png`,result.foreground));
    files.push(saveData(`maps/${seed}-local.png`,result.local));
    fs.writeFileSync(path.join(output,"maps",seed+"-layout.json"),JSON.stringify(result.data));
    maps.push({seed,...result.data.check,maxRoundedCornerMidpointDisplacementModules:result.cornerDisplacement,observations:result.data.stats.observations});
    const bytes=fs.readFileSync(path.join(output,"maps",seed+"-foreground.png"));
    const chunks=[];
    for(let y=0;y<6;y++)for(let x=0;x<10;x++) {
      const chunk=await sharp(bytes).extract({left:x*512,top:y*512,width:512,height:512}).png().toBuffer();
      const name=`chunks/${seed}/${x}-${y}.png`;fs.mkdirSync(path.dirname(path.join(output,name)),{recursive:true});
      fs.writeFileSync(path.join(output,name),chunk);files.push({name,bytes:chunk.length,sha256:hash(chunk)});
      chunks.push({file:name,x:x*512,y:y*512,width:512,height:512});
    }
    fs.writeFileSync(path.join(output,"maps",seed+"-chunks.json"),JSON.stringify({seed,width:5120,height:3072,chunks},null,2));
    const original=await sharp(bytes).raw().toBuffer();
    const rebuilt=Buffer.alloc(original.length);
    for(const c of chunks) {
      const pixels=await sharp(path.join(output,c.file)).raw().toBuffer();
      for(let row=0;row<512;row++) pixels.copy(rebuilt,((c.y+row)*5120+c.x)*4,row*512*4,(row+1)*512*4);
    }
    assert(original.equals(rebuilt),"Chunk reconstruction must be pixel exact");
    checks.push({seed,chunkReconstructionPixelsEqual:true,chunkCount:chunks.length});
    await frame.locator("#art-all").click();await frame.locator("#art-scene").screenshot({path:preview+`/map-${seed}-overview.png`});
  }
  const atlas=await frame.evaluate(()=>{
    const painted=CaveArt.canvas(4096,4096),masks=CaveArt.canvas(4096,4096),pc=painted.getContext("2d"),mc=masks.getContext("2d");
    const entries=[];
    for(let id=0;id<256;id++){
      const x=id%16*256,y=Math.floor(id/16)*256;
      pc.drawImage(CaveArt.tileCanvas(nativeArt,id,256,true),x,y);
      mc.drawImage(CaveArt.tileCanvas(nativeArt,id,256,false),x,y);
      const tile=CaveWB.catalog[id];
      entries.push({id,mask:tile.mask,values:tile.values,edges:tile.edges,sourceRect:[x,y,256,256],polygons:CaveArt.tilePolygons(tile)});
    }
    return {painted:painted.toDataURL("image/png"),masks:masks.toDataURL("image/png"),entries};
  });
  files.push(saveData("geometry-painted-reference.png",atlas.painted));
  files.push(saveData("geometry-alpha-atlas.png",atlas.masks));
  fs.writeFileSync(path.join(output,"socket-catalog.json"),JSON.stringify({
    version:1,cornerOrder:["TL","TR","BR","BL"],edgeOrder:["N","E","S","W"],positive:"solid",levels:[-3,-1,1,3],
    cellsPerModule:4,atlasPixelsPerModule:256,worldPixelsPerModule:64,
    adjacency:"N/S and E/W ordered scalar-pair equality; 16 possible socket values.",
    renderContract:"Use the alpha/geometry atlas with world-coordinate material sampling and shared contour tangents. Painted atlas is a phase-zero reference, not a standalone seamless tile atlas.",
    bakedChunks:"exports/chunks are final RGBA foreground images, reconstructed by each maps/*-chunks.json. For new WFC maps rerun the included renderer.",
    entries:atlas.entries},null,2));
  await frame.evaluate(async()=>{await document.getElementById("painted-wfc-cave")._art.select(271);});
  for(const mode of ["scene","geometry","catalog","sources"]) {
    await frame.locator("#art-mode").selectOption(mode);
    assert.equal((await frame.evaluate(()=>document.getElementById("painted-wfc-cave")._inspect())).state.mode,mode);
    checks.push({mode,ok:true});
    if(mode==="catalog"||mode==="sources")await frame.locator("#painted-wfc-cave").screenshot({path:preview+"/"+mode+".png"});
  }
  await frame.locator("#art-mode").selectOption("scene");
  await frame.locator("#art-grid").check();
  assert.equal((await frame.evaluate(()=>document.getElementById("painted-wfc-cave")._inspect())).state.grid,true);
  await frame.locator("#art-grid").uncheck();await frame.locator("#art-local").click();
  for(const width of [736,360,320]) {
    await page.setViewportSize({width,height:1000});
    for(const theme of ["light","dark"]) {
      await page.emulateMedia({colorScheme:theme});
      const layout=await frame.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,canvas:document.getElementById("art-scene").getBoundingClientRect().width}));
      assert(layout.scroll<=layout.width+1,JSON.stringify(layout));assert(layout.canvas>200);
      await frame.locator("#painted-wfc-cave").screenshot({path:preview+`/responsive-${width}-${theme}.png`});
      checks.push({width,theme,layout});
    }
  }
  await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{version:1,seed:902,mode:"scene",grid:true,cx:32,cy:17,span:8}}}}})));
  await frame.waitForFunction(()=>document.getElementById("painted-wfc-cave")._inspect().seed===902);
  const restored=await frame.evaluate(()=>document.getElementById("painted-wfc-cave")._inspect());
  assert.equal(restored.state.span,8);assert.equal(restored.state.grid,true);checks.push({stateRestore:true});
  assert.equal(errors.length,0,JSON.stringify(errors));
  const sources=JSON.parse(fs.readFileSync(path.join(root,"sources-manifest.json"),"utf8"));
  const identities=["catalog.js","plan.js","solver.js"].map(name=>({name,sha256:hash(fs.readFileSync(path.join(root,"geometry-snapshot",name)))}));
  const report={ok:true,scope:"Browser art assembly, geometry/socket and exported image checks only; no Unity run or character traversal acceptance.",
    previewSha256:hash(fs.readFileSync(path.join(preview,"painted-wfc-cave.html"))),sources,geometrySnapshot:identities,maps,checks,errors,files};
  fs.writeFileSync(path.join(root,"verification.json"),JSON.stringify(report,null,2));
  console.log(JSON.stringify({ok:true,maps,checks:checks.length,files:files.length,bytes:files.reduce((n,f)=>n+f.bytes,0)}));
  await browser.close();
})().catch(error=>{console.error(error);process.exit(1);});
