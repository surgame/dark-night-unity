(function(){
  'use strict';
  const D=window.DN,T=D.T;
  D.createCaveMap=function(c){
    const m={id:c.id,title:c.title,seed:c.seed,organic:true,definition:c,tiles:new Uint8Array(D.cols*D.rows).fill(1),
      rooms:[],gates:[],ores:[],signs:[],worms:[],lights:[{x:11*T,y:12.5*T,radius:315,power:.94}],
      platforms:[],ladders:[],walls:new Map(),nests:[],clues:[],walkRoutes:[],ladderSites:[],seals:[],background:null,backgroundDirty:false};
    const byId=new Map(),degree=new Map(c.rooms.map(r=>[r.id,0]));
    for(const r of c.rooms){
      if(byId.has(r.id)||!Number.isInteger(r.x)||!Number.isInteger(r.floor)||r.x<4||r.x>125||r.floor<8||r.floor>72)throw Error(c.id+'：重复或越界洞室 '+r.id);
      byId.set(r.id,r);D.carveCaveRoom(m,r,m.rooms.length);
      m.rooms.push({id:r.id,name:r.name,x:r.id==='camp'?3:r.x-(r.radius||4),y:r.id==='camp'?7:r.floor-(r.height||5)-4,
        w:r.id==='camp'?22:(r.radius||4)*2+1,h:r.id==='camp'?9:(r.height||5)+5,anchorX:r.x+.5,floor:r.floor,zone:r.floor>45?2:r.floor>23?1:0});
    }
    function connect(a,b){
      if(!byId.has(a)||!byId.has(b))throw Error(c.id+'：通路引用不存在的洞室 '+a+' / '+b);
      degree.set(a,degree.get(a)+1);degree.set(b,degree.get(b)+1);
    }
    function walk(p){
      const a=byId.get(p.from),b=byId.get(p.to),points=p.points||[[a.x,a.floor],[b.x,b.floor]];
      const samples=D.carveCaveWalk(m,points,p.id);
      m.walkRoutes.push({...p,points,samples});
    }
    c.paths.forEach(p=>{connect(p.from,p.to);walk(p);});
    for(const s of c.shafts||[]){
      connect(s.from,s.to);
      if(![s.x,s.top,s.bottom].every(Number.isInteger)||s.x<3||s.x>D.cols-4||s.top<8||s.bottom>D.rows-6||s.bottom-s.top>14||s.bottom-s.top<2||![undefined,1,-1].includes(s.side))throw Error(c.id+'：梯井坐标须在界内、长度2–14格、side为1或-1 '+s.id);
      const lower=byId.get(s.from),upper=byId.get(s.to),side=s.side||1,exit=s.x+side*2;
      walk({id:s.id+'-lower',from:s.from,to:s.to,points:s.lower||[[lower.x,lower.floor],[s.x,s.bottom]],shaftAccess:true});
      walk({id:s.id+'-upper',from:s.from,to:s.to,points:s.upper||[[exit,s.top],[upper.x,upper.floor]],shaftAccess:true});
      D.carveCave(m,s.x-1,s.top-2,3,s.bottom-s.top+2);
      for(let x=s.x-1;x<=s.x+1;x++){m.tiles[s.bottom*D.cols+x]=1;m.tiles[(s.bottom+1)*D.cols+x]=1;}
      for(let x=exit;x!==exit+side*3;x+=side){m.tiles[s.top*D.cols+x]=1;m.tiles[(s.top+1)*D.cols+x]=1;D.carveCave(m,x,s.top-4,1,4);}
      m.ladderSites.push({id:s.id,x:(s.x+.5)*T,top:s.top*T,bottom:s.bottom*T,from:s.from,to:s.to,left:side<0,
        exitX:(exit+.5)*T,exitFloor:s.top,sealedTop:!!s.sealedTop});
      if(s.sealedTop)m.seals.push({id:s.id+'-roof',shaft:s.id,x:s.x-1,y:s.top,w:3,h:1});
    }
    for(const p of c.paths.filter(p=>p.sealX!==undefined)){
      const route=m.walkRoutes.find(r=>r.id===p.id),sample=route.samples.find(r=>r.x===p.sealX);
      if(!sample)throw Error(c.id+'：封岩位置不在通路上 '+p.id);
      m.seals.push({id:p.id+'-seal',path:p.id,x:p.sealX,y:sample.ceiling-2,w:p.sealWidth||2,h:sample.floor-sample.ceiling+2});
    }
    m.rooms.forEach(r=>{r.degree=degree.get(r.id);});
    D.populateCave(m,c);
    m.stats={rooms:m.rooms.length,junctions:m.rooms.filter(r=>r.degree>=3).length,deadEnds:m.rooms.filter(r=>r.id!=='camp'&&r.degree===1).length,
      loops:c.paths.length+(c.shafts||[]).length-c.rooms.length+1,surface:m.ores.filter(o=>!o.buried).length,buried:m.ores.filter(o=>o.buried).length,
      nests:m.nests.length,prebuiltLadders:0,clues:m.clues.length,sealedPassages:m.seals.length};
    return m;
  };
  D.registerMapBuilder('cave',D.createCaveMap);
})();
