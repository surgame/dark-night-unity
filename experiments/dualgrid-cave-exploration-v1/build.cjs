const fs=require("fs");
const path=require("path");
const crypto=require("crypto");
const root=__dirname;
const files=["view.html","dual-grid.js","layout.js","player.js","renderer.js","state.js","app.js"];
const parts=[fs.readFileSync(path.join(root,"view.html"),"utf8")];
for (const file of files.slice(1)) parts.push("<script>\n"+fs.readFileSync(path.join(root,file),"utf8")+"\n</script>");
const fragment=parts.join("\n")+"\n";
if (Buffer.byteLength(fragment)>=1000000) throw Error("Fragment exceeds 1 MB");
const standalone=`<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>地下探索 · Dual Grid圆角白模</title><style>
:root{color-scheme:light dark;--background:light-dark(#f4f4f0,#181b1e);--foreground:light-dark(#252b2f,#e0e4e6);--muted-foreground:light-dark(#5b6368,#aab3b9);--ring:light-dark(#3c505c,#adc3ce)}
body{margin:0;padding:28px 24px;background:var(--background);color:var(--foreground);font:14px/1.5 "Trebuchet MS","Microsoft YaHei",sans-serif}
main{max-width:1180px;margin:auto}.btn,.form-select{color:var(--foreground);background:var(--background);border:1px solid color-mix(in srgb,var(--foreground) 22%,var(--background));border-radius:6px;padding:7px 11px;font:inherit}
.btn{cursor:pointer}.btn[aria-pressed=true]{background:var(--foreground);color:var(--background)}.form-select{padding:5px 8px}
input{accent-color:var(--foreground)}summary{cursor:pointer}@media(max-width:600px){body{padding:16px 12px}.btn{min-height:40px}}
</style></head><body><main>${fragment}</main></body></html>`;
fs.writeFileSync(path.join(root,"index.html"),standalone);
const destination=process.argv[2];
if (destination) {
  fs.mkdirSync(path.dirname(path.resolve(destination)),{recursive:true});
  fs.writeFileSync(destination,fragment);
}
const hash=bytes=>crypto.createHash("sha256").update(bytes).digest("hex");
const stamp=new Date().toISOString().replace(/[:.]/g,"-");
const record={date:"2026-11-01",scope:"Browser whitebox only; no Unity or production changes.",
  standalone:{file:"index.html",bytes:Buffer.byteLength(standalone),sha256:hash(standalone)},
  fragment:destination?{file:path.resolve(destination),bytes:Buffer.byteLength(fragment),sha256:hash(fragment)}:null,
  sources:[...files,"build.cjs"].map(file=>({file,sha256:hash(fs.readFileSync(path.join(root,file)))}))};
fs.mkdirSync(path.join(root,"records"),{recursive:true});
const recordPath=path.join(root,"records",stamp+"-build.json");
fs.writeFileSync(recordPath,JSON.stringify(record,null,2)+"\n");
console.log(JSON.stringify({standalone:path.join(root,"index.html"),fragment:destination||null,
  bytes:record.standalone.bytes,record:recordPath}));
