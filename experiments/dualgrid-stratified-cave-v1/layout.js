(function (C) {
  "use strict";
  // Bedding keeps long calm spans; short smooth transitions form irregular rock shoulders.
  const roof=[[10,27],[16,18],[26,16],[42,16],[49,18],[57,22],[64,28],
    [74,28],[83,26],[91,22],[107,22],[118,25],[124,29],[130,42],
    [140,49],[150,49],[160,43],[172,39],[187,39],[197,42],[203,50],
    [206,62],[213,77]];
  const floor=[[10,27],[16,34],[32,34],[42,34],[52,38],[66,38],
    [74,43],[84,43],[94,49],[111,49],[118,54],[128,54],[136,58],
    [148,58],[156,65],[165,65],[173,73],[190,73],[200,77],[207,78],[213,77]];
  const lowerRoof=[[82,99],[88,89],[101,85],[119,85],[125,88],
    [134,88],[142,95],[149,101]];
  const lowerFloor=[[82,99],[88,106],[101,106],[109,111],
    [125,111],[137,107],[144,106],[149,101]];
  const fissure=[[58,35,3.4],[59,45,3.1],[55,53,3.2],[47,59,3.3],
    [45,67,3.3],[49,73,3.5],[59,77,3.7],[73,81,3.5],[84,89,3.6],[91,98,3.8]];
  const blindCleft=[[140,101,3.1],[154,98,3.1],[161,92,3.2],[163,83,3.4]];
  function profile(points,x) {
    for (let i=1;i<points.length;i++) {
      if (x>points[i][0]) continue;
      const a=points[i-1],b=points[i],t=C.clamp((x-a[0])/(b[0]-a[0]),0,1);
      return C.mix(a[1],b[1],t*t*(3-2*t));
    }
    return points[points.length-1][1];
  }
  function tube(x,y,points) {
    for (let i=1;i<points.length;i++) {
      const a=points[i-1],b=points[i],dx=b[0]-a[0],dy=b[1]-a[1];
      const t=C.clamp(((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy),0,1);
      if (Math.hypot(x-a[0]-dx*t,y-a[1]-dy*t)<C.mix(a[2],b[2],t)) return true;
    }
    return false;
  }
  function air(x,y) {
    const main=x>=10&&x<=213&&y>profile(roof,x)&&y<profile(floor,x);
    const basin=x>=82&&x<=149&&y>profile(lowerRoof,x)&&y<profile(lowerFloor,x);
    const mouth=tube(x,y,[[22,-4,2.8],[23,8,2.9],[26,22,3.2]]);
    // Attached shoulders add a broken-rock silhouette without unsupported islands.
    const shoulder=x>=96&&x<=102&&y<27-1.2*Math.abs(x-99);
    const toe=x>=179&&x<=187&&y>70+Math.abs(x-183)*.7;
    return (main||basin||mouth||tube(x,y,fissure)||tube(x,y,blindCleft))&&!shoulder&&!toe;
  }
  C.sites=[
    {id:"entry",name:"岩层入口",x:27,y:26,zoom:1.2,radius:17,
      detail:"自然裂口落入宽岩台，长直岩层与弯曲侧壁并存。"},
    {id:"terrace",name:"错层岩台",x:65,y:35,zoom:1.4,radius:17,
      detail:"平缓台地逐段错开，洞顶收拢，地面不排成规则台阶阵列。"},
    {id:"hall",name:"层状洞厅",x:104,y:37,zoom:.95,radius:24,
      detail:"较平直的顶板、宽岩台和一侧突出的岩肩，保持非对称空腔。"},
    {id:"elbow",name:"弯折压顶",x:139,y:53,zoom:1.45,radius:15,
      detail:"通道沿岩层拐弯下沉，转折连续收放，保留局部平直段。"},
    {id:"deep",name:"深部岩室",x:183,y:57,zoom:.9,radius:25,
      detail:"近直立断面与弧形岩肩组成大空腔，尽头逐渐压低收窄。"},
    {id:"fissure",name:"曲折裂隙",x:51,y:63,zoom:1.05,radius:19,
      detail:"斜裂隙先向左折回再向右下延伸，打破水平洞厅的方向感。"},
    {id:"basin",name:"下层凹厅",x:116,y:100,zoom:.9,radius:24,
      detail:"平顶段与浅凹地面组合，两侧壁自然内收，形成低位探索区。"},
    {id:"cleft",name:"上折盲隙",x:162,y:90,zoom:1.1,radius:18,
      detail:"下层侧枝逐步向上收窄，终止于厚岩壁，保留天然盲洞。"}
  ];
  C.makeWorld=function (cuts=[]) {
    const world={width:224,height:124,data:new Uint8Array(225*125),
      cuts:new Set(),revision:0};
    for (let y=0;y<=world.height;y++) for (let x=0;x<=world.width;x++)
      world.data[y*(world.width+1)+x]=air(x,y)?0:1;
    for (const index of cuts) {
      if (!Number.isInteger(index)||index<0||index>=world.data.length) continue;
      const x=index%(world.width+1),y=Math.floor(index/(world.width+1));
      if (x<2||y<2||x>world.width-2||y>world.height-2) continue;
      world.data[index]=0;world.cuts.add(index);
    }
    world.ore=[{x:43,y:35},{x:80,y:44},{x:107,y:50},{x:145,y:59},
      {x:188,y:74},{x:204,y:79},{x:105,y:109},{x:134,y:110}];
    const feet=C.floorAt(world,26,29,10);
    world.spawn={x:26,y:(feet===null?33.5:feet)-1.02};
    return world;
  };
  C.nearestSite=function (x,y) {
    return C.sites.reduce((a,b)=>Math.hypot(a.x-x,a.y-y)<Math.hypot(b.x-x,b.y-y)?a:b);
  };
})(globalThis.CaveWhitebox);
