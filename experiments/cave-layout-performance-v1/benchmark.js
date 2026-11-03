(function(P) {
  "use strict";
  const quantile=(values,t)=>{
    const sorted=values.slice().sort((a,b)=>a-b),at=(sorted.length-1)*t,i=Math.floor(at);
    return sorted[i]+(sorted[Math.min(i+1,sorted.length-1)]-sorted[i])*(at-i);
  };
  P.summarize=function(records) {
    const fields=["generation","geometry","chunking","coldCPU","upload","chunkUpload","instanceUpload",
      "chunkDraw","instanceDraw","chunkGroup","instanceGroup"];
    const first=records[0],result={method:first.method,size:first.size,profile:first.profile,
      samples:records.length,resources:first.resources,plan:first.plan,wfc:first.wfc,stats:{}};
    for(const field of fields) {
      const values=records.flatMap(r=>Array.isArray(r[field])?r[field]:[r[field]]);
      result.stats[field]=values.length?{p50:quantile(values,.5),p95:quantile(values,.95),n:values.length}:
        {p50:null,p95:null,n:0,alias:"chunks"};
    }
    return result;
  };
  P.camera=function(plan,index=0) {
    const node=plan.nodes[Math.floor(plan.count*[.28,.63,.82][index%3])];
    return {x:node.x*32+16,y:node.y*32+16,width:256,height:160};
  };
  P.granularity=function() {
    let previous=performance.now(),minimum=Infinity;
    for(let i=0;i<20000;i++) {const now=performance.now();if(now>previous) minimum=Math.min(minimum,now-previous);previous=now;}
    return Number.isFinite(minimum)?minimum:.1;
  };
  P.run=async function(gpu,options={}) {
    const sizes=options.sizes||[128,512,2048],profiles=options.profiles||["standard","mixed"];
    const rounds=options.rounds||16,seed=options.seed||271,records=[];
    const report={date:new Date().toISOString(),identity:gpu.identity,userAgent:navigator.userAgent,
      sizes,profiles,rounds,seed,granularity:P.granularity(),records,summaries:[],cancelled:false,
      drawRepetitions:64,
      scope:"Cold CPU maps and fresh per-map template caches; static WebGL2 completion at fixed 256x160 camera. Group times enclose 64 real draws with finish after every pass. No Unity FPS."};
    if(gpu.error()!==0) throw Error("GPU当前状态异常，未启动测量。");
    const pause=()=>new Promise(resolve=>setTimeout(resolve,0));
    let finished=0;const total=sizes.length*profiles.length*rounds*4;
    for(const profile of profiles) for(const size of sizes) {
      // Warm shaders and the four paths before collecting this workload.
      const warmPlan=P.makePlan(size,seed-1,profile);
      for(const method of P.methods) {
        const p=P.bake(P.prepare(P.generate(warmPlan,method))),scene=gpu.build(p);
        try {gpu.render(scene,"chunks",P.camera(warmPlan));gpu.render(scene,"instances",P.camera(warmPlan));gpu.finish();}
        finally {scene.dispose();}
      }
      for(let round=0;round<rounds;round++) {
        if(options.cancelled?.()) {report.cancelled=true;return report;}
        const plan=P.makePlan(size,seed+round*101,profile);
        const order=P.methods.slice(round%4).concat(P.methods.slice(0,round%4));
        for(const method of order) {
          if(options.cancelled?.()) {report.cancelled=true;return report;}
          const started=performance.now(),g=P.generate(plan,method),prepared=P.prepare(g);
          P.bake(prepared);const coldCPU=performance.now()-started,resources=P.resources(prepared);
          const scene=gpu.build(prepared),chunkDraw=[],instanceDraw=[],chunkGroup=[],instanceGroup=[];
          const drawCounts={chunks:[],instances:[]};
          try {
            for(let sample=0;sample<6;sample++) {
              const camera=P.camera(plan,sample),policies=scene.alias?["chunks"]:
                sample%2?["instances","chunks"]:["chunks","instances"];
              for(const policy of policies) {
                gpu.finish();const begin=performance.now();
                const counts=gpu.render(scene,policy,camera);gpu.finish();
                (policy==="chunks"?chunkDraw:instanceDraw).push(performance.now()-begin);
                drawCounts[policy].push(counts);
              }
            }
            for(let sample=0;sample<4;sample++) {
              const camera=P.camera(plan,sample),policies=scene.alias?["chunks"]:
                sample%2?["instances","chunks"]:["chunks","instances"];
              for(const policy of policies) {
                gpu.finish();const begin=performance.now();
                for(let i=0;i<64;i++) {gpu.render(scene,policy,camera);gpu.finish();}
                (policy==="chunks"?chunkGroup:instanceGroup).push(performance.now()-begin);
              }
            }
            if(gpu.error()!==0) throw Error("WebGL reported an error during measured rendering.");
            records.push({method,size,profile,round,seed:plan.seed,
              generation:g.genMs,geometry:prepared.geometryMs,chunking:prepared.chunkMs,coldCPU,
              upload:scene.uploadMs,chunkUpload:scene.chunkUploadMs,instanceUpload:scene.instanceUploadMs,
              instanceAlias:scene.alias,chunkDraw,instanceDraw,chunkGroup,instanceGroup,resources,drawCounts,
              plan:{nodes:plan.count,pins:plan.pins,edges:plan.edges.length,loops:plan.loops},
              wfc:g.solution?.stats||null});
          } finally {scene.dispose();}
          finished++;options.progress?.({finished,total,profile,size,round,method});
          await pause();
        }
      }
      for(const method of P.methods)
        report.summaries.push(P.summarize(records.filter(r=>r.profile===profile&&r.size===size&&r.method===method)));
    }
    report.ok=true;return report;
  };
})(globalThis.CavePerf);
