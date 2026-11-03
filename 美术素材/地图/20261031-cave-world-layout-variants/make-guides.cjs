const fs=require("fs"),path=require("path");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const plans=JSON.parse(fs.readFileSync(path.join(__dirname,"layouts.json"),"utf8"));
(async()=>{
  for(const key of ["a","b","c"]) {
    const plan=plans[key];
    const [,entryX,entryY]=plan.nodes.find(n=>n[0]==="entry");
    const channels=plan.paths.map(d=>`<path d="${d}" stroke="#626c6c" stroke-width="27" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`).join("");
    const rooms=plan.rooms.map(([x,y,rx,ry,rotation])=>
      `<ellipse cx="${x}" cy="${y}" rx="${rx}" ry="${ry}" transform="rotate(${rotation} ${x} ${y})" fill="#626c6c"/>`).join("");
    const sites=plan.labels.map(([,id])=>{
      const [,x,y]=plan.nodes.find(n=>n[0]===id);
      return `<circle cx="${x}" cy="${y}" r="6" fill="#d8b677"/>`;
    }).join("");
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1100">
<rect width="1600" height="1100" fill="#252625"/>
<path d="M0 137 Q75 125 120 170 Q145 220 165 179 Q225 113 340 135 Q510 110 680 140 Q850 125 1010 143 Q1250 117 1410 142 Q1520 130 1600 148 L1600 1060 L0 1060 Z" fill="#504438"/>
<path d="M${entryX-28} 130 Q${entryX-8} ${entryY-30} ${entryX} ${entryY}" stroke="#626c6c" stroke-width="27" fill="none"/>
${channels}${rooms}${sites}</svg>`;
    fs.writeFileSync(path.join(__dirname,key+"-guide.svg"),svg);
    await sharp(Buffer.from(svg)).png().toFile(path.join(__dirname,key+"-guide.png"));
  }
  console.log("Saved three independent topology guides.");
})().catch(error=>{console.error(error);process.exitCode=1;});
