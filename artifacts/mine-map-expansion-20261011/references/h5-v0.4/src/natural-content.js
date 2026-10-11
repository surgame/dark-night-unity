(function () {
  'use strict';
  const D=window.DN;
  D.populateNatural=function(m,config){
    const T=D.T;let nextOre=0;
    const ore=(kind,tx,ty,amount,buried)=>m.ores.push({id:`${m.id}-ore-${nextOre++}`,kind,x:(tx+.5)*T,y:buried?(ty+.5)*T:(ty+1)*T-8,amount,initial:amount,progress:0,buried,coverIndex:buried?ty*D.cols+tx:null,revealed:!buried});
    function patch(x,y,w=3,h=3){
      for(let ty=y;ty<y+h;ty++)for(let tx=x;tx<x+w;tx++){
        if(tx<1||tx>=D.cols-1||ty<1||ty>=D.rows-1)continue;
        const index=ty*D.cols+tx;if(m.tiles[index]!==1)continue;
        m.tiles[index]=2;m.walls.set(index,{id:`wall-${index}`,index,x:(tx+.5)*T,y:(ty+.5)*T,progress:0});
      }
    }
    ore('iron',15,15,3,false);
    // 沿自然底面分散矿袋，位置跟随真实轮廓，三格深的矿坑仍能跳回。
    config.chambers.forEach((cx,i)=>{
      const x=cx-1,f=m.floorProfile[x],kind=i>1?'gold':'iron';
      patch(x,f);if(m.tiles[(f+1)*D.cols+x+1]===2)ore(kind,x+1,f+1,4+i%3,true);
      if(m.tiles[(f+2)*D.cols+x+2]===2)ore(kind,x+2,f+2,4+i%3,true);
      const sx=cx+4,sf=m.floorProfile[sx];ore(kind,sx,sf-1,3,false);
      if(i===2||i===4)m.nests.push({id:`buried-nest-${i}`,x:(x+1.5)*T,y:(f+2.5)*T,floor:(f+3)*T,coverIndex:(f+2)*D.cols+x+1,opened:false,radius:50});
    });
    m.ladderSites.forEach((site,i)=>{
      if(site.connector)return;
      const tx=Math.floor(site.x/T)+6;
      let floor=Math.floor(site.top/T);
      for(let y=floor;y<floor+6;y++)if(m.tiles[y*D.cols+tx]===1){floor=y;break;}
      ore('gold',tx,floor-1,5,false);patch(tx+2,floor);
      if(m.tiles[(floor+1)*D.cols+tx+3]===2)ore('gold',tx+3,floor+1,6,true);
      if(m.tiles[(floor+2)*D.cols+tx+4]===2)ore('gold',tx+4,floor+2,6,true);
    });
    m.nests=m.nests.filter(n=>m.tiles[n.coverIndex]===2);
  };
})();
