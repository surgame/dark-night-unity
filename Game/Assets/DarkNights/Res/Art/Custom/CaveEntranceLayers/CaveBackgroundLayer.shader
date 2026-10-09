Shader "DarkNights/CaveBackgroundLayer"
{
    Properties { _MainTex ("Existing background layer", 2D) = "black" {} _CaveLight ("Local lights", 2D) = "black" {} _Ambient ("Uniform background illumination", Float) = .36 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Assets/DarkNights/Res/Shared/Lighting/LightingSurface.hlsl"
            sampler2D _MainTex, _CaveLight;
            float _Ambient;
            float4x4 _MapWorldToLocal;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 cell:TEXCOORD1; float2 world:TEXCOORD2; };
            Output vert(Input v)
            {
                Output o; o.vertex=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                float2 p=mul(_MapWorldToLocal,mul(unity_ObjectToWorld,v.vertex)).xy;
                o.cell=float2(p.x,-p.y)+.5; return o;
            }
            float4 frag(Output i):SV_Target
            {
                float4 color=tex2D(_MainTex,i.uv);
                if (_DNLightingActive>.5)
                { color=DNShade(color,i.world,float3(0,0,1),float4(1,1,1,1)); color.rgb*=clamp(_Ambient/.36,.35,1); return color; }
                float2 light=tex2D(_CaveLight,i.cell/float2(320,192)).rg;
                color.rgb=color.rgb*min(1.15,_Ambient+light.r*.65+light.g*.4)+
                    float3(.083,.036,.007)*light.r+float3(.009,.036,.06)*light.g;
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex DNNormalVertex
            #pragma fragment DNNormalFragment
            #pragma target 3.5
            #include "Assets/DarkNights/Res/Shared/Lighting/TerrainNormals.hlsl"
            ENDHLSL
        }
    }
}
