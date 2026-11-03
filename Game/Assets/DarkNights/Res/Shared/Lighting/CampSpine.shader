Shader "Dark Nights/Camp Spine"
{
    Properties
    {
        _MainTex ("Straight Alpha Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _DNSpineTint ("Actor Tint", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "CampSpineUniversal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Assets/DarkNights/Res/Shared/Lighting/LightingSurface.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _DNSpineTint;
            CBUFFER_END
            float _DNLocalFillCount;
            float4 _DNLocalFillOrigins[4];
            float4 _DNLocalFillColors[4];
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.world = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                // Spine槽位和骨骼颜色是作者sRGB；贴图由sRGB采样进入线性空间。
                output.color = half4(SRGBToLinear(input.color.rgb), input.color.a) * _Color * _DNSpineTint;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (_DNLightingActive > .5)
                {
                    float3 fill = 0;
                    for (int n = 0; n < min(4, (int)_DNLocalFillCount); n++)
                    {
                        float distance = length((input.world - _DNLocalFillOrigins[n].xy) /
                            max(float2(.001,.001), _DNLocalFillOrigins[n].zw));
                        fill += pow(saturate(1-distance), 1.1) *
                            _DNLocalFillColors[n].rgb * _DNLocalFillColors[n].w;
                    }
                    half4 shaded = DNShade(color, input.world, float3(0,0,-1), half4(1,1,1,1));
                    color.rgb = shaded.rgb + color.rgb * min(fill, float3(2,2,2));
                }
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "CampSpineNormals"
            Tags { "LightMode"="NormalsRendering" }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vertNormals
            #pragma fragment fragNormals
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _DNSpineTint;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; };
            Varyings vertNormals(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.alpha = input.color.a * _Color.a * _DNSpineTint.a;
                return output;
            }
            half4 fragNormals(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha;
                return NormalsRenderingShared(half4(1,1,1,alpha), half3(0,0,1),
                    half3(1,0,0), half3(0,1,0), half3(0,0,-1));
            }
            ENDHLSL
        }
    }
}
