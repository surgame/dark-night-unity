(function () {
  'use strict';
  const D=window.DN;
  D.createNetworkMap = function (config) {
    const T=D.T,tiles=new Uint8Array(D.cols*D.rows).fill(1),rooms=[],ladders=[],platforms=[],signs=[];
    const carve=(x,y,w,h)=>{for(let j=y;j<y+h;j++)for(let i=x;i<x+w;i++)if(i>0&&j>0&&i<D.cols-1&&j<D.rows-1)tiles[j*D.cols+i]=0;};
    const roomByNode=new Map(),degree=new Map(config.nodes.map(n=>[n,0]));
    config.edges.forEach(([a,b])=>{degree.set(a,degree.get(a)+1);degree.set(b,degree.get(b)+1);});
    config.nodes.forEach(n=>{
      const col=n%5,row=Math.floor(n/5),floor=16+row*16;
      const r={id:n===0?'camp':`room-${n}`,node:n,name:n===0?'飞船入口':`${String(n).padStart(2,'0')} ${degree.get(n)===1?'尽头矿室':degree.get(n)>2?'岔路洞室':'旧矿室'}`,x:3+col*26,y:floor-7,w:n===0?22:config.width,h:7,zone:Math.min(2,row),degree:degree.get(n)};
      rooms.push(r);roomByNode.set(n,r);carve(r.x,r.y,r.w,r.h);
    });
    config.edges.forEach(([a,b])=>{
      const ra=roomByNode.get(a),rb=roomByNode.get(b),floorA=ra.y+ra.h,floorB=rb.y+rb.h;
      if(floorA===floorB){const left=ra.x<rb.x?ra:rb,right=left===ra?rb:ra;carve(left.x+left.w,floorA-4,right.x-left.x-left.w,4);}
      else{
        const upper=floorA<floorB?ra:rb,lower=upper===ra?rb:ra,top=upper.y+upper.h,bottom=lower.y+lower.h,x=upper.x+7;
        carve(x,top-2,3,bottom-top+2);
        ladders.push({id:`ladder-${a}-${b}`,x:(x+1.5)*T,top:top*T,bottom:bottom*T});platforms.push({x:x*T,y:top*T,w:3*T});
      }
    });
    // 部分缺失联通留下短盲道；终端基岩保留，不能把整个迷宫随意挖穿。
    const stubs=[];
    config.nodes.filter(n=>n!==0&&n%3===0).forEach(n=>{
      const r=roomByNode.get(n),hasRight=config.edges.some(([a,b])=>(a===n&&b===n+1)||(b===n&&a===n+1));
      if(n%5<4&&!hasRight){const x=r.x+r.w;carve(x,r.y+r.h-3,4,3);stubs.push({x:x+4,y:r.y+r.h});}
    });
    const ores=[{id:'surface-start',kind:'iron',x:15.5*T,y:16*T-8,amount:3,initial:3,progress:0,buried:false}];
    const m={id:config.id,title:config.title,rooms,roomByNode,edges:config.edges,tiles,gates:[],ores,signs,worms:[],lights:[{x:11*T,y:12.5*T,radius:320,power:.94}],platforms,ladders,walls:new Map(),nests:[],stubs,background:null,backgroundDirty:false};
    let oreId=0;
    const addOre=(kind,tx,ty,amount,buried)=>{ores.push({id:`${config.id}-ore-${oreId++}`,kind,x:(tx+.5)*T,y:buried?(ty+.5)*T:(ty+1)*T-8,amount,initial:amount,progress:0,buried,coverIndex:buried?ty*D.cols+tx:null,revealed:!buried});};
    rooms.filter(r=>r.node!==0).forEach(r=>{
      const floor=r.y+r.h,px=r.x+2,kind=r.zone===2?'gold':'iron';
      for(let y=floor;y<floor+3;y++)for(let x=px;x<px+3;x++)if(tiles[y*D.cols+x]===1){const index=y*D.cols+x;tiles[index]=2;m.walls.set(index,{id:`wall-${index}`,index,x:(x+.5)*T,y:(y+.5)*T,progress:0});}
      addOre(kind,px+1,floor+1,3+r.zone,true);addOre(r.zone>0?'gold':'iron',px+2,floor+2,3+r.zone,true);
      if(r.node%4===1)addOre(kind,r.x+r.w-3,floor-1,3,false);
      // 尽头可有可挖侧岩。每个坑至多三格深，普通跳跃可以脱身。
      if(r.degree===1){
        const rightFree=!config.edges.some(([a,b])=>(a===r.node&&b===r.node+1)||(b===r.node&&a===r.node+1));
        const leftFree=!config.edges.some(([a,b])=>(a===r.node&&b===r.node-1)||(b===r.node&&a===r.node-1));
        const side=rightFree?1:leftFree&&r.x>5?-1:0;
        if(side){
          let sx=side>0?r.x+r.w:r.x-3;
          if(side>0)while(tiles[(floor-2)*D.cols+sx]===0&&sx<D.cols-4)sx++;
          for(let y=floor-3;y<floor;y++)for(let x=sx;x<sx+3;x++)if(tiles[y*D.cols+x]===1){const index=y*D.cols+x;tiles[index]=2;m.walls.set(index,{id:`wall-${index}`,index,x:(x+.5)*T,y:(y+.5)*T,progress:0});}
          const tx=side>0?sx+1:sx+1;addOre('gold',tx,floor-2,5,true);
          m.nests.push({id:`nest-${r.node}`,x:(tx+.5)*T,y:(floor-.5)*T,floor:floor*T,coverIndex:(floor-1)*D.cols+tx,opened:false,radius:200});
        }else m.nests.push({id:`nest-${r.node}`,x:(px+1.5)*T,y:(floor+2.5)*T,floor:(floor+3)*T,coverIndex:(floor+2)*D.cols+px+1,opened:false,radius:50});
      }
    });
    // 各图只保留少量巢穴；其他裂岩是安全矿或普通岩层，避免每次挖墙都变成战斗。
    m.nests=m.nests.filter((n,i)=>i%2===0).slice(0,3);
    signs.push({x:24*T,y:16*T,text:'W / S 扶梯',note:'横道用 A/D，扶梯用 W/S。浅色裂纹岩能挖，深色基岩保留；多数矿藏在岩层里。'});
    const junction=rooms.find(r=>r.degree>=3);
    if(junction)signs.push({x:(junction.x+5)*T,y:(junction.y+junction.h)*T,text:'岔路不全相通',note:'尽头也可能有矿，或藏着巢穴。挖浅色裂岩寻找墙后内容，别把邻近洞室当作一定相通。'});
    m.stats={rooms:rooms.length,junctions:rooms.filter(r=>r.degree>=3).length,deadEnds:rooms.filter(r=>r.node!==0&&r.degree===1).length,loops:config.edges.length-config.nodes.length+1,surface:ores.filter(o=>!o.buried).length,buried:ores.filter(o=>o.buried).length,nests:m.nests.length};
    return m;
  };
  D.registerMapBuilder('network',D.createNetworkMap);
})();
