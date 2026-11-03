(function (C) {
  "use strict";
  // Rectangles are authored in the gameplay plane. Only DualGrid rounds the final corners.
  const openings=[
    [12,11,44,27],[19,0,26,17],
    [40,20,199,27],[61,11,90,27],[108,8,145,27],[158,13,183,27],
    [193,19,203,65],[176,48,216,65],
    [42,58,194,65],[106,47,143,65],[10,58,48,65],
    [65,63,74,109],[68,102,216,109],
    [84,94,108,109],[129,86,177,109]
  ];
  const barriers=[[38,57,42,66],[208,101,213,110]];
  const contains=(r,x,y)=>x>=r[0]&&x<r[2]&&y>=r[1]&&y<r[3];
  C.minePlan={
    openings,barriers,
    rails:[
      {from:39,to:192,floor:26.5,gaps:[[86,90],[151,155]]},
      {from:44,to:214,floor:64.5,gaps:[[78,85],[145,149]]},
      {from:75,to:207,floor:108.5,gaps:[[113,119],[181,188]]}
    ],
    supports:[
      [30,26.5,14.7],[48,26.5,6.3],[70,26.5,14.7],[96,26.5,6.3],
      [116,26.5,17.7],[136,26.5,17.7],[151,26.5,6.3],[170,26.5,12.7],[186,26.5,6.3],
      [54,64.5,6.3],[92,64.5,6.3],[116,64.5,16.7],[136,64.5,16.7],
      [159,64.5,6.3],[185,64.5,15.7],[208,64.5,15.7],
      [81,108.5,6.3],[97,108.5,13.7],[119,108.5,6.3],
      [138,108.5,21.7],[159,108.5,21.7],[188,108.5,6.3],[202,108.5,6.3]
    ],
    carts:[[96,26.5],[153,64.5],[179,108.5]],
    hoist:{x:198,top:19,bottom:64.5},
    serviceShaft:{left:65,right:73,bottom:108.5}
  };
  C.sites=[
    {id:"entry",name:"入井站",x:28,y:20,zoom:1.35,radius:12,
      detail:"短竖口落到平直站台，入口仍有旧支架和装运痕迹。"},
    {id:"haul",name:"运输主巷",x:75,y:23,zoom:1.1,radius:22,
      detail:"长直巷、重复支护和中断轨道建立人工开凿的秩序。"},
    {id:"stope",name:"支护采场",x:127,y:17,zoom:1.05,radius:20,
      detail:"矩形扩挖空间接入主巷，部分支护破损，矿层向四周延伸。"},
    {id:"hoist",name:"提升竖井",x:198,y:42,zoom:.6,radius:21,
      detail:"连接上中两层的直井；提升机和吊笼仅为静态设备占位。"},
    {id:"station",name:"中段装卸站",x:188,y:58,zoom:1.1,radius:18,
      detail:"井底加宽为规则装卸区，横巷向左延伸到旧设施。"},
    {id:"pump",name:"旧泵房",x:124,y:55,zoom:1.2,radius:17,
      detail:"方整设备室与窄巷交替，保留基座和管线的结构位置。"},
    {id:"sealed",name:"封闭支巷",x:25,y:61,zoom:1.35,radius:12,
      detail:"支巷被实心堵段和交叉封板隔开，可用采掘尝试打开。"},
    {id:"service",name:"检修竖井",x:70,y:85,zoom:.57,radius:19,
      detail:"狭长直井连接中下两层，井底接入最旧的一段水平矿道。"},
    {id:"deep",name:"深层工作面",x:158,y:98,zoom:.9,radius:24,
      detail:"较大的矩形采场、遗留矿车和封堵端头，形成深入后的终点。"}
  ];
  C.makeWorld=function (cuts=[]) {
    const world={width:232,height:128,data:new Uint8Array(233*129),
      cuts:new Set(),revision:0};
    for (let y=0;y<=world.height;y++) for (let x=0;x<=world.width;x++) {
      const air=openings.some(r=>contains(r,x,y))&&!barriers.some(r=>contains(r,x,y));
      world.data[y*(world.width+1)+x]=air?0:1;
    }
    for (const index of cuts) {
      if (!Number.isInteger(index) || index<0 || index>=world.data.length) continue;
      const x=index%(world.width+1),y=Math.floor(index/(world.width+1));
      if (x<2 || y<2 || x>world.width-2 || y>world.height-2) continue;
      world.data[index]=0;world.cuts.add(index);
    }
    world.ore=[
      {x:81,y:28},{x:132,y:28},{x:146,y:17},{x:121,y:66},
      {x:144,y:53},{x:168,y:110},{x:204,y:110},{x:179,y:100}
    ];
    const feet=C.floorAt(world,25,22,9);
    world.spawn={x:25,y:(feet===null?26.5:feet)-1.02};
    return world;
  };
  C.nearestSite=function (x,y) {
    return C.sites.reduce((a,b)=>Math.hypot(a.x-x,a.y-y)<Math.hypot(b.x-x,b.y-y)?a:b);
  };
})(globalThis.CaveWhitebox);
