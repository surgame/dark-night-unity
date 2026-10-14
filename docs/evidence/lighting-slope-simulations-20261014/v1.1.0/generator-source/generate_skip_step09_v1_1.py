"""Versioned experiment: original reflection visibility (step 09) fixed to one."""
from pathlib import Path
from datetime import datetime, timezone, timedelta
import argparse
import importlib.util
import hashlib
import json
import shutil
import platform
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

CN=timezone(timedelta(hours=8))
VERSION="v1.1.0"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def repository():
    for path in Path(__file__).resolve().parents:
        if (path/"Game/Assets/DarkNights").is_dir() and (path/"docs").is_dir():
            return path
    raise RuntimeError("Repository root not found")


def main():
    root=repository()
    parser=argparse.ArgumentParser()
    parser.add_argument("--output",type=Path,default=root/"docs/evidence/lighting-slope-simulations-20261014"/VERSION)
    output=parser.parse_args().output.resolve()
    if output.exists():
        raise RuntimeError("Version/output already exists; choose a fresh output, never overwrite evidence")
    frozen=root/"docs/evidence/lighting-slope-simulations-20261014/v1.0.0"
    baseline_manifest=json.loads((frozen/"manifest.json").read_text(encoding="utf-8"))
    for entry in baseline_manifest["files"]:
        if sha(frozen/entry["path"])!=entry["sha256"]:
            raise RuntimeError("Frozen v1.0.0 identity mismatch")
    spec=importlib.util.spec_from_file_location("frozen_original",frozen/"generator-source/render_original_light_pipeline.py")
    a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
    a.s.configure(a.s.REFERENCE,{})
    m,t=a.m,a.t
    generated=datetime.now(CN)
    x=np.linspace(a.RECT[0],a.RECT[1],a.W)
    y=np.linspace(a.RECT[2],a.RECT[3],a.H)
    points=np.stack(np.meshgrid(x,y),-1)
    direct=t.direct(points)[0]
    delta=points-m.START
    local,coverage,radius=t.shape(points,delta@m.TANGENT,delta@m.NORMALS[0])
    target=np.where((local>0)&(coverage>0),t.C["gain"]*(.8*local+.2*m.MEAN),0)
    unblocked=m.positive_excess(target,direct)*coverage
    vis,_,_=a.visibility(points,radius)
    original_bounce=unblocked*vis
    # Sole experimental change: V=1 for reflection. No extra air-path mask.
    skip_bounce=unblocked.copy()
    rock=m.solid(points)
    signed=np.sum((points[...,None,:]-m.WALL)*m.NORMALS,axis=-1)
    depth=-np.max(signed,axis=-1)
    depth_valid=(~rock)|(depth<=m.WALL_DEPTH)

    def display(returned):
        raw=direct+returned
        capped=np.where(rock,np.minimum(.65,raw),raw)
        fraction=np.divide(direct,raw,out=np.zeros_like(raw),where=raw>1e-12)
        relief=1+fraction*((.55+.65*.7/np.sqrt(1+.7*.7))-1)*.22
        image=t.srgb(np.clip(.18*(.10+np.where(depth_valid,capped*relief,0)[...,None]*a.WARM),0,1))
        return capped,image

    q=m.START+m.TANGENT*.01+m.NORMALS[0]*.003
    edge=m.WALL[0]-q;edge/=np.linalg.norm(edge)
    normal=np.array([-edge[1],edge[0]])
    relative=points-m.WALL[0]
    distance=relative@normal
    foot=points-distance[...,None]*normal-normal*.0001
    band=(~rock)&(m.visibility(foot,points)>0)&(distance>0)&(distance<.5)&((relative@edge)>0)
    feather_vis=np.where(band,np.maximum(vis,1-m.smoothstep(0,.5,distance)),vis)
    feather_bounce=unblocked*feather_vis
    original_field,original_image=display(original_bounce)
    feather_field,feather_image=display(feather_bounce)
    skip_field,skip_image=display(skip_bounce)
    check_original=a.s.evaluate(points)[0]
    checks=dict(original_field_matches_v1_0_0_max_error=float(np.max(np.abs(original_field-check_original))),
        step09_bypass_identity_error=float(np.max(np.abs(skip_bounce-unblocked))),
        all_reflection_changes_nonnegative=bool(np.min(skip_bounce-original_bounce)>=-1e-12),
        finite=bool(np.all(np.isfinite(skip_image))),deep_wall_display_preserves_ambient=bool(np.max(np.abs(skip_image[rock&~depth_valid]-t.srgb(np.array(.018)))))<1e-12)
    shadow=(~rock)&(vis<1e-9)&(unblocked>.01)
    statistics=dict(affected_previously_dark_air_pixels=int(np.count_nonzero(shadow)),
        max_air_reflection_increase=float(np.max((skip_bounce-original_bounce)[~rock])),
        mean_increase_in_previously_occluded_supported_air=float(np.mean((skip_bounce-original_bounce)[shadow])),
        retained_wall_cap=.65,retained_wall_depth=.375)
    output.mkdir(parents=True)
    clock=generated.strftime("%Y-%m-%d %H:%M:%S +08:00")
    fig,axes=plt.subplots(1,3,figsize=(16.2,5.7),layout="constrained")
    for ax,img,title in zip(axes,[original_image,feather_image,skip_image],
            ["A 原算法：V按探针遮挡","B v1.0.0：主切面外扩0.5格","C v1.1.0：取消第9步，V=1"]):
        ax.imshow(img,extent=a.RECT,origin="lower",interpolation="nearest")
        a.decorate(ax,11);ax.set_title(title,fontproperties=a.FONT,fontsize=13)
    fig.suptitle(f"{VERSION}  生成于 {clock}\n同站位／墙形／直射／增益／半径；取消的仅是返光可见性裁切",fontproperties=a.FONT,fontsize=15)
    fig.supxlabel("离线近似；直射阴影、墙表层深度、墙内亮度上限保留；无模糊、颗粒或曝光调整",fontproperties=a.FONT,fontsize=10)
    fig.savefig(output/"skip-step09-comparison.png",dpi=150);plt.close(fig)
    fig,axes=plt.subplots(2,2,figsize=(11.6,10.8),layout="constrained")
    rows=[(vis,"原第9步：探针V",True),(np.ones_like(vis),"本次第9步：V固定为1",True),
          (original_bounce,"原返光：B=U×V",False),(skip_bounce,"取消第9步：B=U",False)]
    for ax,(data,title,is_mask) in zip(axes.flat,rows):
        ax.imshow(data,extent=a.RECT,origin="lower",interpolation="nearest",cmap="gray" if is_mask else "inferno",vmin=0,vmax=1 if is_mask else 4)
        a.decorate(ax,11);ax.set_title(title,fontproperties=a.FONT,fontsize=13)
    fig.suptitle(f"{VERSION}  第9步取消前后的分项\n黑白图0—1；返光图0—4；墙体此时只画轮廓，最终仍有限深和限亮",fontproperties=a.FONT,fontsize=15)
    fig.savefig(output/"skip-step09-components.png",dpi=145);plt.close(fig)
    examples=np.array([[-1.25,-1.],[-3.5,-3.5],[-.75,1.1],[1.8,-1.5]])
    d=t.direct(examples)[0]
    e=examples-m.START
    i,c,r=t.shape(examples,e@m.TANGENT,e@m.NORMALS[0])
    e0=m.positive_excess(np.where((i>0)&(c>0),2*(.8*i+.2*m.MEAN),0),d)*c
    v=a.visibility(examples,r)[0]
    report=dict(version=VERSION,generated_at=generated.isoformat(timespec="seconds"),timezone="Asia/Shanghai",
        input_version="v1.0.0",input_manifest_sha256=sha(frozen/"manifest.json"),geometry=a.s.REFERENCE,parameters=t.C,
        sole_change="Step09 reflected-light visibility V is fixed to 1; B=U instead of U*V, for both air and wall texels. Final wall receiver limits are still applied.",
        retained=["Original direct lighting and its shadows","Wall incident preparation and 33 nodes","Original SoftBlob target/radius/coverage and positive-excess blend","Distance fade","WallDepth .375 and WallStrength .65","Ambient .1, Relief .22, fixed uniform albedo .18 and linear lamp color"],
        checks=checks,statistics=statistics,
        sample_points=[dict(position=p.tolist(),direct=float(dd),original_return=float(uu*vv),without_step09_return=float(uu),original_visibility=float(vv)) for p,dd,uu,vv in zip(examples,d,e0,v)],
        limitations=baseline_manifest.get("limitations",[])+[
            "Single convex slope CPU approximation, not a Unity frame or reconstructed authored textures.",
            "Removing reflected visibility permits reflected energy at originally occluded air points. Final wall-depth clipping does not restore missing source-to-receiver occlusion.",
            "This experiment evaluates the requested bypass only; no performance, multi-lamp or neighboring-cave acceptance is claimed."],
        display=dict(rect=a.RECT,width=a.W,height=a.H,albedo=.18,ambient=.1,exposure=1,post_blur=False))
    (output/"result.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    source_dir=output/"generator-source";source_dir.mkdir()
    shutil.copy2(Path(__file__),source_dir/"generate_skip_step09_v1_1.py")
    finished=datetime.now(CN).isoformat(timespec="seconds")
    files=[dict(path=str(p.relative_to(output)).replace("\\","/"),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(output.rglob("*")) if p.is_file()]
    manifest=dict(version=VERSION,generated_at=report["generated_at"],recorded_at=finished,timezone="Asia/Shanghai",
        input_version="v1.0.0",input_manifest_sha256=sha(frozen/"manifest.json"),files=files,
        runtime=dict(python=platform.python_version(),numpy=np.__version__,matplotlib=matplotlib.__version__,font="C:/Windows/Fonts/msyh.ttc"),
        rebuild="Run generator-source/generate_skip_step09_v1_1.py --output <fresh-directory> within the same repository. Frozen v1.0.0 inputs are verified first; existing outputs are never overwritten.")
    (output/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(dict(version=VERSION,generated_at=report["generated_at"],recorded_at=finished,checks=checks,statistics=statistics,sample_points=report["sample_points"]),ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
