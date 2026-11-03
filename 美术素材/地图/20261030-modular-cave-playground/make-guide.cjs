const fs = require("fs");
const path = require("path");
const sharp = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const modules = [
  { id: "shelf", name: "低顶岩廊", left: "A", right: "A",
    floor: [[0,360],[96,360],[230,342],[370,380],[530,350],[672,360],[768,360]],
    roof: [[0,175],[96,175],[240,145],[355,208],[515,155],[672,175],[768,175]] },
  { id: "descent", name: "向下坡道", left: "A", right: "B",
    floor: [[0,360],[96,360],[230,371],[380,396],[530,417],[672,424],[768,424]],
    roof: [[0,175],[96,175],[245,162],[385,206],[540,220],[672,239],[768,239]] },
  { id: "ascent", name: "向上坡道", left: "B", right: "A",
    floor: [[0,424],[96,424],[235,404],[385,387],[535,363],[672,360],[768,360]],
    roof: [[0,239],[96,239],[245,200],[385,175],[535,150],[672,175],[768,175]] },
  { id: "chamber", name: "扩张矿室", left: "B", right: "B",
    floor: [[0,424],[96,424],[230,408],[390,444],[540,412],[672,424],[768,424]],
    roof: [[0,239],[96,239],[230,145],[385,115],[540,170],[672,239],[768,239]] },
  { id: "alcove", name: "矿窝岩廊", left: "A", right: "A",
    floor: [[0,360],[96,360],[235,385],[390,393],[540,365],[672,360],[768,360]],
    roof: [[0,175],[96,175],[230,155],[385,95],[540,155],[672,175],[768,175]] },
  { id: "low", name: "低顶深廊", left: "B", right: "B",
    floor: [[0,424],[96,424],[235,406],[385,433],[540,405],[672,424],[768,424]],
    roof: [[0,239],[96,239],[235,220],[385,284],[540,224],[672,239],[768,239]] }
];
const polygon = points => points.map(p => p.join(",")).join(" ");
const panels = modules.map((m,i) => {
  const x = (i%2)*768, y = Math.floor(i/2)*512;
  return `<g transform="translate(${x} ${y})">
    <rect width="768" height="512" fill="#363a3d"/>
    <polygon points="0,0 768,0 ${polygon([...m.roof].reverse())}" fill="#675d4b" stroke="#18191a" stroke-width="4"/>
    <polygon points="${polygon(m.floor)} 768,512 0,512" fill="#675d4b" stroke="#18191a" stroke-width="4"/>
    <polyline points="${polygon(m.floor)}" fill="none" stroke="#b6a079" stroke-width="13"/>
    <line x1="768" x2="768" y1="0" y2="512" stroke="#e5e5e5" stroke-width="2"/>
    <line x1="0" x2="768" y1="512" y2="512" stroke="#e5e5e5" stroke-width="2"/>
  </g>`;
}).join("");
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1536" height="1536">${panels}</svg>`;
fs.mkdirSync(path.join(__dirname,"sources"), {recursive:true});
fs.writeFileSync(path.join(__dirname,"modules.json"), JSON.stringify(modules,null,2));
sharp(Buffer.from(svg)).png().toFile(path.join(__dirname,"sources/module-layout-guide.png")).then(()=>console.log("Guide and six fixed profiles written."));
