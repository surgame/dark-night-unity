// Offline visual proof only. Does not run Unity or certify runtime integration.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {chromium}=require(process.env.DN_PLAYWRIGHT || 'C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
  const browser=await chromium.launch({headless:true,channel:'chrome'}),checks=[],errors=[];
  const check=(name,ok)=>{checks.push({name,passed:ok});if(!ok)throw Error(name);};
  try{
    const page=await browser.newPage({viewport:{width:960,height:1100}});page.on('pageerror',e=>errors.push(String(e)));
    await page.goto('file:///'+path.resolve(process.argv[2]).replaceAll('\\','/'));
    const f=page.frameLocator('iframe');await f.locator('#dn-grid-ore[data-ready="true"]').waitFor();
    const hash=async()=>crypto.createHash('sha256').update(await f.locator('#dg-paint').evaluate(c=>c.toDataURL())).digest('hex');
    const hashes=[];
    for(let ore=0;ore<5;ore++){await f.locator('#dg-ore').selectOption(String(ore));hashes.push(await hash());}
    check('五矿画面各不相同',new Set(hashes).size===5);
    const depth=[];for(let i=0;i<3;i++){await f.locator('#dg-depth').selectOption(String(i));depth.push(await hash());}
    check('三种深度色样不同',new Set(depth).size===3);
    await f.locator('#dg-depth').selectOption('0');await f.locator('#dg-ore').selectOption('0');
    const before=await hash(),status=await f.locator('#dg-status').textContent();
    const bounds=await f.locator('#dg-paint').boundingBox();
    await f.locator('#dg-paint').click({position:{x:bounds.width*16/184,y:bounds.height*16/120}});
    check('按格添加改变占用数量',(await f.locator('#dg-status').textContent())!==status);
    check('添加后连接图变化',(await hash())!==before);
    await f.locator('#dg-brush').selectOption('0');
    await f.locator('#dg-paint').click({position:{x:bounds.width*16/184,y:bounds.height*16/120}});
    check('擦除恢复原始矿格',(await hash())===before);
    await f.locator('#dg-brush').selectOption('1');await f.locator('#dg-lines').uncheck();
    check('格线可关闭',(await hash())!==before);
    await page.screenshot({path:path.join(__dirname,'preview','desktop.png'),fullPage:true});
    await page.setViewportSize({width:360,height:950});
    check('窄屏无横向溢出',await f.locator('body').evaluate(b=>b.scrollWidth<=window.innerWidth));
    await page.screenshot({path:path.join(__dirname,'preview','mobile.png'),fullPage:true});
    check('无脚本错误',errors.length===0);
  }finally{
    await browser.close();fs.writeFileSync(path.join(__dirname,'browser-verification.json'),JSON.stringify({checks,errors},null,2));
    console.log(JSON.stringify({passed:checks.filter(c=>c.passed).length,total:checks.length,errors}));
  }
})().catch(e=>{console.error(e);process.exitCode=1;});
