"""岩舟 v2：在原生像素上绘制、分层、装配；Pillow 12。无缩图、无噪点滤镜。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, hashlib, csv, io, base64

import argparse
parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path, help="New empty output directory")
ROOT = parser.parse_args().output.resolve()
if ROOT.exists() and any(ROOT.iterdir()): raise SystemExit("Output must be empty")
ROOT.mkdir(parents=True, exist_ok=True)
OUT = ROOT / 'assets'
OUT.mkdir(exist_ok=True)
P = dict(ink='#1d211f', shade='#2d322d', dark='#40483e', mid='#5b6352', metal='#7c826c',
         light='#a2a68a', edge='#c2bea0', rust='#785044', copper='#a06e51', ochre='#b39560',
         cream='#d5c49a', glass='#243e3d', teal='#456461', mint='#759991', glow='#b7c6aa',
         void='#262823', wall='#353a31', warm='#5c5541', dirt='#514637', rubber='#171d1c')
ASSETS = {}
CANVAS=(176,104)

def new(size): return Image.new('RGBA', size)
def rect(d, box, c): d.rectangle(box, fill=P.get(c,c))
def poly(d, pts, c): d.polygon(pts, fill=P.get(c,c))
def line(d, pts, c, w=1): d.line(pts, fill=P.get(c,c), width=w)
def plate(d, box, base='mid'):
    x,y,r,b=box
    poly(d,[(x+3,y),(r-3,y),(r,y+3),(r,b-3),(r-3,b),(x+3,b),(x,b-3),(x,y+3)],'ink')
    poly(d,[(x+3,y+1),(r-3,y+1),(r-1,y+3),(r-1,b-3),(r-3,b-1),(x+3,b-1),(x+1,b-3),(x+1,y+3)],base)
    line(d,[(x+3,y+1),(r-4,y+1)],'light')
    line(d,[(x+2,y+4),(x+2,b-4)],'metal')
    line(d,[(x+4,b-2),(r-3,b-2),(r-2,b-4)],'dark')
def save(name, im, offset=(0,0), pivot=None, category='ship', role='', layer=0, state='static'):
    if pivot is None: pivot=(im.width/2,im.height)
    im.save(OUT/(name+'.png'))
    ASSETS[name]=dict(file='assets/'+name+'.png',size=list(im.size),pivot_top_left=list(pivot),
        pivot_unity_normalized=[pivot[0]/im.width,1-pivot[1]/im.height],assembly_top_left=list(offset),
        category=category,role=role,sorting_order=layer,state=state,alpha='binary straight',status='候选可导入像素源，未在 Unity 导入验收')
    return im

# 船体后壁：上下空间沿一条可行走剖面连通；后壁不承载碰撞。
back=new(CANVAS); d=ImageDraw.Draw(back)
body=[(20,46),(30,38),(100,38),(108,16),(144,12),(160,28),(166,48),(160,72),(148,80),(30,80),(20,70)]
poly(d,body,'ink');poly(d,[(24,48),(32,42),(102,42),(112,20),(142,16),(156,30),(160,48),(156,69),(145,76),(33,76),(24,68)],'wall')
poly(d,[(30,46),(99,46),(107,28),(149,28),(155,46),(150,54),(128,54),(96,74),(32,74)],'void')
for x in (36,64,88):
    rect(d,(x,46,x+19,48),'dark');rect(d,(x,49,x+18,67),'wall');line(d,[(x+2,51),(x+16,51)],'warm')
poly(d,[(107,26),(114,21),(141,19),(151,30),(151,39),(107,39)],'glass')
poly(d,[(110,27),(115,23),(138,21),(143,25),(110,33)],'teal')
line(d,[(111,27),(116,23),(138,21)],'mint');line(d,[(129,22),(132,39)],'dark',2)
rect(d,(132,59,151,71),'shade');line(d,[(134,61),(147,61)],'dark')
save('hull_back',back,role='后壁与舱内远侧玻璃；无角色、门和设备烘焙',layer=10)

# 固定结构：下层平甲板 → 32:20 真斜面短梯 → 前上方驾驶舱。
frame=new(CANVAS); d=ImageDraw.Draw(frame)
line(d,body+[body[0]],'ink',3)
line(d,[(29,39),(101,39),(109,17),(144,13),(159,28)],'metal',2)
line(d,[(32,40),(100,40)],'light')
line(d,[(24,70),(33,78),(149,78),(160,69)],'dark',3)
line(d,[(48,76),(96,76),(128,56),(151,56)],'ink',4)
line(d,[(48,75),(96,75),(128,55),(151,55)],'metal',1)
for k in range(8):
    x=96+k*4;y=75-int(k*2.5)
    line(d,[(x,y),(x+3,y)],'ochre')
rect(d,(95,44,97,55),'dark');line(d,[(96,43),(102,43),(110,19)],'light')
rect(d,(28,69,43,75),'dark');rect(d,(32,72,43,74),'metal')
rect(d,(132,74,149,78),'mid');rect(d,(136,75,144,76),'rust')
save('hull_structure',frame,role='固定骨架与短梯；玩法斜面由 geometry.json 定义',layer=50)

shell=new(CANVAS); d=ImageDraw.Draw(shell)
poly(d,body,'ink')
poly(d,[(22,46),(31,40),(102,40),(110,17),(144,14),(157,28),(162,47),(156,70),(148,77),(31,77),(22,69)],'mid')
poly(d,[(31,41),(101,41),(97,50),(29,50),(24,55),(24,47)],'metal')
poly(d,[(25,58),(151,58),(154,50),(160,49),(155,69),(147,75),(31,75),(25,68)],'dark')
poly(d,[(34,52),(91,52),(91,68),(35,68),(31,64),(31,56)],'mid')
line(d,[(36,53),(85,53)],'metal');line(d,[(38,67),(85,67)],'shade')
plate(d,(68,48,94,71),'mid');rect(d,(73,53,87,56),'dark');rect(d,(74,53,86,54),'shade')
rect(d,(73,62,78,64),'rust');rect(d,(83,62,88,64),'ochre')
poly(d,[(109,27),(116,20),(140,18),(153,30),(154,47),(132,48),(108,43)],'ink')
poly(d,[(112,28),(117,23),(139,21),(150,31),(151,44),(132,45),(111,41)],'glass')
poly(d,[(113,28),(118,24),(138,22),(143,26),(113,36)],'teal')
line(d,[(115,28),(119,25),(137,23)],'mint');line(d,[(130,23),(132,44)],'metal',2)
poly(d,[(99,54),(107,47),(129,50),(151,50),(150,56),(126,59),(101,72)],'metal')
poly(d,[(102,58),(111,51),(128,53),(149,53),(148,55),(125,56),(103,69)],'mid')
line(d,[(106,58),(117,51)],'light')
for x,y in ((35,44),(85,44),(99,46),(144,69)):
    rect(d,(x,y,x+1,y+1),'light')
for x in range(106,128,4):line(d,[(x,72),(x+2,69)],'shade')
rect(d,(38,43,55,45),'copper');rect(d,(40,43,45,44),'ochre')
save('hull_front_shell',shell,role='完整外壳；进入时按舱段隐藏，不把小人画在壳上',layer=80)
# 可独立收起的外壳区域保留完整画布坐标，接入按舱段切换。
for name,box in [('shell_work_section',(18,35,101,83)),('shell_cockpit_section',(101,10,168,83))]:
    part=new(CANVAS); part.paste(shell.crop(box),(box[0],box[1]));save(name,part,role='舱段剖切遮挡层，与 hull_front_shell 二选一',layer=80)

# 引擎：实体与火焰独立；上方大块形体，没有密集发光装饰。
engine=new((24,40));d=ImageDraw.Draw(engine)
plate(d,(0,3,23,32),'dark');plate(d,(4,0,19,27),'mid')
rect(d,(7,4,15,6),'metal');rect(d,(7,10,16,22),'shade')
for y in (11,15,19):line(d,[(8,y),(15,y)],'dark')
rect(d,(3,29,20,33),'shade');rect(d,(5,32,18,35),'ink');rect(d,(7,33,16,35),'rust')
rect(d,(1,11,3,25),'metal');rect(d,(19,6,21,17),'copper')
save('engine_left',engine,(0,36),(12,36),role='左动力舱；不含火焰',layer=60)
save('engine_right',engine.transpose(Image.Transpose.FLIP_LEFT_RIGHT),(152,36),(12,36),role='右动力舱；镜像后独立导出',layer=60)
for name,size,role in [('gear_socket',(12,8),'固定安装座'),('gear_piston',(6,16),'独立活塞，不缩放整套支柱'),('gear_foot',(18,6),'脚垫底面为接触锚点')]:
    im=new(size);d=ImageDraw.Draw(im)
    if name=='gear_socket':plate(d,(0,0,11,7),'dark');rect(d,(4,4,7,7),'metal')
    elif name=='gear_piston':rect(d,(0,0,5,15),'ink');rect(d,(1,0,4,15),'mid');rect(d,(2,3,3,14),'light')
    else:poly(d,[(3,0),(14,0),(17,3),(17,5),(0,5),(0,3)],'ink');rect(d,(2,2,15,3),'metal');rect(d,(3,4,14,4),'dark')
    save(name,im,pivot=(size[0]/2,size[1] if name=='gear_foot' else 0),category='mechanism',role=role,layer=40)
ramp=new((42,24));d=ImageDraw.Draw(ramp)
poly(d,[(0,20),(40,0),(41,4),(1,23)],'ink');line(d,[(0,20),(40,0)],'metal',2)
for k in range(10):rect(d,(k*4,20-k*2,k*4+2,20-k*2),'ochre' if k in (0,9) else 'mid')
save('ramp_deployed',ramp,(8,76),(40,0),category='mechanism',role='1:2 展开坡道；铰点为 (48,76)，足端 (8,96)',layer=45,state='deployed')
stowed=new((12,24));d=ImageDraw.Draw(stowed);plate(d,(1,0,10,23),'dark')
for y in range(4,22,4):rect(d,(3,y,8,y+1),'mid')
save('ramp_stowed',stowed,(38,52),(10,24),category='mechanism',role='收拢坡道独立状态，不与展开态同时显示',layer=65,state='stowed')
for state in ('closed','half','open'):
    im=new((20,28));d=ImageDraw.Draw(im);plate(d,(0,0,19,27),'dark');rect(d,(2,2,17,26),(0,0,0,0))
    if state!='open':
        h=24 if state=='closed' else 11
        rect(d,(2,2,17,h+1),'mid');line(d,[(4,3),(16,3)],'metal')
        for y in range(7,h,6):line(d,[(4,y),(13,y)],'dark')
        rect(d,(5,h-1,7,h),'ochre')
    rect(d,(0,7,1,8),'mint');save('airlock_'+state,im,(38,49),(10,27),category='mechanism',role='工作气闸：24px 垂直净空；帧间同锚点',layer=70,state=state)
for state in ('closed','open'):
    im=new((24,10));d=ImageDraw.Draw(im);plate(d,(0,2,23,9),'dark')
    if state=='closed':rect(d,(3,4,20,6),'mid');rect(d,(6,4,10,5),'ochre')
    else:rect(d,(4,3,19,8),(0,0,0,0));rect(d,(3,0,20,1),'metal')
    save('drone_hatch_'+state,im,(56,34),(12,6),category='mechanism',role='无人机独立顶部出口；离舱先上升再侧移',layer=55,state=state)

def prop(name,size,draw,offset,role,layer=25):
    im=new(size);draw(ImageDraw.Draw(im));return save(name,im,offset,category='interior',role=role,layer=layer)
def cargo(d):
    plate(d,(0,2,19,15),'dark');rect(d,(4,4,15,5),'metal');rect(d,(9,3,10,14),'rust');rect(d,(5,10,14,12),'shade')
prop('cargo_locker',(20,16),cargo,(72,58),'低矮背景货柜，不阻断行走层')
def charger(d):
    rect(d,(0,17,23,19),'ink');rect(d,(1,16,22,17),'metal')
    for x in (2,14):
        line(d,[(x,5),(x,14),(x+7,14)],'dark')
        rect(d,(x+1,15,x+7,16),'shade');rect(d,(x+6,16,x+7,16),'teal')
prop('robot_dock_empty',(24,20),charger,(55,56),'两个空机器人泊位；绝不烘焙单位')
def pilot(d):
    rect(d,(5,0,8,11),'ink');rect(d,(6,1,7,8),'copper');rect(d,(0,9,8,12),'dark');rect(d,(2,10,6,11),'metal');rect(d,(4,12,5,15),'ink')
prop('pilot_seat',(12,16),pilot,(130,40),'可占用座椅；就座挂点 (136,56)')
def console(d):
    poly(d,[(0,0),(11,0),(15,8),(14,12),(4,12)],'ink');poly(d,[(2,1),(10,1),(12,5),(4,5)],'teal');line(d,[(3,2),(8,2)],'mint');rect(d,(6,7,10,8),'dark');rect(d,(9,7,10,7),'ochre')
prop('pilot_console',(16,13),console,(138,39),'驾驶台；屏幕只有小面积亮色')
def strip(d):rect(d,(0,0,15,3),'ink');rect(d,(2,1,13,2),'ochre');rect(d,(5,1,10,1),'cream')
prop('cabin_lamp',(16,4),strip,(60,42),'灯罩像素；实际柔光需运行时独立生成',45)

# 独立单位：统一聚色，脚底锚点固定；四帧为首轮动作草稿。
for kind in ('crew','robot'):
    for f in range(4):
        im=new((12,16));d=ImageDraw.Draw(im)
        if kind=='crew':
            rect(d,(3,1,7,5),'ink');rect(d,(4,1,6,2),'edge');rect(d,(4,3,8,4),'teal');rect(d,(5,3,7,3),'mint')
            rect(d,(3,6,7,11),'dark');rect(d,(4,6,7,9),'copper');rect(d,(1,7,2,11),'mid');rect(d,(8,7,9,10),'metal')
        else:
            plate(d,(2,1,9,7),'mid');rect(d,(3,3,8,5),'glass');rect(d,(5,3,7,4),'mint')
            rect(d,(3,8,8,11),'dark');rect(d,(4,8,7,9),'metal');rect(d,(0,8,2,11),'mid');rect(d,(9,8,11,11),'mid')
        a=[0,1,0,-1][f];rect(d,(3+a,12,4+a,14),'mid');rect(d,(7-a,12,8-a,14),'dark');rect(d,(2+a,15,4+a,15),'ink');rect(d,(7-a,15,9-a,15),'ink')
        save(f'{kind}_walk_{f}',im,pivot=(6,16),category='unit',role='美术候选动作草稿；非既有正式人物替换',layer=35,state=f'walk_{f}')
for f in range(3):
    im=new((16,12));d=ImageDraw.Draw(im);line(d,[(2,3),(13,3)],'ink',2)
    plate(d,(4,3,11,9),'mid');rect(d,(6,5,10,6),'glass');rect(d,(7,5,9,5),'mint')
    for x in (0,12):rect(d,(x,4,x+3,7),'dark');rect(d,(x,[2,3,1][f],x+3,[2,3,1][f]),'metal')
    line(d,[(6,9),(5,11)],'shade');line(d,[(10,9),(11,11)],'shade')
    save('drone_hover_'+str(f),im,pivot=(8,6),category='unit',role='独立照明／勘探无人机，未赋予自动采矿能力',layer=35,state=f'hover_{f}')
for name in ('field_lamp','oxygen_pack','cargo_crate'):
    im=new((16,24));d=ImageDraw.Draw(im)
    if name=='field_lamp':
        plate(d,(3,1,12,8),'dark');rect(d,(5,3,10,6),'ochre');rect(d,(6,3,9,4),'cream');rect(d,(7,9,8,19),'mid');poly(d,[(7,19),(2,22),(2,23),(13,23),(13,22),(8,19)],'ink')
    elif name=='oxygen_pack':
        for x in (1,9):plate(d,(x,4,x+5,20),'dark');rect(d,(x+1,8,x+3,16),'teal');rect(d,(x+1,6,x+3,7),'metal')
        rect(d,(2,22,13,23),'ink');line(d,[(3,3),(12,3)],'copper')
    else:plate(d,(1,9,14,23),'mid');rect(d,(3,12,12,14),'dark');rect(d,(7,10,8,22),'rust');rect(d,(4,18,6,19),'ochre')
    save(name,im,pivot=(8,24),category='equipment',role='同风格可搬设备，玩法仍归正式设备对象',layer=35)
for strength,length in [('low',12),('medium',20),('high',28),('shutdown',6)]:
    for f in range(3 if strength!='shutdown' else 1):
        im=new((16,32));d=ImageDraw.Draw(im);end=length-[0,2,1][f]
        poly(d,[(2,0),(13,0),(12,8),(10,end-3),(8,end),(5,end-4),(3,7)],'rust')
        poly(d,[(4,0),(11,0),(10,9),(8,end-3),(6,8)],'copper')
        poly(d,[(6,0),(9,0),(8,max(3,end//2)),(7,6)],'ochre')
        rect(d,(6,0,9,2),'cream')
        save(f'flame_{strength}_{f}',im,pivot=(8,0),category='effect',role='按推力档选帧；同档 3 帧，熄火 1 帧',layer=5,state=strength)
for f in range(4):
    im=new((48,16));d=ImageDraw.Draw(im)
    for side in (-1,1):
        for n in range(4):
            x=24+side*(4+f*3+n*3);y=13-(n%2)*2-f
            if 0<x<45:
                poly(d,[(x-3,y),(x-1,y-3),(x+2,y-2),(x+3,y),(x+1,y+2),(x-2,y+2)],'dirt' if n%2 else 'warm')
    save('dust_'+str(f),im,pivot=(24,16),category='effect',role='岩尘颜色低饱和；整帧 Alpha 可在运行时衰减',layer=90,state=f'dust_{f}')

# 装配合同使用原生左上角坐标，与 Unity pivot 坐标显式分开。
assembly=[('hull_back',(0,0)),('cargo_locker',(72,58)),('robot_dock_empty',(55,56)),('pilot_seat',(130,40)),('pilot_console',(138,39)),('cabin_lamp',(60,42)),('hull_structure',(0,0)),('engine_left',(0,36)),('engine_right',(152,36)),('drone_hatch_open',(56,34)),('ramp_deployed',(8,76)),('airlock_open',(38,49))]
for x in (60,140):
    assembly.extend([('gear_socket',(x-6,76)),('gear_piston',(x-3,78)),('gear_foot',(x-9,90))])
def assembled(exterior=False, units=False):
    im=new(CANVAS)
    for name,xy in sorted(assembly,key=lambda p:ASSETS[p[0]]['sorting_order']):im.alpha_composite(Image.open(OUT/(name+'.png')),xy)
    if units:
        for name,xy in [('robot_walk_0',(57,60)),('robot_walk_2',(69,60)),('crew_walk_0',(126,40))]:im.alpha_composite(Image.open(OUT/(name+'.png')),xy)
    if exterior:
        im.alpha_composite(shell)
        im.alpha_composite(Image.open(OUT/'airlock_closed.png'),(38,49))
        for name,xy in [('engine_left',(0,36)),('engine_right',(152,36))]:im.alpha_composite(Image.open(OUT/(name+'.png')),xy)
    return im
assembled(False).save(ROOT/'cutaway_native.png');assembled(True).save(ROOT/'exterior_native.png')
assembled(False,True).save(ROOT/'cutaway_with_units_native.png')
palette={'name':'Strata Utility / 岩层工业','native_pixels_per_cell':8,'colors':P,
         'method':'整数坐标直接绘制；无大图缩小、像素化滤镜、随机散点或生成式图像',
         'material_rule':'2–4px 成组明暗；1px 接缝；主面板 3 级明度；发光只做小范围标识'}
(ROOT/'palette.json').write_text(json.dumps(palette,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'palette.gpl').write_text('GIMP Palette\nName: Strata Utility\nColumns: 5\n#\n'+'\n'.join(' '.join(str(int(c[i:i+2],16)) for i in (1,3,5))+' '+n for n,c in P.items()),encoding='utf-8')
manifest=dict(schema='dark-nights-ship-art/2',version='2026-09-22',ship_canvas=list(CANVAS),ship_origin_top_left=[88,96],
    pixels_per_cell=8,logical_units_per_cell=16,pixels_per_unity_unit=8,
    units_note='每原生像素对应2个权威坐标单位；PPU=8 以当前地形每格1 Unity单位为前提，Prefab接入须核对父缩放',
    texture=dict(filter='Point',compression='None',mipmaps=False,sRGB=True,alpha='straight; no premultiply; RGB=0 when A=0',mesh='FullRect'),
    assets=ASSETS,assembly=[dict(asset=n,top_left=list(xy)) for n,xy in assembly],
    animation=dict(crew_walk=dict(frames=4,fps=6),robot_walk=dict(frames=4,fps=6),drone_hover=dict(frames=3,fps=8),flame=dict(frames_per_strength=3,fps=10,shutdown_frames=1)))
for name,a in ASSETS.items():
    p=OUT/(name+'.png');a['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (ROOT/'资源清单.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['id','路径','宽px','高px','枢轴X_左上','枢轴Y_左上','分类','用途','状态'])
    for n,a in ASSETS.items():w.writerow([n,a['file'],*a['size'],*a['pivot_top_left'],a['category'],a['role'],a['status']])

# 带2px透明外边的确定性图集；矩形不包含padding。
atlas=new((512,512));frames={};x=y=2;rowh=0
for name,a in ASSETS.items():
    im=Image.open(OUT/(name+'.png'))
    if x+im.width+2>512:x=2;y+=rowh+4;rowh=0
    assert y+im.height+2<=512
    atlas.alpha_composite(im,(x,y));frames[name]=dict(rect_top_left=[x,y,im.width,im.height],pivot_top_left=a['pivot_top_left'],rotated=False,trimmed=False)
    x+=im.width+4;rowh=max(rowh,im.height)
atlas.save(ROOT/'ship_atlas.png');(ROOT/'ship_atlas.json').write_text(json.dumps(dict(size=[512,512],padding=2,frames=frames),ensure_ascii=False,indent=2),encoding='utf-8')
# 浏览器不依赖 fetch：一个本地可审查的图片字典。
bundle={n:'data:image/png;base64,'+base64.b64encode((OUT/(n+'.png')).read_bytes()).decode() for n in ASSETS}
(ROOT/'assets.js').write_text('window.SHIP_ASSETS='+json.dumps(bundle)+';\nwindow.SHIP_MANIFEST='+json.dumps(manifest,ensure_ascii=False)+';',encoding='utf-8')
print(json.dumps(dict(assets=len(ASSETS),atlas=[512,512],root=str(ROOT)),ensure_ascii=False))
