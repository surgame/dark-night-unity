"""Five native 8px mineral block textures, each with 16 DualGrid masks × 4 variants.

No seam drawing, image resizing, Unity writes or authority state. Corner order
matches locked AnyRuleD: NW, NE, SW, SE. PNG coordinates have downward-positive Y.
"""
from pathlib import Path
from io import BytesIO
import base64
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
KEYS=['copper','iron','gold','silver','diamond']
LABELS=['铜','铁','金','银','钻石']
PALETTES=[
 ['483522','654130','8b4e32','ad6943','c88d58','d9aa74','46675a','332b21'],
 ['3e352a','353936','484d47','5c6258','73796a','929780','785037','282925'],
 ['4c3921','71502c','987037','b68d47','d1ae64','e2c787','695c3d','392d20'],
 ['443b30','414847','596462','788783','96a79e','bdc8b5','665d4c','2b302d'],
 ['393b32','354d50','496c71','668e94','8bb3b6','bad5cc','555d4d','252f2f']]
PATTERNS=[
 ['22332222','23333222','33442222','33440332','23001333','22611343','22233332','22332222'],
 ['22233222','23334322','23333222','22100222','22122332','22233442','22333222','22233222'],
 ['22232222','23333222','23442222','23310222','22001232','22122343','22233322','22232222'],
 ['22332222','23433222','23322122','22001222','22123342','22334432','22333222','22332222'],
 ['22233222','22344322','22354222','22231222','22001222','22123432','22234422','22233222']]

def lin(c):return np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
def srgb(c):return np.where(c<=.0031308,c*12.92,1.055*np.maximum(c,0)**(1/2.4)-.055)
def over(dst,src):
    a=src[...,3:4];b=dst[...,3:4];outa=a+b*(1-a)
    c=(lin(src[...,:3])*a+lin(dst[...,:3])*b*(1-a))/np.maximum(outa,1e-8)
    return np.concatenate([np.clip(srgb(c),0,1),outa],axis=-1)
def decode(s):return np.asarray(Image.open(BytesIO(base64.b64decode(s.split(',')[1]))).convert('RGBA')).astype(float)/255
def image(a):return Image.fromarray(np.round(np.clip(a,0,1)*255).astype('uint8'))

def texture(kind,variant):
    pattern=np.array([[int(c) for c in row] for row in PATTERNS[kind]])
    # Variants alter only the interior. All shared boundary samples are invariant.
    patch=pattern[2:6,2:6].copy()
    pattern[2:6,2:6]=np.rot90(patch,variant)
    pattern[:,7]=pattern[:,0];pattern[7,:]=pattern[0,:]
    palette=np.array([tuple(bytes.fromhex(c)) for c in PALETTES[kind]])
    opacity=np.array([112,196,232,255,255,255,184,96])
    return np.concatenate([palette[pattern],opacity[pattern][...,None]],axis=-1).astype('uint8')

def tile(kind,variant,mask):
    tex=texture(kind,variant).astype(float)/255
    yy,xx=np.indices((8,8));x=xx/7;y=yy/7
    f=(bool(mask&1)*(1-x)*(1-y)+bool(mask&2)*x*(1-y)+bool(mask&4)*(1-x)*y+bool(mask&8)*x*y)
    alpha=np.select([f<.34,f<.43,f<.53,f<.65,f<.82],[0,48,112,192,232],default=255)/255
    halo_alpha=np.select([f<.15,f<.26,f<.39,f<.53,f<.68],[0,16,40,64,32],default=0)/255
    if mask in [6,9]:
        # Diagonal-only occupancy stays split; outer boundary samples are untouched.
        alpha[3:5,3:5]=0;halo_alpha[3:5,3:5]=0
    body=tex.copy();body[...,3]*=alpha
    halo=np.zeros_like(body);halo[...,:3]=np.array(tuple(bytes.fromhex(PALETTES[kind][2])))/255
    halo[...,3]=halo_alpha
    merged=over(halo,body)
    for a in [body,halo,merged]:a[a[...,3]==0]=0
    return {'body':image(body),'alteration':image(halo),'merged':image(merged)}

def masks(grid):
    return grid[:-1,:-1].astype(int)+2*grid[:-1,1:]+4*grid[1:,:-1]+8*grid[1:,1:]

