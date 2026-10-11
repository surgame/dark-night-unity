import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const task=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const workspace=path.resolve(task,'../..');
const sourceWorkspace='D:/Developer/MiniGames/Dark Nights/unity-projects';
const source=path.join(sourceWorkspace,'experiments/mine-level-h5');
const destination=path.join(task,'references/h5-v0.4');
const digest=p=>createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase();
function inside(p){const relative=path.relative(task,p);if(relative.startsWith('..')||path.isAbsolute(relative))throw Error('Outside task root: '+p);}
if(fs.existsSync(destination))throw Error('Snapshot already exists; do not replace.');
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const inputs=files(path.join(source,'src')).filter(f=>/\.(js|css|html|md)$/.test(f)).map(f=>path.relative(source,f));
inputs.push(...fs.readdirSync(source).filter(f=>/\.(mjs|py|md)$/.test(f)),'dev.html','dist/index.html');
const records=[];
function copy(from,to,kind){
  inside(to);if(fs.existsSync(to))throw Error('Refuse overwrite: '+to);
  const before=digest(from);fs.mkdirSync(path.dirname(to),{recursive:true});fs.copyFileSync(from,to);
  const after=digest(from),saved=digest(to);if(before!==after||before!==saved)throw Error('Source changed or copy differs: '+from);
  records.push({kind,source:from,snapshot:path.relative(task,to).replaceAll('\\','/'),bytes:fs.statSync(to).size,sha256:saved});
}
for(const relative of inputs)copy(path.join(source,relative),path.join(destination,relative),'h5-v0.4');
const originalHtml=path.join(destination,'dist/index.html');
if(digest(originalHtml)!=='BD10A104CA29C43FDCE35532F74AC6EB5CC43CA1D8673C5928202FFFD186DE14')throw Error('Unexpected v0.4 candidate; review baseline.');
copy('D:/Downloads/dark_nights_mine_gameplay_direction_v0.1.html',path.join(task,'references/玩法方向文档.html'),'design-reference');
for(const name of ['verification.json','concept-final.json','ui-wiring-final.json','natural-compatibility-final.json'])
  copy(path.join(sourceWorkspace,'artifacts/mine-concept-pack-20261011',name),path.join(task,'references/evidence',name),'historical-v0.4-evidence');
const git=(cwd,args)=>execFileSync('git',args,{cwd,encoding:'utf8'}).trim();
const report={createdAt:new Date().toISOString(),scope:'Development plan and read-only baseline; no enlarged game implementation or gameplay/Unity tests.',
  workspace,branch:git(workspace,['branch','--show-current']),worktreeHead:git(workspace,['rev-parse','HEAD']),
  sourceWorkspace,sourceBranch:git(sourceWorkspace,['branch','--show-current']),sourceHead:git(sourceWorkspace,['rev-parse','HEAD']),
  sourceStatus:git(sourceWorkspace,['status','--short']),sourceH5Version:'mine-concept-pack-v0.4',
  note:'H5 sources were uncommitted in the original workspace; file SHA256 identifies the input, not the new worktree commit.',
  taskRoot:task,files:records,sourceProtectedDocuments:['docs/DEVELOPMENT.md','experiments/README.md'].map(f=>({path:path.join(sourceWorkspace,f),sha256:digest(path.join(sourceWorkspace,f))}))};
const manifest=path.join(task,'references/baseline-manifest.json');fs.writeFileSync(manifest,JSON.stringify(report,null,2));
console.log(JSON.stringify({branch:report.branch,files:records.length,bytes:records.reduce((n,r)=>n+r.bytes,0),manifest,originalPreserved:true}));
