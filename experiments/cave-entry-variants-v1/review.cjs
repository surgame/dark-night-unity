const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const {pathToFileURL} = require("node:url");
const {chromium} = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const {variants,sample,tangent} = require("./variants.cjs");
const destination = process.argv[2], wrapper = process.argv[3];
if (!destination || !wrapper) throw new Error("Provide record directory and preview wrapper.");
const folder = path.resolve(destination,`display-${Date.now()}`);
fs.mkdirSync(folder,{recursive:true});
const identity=file=>({path:path.resolve(file),sha256:crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex")});
const result = {
  scope:"Diagram display and finite motion checks; not game, collision, movement or production WFC validation.",
  createdLocalDate:"2026-11-03", source:[__filename,path.join(__dirname,"variants.cjs")].map(identity),
  models:[], layouts:[], errors:[], screenshots:[], interactions:{}
};
function check(value,message) { if(!value) throw new Error(message); }
async function run() {
  for(const variant of variants) {
    const seamF=variant.floor.findIndex(p=>p[0]===6.4), seamC=variant.ceiling.findIndex(p=>p[0]===6.4);
    check(seamF>0 && seamC>0,"Missing seam: "+variant.id);
    check(tangent(variant.floor,seamF)===0 && tangent(variant.ceiling,seamC)===0,"Seam tangent: "+variant.id);
    const remaining=variant.floor.filter(p=>p[0]>=6.4);
    check(remaining.length>=8,"Second segment is empty: "+variant.id);
    result.models.push({id:variant.id,port:variant.port,seamFloorTangent:0,seamRoofTangent:0,secondSegmentSamples:remaining.length,surfaceFloor:sample(variant.floor,.4),endFloors:variant.ends});
  }
  const browser = await chromium.launch({headless:true,executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try {
    for (const config of [{width:736,theme:"light"},{width:320,theme:"dark"}]) {
      const page=await browser.newPage({viewport:{width:config.width,height:1100},colorScheme:config.theme});
      page.on("pageerror",error=>result.errors.push(error.message));
      await page.goto(pathToFileURL(path.resolve(wrapper)).href);
      await page.evaluate(()=>{document.body.style.padding="0";document.querySelector("iframe").style.height="1100px";});
      const frame=page.frames().find(f=>f.parentFrame());
      await frame.waitForSelector("#ev-map [data-floor-edge]");
      await frame.evaluate(()=>{window.__writes=[];window.openai.setWidgetState=snapshot=>{window.__writes.push(snapshot);return Promise.resolve();};});
      const initial=await frame.evaluate(()=>JSON.parse(document.getElementById("cave-entry-variants-v1").dataset.state));
      check(initial.view===(config.width<520?"entry":"all"),"Initial view");
      for (const variant of variants) {
        await frame.selectOption("#ev-variant",variant.id);
        for (const view of ["all","entry","join","next"]) {
          await frame.selectOption("#ev-view",view);
          const layout=await frame.evaluate(()=>{
            const root=document.getElementById("cave-entry-variants-v1"),svg=document.getElementById("ev-map");
            const rootBox=root.getBoundingClientRect(), box=svg.getBoundingClientRect();
            const labels=[...svg.querySelectorAll("text")].map(el=>({text:el.textContent,box:el.getBoundingClientRect().toJSON()}));
            const clipped=labels.filter(({box:b})=>b.left<box.left-1||b.right>box.right+1);
            const overlap=[];
            for(let i=0;i<labels.length;i++)for(let j=i+1;j<labels.length;j++){
              const a=labels[i].box,b=labels[j].box;
              if(Math.min(a.right,b.right)-Math.max(a.left,b.left)>1&&Math.min(a.bottom,b.bottom)-Math.max(a.top,b.top)>1)overlap.push([labels[i].text,labels[j].text]);
            }
            const hero=[...svg.querySelectorAll("[data-hero]")].map(el=>el.getBoundingClientRect().height);
            const worldPoint=(x,y)=>new DOMPoint(x,y);
            const roof=svg.querySelector("[data-ceiling-solid]"),floor=svg.querySelector("[data-floor-solid]");
            return {state:JSON.parse(root.dataset.state),rootWidth:rootBox.width,scale:Number(svg.dataset.scale),window:JSON.parse(svg.dataset.worldWindow),heroHeights:hero,overflow:document.documentElement.scrollWidth>document.documentElement.clientWidth,clipped,overlap,skyOpen:!roof.isPointInFill(worldPoint(.4,.1)),groundAtLeft:floor.isPointInFill(worldPoint(.4,3)),realHero:svg.querySelector("[data-hero]").getAttribute("href").startsWith("data:image/png;base64,"),finite:/NaN|Infinity/.test(svg.innerHTML)===false};
          });
          check(!layout.overflow && !layout.clipped.length && !layout.overlap.length && layout.finite,`Layout issue ${variant.id}/${view}/${config.width}`);
          check(layout.skyOpen&&layout.groundAtLeft&&layout.realHero,"Missing surface or role");
          check(layout.heroHeights.every(height=>Math.abs(height-.48*layout.scale)<.02),"Hero scale");
          result.layouts.push({width:config.width,theme:config.theme,...layout});
          if(config.width===736&&view==="all"||config.width===320&&((variant.id==="steps"&&view==="entry")||(variant.id==="stream"&&view==="next"))){
            const file=path.join(folder,`${config.width}-${config.theme}-${variant.id}-${view}.png`);
            await frame.locator("#cave-entry-variants-v1").screenshot({path:file});
            result.screenshots.push(identity(file));
          }
        }
      }
      await frame.selectOption("#ev-variant","stream");
      await frame.selectOption("#ev-view","next");
      await frame.click("#ev-preview");
      await page.waitForTimeout(100);
      const motionA=await frame.evaluate(()=>[...document.querySelectorAll("[data-animation]")].map(el=>[el.dataset.animation,el.getAttribute("style"),el.getAttribute("transform")]));
      await page.waitForTimeout(180);
      const motionB=await frame.evaluate(()=>[...document.querySelectorAll("[data-animation]")].map(el=>[el.dataset.animation,el.getAttribute("style"),el.getAttribute("transform")]));
      check(JSON.stringify(motionA)!==JSON.stringify(motionB),"Motion did not change");
      await frame.click("#ev-preview");
      const stopped=await frame.evaluate(()=>[...document.querySelectorAll("[data-animation]")].every(el=>!el.getAttribute("style")&&(el.dataset.animation!=="flow"||el.getAttribute("transform")==="translate(0 0)")));
      check(stopped,"Motion not stopped");
      await frame.uncheck("#ev-ecology");
      check(await frame.locator("[data-ecology]").count()===0,"Ecology toggle");
      check(await frame.locator("#ev-preview").isDisabled(),"Motion disabled without ecology");
      await frame.uncheck("#ev-bounds");
      check(await frame.locator(".ev-boundary").count()===0,"Boundary toggle");
      const writes=await frame.evaluate(()=>window.__writes);
      check(writes.length>0&&writes.at(-1).modelContent.ecology===false&&writes.at(-1).modelContent.bounds===false,"State writes");
      const beforeRestore=writes.length;
      await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{design:"cave-entry-variants-v1",variant:"lowpass",view:"join",ecology:true,bounds:true}}}})));
      check(await frame.locator("#ev-variant").inputValue()==="lowpass","Restore variant");
      check((await frame.evaluate(()=>window.__writes.length))===beforeRestore,"Restore wrote state");
      await page.emulateMedia({reducedMotion:"reduce"});
      await frame.click("#ev-preview");
      check((await frame.locator("#ev-motion-status").textContent()).includes("减少动态"),"Reduced motion");
      result.interactions[config.width]={finiteMotionChanged:true,manualStop:true,ecologyToggle:true,boundsToggle:true,stateWrites:true,restoreWithoutWrite:true,reducedMotion:true};
      await page.close();
    }
    check(result.errors.length===0,"Page error");
    result.completed=true;
  } finally {
    await browser.close();
  }
}
run().catch(error=>{result.completed=false;result.failure=error.stack;process.exitCode=1;}).finally(()=>{
  fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(result,null,2)+"\n");
  console.log(JSON.stringify({completed:result.completed,models:result.models.length,layouts:result.layouts.length,screenshots:result.screenshots.length,errors:result.errors,failure:result.failure,report:path.join(folder,"report.json")}));
});
