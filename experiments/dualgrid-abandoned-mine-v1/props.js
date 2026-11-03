(function (C) {
  "use strict";
  C.drawMineProps=function (ctx,world,state,palette) {
    const plan=C.minePlan;
    const supported=(x,y)=>C.solid(world,x,y+.13);
    function line(points,width=.14) {
      ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(points[0][0],points[0][1]);
      for (const p of points.slice(1)) ctx.lineTo(p[0],p[1]);
      ctx.stroke();
    }
    ctx.save();ctx.strokeStyle=palette.edge;ctx.fillStyle=palette.edge;
    ctx.lineCap="round";ctx.lineJoin="round";ctx.globalAlpha=.68;
    for (const rail of plan.rails) {
      for (let x=rail.from;x<rail.to;x+=1.5) {
        if (rail.gaps.some(g=>x>=g[0]&&x<=g[1])) continue;
        if (!supported(x,rail.floor)||!supported(x+1.45,rail.floor)) continue;
        line([[x,rail.floor-.14],[Math.min(x+1.5,rail.to),rail.floor-.14]],.11);
        line([[x+.2,rail.floor-.34],[x+.2,rail.floor]],.16);
      }
      for (const gap of rail.gaps) if (supported(gap[0]-.4,rail.floor))
        line([[gap[0]-1.3,rail.floor-.14],[gap[0]-.3,rail.floor-.65]],.11);
    }
    plan.supports.forEach(([x,floor,height],index)=>{
      if (!supported(x-3.1,floor)||!supported(x+3.1,floor)) return;
      const top=floor-height,broken=index%5===2;
      line([[x-3.1,floor],[x-3.1,top],[x+3.1,top]],.24);
      if (broken) {
        line([[x+3.1,top],[x+3.1,top+height*.36]],.24);
        line([[x+3.1,floor-1.3],[x+2.6,floor]],.24);
        line([[x+.6,floor-.1],[x+2.5,floor-.75]],.2);
      } else line([[x+3.1,top],[x+3.1,floor]],.24);
      line([[x-3.1,top+1.7],[x-1.3,top]],.17);
      line([[x+1.3,top],[x+3.1,top+1.7]],.17);
      ctx.beginPath();ctx.arc(x-3.1,top,.17,0,Math.PI*2);ctx.fill();
    });
    for (const [x,floor] of plan.carts) {
      if (!supported(x-1.5,floor)||!supported(x+1.5,floor)) continue;
      ctx.fillStyle=palette.rock;ctx.strokeStyle=palette.edge;ctx.lineWidth=.16;
      ctx.beginPath();ctx.moveTo(x-2.2,floor-3);ctx.lineTo(x+2.2,floor-3);
      ctx.lineTo(x+1.6,floor-.8);ctx.lineTo(x-1.6,floor-.8);ctx.closePath();
      ctx.fill();ctx.stroke();
      for (const side of [-1,1]) {
        ctx.beginPath();ctx.arc(x+side*1.25,floor-.4,.4,0,Math.PI*2);ctx.fill();ctx.stroke();
      }
      line([[x-1.4,floor-2.1],[x+1.4,floor-2.1]],.09);
    }
    for (const [l,t,r,b] of plan.barriers) {
      if (!C.solid(world,(l+r)/2,(t+b)/2)) continue;
      line([[l-.3,t+.6],[r-.3,b-1]],.28);
      line([[r-.3,t+.6],[l-.3,b-1]],.28);
      line([[l-.4,t+1],[l-.4,b-1]],.15);
    }
    const hoist=plan.hoist;
    line([[hoist.x-5.5,hoist.top-.8],[hoist.x+5.5,hoist.top-.8]],.27);
    ctx.beginPath();ctx.arc(hoist.x,hoist.top-1.2,.75,0,Math.PI*2);ctx.stroke();
    line([[hoist.x,hoist.top-.5],[hoist.x,hoist.bottom-4.5]],.1);
    for (const x of [hoist.x-4.3,hoist.x+4.3])
      line([[x,hoist.top],[x,hoist.bottom]],.08);
    if (supported(hoist.x,hoist.bottom)) {
      const y=hoist.bottom;
      ctx.fillStyle=palette.rock;ctx.lineWidth=.18;
      ctx.beginPath();ctx.rect(hoist.x-2.3,y-4.5,4.6,4);ctx.fill();ctx.stroke();
      line([[hoist.x-2.3,y-1.4],[hoist.x+2.3,y-1.4]],.1);
      line([[hoist.x-2.3,y-4.5],[hoist.x+2.3,y-.5]],.1);
    }
    const shaft=plan.serviceShaft;
    line([[shaft.left+.1,64],[shaft.left+.1,shaft.bottom]],.08);
    line([[shaft.right-.1,64],[shaft.right-.1,shaft.bottom]],.08);
    // A simple pump base and pipe are non-colliding background structure marks.
    if (supported(123,64.5)) {
      ctx.fillStyle=palette.rock;ctx.lineWidth=.17;
      ctx.beginPath();ctx.roundRect(120,61.8,6,2.2,.25);ctx.fill();ctx.stroke();
      ctx.beginPath();ctx.arc(123,61.6,1.4,0,Math.PI*2);ctx.fill();ctx.stroke();
      line([[124.4,61.6],[130,61.6],[130,48.2],[137,48.2]],.2);
    }
    ctx.restore();
  };
})(globalThis.CaveWhitebox);
