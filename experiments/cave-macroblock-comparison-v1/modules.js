(function (M) {
  "use strict";
  const boundaries=[8,26,50,70,90,108,132,154,172];
  const roof=[13,14,23,16,32,39,33,49,56],floor=[29,30,36,41,45,52,58,67,68];
  const specs=[
    ["entry","岩层入口",[[0,0],[.2,-3],[.72,-3],[1,0]],[[0,0],[.25,0],[.75,0],[1,0]]],
    ["terrace","长岩台",[[0,0],[.2,-3],[.7,-3],[1,0]],[[0,0],[.24,0],[.4,-3],[.78,-3],[1,0]]],
    ["hall","高顶洞厅",[[0,0],[.16,-8],[.4,-10],[.78,-10],[1,0]],[[0,0],[.25,1],[.7,1],[1,0]]],
    ["elbow","弯折岩肩",[[0,0],[.3,1],[.5,5],[.75,1],[1,0]],[[0,0],[.28,0],[.48,-2],[.77,-1],[1,0]]],
    ["throat","压顶窄道",[[0,0],[.22,3],[.52,5],[.8,3],[1,0]],[[0,0],[.3,-.6],[.7,-.6],[1,0]]],
    ["basin","下凹洞厅",[[0,0],[.25,-5],[.7,-5],[1,0]],[[0,0],[.25,5],[.65,7],[1,0]]],
    ["ledge","断层台地",[[0,0],[.25,-1],[.5,2],[.65,-2],[1,0]],[[0,0],[.2,-2],[.6,-2],[.78,0],[1,0]]],
    ["end","收拢盲端",[[0,0],[.3,-2],[.6,-2],[.86,0],[1,0]],[[0,0],[.28,1],[.65,2],[1,0]]]
  ];
  M.slots=specs.map((s,i)=>({id:s[0],name:s[1],top:s[2],bottom:s[3],
    index:i,start:boundaries[i],end:boundaries[i+1],width:boundaries[i+1]-boundaries[i]}));
  M.flatten=function(knots,role,edge) {
    const spans={"0:1":[1,2],"1:1":[2,3],"2:0":[2,3],
      "5:0":[1,2],"6:1":[1,2],"7:0":[1,2]};
    const pair=spans[role+":"+edge];if(!pair) return;
    const y=(knots[pair[0]][1]+knots[pair[1]][1])/2;
    knots[pair[0]][1]=y;knots[pair[1]][1]=y;
  };
  M.port=function(index,state) {
    const offset=[-1.4,0,1.4][state];
    return [roof[index]-offset,floor[index]+offset*.65];
  };
  M.catalog=[];
  for(const slot of M.slots) for(let left=0;left<3;left++) for(let right=0;right<3;right++)
    for(let variant=0;variant<3;variant++) {
      if(Math.abs(left-right)>1) continue;
      if(slot.id==="throat"&&variant!==2&&left!==right) continue;
      M.catalog.push({id:M.catalog.length,role:slot.index,left,right,variant,
        weight:[1.3,1,.8][variant]});
    }
  M.block=function(slot,candidate,start=slot.start,end=slot.end) {
    const left=M.port(slot.index,candidate.left),right=M.port(slot.index+1,candidate.right);
    const v=candidate.variant;
    const make=(points,edge)=>{
      const knots=points.map(p=>{
        const base=M.mix(left[edge],right[edge],p[0]);
        const shape=p[1]*(v===1?.78:v===2?1.14:1);
        const amplitude=edge===0?(slot.id==="throat"?1:2.8):1.8;
        const drift=(v===1?-1:v===2?1:0)*amplitude*Math.sin(Math.PI*p[0])**2;
        const t=p[0]===0||p[0]===1?p[0]:M.clamp(p[0]+(v-1)*.055*Math.sin(Math.PI*p[0]),.02,.98);
        return [start+t*(end-start),base+shape+drift];
      });
      M.flatten(knots,slot.index,edge);
      const hard=v===2&&(slot.id==="elbow"||slot.id==="ledge")?[2,3]:[];
      return M.curve(knots,hard);
    };
    const top=make(slot.top,0),bottom=make(slot.bottom,1);
    return {...candidate,slot:slot.index,name:slot.name,start,end,
      leftPort:left,rightPort:right,profile:x=>[top(x),bottom(x)]};
  };
  M.assemble=function(method,seed) {
    const rng=M.random(seed^0xA50F17),solution=method==="wfc"?M.solve(seed):null;
    const blocks=M.slots.map((slot,i)=>{
      let candidate;
      if(solution) candidate=M.catalog[solution.ids[i]];
      else if(method==="blocks")
        candidate={id:-1,role:i,left:1,right:1,variant:Math.floor(rng()*3)};
      else candidate={id:-1,role:i,left:i===0?1:Math.floor(rng()*3),
        right:i===7?1:Math.floor(rng()*3),variant:Math.floor(rng()*3)};
      return M.block(slot,candidate,slot.start+(method==="bridges"&&i?3:0),
        slot.end-(method==="bridges"&&i<7?3:0));
    });
    const transitions=[];
    if(method==="bridges") for(let i=1;i<blocks.length;i++) {
      const a=blocks[i-1],b=blocks[i];
      transitions.push({start:a.end,end:b.start,left:a.rightPort,right:b.leftPort});
    }
    function interior(x) {
      const bridge=transitions.find(b=>x>=b.start&&x<=b.end);
      if(bridge) {
        const t=(x-bridge.start)/(bridge.end-bridge.start),u=t*t*t*(10-15*t+6*t*t);
        return [M.mix(bridge.left[0],bridge.right[0],u),M.mix(bridge.left[1],bridge.right[1],u)];
      }
      const block=blocks.find(b=>x>=b.start&&x<=b.end)||blocks[blocks.length-1];
      return block.profile(x);
    }
    return {profile:M.withCaps(interior),blocks,transitions,solution};
  };
})(globalThis.MacroCave);
