(function(P) {
  "use strict";
  const root=document.getElementById("cave-layout-performance");if(!root) return;
  const $=id=>root.querySelector("#cp-"+id),kind="cave-layout-perf-v1",storage="dark-nights-cave-layout-performance-v1";
  let state={method:"bridges",profile:"mixed",size:512,rounds:16,seed:271,view:"local",policy:"chunks"};
  let gpu,scene=null,prepared=null,plan=null,report=P.reference||null,job=null,disposed=false,lastWritten="";
  const pending=[],on=(target,type,handler,options)=>{
    target.addEventListener(type,handler,options);pending.push(()=>target.removeEventListener(type,handler,options));};
  function compatible(value) {const data=value?.privateContent||value;return data?.kind===kind?data:null;}
  function sanitize(data) {
    return {method:P.methods.includes(data.method)?data.method:"bridges",
      profile:data.profile==="standard"?"standard":"mixed",size:[128,512,2048].includes(data.size)?data.size:512,
      rounds:[8,16,32].includes(data.rounds)?data.rounds:16,seed:Number.isInteger(data.seed)&&data.seed>0?data.seed:271,
      view:data.view==="all"?"all":"local",policy:data.policy==="instances"?"instances":"chunks"};
  }
  let loaded=compatible(window.openai?.widgetState);
  if(!loaded) try {loaded=compatible(JSON.parse(localStorage.getItem(storage)));}catch{}
  if(loaded) state=sanitize(loaded);
  function color(token) {
    const span=document.createElement("span");span.style.color="var("+token+")";root.append(span);
    const style=getComputedStyle(span).color;span.remove();
    const c=document.createElement("canvas");c.width=c.height=1;const ctx=c.getContext("2d");
    ctx.fillStyle=style;ctx.fillRect(0,0,1,1);return [...ctx.getImageData(0,0,1,1).data].slice(0,3).map(v=>v/255);
  }
  function colors() {if(gpu) gpu.colors({air:color("--background"),rock:color("--cp-rock"),edge:color("--cp-edge")});}
  function camera() {
    if(state.view==="local") return {...plan.focus,width:256,height:160};
    const width=Math.max(plan.width+32,(plan.height+32)*1.6);
    return {x:plan.width/2,y:plan.height/2,width,height:width/1.6};
  }
  function overlay(cam) {
    const canvas=$("overlay"),box=canvas.getBoundingClientRect(),dpr=Math.min(devicePixelRatio||1,2);
    canvas.width=box.width*dpr;canvas.height=box.height*dpr;
    const ctx=canvas.getContext("2d");ctx.setTransform(dpr,0,0,dpr,0,0);
    ctx.clearRect(0,0,box.width,box.height);ctx.strokeStyle="rgb("+color("--foreground").map(v=>v*255).join(",")+")";
    ctx.fillStyle=ctx.strokeStyle;ctx.lineWidth=1;ctx.setLineDash([3,4]);
    ctx.font='12px "Microsoft YaHei",sans-serif';ctx.textAlign="left";
    for(const node of plan.nodes) if(node.pin) {
      const x=((node.x*32-cam.x)/cam.width+.5)*box.width;
      const y=((node.y*32-cam.y)/cam.height+.5)*box.height,size=32/cam.width*box.width;
      if(x+size<0||y+size<0||x>box.width||y>box.height) continue;
      ctx.strokeRect(x+2,y+2,size-4,size-4);
      if(state.view==="local"&&box.width>480) ctx.fillText("预设",x+5,y+15);
    }
  }
  function draw() {
    if(!gpu||!scene||job?.running) return;
    const cam=camera();gpu.render(scene,state.policy,cam);gpu.finish();overlay(cam);
  }
  function preview() {
    if(job?.running) return;
    scene?.dispose();plan=P.makePlan(state.size,state.seed,state.profile);
    prepared=P.bake(P.prepare(P.generate(plan,state.method)));scene=gpu.build(prepared);
    $("plan").textContent=plan.count+"位置 · "+plan.pins+"锁定房间 · "+plan.edges.length+"连接 · "+
      plan.loops+"回路 · 种子"+state.seed;
    $("caption").textContent=P.names[state.method]+" · "+(state.view==="local"?"固定相机近看":"全图");
    draw();
  }
  function currentRows() {
    if(!report||report.seed!==state.seed) return [];
    return report.summaries.filter(r=>r.size===state.size&&r.profile===state.profile);
  }
  function fmt(value) {
    const threshold=report?.granularity||.1;
    return value<threshold?"<"+threshold.toFixed(2):value.toFixed(value<10?2:1);
  }
  const pair=s=>fmt(s.p50)+" / "+fmt(s.p95);
  function tables() {
    const rows=currentRows(),timing=[],resources=[];
    for(const method of P.methods) {
      const row=rows.find(r=>r.method===method),name=P.names[method];
      if(!row) {
        timing.push("<tr><td>"+name+"</td><td colspan=\"7\">尚未测量当前输入</td></tr>");
        resources.push("<tr><td>"+name+"</td><td colspan=\"5\">尚未测量当前输入</td></tr>");continue;
      }
      const s=row.stats,r=row.resources;
      timing.push("<tr><td>"+name+"</td>"+["generation","geometry","chunking"].map(k=>
        '<td class="text-end">'+fmt(s[k].p50)+"</td>").join("")+
        '<td class="text-end">'+pair(s.coldCPU)+"</td>"+
        '<td class="text-end">'+fmt(s.chunkUpload.p50)+" / "+
          (method==="contour"?"同左":fmt(s.instanceUpload.p50))+"</td>"+
        '<td class="text-end">'+pair(s.chunkGroup)+"</td>"+
        '<td class="text-end">'+(method==="contour"?"同分块":pair(s.instanceGroup))+"</td></tr>");
      resources.push("<tr><td>"+name+"</td>"+[
        r.presetUsed+" / "+r.presetLibrary,r.freeUsed+" / "+r.freeLibrary,r.instances,
        r.chunkVertices.toLocaleString()+" / "+r.sourceVertices.toLocaleString(),
        (r.chunkBytes/1048576).toFixed(2)+" / "+(r.sourceBytes/1048576).toFixed(2)
      ].map(v=>'<td class="text-end">'+v+"</td>").join("")+"</tr>");
    }
    $("times").innerHTML=timing.join("");$("resources").innerHTML=resources.join("");
    const date=report?new Date(report.date).toLocaleString("zh-CN",{timeZone:"Asia/Shanghai",hour12:false}):"";
    $("source").textContent=rows.length?(report===P.reference?"已测参考":"本页实测")+" · "+
      report.rounds+"轮独立地图 · "+date+" · 每方案"+rows[0].stats.chunkGroup.n+"组×64次同步绘制":
      "选择测量按钮生成当前输入的结果。";
  }
  function update() {
    for(const key of ["profile","size","rounds","view","policy"]) $(key).value=String(state[key]);
    root.querySelectorAll("[data-method]").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.method===state.method)));
    $("hardware").textContent=gpu?"当前GPU："+gpu.identity.renderer:"";
    if(report===P.reference&&report&&report.identity.renderer!==gpu?.identity.renderer)
      $("hardware").textContent+="；参考GPU："+report.identity.renderer;
    tables();
  }
  function save() {
    const data={kind,...state};lastWritten=JSON.stringify(data);
    try{localStorage.setItem(storage,lastWritten);}catch{}
    window.openai?.setWidgetState?.({modelContent:{view:"cave-layout-performance",...state,
      benchmark:report?{rounds:report.rounds,renderer:report.identity.renderer,reference:report===P.reference}:null},
      privateContent:data})?.catch(()=>{});
  }
  function fail(e) {$("error").hidden=false;$("error").textContent=String(e.message||e);}
  async function run(options={}) {
    if(job?.running) throw Error("已有基准运行，不重复启动。");
    job={running:true,cancelled:false,finished:0,total:0};
    $("progress").hidden=false;$("cancel").hidden=false;$("error").hidden=true;
    root.querySelectorAll("button:not(#cp-cancel),select").forEach(e=>e.disabled=true);
    let stamp=0;
    try {
      const result=await P.run(gpu,{sizes:options.sizes||[state.size],
        profiles:options.profiles||[state.profile],rounds:options.rounds||state.rounds,seed:state.seed,
        cancelled:()=>job.cancelled||disposed,progress:p=>{
          Object.assign(job,p);
          if(performance.now()-stamp>200||p.finished===p.total) {
            stamp=performance.now();$("progress").max=p.total;$("progress").value=p.finished;
            $("status").textContent="测量 "+p.finished+" / "+p.total+" · "+p.size+"位置 · "+P.names[p.method];
          }
        }});
      if(result.ok) {report=result;$("status").textContent="完成 · "+result.records.length+"个独立地图管线。";}
      else $("status").textContent="已停止，保留上一完整结果。";
      return result;
    } catch(e) {fail(e);throw e;}
    finally {
      job.running=false;$("progress").hidden=true;$("cancel").hidden=true;
      root.querySelectorAll("button,select").forEach(e=>e.disabled=false);update();draw();save();
    }
  }
  try {gpu=P.createGPU($("scene"));colors();preview();update();}
  catch(e) {fail(e);}
  root.querySelectorAll("[data-method]").forEach(b=>on(b,"click",()=>{
    state.method=b.dataset.method;try{preview();update();save();}catch(e){fail(e);}
  }));
  for(const key of ["profile","size","rounds","view","policy"]) on($(key),"change",()=>{
    state=sanitize({...state,[key]:["size","rounds"].includes(key)?Number($(key).value):$(key).value});
    try {if(["profile","size"].includes(key)) preview();else draw();update();save();}catch(e){fail(e);}
  });
  on($("seed"),"click",()=>{state.seed+=101;try{preview();update();save();}catch(e){fail(e);}});
  on($("run"),"click",()=>run().catch(()=>{}));
  on($("all"),"click",()=>run({sizes:[128,512,2048]}).catch(()=>{}));
  on($("cancel"),"click",()=>{if(job) job.cancelled=true;});
  on($("export"),"click",()=>{
    if(!report) return;
    const url=URL.createObjectURL(new Blob([JSON.stringify(report,null,2)],{type:"application/json"}));
    const a=document.createElement("a");a.href=url;a.download="cave-layout-performance.json";a.click();
    setTimeout(()=>URL.revokeObjectURL(url),1000);
  });
  const resize=new ResizeObserver(draw);resize.observe($("scene"));
  const media=matchMedia("(prefers-color-scheme: dark)");
  on(media,"change",()=>{colors();draw();});
  const observer=new MutationObserver(()=>{colors();draw();});
  observer.observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
  on(window,"openai:set_globals",event=>{
    const data=compatible(event.detail?.globals?.widgetState);
    if(data&&JSON.stringify(data)!==lastWritten&&!job?.running) {state=sanitize(data);preview();update();}
  });
  root._benchmark=run;
  root._inspect=()=>({ready:!!gpu,choices:{...state},plan:plan?{nodes:plan.count,pins:plan.pins,
    loops:plan.loops,edges:plan.edges.length}:null,resources:prepared?P.resources(prepared):null,
    uploadBytes:scene?.uploadBytes||0,
    gpu:gpu?.identity,job:job?{running:job.running,finished:job.finished,total:job.total}:null,
    resultRows:currentRows().length,error:$("error").hidden?null:$("error").textContent});
  on(window,"pagehide",()=>{disposed=true;if(job) job.cancelled=true;
    resize.disconnect();observer.disconnect();pending.forEach(fn=>fn());scene?.dispose();gpu?.dispose();});
})(globalThis.CavePerf);
