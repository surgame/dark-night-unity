(function (M) {
  "use strict";
  function entropy(options) {
    const sum=options.reduce((n,id)=>n+M.catalog[id].weight,0);
    const log=options.reduce((n,id)=>{const w=M.catalog[id].weight;return n+w*Math.log(w);},0);
    return Math.log(sum)-log/sum;
  }
  M.solve=function(seed,injectConflict=false) {
    const stats={observations:0,propagations:0,removed:0,attempts:0},trace=[];
    for(let attempt=0;attempt<4;attempt++) {
      stats.attempts=attempt+1;
      trace.length=0;
      const rng=M.random(seed^Math.imul(attempt+1,0x9E3779B1));
      const domains=M.slots.map((s,i)=>M.catalog.filter(c=>c.role===i&&
        (i!==0||c.left===1)&&(i!==7||c.right===1)).map(c=>c.id));
      if(injectConflict) {
        domains[3]=domains[3].filter(id=>M.catalog[id].right===0);
        domains[4]=domains[4].filter(id=>M.catalog[id].left===2);
      }
      const initial=domains.map(d=>d.length);
      function propagate(queue) {
        let at=0;
        while(at<queue.length) {
          const index=queue[at++];
          for(const delta of [-1,1]) {
            const neighbor=index+delta;if(neighbor<0||neighbor>=domains.length) continue;
            const from=delta<0?"left":"right",to=delta<0?"right":"left";
            const ports=new Set(domains[index].map(id=>M.catalog[id][from]));
            const next=domains[neighbor].filter(id=>ports.has(M.catalog[id][to]));
            stats.propagations++;
            if(!next.length) return false;
            if(next.length!==domains[neighbor].length) {
              stats.removed+=domains[neighbor].length-next.length;
              domains[neighbor]=next;queue.push(neighbor);
            }
          }
        }
        return true;
      }
      if(!propagate(domains.map((_,i)=>i))) continue;
      let failed=false;
      while(true) {
        let best=-1,minimum=Infinity;
        domains.forEach((d,i)=>{
          if(d.length<2) return;
          const value=entropy(d)+i*1e-8;
          if(value<minimum) {minimum=value;best=i;}
        });
        if(best<0) {
          const ids=domains.map(d=>d[0]);
          if(ids.some((id,i)=>i&&M.catalog[ids[i-1]].right!==M.catalog[id].left))
            throw Error("Solved macro ports do not match");
          return {ids,stats,trace,initial,library:M.catalog.length};
        }
        const before=domains.map(d=>d.length),options=domains[best].slice();
        let roll=rng()*options.reduce((n,id)=>n+M.catalog[id].weight,0);
        let chosen=options[options.length-1];
        for(const id of options) {roll-=M.catalog[id].weight;if(roll<=0) {chosen=id;break;}}
        domains[best]=[chosen];stats.observations++;
        if(!propagate([best])) {failed=true;break;}
        trace.push({slot:best,options,chosen,entropy:minimum,
          before,after:domains.map(d=>d.length)});
      }
      if(!failed) break;
    }
    throw Error("宏观端口冲突：四次有界求解均失败，保留上一有效地图。");
  };
})(globalThis.MacroCave);
