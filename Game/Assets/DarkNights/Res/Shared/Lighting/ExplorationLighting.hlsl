#ifndef DN_EXPLORATION_LIGHTING
#define DN_EXPLORATION_LIGHTING
#include "ExplorationOcclusion.hlsl"
Texture2D<float4> _DNLightField;
Texture2D<float4> _DNLightDirection;
Texture2D<float> _DNWallDistance;
SamplerState sampler_DNLightField;
SamplerState sampler_DNLightDirection;
SamplerState sampler_DNWallDistance;
float4x4 _DNMapWorldToLocal;
float4 _DNLightRect;
float4 _DNDistanceRect;
float _DNLightingActive, _DNLightingAmbient, _DNRelief;

float2 DNGrid(float2 world)
{
    float2 p=mul(_DNMapWorldToLocal,float4(world,0,1)).xy;
    return float2(p.x,-p.y)+.5;
}
float3 DNIrradiance(float2 world,float3 normal)
{
    float2 p=DNGrid(world);
    float2 uv=(p-_DNLightRect.xy)/max(_DNLightRect.zw,.001);
    float3 energy=0;
    if (all(uv>=0) && all(uv<=1) && DNKnown(p))
    {
        // 按几何分隔插值：墙内补光不会因滤波扩散到墙后的空气。
        uint width,height; _DNLightField.GetDimensions(width,height);
        float2 texel=uv*float2(width,height)-.5;
        int2 base=(int2)floor(texel);
        float2 fraction=frac(texel);
        float total=0;
        bool solid=DNSolid(p);
        for(int y=0;y<2;y++) for(int x=0;x<2;x++)
        {
            int2 index=clamp(base+int2(x,y),int2(0,0),int2(width-1,height-1));
            float2 q=_DNLightRect.xy+(index+.5)/float2(width,height)*_DNLightRect.zw;
            if (DNSolid(q)!=solid || (!solid && DNSolid((q+p)*.5))) continue;
            float w=(x==0?1-fraction.x:fraction.x)*(y==0?1-fraction.y:fraction.y);
            energy+=_DNLightField.Load(int3(index,0)).rgb*w; total+=w;
        }
        energy/=max(total,.001);
        if (solid)
        {
            float2 duv=(p-_DNDistanceRect.xy)/max(_DNDistanceRect.zw,.001);
            float depth=_DNWallDistance.SampleLevel(sampler_DNWallDistance,duv,0);
            if (_DNShadowSettings.y<=0 || depth>_DNShadowSettings.y) energy=0;
            energy=min(energy,_DNShadowSettings.z);
        }
        float2 direction=_DNLightDirection.SampleLevel(sampler_DNLightDirection,uv,0).xy;
        float diffuse=saturate(dot(normalize(normal),normalize(float3(direction,.7))));
        energy*=lerp(1,.55+.65*diffuse,_DNRelief);
        if (solid) energy=min(energy,_DNShadowSettings.z);
    }
    return _DNLightingAmbient+energy;
}
#endif
