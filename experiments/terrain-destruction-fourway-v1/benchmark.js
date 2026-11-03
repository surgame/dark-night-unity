(function(T) {
  "use strict";
  const pause=()=>new Promise(resolve=>setTimeout(resolve,0));
  T.distribution = values => {
    const a=values.slice().sort((x,y)=>x-y),at=p=>a[Math.min(a.length-1,Math.floor((a.length-1)*p))]??0;
    return {n:a.length,p50:at(.5),p95:at(.95),max:a.at(-1)??0};
  };
  T.benchmark = async function(options,onProgress=()=>{}) {
    const {w=96,h=60,rounds=3,samples=32,canvas,palette}=options,warmup=8;
    const gpu=T.gpu(canvas),camera={x:0,y:0,w,h};
    const field=T.field(w,h),start=performance.now();
    let quantum=Infinity,previous=performance.now();
    for(let i=0;i<20000;i++){const now=performance.now();if(now>previous)quantum=Math.min(quantum,now-previous);previous=now;}
    const report={version:1,date:new Date().toISOString(),userAgent:navigator.userAgent,renderer:gpu.renderer,
      timerResolutionMs:Number.isFinite(quantum)?Math.round(quantum*100000)/100000:.1,
      software:/swiftshader|llvmpipe|software|basic render/i.test(gpu.renderer),
      width:w,height:h,canvas:[canvas.width,canvas.height],rounds,samples,warmup,
      textureBytes:gpu.textureBytes,assetMs:gpu.assetMs,details:false,
      timing:"JS performance.now; gl.finish + 1 pixel readback. Synchronous completion is not isolated GPU time or FPS.",
      scope:"Standalone WebGL2; no Unity, collision, lighting, networking, Spine or particles.",
      order:[],raw:[],static:[],initial:[],summary:{},checks:[],success:false};
    try {
      for(let round=0;round<rounds;round++) {
        const order=T.methods.map((_,j)=>T.methods[(j+round)%T.methods.length]);report.order.push(order);
        for(const method of order) {
          for(const kind of ["scatter","tunnel"]) {
            onProgress({round,rounds,method,kind,progress:(round*8+order.indexOf(method)*2+(kind==="tunnel"?1:0))/(rounds*8)});
            await pause();gpu.clear();
            const a=T.adapter(field,method),uploaded=gpu.upload(a);
            gpu.gl.finish();
            report.initial.push({round,method,kind,...a.initial,upload:uploaded.ms,payload:T.payload(a)});
            for(let i=0;i<4;i++)gpu.draw(a,camera,palette,false,true);
            if(kind==="scatter")for(let i=0;i<16;i++)
              report.static.push({round,method,...gpu.draw(a,camera,palette,false,true)});
            const hits=T.workload(field,samples+warmup,kind);
            if(hits.length!==samples+warmup)throw Error("共同有效破坏序列不足。");
            for(let n=0;n<hits.length;n++) {
              const t=performance.now(),result=T.breakCell(a,hits[n]);
              if(!result)throw Error("共同破坏序列遇到无效格。");
              const upload=gpu.upload(a,result.chunks),draw=gpu.draw(a,camera,palette,false,true);
              const total=performance.now()-t;
              if(n>=warmup)report.raw.push({round,method,kind,hit:n-warmup,index:hits[n],
                select:result.select,geometry:result.geometry,upload:upload.ms,submit:draw.submit,complete:draw.sync,
                total,uploadBytes:upload.bytes,patches:result.patches.length,chunks:result.chunks.length,
                changed:result.changed.length,nodes:result.nodes,propagations:result.propagations,
                backtracks:result.backtracks,expansions:result.expansions,fallback:result.fallback});
              if(n%8===7)await pause();
            }
            const errors=T.checkPorts(a.masks,a.variants,w,h),actual=T.signature(a.masks),
              expected=T.signature(T.masks(a.field));
            report.checks.push({round,method,kind,ports:errors,geometryEqual:actual===expected,signature:actual});
            if(errors||actual!==expected)throw Error("基准的几何或端口合同失效。");
          }
        }
      }
      for(const method of T.methods) {
        const raw=report.raw.filter(r=>r.method===method),idle=report.static.filter(r=>r.method===method);
        const initial=report.initial.filter(r=>r.method===method),s={};
        for(const name of ["select","geometry","upload","submit","complete","total","chunks","nodes","uploadBytes"])
          s[name]=T.distribution(raw.map(r=>r[name]));
        s.static=T.distribution(idle.map(r=>r.sync));
        s.initial=T.distribution(initial.map(r=>r.select+r.geometry+r.upload));
        s.payload=initial[0].payload;s.fallbacks=raw.filter(r=>r.fallback).length;
        s.scatter=T.distribution(raw.filter(r=>r.kind==="scatter").map(r=>r.total));
        s.tunnel=T.distribution(raw.filter(r=>r.kind==="tunnel").map(r=>r.total));
        report.summary[method]=s;
      }
      report.success=true;report.elapsedMs=performance.now()-start;return report;
    }finally{gpu.dispose();}
  };
})(globalThis.TerrainFour);
