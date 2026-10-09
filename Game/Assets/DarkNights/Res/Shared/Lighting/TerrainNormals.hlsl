#ifndef DN_TERRAIN_NORMALS
#define DN_TERRAIN_NORMALS
#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
sampler2D _MainTex, _RockSurface, _CaveMap;
float4x4 _MapWorldToLocal;
float _Background;
struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 local : TEXCOORD1; };
Varyings DNNormalVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.local = mul(_MapWorldToLocal,float4(TransformObjectToWorld(input.positionOS),1)).xy;
    return output;
}
half4 DNNormalFragment(Varyings input) : SV_Target
{
    // URP 2D 光源的模拟深度朝向相机，使用与原生 Sprite 法线相同的 -Z 朝向。
    float3 normal = float3(0,0,-1);
    float alpha = 1;
#if defined(DN_NORMAL_STRATA)
    float2 cell = float2(input.local.x,-input.local.y)+.5;
    float2 uv = (floor(cell*8)+.5)/float2(2560,1536);
    float4 rock = tex2D(_RockSurface,uv);
    if (_Background<.5 || _Background>1.5) clip(rock.a-.5);
    float dx=dot(tex2D(_RockSurface,uv+float2(1.0/2560,0)).rgb-tex2D(_RockSurface,uv-float2(1.0/2560,0)).rgb,float3(.2126,.7152,.0722));
    float dy=dot(tex2D(_RockSurface,uv+float2(0,1.0/1536)).rgb-tex2D(_RockSurface,uv-float2(0,1.0/1536)).rgb,float3(.2126,.7152,.0722));
    normal=normalize(float3(-dx*2,dy*2,-1));
#elif defined(DN_NORMAL_PIXEL_ROCK)
    if (_Background<.5)
    {
        float2 p=input.local+.5, cell=floor(float2(input.local.x,-input.local.y)+.5);
        float4 data=tex2D(_CaveMap,(cell+.5)/float2(320,192));
        clip(data.r-.0001);
        int shape=(int)round(data.g*255);
        if (shape!=0)
        {
            float2 f=frac(p); bool ceiling=shape>=7;
            int s=ceiling?shape-6:shape;
            float edge=s==1?f.x:s==2?1-f.x:s==3?f.x*.5:s==4?.5+f.x*.5:s==5?1-f.x*.5:.5-f.x*.5;
            clip((ceiling?f.y-edge:edge-f.y)+.00001);
        }
    }
#else
    alpha=tex2D(_MainTex,input.uv).a;
    clip(alpha-.001);
#endif
    normal=normalize(TransformObjectToWorldNormal(normal));
    return half4(normal*.5+.5,alpha);
}
#endif
