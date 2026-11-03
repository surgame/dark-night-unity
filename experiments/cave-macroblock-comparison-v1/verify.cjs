const fs=require("fs"),path=require("path"),vm=require("vm"),crypto=require("crypto");
const root=__dirname,folder=path.join(root,"records",Date.now()+"-model-review");
fs.mkdirSync(folder,{recursive:true});
const context=vm.createContext({performance,console});
const scripts=["../dualgrid-cave-exploration-v1/dual-grid.js",
  "../dualgrid-cave-exploration-v1/player.js","geometry.js","modules.js","solver.js"];
const sha=p=>crypto.createHash("sha256").update(fs.readFileSync(p)).digest("hex");
for(const file of scripts) vm.runInContext(fs.readFileSync(path.join(root,file),"utf8"),context,{filename:file});
const M=context.MacroCave,C=context.CaveWhitebox;
const methods=["dual","expanded","contour","blocks","bridges","wfc"];
const report={date:"2026-11-01",scope:"Finite model and interface checks for this independent whitebox only.",
  sources:scripts.map(file=>({file,sha256:sha(path.join(root,file))})),seeds:[],catalogPairs:0,minimumClearance:Infinity};
const check=(v,m)=>{if(!v) throw Error(m);};
const near=(a,b,epsilon=1e-7)=>Math.abs(a-b)<epsilon;
const same=(a,b)=>a.length===b.length&&a.every((v,i)=>near(v,b[i]));
function edge(tile,axis,value) {
  return [...new Set(tile.polygons.flat().filter(p=>near(p[axis],value))
    .map(p=>p[1-axis]))].sort((a,b)=>a-b);
}
try {
  check(M.gridCatalog.length===256,"Expanded catalog incomplete");
  for(const a of M.gridCatalog) for(const b of M.gridCatalog) {
    if(a.values[1]===b.values[0]&&a.values[2]===b.values[3]) {
      check(same(edge(a,0,1),edge(b,0,0)),"Expanded horizontal endpoint mismatch");
      report.catalogPairs++;
    }
    if(a.values[3]===b.values[0]&&a.values[2]===b.values[1]) {
      check(same(edge(a,1,1),edge(b,1,0)),"Expanded vertical endpoint mismatch");
      report.catalogPairs++;
    }
  }
  for(const candidate of M.catalog) {
    const b=M.block(M.slots[candidate.role],candidate);
    check(same(b.profile(b.start),b.leftPort)&&same(b.profile(b.end),b.rightPort),"Module end values differ");
    for(let x=b.start;x<=b.end;x+=.125) {
      const p=b.profile(x),gap=p[1]-p[0];
      report.minimumClearance=Math.min(report.minimumClearance,gap);
      check(gap>3.5&&p[0]>0&&p[1]<M.height,"Invalid module envelope");
    }
  }
  for(const seed of [271,372,473]) {
    const worlds=methods.map(method=>M.makeWorld(method,seed));
    check(Buffer.from(worlds[0].grid.data).equals(Buffer.from(worlds[1].grid.data)),"16 and 256 signs differ");
    const stats=[];
    for(const world of worlds) {
      check(!C.playerCollides(world,world.spawn.x,world.spawn.y),"Spawn intersects terrain");
      check(M.solid(world,4,43)&&M.solid(world,176,43),"Open cap outside authored bounds");
      for(let x=6;x<175;x+=.125) {
        const p=world.profile(x);check(p.every(Number.isFinite)&&p[1]>=p[0],"Invalid sampled outline");
      }
      if(world.blocks.length) {
        for(let i=1;i<world.blocks.length;i++) {
          const a=world.blocks[i-1],b=world.blocks[i],bridge=world.transitions[i-1];
          if(bridge) {
            check(same(world.profile(bridge.start),a.rightPort),"Bridge start mismatch");
            check(same(world.profile(bridge.end),b.leftPort),"Bridge end mismatch");
          } else check(same(a.rightPort,b.leftPort),"Direct/WFC adjacent ports differ");
        }
      }
      if(world.solution) {
        const repeated=M.solve(seed);
        check(JSON.stringify(repeated.ids)===JSON.stringify(world.solution.ids),"WFC seed is not reproducible");
        check(world.solution.stats.observations>0&&world.solution.stats.removed>0,"No real WFC selection/propagation");
        for(const step of world.solution.trace) {
          check(step.options.includes(step.chosen),"Choice was outside candidate domain");
          check(step.before[step.slot]>1&&step.after[step.slot]===1,"Observation did not collapse a domain");
          check(step.after.every((n,i)=>n<=step.before[i]),"Propagation expanded a domain");
        }
      }
      stats.push({method:world.method,blocks:world.blocks.length,bridges:world.transitions.length,
        wfc:world.solution?.stats||null});
    }
    report.seeds.push({seed,methods:stats});
  }
  let rejected=false;try {M.solve(271,true);} catch(e) {rejected=/四次/.test(e.message);}
  check(rejected,"Impossible adjacent ports were accepted");report.conflictRejected=true;
  const shapes=methods.map(method=>JSON.stringify(M.makeWorld(method,271).outline.top));
  check(new Set(shapes.slice(2)).size===4,"Continuous and macro outputs are identical");
  report.distinctContinuousOutputs=4;report.ok=true;
} catch(e) {report.ok=false;report.failure=e.stack;process.exitCode=1;}
fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
console.log(JSON.stringify({ok:report.ok,folder,catalogPairs:report.catalogPairs,
  minimumClearance:report.minimumClearance,seeds:report.seeds.length,failure:report.failure}));
