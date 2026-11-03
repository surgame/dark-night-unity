(function(root) {
  "use strict";
  const api=root.FlatCave,W=1600,H=900,CELL=32;
  const ceiling=[[0,135],[150,110],[310,170],[490,188],[640,225],[790,290],[920,370],[995,465],[1024,544]];
  const floor=[[0,585],[160,598],[360,625],[550,650],[760,682],[930,704],[1024,704]];
  const air=new Path2D();
  ceiling.forEach((p,i)=>i?air.lineTo(...p):air.moveTo(...p));
  [...floor].reverse().forEach(p=>air.lineTo(...p));air.closePath();
  const solid=new Path2D();solid.rect(0,0,W,H);solid.addPath(air);
  const edges=[];
  for(const [points,direction] of [[floor,-1],[ceiling,1]])for(let i=1;i<points.length;i++){
    const a={x:points[i-1][0],y:points[i-1][1]},b={x:points[i][0],y:points[i][1]};
    const len=Math.hypot(b.x-a.x,b.y-a.y);
    edges.push({a,b,nx:direction*(a.y-b.y)/len,ny:direction*(b.x-a.x)/len});
  }
  const visual={solid,outline:air,edges};
  function cellsFor(stage) {
    const cells=[];
    if(stage>=1)for(let row=0;row<4;row++)cells.push([32,18+row]);
    if(stage>=2)for(let col=33;col<=35;col++)for(let row=0;row<4;row++)
      if(!(col===35&&row===0))cells.push([col,18+row]);
    return cells;
  }
  function cutPath(stage) {
    const path=new Path2D();cellsFor(stage).forEach(([x,y])=>path.rect(x*CELL,y*CELL,CELL,CELL));
    return path;
  }
  function cutOutline(stage) {
    const cells=cellsFor(stage),set=new Set(cells.map(c=>c.join(","))),path=new Path2D();
    for(const [x,y] of cells) {
      if(!set.has([x,y-1].join(","))){path.moveTo(x*CELL,y*CELL);path.lineTo((x+1)*CELL,y*CELL);}
      if(!set.has([x+1,y].join(","))){path.moveTo((x+1)*CELL,y*CELL);path.lineTo((x+1)*CELL,(y+1)*CELL);}
      if(!set.has([x,y+1].join(","))){path.moveTo(x*CELL,(y+1)*CELL);path.lineTo((x+1)*CELL,(y+1)*CELL);}
      // Leftmost edge is the existing open wall, not a new opaque rim.
      if(x>32&&!set.has([x-1,y].join(","))){path.moveTo(x*CELL,y*CELL);path.lineTo(x*CELL,(y+1)*CELL);}
    }
    return path;
  }
  function fillForeground(ctx,art,stage) {
    ctx.fillStyle=art.pattern;ctx.fill(solid,"evenodd");
    api.surfaceBands(ctx,art,visual,{x:0,y:0,w:W,h:H});
    if(stage>0) {
      ctx.save();ctx.globalCompositeOperation="destination-out";ctx.fill(cutPath(stage));ctx.restore();
      ctx.strokeStyle="#272b28";ctx.lineWidth=4;ctx.stroke(cutOutline(stage));
    }
  }
  function render(art,stage=0,lit=false,actors=true) {
    const c=api.canvas(W,H),ctx=c.getContext("2d"),fg=api.canvas(W,H),fc=fg.getContext("2d");
    api.backdrop(ctx,art,{x:0,y:0,w:W,h:H});
    // This backwall is fixed in the original solid region; digging never reveals sky.
    ctx.save();ctx.clip(solid,"evenodd");ctx.fillStyle="#354953";ctx.fillRect(0,0,W,H);
    ctx.globalAlpha=.12;ctx.fillStyle=art.pattern;ctx.fillRect(0,0,W,H);
    ctx.globalAlpha=1;
    api.ore(ctx,art,1035,651,83,2);api.ore(ctx,art,1100,650,110,0);api.ore(ctx,art,1175,653,92,3);
    ctx.restore();
    fillForeground(fc,art,stage);
    ctx.drawImage(fg,0,0);
    // Small initial outcrop remains local and does not disclose the hidden body.
    if(stage===0)api.ore(ctx,art,1020,653,33,1);
    if(stage>0) {
      ctx.save();ctx.clip(cutPath(stage));ctx.strokeStyle="#9a8b6e";ctx.lineWidth=4;ctx.stroke(cutOutline(stage));ctx.restore();
      ctx.fillStyle="#a3977c";
      for(let i=0;i<7;i++){const x=980+i*18,y=704;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+5,y-5-(i%3)*2);ctx.lineTo(x+12,y);ctx.fill();}
    }
    if(lit) {
      const shade=api.canvas(W,H),sc=shade.getContext("2d");
      sc.fillStyle="rgba(8,17,25,.72)";sc.fillRect(0,0,W,H);
      sc.globalCompositeOperation="destination-out";
      const g=sc.createRadialGradient(1040,650,30,1040,650,480);
      g.addColorStop(0,"rgba(0,0,0,.95)");g.addColorStop(.55,"rgba(0,0,0,.7)");g.addColorStop(1,"transparent");
      sc.fillStyle=g;sc.fillRect(0,0,W,H);ctx.drawImage(shade,0,0);
    }
    if(actors) {
      const x=stage===2?1068:950;
      api.hero(ctx,art,x,704,92);api.hero(ctx,art,725,677,92);
      // Tool silhouettes are visual props only, without changing the source hero.
      ctx.strokeStyle="#2b302c";ctx.lineWidth=5;ctx.beginPath();ctx.moveTo(x+20,662);ctx.lineTo(x+57,623);ctx.stroke();
      ctx.strokeStyle="#b7b4a5";ctx.lineWidth=7;ctx.beginPath();ctx.moveTo(x+39,613);ctx.quadraticCurveTo(x+60,612,x+72,640);ctx.stroke();
      if(lit) {
        ctx.fillStyle="#cec7ab";ctx.fillRect(748,636,15,8);
        ctx.save();ctx.clip(air);ctx.beginPath();ctx.moveTo(763,640);ctx.lineTo(1140,578);ctx.lineTo(1140,726);ctx.closePath();
        const beam=ctx.createLinearGradient(763,640,1140,640);beam.addColorStop(0,"rgba(245,214,147,.18)");beam.addColorStop(1,"rgba(245,214,147,.02)");
        ctx.fillStyle=beam;ctx.fill();ctx.restore();
      }
    }
    return {canvas:c,foreground:fg,cells:cellsFor(stage),bounds:{width:W,height:H,cell: CELL}};
  }
  api.mining={render,cellsFor,cutPath,solid,air,W,H,CELL};
})(globalThis);
