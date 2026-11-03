(function(P) {
  "use strict";
  function entropy(ids) {
    let sum=0,log=0;
    for(const id of ids) {const w=P.catalog[id].weight;sum+=w;log+=w*Math.log(w);}
    return Math.log(sum)-log/sum;
  }
  P.solve=function(plan) {
    const rng=P.random(plan.seed^0x7C11),versions=new Uint32Array(plan.count),heap=[];
    const stats={observations:0,propagations:0,removed:0,peakDomainValues:0};
    const domains=plan.nodes.map(node=>{
      const ids=P.catalog.filter(c=>c.mask===node.mask&&
        (plan.profile!=="standard"||c.code===node.mask)&&
        (!node.pin||(c.code===node.definition.code&&c.variant===node.definition.variant))).map(c=>c.id);
      stats.peakDomainValues+=ids.length;return ids;
    });
    function push(i) {
      versions[i]++;if(domains[i].length<2) return;
      const entry={i,version:versions[i],value:entropy(domains[i])+i*1e-10};
      let at=heap.length;heap.push(entry);
      while(at) {const parent=(at-1)>>1;if(heap[parent].value<=entry.value) break;
        heap[at]=heap[parent];at=parent;heap[at]=entry;}
    }
    function pop() {
      while(heap.length) {
        const first=heap[0],last=heap.pop();
        if(heap.length) {
          let at=0;heap[0]=last;
          while(true) {
            let child=at*2+1;if(child>=heap.length) break;
            if(child+1<heap.length&&heap[child+1].value<heap[child].value) child++;
            if(heap[at].value<=heap[child].value) break;
            [heap[at],heap[child]]=[heap[child],heap[at]];at=child;
          }
        }
        if(first.version===versions[first.i]&&domains[first.i].length>1) return first.i;
      }
      return -1;
    }
    function propagate(queue) {
      let cursor=0;
      while(cursor<queue.length) {
        const index=queue[cursor++],node=plan.nodes[index];
        for(let d=0;d<4;d++) {
          const at=node.neighbors[d];if(at<0) continue;
          let ports=0;for(const id of domains[index]) ports|=1<<P.port(P.catalog[id].code,d);
          const next=domains[at].filter(id=>ports&(1<<P.port(P.catalog[id].code,(d+2)%4)));
          stats.propagations++;
          if(!next.length) throw Error("预设端口冲突，未安装回退地图。");
          if(next.length!==domains[at].length) {
            stats.removed+=domains[at].length-next.length;domains[at]=next;push(at);queue.push(at);
          }
        }
      }
    }
    domains.forEach((_,i)=>push(i));propagate(plan.nodes.map(n=>n.id));
    while(true) {
      const best=pop();if(best<0) break;
      const options=domains[best];let roll=rng()*options.reduce((s,id)=>s+P.catalog[id].weight,0);
      let chosen=options[options.length-1];
      for(const id of options) {roll-=P.catalog[id].weight;if(roll<=0) {chosen=id;break;}}
      domains[best]=[chosen];versions[best]++;stats.observations++;propagate([best]);
    }
    const ids=domains.map(d=>d[0]);
    for(const edge of plan.edges) {
      const a=P.catalog[ids[edge.a]],b=P.catalog[ids[edge.b]];
      if(P.port(a.code,edge.direction)!==P.port(b.code,(edge.direction+2)%4))
        throw Error("WFC结果端口不一致。");
    }
    return {ids,stats};
  };
})(globalThis.CavePerf);
