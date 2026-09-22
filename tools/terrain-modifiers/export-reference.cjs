// Frozen H5 algorithms execute directly in Node; output hashes are reference inputs, never generated from C#.
const fs=require('fs'),crypto=require('crypto'),path=require('path');
const down=require('./reference/downward-edges.js'),round=require('./reference/rounded-clusters.js');
const root=path.resolve(__dirname,'../..'),sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const input=fs.readFileSync(path.join(root,'tools/contour-reference/outline-hybridB.bin'));
const flat=new Uint8Array(1024*384);
for(let y=0;y<384;y++)for(let x=0;x<1024;x++)flat[y*1024+x]=y<100+Math.floor(4*Math.sin(x/37))||y>280?1:0;
const cases=[];
for(const [name,mask,w,h,top] of [['profile',input,504,312,56],['wide',flat,1024,384,0]]) {
  for(const [mode,cfg] of [
    ['downward',{length:7,density:100,width:5,sharpness:0,variation:89}],
    ['downward',{length:4,density:45,width:11,sharpness:15,variation:70}],
    ['downward',{length:24,density:100,width:19,sharpness:100,variation:100}],
    ['rounded',{depth:8,size:20,petal:3,density:90,variation:65}],
    ['rounded',{depth:3,size:13,petal:2,density:75,variation:60}],
    ['rounded',{depth:12,size:30,petal:6,density:100,variation:100}]]) {
    const seed=1938411237,result=mode==='downward'?down.extend(mask,w,h,seed,cfg,top):round.build(mask,w,h,seed,cfg,top);
    cases.push({source:name,width:w,height:h,top,seed,mode,cfg,sha256:sha(result.mask),added:result.added.reduce((a,b)=>a+b,0)});
  }
}
fs.writeFileSync(path.join(__dirname,'reference/golden.json'),JSON.stringify({source:'Original H5 JS retained byte-for-byte',
  scripts:['downward-edges.js','rounded-clusters.js'].map(f=>({file:f,sha256:sha(fs.readFileSync(path.join(__dirname,'reference',f)))})),cases},null,2)+'\n');
console.log('H5 modifier reference cases: '+cases.length);
