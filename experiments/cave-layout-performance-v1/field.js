(function(P) {
  "use strict";
  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  function roundedBox(x,y,rx,ry,r) {
    const qx=Math.abs(x)-rx+r,qy=Math.abs(y)-ry+r;
    return r-Math.hypot(Math.max(qx,0),Math.max(qy,0))-Math.min(Math.max(qx,qy),0);
  }
  P.moduleField=function(def,x,y,noise=0) {
    const size=def.size,c=size/2,variant=def.variant;
    const rx=size*(def.pin?.34:.25)+(def.pin?0:noise*1.7);
    const ry=size*(def.pin?.22:.20)+(variant-1)*.9+(def.pin?0:noise*.8);
    let value=roundedBox(x-c,y-c,rx,ry,variant===0?1.8:variant===1?3.8:2.7);
    for(let d=0;d<4;d++) {
      if(!(def.mask&(1<<d))) continue;
      const along=d===0?c-y:d===1?x-c:d===2?y-c:c-x;
      if(along<0) continue;
      const t=clamp(along/c,0,1),bend=(variant-1)*1.5*Math.sin(Math.PI*t)**2;
      const sideways=d%2?y-c:x-c;
      const width=P.width(P.port(def.code,d))*(1+.10*Math.sin(Math.PI*t)**2);
      const corridor=Math.min(width-Math.abs(sideways-bend),along+1);
      value=Math.max(value,corridor);
    }
    return value;
  };
  P.makeField=function(generated) {
    const plan=generated.plan,w=plan.columns*16+1,h=plan.rows*16+1;
    let data=new Float32Array(w*h).fill(-50);
    const frozen=new Uint8Array(w*h);
    for(const choice of generated.choices) {
      const node=plan.nodes[choice.node],noise=(P.hash(plan.seed^node.id*1777)/4294967296-.5)*2;
      for(let y=0;y<=16;y++) for(let x=0;x<=16;x++) {
        const at=(node.y*16+y)*w+node.x*16+x;
        const v=P.moduleField(choice,x*2,y*2,choice.pin?0:noise);
        if(choice.pin) {data[at]=v;frozen[at]=1;}
        else if(!frozen[at]&&v>data[at]) data[at]=v;
      }
    }
    let next=new Float32Array(data.length);
    for(let pass=0;pass<2;pass++) {
      next.set(data);
      for(let y=1;y<h-1;y++) for(let x=1;x<w-1;x++) {
        const i=y*w+x;if(frozen[i]) continue;
        next[i]=data[i]*.5+(data[i-1]+data[i+1]+data[i-w]+data[i+w])*.125;
      }
      [data,next]=[next,data];
    }
    return {data,frozen,width:w,height:h};
  };
  P.generate=function(plan,method) {
    const started=performance.now(),g=P.select(plan,method);
    if(method==="contour") g.field=P.makeField(g);
    g.genMs=performance.now()-started;return g;
  };
})(globalThis.CavePerf);
