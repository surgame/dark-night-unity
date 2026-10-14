"""Original light pipeline, decomposed in the unchanged slope/standing model.

Twelve logical stages, not twelve GPU passes. All pictures are calculated CPU
approximations of the existing formulas; no new transport/projection/blur is used.
"""
from pathlib import Path
import importlib.util
import json
import hashlib
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
from matplotlib.patches import Polygon, Rectangle
from matplotlib.collections import LineCollection

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
spec = importlib.util.spec_from_file_location("original_stages", OUT / "stress_h_parameters.py")
s = importlib.util.module_from_spec(spec)
spec.loader.exec_module(s)
t, m = s.t, s.t.m
FONT = FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
RECT = (-5.2, 3.2, -4.2, 3.3)
W, H = 672, 600
WARM = np.array([1., .7484141, .4338784])
VMAX = 4.
TITLES = [
    "① 输入：墙形、灯口和朝向",
    "② 灯口光束＋距离衰减（未遮挡）",
    "③ 直射遮挡后：D",
    "④ 受光墙段：裁取后存33个入射节点",
    "⑤ 积分入射密度，得到返光目标：T",
    "⑥ 圆形核的覆盖／距离衰减：C",
    "⑦ 扣除已有直射，得到正余量：E",
    "⑧ 遮挡前返光：U＝E×C×F",
    "⑨ 探针可见性：V（此处产生硬切）",
    "⑩ 遮挡后返光：B＝U×V",
    "11 直射＋返光，限制墙内亮度",
    "12 加底光、材质和法线后的显示示意",
]
DETAILS = [
    "L=(-0.75,1.1)，向下93.44°；红虚线是未遮挡光束外边界。",
    "按原灯口宽0.967、锥角46.2°、强度2.78、照距24计算；暂不测试墙体。",
    "原五个灯口采样做遮挡；部分直射从左端绕出，另一部分落在斜面。NearIntensity=0。",
    "准备阶段按同一光束公式另算墙面入射，裁出受光首尾，再重新采样33节点；不是33盏点光。",
    "SoftBlob用径向核积分节点间的连续密度，取局部照度I；匹配模式T=2*(0.8I+0.2M)。",
    "C来自径向核权重积分，R=4.69格。本场景距离门控F=1；圆形外围已经连续归零。",
    "E=DNPositiveExcess(T,D)，近似max(T-D,0)，零附近有小过渡；因此直射区会出现补光缺口。",
    "U=E*C*F。到这一步尚未使用返光遮挡，可见圆润原光团与直射扣除的形状。",
    "两探针按距离加权求V；角落中两探针钳到同一端点，退化成一条硬阴影射线。",
    "U乘V。V从1跳至0时，原本非零的U被直接切断；切面在此进入实际返光。",
    "本模型只有一条受光墙段，八槽融合退化为B；加D后，岩体RGB最大通道限制到0.65。",
    "游戏写384x216光场，再按几何约束插值、限制墙表层、按直射占比处理Relief，最后加底光乘材质。此图用高精度场和均匀材质示意。",
]


def visibility(points, radius):
    delta = points - m.START
    along = delta @ m.TANGENT
    inset = min(.01, m.LENGTH * .25)
    result = np.zeros(points.shape[:-1]); weights = np.zeros_like(result)
    locations, source_values = [], []
    for probe in range(2):
        offset = 0 if probe == 0 else np.clip((m.LENGTH * .5 - along) * .5, -radius * .65, radius * .65)
        at = np.clip(along + offset, inset, m.LENGTH - inset)
        q = m.START + at[..., None] * m.TANGENT + m.NORMALS[0] * .003
        w = 1 / (.04 + np.sum((points - q) ** 2, axis=-1))
        v = m.visibility(q, points, m.WALL_DEPTH)
        result += w * v; weights += w
        locations.append(at); source_values.append(v)
    return result / weights, locations, source_values


