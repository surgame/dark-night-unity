"use strict";
const fs=require("fs"),path=require("path"),assert=require("assert"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const out=path.join(base,"ecology-review"),report={date:"2026-10-31",errors:[],layouts:[],walks:[],motion:{},interactions:{}};
fs.mkdirSync(out,{recursive:true});
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try {
    const page=await browser.newPage({viewport:{width:768,height:1250}});
    page.on("pageerror",error=>report.errors.push(error.message));
    await page.goto("file:///"+base+"/ecology-walk-browser.html");
    const frame=page.frames()[1];
    await frame.waitForFunction(()=>document.getElementById("ecology-walk")?.dataset.ready==="true");
    const inspect=()=>frame.evaluate(()=>document.getElementById("ecology-walk")._inspect());
    const capture=async name=>frame.locator("#ecology-walk").screenshot({path:path.join(out,name+".png")});
    for(const width of [736,360,320])for(const theme of ["light","dark"]) {
      await page.setViewportSize({width:width+32,height:1250});await page.emulateMedia({colorScheme:theme});
      await frame.locator("#ew-room-select").selectOption("r6");
      await frame.waitForFunction(()=>{const r=document.getElementById("ecology-walk")._authoring.renderer;return r.focus==="r6"&&r.width>0;});
      const layout=await frame.evaluate(()=>{
        const r=document.getElementById("ecology-walk"),b=r.getBoundingClientRect(),nodes=[...r.querySelectorAll("button,input,select")];
        return {width:b.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:nodes.filter(n=>{const x=n.getBoundingClientRect();return x.left<b.left-1||x.right>b.right+1;}).map(n=>n.id||n.textContent),
          canvas:[r.querySelector("canvas").clientWidth,r.querySelector("canvas").clientHeight]};
      });
      assert(!layout.overflow&&!layout.clipped.length,"Layout overflow "+JSON.stringify(layout));
      report.layouts.push({width,theme,...layout});await capture("final-"+width+"-"+theme);
    }
    await page.setViewportSize({width:768,height:1250});await page.emulateMedia({colorScheme:"light"});
    await frame.locator("#ew-room-select").selectOption("r6");
    await frame.waitForFunction(()=>{const a=document.getElementById("ecology-walk")._authoring,r=a.world.rooms[6];return Math.abs(a.renderer.camera.x-(r.x+312-a.renderer.width/2))<.05;});
    const canvas=frame.locator("#ew-canvas");
    const a=await canvas.screenshot();
    await frame.evaluate(()=>new Promise(resolve=>setTimeout(resolve,420)));
    const b=await canvas.screenshot();
    const pixels=async buf=>sharp(buf).removeAlpha().raw().toBuffer();
    const pa=await pixels(a),pb=await pixels(b);let changed=0;
    for(let i=0;i<pa.length;i+=3)if(Math.abs(pa[i]-pb[i])+Math.abs(pa[i+1]-pb[i+1])+Math.abs(pa[i+2]-pb[i+2])>12)changed++;
    assert(changed>100,"Ecology layer did not animate.");
    report.motion.changedPixelsOver420ms=changed;
    await frame.locator("#ew-motion").uncheck();
    await frame.evaluate(()=>new Promise(resolve=>setTimeout(resolve,150)));
    const freezeA=await canvas.screenshot();await frame.evaluate(()=>new Promise(resolve=>setTimeout(resolve,350)));
    const freezeB=await canvas.screenshot();const pfa=await pixels(freezeA),pfb=await pixels(freezeB);let frozenChanged=0;
    for(let i=0;i<pfa.length;i+=3)if(Math.abs(pfa[i]-pfb[i])+Math.abs(pfa[i+1]-pfb[i+1])+Math.abs(pfa[i+2]-pfb[i+2])>12)frozenChanged++;
    assert(frozenChanged<20,"Motion toggle did not settle.");report.motion.frozenChangedPixels=frozenChanged;
    await frame.locator("#ew-motion").check();
    for(const id of ["r0","r2","r3","r4","r5","r6","r7"]) {
      await frame.locator("#ew-room-select").selectOption(id);
      await frame.waitForFunction(id=>{const a=document.getElementById("ecology-walk")._authoring,r=a.world.rooms.find(r=>r.id===id),target=Math.max(0,Math.min(a.world.bounds.w-a.renderer.width,r.x+312-a.renderer.width/2));return Math.abs(a.renderer.camera.x-target)<1;},id);
      await capture("final-room-"+id);
    }
    await frame.locator("#ew-start").click();
    const before=(await inspect()).state.hero.x;await page.keyboard.down("KeyD");
    await frame.evaluate(()=>new Promise(resolve=>setTimeout(resolve,300)));await page.keyboard.up("KeyD");
    const after=await inspect();assert(after.active&&after.state.hero.x>before+20,"Manual movement or save echo reset failed.");
    report.interactions.keyboard={before,after:after.state.hero.x,active:after.active};
    await frame.locator("#ew-room-select").selectOption("r2");
    await frame.locator("#ew-start").click();
    assert((await inspect()).active&&(await inspect()).focus===null,"One-click return to the hero failed.");
    report.interactions.oneClickReturn=true;
    await frame.locator("#ew-start").click();
    const touch=frame.locator("[data-hold=right]"),bb=await touch.boundingBox();
    await page.mouse.move(bb.x+bb.width/2,bb.y+bb.height/2);await page.mouse.down();
    await frame.evaluate(()=>new Promise(resolve=>setTimeout(resolve,180)));await page.mouse.up();
    assert((await inspect()).state.hero.x>after.state.hero.x+10,"Pointer movement failed.");report.interactions.pointer=true;
    for(const seed of [271,902,1643]) {
      await frame.evaluate(seed=>document.getElementById("ecology-walk")._authoring.install(seed),seed);
      await frame.locator("#ew-auto").click();
      const walk=await frame.evaluate(()=>{
        const root=document.getElementById("ecology-walk"),a=root._authoring,visited=new Set();let steps=0;
        while(steps<16000&&!a.engine.completed) {
          const s=a.engine.snapshot(),room=a.world.rooms.find(r=>s.hero.x>=r.x&&s.hero.x<=r.x+623);
          if(room)visited.add(room.id);
          a.engine.step(1/60,a.automated(s,1/60));steps++;
        }
        a.automated(a.engine.snapshot(),1/60);
        return {seed:a.world.seed,steps,completed:a.engine.completed,visited:[...visited],flags:a.engine.flags,
          bridge:a.engine.objects.find(o=>o.type==="bridge").built,hero:a.engine.hero,
          stateBytes:new TextEncoder().encode(JSON.stringify(a.engine.exportState())).length,
          events:a.engine.events.map(e=>e.type)};
      });
      assert(walk.completed&&walk.visited.length===8&&walk.bridge,"Integrated autopilot failed "+JSON.stringify(walk));
      assert(walk.stateBytes<14000,"State exceeds host snapshot budget.");
      report.walks.push(walk);
    }
    await capture("final-delivery");
    const old=(await inspect()).seed;await frame.locator("#ew-new").click();
    assert((await inspect()).seed===old+1009,"Regeneration failed.");report.interactions.regenerate=true;
    await frame.locator("#ew-map").click();assert((await inspect()).overview,"Overview failed.");await capture("final-overview");
    const restore=await frame.evaluate(()=>{
      const root=document.getElementById("ecology-walk"),a=root._authoring;
      const state={version:1,seed:a.world.seed,engine:a.engine.exportState(),visited:["r0","r1"],focus:"r2",overview:false,motion:false};
      window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:state}}}}));
      return root._inspect();
    });
    assert(restore.focus==="r2"&&!restore.motion&&restore.visited.length===2,"State restore failed.");
    report.interactions.stateRestore=true;
    const state=await inspect();
    report.inventory={catalog:state.catalog.length,rooms:state.rooms.length,plants:state.rooms.reduce((n,r)=>n+r.plants,0),water:state.rooms.reduce((n,r)=>n+r.water,0)};
    assert(!report.errors.length,"Runtime errors "+report.errors.join(","));
    report.source={path:base+"/ecology-walk.html",sha256:crypto.createHash("sha256").update(fs.readFileSync(base+"/ecology-walk.html")).digest("hex")};
  } catch(error) {report.failure=error.stack;process.exitCode=1;}
  finally {
    const filename=path.join(__dirname,"verification-browser.json");
    if(fs.existsSync(filename)){const prior=JSON.parse(fs.readFileSync(filename));report.previousAttempts=prior.failure?[...(prior.previousAttempts||[]),prior]:prior.previousAttempts||[];}
    fs.writeFileSync(filename,JSON.stringify(report,null,2)+"\n");await browser.close();
    console.log(JSON.stringify({errors:report.errors,failure:report.failure,layouts:report.layouts.length,motion:report.motion,walks:report.walks.map(x=>({seed:x.seed,steps:x.steps,completed:x.completed})),interactions:report.interactions},null,2));
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
