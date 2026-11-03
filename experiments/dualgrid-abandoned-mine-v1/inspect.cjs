const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,fragment=process.argv[2],wrapper=process.argv[3];
const folder=path.join(root,"records",Date.now()+"-browser-inspection");
fs.mkdirSync(folder,{recursive:true});
const sha=file=>crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const report={date:"2026-11-01",scope:"New mine layout, visible structure, local input and state isolation only; no Unity or complete shaft/route acceptance.",
  layouts:[],focus:[],errors:[],sources:{standalone:sha(path.join(root,"index.html")),
    fragment:fragment?sha(fragment):null,layout:sha(path.join(root,"layout.js")),props:sha(path.join(root,"props.js"))}};
let browser;
function check(value,message) {if (!value) throw Error(message);}
(async()=>{
  browser=await chromium.launch({
    executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:1000,height:1050},colorScheme:"light",reducedMotion:"reduce"});
  page.on("pageerror",e=>report.errors.push(e.message));
  await page.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await page.waitForFunction(()=>document.getElementById("dg-abandoned-mine")._inspect);
  const state=()=>page.evaluate(()=>document.getElementById("dg-abandoned-mine")._inspect());
  report.initial=await state();check(report.initial.size.join(",")==="232,128","Unexpected mine dimensions");
  check(await page.locator("#cw-site option").count()===9,"Missing mine design point");
  await page.evaluate(()=>localStorage.setItem("dark-nights-dualgrid-cave-whitebox-v1","old-cave-state-sentinel"));
  await page.locator("#dg-abandoned-mine").screenshot({path:path.join(folder,"overview.png")});
  for (const id of ["stope","hoist","sealed","deep","service"]) {
    await page.locator("#cw-site").selectOption(id);
    const current=await state();
    check(current.mode==="inspect"&&current.site===id,"Focus did not update");
    report.focus.push({id,camera:current.camera});
    if (["stope","hoist","deep"].includes(id))
      await page.locator("#dg-abandoned-mine").screenshot({path:path.join(folder,id+".png")});
  }
  await page.locator("#cw-walk").click();await page.waitForTimeout(120);
  const start=await state();
  await page.keyboard.down("KeyD");await page.waitForTimeout(1400);await page.keyboard.up("KeyD");
  const moved=await state();check(moved.player.x>start.player.x+6&&!moved.insideRock,"Flat drift movement failed");
  await page.keyboard.press("Space");await page.waitForTimeout(120);
  const jumped=await state();check(jumped.player.y<moved.player.y-.2&&!jumped.insideRock,"Jump failed");
  await page.waitForTimeout(900);
  const target=await page.evaluate(()=>document.getElementById("dg-abandoned-mine")._digTarget());
  const box=await page.locator("#cw-scene").boundingBox();
  await page.mouse.click(box.x+target.x,box.y+target.y);
  await page.waitForFunction(()=>document.getElementById("dg-abandoned-mine")._inspect().dug>0);
  await page.waitForTimeout(350);
  const dug=await state();
  await page.reload();await page.waitForFunction(()=>document.getElementById("dg-abandoned-mine")._inspect);
  const restored=await state();check(restored.dug===dug.dug,"Mine cuts did not restore");
  const storage=await page.evaluate(()=>({
    old:localStorage.getItem("dark-nights-dualgrid-cave-whitebox-v1"),
    mine:JSON.parse(localStorage.getItem("dark-nights-dualgrid-abandoned-mine-v1"))
  }));
  check(storage.old==="old-cave-state-sentinel"&&storage.mine.kind==="dg-mine-v1","State namespaces collided");
  report.interactions={start,moved,jumped,dug,restored,stateIsolation:true};
  await page.locator("#cw-grid").check();
  await page.locator("#dg-abandoned-mine").screenshot({path:path.join(folder,"walk-grid.png")});
  await page.locator("#cw-reset").click();
  for (const [width,theme] of [[1000,"light"],[736,"dark"],[360,"light"],[320,"dark"]]) {
    await page.setViewportSize({width,height:1050});await page.emulateMedia({colorScheme:theme});
    await page.waitForTimeout(70);
    const layout=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
    check(layout.scroll<=layout.width,"Toolbar overflow");
    await page.locator("#dg-abandoned-mine").screenshot({path:path.join(folder,width+"-"+theme+".png")});
    report.layouts.push({...layout,theme});
  }
  if (wrapper) {
    await page.goto("file:///"+path.resolve(wrapper).replaceAll("\\","/"));
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("dg-abandoned-mine")._inspect);
    await frame.locator("#cw-site").selectOption("stope");
    report.inline=await frame.evaluate(()=>({
      width:innerWidth,scroll:document.documentElement.scrollWidth,
      state:document.getElementById("dg-abandoned-mine")._inspect()
    }));
    check(report.inline.width>=report.inline.scroll&&report.inline.state.site==="stope","Inline view failed");
    await frame.locator("#dg-abandoned-mine").screenshot({path:path.join(folder,"inline-320.png")});
  }
  check(report.errors.length===0,"Browser script error");
  report.ok=true;
})().catch(e=>{
  report.ok=false;report.failure=e.stack;process.exitCode=1;
}).finally(async()=>{
  fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  console.log(JSON.stringify({ok:report.ok,folder,dug:report.interactions?.dug.dug,
    stateIsolation:report.interactions?.stateIsolation,layouts:report.layouts,errors:report.errors,failure:report.failure}));
  if (browser) await browser.close();
});
