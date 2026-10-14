#ifndef DN_WALL_DIFFUSE_FEATHER
#define DN_WALL_DIFFUSE_FEATHER
RWTexture2DArray<float4> _DNFeatherRaw;
RWTexture2DArray<float4> _DNFeatherTemporary;
float4 _DNFeatherGroups[16]; // 单灯所属宽度层；-1保留原路径。
float4 _DNFeatherWidths[16]; // sigma（格）、10%—90%主要过渡宽度（格）。
int _DNFeatherCount;

#ifdef DN_FIELD_FILTER
// 每个双线性源像素分别核对空气路径；不让硬件插值跨薄墙，也不以照度差保护旧切线。
float4 DNFeatherSample(float2 coordinate,float2 p,int group)
{
    int2 base=(int2)floor(coordinate);
    float2 fraction=frac(coordinate);
    float3 sum=0;
    float accepted=0;
    for(int y=0;y<2;y++) for(int x=0;x<2;x++)
    {
        int2 index=base+int2(x,y);
        if(any(index<0) || any(index>=(int2)_DNTargetSize.xy)) continue;
        float weight=(x==0?1-fraction.x:fraction.x)*(y==0?1-fraction.y:fraction.y);
        if(weight<=1e-6) continue;
#ifdef DN_FEATHER_HORIZONTAL
        float4 value=_DNFeatherRaw[int3(index,group)];
#else
        float4 value=_DNFeatherTemporary[int3(index,group)];
#endif
        if(value.a<.5) continue;
        float2 q=_DNLightRect.xy+(index+.5)/_DNTargetSize.xy*_DNLightRect.zw;
        // 同格空气是凸区域，端点已知为空气即可跳过逐格查询。
        if(any(floor(p)!=floor(q)) && DNExactVisibility(p,q)<=0) continue;
        sum+=value.rgb*weight;
        accepted+=weight;
    }
    return float4(sum/max(accepted,1e-6),accepted>1e-6?1:0);
}

float3 DNFeatherFilter(uint2 index,float2 p,int group,float2 axis)
{
    float sigma=_DNFeatherWidths[group].x;
    float cellPerTexel=dot(_DNLightRect.zw/_DNTargetSize.xy,axis);
    // 九个有界高斯位置覆盖约±3 sigma，亚像素位置用上方几何约束重建。
    float step=max(cellPerTexel,.75*sigma);
    float3 sum=0;
    float accepted=0;
    [unroll] for(int tap=-4;tap<=4;tap++)
    {
        float distance=tap*step;
        float weight=exp(-.5*distance*distance/max(sigma*sigma,1e-12));
        if(weight<=1e-6) continue;
        float4 sample=DNFeatherSample(index+axis*(distance/cellPerTexel),p,group);
        sum+=sample.rgb*(weight*sample.a);
        accepted+=weight*sample.a;
    }
    return sum/max(accepted,1e-6);
}

[numthreads(8,8,1)]
void FeatherHorizontal(uint3 id:SV_DispatchThreadID)
{
    if(any(id.xy>=(uint2)_DNTargetSize.xy) || id.z>=(uint)_DNFeatherCount) return;
    float2 p=_DNLightRect.xy+(id.xy+.5)/_DNTargetSize.xy*_DNLightRect.zw;
    bool air=DNKnown(p) && !DNSolid(p);
    float3 value=0;
    if(air) value=DNFeatherFilter(id.xy,p,id.z,float2(1,0));
    _DNFeatherTemporary[id]=float4(value,air?1:0);
}

[numthreads(8,8,1)]
void FeatherCompose(uint3 id:SV_DispatchThreadID)
{
    if(any(id.xy>=(uint2)_DNTargetSize.xy)) return;
    float2 p=_DNLightRect.xy+(id.xy+.5)/_DNTargetSize.xy*_DNLightRect.zw;
    if(!DNKnown(p) || DNSolid(p)) return; // 墙表层、深岩及未知域保留原接收合同。
    float3 returned=0;
    [loop] for(int group=0;group<_DNFeatherCount;group++)
        returned+=DNFeatherFilter(id.xy,p,group,float2(0,1));
    float4 output=_DNLightOutput[id.xy];
    output.rgb+=returned;
    _DNLightOutput[id.xy]=output;
    float4 direction=_DNDirectionOutput[id.xy];
    direction.w=dot(output.rgb,float3(.2126,.7152,.0722));
    _DNDirectionOutput[id.xy]=direction; // 方向矩与直射照度不被返光滤波修改。
}
#endif
#endif
