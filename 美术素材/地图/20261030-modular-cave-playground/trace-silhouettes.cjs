const fs=require("fs"),path=require("path"),crypto=require("crypto");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const root=__dirname,W=623,H=414;
const profiles=JSON.parse(fs.readFileSync(path.join(root,"art-profiles.json"),"utf8"));
const sample=(points,x)=>{
  for(let i=1;i<points.length;i++)if(x<=points[i][0]) {
    const [a,b]=[points[i-1],points[i]];
    return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
  }
  return points[points.length-1][1];
};
function connected(warm,limit,top) {
  const allowed=new Uint8Array(W*H),seen=new Uint8Array(W*H),queue=[];
  // Bridge the source's narrow black ink strokes before tracing warm rock.
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(warm[y*W+x]) {
    for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++) {
      if(dx*dx+dy*dy>5)continue;
      const xx=x+dx,yy=y+dy;
      if(xx<0||xx>=W||yy<0||yy>=H)continue;
      if(top?yy<limit[xx]:yy>limit[xx])allowed[yy*W+xx]=1;
    }
  }
  const edge=top?0:H-1;
  for(let x=0;x<W;x++)if(allowed[edge*W+x]){seen[edge*W+x]=1;queue.push(edge*W+x);}
  for(let i=0;i<queue.length;i++) {
    const id=queue[i],x=id%W,y=Math.floor(id/W);
    for(const [xx,yy] of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]]) {
      if(xx<0||xx>=W||yy<0||yy>=H)continue;
      const next=yy*W+xx;
      if(allowed[next]&&!seen[next]){seen[next]=1;queue.push(next);}
    }
  }
  return Array.from({length:W},(_,x)=>{
    for(let y=top?H-1:0;top?y>=0:y<H;y+=top?-1:1)if(seen[y*W+x])return top?y+1:y;
    throw Error("Missing connected foreground at column "+x);
  });
}
(async()=>{
  const source=fs.readFileSync(path.join(root,"sources/cave-modules-generated.png"));
  const result=[];
  for(let panel=0;panel<6;panel++) {
    const {data}=await sharp(source).extract({left:(panel%2)*627+2,top:Math.floor(panel/2)*418+2,
      width:W,height:H}).removeAlpha().raw().toBuffer({resolveWithObject:true});
    const warm=new Uint8Array(W*H),floorWarm=new Uint8Array(W*H);
    for(let i=0;i<warm.length;i++) {
      const [r,g,b]=data.subarray(i*3,i*3+3);
      const gold=r-g>33&&g-b>28&&b<70;
      warm[i]=r>=30&&r-g>=6&&g-b>=8&&r-b>=23&&!gold?1:0;
      floorWarm[i]=r>=25&&r-g>=4&&g-b>=4&&r-b>=14?1:0;
    }
    const limit=Array.from({length:W},(_,x)=>
      (sample(profiles[panel].roof,x)+sample(profiles[panel].floor,x))*.5);
    const floorLimit=limit.slice();
    // This source's painted ore touches the alcove rubble; author that boundary explicitly.
    const oreBoundary=[[84,233],[91,225],[100,218],[107,217],[118,222],
      [130,226],[139,230],[145,238],[153,244],[162,250]];
    if(panel===4)for(let x=84;x<=162;x++)floorLimit[x]=Math.max(floorLimit[x],sample(oreBoundary,x));
    const roof=connected(warm,limit,true),floor=connected(floorWarm,floorLimit,false);
    result.push({id:profiles[panel].id,roof,floor});
  }
  fs.writeFileSync(path.join(root,"art-silhouettes.json"),JSON.stringify({
    source:"sources/cave-modules-generated.png",
    sha256:crypto.createHash("sha256").update(source).digest("hex"),
    width:W,height:H,method:"Connected warm rock; native-pixel boundary including ink. Original source unchanged.",
    panels:result
  }));
  console.log(JSON.stringify({ok:true,panels:result.map(p=>({
    id:p.id,roof:[Math.min(...p.roof),Math.max(...p.roof)],floor:[Math.min(...p.floor),Math.max(...p.floor)]
  }))}));
})().catch(e=>{console.error(e);process.exitCode=1;});
