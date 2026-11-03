const fs=require("fs"),path=require("path");
const sharp=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const p=JSON.parse(fs.readFileSync(path.join(__dirname,"room-layout.json"),"utf8"));
const channels=p.connections.map(c=>`<path d="${c.path}" fill="none" stroke="#626c6c" stroke-width="${c.width}" stroke-linecap="round"/>`).join("");
const rooms=p.airPaths.map(r=>`<path d="${r.path}" fill="#626c6c"/>`).join("");
const solids=p.solidDetails.map(d=>`<path d="${d}" fill="#504438"/>`).join("");
const people=p.scaleMarkers.map(m=>`<path d="M${m.x-4} ${m.footY} v-${m.height-6} h8 v${m.height-6}" stroke="#8dd2d7" stroke-width="3" fill="#8dd2d7"/><circle cx="${m.x}" cy="${m.footY-m.height+2}" r="4" fill="#8dd2d7"/>`).join("");
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="1200">
<rect width="1800" height="1200" fill="#252625"/>
<path d="M0 139 Q65 115 105 150 Q140 170 188 125 Q380 87 550 135 Q770 110 970 140 Q1260 110 1460 140 Q1670 113 1800 145 L1800 1135 L0 1135 Z" fill="#504438"/>
${channels}${rooms}${solids}${people}</svg>`;
fs.writeFileSync(path.join(__dirname,"shape-guide.svg"),svg);
sharp(Buffer.from(svg)).png().toFile(path.join(__dirname,"shape-guide.png"))
  .then(()=>console.log("Saved heterogeneous chamber-shape guide with fixed character scale."))
  .catch(error=>{console.error(error);process.exitCode=1;});
