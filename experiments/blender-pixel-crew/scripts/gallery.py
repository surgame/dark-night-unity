"""生成离线可交互精灵表预览和演示 GIF；所有显示帧都来自实际 Blender 导出。"""

import argparse
import base64
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


HTML = r'''<!doctype html>
<html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Pixel Crew · 技术样机</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#202630;color:#eadfc9;
font-family:system-ui,"Microsoft YaHei",sans-serif}main{max-width:1040px;margin:auto;padding:36px 28px}
header{display:flex;justify-content:space-between;gap:20px;align-items:center}
h1{font-size:26px;letter-spacing:.03em;margin:0 0 8px}p{line-height:1.7;color:#b3b5b7;font-size:14px}
.flag{border:1px solid #bd8468;color:#ebc09a;padding:8px 12px;font-size:12px;border-radius:5px}
.layout{display:grid;grid-template-columns:minmax(360px,1fr) 300px;gap:22px;margin-top:24px}
.stage{border:1px solid #49505b;border-radius:12px;background:#191f28;display:grid;place-items:center;
min-height:440px;padding:20px}.side{background:#2b323d;border:1px solid #49505b;border-radius:12px;padding:22px}
label{display:block;color:#c5c4bc;font-size:13px;margin:0 0 7px}
select,input[type=range]{width:100%;margin-bottom:20px}select,button{
font:inherit;font-size:14px;color:#ece2cd;background:#39424e;border:1px solid #626a72;border-radius:5px;padding:9px}
button{cursor:pointer}button:hover{background:#4c5865}select:focus,button:focus{outline:2px solid #d7aa70}
.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.buttons{display:flex;gap:10px;margin:8px 0 18px}
canvas{image-rendering:pixelated;max-width:100%;height:auto}.readout{font:12px/1.9 Consolas,monospace;color:#aab9bf}
.toggles label{display:inline-block;margin-right:12px}footer{border-top:1px solid #444c57;margin-top:28px;padding-top:18px}
@media(max-width:780px){.layout{grid-template-columns:1fr}header{display:block}.flag{display:inline-block;margin-top:12px}}
</style>
<main><header><div><h1>PIXEL CREW</h1><p>共用骨架 · 模块化穿搭 · 固定视角序列帧试验</p></div>
<span class="flag">美术风格验证未通过</span></header>
<p>此页用于检查动作复用、视角和像素稳定性。角色外观仍未达到所给参考图的要求。</p>
<div class="layout"><div class="stage"><canvas id="canvas" width="384" height="384"></canvas></div>
<div class="side"><label for="preset">穿搭</label><select id="preset"></select>
<label for="action">动作</label><select id="action"></select>
<div class="pair"><div><label for="size">原始像素</label><select id="size"></select></div>
<div><label for="direction">视角</label><select id="direction"></select></div></div>
<label for="zoom">整数倍放大</label><select id="zoom"><option value="4">4×</option>
<option value="6" selected>6×</option><option value="8">8×</option></select>
<div class="buttons"><button id="play">暂停</button><button id="step">下一帧</button></div>
<label for="frame">逐帧查看</label><input id="frame" type="range" min="0" max="7" value="0">
<div class="toggles"><label><input id="pivot" type="checkbox">脚底原点</label>
<label><input id="grid" type="checkbox">像素网格</label></div>
<label for="background">背景</label><select id="background"><option value="paper">浅色</option>
<option value="dark">深色</option><option value="checker">透明棋盘</option></select>
<div class="readout" id="readout"></div></div></div>
<footer><p>每个动作使用同一套骨架和 Action。左右视角分别渲染；图片没有逐帧裁切或重心对齐。
32 / 48 像素档在这份预览中只包含站立样本。非循环动作在本页循环播放，便于观察。</p>
<p>原始帧、精灵表和切片坐标见同目录。此试验未接入正式游戏，也不定义游戏攻击或交互时点。</p></footer></main>
<script id="data" type="application/json">__DATA__</script>
<script>
const data=JSON.parse(document.getElementById('data').textContent);
const $=id=>document.getElementById(id);
const label={worker:'工人',mechanic:'机械师',ranger:'旅人',rabbit:'兔帽',
idle:'站立',walk:'行走',run:'奔跑',jump:'跳跃',interact:'交互',cheer:'欢呼',
sit:'坐下',sleep:'睡眠',hurt:'受击',right:'朝右',left:'朝左',front:'正面',back:'背面'};
const images={};let clip,index=0,playing=true,last=0;
function options(id,values,preferred){
 const selected=preferred||$(id).value;
 $(id).replaceChildren(...values.map(value=>{
  const option=document.createElement('option');option.value=value;
  option.textContent=label[value]||value;return option;
 }));
 $(id).value=values.map(String).includes(String(selected))?selected:values[0];
}
function unique(rows,key){return [...new Set(rows.map(row=>row[key]))]}
options('preset',unique(data.clips,'preset'),'worker');
options('size',unique(data.clips,'size'),64);
function choose(){
 let rows=data.clips.filter(c=>c.preset===$('preset').value&&c.size===Number($('size').value));
 options('action',unique(rows,'action'),$('action').value||'walk');
 rows=rows.filter(c=>c.action===$('action').value);
 options('direction',unique(rows,'direction'),$('direction').value||'right');
 clip=rows.find(c=>c.direction===$('direction').value);
 index=0;last=performance.now();$('frame').max=clip.frames.length-1;draw();
}
function draw(){
 if(!clip||!images[clip.atlas])return;
 const zoom=Number($('zoom').value),size=clip.size,extent=size*zoom;
 const canvas=$('canvas');canvas.width=canvas.height=extent;
 const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;
 const background=$('background').value;
 ctx.fillStyle=background==='dark'?'#242b36':'#e8dcc6';ctx.fillRect(0,0,extent,extent);
 if(background==='checker'){
  for(let y=0;y<size;y+=4)for(let x=0;x<size;x+=4){
   ctx.fillStyle=((x+y)/4)%2?'#bdb9b0':'#ded8ca';ctx.fillRect(x*zoom,y*zoom,4*zoom,4*zoom);
  }
 }
 const frame=clip.frames[index],r=frame.rect;
 ctx.drawImage(images[clip.atlas],...r,0,0,extent,extent);
 if($('grid').checked){
  ctx.strokeStyle='rgba(80,80,80,.2)';ctx.lineWidth=1;ctx.beginPath();
  for(let i=0;i<=size;i++){ctx.moveTo(i*zoom+.5,0);ctx.lineTo(i*zoom+.5,extent);
   ctx.moveTo(0,i*zoom+.5);ctx.lineTo(extent,i*zoom+.5)}ctx.stroke();
 }
 if($('pivot').checked){
  const x=frame.pivot_pixels[0]*zoom,y=frame.pivot_pixels[1]*zoom;
  ctx.strokeStyle='#d94253';ctx.lineWidth=2;ctx.beginPath();
  ctx.moveTo(x-9,y);ctx.lineTo(x+9,y);ctx.moveTo(x,y-9);ctx.lineTo(x,y+9);ctx.stroke();
 }
 $('frame').value=index;
 $('readout').textContent=size+' × '+size+' px | '+clip.fps+' fps | frame '+(index+1)+' / '+
 clip.frames.length+' | '+(clip.loop?'循环动作':'单次动作');
}
for(const id of ['preset','size','action','direction'])$(id).addEventListener('change',choose);
for(const id of ['zoom','pivot','grid','background'])$(id).addEventListener('change',draw);
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放';last=performance.now()};
$('step').onclick=()=>{playing=false;$('play').textContent='播放';index=(index+1)%clip.frames.length;draw()};
$('frame').oninput=()=>{playing=false;$('play').textContent='播放';index=Number($('frame').value);draw()};
Promise.all(data.atlases.map(atlas=>new Promise((resolve,reject)=>{
 const img=new Image();img.onload=()=>{images[atlas.id]=img;resolve()};img.onerror=reject;img.src=atlas.data;
}))).then(choose);
function tick(now){if(playing&&clip&&now-last>=1000/clip.fps){
 const steps=Math.floor((now-last)*clip.fps/1000);index=(index+steps)%clip.frames.length;
 last+=steps*1000/clip.fps;draw()}requestAnimationFrame(tick)}requestAnimationFrame(tick);
</script></html>'''


