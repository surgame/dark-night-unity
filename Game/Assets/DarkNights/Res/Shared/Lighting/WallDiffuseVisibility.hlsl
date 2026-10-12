#ifndef DN_WALL_DIFFUSE_VISIBILITY
#define DN_WALL_DIFFUSE_VISIBILITY
#include "ExplorationOcclusion.hlsl"
#ifdef DN_QUERY_COUNTERS
RWStructuredBuffer<uint> _DNDiffuseQueries;
int _DNDiffuseCountQueries, _DNDiffuseQueryStage;
#endif
// 新路径的格 DDA。格内岩体为一个凸半平面，沿线段解析求交；不以固定步长掠过薄坡角。
float DNShapeSigned(float2 p, int2 cell, int shape)
{
    float x=p.x-cell.x, y=1-(p.y-cell.y);
    bool ceiling=shape>=7; int s=ceiling?shape-6:shape;
    float edge=s==1?x:s==2?1-x:s==3?x*.5:s==4?.5+x*.5:s==5?1-x*.5:.5-x*.5;
    return ceiling?y-edge:edge-y;
}
float DNExactSameSide(float2 origin, float2 target, bool solidSide)
{
#ifdef DN_QUERY_COUNTERS
    if(_DNDiffuseCountQueries!=0) InterlockedAdd(_DNDiffuseQueries[_DNDiffuseQueryStage],1);
#endif
    if (!DNKnown(origin) || !DNKnown(target) || DNSolid(origin)!=solidSide || DNSolid(target)!=solidSide) return 0;
    float2 delta=target-origin;
    if (dot(delta,delta)<1e-10) return 1;
    int2 cell=(int2)floor(origin), step=int2(delta.x>0?1:-1,delta.y>0?1:-1);
    float2 dt=1/max(abs(delta),1e-12);
    float2 edge=cell+float2(delta.x>0?1:0,delta.y>0?1:0);
    float2 next=(edge-origin)/float2(abs(delta.x)>1e-12?delta.x:1e-12,abs(delta.y)>1e-12?delta.y:1e-12);
    if(abs(delta.x)<1e-12) next.x=1e20;
    if(abs(delta.y)<1e-12) next.y=1e20;
    float entered=0;
    for(int i=0;i<512;i++)
    {
        float exited=min(1,min(next.x,next.y));
        if (!DNKnown(cell+.5)) return 0;
        float4 data=_DNLightCells.Load(int3(cell,0));
        if(data.a<.5) return 0;
        if(solidSide && data.r<=.001) return 0;
        if(data.r>.001 && exited-entered>1e-7)
        {
            int shape=(int)round(data.g*255);
            if(shape==0 || shape>12) { if(!solidSide) return 0; }
            else
            {
                float first=DNShapeSigned(origin+delta*entered,cell,shape);
                float last=DNShapeSigned(origin+delta*exited,cell,shape);
                if(solidSide ? min(first,last)<-1e-6 : max(first,last)>1e-6) return 0;
            }
        }
        if(exited>=1) return 1;
        bool crossX=next.x<=next.y, crossY=next.y<=next.x;
        if(crossX) { cell.x+=step.x; next.x+=dt.x; }
        if(crossY) { cell.y+=step.y; next.y+=dt.y; }
        entered=exited;
    }
    return 0; // 达到预算仍未到达目标时保守拒绝。
}
float DNExactVisibility(float2 origin,float2 target) { return DNExactSameSide(origin,target,false); }
#endif
