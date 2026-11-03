const fs=require("fs"),path=require("path"),assert=require("assert");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,preview="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a18896-3db9-7340-b606-b6184a07f49c";
function ratio(a,b,c,d) {
  const s=a*a+b*b+c*c+d*d,det=a*d-b*c,delta=Math.sqrt(Math.max(0,s*s-4*det*det));
  return Math.sqrt((s+delta)/(s-delta));
}
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try {
    const page=await browser.newPage({viewport:{width:1056,height:1000}}),errors=[];
    page.on("pageerror",e=>errors.push(e.message));await page.goto("file:///"+preview+"/flat-wfc-browser.html");
    const frame=page.frames()[1];await frame.waitForFunction(()=>document.getElementById("flat-cave-kit")?.dataset.ready==="true");
    const sources=JSON.parse(fs.readFileSync(path.join(root,"sources-manifest.json"),"utf8"));
    const urls=Object.fromEntries(sources.map(s=>[s.key,"data:image/png;base64,"+fs.readFileSync(path.join(root,s.path)).toString("base64")]));
    const legacy=["surfaces.js","materials.js"].map(name=>fs.readFileSync(path.join(root,"revisions/column-shear-v2/renderer",name),"utf8")).join("\n");
    const rendered=await frame.evaluate(async({urls,legacy})=>{
      const images={};await Promise.all(Object.entries(urls).map(async([k,u])=>{const im=new Image();im.src=u;await im.decode();images[k]=im;}));
      const a=FlatCave,map=await document.getElementById("flat-cave-kit")._art.ensure(271),node=map.plan.nodes[3];
      const box={x:node.x*a.U-768,y:node.y*a.U-400,w:1536,h:864},c=a.canvas(1536,864);
      const current={...a};
      const prepared=a.prepare(images),alphaChecks={};
      for(const [name,texture] of Object.entries({rock:prepared.rock,floor:prepared.surfaces.floor,ceiling:prepared.surfaces.ceiling})){
        const pixels=texture.getContext("2d").getImageData(0,0,texture.width,texture.height).data;
        let intermediate=0;for(let i=3;i<pixels.length;i+=4)if(pixels[i]!==0&&pixels[i]!==255)intermediate++;
        alphaChecks[name]={intermediateAlphaPixels:intermediate};
      }
      a.draw(c.getContext("2d"),prepared,map,box,1536,864,{hero:true,room:3});const after=c.toDataURL();
      Function(legacy)();
      a.draw(c.getContext("2d"),a.prepare(images),map,box,1536,864,{hero:true,room:3});const before=c.toDataURL();
      Object.assign(a,current);
      const straight=[];
      for(const angle of [0,15,30,45,60]) {
        const radians=angle*Math.PI/180,dx=400*Math.cos(radians),dy=400*Math.sin(radians);
        const run=a.surfaceRuns([{a:{x:0,y:0},b:{x:dx,y:dy},nx:Math.sin(radians),ny:-Math.cos(radians)}])[0];
        const f=a.surfaceFrameAt(run,200);
        straight.push({angle,tangent:[f.tx,f.ty],normal:[f.nx,f.ny],scale:a.surfaceScale});
      }
      return {before,after,straight,alphaChecks};
    },{urls,legacy});
    fs.mkdirSync(path.join(root,"review"),{recursive:true});
    for(const key of ["before","after"])fs.writeFileSync(path.join(root,"review","mapping-"+key+".png"),Buffer.from(rendered[key].split(",")[1],"base64"));
    const measurements=rendered.straight.map(f=>{
      const t=f.tangent,n=f.normal,scale=f.scale;
      const measured={angle:f.angle,oldAxisRatio:ratio(.4,Math.tan(f.angle*Math.PI/180)*.4,0,.4),
        newAxisRatio:ratio(t[0]*scale,t[1]*scale,n[0]*scale,n[1]*scale),
        orthogonalDot:t[0]*n[0]+t[1]*n[1],scaleX:Math.hypot(...t)*scale,scaleY:Math.hypot(...n)*scale};
      assert(Math.abs(measured.newAxisRatio-1)<1e-6);assert(Math.abs(measured.orthogonalDot)<1e-9);
      return measured;
    });
    assert.equal(errors.length,0);
    for(const item of Object.values(rendered.alphaChecks))assert.equal(item.intermediateAlphaPixels,0);
    const report={ok:true,measurements,alphaChecks:rendered.alphaChecks,scope:"Straight-slope local scale and orthogonality; native same-camera before/after images. Curved ribbons still follow the contour; no Unity claim.",errors};
    fs.writeFileSync(path.join(root,"mapping-verification.json"),JSON.stringify(report,null,2));
    console.log(JSON.stringify(report));
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