def make_walk_gif(root, data):
    clips = [clip for clip in data["clips"] if clip["size"] == 64
             and clip["direction"] == "right" and clip["action"] == "walk"]
    if not clips:
        return
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)
    sheets = {item["id"]: Image.open(root / item["file"]).convert("RGBA")
              for item in data["atlases"]}
    frames = []
    for frame_index in range(8):
        board = Image.new("RGB", (208 * len(clips), 258), "#e8dcc6")
        draw = ImageDraw.Draw(board)
        draw.text((18, 15), "PIPELINE PROTOTYPE / shared walk", font=font, fill="#453a38")
        for i, clip in enumerate(clips):
            x, y, w, h = clip["frames"][frame_index % len(clip["frames"])]["rect"]
            sprite = sheets[clip["atlas"]].crop((x, y, x+w, y+h))
            sprite = sprite.resize((192, 192), Image.Resampling.NEAREST)
            board.paste(sprite, (i*208+8, 43), sprite)
            draw.text((i*208+20, 235), clip["preset"], font=font, fill="#453a38")
        frames.append(board)
    palette = frames[0].quantize(colors=128, dither=Image.Dither.NONE)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(root / "walk-loop.gif", save_all=True, append_images=frames[1:],
                   duration=round(1000/clips[0]["fps"]), loop=0, optimize=False, disposal=2)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    args = parser.parse_args()
    root = Path(args.input).resolve()
    data = json.loads((root / "atlas.json").read_text(encoding="utf-8"))
    make_walk_gif(root, data)
    for atlas in data["atlases"]:
        payload = base64.b64encode((root / atlas["file"]).read_bytes()).decode("ascii")
        atlas["data"] = "data:image/png;base64," + payload
    (root / "index.html").write_text(
        HTML.replace("__DATA__", json.dumps(data, ensure_ascii=False).replace("</", "<\\/")),
        encoding="utf-8")
    print(json.dumps({"status": "passed", "preview": str(root / "index.html")}))


if __name__ == "__main__":
    main()
