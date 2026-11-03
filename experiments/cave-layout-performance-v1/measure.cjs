const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,full=process.argv.includes("--measure"),folder=path.join(root,"records",Date.now()+"-measurement");
fs.mkdirSync(folder,{recursive:true});
const sha=p=>crypto.createHash("sha256").update(fs.readFileSync(p)).digest("hex");
const sources=["../dualgrid-cave-exploration-v1/dual-grid.js","plan.js","solver.js","field.js","mesh.js","gpu.js","benchmark.js"];
const sourceHashes=sources.map(file=>({file,sha256:sha(path.join(root,file))}));
let browser;
(async()=>{
  browser=await chromium.launch({
    executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe",
    args:["--use-angle=d3d11","--enable-gpu"]});
  const page=await browser.newPage({viewport:{width:1000,height:1250},colorScheme:"light"}),errors=[];
  page.on("pageerror",e=>errors.push(e.message));
  await page.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await page.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect);
  const initial=await page.evaluate(()=>document.getElementById("cave-layout-performance")._inspect());
  fs.writeFileSync(path.join(folder,"initial.json"),JSON.stringify(initial,null,2)+"\n");
  if(!initial.ready||initial.error)throw Error(initial.error||"Benchmark unavailable");
  if(full&&initial.gpu.software)throw Error("Software rendering is not accepted as hardware reference");
  const report=await page.evaluate(async full=>document.getElementById("cave-layout-performance")
    ._benchmark({sizes:full?[128,512,2048]:[128],profiles:["standard","mixed"],rounds:full?16:2}),full);
  report.sourceHashes=sourceHashes;report.errors=errors;
  fs.writeFileSync(path.join(folder,"raw.json"),JSON.stringify(report,null,2)+"\n");
  if(!report.ok||errors.length)throw Error("Measured batch incomplete or browser errored");
  if(full) {
    const reference={...report};delete reference.records;
    reference.sourceRecord=path.relative(root,path.join(folder,"raw.json")).replaceAll("\\","/");
    fs.writeFileSync(path.join(root,"reference.json"),JSON.stringify(reference,null,2)+"\n");
  }
  await page.locator("#cave-layout-performance").screenshot({path:path.join(folder,"completed.png")});
  console.log(JSON.stringify({ok:true,full,folder,identity:report.identity,
    records:report.records.length,summary:report.summaries.map(r=>({profile:r.profile,size:r.size,method:r.method,
      cold:[r.stats.coldCPU.p50,r.stats.coldCPU.p95],
      draw64:[r.stats.chunkGroup.p50,r.stats.instanceGroup.p50],
      templates:r.resources.freeUsed+"/"+r.resources.freeLibrary,
      payloadMiB:[r.resources.chunkBytes/1048576,r.resources.sourceBytes/1048576]}))}));
})().catch(e=>{
  fs.writeFileSync(path.join(folder,"failure.txt"),e.stack+"\n");console.error(e.stack);process.exitCode=1;
}).finally(async()=>{if(browser)await browser.close();});
