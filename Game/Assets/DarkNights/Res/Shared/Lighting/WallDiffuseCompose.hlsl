#ifndef DN_WALL_DIFFUSE_COMPOSE
#define DN_WALL_DIFFUSE_COMPOSE
#include "WallDiffuseProfile.hlsl"
#include "WallDiffuseVisibility.hlsl"
float DNPositiveExcess(float target,float direct)
{
    float x=target-direct, epsilon=.04*target;
    if(target<=0 || x<=0) return 0;
    return x<epsilon?x*x/(2*epsilon):x-epsilon*.5;
}
float2 DNWallContribution(int index,float2 p,float direct)
{
    DNWallProfile wall=_DNWallProfiles[index];
    float reach=wall.shape.w;
    if(reach<=0 || wall.stats.x<=0) return 0;
    float2 delta=p-wall.frame.xy;
    float normal=dot(delta,wall.shape.xy), along=dot(delta,wall.frame.zw), length=wall.shape.z;
    if(normal<0 || normal>=reach) return 0;
    float radius=min(.65*reach,.03+length*(.035+_DNDiffuseSpread*smoothstep(0,.8*reach,normal)));
    if(radius<=0 || along<=-radius || along>=length+radius) return 0;
    float local=DNWallLocal(index,wall,along,radius);
    float harmonic=2*local*wall.stats.x/max(local+wall.stats.x,1e-6);
    float target=_DNDiffuseSettings.x*(.80*local+.20*harmonic);
    float excess=DNPositiveExcess(target,direct);
    if(excess<=0) return 0; // 无补光余量时不查询遮挡。
    float coverage=smoothstep(-radius,0,along)*(1-smoothstep(length,length+radius,along));
    float fade=(1-smoothstep(0,reach,normal))*wall.stats.y;
    float visibility=0;
    // 探针只近似可见性，积分、覆盖及衰减与探针数量无关。
    for(int probe=0;probe<_DNDiffuseProbes;probe++)
    {
        float offset=radius*(2*((probe+.5)/_DNDiffuseProbes)-1);
        float at=clamp(along+offset,0,length);
        float2 emitter=wall.frame.xy+wall.frame.zw*at+wall.shape.xy*.003;
        visibility+=DNExactVisibility(emitter,p);
    }
    visibility/=_DNDiffuseProbes;
    return float2(excess,saturate(coverage*visibility*fade)*excess);
}
float DNWallDiffuse(int lamp,float2 p,float direct)
{
    if(_DNDiffuseSettings.x<=0 || DNSolid(p)) return 0;
    float2 one=DNWallContribution(lamp*2,p,direct), two=DNWallContribution(lamp*2+1,p,direct);
    float bound=max(one.x,two.x);
    return bound>1e-6?one.y+two.y-one.y*two.y/bound:0;
}
#endif
