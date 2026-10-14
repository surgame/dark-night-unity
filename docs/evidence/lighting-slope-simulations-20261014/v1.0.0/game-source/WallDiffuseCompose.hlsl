#ifndef DN_WALL_DIFFUSE_COMPOSE
#define DN_WALL_DIFFUSE_COMPOSE
#include "WallDiffuseProfile.hlsl"
#include "WallDiffuseVisibility.hlsl"
#include "WallDiffuseShape.hlsl"
float DNPositiveExcess(float target,float direct)
{
    float x=target-direct, epsilon=.04*target;
    if(target<=0 || x<=0) return 0;
    return x<epsilon?x*x/(2*epsilon):x-epsilon*.5;
}
float2 DNWallContribution(int lamp,int index,float2 p,float direct)
{
    DNWallProfile wall=_DNWallProfiles[index];
    float reach=wall.shape.w;
    if(reach<=0 || wall.stats.x<=0) return 0;
    // 连续非负核的归一化局部照度不超过节点最大值；留浮点余量，直接光已覆盖目标时省去积分与探针。
    float upper=_DNDiffuseSettings[lamp].x*max(wall.stats.x,wall.stats.z);
    if(direct>=upper*(1+1e-6)+1e-6) return 0;
    float2 delta=p-wall.frame.xy;
    float normal=dot(delta,wall.shape.xy), along=dot(delta,wall.frame.zw), length=wall.shape.z;
    float radius=0;
    float2 field=DNWallShape(lamp,index,wall,along,normal,radius);
    float local=field.x;
    if(local<=0 || field.y<=0) return 0;
    float harmonic=2*local*wall.stats.x/max(local+wall.stats.x,1e-6);
    float target=_DNDiffuseSettings[lamp].x*(.80*local+.20*((int)_DNDiffuseOptions[lamp].w!=0?wall.stats.x:harmonic));
    float excess=DNPositiveExcess(target,direct);
    if(excess<=0) return 0; // 无补光余量时不查询遮挡。
    float visibility=0, visibilityWeight=0;
    float nearestAt=clamp(along,min(.01,length*.25),length-min(.01,length*.25)), nearestVisibility=0;
    // 探针只近似可见性，积分、覆盖及衰减与探针数量无关。
    [loop] for(int probe=0;probe<(int)_DNDiffuseOptions[lamp].y;probe++)
    {
        // 首探针固定到最近表面，第二探针向受光段内部平滑移动，避免两端都被坡角遮住却漏掉中间可见区。
        float offset=probe==0?0:probe==1?clamp((length*.5-along)*.5,-radius*.65,radius*.65):
            radius*(probe==2?-.65:.65);
        float inset=min(.01,length*.25);
        float at=clamp(along+offset,inset,length-inset);
        float2 emitter=wall.frame.xy+wall.frame.zw*at+wall.shape.xy*.003;
        float2 toPoint=p-emitter;
        float probeWeight=1/(.04+dot(toPoint,toPoint));
        float visible=0;
        if(probe>0 && at==nearestAt) visible=nearestVisibility;
        else visible=DNExactWallVisibility(emitter,p);
        if(probe==0) nearestVisibility=visible;
        visibility+=visible*probeWeight;
        visibilityWeight+=probeWeight;
    }
    visibility/=max(visibilityWeight,1e-6);
    return float2(excess,saturate(field.y*visibility*wall.stats.y)*excess);
}
float DNWallDiffuse(int lamp,float2 p,float direct)
{
    if((int)_DNLightBounce[lamp].x!=2 || _DNDiffuseSettings[lamp].x<=0 || _DNDiffuseSettings[lamp].w<=0 || !DNKnown(p)) return 0;
    if(DNSolid(p) && _DNShadowSettings.y<=0) return 0;
    float2 contributions[8]; float bound=0;
    [unroll] for(int slot=0;slot<8;slot++) contributions[slot]=0;
    for(int contributionSlot=0;contributionSlot<8;contributionSlot++)
    {
        contributions[contributionSlot]=DNWallContribution(lamp,lamp*8+contributionSlot,p,direct);
        bound=max(bound,contributions[contributionSlot].x);
    }
    if(bound<=1e-6) return 0;
    float remaining=1;
    for(int mergeSlot=0;mergeSlot<8;mergeSlot++) remaining*=1-saturate(contributions[mergeSlot].y/bound);
    return bound*(1-remaining);
}
#endif
