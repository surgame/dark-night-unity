const fs=require("fs"),path=require("path"),assert=require("assert"),crypto=require("crypto");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,review=path.join(root,"review-v3");
const preview="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785";
const hash=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
let browser;
(async()=>{
  const manifest=JSON.parse(fs.readFileSync(path.join(root,"manifest.json")));
  const old=JSON.parse(fs.readFileSync(path.join(root,"revisions/coarse-mask-v2/manifest.json")));
  for(const source of manifest.sources)
    assert.strictEqual(source.sha256,old.sources.find(s=>s.key===source.key).sha256,"Source art must stay unchanged.");
  const samples=[
    [0,289,183,255],[0,348,177,255],[1,72,175,255],[1,548,224,255],
    [2,75,177,255],[3,127,145,255],[3,196,118,255],[3,382,95,255],
    [3,476,147,255],[3,543,175,255],[4,191,140,255],[5,319,230,255],
    [3,300,50,0],[3,342,52,0],[0,100,220,255]
  ];
  const before=[],after=[],contact=[];
  for(let panel=0;panel<6;panel++) {
    before.push(await sharp(path.join(root,`revisions/coarse-mask-v2/exports-v2/foreground/module-${panel+1}.png`)).raw().toBuffer());
    const filename=path.join(root,`exports-v3/foreground/module-${panel+1}.png`);
    after.push(await sharp(filename).raw().toBuffer());
    contact.push({input:await sharp(filename).flatten({background:"#2d4c5b"}).png().toBuffer(),
      left:panel%2*623,top:Math.floor(panel/2)*414});
  }
  for(const sample of samples) {
    const [panel,x,y,expected]=sample,index=(y*623+x)*4+3;
    assert.strictEqual(after[panel][index],expected,"Native-art anchor "+sample);
    assert.notStrictEqual(before[panel][index],expected,"Anchor must reproduce the old crop error.");
  }
  await sharp({create:{width:1246,height:1242,channels:4,background:"#2d4c5b"}})
    .composite(contact).png().toFile(path.join(review,"native-layer-contact.png"));
  browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:768,height:1000}}),errors=[];
  page.on("pageerror",error=>errors.push(error.message));
  await page.route(/^https?:/,route=>route.abort());
  await page.goto("file:///"+preview+"/modular-cave-roam-browser.html");
  const frame=page.frames()[1];
  await frame.waitForFunction(()=>document.getElementById("macro-cave-playable")?.dataset.ready==="true");
  const geometry=await frame.evaluate(()=>{
    const api=MacroCave,tiles=api.catalog(MACRO_PROFILES);
    let count=0,conflicts=0;
    for(let panel=0;panel<6;panel++)for(let mirror=0;mirror<2;mirror++)for(let x=0;x<623;x++) {
      const world={placements:[{id:panel*2+mirror,x:0,y:0}],tiles,clear:new Set()};
      const column=mirror?622-x:x,shape=MACRO_SILHOUETTES.panels[panel];
      for(const [y,expected] of [[shape.roof[column]-.5,true],[shape.roof[column]+.5,false],
        [shape.floor[column]-.5,false],[shape.floor[column]+.5,true]]) {
        if(api.terrainSolid(world,x+.5,y)!==expected)conflicts++;
        count++;
      }
    }
    return {count,conflicts};
  });
  assert.strictEqual(geometry.conflicts,0,"Mining/light queries must match normal and mirrored silhouettes.");
  await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{
    detail:{globals:{widgetState:{privateContent:{version:2,seed:7630,x:923,view:"walk",light:true,layer:"all"}}}}
  })));
  await frame.locator("#mc-scene").screenshot({path:path.join(review,"chamber-after.png")});
  const afterState=await frame.evaluate(()=>document.getElementById("macro-cave-playable")._inspect());
  assert.strictEqual(afterState.moduleSequence[1],"chamber-r");
  await frame.locator("#mc-light").uncheck();
  await frame.locator("#mc-scene").screenshot({path:path.join(review,"chamber-after-unlit.png")});
  await frame.locator("#mc-view").selectOption("atlas");
  await frame.locator("#mc-scene").screenshot({path:path.join(review,"atlas-after.png")});
  assert.deepStrictEqual(errors,[]);
  const result={ok:true,date:"2026-10-31",scope:"Independent layered browser prototype only.",
    checked:["unchanged original artwork","12 restored stalactite tips","2 removed backwall strips",
      "restored rubble","same chamber seed and X before/after","normal/mirrored rock query agreement"],
    anchors:samples.map(([panel,x,y,alpha])=>({panel,x,y,before:alpha?0:255,after:alpha})),geometry,
    before:"review-v3/chamber-before.png",after:"review-v3/chamber-after.png",
    seed:7630,x:923,afterPlayer:afterState.player,errors,
    identities:[...manifest.sources,...manifest.inputIdentity,...manifest.code],
    preview:{bytes:fs.statSync(manifest.preview).size,sha256:hash(fs.readFileSync(manifest.preview))}};
  fs.writeFileSync(path.join(root,"silhouette-verification.json"),JSON.stringify(result,null,2));
  await browser.close();
  console.log(JSON.stringify({ok:true,anchors:samples.length,geometry,errors}));
})().catch(async error=>{
  fs.writeFileSync(path.join(review,`silhouette-failure-${Date.now()}.json`),
    JSON.stringify({message:error.message,stack:error.stack},null,2));
  if(browser)await browser.close();
  console.error(error);process.exitCode=1;
});
