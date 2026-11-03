const fs = require("fs");
const path = require("path");
const sharp = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp");
const config = JSON.parse(fs.readFileSync(path.join(__dirname, "modules.json"), "utf8"));
const W = config.world.width, H = config.world.height;
const points = values => values.map(p => p.join(",")).join(" ");
const polygon = values => `<polygon points="${points(values)}" fill="#66513b"/>`;

(async () => {
  fs.mkdirSync(path.join(__dirname, "guides"), {recursive:true});
  for (const m of config.modules) {
    const closed = m.ports.length === 1;
    const air = [...m.roof, ...m.floor.slice().reverse()];
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1536" height="1024" viewBox="0 0 ${W} ${H}">
      <rect width="${W}" height="${H}" fill="#66513b"/>
      <polygon points="${points(air)}" fill="#283237"/>
      ${(m.solids || []).map(polygon).join("")}
      ${m.ports.map(p => {
        const x = p.side === "left" ? 0 : W - 64;
        return `<rect x="${x}" y="${p.floor - 128}" width="64" height="128" fill="#283237"/>`;
      }).join("")}
    </svg>`;
    fs.writeFileSync(path.join(__dirname, "guides", m.id + ".svg"), svg);
    await sharp(Buffer.from(svg)).png().toFile(path.join(__dirname, "guides", m.id + ".png"));
  }
  console.log(JSON.stringify({guides:config.modules.length, size:[1536,1024]}));
})().catch(error => {console.error(error);process.exitCode=1;});
