(function (C) {
  "use strict";
  const floorPoints=[[8,23],[26,28],[45,42],[68,47],[89,53],[109,64],
    [135,76],[158,84],[183,94],[205,108],[238,119]];
  const chambers=[[22,14,8],[57,27,30],[143,29,36],[217,22,19]];
  const lower=[[82,53],[77,68],[62,82],[48,92],[41,101]];
  const upper=[[68,29],[79,20],[94,19]];
  function profile(x) {
    for (let i=1;i<floorPoints.length;i++) {
      if (x>floorPoints[i][0]) continue;
      const a=floorPoints[i-1],b=floorPoints[i],t=C.clamp((x-a[0])/(b[0]-a[0]),0,1);
      return C.mix(a[1],b[1],t*t*(3-2*t))+.3*Math.sin(x*.29);
    }
    return floorPoints[floorPoints.length-1][1];
  }
  const ellipse=(x,y,cx,cy,rx,ry)=>((x-cx)/rx)**2+((y-cy)/ry)**2<1;
  function distance(x,y,a,b) {
    const dx=b[0]-a[0],dy=b[1]-a[1];
    const t=C.clamp(((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy),0,1);
    return Math.hypot(x-a[0]-dx*t,y-a[1]-dy*t);
  }
  const tube=(x,y,points,r)=>points.slice(1).some((p,i)=>distance(x,y,points[i],p)<r);
  function air(x,y) {
    const f=profile(x);let extra=0;
    for (const [cx,rx,height] of chambers) {
      const u=(x-cx)/rx;
      if (Math.abs(u)<1) extra=Math.max(extra,height*Math.sqrt(1-u*u));
    }
    let clearance=6+extra;
    if (x>227) clearance*=Math.sqrt(Math.max(0,1-((x-227)/13)**2));
    const main=x>=9 && x<240 && y<f && y>f-clearance;
    let empty=main || tube(x,y,[[20,-4],[20,20]],3.2) ||
      tube(x,y,upper,2.7) || ellipse(x,y,101,19,13,8) ||
      tube(x,y,lower,3.6) || ellipse(x,y,45,108,25,14) ||
      tube(x,y,[[63,109],[82,115]],3) || ellipse(x,y,84,115,6,5);
    // A ceiling-attached rock shoulder divides the large hall without a floating platform.
    if (ellipse(x,y,165,54,8,14) && main) empty=false;
    return empty;
  }
  C.sites=[
    {id:"entry",name:"缓坡入口",x:23,y:22,zoom:1,detail:"短竖口接缓坡，形成入洞与返程的起点。"},
    {id:"bell",name:"钟形洞厅",x:57,y:30,zoom:.8,radius:24,detail:"大净空与低位地面，先建立明显的空间尺度变化。"},
    {id:"cleft",name:"压顶通道",x:103,y:58,zoom:1.45,detail:"洞顶贴近行走区，连接两侧开放洞厅。"},
    {id:"hall",name:"断层大厅",x:144,y:57,zoom:.8,radius:28,detail:"高顶与附壁岩肩组合，保留主路和高位内容位置。"},
    {id:"mine",name:"深处矿窝",x:219,y:106,zoom:1.2,detail:"主路末端收拢，矿层占位靠近可采掘边界。"},
    {id:"upper",name:"高位侧穴",x:99,y:18,zoom:1.3,detail:"短小高位支线；到达方式保留为后续设计，不预装梯具。"},
    {id:"rift",name:"下行裂隙",x:68,y:78,zoom:1.1,detail:"狭长斜裂支路，改变探索方向与视野范围。"},
    {id:"basin",name:"碎石盆地",x:45,y:107,zoom:1,radius:20,detail:"宽而低的凹形支洞，末端留一个小矿窝。"}
  ];
  C.makeWorld=function (cuts=[]) {
    const world={width:248,height:140,data:new Uint8Array(249*141),
      cuts:new Set(),revision:0};
    for (let y=0;y<=world.height;y++) for (let x=0;x<=world.width;x++)
      world.data[y*(world.width+1)+x]=air(x,y)?0:1;
    for (const index of cuts) {
      if (!Number.isInteger(index) || index<0 || index>=world.data.length) continue;
      const x=index%(world.width+1),y=Math.floor(index/(world.width+1));
      if (x<2 || y<2 || x>world.width-2 || y>world.height-2) continue;
      world.data[index]=0;world.cuts.add(index);
    }
    world.ore=[
      {x:61,y:profile(61)+1.25},{x:108,y:profile(108)+1.7},
      {x:147,y:profile(147)+1.1},{x:221,y:profile(221)+1.6},
      {x:227,y:profile(227)+2.2},{x:83,y:119},{x:103,y:25}
    ];
    const feet=C.floorAt(world,20,profile(20)-4,10);
    world.spawn={x:20,y:(feet===null?profile(20):feet)-1.02};
    return world;
  };
  C.nearestSite=function (x,y) {
    return C.sites.reduce((a,b)=>Math.hypot(a.x-x,a.y-y)<Math.hypot(b.x-x,b.y-y)?a:b);
  };
})(globalThis.CaveWhitebox);
