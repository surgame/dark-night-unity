"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const here=__dirname,old=path.resolve(here,"../cave-wfc-whitebox-v1"),files=[
  ...["plan.js","catalog.js","solver.js"].map(name=>path.join(old,name)),
  ...["model.js","macro.js","hybrid.js"].map(name=>path.join(here,name))];
files.forEach(file=>vm.runInThisContext(fs.readFileSync(file,"utf8"),{filename:file}));
const report={date:"2026-10-31",scope:"Independent shape and dependency checks; no Unity, collision or actor traversal.",
  source:files.map(file=>({file,sha256:crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex")})),cases:[],failures:[]};
(async()=>{
  for(let mask=0;mask<16;mask++)assert.equal(ShapeStudy.toMask(ShapeStudy.tile16(mask).mask),mask);
  const expected=[[0,1],[1,0],[0,.5],[.5,1],[1,.5],[.5,0]];
  for(let shape=1;shape<=12;shape++){
    assert.deepEqual([ShapeStudy.edge(shape,0),ShapeStudy.edge(shape,1)],expected[(shape-1)%6]);
    assert(ShapeStudy.slopePolygon(shape).every(p=>p.every(v=>v>=0&&v<=1)));
  }
  report.existingSlopeFormulas=12;
  for(const scene of ["basin","shoulder","network"])for(const seed of [271,902,1643]){
    try{
      const pair=await ShapeStudy.generate(seed,scene);
      assert.equal(pair.hybridPlan.macroLayout.mismatches,0);
      assert.equal(pair.hybridPlan.macroLayout.selected.length,6);
      assert.equal(pair.hybridPlan.macroLayout.selected[0].left,1);
      assert.equal(pair.hybridPlan.macroLayout.selected[5].right,1);
      const before={dual:Array.from(pair.dual.tiles),wfc:Array.from(pair.wfc.tiles),hybrid:Array.from(pair.hybrid.flags)};
      const a=ShapeStudy.metrics(pair.dual),b=ShapeStudy.metrics(pair.wfc);
      assert.deepEqual(a.positions,[.5]);assert(b.positions.some(v=>v!==.5));
      assert(pair.baseSamples.dual.every((v,i)=>(v>0)===(pair.baseSamples.wfc[i]>0)));
      ShapeStudy.dig(pair,true);
      const changed={dual:pair.dual.changed,wfc:pair.wfc.changed};
      assert.equal(changed.dual.length,4,"Dual Grid local dependency");
      assert.equal(changed.wfc.length,4,"Frozen WFC local dependency");
      assert.deepEqual(changed.dual,changed.wfc);
      assert(pair.hybrid.changed.length>0&&pair.hybrid.changed.length<16,"Slope update remains local.");
      ShapeStudy.dig(pair,false);
      assert.deepEqual(Array.from(pair.dual.tiles),before.dual);
      assert.deepEqual(Array.from(pair.wfc.tiles),before.wfc);
      assert.deepEqual(Array.from(pair.hybrid.flags),before.hybrid);
      report.cases.push({scene,seed,dual:a,wfc:b,stats:pair.wfc.stats,changed});
    }catch(error){report.failures.push({scene,seed,error:String(error.stack||error)});}
  }
  const bad=ShapeStudy.makePlan(271,"basin");bad.allowed[0]=0;
  try{await CaveWB.solve(bad);report.failures.push({kind:"contradiction",error:"Invalid domain unexpectedly solved"});}
  catch(error){assert.match(error.message,/four bounded attempts/);report.boundedContradiction=true;}
})().catch(error=>report.failures.push({error:String(error.stack||error)})).finally(()=>{
  report.passed=!report.failures.length;
  const folder=path.join(here,"records");fs.mkdirSync(folder,{recursive:true});
  const file=path.join(folder,Date.now()+"-geometry.json");fs.writeFileSync(file,JSON.stringify(report,null,2)+"\n",{flag:"wx"});
  console.log(JSON.stringify({passed:report.passed,cases:report.cases.length,failures:report.failures,report:file},null,2));
  process.exitCode=report.passed?0:1;
});
