"use strict";
const fs=require("node:fs"),path=require("node:path"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-verification");fs.mkdirSync(folder,{recursive:true});
const inputDir=path.resolve(__dirname,"../../artifacts/terrain-render-bench-20261101");fs.mkdirSync(inputDir,{recursive:true});
const report={date:"2026-11-01",scope:"Independent render strategy comparison; shape equality, bounded invalidation, browser timings and Unity frozen input.",views:[],layouts:[],errors:[]};
(async()=>{
  let browser;
  try{
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
    const page=await browser.newPage({viewport:{width:768,height:1100}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/terrain-performance-browser.html");
    const frame=page.frames()[1],root=frame.locator("#terrain-performance-study");
    const fragment=fs.readFileSync(base+"/terrain-performance.html","utf8").replace(/\r\n/g,"\n").trim();
    const embedded=await page.evaluate(html=>new DOMParser().parseFromString(html,"text/html").querySelector("iframe").getAttribute("data-srcdoc"),fs.readFileSync(base+"/terrain-performance-browser.html","utf8"));
    assert(embedded.includes(fragment),"Inspection wrapper does not contain current fragment.");
    await frame.waitForFunction(()=>{const r=document.getElementById("terrain-performance-study");return r?.dataset.ready||r?.dataset.error;},{timeout:60000});
    assert(!await root.getAttribute("data-error"),await root.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("terrain-performance-study")._inspect());
    if(process.argv.includes("--final")){
      assert.equal(await frame.locator("#tb-runtime").inputValue(),"unity");
      assert.equal(await frame.locator("#tb-results tr").count(),3);
      assert((await frame.locator("#tb-method-note").innerText()).includes("RTX 5060 Ti"));
      await frame.locator("#tb-runtime").selectOption("browser");
      assert((await frame.locator("#tb-method-note").innerText()).includes("Chromium"));
      await frame.locator("#tb-runtime").selectOption("unity");
      report.sourcesSwitch=true;
    }
    const initial=await inspect(),hashes=[];
    for(const mode of ["baked","strip","atlas"]){
      await frame.locator('[data-mode="'+mode+'"]').click();assert.equal((await inspect()).hash,initial.hash);
      await root.screenshot({path:path.join(folder,mode+"-initial.png")});
      await frame.evaluate(()=>{
        const c=document.getElementById("tb-map"),gl=c.getContext("webgl"),r=document.getElementById("terrain-performance-study");
        r._pixelBefore=new Uint8Array(c.width*c.height*4);gl.readPixels(0,0,c.width,c.height,gl.RGBA,gl.UNSIGNED_BYTE,r._pixelBefore);
      });
      await frame.locator("#tb-hit").click();await frame.locator("#tb-blast").click();const changed=await inspect();
      assert.notEqual(changed.hash,initial.hash);hashes.push(changed.hash);
      assert(changed.last.chunks>0&&changed.last.chunks<=4);
      const farChanges=await frame.evaluate(()=>{
        const c=document.getElementById("tb-map"),gl=c.getContext("webgl"),r=document.getElementById("terrain-performance-study"),now=new Uint8Array(c.width*c.height*4);
        gl.readPixels(0,0,c.width,c.height,gl.RGBA,gl.UNSIGNED_BYTE,now);let changes=0;
        for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++){
          if(x>=60&&x<=530&&c.height-y-1>=440)continue;
          const i=(y*c.width+x)*4;if([0,1,2,3].some(k=>now[i+k]!==r._pixelBefore[i+k]))changes++;
        }
        return changes;
      });
      assert.equal(farChanges,0,mode+" changed remote pixels.");
      await frame.locator("#tb-zones").check();
      await root.screenshot({path:path.join(folder,mode+"-broken.png")});
      report.views.push({mode,hash:changed.hash,stats:changed.stats,last:changed.last,farChanges});
      await frame.locator("#tb-reset").click();assert.equal((await inspect()).hash,initial.hash);
    }
    assert.equal(new Set(hashes).size,1,"Strategies changed terrain differently.");
    report.geometry=await frame.evaluate(()=>{
      const M=TerrainBench,a=M.create(),before=Array.from({length:60},(_,k)=>M.chunk(a,k,"strip"));
      const atlasBefore=Array.from({length:60},(_,k)=>M.chunk(a,k,"atlas"));
      const d=M.damage(a,31,110,2.2);let unchanged=0,stableFar=0;
      for(let k=0;k<60;k++){
        const now=M.chunk(a,k,"strip"),at=M.chunk(a,k,"atlas");
        if(!d.dirty.includes(k)){
          for(let i=0;i<3;i++)if(String(now[i])!==String(before[k][i]))throw Error("Remote strip geometry/UV changed.");
          if(String(at[0])!==String(atlasBefore[k][0]))throw Error("Remote atlas geometry changed.");
          unchanged++;
        }
      }
      for(let m=0;m<16;m++)for(const p of M.polygon[m])for(const v of p)if(v.some(n=>!Number.isFinite(n)||n<0||n>1))throw Error("Invalid polygon.");
      // Every modified lattice vertex has exactly the incident cells in the invalidation set.
      for(const i of d.changed)for(const dy of [-1,0])for(const dx of [-1,0])
        if(!d.tiles.includes((Math.floor(i/321)+dy)*320+i%321+dx))throw Error("Missing dependent tile.");
      return{unchangedChunks:unchanged,changedChunks:d.dirty.length,farTextureCoordinatesStable:true,shapeHash:M.hash(a)};
    });
    if(!process.argv.includes("--final")){
    const frozen=await frame.evaluate(()=>document.getElementById("terrain-performance-study")._tools.unityInput());
    const serialized=JSON.stringify(frozen);
    const inputPath=path.join(inputDir,"input-960x640.json");
    if(fs.existsSync(inputPath))assert.equal(fs.readFileSync(inputPath,"utf8"),serialized,"Frozen input changed.");
    else fs.writeFileSync(inputPath,serialized);
    report.unityInput={path:inputPath,bytes:Buffer.byteLength(serialized),sha256:crypto.createHash("sha256").update(serialized).digest("hex"),frames:frozen.frames.length};
    const measured=await frame.evaluate(async()=>{
      const r=document.getElementById("terrain-performance-study");return await r._tools.measure(r._tools.art);
    });
    measured.sourceLabel="本机 Chromium 离屏实测（3 轮）；含 GPU／驱动等待与读回，不是 Unity 帧率。无光照、碰撞、生态或联网。";
    fs.writeFileSync(path.join(__dirname,"measured-browser.json"),JSON.stringify(measured,null,2)+"\n");
    fs.writeFileSync(path.join(folder,"browser-performance.json"),JSON.stringify(measured,null,2)+"\n");
    report.measurement=measured;
    }
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1200});await page.emulateMedia({colorScheme:theme});
      const b=await frame.evaluate(()=>{
        const r=document.getElementById("terrain-performance-study"),b=r.getBoundingClientRect();
        return{overflow:document.documentElement.scrollWidth>innerWidth,clipped:[...r.querySelectorAll("button,input")].filter(n=>{
          const a=n.getBoundingClientRect();return a.left<b.left-1||a.right>b.right+1;
        }).length};
      });
      assert(!b.overflow&&!b.clipped);report.layouts.push({width,theme,...b});
      await root.screenshot({path:path.join(folder,width+"-"+theme+".png")});
    }
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{
      privateContent:{version:1,mode:"strip",hits:[{x:25,y:108,r:.75}],zones:true}
    }}}})));
    const restored=await inspect();assert.equal(restored.state.mode,"strip");assert.equal(restored.state.hits.length,1);assert(restored.state.zones);
    assert.equal(report.errors.length,0);
    report.fragment={sha256:crypto.createHash("sha256").update(fs.readFileSync(base+"/terrain-performance.html")).digest("hex")};
    report.passed=true;
  }catch(e){report.passed=false;report.failure=e.stack;process.exitCode=1;}
  finally{await browser?.close();fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,views:report.views.length,layouts:report.layouts.length,geometry:report.geometry,
      unityInput:report.unityInput,failure:report.failure,errors:report.errors}));}
})();
