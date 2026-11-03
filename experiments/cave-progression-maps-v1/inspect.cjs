const fs=require("fs"),path=require("path"),vm=require("vm"),{pathToFileURL}=require("url");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const folder=path.join(__dirname,"records",Date.now()+"-display-review");
fs.mkdirSync(folder,{recursive:true});
const report={scope:"Authored graph and display geometry, no actor physics, gameplay, actual travel time or Unity test.",errors:[],models:[],layouts:[],selections:[]};
const sandbox={};
vm.createContext(sandbox);
for(const f of ["data.js","geometry.js"])vm.runInContext(fs.readFileSync(path.join(__dirname,f),"utf8"),sandbox);
function pointSegment(p,a,b){
  const dx=b[0]-a[0],dy=b[1]-a[1],l=dx*dx+dy*dy;
  const t=l?Math.max(0,Math.min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/l)):0;
  return Math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dy*t);
}
function segmentGap(a,b,c,d){
  const cross=(u,v,w)=>(v[0]-u[0])*(w[1]-u[1])-(v[1]-u[1])*(w[0]-u[0]);
  const ab1=cross(a,b,c),ab2=cross(a,b,d),cd1=cross(c,d,a),cd2=cross(c,d,b);
  if(ab1*ab2<0&&cd1*cd2<0)return 0;
  return Math.min(pointSegment(a,c,d),pointSegment(b,c,d),pointSegment(c,a,b),pointSegment(d,a,b));
}
for(const m of sandbox.CaveProgressionMaps.maps){
  const G=sandbox.CaveProgressionGeometry;
  const visited=new Set();
  function walk(id){if(visited.has(id))throw new Error("Graph cycle: "+id);visited.add(id);m.byId(id).children.forEach(walk);}
  walk(m.rooms[0].id);
  const overlaps=[];
  const parts=G.air(m);
  const owners=new Map(m.rooms.map(r=>[r.id,new Set([r.id,...r.children,r.parent].filter(Boolean))]));
  // Only unintended room/room and link/unrelated-room drawing contacts are checked.
  for(let y=1;y<104;y+=.25){
    for(let x=1;x<139;x+=.25){
      const p=[x,y],rooms=m.rooms.filter(r=>G.inside(p,{polygon:r.polygon}));
      for(let i=1;i<rooms.length;i++){
        if(!owners.get(rooms[0].id).has(rooms[i].id))overlaps.push({kind:"rooms",p,rooms:rooms.map(r=>r.id)});
      }
      if(!rooms.length)continue;
      for(const part of parts){
        if(!part.link||!G.inside(p,part))continue;
        const l=m.links.find(l=>l.to===part.link);
        if(rooms.some(r=>r.id!==l.from&&r.id!==l.to))overlaps.push({kind:"link-room",p,link:l.to,rooms:rooms.map(r=>r.id)});
      }
    }
  }
  const crossedLinks=[];
  for(let i=0;i<m.links.length;i++){
    for(let j=i+1;j<m.links.length;j++){
      const a=m.links[i],b=m.links[j];
      if([a.from,a.to].some(id=>id===b.from||id===b.to))continue;
      for(let s=1;s<a.points.length;s++){
        for(let t=1;t<b.points.length;t++){
          if(segmentGap(a.points[s-1],a.points[s],b.points[t-1],b.points[t])<(a.width+b.width)/2){
            crossedLinks.push({a:a.to,b:b.to});
          }
        }
      }
    }
  }
  report.models.push({id:m.id,stats:m.stats,connected:visited.size===m.rooms.length,tree:m.links.length===m.rooms.length-1,unintendedDrawingContacts:overlaps.slice(0,40),contactSamples:overlaps.length,crossedLinks});
}
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    const page=await browser.newPage({viewport:{width:736,height:1500}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname,"records/preview.html")).href);
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("cave-progression-maps")?.dataset.ready==="true");
    const inspect=()=>frame.evaluate(()=>document.getElementById("cave-progression-maps")._inspect());
    const settle=()=>frame.waitForFunction(()=>!document.getElementById("cave-progression-maps")._inspect().moving);
    report.initial=await inspect();
    await frame.locator("#cpm-route").check();
    await frame.locator("#cpm-groups").check();
    report.checks=await inspect();
    await frame.locator("#cpm-groups").uncheck();
    report.routeSamples=await frame.evaluate(()=>{
      const C=globalThis.CaveProgressionMaps;
      return C.maps.map(m=>{
        const route=document.querySelector(`[data-route="${m.id}"]`),length=route.getTotalLength();
        const cavities=[...document.querySelector(`[data-air="${m.id}"]`).children],outside=[];
        for(let i=0;i<=800;i++){
          const p=route.getPointAtLength(length*i/800);
          if(!cavities.some(c=>c.isPointInFill(p)))outside.push({x:p.x,y:p.y});
        }
        return {id:m.id,length,expected:m.routeLength,samples:801,outside};
      });
    });
    await frame.locator("#cpm-route").uncheck();
    for(const width of [736,360,320]){
      await page.setViewportSize({width,height:1500});
      await frame.waitForFunction(w=>innerWidth<=w&&innerWidth>=w-48,width);
      await page.emulateMedia({colorScheme:width===320?"dark":"light"});
      for(const map of ["advanced","expert"]){
        const first=map==="advanced"?"A03":"B07",last=map==="advanced"?"A09":"B27";
        for(const room of ["all",first,last]){
          await frame.locator(`#cpm-${map}-room`).selectOption(room);
          await settle();
          report.layouts.push(await frame.evaluate(map=>{
            const host=document.getElementById(`cpm-${map}-map`);
            const texts=[...host.querySelectorAll("text")].map(n=>{
              const b=n.getBoundingClientRect();
              return {text:n.textContent,left:b.left,right:b.right,top:b.top,bottom:b.bottom};
            });
            return {map,width:innerWidth,scroll:document.documentElement.scrollWidth,
              state:document.getElementById("cave-progression-maps")._inspect().state,
              invalid:/NaN|undefined|Infinity/.test(host.innerHTML),labels:texts};
          },map));
          await frame.locator(`[data-panel="${map}"]`).screenshot({path:path.join(folder,`${width}-${map}-${room}.png`)});
        }
        await frame.locator(`#cpm-${map}-room`).selectOption("all");
        await settle();
      }
    }
    await page.setViewportSize({width:736,height:1500});
    for(const m of sandbox.CaveProgressionMaps.maps){
      for(const r of m.rooms){
        await frame.locator(`#cpm-${m.id}-room`).selectOption(r.id);
        await settle();
        report.selections.push({map:m.id,room:r.id,detail:await frame.locator(`#cpm-${m.id}-detail`).textContent()});
      }
    }
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"cave-progression-maps-v1",advanced:"A15",expert:"B19",route:true,groups:true}}}}})));
    report.restored=await inspect();
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"compact-natural-mine-v1",goal:"B",view:"all"}}}}})));
    report.oldStateIgnored=await inspect();
    report.ok=!report.errors.length&&report.models.every(m=>m.tree&&m.connected&&!m.contactSamples&&!m.crossedLinks.length)&&
      report.routeSamples.every(r=>!r.outside.length&&Math.abs(r.length-r.expected)<.05)&&
      report.layouts.every(l=>!l.invalid&&l.scroll<=l.width+1&&l.labels.every(b=>b.left>=-1&&b.right<=l.width+1))&&
      report.restored.state.expert==="B19"&&report.oldStateIgnored.state.expert==="B19"&&report.selections.length===47;
  }finally{
    await browser.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  }
  console.log(JSON.stringify({ok:report.ok,folder,models:report.models.map(m=>({id:m.id,stats:m.stats,contacts:m.contactSamples})),layouts:report.layouts.length,selections:report.selections.length,errors:report.errors}));
  if(!report.ok)process.exitCode=1;
})().catch(e=>{
  report.errors.push(e.stack);fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  console.error(e.stack);process.exitCode=1;
});