def decorate(ax, index, outline=True, lamp=True):
    if outline:
        ax.add_patch(Polygon(m.WALL, facecolor="none", edgecolor="#a59a85", linewidth=.8))
    if lamp:
        x, y = m.LIGHT
        ax.add_patch(Rectangle((x-.055,y-.035),.11,.26,color="#aeb6b2",zorder=5))
        ax.add_patch(Rectangle((x-.1,y-.11),.2,.07,color="#fff2ac",zorder=6))
        ax.annotate("",xy=m.LIGHT+m.FORWARD*.58,xytext=m.LIGHT+m.FORWARD*.19,
                    arrowprops={"arrowstyle":"->","color":"#e5d9b9","lw":1.1})
    ax.set_xlim(RECT[:2]);ax.set_ylim(RECT[2:]);ax.set_aspect("equal")
    ax.set_xticks([]);ax.set_yticks([])
    ax.set_title(TITLES[index],fontproperties=FONT,fontsize=13,pad=8)


def draw(ax, index, fields):
    if index == 0:
        ax.set_facecolor("#11151b")
        ax.add_patch(Polygon(m.WALL,facecolor="#343a40",edgecolor="#b7bdc1",linewidth=1.2))
        angle=np.deg2rad(m.CONE/2)
        for sign in (-1,1):
            start=m.LIGHT+sign*m.PERP*m.HALF_WIDTH
            edge=m.FORWARD*np.cos(angle)+sign*m.PERP*np.sin(angle)
            end=start+edge*12
            ax.plot([start[0],end[0]],[start[1],end[1]],"--",color="#cf7777",lw=1)
        ax.text(1.7,-2.8,"岩体",fontproperties=FONT,color="#c2c6c9",ha="center",fontsize=13)
        decorate(ax,index,outline=False)
        ax.text(.025,.965,"原始站位／向下照射／部分直射漏出左端",transform=ax.transAxes,va="top",fontproperties=FONT,color="#d3d8dd",fontsize=10)
        return
    if index == 3:
        ax.set_facecolor("#11151b")
        ax.add_patch(Polygon(m.WALL,facecolor="#343a40",edgecolor="#b7bdc1",linewidth=1))
        nodes=m.START+np.linspace(0,m.LENGTH,33)[:,None]*m.TANGENT
        segments=np.stack([nodes[:-1],nodes[1:]],1)
        line=LineCollection(segments,cmap="inferno",norm=plt.Normalize(0,VMAX),linewidths=8)
        line.set_array((m.DENSITY[:-1]+m.DENSITY[1:])*.5);ax.add_collection(line)
        ax.scatter(nodes[:,0],nodes[:,1],c=m.DENSITY,cmap="inferno",vmin=0,vmax=VMAX,s=8,edgecolors="none",zorder=5)
        ax.text(.025,.965,"彩色：受光墙段；33节点定义连续密度",transform=ax.transAxes,va="top",fontproperties=FONT,color="#d3d8dd",fontsize=10)
        inset=ax.inset_axes([.08,.08,.78,.21])
        inset.plot(np.linspace(0,m.LENGTH,33),m.DENSITY,color="#e7ad50",lw=1.5)
        inset.scatter(np.linspace(0,m.LENGTH,33),m.DENSITY,color="#e7ad50",s=5)
        inset.set_facecolor("#191d23");inset.set_ylim(0,VMAX)
        inset.tick_params(colors="#c7ccd1",labelsize=8)
        inset.set_xlabel("沿受光墙段的位置（格）",fontproperties=FONT,color="#c7ccd1",fontsize=9)
        inset.set_ylabel("入射",fontproperties=FONT,color="#c7ccd1",fontsize=9)
        decorate(ax,index,outline=False)
        return
    names={1:"beam",2:"direct",4:"target",5:"coverage",6:"excess",7:"unblocked",8:"visibility",9:"bounce",10:"capped"}
    if index == 11:
        ax.imshow(fields["display"],extent=RECT,origin="lower",interpolation="nearest")
        decorate(ax,index)
        ax.text(.025,.965,"底光0.1／均匀材质0.18／平面法线示意",transform=ax.transAxes,va="top",fontproperties=FONT,color="#d7cebf",fontsize=10)
        return
    data=fields[names[index]]
    mask=index in (5,8)
    ax.imshow(data,extent=RECT,origin="lower",cmap="gray" if mask else "inferno",vmin=0,vmax=1 if mask else VMAX,interpolation="nearest")
    decorate(ax,index)
    note="覆盖／可见性标尺：0黑 → 1白" if mask else "照度标尺：0黑 → 4亮黄（全程相同）"
    if index in (4,5,6,7):
        note+="\n墙体仅画轮廓；返光遮挡尚未乘入"
    ax.text(.025,.965,note,transform=ax.transAxes,va="top",fontproperties=FONT,color="#eee8df",fontsize=10,
            bbox={"facecolor":"#181818","alpha":.78,"edgecolor":"none","pad":4})
    if index==8:
        q=m.START+m.TANGENT*.01+m.NORMALS[0]*.003
        e=m.WALL[0]-q;e/=np.linalg.norm(e)
        end=m.WALL[0]+e*10
        ax.plot([m.WALL[0,0],end[0]],[m.WALL[0,1],end[1]],color="#ec7c67",lw=1.2)
        ax.text(-3.9,-2.9,"端点硬阴影边界",fontproperties=FONT,color="#ec7c67",fontsize=10)


