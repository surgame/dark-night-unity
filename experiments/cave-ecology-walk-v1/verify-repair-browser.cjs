"use strict";
const fs=require("node:fs"),path=require("node:path"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const stamp=Date.now(),out=path.join(__dirname,"verification-records","browser-"+stamp);
fs.mkdirSync(out,{recursive:true});
const report={date:"2026-10-31",errors:[],layouts:[],seams:[],interactions:{},scope:"Current browser candidate only; no Unity or production art acceptance."};
async function difference(a,b) {
  const pixels=async buffer=>sharp(buffer).removeAlpha().raw().toBuffer();
  const first=await pixels(a),last=await pixels(b);let changed=0;
  for(let i=0;i<first.length;i+=3)
    if(Math.abs(first[i]-last[i])+Math.abs(first[i+1]-last[i+1])+Math.abs(first[i+2]-last[i+2])>12)changed++;
  return changed;
}
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try {
    const page=await browser.newPage({viewport:{width:768,height:1250}});
    page.on("pageerror",error=>report.errors.push(error.message));
    await page.goto("file:///"+base+"/ecology-walk-repaired-browser.html");
    const frame=page.frames()[1];
    await frame.waitForFunction(()=>document.getElementById("ecology-walk")?.dataset.ready==="true");
    const inspect=()=>frame.evaluate(()=>document.getElementById("ecology-walk")._inspect());
    const capture=name=>frame.locator("#ecology-walk").screenshot({path:path.join(out,name+".png")});
    const initial=await inspect();report.buildId=initial.buildId;
    assert(await frame.evaluate(()=>{
      const a=document.getElementById("ecology-walk")._authoring;
      return !!a.world.rooms.find(room=>room.id===a.renderer.focus)?.authoredArt;
    }),"First view must show the actual author bridge.");
    await page.waitForTimeout(650);await capture("author-bridge");
    for(const width of [736,360,320])for(const theme of ["light","dark"]) {
      await page.setViewportSize({width:width+32,height:1250});await page.emulateMedia({colorScheme:theme});
      const layout=await frame.evaluate(()=>{
        const root=document.getElementById("ecology-walk"),bounds=root.getBoundingClientRect();
        return {width:bounds.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:[...root.querySelectorAll("button,input,select")].filter(node=>{
            const b=node.getBoundingClientRect();return b.width>0&&(b.left<bounds.left-1||b.right>bounds.right+1);
          }).map(node=>node.id||node.textContent)};
      });
      assert(!layout.overflow&&!layout.clipped.length,"Layout overflow "+JSON.stringify(layout));
      report.layouts.push({width,theme,...layout});await capture(width+"-"+theme);
    }
    await page.setViewportSize({width:768,height:1250});await page.emulateMedia({colorScheme:"light"});
    await frame.locator("#ew-inspect").click();
    for(let index=0;index<7;index++) {
      await frame.locator("#ew-seam-select").selectOption(String(index));
      const seam=(await inspect()).assembly;
      assert(seam.enabled&&seam.index===index&&seam.hasAuthoredShapes&&seam.hasPortSignatures);
      report.seams.push(seam);
    }
    await capture("seam-7");
    await frame.locator("#ew-room-select").selectOption("r6");await page.waitForTimeout(900);
    const canvas=frame.locator("#ew-canvas"),first=await canvas.screenshot();
    await page.waitForTimeout(420);report.motionPixels=await difference(first,await canvas.screenshot());
    assert(report.motionPixels>100,"Ecology animation has no visible change.");
    await frame.locator("#ew-motion").uncheck();await page.waitForTimeout(250);
    const frozen=await canvas.screenshot();await page.waitForTimeout(350);
    report.frozenPixels=await difference(frozen,await canvas.screenshot());
    assert(report.frozenPixels<20,"Motion-off view did not settle.");
    await frame.locator("#ew-motion").check();
    await frame.locator("#ew-start").click();
    const before=(await inspect()).state.hero.x;await page.keyboard.down("KeyD");
    await page.waitForTimeout(300);await page.keyboard.up("KeyD");
    assert((await inspect()).state.hero.x>before+20,"Manual movement failed.");
    report.interactions.keyboard=true;
    report.interactions.failedGenerationRetainsScene=await frame.evaluate(()=>{
      const root=document.getElementById("ecology-walk"),a=root._authoring,old=a.engine,world=a.world;
      const create=window.EcologyWorld.create;window.EcologyWorld.create=()=>{throw new Error("fixture contradiction");};
      try {return !a.install(999)&&a.engine===old&&a.world===world&&!root.querySelector("#ew-status").hidden;}
      finally {window.EcologyWorld.create=create;}
    });
    assert(report.interactions.failedGenerationRetainsScene);
    const restore=await frame.evaluate(()=>{
      const root=document.getElementById("ecology-walk"),a=root._authoring;
      const value={version:2,buildId:window.ECOLOGY_BUILD_ID,seed:a.world.seed,engine:a.engine.exportState(),
        visited:["r0","r1"],focus:"r3",motion:false};
      window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:value}}}}));
      const good=root._inspect();value.buildId="stale-build";value.focus="r6";
      window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:value}}}}));
      return {good:good.focus==="r3"&&!good.motion,staleIgnored:root._inspect().focus==="r3"};
    });
    assert(restore.good&&restore.staleIgnored);report.interactions.restore=restore;
    await frame.evaluate(()=>document.getElementById("ecology-walk")._authoring.install(271));
    await frame.locator("#ew-auto").click();
    report.walk=await frame.evaluate(()=>{
      const a=document.getElementById("ecology-walk")._authoring;let steps=0;
      while(steps<16000&&!a.engine.completed){const state=a.engine.snapshot();a.engine.step(1/60,a.automated(state,1/60));steps++;}
      return {steps,completed:a.engine.completed,recoveries:a.engine.recoveries||0,flags:a.engine.flags,
        stateBytes:new TextEncoder().encode(JSON.stringify(a.engine.exportState())).length};
    });
    assert(report.walk.completed&&report.walk.stateBytes<14000,"Browser input route failed.");
    await capture("delivered");
    const old=(await inspect()).seed;await frame.locator("#ew-new").click();
    assert((await inspect()).seed===(old+1009)>>>0);report.interactions.regenerate=true;
    assert(!report.errors.length,"Runtime errors "+report.errors.join(", "));
    const file=path.join(base,"ecology-walk-repaired.html");
    report.fragment={path:file,sha256:crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex")};
    report.passed=true;
  } catch(error){report.failure=String(error.stack||error);report.passed=false;process.exitCode=1;}
  finally {
    fs.writeFileSync(path.join(out,"report.json"),JSON.stringify(report,null,2)+"\n",{flag:"wx"});
    await browser.close();console.log(JSON.stringify({passed:report.passed,buildId:report.buildId,
      errors:report.errors,failure:report.failure,layouts:report.layouts.length,seams:report.seams.length,
      motionPixels:report.motionPixels,frozenPixels:report.frozenPixels,walk:report.walk,report:path.join(out,"report.json")},null,2));
  }
})();
