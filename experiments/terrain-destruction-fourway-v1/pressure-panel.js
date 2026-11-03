(function(T) {
  "use strict";
  T.installPressure=function(root,api) {
    const el=id=>root.querySelector("#tp-"+id),base=T.pressureReferences??{},overrides=new Map();
    const colors={dual:"var(--viz-series-1)",whole:"var(--viz-series-2)",
      hybrid:"var(--viz-series-3)",edge:"var(--viz-series-4)"};
    let lastReport=null,local=false;
    const initial={profile:"holes",policy:"merged",rounds:16,load:1024};
    function state() {
      const s={...initial,...api.getState()};
      if(!T.pressureProfiles.includes(s.profile))s.profile=initial.profile;
      if(!T.pressurePolicies.includes(s.policy))s.policy=initial.policy;
      if(![8,16,32].includes(s.rounds))s.rounds=16;
      if(!T.pressureLoads.includes(s.load))s.load=1024;
      return s;
    }
    function source() {
      const s=state(),w=api.field().w,key=w+"/"+s.profile+"/"+s.policy;
      local=overrides.has(key);return overrides.get(key)??base[String(w)]??null;
    }
    const number=n=>n<.001?"<0.001":n<1?n.toFixed(3):n.toFixed(2);
    function drawChart(id,metric,data) {
      const node=el(id),d3=window.d3;if(!d3){node.replaceChildren();return;}
      const width=Math.max(260,node.parentElement.getBoundingClientRect().width),height=270;
      const svg=d3.select(node);svg.selectAll("*").remove();svg.attr("viewBox",`0 0 ${width} ${height}`);
      const margin={left:64,right:20,top:20,bottom:48};
      const points=T.methods.flatMap(method=>T.pressureLoads.map(load=>({method,load,
        value:data?.[load]?.[method]?.[metric]?.p50??0,p95:data?.[load]?.[method]?.[metric]?.p95??0})));
      const x=d3.scaleLog().base(4).domain([16,1024]).range([margin.left+4,width-margin.right-4]);
      const maximum=Math.max(16.67,d3.max(points,p=>p.p95)??0);
      const y=d3.scaleLinear().domain([0,maximum*1.13]).nice().range([height-margin.bottom-4,margin.top+4]);
      svg.append("rect").attr("data-chart-frame","").attr("x",margin.left).attr("y",margin.top)
        .attr("width",width-margin.left-margin.right).attr("height",height-margin.top-margin.bottom)
        .attr("fill","none").attr("stroke","var(--border)");
      const axisX=svg.append("g").attr("transform",`translate(0,${height-margin.bottom})`)
        .call(d3.axisBottom(x).tickValues(T.pressureLoads).tickFormat(d3.format("d")));
      const axisY=svg.append("g").attr("transform",`translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(4).tickFormat(d3.format("~g")));
      for(const axis of [axisX,axisY]) {
        axis.selectAll("path,line").attr("stroke","var(--border)");
        axis.selectAll("text").attr("fill","var(--foreground)");
      }
      svg.append("text").attr("class","axis-title").attr("data-axis","x")
        .attr("x",(margin.left+width-margin.right)/2).attr("y",height-7).attr("text-anchor","middle").text("每批破坏格数");
      svg.append("text").attr("class","axis-title").attr("data-axis","y")
        .attr("x",margin.left).attr("y",12).text(metric==="cpu"?"CPU（ms／批）":"完成（ms／批）");
      const budget=y(16.67);
      svg.append("line").attr("x1",margin.left).attr("x2",width-margin.right).attr("y1",budget).attr("y2",budget)
        .attr("stroke","var(--muted-foreground)").attr("stroke-dasharray","3 3");
      svg.append("text").attr("x",width-margin.right-3).attr("y",Math.max(margin.top+12,budget-5))
        .attr("text-anchor","end").text("16.67 ms");
      if(!data)return;
      for(const method of T.methods) {
        const rows=points.filter(p=>p.method===method);
        svg.append("path").datum(rows).attr("fill","none").attr("stroke",colors[method]).attr("stroke-width",2)
          .attr("d",d3.line().x(p=>x(p.load)).y(p=>y(p.value)));
        for(const p of rows) {
          svg.append("line").attr("x1",x(p.load)).attr("x2",x(p.load)).attr("y1",y(p.value)).attr("y2",y(p.p95))
            .attr("stroke",colors[method]).attr("stroke-width",1).attr("opacity",.6);
          svg.append("circle").attr("cx",x(p.load)).attr("cy",y(p.value)).attr("r",3).attr("fill",colors[method]);
          svg.append("circle").attr("cx",x(p.load)).attr("cy",y(p.value)).attr("r",16).attr("fill","transparent")
            .attr("data-tooltip",T.names[method]+" · "+p.load+"格 · P50 "+number(p.value)+"ms / P95 "+number(p.p95)+"ms");
        }
      }
      const ticks=axisX.selectAll("text").nodes();
      if(ticks.length){d3.select(ticks[0]).attr("text-anchor","start");d3.select(ticks.at(-1)).attr("text-anchor","end");}
    }
    function render() {
      const s=state(),report=source(),data=report?.summary?.[s.profile]?.[s.policy];
      const selected=data?.[s.load]?.dual;
      el("profile").value=s.profile;el("policy").value=s.policy;el("rounds").value=String(s.rounds);el("load").value=String(s.load);
      el("meta").textContent=report&&data?
        (local?"本机重测":"参考实测")+" · "+report.width+"×"+report.height+" 格 · "+report.rounds+
        " 轮 · 固定64×40格相机 · "+new Date(report.date).toLocaleDateString("zh-CN",{timeZone:"Asia/Shanghai"})+
        " · 时钟 "+report.timerResolutionMs.toFixed(2)+"ms"+
        (selected?" · 每组 "+selected.replicas+" 批，当前负载每方案累计 "+selected.operations.toLocaleString()+" 次 CPU 编辑":"")+
        " · "+report.renderer:"尚无当前策略测量 · 使用独立初始地图，保留上方切口";
      const legend=el("legend");legend.replaceChildren();
      for(const method of T.methods) {
        const span=document.createElement("span"),swatch=document.createElement("i");
        swatch.style.background=colors[method];span.append(swatch,document.createTextNode(T.names[method]));legend.append(span);
      }
      el("shape").textContent=data?T.methods.map(method=>{
        const p=data[s.load][method];return T.names[method]+"：末态网格 "+(p.meshBytes.p50/1048576).toFixed(2)+
          " MiB，批次上传 "+(p.uploadBytes.p50/1048576).toFixed(2)+" MiB";
      }).join("；")+"。有效载荷各计单份。":"";
      drawChart("cpu","cpu",data);drawChart("complete","complete",data);
      const body=el("results");body.replaceChildren();
      for(const method of T.methods) {
        const p=data?.[s.load]?.[method],tr=document.createElement("tr");
        const values=[T.names[method],p?number(p.groupCpu.p50)+" ms / "+p.replicas:"—",
          p?number(p.cpu.p50)+" ms":"—",p?number(p.complete.p50)+" / "+number(p.complete.p95)+" ms":"—",
          p?p.rebuilt.p50+" / "+p.dirty.p50:"—",p?p.nodes.p50.toLocaleString():"—"];
        values.forEach((value,i)=>{const td=document.createElement(i?"td":"th");td.textContent=value;
          if(i)td.className="text-end";tr.append(td);});body.append(tr);
      }
      el("capacity").textContent=data?T.methods.map(method=>{
        const within=T.pressureLoads.filter(load=>data[load][method].complete.p95<=16.67);
        return T.names[method]+"：已测负载中 P95≤16.67ms 的最大批次 "+(within.at(-1)??"不足16")+" 格";
      }).join("；")+"。仅为此实现与测试环境的预算参照。":"";
    }
    async function run(options={}) {
      if(api.busy())return null;
      const s=state(),f=api.field(),canvas=document.createElement("canvas");canvas.width=960;canvas.height=600;
      api.lock(true);
      try {
        lastReport=await T.pressureBenchmark({w:f.w,h:f.h,rounds:s.rounds,profiles:[s.profile],policies:[s.policy],
          canvas,palette:api.palette(),...options},p=>{
          el("progress").textContent=(p.phase==="warmup"?"预热／校准":"第 "+(p.round+1)+"/"+p.rounds+" 轮")+
            " · "+T.pressureLabels[p.profile]+" · "+T.policyLabels[p.policy]+" · "+p.load+
            " 格"+(p.method?" · "+T.names[p.method]:"");
        });
        for(const profile of lastReport.profiles)for(const policy of lastReport.policies)
          overrides.set(f.w+"/"+profile+"/"+policy,lastReport);
        el("progress").textContent="压力评估完成";render();return lastReport;
      }catch(error){const box=root.querySelector("#tf-error");box.hidden=false;box.textContent=error.message;throw error;}
      finally{api.lock(false);}
    }
    for(const name of ["profile","policy","rounds","load"])el(name).addEventListener("change",()=>{
      const value=el(name).value;api.setState({...state(),[name]:name==="rounds"||name==="load"?Number(value):value});render();
    });
    el("run").addEventListener("click",()=>run().catch(()=>{}));
    root._pressure=run;root._pressureInspect=()=>({state:state(),report:source(),last:lastReport});
    root._pressureRefresh=render;
    new ResizeObserver(render).observe(root.querySelector(".tp-charts"));
    render();
  };
})(globalThis.TerrainFour);