def main():
    s.configure(s.REFERENCE,{})
    x=np.linspace(RECT[0],RECT[1],W);y=np.linspace(RECT[2],RECT[3],H)
    points=np.stack(np.meshgrid(x,y),-1)
    distance=np.linalg.norm(points-m.LIGHT,axis=-1)
    beam=m.beam(points)*np.clip(1-distance/m.RANGE,0,1)**1.15*m.INTENSITY
    direct=t.direct(points)[0]
    delta=points-m.START
    local,coverage,radius=t.shape(points,delta@m.TANGENT,delta@m.NORMALS[0])
    active=(local>0)&(coverage>0)
    target=np.where(active,t.C["gain"]*(.8*local+.2*m.MEAN),0)
    excess=m.positive_excess(target,direct)
    fade=t.C["_fade"]
    unblocked=excess*coverage # t.shape already includes fade in coverage.
    vis,probe_at,probe_v=visibility(points,radius)
    bounce=unblocked*vis
    raw=direct+bounce
    rock=m.solid(points)
    capped=np.where(rock,np.minimum(.65,raw),raw)
    scale=np.divide(capped,raw,out=np.ones_like(raw),where=raw>1e-12)
    # Final display is a flat-normal material example, not a GPU screenshot.
    signed=(points[...,None,:]-m.WALL)*m.NORMALS
    depth=-np.max(np.sum(signed,axis=-1),axis=-1)
    depth_valid=(~rock)|(depth<=m.WALL_DEPTH)
    direct_fraction=np.divide(direct,raw,out=np.zeros_like(raw),where=raw>1e-12)
    relief_factor=1+direct_fraction*((.55+.65*.7/np.sqrt(1+.7*.7))-1)*.22
    displayed=np.where(depth_valid,capped*relief_factor,0)
    rgb=.18*(.10+displayed[...,None]*WARM)
    display=t.srgb(np.clip(rgb,0,1))
    fields=dict(beam=beam,direct=direct,local=local,target=target,coverage=coverage,
        excess=excess,unblocked=unblocked,visibility=vis,bounce=bounce,raw=raw,capped=capped,display=display)
    original,_,old_bounce,_=s.evaluate(points)
    checks=dict(return_formula_max_error=float(np.max(np.abs(bounce-old_bounce))),
                final_field_max_error=float(np.max(np.abs(capped-original))),
                finite=all(bool(np.all(np.isfinite(v))) for v in fields.values()),
                prepared_profile_length=float(m.LENGTH),distance_fade=float(fade))
    # Each numbered stage is also saved separately for full-size inspection.
    for index in range(12):
        fig,ax=plt.subplots(figsize=(6.4,5.9),layout="constrained")
        draw(ax,index,fields)
        fig.savefig(OUT/f"original-pipeline-stage-{index+1:02}.png",dpi=135)
        plt.close(fig)
    for page in range(3):
        fig,axes=plt.subplots(2,2,figsize=(11.6,11),layout="constrained")
        for ax,index in zip(axes.flat,range(page*4,page*4+4)):
            draw(ax,index,fields)
        fig.suptitle(f"原始算法逐步形成：{page*4+1}—{page*4+4}（离线分解）\n站位、墙形、朝向、增益与半径全部保持原值",fontproperties=FONT,fontsize=16)
        fig.supxlabel("这是逻辑分项，不是独立GPU Pass；同一照度标尺，无新模型／模糊／颗粒",fontproperties=FONT,fontsize=10)
        fig.savefig(OUT/f"original-pipeline-page-{page+1}.png",dpi=145)
        plt.close(fig)
    # The one-sided mask proposal is shown as a function, not silently applied.
    d=np.linspace(-.7,.4,600)
    fig,ax=plt.subplots(figsize=(8.3,3.6),layout="constrained")
    ax.plot(d,(d>=0).astype(float),label="原硬遮挡",color="#5e6670",lw=1.5)
    ax.plot(d,m.smoothstep(-.5,0,d),label="仅向遮挡侧外扩0.5格",color="#c17421",lw=2)
    ax.axvline(0,color="#aaaaaa",lw=.8,linestyle="--")
    ax.set_xlabel("到切面的有符号距离（格）：负值为遮挡侧",fontproperties=FONT)
    ax.set_ylabel("可见性 V",fontproperties=FONT)
    ax.set_ylim(-.05,1.1);ax.legend(prop=FONT)
    ax.set_title("单侧距离羽化：亮侧保持1，过渡全部放在影侧",fontproperties=FONT)
    fig.savefig(OUT/"one-sided-cut-feather-profile.png",dpi=140)
    plt.close(fig)
    # Additional proposal preview: ONLY the already diagnosed endpoint cut.
    # Signed distance to its shadow ray, with an analytic air-path guard. This
    # is not a generic production shadow-boundary extractor or a per-light cost
    # guarantee. None of the twelve original-stage pictures uses this result.
    width=.5
    q=m.START+m.TANGENT*.01+m.NORMALS[0]*.003
    direction=m.WALL[0]-q;direction/=np.linalg.norm(direction)
    normal=np.array([-direction[1],direction[0]])
    relative=points-m.WALL[0]
    shadow_distance=relative@normal
    along_cut=relative@direction
    foot=points-shadow_distance[...,None]*normal-normal*.0001
    guard=m.visibility(foot,points)>0
    band=(~rock)&guard&(shadow_distance>0)&(shadow_distance<width)&(along_cut>0)
    proposed_visibility=np.where(band,np.maximum(vis,1-m.smoothstep(0,width,shadow_distance)),vis)
    proposed_bounce=unblocked*proposed_visibility
    proposed_raw=direct+proposed_bounce
    proposed_capped=np.where(rock,np.minimum(.65,proposed_raw),proposed_raw)
    proposed_fraction=np.divide(direct,proposed_raw,out=np.zeros_like(raw),where=proposed_raw>1e-12)
    proposed_relief=1+proposed_fraction*((.55+.65*.7/np.sqrt(1+.7*.7))-1)*.22
    proposed_display=t.srgb(np.clip(.18*(.10+np.where(depth_valid,proposed_capped*proposed_relief,0)[...,None]*WARM),0,1))
    fig,axes=plt.subplots(1,2,figsize=(11.6,5.8),layout="constrained")
    for ax,img,title in zip(axes,[display,proposed_display],["原算法：切面硬裁","只对主切面向影侧羽化0.5格"]):
        ax.imshow(img,extent=RECT,origin="lower",interpolation="nearest")
        decorate(ax,11)
        ax.set_title(title,fontproperties=FONT,fontsize=14)
    fig.suptitle("针对切面的单侧羽化示意：原圆形范围／亮侧／直射保持\n仅定位本场景已确认的主切面；离线方案示意，非游戏实现",fontproperties=FONT,fontsize=15)
    fig.savefig(OUT/"one-sided-cut-feather-scene.png",dpi=155)
    plt.close(fig)
    feather_checks=dict(width_cells=width,
        all_reflection_changes_nonnegative=bool(np.min(proposed_bounce-bounce)>=-1e-12),
        outside_band_max_error=float(np.max(np.abs(proposed_bounce[~band]-bounce[~band]))),
        fully_visible_max_error=float(np.max(np.abs(proposed_bounce[vis>1-1e-9]-bounce[vis>1-1e-9]))),
        wall_max_error=float(np.max(np.abs(proposed_bounce[rock]-bounce[rock]))),
        scope="Only the dominant endpoint shadow cut is expanded, using signed ray distance and an air-path guard; no source projection or radial change.")
    (OUT/'one-sided-cut-feather-scene-result.json').write_text(json.dumps(feather_checks,ensure_ascii=False,indent=2),encoding="utf-8")
    sources=["ExplorationLighting.compute","WallDiffusePrepare.hlsl","WallDiffuseShape.hlsl","WallDiffuseCompose.hlsl","WallDiffuseVisibility.hlsl","ExplorationLighting.hlsl"]
    report=dict(scope="Current-original algorithm stages only; unchanged convex slope, standing/aim and parameters. CPU approximation, not captured game intermediate textures.",
        geometry=s.REFERENCE,parameters=t.C,profile=dict(start=m.START.tolist(),length=float(m.LENGTH),nodes=m.DENSITY.tolist(),mean=float(m.MEAN),weighted_wall_distance=float(m.WALL_DISTANCE)),
        stages=[dict(number=i+1,title=TITLES[i],explanation=DETAILS[i],image=f"original-pipeline-stage-{i+1:02}.png") for i in range(12)],
        checks=checks,source_sha256={name:hashlib.sha256((ROOT/'Game/Assets/DarkNights/Res/Shared/Lighting'/name).read_bytes()).hexdigest() for name in sources},
        limitations=["Analytic convex intersection replaces game grid DDA and fixed-step direct traversal.",
            "One selected reflecting wall segment; the eight-wall merge is the identity in this example, and there is one lamp.",
            "Stages are logical quantities computed for illustration. Several exist only in registers in the game, not as separate textures/passes.",
            "High-resolution scalar fields show the existing formulas, not the game's 384x216 half-float storage/reconstruction.",
            "Final display uses uniform albedo .18, a flat normal, analytic wall depth and no authored textures; real normal maps and exact material reconstruction are not reproduced.",
            "Local-fill affects explicitly bound sprites, not this environment field; the zero near-intensity and continuous mode skip near/legacy branches.",
            "One-sided feather profile is a proposed mask function only. It is not applied in any of the twelve original-stage images; boundary-distance construction and wall barriers remain implementation work."])
    (OUT/'original-pipeline-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    np.savez_compressed(OUT/'original-pipeline-fields.npz',**fields)
    lines=["# 原始光照形成过程（离线分解）","","沿用先前站位与斜墙，所有图均是原始算法，不含新方案。","","| 序号 | 步骤 | 形成方式 |","| --- | --- | --- |"]
    for i in range(12):
        lines.append(f"| {i+1} | {TITLES[i]} | {DETAILS[i]} |")
    lines.extend(["","游戏写入384×216光场并进行几何约束插值；离线图使用高精度场展示各数学分项，不冒充实际GPU中间纹理。"])
    (OUT/'original-pipeline-explanation.md').write_text("\n".join(lines)+"\n",encoding="utf-8")
    print(json.dumps(dict(original=checks,cut_feather=feather_checks),ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
