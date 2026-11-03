const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
(async()=>{
  const root=path.resolve(__dirname,"../.."),record=path.join(root,"artifacts/vault-svg-styles-20261102");
  const manifest=JSON.parse(fs.readFileSync(path.join(record,"design-inputs.json"),"utf8"));
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const outputs=[];
  try{
    const page=await browser.newPage({viewport:{width:1920,height:1200},deviceScaleFactor:1});
    for(const item of manifest.outputs){
      const svg=fs.readFileSync(item.svg,"utf8");
      await page.setContent('<style>html,body{margin:0;background:transparent}svg{display:block}</style>'+svg);
      await page.locator("svg").screenshot({path:item.png,omitBackground:true});
      const bytes=fs.readFileSync(item.png);
      outputs.push({...item,bytes:bytes.length,pngSha256:crypto.createHash("sha256").update(bytes).digest("hex")});
    }
  }finally{await browser.close();}
  fs.writeFileSync(path.join(record,"raster-result.json"),JSON.stringify({ok:true,outputs},null,2)+"\n");
  console.log(JSON.stringify({ok:true,outputs:outputs.length,bytes:outputs.reduce((n,x)=>n+x.bytes,0)}));
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
