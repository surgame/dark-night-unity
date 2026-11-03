const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,fragment=process.argv[2],wrapper=process.argv[3];
const folder=path.join(root,"records",Date.now()+"-browser-review");fs.mkdirSync(folder,{recursive:true});
const sha=p=>crypto.createHash("sha256").update(fs.readFileSync(p)).digest("hex");
const report={date:"2026-11-01",
  scope:"Six visual methods, fixed camera, local movement, controls and state; no full-route, Unity or performance acceptance.",
  sources:{standalone:sha(path.join(root,"index.html")),fragment:fragment?sha(fragment):null,
    scripts:["geometry.js","modules.js","solver.js","renderer.js","app.js"].map(file=>({file,sha256:sha(path.join(root,file))}))},
  methods:[],layouts:[],errors:[]};
const check=(v,m)=>{if(!v) throw Error(m);};
let browser;
(async()=>{
  browser=await chromium.launch({
    executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:1000,height:1150},colorScheme:"light",reducedMotion:"reduce"});
  page.on("pageerror",e=>report.errors.push(e.message));
  await page.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await page.waitForFunction(()=>document.getElementById("macro-cave-compare")._inspect);
  const get=()=>page.evaluate(()=>document.getElementById("macro-cave-compare")._inspect());
  const shot=name=>page.locator("#macro-cave-compare").screenshot({path:path.join(folder,name+".png")});
  report.initial=await get();
  check(await page.evaluate(()=>localStorage.getItem("dark-nights-macro-cave-comparison-v1"))===null,"Saved on initial load");
  await page.evaluate(()=>{
    for(const name of ["cave-whitebox","abandoned-mine","stratified-cave"])
      localStorage.setItem("dark-nights-dualgrid-"+name+"-v1","prior-"+name);
  });
  const camera=JSON.stringify(report.initial.camera);
  for(const method of ["dual","expanded","contour","blocks","bridges","wfc"]) {
    await page.locator('[data-method="'+method+'"]').click();
    const current=await get();
    check(current.method===method&&JSON.stringify(current.camera)===camera,"Method switched camera");
    check(!current.insideRock,"Method placed reference player in solid");
    report.methods.push(current);await shot(method);
  }
  await page.locator("#mc-focus").selectOption("all");await shot("wfc-overview");
  await page.locator("#mc-library-open summary").click();await shot("wfc-library");
  const library=await page.locator("#mc-library").evaluate(e=>({width:e.width,height:e.height}));
  check(library.width>0&&library.height>0,"Module library did not paint");
  await page.locator("#mc-library-open summary").click();
  await page.locator("#mc-focus").selectOption("elbow");
  await page.locator('[data-method="bridges"]').click();
  await page.locator("#mc-seams").uncheck();await shot("bridges-clean");
  await page.locator("#mc-reference").check();await shot("bridges-reference");
  const beforePan=await get(),box=await page.locator("#mc-scene").boundingBox();
  await page.mouse.move(box.x+box.width*.5,box.y+box.height*.5);
  await page.mouse.down({button:"right"});await page.mouse.move(box.x+box.width*.5+40,box.y+box.height*.5+15);
  await page.mouse.up({button:"right"});const afterPan=await get();
  check(afterPan.camera.x!==beforePan.camera.x,"Drag did not move camera");
  await page.mouse.wheel(0,-180);await page.waitForTimeout(70);
  const afterZoom=await get();check(afterZoom.camera.scale>afterPan.camera.scale,"Wheel did not zoom");
  await page.locator("#mc-walk").click();await page.waitForTimeout(220);
  const start=await get();
  await page.keyboard.down("KeyD");await page.waitForTimeout(1300);await page.keyboard.up("KeyD");
  const moved=await get();check(moved.player.x>start.player.x+5&&!moved.insideRock,"Local move failed");
  await page.keyboard.press("Space");await page.waitForTimeout(130);
  const jumped=await get();check(jumped.player.y<moved.player.y-.3&&!jumped.insideRock,"Jump failed");
  await page.waitForTimeout(950);await page.locator("#mc-walk").click();
  await page.locator("#mc-next").click();const changed=await get();
  check(changed.seed===372,"Seed did not change");
  await page.locator('[data-method="wfc"]').click();const wfc=await get();
  check(wfc.wfc.observations>0&&wfc.wfc.removed>0,"WFC did not select/propagate");
  await page.reload();await page.waitForFunction(()=>document.getElementById("macro-cave-compare")._inspect);
  const restored=await get();
  check(restored.seed===372&&restored.method==="wfc"&&restored.reference&&!restored.seams,"Preferences did not restore");
  report.interactions={beforePan,afterPan,afterZoom,start,moved,jumped,changed,restored};
  report.priorKeys=await page.evaluate(()=>["cave-whitebox","abandoned-mine","stratified-cave"].map(name=>
    localStorage.getItem("dark-nights-dualgrid-"+name+"-v1")==="prior-"+name));
  check(report.priorKeys.every(Boolean),"A prior state key changed");
  const stored=await page.evaluate(()=>localStorage.getItem("dark-nights-macro-cave-comparison-v1"));
  check(Buffer.byteLength(stored)<16384,"Saved state exceeds host limit");
  report.stateBytes=Buffer.byteLength(stored);
  for(const [width,theme] of [[1000,"light"],[736,"dark"],[360,"light"],[320,"dark"]]) {
    await page.setViewportSize({width,height:1150});await page.emulateMedia({colorScheme:theme});
    await page.waitForTimeout(70);
    const size=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
    check(size.scroll<=size.width,"Controls overflow");
    report.layouts.push({...size,theme});await shot(width+"-"+theme);
  }
  await page.locator("#mc-walk").click();await page.waitForTimeout(140);
  const touchStart=await get(),hold=page.locator('[data-hold="right"]');
  const holdBox=await hold.boundingBox();
  await page.mouse.move(holdBox.x+holdBox.width/2,holdBox.y+holdBox.height/2);await page.mouse.down();
  await page.waitForTimeout(900);
  await page.mouse.up();
  const touchEnd=await get();check(touchEnd.player.x>touchStart.player.x+3,"Touch movement failed");
  report.onScreenHold={start:touchStart.player,end:touchEnd.player};
  if(wrapper) {
    await page.goto("file:///"+path.resolve(wrapper).replaceAll("\\","/"));
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("macro-cave-compare")._inspect);
    await frame.locator('[data-method="blocks"]').click();
    await frame.locator("#mc-focus").selectOption("all");
    report.inline=await frame.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,
      state:document.getElementById("macro-cave-compare")._inspect()}));
    check(report.inline.width>=report.inline.scroll&&report.inline.state.method==="blocks","Inline view failed");
    await frame.locator("#macro-cave-compare").screenshot({path:path.join(folder,"inline-320.png")});
  }
  check(!report.errors.length,"Browser runtime error");report.ok=true;
})().catch(e=>{report.ok=false;report.failure=e.stack;process.exitCode=1;}).finally(async()=>{
  fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  console.log(JSON.stringify({ok:report.ok,folder,methods:report.methods.length,
    layouts:report.layouts,errors:report.errors,failure:report.failure}));
  if(browser) await browser.close();
});
