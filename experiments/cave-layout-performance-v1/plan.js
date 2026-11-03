(function(root) {
  "use strict";
  const P=root.CavePerf={cell:32,step:2,chunk:64,
    methods:["contour","blocks","bridges","wfc"],
    names:{contour:"连续轮廓",blocks:"大块直拼",bridges:"大块＋过渡",wfc:"大块WFC"},
    directions:[[0,-1],[1,0],[0,1],[-1,0]]};
  P.hash=function(v) {v=Math.imul(v^(v>>>16),0x7FEB352D);v=Math.imul(v^(v>>>15),0x846CA68B);return (v^(v>>>16))>>>0;};
  P.random=function(seed) {let s=seed>>>0;return ()=>{s=P.hash(s+0x9E3779B9);return s/4294967296;};};
  P.key=(mask,code,variant)=>"m:"+mask+":"+code+":"+variant;
  P.port=(code,d)=>(code>>d)&1;
  P.width=state=>state?4.5:3;
  P.catalog=[];
  for(let mask=0;mask<16;mask++) for(let code=0;code<16;code++) {
    if(code&~mask) continue;
    for(let variant=0;variant<3;variant++)
      P.catalog.push({id:P.catalog.length,mask,code,variant,weight:[1.2,1,.8][variant]});
  }
  P.definition=function(mask,style,profile) {
    const code=profile==="standard"?mask:(P.hash(mask*313+style*7159)&mask);
    return {mask,code,variant:style,pin:true,key:"p:"+mask+":"+style+":"+profile,size:32};
  };
  P.makePlan=function(count=128,seed=271,profile="mixed") {
    const dimensions=count<=128?[16,12]:count<=512?[32,24]:[64,48];
    const [columns,rows]=dimensions,rng=P.random(seed),lookup=new Int32Array(columns*rows).fill(-1);
    const nodes=[],stack=[];
    function add(x,y,parent) {
      const node={id:nodes.length,x,y,mask:0,neighbors:[-1,-1,-1,-1],pin:false};
      lookup[y*columns+x]=node.id;nodes.push(node);stack.push(node.id);
      if(parent) connect(parent,node);
      return node;
    }
    function connect(a,b) {
      const dx=b.x-a.x,dy=b.y-a.y;
      const d=dx===1?1:dx===-1?3:dy===1?2:0;
      a.mask|=1<<d;b.mask|=1<<((d+2)%4);
      a.neighbors[d]=b.id;b.neighbors[(d+2)%4]=a.id;
    }
    add(1,1,null);
    while(nodes.length<count) {
      const node=nodes[stack[stack.length-1]],available=[];
      for(let d=0;d<4;d++) {
        const x=node.x+P.directions[d][0],y=node.y+P.directions[d][1];
        if(x>=0&&y>=0&&x<columns&&y<rows&&lookup[y*columns+x]<0) available.push([x,y]);
      }
      if(!available.length) {stack.pop();continue;}
      const point=available[Math.floor(rng()*available.length)];add(...point,node);
    }
    for(const node of nodes) for(const d of [1,2]) {
      const x=node.x+P.directions[d][0],y=node.y+P.directions[d][1];
      if(x>=columns||y>=rows) continue;
      const other=lookup[y*columns+x];
      if(other>=0&&node.neighbors[d]<0&&rng()<.24) connect(node,nodes[other]);
    }
    for(const node of nodes) if((node.x+node.y)%2===0&&rng()<.6) {
      node.pin=true;node.definition=P.definition(node.mask,Math.floor(rng()*2),profile);
    }
    const edges=[];
    for(const node of nodes) for(const d of [1,2]) if(node.neighbors[d]>=0)
      edges.push({a:node.id,b:node.neighbors[d],direction:d});
    const pinTypes=new Set(nodes.filter(n=>n.pin).map(n=>n.definition.key));
    const pinned=nodes.filter(n=>n.pin).sort((a,b)=>
      Math.hypot(a.x-columns/2,a.y-rows/2)-Math.hypot(b.x-columns/2,b.y-rows/2))[0]||nodes[0];
    return {count,seed,profile,columns,rows,nodes,edges,lookup,pinTypes:pinTypes.size,
      pins:nodes.filter(n=>n.pin).length,loops:edges.length-count+1,
      focus:{x:pinned.x*32+16,y:pinned.y*32+16},width:columns*32,height:rows*32};
  };
  P.fixedCodes=function(plan) {
    const codes=plan.nodes.map(n=>n.pin?n.definition.code:n.mask);
    for(const edge of plan.edges) {
      const a=plan.nodes[edge.a],b=plan.nodes[edge.b],d=edge.direction,o=(d+2)%4;
      const value=a.pin?P.port(a.definition.code,d):b.pin?P.port(b.definition.code,o):1;
      codes[a.id]=(codes[a.id]&~(1<<d))|(value<<d);
      codes[b.id]=(codes[b.id]&~(1<<o))|(value<<o);
    }
    return codes;
  };
  P.select=function(plan,method) {
    const started=performance.now(),rng=P.random(plan.seed^0xB113A),codes=P.fixedCodes(plan);
    const solution=method==="wfc"?P.solve(plan):null;
    const choices=plan.nodes.map(node=>{
      if(node.pin) return {...node.definition,node:node.id,x:node.x*32,y:node.y*32};
      const chosen=solution?P.catalog[solution.ids[node.id]]:{
        mask:node.mask,code:method==="bridges"?node.mask:codes[node.id],
        variant:Math.floor(rng()*3)};
      const size=method==="bridges"?24:32,pad=(32-size)/2;
      return {...chosen,size,pin:false,node:node.id,x:node.x*32+pad,y:node.y*32+pad,
        key:(size===24?"c:":"")+P.key(chosen.mask,chosen.code,chosen.variant)};
    });
    const connectors=[];
    if(method==="bridges") for(const edge of plan.edges) {
      const a=choices[edge.a],b=choices[edge.b],d=edge.direction,o=(d+2)%4;
      const length=32-(a.size+b.size)/2;if(!length) continue;
      const from=P.port(a.code,d),to=P.port(b.code,o),variant=Math.floor(rng()*3);
      connectors.push({kind:"bridge",length,from,to,variant,vertical:d===2,
        x:plan.nodes[edge.a].x*32+16+(d===1?a.size/2:0),
        y:plan.nodes[edge.a].y*32+16+(d===2?a.size/2:0),
        key:"b:"+length+":"+from+":"+to+":"+variant+":"+d});
    }
    return {plan,method,choices,connectors,solution,genMs:performance.now()-started};
  };
})(globalThis);
