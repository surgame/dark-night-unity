(function () {
  'use strict';
  const D=window.DN;
  function interpolate(points,x){
    for(let i=1;i<points.length;i++)if(x<=points[i][0]){const a=points[i-1],b=points[i];return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);}
    return points.at(-1)[1];
  }
  D.createNaturalMap=function(config){
    const T=D.T,tiles=new Uint8Array(D.cols*D.rows).fill(1),floorProfile=[],ceilProfile=[],rooms=[],sites=[];
    const carve=(x,y,w,h)=>{for(let j=Math.max(1,y);j<Math.min(D.rows-1,y+h);j++)for(let i=Math.max(1,x);i<Math.min(D.cols-1,x+w);i++)tiles[j*D.cols+i]=0;};
    for(let x=3;x<=128;x++){
      const wave=x<25?0:Math.sin(x*.28+config.seed)*.8+Math.sin(x*.67)*.35;
      let floor=Math.round(interpolate(config.profile,x)+wave);
      if(x>3)floor=D.clamp(floor,floorProfile[x-1]-1,floorProfile[x-1]+2);
      floorProfile[x]=floor;
      let height=4.5+Math.sin(x*.19+config.seed)*.6;
      config.chambers.forEach((c,i)=>{height+=(config.bulge+(i%2)*2)*Math.exp(-(((x-c)/(config.spread+i%3))**2));});
      if(x<25)height=5+3*Math.sin((x-3)/22*Math.PI);
      const ceiling=Math.max(2,floor-Math.round(D.clamp(height,4,14)));
      ceilProfile[x]=ceiling;carve(x,ceiling,1,floor-ceiling);
    }
    const camp={id:'camp',name:'飞船入口',x:3,y:7,w:22,h:9,zone:0,anchorX:10,floor:16};rooms.push(camp);
    config.chambers.forEach((cx,i)=>{
      const floor=floorProfile[cx];rooms.push({id:`cavern-${i}`,name:['碎岩拱厅','晶脉弯洞','回声岩腔','深层矿庭','尽头金矿'][i],x:cx-6,y:ceilProfile[cx]-1,w:13,h:floor-ceilProfile[cx]+3,zone:floor>45?2:floor>23?1:0,anchorX:cx,floor});
    });
    function sideChamber(sx,index){
      const bottom=floorProfile[sx],top=Math.max(4,bottom-config.shaftDepths[index]),width=13+(index%2)*3;
      // 竖井到高处岩台，主洞地面仍完整；高处石沿为实际基岩，没有预搭梯具。
      carve(sx-1,top-2,3,bottom-top+2);
      for(let x=sx+2;x<sx+2+width&&x<129;x++){
        const t=(x-sx-2)/(width-1),f=top+(x<sx+5?0:Math.round(Math.sin(t*Math.PI)*1.5));
        const h=Math.max(3,Math.round(3+4*Math.sin(t*Math.PI)+Math.sin(x*.6)));
        carve(x,f-h,1,h);tiles[f*D.cols+x]=1;tiles[(f+1)*D.cols+x]=1;
      }
      const r={id:`upper-${index}`,name:`高处侧洞 ${index+1}`,x:sx+2,y:top-7,w:width,h:10,zone:bottom>45?2:1,anchorX:sx+6,floor:top+1,upper:true};rooms.push(r);
      sites.push({id:`shaft-${index}`,x:(sx+.5)*T,top:top*T,bottom:bottom*T,label:'高处矿台',roomId:r.id});
    }
    config.shafts.forEach(sideChamber);
    let loopPath=null;
    if(config.loop){
      const points=[[35,17],[48,18],[61,25],[73,33],[86,floorProfile[86]]];loopPath=[];
      for(let x=35;x<=86;x++){
        const f=Math.round(interpolate(points,x)),h=4+Math.round(Math.sin(x*.33+config.seed)*.6);
        carve(x,f-h,1,h);loopPath.push({x,floor:f});
        // 保留两条洞道之间的真实岩柱，前端高台不把主洞地面挖掉。
        if(x<71&&f+3<floorProfile[x]){tiles[f*D.cols+x]=1;tiles[(f+1)*D.cols+x]=1;}
      }
      carve(34,15,3,floorProfile[35]-15);
      sites.push({id:'loop-shaft',x:35.5*T,top:17*T,bottom:floorProfile[35]*T,label:'上层回环',roomId:'upper-loop'});
      sites.push({id:'loop-return',x:71.5*T,top:loopPath.find(p=>p.x===70).floor*T,bottom:floorProfile[71]*T,label:'回环后口',roomId:'upper-loop',connector:true,left:true});
      rooms.push({id:'upper-loop',name:'岩柱上层曲道',x:37,y:12,w:37,h:23,zone:1,anchorX:47,floor:18,upper:true});
    }
    const m={id:config.id,title:config.title,natural:true,needsPlayerLadders:true,rooms,tiles,floorProfile,ceilProfile,ladderSites:sites,loopPath,gates:[],ores:[],signs:[],worms:[],lights:[{x:11*T,y:12.5*T,radius:315,power:.94}],platforms:[],ladders:[],walls:new Map(),nests:[],background:null,backgroundDirty:false};
    D.populateNatural(m,config);
    m.signs.push({x:23*T,y:16*T,text:'4 梯具 / X 回收',note:'这里没有现成梯子。4 号工具可在有支撑且通畅的位置搭梯，瞄准上下方向选择长度，落脚后 X 回收。主洞能步行往返。'});
    sites.forEach(site=>m.signs.push({x:site.x-18,y:site.bottom,text:'↑ 岩台',note:'竖井上方可能有收获。站稳后选 4 号梯具，瞄准井内上方并按住左键；W 爬到梯顶后，用空格加方向键跳上岩台。也可从高台往下搭。'}));
    m.stats={rooms:rooms.length,junctions:sites.length,deadEnds:config.shafts.length,loops:config.loop?1:0,surface:m.ores.filter(o=>!o.buried).length,buried:m.ores.filter(o=>o.buried).length,nests:m.nests.length,prebuiltLadders:0};
    return m;
  };
  D.registerMapBuilder('natural',D.createNaturalMap);
})();
