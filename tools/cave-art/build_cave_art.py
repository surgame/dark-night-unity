"""Deterministic palette and seam refinement of the retained provider source; exact tile masks are authored by code."""
from pathlib import Path
import random, math, json, hashlib
from PIL import Image, ImageDraw
OUT=Path('Game/Assets/DarkNights/Res/Art/Custom/CaveExploration')
OUT.mkdir(parents=True,exist_ok=True)
import argparse, numpy as np
parser=argparse.ArgumentParser();parser.add_argument('--update-generated',action='store_true');args=parser.parse_args()
manifest=OUT/'cave-art-manifest.json'
names=['cave-rock.png','cave-dualgrid.png']
if any((OUT/n).exists() for n in names):
 if not args.update_generated or not manifest.exists(): raise SystemExit('Existing assets: explicit --update-generated and unchanged output hashes required.')
 previous=json.loads(manifest.read_text(encoding='utf-8'))['outputs']
 for n in names:
  if hashlib.sha256((OUT/n).read_bytes()).hexdigest()!=previous[n]: raise SystemExit('Manual changes detected; refusing to overwrite '+n)
source=OUT/'rock-source-gpt-image-2.5.png'
a=np.asarray(Image.open(source).convert('RGB').resize((256,256),Image.Resampling.BOX)).astype(float)
for k in range(12):
 t=(12-k)/12*.5
 left=a[:,k].copy();right=a[:,-1-k].copy();a[:,k]=left*(1-t)+right*t;a[:,-1-k]=right*(1-t)+left*t
 top=a[k].copy();bottom=a[-1-k].copy();a[k]=top*(1-t)+bottom*t;a[-1-k]=bottom*(1-t)+top*t
rock=Image.fromarray(np.uint8(np.clip(a,0,255))).quantize(colors=28,dither=Image.Dither.NONE).convert('RGB')
rock.save(OUT/'cave-rock.png')
keys=['loam','slate','basalt','copper','iron','gold','moss','bedrock']
atlas=Image.new('RGBA',(512,1024))
for mat in range(8):
 for var in range(4):
  for mask in range(16):
   tile=Image.new('RGBA',(32,32)); pix=tile.load()
   for y in range(32):
    for x in range(32):
     xx=x/31; yy=y/31
     coverage=(bool(mask&1)*(1-xx)*(1-yy)+bool(mask&2)*xx*(1-yy)+bool(mask&4)*(1-xx)*yy+bool(mask&8)*xx*yy)
     if coverage<.5: continue
     col=rock.getpixel(((x+var*51)%256,(y+mat*29)%256))
     tint=[(1.2,.9,.65),(.85,.94,1.05),(.63,.65,.77),(1.1,.82,.54),(.73,1.,1.2),(1.3,1.02,.52),(.8,.93,.69),(.6,.62,.7)][mat]
     pix[x,y]=tuple(min(255,int(col[k]*tint[k])) for k in range(3))+(255,)
   atlas.paste(tile,(var*128+mask%4*32,mat*128+mask//4*32))
atlas.save(OUT/'cave-dualgrid.png')
manifest.write_text(json.dumps({'source':source.name,'model':'gpt-image-2.5','sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'outputs':{n:hashlib.sha256((OUT/n).read_bytes()).hexdigest() for n in names}},indent=2)+'\n',encoding='utf-8')
print('Rebuilt native palette rock and exact DualGrid masks; hashes updated.')
