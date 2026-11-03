(function (C) {
  "use strict";
  const radius=.38,halfLine=.59,halfHeight=radius+halfLine;
  const shape=[];
  for (const cy of [-halfLine,halfLine]) for (let i=0;i<16;i++) {
    const a=i*Math.PI/8;shape.push([Math.cos(a)*radius,cy+Math.sin(a)*radius]);
  }
  shape.push([0,0],[-radius,0],[radius,0]);
  function collides(world,x,y) { return shape.some(p=>C.solid(world,x+p[0],y+p[1])); }
  C.playerCollides=collides;
  C.makePlayer=function (world,saved) {
    const p={x:world.spawn.x,y:world.spawn.y,vx:0,vy:0,facing:1,
      grounded:false,coyote:0,jumpBuffer:0,mining:0};
    if (saved && Number.isFinite(saved.x) && Number.isFinite(saved.y) &&
      !collides(world,saved.x,saved.y)) {
      p.x=saved.x;p.y=saved.y;p.facing=saved.facing===-1?-1:1;
    }
    for (let i=0;i<40 && collides(world,p.x,p.y);i++) p.y-=.1;
    return p;
  };
  function moveVertical(world,p,amount) {
    if (!collides(world,p.x,p.y+amount)) { p.y+=amount;return false; }
    let lo=0,hi=1;
    for (let i=0;i<12;i++) {
      const t=(lo+hi)/2;
      if (collides(world,p.x,p.y+amount*t)) hi=t;else lo=t;
    }
    p.y+=amount*lo;
    return true;
  }
  C.stepPlayer=function (world,p,input,dt) {
    p.mining=Math.max(0,p.mining-dt);
    p.jumpBuffer=Math.max(0,p.jumpBuffer-dt);
    p.coyote=p.grounded?.1:Math.max(0,p.coyote-dt);
    const axis=(input.right?1:0)-(input.left?1:0);
    const target=axis*6.6,acceleration=p.grounded?48:25;
    p.vx+=C.clamp(target-p.vx,-acceleration*dt,acceleration*dt);
    if (axis) p.facing=axis;
    if (p.jumpBuffer>0 && p.coyote>0) {
      p.vy=-10.3;p.grounded=false;p.coyote=0;p.jumpBuffer=0;
    }
    const dx=p.vx*dt,wasGrounded=p.grounded;
    if (!collides(world,p.x+dx,p.y)) p.x+=dx;
    else {
      let stepped=false;
      if (wasGrounded && p.vy>=0) {
        for (let rise=.06;rise<=.62;rise+=.06) if (!collides(world,p.x+dx,p.y-rise)) {
          p.x+=dx;p.y-=rise;stepped=true;break;
        }
      }
      if (!stepped) p.vx=0;
    }
    p.vy=Math.min(18,p.vy+25*dt);
    const dy=p.vy*dt,hit=moveVertical(world,p,dy);
    p.grounded=hit && dy>0;
    if (hit) p.vy=0;
    if (!p.grounded && wasGrounded && p.vy>=0 && p.vy<3) {
      const original=p.y;
      if (moveVertical(world,p,.45)) {p.grounded=true;p.vy=0;}
      else p.y=original;
    }
    p.x=C.clamp(p.x,1,world.width-1);
    p.y=C.clamp(p.y,1,world.height-1);
  };
  C.playerShape={radius,halfLine,halfHeight};
})(globalThis.CaveWhitebox);