def render_grid(grid,atlases):
    gh,gw=grid.shape;out=Image.new('RGBA',((gw-1)*8,(gh-1)*8))
    for kind in range(5):
        mi=masks(grid==kind+1)
        layer=Image.new('RGBA',out.size)
        for y in range(gh-1):
            for x in range(gw-1):
                m=int(mi[y,x]);v=variant_at(x,y)
                sx=v*32+(m%4)*8;sy=(m//4)*8
                layer.paste(atlases[kind]['merged'].crop((sx,sy,sx+8,sy+8)),(x*8,y*8))
        out=image(over(np.asarray(out)/255,np.asarray(layer)/255))
    return out

def variant_at(x,y):
    n=((x+1)*374761393+(y+1)*668265263)&0xffffffff
    n=((n^(n>>13))*1274126177)&0xffffffff
    return (n^(n>>16))&3

def proof_grid():
    g=np.zeros((12,19),dtype=np.uint8)
    g[2:7,2:8]=1;g[3:6,3:5]=0;g[6:9,6:12]=1;g[7:10,9:15]=1
    g[2,12]=1;g[3,13]=1;g[2:4,16]=1
    return g

def font(size):return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',size)

def previews(atlases,reference):
    base=decode(reference['base']);fore=decode(reference['foreground']);layers=[decode(a) for a in reference['layers']]
    wall=base.copy()
    for k in [2,1,0]:wall=over(wall,layers[k])
    board=Image.new('RGB',(1504,1100),(20,21,21));pen=ImageDraw.Draw(board)
    pen.text((32,20),'DARK NIGHTS / 按格矿层 · DualGrid 自动连接',font=font(28),fill=(224,217,203))
    pen.text((32,64),'原生 8 × 8 px  |  16 个连接形状 × 4 个内部变体 / 矿种',font=font(18),fill=(174,170,156))
    sample=proof_grid()
    for k in range(5):
        x0=32+k*288;pen.text((x0,104),LABELS[k]+' / '+KEYS[k].upper(),font=font(20),fill=(224,217,203))
        # One shape for all ores: solid area, inside corner, hole, singleton, diagonal pair.
        mineral=np.asarray(render_grid(sample*(k+1),atlases))/255
        h,w=mineral.shape[:2]
        for d in range(3):
            bg=wall[110:110+h,176:176+w].copy()
            stamp=mineral.copy();stamp[...,:3]=srgb(lin(stamp[...,:3])*[.72,.48,.28][d])
            result=over(bg,stamp)
            pic=image(result).convert('RGB').resize((w*2,h*2),Image.Resampling.NEAREST)
            y0=145+d*218;board.paste(pic,(x0,y0))
            pen.text((x0,y0+182),['浅层岩面色样','中层岩面色样','深层岩面色样'][d],font=font(16),fill=(175,171,156))
        # 16 masks, one variant. The atlas itself has no grid or labels.
        tile0=atlases[k]['merged'].crop((0,0,32,32))
        check=Image.new('RGBA',(32,32),(52,53,52,255));check.alpha_composite(tile0)
        board.paste(check.convert('RGB').resize((160,160),Image.Resampling.NEAREST),(x0,840))
        pen.text((x0,1012),'连接图集 · 0–15',font=font(16),fill=(175,171,156))
    board.save(ROOT/'preview'/'five-ore-dualgrid.png')
    # Compact block deposits fitted to the original cave. Values are ore-cell occupancy.
    placements=[]
    shape=np.array([[0,0,0,0,0,0,0,0],[0,0,1,1,1,0,0,0],[0,1,1,1,1,1,0,0],
                    [0,1,1,1,1,1,1,0],[0,0,1,1,1,1,0,0],[0,0,0,0,0,0,0,0]],dtype=np.uint8)
    scene=base.copy();locs=[(68,88,1),(148,102,0),(246,87,1),(341,85,2),(391,227,0)]
    for k,(x,y,d) in enumerate(locs):placements.append({'ore':k,'x':x,'y':y,'layer':d,'grid':shape.tolist()})
    for d in [2,1,0]:
        scene=over(scene,layers[d])
        for p in [v for v in placements if v['layer']==d]:
            stamp=np.asarray(render_grid(shape*(p['ore']+1),atlases))/255
            h,w=stamp.shape[:2];x,y=p['x'],p['y'];stamp[...,3]*=layers[d][y:y+h,x:x+w,3]
            stamp[...,:3]=srgb(lin(stamp[...,:3])*[.72,.48,.28][d])
            scene[y:y+h,x:x+w]=over(scene[y:y+h,x:x+w],stamp)
    scene=over(scene,fore);image(scene).save(ROOT/'preview'/'cave-grid-ores.png')
    image(scene[48:]).resize((1008,528),Image.Resampling.NEAREST).save(ROOT/'preview'/'cave-grid-ores-2x.png')
    return placements

def verify(atlases):
    checked=0
    for kind in range(5):
        for part in ['body','alteration','merged']:
            atlas=atlases[kind][part]
            def a(m,v):return np.asarray(atlas.crop((v*32+m%4*8,m//4*8,v*32+m%4*8+8,m//4*8+8)))
            for m in range(16):
                for n in range(16):
                    for v in range(4):
                        for z in range(4):
                            if ((m>>1)&1)==(n&1) and ((m>>3)&1)==((n>>2)&1):
                                assert np.array_equal(a(m,v)[:,-1],a(n,z)[:,0]),('horizontal',kind,part,m,n,v,z)
                                checked+=1
                            if ((m>>2)&1)==(n&1) and ((m>>3)&1)==((n>>1)&1):
                                assert np.array_equal(a(m,v)[-1],a(n,z)[0]),('vertical',kind,part,m,n,v,z)
                                checked+=1
            for v in range(4):
                assert not a(0,v)[...,3].any()
            for m in [6,9]:assert not a(m,0)[3:5,3:5,3].any()
    return checked

def main():
    manifest_path=ROOT/'manifest.json'
    if manifest_path.exists():
        previous=json.loads(manifest_path.read_text(encoding='utf-8'))
        for entry in previous['files']:
            path=ROOT/entry['path']
            if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest()!=entry['sha256']:
                raise SystemExit('Manual change or missing prior atlas; refusing to overwrite: '+str(path))
    elif (ROOT/'atlases').exists() and any((ROOT/'atlases').glob('*.png')):
        raise SystemExit('Existing atlas without provenance; refusing to overwrite.')
    for name in ['atlases','preview']:(ROOT/name).mkdir(exist_ok=True)
    atlases=[];files=[];rules=[]
    for kind,key in enumerate(KEYS):
        mats={part:Image.new('RGBA',(128,32)) for part in ['body','alteration','merged']}
        for v in range(4):
            for m in range(16):
                for part,t in tile(kind,v,m).items():mats[part].paste(t,(v*32+m%4*8,m//4*8))
        for part,a in mats.items():
            path=ROOT/'atlases'/f'{key}-{part}.png';a.save(path)
            files.append({'path':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        atlases.append(mats)
        for m in range(1,16):rules.append({'ore':key,'mask':m,'corners':['This' if m&(1<<c) else 'NotThis' for c in range(4)],
            'channel':'ore','tileableFill':m==15,'transforms':'Identity','variants':[{'name':f'{key}_m{m}_v{v}','rectUnityBottomLeft':[v*32+m%4*8,32-(m//4+1)*8,8,8]} for v in range(4)]})
    checks=verify(atlases);reference=json.loads((ROOT/'reference.json').read_text());placements=previews(atlases,reference)
    (ROOT/'anyruled-contract.json').write_text(json.dumps({'status':'art contract; not imported Unity assets','cornerOrder':['NW','NE','SW','SE'],
        'cornerBits':[1,2,4,8],'pixelsPerUnit':8,'filter':'Point','spriteMesh':'FullRect','rgb':'sRGB','alpha':'straight',
        'ruleSlotCoverage':'ExactCell','boundaryContract':'CanonicalMaterialEdgesV1','mask0':'no output','diagonalPolicy':'6 and 9 disconnected',
        'rules':rules},ensure_ascii=False,indent=2),encoding='utf-8')
    manifest={'nativeTile':[8,8],'atlasSize':[128,32],'variants':4,'masks':16,'minerals':KEYS,'files':files,'placements':placements,
              'checks':{'exactRgbaSharedEdges':checks,'emptyMask':'pass','disconnectedDiagonals':'pass'},'unityValidation':'not run'}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'atlases':len(files),'spritesIncludingEmpty':320,'nonEmptyRules':75,'exactEdgeChecks':checks}))

if __name__=='__main__':main()
