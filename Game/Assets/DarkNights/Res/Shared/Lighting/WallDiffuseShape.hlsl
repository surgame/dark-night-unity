#ifndef DN_WALL_DIFFUSE_SHAPE
#define DN_WALL_DIFFUSE_SHAPE
#include "WallDiffuseProfile.hlsl"

float DNTriangleCoverage(float along,float length,float radius)
{
    float a=clamp(along/radius,-1,1),b=clamp((along-length)/radius,-1,1);
    float ca=a<0?.5*(a+1)*(a+1):1-.5*(1-a)*(1-a);
    float cb=b<0?.5*(b+1)*(b+1):1-.5*(1-b)*(1-b);
    return max(0,ca-cb);
}

// 每个线性区间用两点高斯积分；采样节点只定义连续密度，不产生独立亮心。
float2 DNWallRadial(int index,DNWallProfile wall,float along,float normal,float radius)
{
    if(abs(normal)>=radius || along<=-radius || along>=wall.shape.z+radius) return 0;
    float step=wall.shape.z/32, mass=0, weight=0;
    float support=sqrt(max(0,radius*radius-normal*normal));
    int first=max(0,(int)floor((along-support)/max(step,1e-6)));
    int last=min(31,(int)floor((along+support)/max(step,1e-6)));
    [loop] for(int j=first;j<=last;j++)
    {
        float a=_DNWallNodes[index*33+j],b=_DNWallNodes[index*33+j+1];
        for(int sample=0;sample<2;sample++)
        {
            float fraction=sample==0?.2113248654:.7886751346;
            float offset=(j+fraction)*step-along;
            float kernel=1-smoothstep(0,radius,sqrt(offset*offset+normal*normal));
            float w=kernel*step*.5;
            mass+=lerp(a,b,fraction)*w; weight+=w;
        }
    }
    if(mass<=1e-8 || weight<=1e-8) return 0;
    return float2(mass/weight,saturate(weight/max(min(radius,wall.shape.z),1e-6)));
}

float2 DNWallShape(int lamp,int index,DNWallProfile wall,float along,float normal,out float probeRadius)
{
    float reach=wall.shape.w, length=wall.shape.z;
    probeRadius=reach;
    if(abs(normal)>=reach || along<=-reach || along>=length+reach) return 0;
    if((int)_DNDiffuseOptions[lamp].z==1) return DNWallRadial(index,wall,along,normal,reach);
    float2 band=0;
    float radius=min(.65*reach,.03+length*(.035+_DNDiffuseOptions[lamp].x*smoothstep(0,.8*reach,max(0,normal))));
    probeRadius=radius;
    bool surface=normal<0 && DNSolid(wall.frame.xy+wall.frame.zw*clamp(along,0,length)+wall.shape.xy*normal);
    if((normal>=0 || surface) && abs(normal)<reach && along>-radius && along<length+radius)
    {
        float coverage=DNTriangleCoverage(along,length,radius);
        float integrated=0;
        DNWallLocal(index,wall,along,radius,integrated);
        band=float2(coverage>1e-6?integrated/coverage:0,coverage*(1-smoothstep(0,reach,abs(normal))));
    }
    int flags=(int)wall.stats.w;
    float corner=min(reach,_DNLightBounce[lamp].z);
    if(flags!=0 && corner>0)
    {
        float joinWeight=0;
        if((flags&1)!=0) joinWeight=max(joinWeight,1-smoothstep(corner*.5,corner,abs(along)));
        if((flags&2)!=0) joinWeight=max(joinWeight,1-smoothstep(corner*.5,corner,abs(along-length)));
        if(joinWeight>0 && band.y<1)
        {
            float2 rounded=DNWallRadial(index,wall,along,normal,corner);
            float added=rounded.y*joinWeight*(1-band.y), coverage=band.y+added;
            band=float2(coverage>1e-6?(band.x*band.y+rounded.x*added)/coverage:0,coverage);
            probeRadius=lerp(radius,max(radius,corner),coverage>1e-6?added/coverage:0);
        }
    }
    return band;
}
#endif
