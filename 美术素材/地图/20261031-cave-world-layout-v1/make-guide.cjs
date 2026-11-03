const fs=require("fs"),path=require("path");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const plan=JSON.parse(fs.readFileSync(path.join(__dirname,"layout.json"),"utf8"));
const paths=[plan.mainGuide,...plan.branchGuides].map(d=>
  `<path d="${d}" stroke="#626c6c" stroke-width="33" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`).join("");
const rooms=plan.roomGuides.map(r=>
  `<ellipse cx="${r.center[0]}" cy="${r.center[1]}" rx="${r.radii[0]}" ry="${r.radii[1]}" fill="#626c6c"/>`).join("");
const dots=plan.nodes.map(n=>`<circle cx="${n.center[0]}" cy="${n.center[1]}" r="5" fill="#e1c38c"/>`).join("");
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1000" viewBox="0 0 1600 1000">
<rect width="1600" height="1000" fill="#242525"/>
<path d="M0 150 Q55 115 100 155 Q130 200 140 205 Q165 185 200 139 Q265 100 350 136 Q455 115 560 142 Q715 106 840 136 Q995 111 1120 139 Q1280 112 1440 143 Q1520 135 1600 151 L1600 925 L0 925 Z" fill="#504438"/>
${paths}${rooms}${dots}
<path d="${plan.mainGuide}" stroke="#dbc79e" stroke-width="2" stroke-dasharray="7 7" fill="none"/>
</svg>`;
fs.writeFileSync(path.join(__dirname,"layout-guide.svg"),svg);
sharp(Buffer.from(svg)).png().toFile(path.join(__dirname,"layout-guide.png"))
  .then(()=>console.log("Saved deterministic side-view topology guide."))
  .catch(error=>{console.error(error);process.exitCode=1;});
