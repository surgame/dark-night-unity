(function () {
  'use strict';
  const D=window.DN;
  D.wallTarget=function(s){
    if(!s.mouse.active&&!s.touchMining)return null;
    const p=s.player,origin={x:p.x,y:p.y-20};
    const cursor={x:s.mouse.x+s.camera.x,y:s.mouse.y+s.camera.y};
    const angle=Math.atan2(cursor.y-origin.y,cursor.x-origin.x),range=Math.min(82,D.dist(origin,cursor)+5);
    const dx=Math.cos(angle),dy=Math.sin(angle);
    for(let r=0;r<=range;r+=2){
      const x=origin.x+dx*r,y=origin.y+dy*r;
      if(!D.pointSolid(s.map,x,y))continue;
      const tx=Math.floor(x/D.T),ty=Math.floor(y/D.T),index=ty*D.cols+tx;
      const wall=s.map.walls.get(index);
      if(wall&&s.map.tiles[index]===2)return{type:'wall',object:wall};
      return{type:'bedrock',object:{id:`rock-${index}`,x:(tx+.5)*D.T,y:(ty+.5)*D.T,index}};
    }
    return null;
  };
  D.excavate=function(s,wall,blast=false){
    const m=s.map,indices=[wall.index],tx=wall.index%D.cols,ty=Math.floor(wall.index/D.cols);
    if(blast)for(let y=ty-1;y<=ty+1;y++)for(let x=tx-1;x<=tx+1;x++)if(x>0&&y>0&&x<D.cols-1&&y<D.rows-1)indices.push(y*D.cols+x);
    let count=0;
    new Set(indices).forEach(index=>{if(m.tiles[index]!==2)return;m.tiles[index]=0;count++;const w=m.walls.get(index);w.progress=0;D.particles(s,w.x,w.y,'#adab95',blast?8:10);});
    if(!count)return;
    m.backgroundDirty=true;s.metrics.wallsBroken+=count;
    D.record(s,'wall-excavated',blast?`爆破移开 ${count} 格裂纹岩。`:'挖掉一格岩壁。',{cells:count});
    m.ores.forEach(o=>{
      if(o.buried&&!o.revealed&&D.oreExposed(m,o)){o.revealed=true;s.metrics.hiddenRevealed++;D.record(s,'ore-revealed','岩层后露出了矿。挖墙没有产矿，继续用矿镐开采。',{oreId:o.id});D.notify(s,'墙后发现矿簇！继续瞄准矿物开采。');}
    });
    m.nests.forEach(n=>{
      if(n.opened||m.tiles[n.coverIndex]!==0)return;
      n.opened=true;s.metrics.nestsOpened++;
      // 一次性创建固定数量，开口清理后不重新生成。前兆留出撤退窗口。
      m.worms.push({id:`${n.id}-worm`,x:n.x,y:n.floor,anchor:n.x,floor:n.floor,radius:n.radius,noise:0,mode:'warning',timer:1.8,target:D.clamp(s.player.x,n.x-n.radius,n.x+n.radius),cooldown:0,fromNest:true});
      D.record(s,'nest-opened','岩壁后是巢穴，里面开始蠕动。先退到开阔处！',{nestId:n.id});D.notify(s,'挖开了怪物巢穴！有动静，先退开！');
    });
  };
  D.climb=function(s,dt){
    const p=s.player,k=s.keys,dir=(k.KeyS||k.ArrowDown?1:0)-(k.KeyW||k.ArrowUp?1:0);
    if(!dir||k.KeyA||k.KeyD||k.ArrowLeft||k.ArrowRight)return false;
    const ladder=s.map.ladders.find(l=>Math.abs(p.x-l.x)<27&&(dir>0?p.y>=l.top-18&&p.y<l.bottom-.1:p.y>l.top+.1&&p.y<=l.bottom+15));
    if(!ladder)return false;
    if(s.jumpBuffer>0){p.coyote=.12;return false;}
    p.x=ladder.x;p.y=D.clamp(p.y+dir*145*dt,ladder.top,ladder.bottom);p.vx=0;p.vy=0;p.grounded=false;p.coyote=.12;
    return true;
  };
})();
