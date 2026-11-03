(function () {
  "use strict";
  const P=globalThis.CaveBlockDesign,Q=globalThis.CaveDepthDesign={};
  Q.kind="cave-surface-depth-v1";
  Q.ids=["M01","M02","M03","M05","M04","M11","M06","M09","M12","M08","M10"];
  Q.modules=Q.ids.map(id=>({...P.byId(id)}));
  Q.modules.find(m=>m.id==="M01").name="地表洞口";
  Q.modules.push(
    {id:"D01",name:"上层回折下行",w:68,h:56,form:"descent",goal:"用两段下坡和转折平台，从浅洞降到中层洞厅。"},
    {id:"D02",name:"深层折坡",w:66,h:60,form:"descent",goal:"从中层继续回折下探，接近深层富矿。"}
  );
  Q.byId=id=>Q.modules.find(m=>m.id===id)||Q.modules[0];
  const deltas={M03:4,M04:2,M05:4,M06:4,M09:3,M11:-3};
  const offset=(m,x)=>(deltas[m.id]||0)*Math.max(0,Math.min(1,(x-1)/(m.w-2)));
  Q.shape=function(m){
    if(m.id==="M01")return {
      roof:[[0,-3],[2,-2.5],[6,0],[12,2],[18,5],[23,7.5],[24,7.5]],
      floor:[[0,1],[3,1],[8,3],[13,6],[18,8],[23,10],[24,10]]
    };
    const s=P.shape(m);
    return {roof:s.roof.map(([x,y])=>[x,y+offset(m,x)]),floor:s.floor.map(([x,y])=>[x,y+offset(m,x)])};
  };
  Q.branch=function(m){
    const b=P.branch(m);
    return b?{roof:b.roof.map(([x,y])=>[x,y+offset(m,x)]),floor:b.floor.map(([x,y])=>[x,y+offset(m,x)])}:null;
  };
  Q.surface=[[-100,-2],[-28,-2],[-6,-1],[14,-3],[22,-4],[30,-1],[38,0],[44,-2],[62,-4],[82,-2],[110,0],[140,-1],[180,-3],[220,-2],[240,0],[350,0]];
  Q.surfaceY=x=>{
    let i=1;
    while(i<Q.surface.length-1 && Q.surface[i][0]<x)i++;
    const a=Q.surface[i-1],b=Q.surface[i];
    return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
  };
  const place=(id,x,floor,flip=false)=>{
    const m=Q.byId(id),s=Q.shape(m);
    return {id,x,y:floor-s.floor[0][1],flip};
  };
  Q.nodes=[
    {id:"M01",x:38,y:0,flip:false},
    place("M02",72,16),place("M03",108,22),place("M05",144,34),
    place("M04",108,90,true),place("M11",60,96,true),place("M06",14,98,true),
    place("M09",58,156),place("M12",126,172),
    place("M08",190,27),place("M10",54,64,true)
  ];
  Q.point=(n,p)=>{
    const m=Q.byId(n.id);
    return [n.x+(n.flip?m.w-p[0]:p[0]),n.y+p[1]];
  };
  Q.world=(n,s)=>({roof:s.roof.map(p=>Q.point(n,p)),floor:s.floor.map(p=>Q.point(n,p))});
  Q.port=(id,type)=>{
    const n=Q.nodes.find(node=>node.id===id),m=Q.byId(id);
    const s=type==="branch"?Q.branch(m):Q.shape(m);
    return Q.point(n,type==="entry"?s.floor[0]:s.floor.at(-1));
  };
  function short(from,to,branch=false) {
    const a=Q.port(from,branch?"branch":"exit"),b=Q.port(to,"entry"),direction=Math.sign(b[0]-a[0])||1;
    return {from,to,branch,floor:[a,[a[0]+direction*2,a[1]],[b[0]-direction*2,b[1]],b]};
  }
  Q.links=[
    short("M01","M02"),short("M02","M03"),short("M03","M05"),
    {id:"D01",from:"M05",to:"M04",floor:[
      Q.port("M05","exit"),[184,38],[210,51],[216,56],[210,62],[186,74],[178,74],[154,90],Q.port("M04","entry")
    ]},
    short("M04","M11"),short("M11","M06"),
    {id:"D02",from:"M06",to:"M09",floor:[
      Q.port("M06","exit"),[7,100],[-4,108],[-8,114],[-2,120],[22,132],[28,132],[52,156],Q.port("M09","entry")
    ]},
    short("M09","M12"),short("M05","M08",true),
    {from:"M04",to:"M10",branch:true,floor:[
      Q.port("M04","branch"),[104,87.5],[92,81],[86,77],[92,73],[104,67],[98,64],Q.port("M10","entry")
    ]}
  ];
  Q.linkShape=l=>({
    floor:l.floor,
    roof:l.floor.map(([x,y],i)=>{
      const t=i/(l.floor.length-1);
      return [x,y-(l.branch?1.5+t:2.5)];
    })
  });
  Q.contents=m=>P.contents(m).map(c=>({...c,y:c.y+offset(m,c.x)}));
  Q.cameras={
    all:{x:-25,y:-22,w:264,h:222},
    surface:{x:15,y:-15,w:102,h:57},
    shallow:{x:30,y:-12,w:201,h:89},
    middle:{x:-16,y:43,w:233,h:79},
    deep:{x:-19,y:108,w:211,h:87}
  };
}());
