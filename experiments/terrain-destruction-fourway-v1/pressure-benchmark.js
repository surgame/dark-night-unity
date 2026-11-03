(function(T) {
  "use strict";
  const pause=()=>new Promise(resolve=>setTimeout(resolve,0));
  T.pressureBenchmark=async function(options,onProgress=()=>{}) {
    const {w=320,h=192,rounds=16,profiles=T.pressureProfiles,policies=T.pressurePolicies,
      loads=T.pressureLoads,canvas,palette}=options;
    const gpu=T.gpu(canvas),field=T.field(w,h),camera={
      x:Math.max(0,Math.min(w-64,w*.5-32)),y:Math.max(0,Math.min(h-40,h*.70-20)),w:64,h:40};
    if(w<64||h<40)throw Error("压力相机尺寸无效。");
    const started=performance.now(),bases=Object.fromEntries(T.methods.map(m=>[m,T.adapter(field,m)]));
    const report={version:2,date:new Date().toISOString(),width:w,height:h,rounds,profiles,policies,loads,
      renderer:gpu.renderer,software:/swiftshader|llvmpipe|software|basic render/i.test(gpu.renderer),
      userAgent:navigator.userAgent,canvas:[canvas.width,canvas.height],camera,timerResolutionMs:.1,
      warmupPerMethod:2,targetGroupMs:8,maxReplicas:128,details:false,raw:[],calibration:[],order:[],summary:{},checks:[],
      scope:"CPU throughput groups plus independent single-batch completion; WebGL2, not Unity FPS.",
      scheduling:"Both policies upload final dirty chunks once per batch; merged only defers geometry, not WFC solving.",
      success:false};
    let q=Infinity,last=performance.now();
    for(let i=0;i<20000;i++){const now=performance.now();if(now>last)q=Math.min(q,now-last);last=now;}
    report.timerResolutionMs=Number.isFinite(q)?Math.round(q*100000)/100000:.1;
    const replicas={},cache=new Map();
    function editsFor(profile,round) {
      const key=profile+":"+round;if(!cache.has(key))
        cache.set(key,T.pressureEdits(field,Math.max(...loads),profile,T.hash(round,profile.length,271)));
      return cache.get(key);
    }
    function prepare(a) {gpu.upload(a);gpu.draw(a,camera,palette,false,true);}
    try {
      for(const profile of profiles)for(const policy of policies)for(const load of loads) {
        onProgress({phase:"warmup",profile,policy,load,round:0,rounds});
        await pause();let fastest=Infinity;
        const edits=editsFor(profile,0).slice(0,load);
        for(const method of T.methods)for(let n=0;n<report.warmupPerMethod;n++) {
          const a=T.forkAdapter(bases[method]),start=performance.now();
          T.pressureBatch(a,edits,policy);const elapsed=performance.now()-start;
          fastest=Math.min(fastest,Math.max(elapsed,report.timerResolutionMs*.5));
        }
        const count=Math.min(report.maxReplicas,Math.max(1,2**Math.ceil(Math.log2(report.targetGroupMs/fastest))));
        replicas[profile+"/"+policy+"/"+load]=count;
        report.calibration.push({profile,policy,load,fastestMs:fastest,replicas:count});
      }
      for(let round=0;round<rounds;round++) {
        const methods=T.methods.map((_,j)=>T.methods[(j+round)%4]);report.order.push(methods);
        const scenarios=profiles.map((_,j)=>profiles[(j+round)%profiles.length]);
        for(const profile of scenarios)for(const policy of policies)for(const load of loads) {
          const edits=editsFor(profile,round).slice(0,load),k=replicas[profile+"/"+policy+"/"+load];
          let expected=null,expectedWfc=null;
          for(const method of methods) {
            onProgress({phase:"measure",profile,policy,load,method,round,rounds});
            await pause();
            // Fork/reset stays outside the measured group; changed meshes remain owned by each fork.
            const worlds=Array.from({length:k},()=>T.forkAdapter(bases[method]));
            const cpuStart=performance.now();let result;
            for(const a of worlds)result=T.pressureBatch(a,edits,policy);
            const groupCpu=performance.now()-cpuStart,cpu=groupCpu/k;
            const check=T.pressureCheck(worlds.at(-1));
            if(expected===null)expected=check.geometry;
            if(expected!==check.geometry)throw Error("方案间破坏结果不同。");
            if(method==="whole"||method==="hybrid") {
              if(expectedWfc===null)expectedWfc=check.variant;
              else if(expectedWfc!==check.variant)throw Error("两种WFC在压力序列中终态不同。");
            }
            const a=T.forkAdapter(bases[method]);prepare(a);
            // An independent real batch measures latency; it is never inferred from throughput.
            const begin=performance.now(),update=T.pressureBatch(a,edits,policy),cpuLatency=performance.now()-begin;
            const upload=gpu.upload(a,update.chunks),draw=gpu.draw(a,camera,palette,false,true),
              complete=performance.now()-begin,payload=T.payload(a);
            const final=T.pressureCheck(a);
            if(final.geometry!==check.geometry||final.variant!==check.variant)
              throw Error("CPU组与独立完成测量的结果不一致。");
            report.raw.push({round,profile,policy,load,method,replicas:k,groupCpu,cpu,cpuLatency,
              upload:upload.ms,uploadBytes:upload.bytes,submit:draw.submit,complete,
              rebuilt:update.rebuilds,dirty:update.chunks.length,nodes:update.nodes,
              propagations:update.propagations,backtracks:update.backtracks,expansions:update.expansions,
              fallbacks:update.fallbacks,meshBytes:payload.bytes,vertices:payload.vertices,
              draws:draw.draws,drawnVertices:draw.vertices,signature:check.geometry,variant:check.variant});
            report.checks.push({round,profile,policy,load,method,geometry:check.geometry,ports:check.ports});
          }
        }
      }
      for(const profile of profiles) {
        report.summary[profile]={};
        for(const policy of policies) {
          report.summary[profile][policy]={};
          for(const load of loads) {
            const row={};report.summary[profile][policy][load]=row;
            for(const method of T.methods) {
              const raw=report.raw.filter(r=>r.profile===profile&&r.policy===policy&&r.load===load&&r.method===method);
              const s={replicas:raw[0].replicas,operations:raw.reduce((n,r)=>n+r.load*r.replicas,0)};
              for(const name of ["cpu","groupCpu","cpuLatency","complete","upload","uploadBytes","rebuilt","dirty",
                "nodes","propagations","meshBytes","vertices","draws","drawnVertices"])
                s[name]=T.distribution(raw.map(r=>r[name]));
              s.fallbacks=raw.reduce((n,r)=>n+r.fallbacks,0);s.expansions=raw.reduce((n,r)=>n+r.expansions,0);
              row[method]=s;
            }
          }
        }
      }
      report.success=true;report.elapsedMs=performance.now()-started;
      report.validOperations=report.raw.reduce((n,r)=>n+r.load*(r.replicas+1),0);
      return report;
    }finally{gpu.dispose();}
  };
})(globalThis.TerrainFour);
