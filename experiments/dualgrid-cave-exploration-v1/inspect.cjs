const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname;
const fragment=process.argv[2];
const wrapper=process.argv[3];
const folder=path.join(root,"records",Date.now()+"-browser-inspection");
fs.mkdirSync(folder,{recursive:true});
const hash=file=>crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const report={date:"2026-11-01",scope:"Bounded browser artwork/interaction inspection; no Unity, performance or full route acceptance.",
  layouts:[],errors:[],source:{standalone:hash(path.join(root,"index.html")),fragment:fragment?hash(fragment):null}};
let browser;
function requireCondition(condition,message) {if (!condition) throw Error(message);}
(async()=>{
  browser=await chromium.launch({
    executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"
  });
  const context=await browser.newContext({viewport:{width:1000,height:1000},colorScheme:"light",reducedMotion:"reduce"});
  const page=await context.newPage();
  page.on("pageerror",e=>report.errors.push(e.message));
  await page.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await page.waitForFunction(()=>document.getElementById("dg-cave-whitebox")._inspect);
  const inspect=()=>page.evaluate(()=>document.getElementById("dg-cave-whitebox")._inspect());
  report.initial=await inspect();
  report.geometry=await page.evaluate(()=>{
    const C=CaveWhitebox,canvas=document.createElement("canvas");
    canvas.width=canvas.height=128;
    const ctx=canvas.getContext("2d"),mismatches=[];let samples=0;
    for (let m=0;m<16;m++) {
      ctx.clearRect(0,0,128,128);C.paintTile(ctx,m,128,{rock:"white",edge:"transparent"});
      const data=ctx.getImageData(0,0,128,128).data;
      for (let y=5;y<128;y+=9) for (let x=5;x<128;x+=9) {
        const a=data[(y*128+x)*4+3];if (a>0&&a<255) continue;
        samples++;
        if ((a>127)!==C.inside(m,(x+.5)/128,(y+.5)/128)) mismatches.push({m,x,y});
      }
    }
    return {templates:C.templates.length,samples,mismatches};
  });
  requireCondition(report.geometry.mismatches.length===0,"Rendered masks differ from collision shapes");
  await page.locator("#cw-walk").click();
  await page.waitForTimeout(150);
  const start=await inspect();
  await page.keyboard.down("KeyD");await page.waitForTimeout(1700);await page.keyboard.up("KeyD");
  await page.waitForTimeout(120);
  const moved=await inspect();
  requireCondition(moved.player.x>start.player.x+8&&!moved.insideRock,"Walking did not advance safely");
  await page.keyboard.press("Space");await page.waitForTimeout(120);
  const jumped=await inspect();
  requireCondition(jumped.player.y<moved.player.y-.2&&!jumped.insideRock,"Jump did not leave the floor");
  await page.waitForTimeout(1000);
  const digBefore=await inspect(),target=await page.evaluate(()=>document.getElementById("dg-cave-whitebox")._digTarget());
  const bounds=await page.locator("#cw-scene").boundingBox();
  await page.mouse.click(bounds.x+target.x,bounds.y+target.y);
  await page.waitForFunction(n=>document.getElementById("dg-cave-whitebox")._inspect().dug>n,digBefore.dug);
  await page.waitForTimeout(350);
  const dug=await inspect();
  requireCondition(!dug.insideRock,"Player overlaps rock after digging");
  await page.locator("#dg-cave-whitebox").screenshot({path:path.join(folder,"walk-dug.png")});
  await page.reload();await page.waitForFunction(()=>document.getElementById("dg-cave-whitebox")._inspect);
  const restored=await inspect();
  requireCondition(restored.dug===dug.dug,"Cut history did not restore");
  report.interactions={start,moved,jumped,dug,restored};
  await page.locator("#cw-site").selectOption("cleft");
  await page.locator("#cw-grid").check();
  await page.locator("#dg-cave-whitebox").screenshot({path:path.join(folder,"rounding-grid.png")});
  await page.locator("#cw-grid").uncheck();
  await page.locator("#cw-reset").click();
  for (const [width,theme] of [[1000,"light"],[1000,"dark"],[360,"light"],[320,"dark"]]) {
    await page.setViewportSize({width,height:1000});await page.emulateMedia({colorScheme:theme});
    await page.waitForTimeout(70);
    const layout=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,
      state:document.getElementById("dg-cave-whitebox")._inspect()}));
    requireCondition(layout.scroll<=layout.width,"Horizontal overflow");
    await page.locator("#dg-cave-whitebox").screenshot({path:path.join(folder,width+"-"+theme+".png")});
    report.layouts.push({width,theme,overflow:false});
  }
  if (wrapper) {
    await page.goto("file:///"+path.resolve(wrapper).replaceAll("\\","/"));
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("dg-cave-whitebox")._inspect);
    await frame.locator("#cw-site").selectOption("hall");
    report.inline=await frame.evaluate(()=>document.getElementById("dg-cave-whitebox")._inspect());
    requireCondition(report.inline.mode==="inspect"&&report.inline.site==="hall","Inline controls failed");
    report.inlineLayout=await frame.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
    requireCondition(report.inlineLayout.scroll<=report.inlineLayout.width,"Inline toolbar overflow");
    await frame.locator("#dg-cave-whitebox").screenshot({path:path.join(folder,"inline-320.png")});
  }
  const touch=await browser.newPage({viewport:{width:360,height:900},isMobile:true,hasTouch:true});
  touch.on("pageerror",e=>report.errors.push(e.message));
  await touch.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await touch.waitForFunction(()=>document.getElementById("dg-cave-whitebox")._inspect);
  await touch.locator("#cw-walk").tap();
  await touch.waitForTimeout(120);
  await touch.locator("#cw-touch-mine").tap();
  await touch.waitForFunction(()=>document.getElementById("dg-cave-whitebox")._inspect().dug>0);
  report.touch=await touch.evaluate(()=>document.getElementById("dg-cave-whitebox")._inspect());
  await touch.close();
  requireCondition(report.errors.length===0,"Browser errors were recorded");
  report.ok=true;
})().catch(e=>{
  report.ok=false;report.failure=e.stack;process.exitCode=1;
}).finally(async()=>{
  fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  console.log(JSON.stringify({ok:report.ok,folder,geometry:report.geometry,
    dug:report.interactions?.dug.dug,layouts:report.layouts,errors:report.errors,failure:report.failure}));
  if (browser) await browser.close();
});
