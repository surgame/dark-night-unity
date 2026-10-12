(function(){
  'use strict';
  const D=window.DN,T=D.T;
  D.populateCave=function(m,c){
    let next=0;
    function rock(x,y,w=3,h=3,force=false){
      const indices=[];
      for(let j=y;j<y+h;j++)for(let i=x;i<x+w;i++){
        if(i<1||j<1||i>=D.cols-1||j>=D.rows-1)continue;
        const index=j*D.cols+i;
        if(!force&&m.tiles[index]===0)continue;
        m.tiles[index]=2;m.walls.set(index,{id:'wall-'+index,index,x:(i+.5)*T,y:(j+.5)*T,progress:0});indices.push(index);
      }return indices;
    }
    function ore(kind,x,y,amount,buried){
      const coverIndex=y*D.cols+x;
      if(buried&&m.tiles[coverIndex]!==2)return;
      m.ores.push({id:m.id+'-ore-'+next++,kind,x:(x+.5)*T,y:buried?(y+.5)*T:(y+1)*T-8,amount,initial:amount,progress:0,buried,coverIndex:buried?coverIndex:null,revealed:!buried});
    }
    m.seals.forEach(s=>{s.coverIndices=rock(s.x,s.y,s.w,s.h,true);});
    ore('iron',15,15,3,false);
    c.rooms.filter(r=>r.id!=='camp').forEach((r,i)=>{
      const x=r.x+1,floor=D.caveFloorAt(m,x,r.floor);
      if(floor===null)return;
      const kind=r.floor>43?'gold':'iron';
      rock(x,floor);ore(kind,x+1,floor+1,4+i%3,true);ore(kind,x+2,floor+2,5,true);
      if(r.surface)ore(kind,r.x,r.floor-1,3,false);
      if(r.nest){
        const coverIndex=(floor+2)*D.cols+x+1;
        if(m.tiles[coverIndex]===2)m.nests.push({id:m.id+'-nest-'+r.id,x:(x+1.5)*T,y:(floor+2.5)*T,floor:(floor+3)*T,coverIndex,opened:false,radius:55});
      }
    });
    m.clues=(c.clues||[]).map((q,i)=>({id:m.id+'-clue-'+i,...q,x:(q.x+.5)*T,y:q.y*T,discovered:false}));
    m.signs.push({x:23*T,y:16*T,text:'痕迹比木牌更远',note:'手电照到晶脉、矿屑或气流痕迹时，走近按 E 观察。高处和侧壁都可能有岔路；有些裂岩要试挖才知道后面是什么。'});
  };
})();
