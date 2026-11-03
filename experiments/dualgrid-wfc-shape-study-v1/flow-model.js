(function(root){
  "use strict";
  const api=root.ShapeStudy,lib=root.CaveWB,points=[[0,0],[1,0],[1,1],[0,1]],edges=[[0,1],[1,2],[3,2],[0,3]];
  function floatTile(values){
    const mask=values.reduce((n,v,i)=>n|(v>0?1<<i:0),0),hits=[];
    edges.forEach(([a,b],edge)=>{
      if((values[a]>0)===(values[b]>0))return;
      const t=values[a]/(values[a]-values[b]);
      hits.push({edge,point:[points[a][0]+(points[b][0]-points[a][0])*t,points[a][1]+(points[b][1]-points[a][1])*t]});
    });
    let segments=hits.length===2?[[hits[0],hits[1]]]:[];
    if(hits.length===4){
      const q=values[0]*values[2]-values[1]*values[3];
      const connect=q===0?mask===5:mask===5?q>0:q<0,pairA=mask===5?!connect:connect;
      segments=(pairA?[[0,3],[1,2]]:[[0,1],[2,3]]).map(pair=>pair.map(i=>hits.find(h=>h.edge===i)));
    }
    return{values,mask,segments};
  }
  function summary(domains){
    const values=new Float32Array(domains.length*4),counts=new Uint8Array(domains.length);
    domains.forEach((options,at)=>{
      counts[at]=options.length;
      for(const id of options)lib.catalog[id].values.forEach((v,i)=>{values[at*4+i]+=v/options.length;});
    });
    return{values,counts};
  }
  function replay(trace){
    const domains=trace.timeline.start.map(d=>d.slice()),frames=[summary(domains)];
    for(const event of trace.timeline.events){
      domains[event.at]=[event.chosen];
      for(const change of event.changes)domains[change.at]=change.after.slice();
      frames.push(summary(domains));
    }
    if(domains.some((d,i)=>d.length!==1||d[0]!==trace.map.tiles[i]))throw new Error("Replay diverged from the solved map.");
    return frames;
  }
  function line(points,x){
    for(let i=1;i<points.length;i++)if(x<=points[i][0]){
      const a=points[i-1],b=points[i];return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
    }
    return points.at(-1)[1];
  }
  function macroCurve(plan,config,slot,roof){
    const kind=api.macroKinds[config.kind],result=[],ports=[-.5,0,.5];
    for(let i=0;i<=32;i++){
      const t=i/32,x=slot*8+t*8,upper=line(plan.roof,x),lower=line(plan.floor,x);
      const baseline=roof?upper:lower,common=ports[config.left]*(1-t)+ports[config.right]*t,bump=Math.sin(Math.PI*t)**2;
      let y=baseline;
      for(let n=0;n<12;n++){
        const relative=Math.max(0,Math.min(1,(y-upper)/Math.max(1,lower-upper)));
        y=baseline+common+bump*(kind.roof*(1-relative)+kind.floor*relative);
      }
      result.push([x,y]);
    }
    return result;
  }
  async function buildFlow(seed=271){
    const data=await api.buildSteps(seed,{timeline:true}),e=data.trace.event;
    data.micro={initial:summary(data.trace.initial),before:summary(e.before),
      observed:summary(e.observed),after:summary(e.after),frames:replay(data.trace)};
    const domains=e.observed.map(d=>d.slice());
    data.propagationFrames=[summary(domains)];
    for(const change of e.changes){domains[change.at]=change.after.slice();data.propagationFrames.push(summary(domains));}
    data.macroCurves=data.macro.catalog.map(config=>Array.from({length:6},(_,slot)=>({
      roof:macroCurve(data.pair.plan,config,slot,true),floor:macroCurve(data.pair.plan,config,slot,false)
    })));
    return data;
  }
  Object.assign(api,{floatTile,domainSummary:summary,buildFlow,macroCurve});
})(globalThis);
