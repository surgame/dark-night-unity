"""Replace reflected path visibility with a destination rock mask, offline only."""
from pathlib import Path
from datetime import datetime, timezone, timedelta
import argparse
import importlib.util
import hashlib
import json
import platform
import shutil
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

VERSION="v1.2.0"
CN=timezone(timedelta(hours=8))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    root=next(p for p in Path(__file__).resolve().parents if (p/"Game/Assets/DarkNights").is_dir())
    package=root/"docs/evidence/lighting-slope-simulations-20261014"
    parser=argparse.ArgumentParser()
    parser.add_argument("--output",type=Path,default=package/VERSION)
    output=parser.parse_args().output.resolve()
    if output.exists():
        raise RuntimeError("Choose a fresh output directory; never overwrite a version")
    for version in ["v1.0.0","v1.1.0"]:
        manifest=json.loads((package/version/"manifest.json").read_text(encoding="utf-8"))
        for entry in manifest["files"]:
            if sha(package/version/entry["path"])!=entry["sha256"]:
                raise RuntimeError("Frozen input identity mismatch: "+version+"/"+entry["path"])
    spec=importlib.util.spec_from_file_location("frozen_v1",package/"v1.0.0/generator-source/render_original_light_pipeline.py")
    a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
    a.s.configure(a.s.REFERENCE,{})
    m,t=a.m,a.t
    started=datetime.now(CN)
    x=np.linspace(a.RECT[0],a.RECT[1],a.W);y=np.linspace(a.RECT[2],a.RECT[3],a.H)
    points=np.stack(np.meshgrid(x,y),-1)
    direct=t.direct(points)[0]
    delta=points-m.START
    local,c,r=t.shape(points,delta@m.TANGENT,delta@m.NORMALS[0])
    target=np.where((local>0)&(c>0),2*(.8*local+.2*m.MEAN),0)
    u=m.positive_excess(target,direct)*c
    original_v=a.visibility(points,r)[0]
    original_b=u*original_v
    skip_b=u.copy()
    rock=m.solid(points)
    # All sampled cells are known in this convex reference. The production
    # counterpart would also reject unknown cells: DNKnown(p) && !DNSolid(p).
    wall_mask=(~rock).astype(float)
    wall_b=u*wall_mask
    depth=-np.max(np.sum((points[...,None,:]-m.WALL)*m.NORMALS,axis=-1),axis=-1)
    depth_valid=(~rock)|(depth<=m.WALL_DEPTH)

    def display(b):
        total=direct+b
        capped=np.where(rock,np.minimum(.65,total),total)
        fraction=np.divide(direct,total,out=np.zeros_like(total),where=total>1e-12)
        relief=1+fraction*((.55+.65*.7/np.sqrt(1+.7*.7))-1)*.22
        return t.srgb(np.clip(.18*(.10+np.where(depth_valid,capped*relief,0)[...,None]*a.WARM),0,1))

    images=[display(original_b),display(skip_b),display(wall_b)]
    checks=dict(wall_reflected_energy_max=float(np.max(wall_b[rock])),
        air_difference_vs_v1_1_0_max=float(np.max(np.abs(wall_b[~rock]-skip_b[~rock]))),
        raw_wall_energy_matches_direct_max_error=float(np.max(np.abs((direct+wall_b)[rock]-direct[rock]))),
        original_return_matches_frozen_reference_error=float(np.max(np.abs(original_b-a.s.evaluate(points)[2]))),
        finite=all(bool(np.all(np.isfinite(img))) for img in images))
    output.mkdir(parents=True)
    stamp=started.strftime("%Y-%m-%d %H:%M:%S +08:00")
    fig,axes=plt.subplots(1,3,figsize=(16.2,5.7),layout="constrained")
    for ax,img,title in zip(axes,images,["A 原算法：返光投影遮挡","B v1.1.0：V=1，仅保留最终墙限深","C v1.2.0：只裁实际岩体中的返光"]):
        ax.imshow(img,extent=a.RECT,origin="lower",interpolation="nearest")
        a.decorate(ax,11);ax.set_title(title,fontproperties=a.FONT,fontsize=12)
    fig.suptitle(f"{VERSION}  生成于 {stamp}\n返光遮罩从投影切面改为实际岩体：保留直射及墙面直射，不裁空气中的圆形返光",fontproperties=a.FONT,fontsize=15)
    fig.supxlabel("离线近似；B返光=U×墙体遮罩，已知空气为1、实际岩体为0；未修改正式游戏",fontproperties=a.FONT,fontsize=10)
    fig.savefig(output/"wall-mask-comparison.png",dpi=150);plt.close(fig)
    fig,axes=plt.subplots(2,2,figsize=(11.6,10.8),layout="constrained")
    for ax,(data,title,mask) in zip(axes.flat,[(original_v,"原⑨：源到目标可见性V",True),
        (wall_mask,"新⑨：实际墙体遮罩M",True),(u,"遮挡前返光U",False),(wall_b,"新返光B=U×M",False)]):
        ax.imshow(data,extent=a.RECT,origin="lower",interpolation="nearest",cmap="gray" if mask else "inferno",vmin=0,vmax=1 if mask else 4)
        a.decorate(ax,11);ax.set_title(title,fontproperties=a.FONT,fontsize=13)
    fig.suptitle(f"{VERSION}  投影遮挡与墙体遮罩分开\n白=空气可保留、黑=裁掉；直射D另走原路径，墙面不会被整幅画面遮罩抹黑",fontproperties=a.FONT,fontsize=15)
    fig.savefig(output/"wall-mask-components.png",dpi=145);plt.close(fig)
    report=dict(version=VERSION,generated_at=started.isoformat(timespec="seconds"),timezone="Asia/Shanghai",
        geometry=a.s.REFERENCE,parameters=t.C,
        formula="Reflected B(p)=U(p)*M(p), where M=1 for known air and M=0 for actual rock/unknown; original direct D is computed and shaded separately.",
        change_from_v1_1_0="Restore an explicit destination wall mask on reflected energy; do not restore source-to-receiver ray visibility or its projected cutoff.",
        retained=["Original direct light and its shadows including wall-surface direct contribution","Wall incident preparation and original radial reflection distribution","Wall-depth .375 and wall-strength .65 final limits","Fixed ambient, relief, uniform albedo and exposure"],
        checks=checks,
        consequences=["In this same scene, all air reflected values match v1.1.0 exactly; the difference is reflected energy inside rock is zero.",
            "Direct illumination on walls remains. Indirect illumination inside wall material is intentionally zero in this variant; preserving a separate wall indirect layer would be a different variant.",
            "A destination rock mask does not encode visibility from the original reflected source. Air on the far side of thin/other walls may still receive the radial field; this scene is not a neighboring-cave or multi-light acceptance."],
        limitations=["One convex slope and one selected reflecting wall in a CPU approximation; no authored texture, full material normals, Unity execution or GPU timing.",
            "The known map domain is assumed for the whole pictured rectangle; production unknown-cell rejection is described, not exercised here."],
        input_manifest_sha256={v:sha(package/v/"manifest.json") for v in ["v1.0.0","v1.1.0"]})
    (output/"result.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (output/"generator-source").mkdir()
    shutil.copy2(Path(__file__),output/"generator-source/generate_wall_mask_v1_2.py")
    finished=datetime.now(CN).isoformat(timespec="seconds")
    files=[dict(path=str(p.relative_to(output)).replace("\\","/"),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(output.rglob("*")) if p.is_file()]
    manifest=dict(version=VERSION,generated_at=report["generated_at"],recorded_at=finished,timezone="Asia/Shanghai",
        input_manifest_sha256=report["input_manifest_sha256"],files=files,
        runtime=dict(python=platform.python_version(),numpy=np.__version__,matplotlib=matplotlib.__version__,font="C:/Windows/Fonts/msyh.ttc"),
        rebuild="Run generator-source/generate_wall_mask_v1_2.py --output <fresh-directory> in the same repository; existing outputs are refused and frozen inputs are verified.")
    (output/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(dict(version=VERSION,generated_at=report["generated_at"],recorded_at=finished,checks=checks),ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
