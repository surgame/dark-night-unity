#ifndef DN_LIGHTING_SURFACE
#define DN_LIGHTING_SURFACE
#include "ExplorationLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
float _DNLightingBackend;

// 材质只提供表面数据；URP 模式读取原生光照纹理，私有模式读取相机光场。
half4 DNShade(half4 color, float2 world, float3 normal, half4 mask)
{
    if (_DNLightingBackend > 1.5)
    {
        float4 screen = ComputeScreenPos(TransformWorldToHClip(float3(world, 0)));
        SurfaceData2D surface;
        InputData2D input;
        InitializeSurfaceData(color.rgb, color.a, mask, surface);
        InitializeInputData(float2(0, 0), screen.xy / max(screen.w, .0001), input);
        return CombinedShapeLightShared(surface, input);
    }
    return half4(color.rgb * DNIrradiance(world, normal), color.a);
}
#endif
