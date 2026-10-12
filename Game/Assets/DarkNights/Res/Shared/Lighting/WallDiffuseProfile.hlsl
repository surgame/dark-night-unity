#ifndef DN_WALL_DIFFUSE_PROFILE
#define DN_WALL_DIFFUSE_PROFILE
#define DN_WALL_CANDIDATES 64
#define DN_WALL_NODES 33
struct DNWallCandidate { float4 ends; float4 normal; };
struct DNWallProfile
{
    float4 frame; // start.xy, unit tangent.xy
    float4 shape; // air normal.xy, lit length, normal reach
    float4 stats; // mean incident, distance fade, quality integral, reserved
};
RWStructuredBuffer<DNWallProfile> _DNWallProfiles;
RWStructuredBuffer<float> _DNWallNodes;
float4 _DNDiffuseSettings; // Gain / Near / Far / MaxReach
float _DNDiffuseSpread;
int _DNDiffuseProbes;
int _DNDiffuseBenchmark; // 开发 A1 四区间常量分布，不进入场景配置。

// 线性照度乘线性三角核的精确区间积分；权重分母包含暗段与端点外的零照度支持域。
float DNIntegrateLinear(float x0,float x1,float p0,float p1,float center,float radius)
{
    float slope=(p1-p0)/max(x1-x0,1e-8), total=0;
    for(int side=0;side<2;side++)
    {
        float lo=max(x0,center+(side==0?-radius:0));
        float hi=min(x1,center+(side==0?0:radius));
        if(hi<=lo) continue;
        float span=hi-lo, incident=p0+slope*(lo-x0);
        float k=side==0?1+(lo-center)/radius:1-(lo-center)/radius;
        float ks=side==0?1/radius:-1/radius;
        total+=incident*k*span+(incident*ks+slope*k)*span*span*.5+slope*ks*span*span*span/3;
    }
    return total;
}
float DNWallLocal(int index,DNWallProfile profile,float along,float radius)
{
    if(_DNDiffuseBenchmark==1)
    {
        float total=0, interval=profile.shape.z/4;
        for(int bin=0;bin<4;bin++)
        {
            float value=_DNWallNodes[index*33+bin*8+4];
            total+=DNIntegrateLinear(bin*interval,(bin+1)*interval,value,value,along,radius);
        }
        return max(0,total/max(radius,1e-6));
    }
    float step=profile.shape.z/32, sum=0;
    int first=max(0,(int)floor((along-radius)/max(step,1e-6)));
    int last=min(31,(int)floor((along+radius)/max(step,1e-6)));
    for(int j=first;j<=last;j++)
        sum+=DNIntegrateLinear(j*step,(j+1)*step,_DNWallNodes[index*33+j],_DNWallNodes[index*33+j+1],along,radius);
    return max(0,sum/max(radius,1e-6));
}
#endif
