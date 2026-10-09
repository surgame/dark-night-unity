Shader "Dark Nights/Camp Sprite"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _MaskTex ("Light Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _UseGlobalAmbient ("Apply Ambient To Static Mesh", Float) = 0
        _VertexColorIsGamma ("Mesh Vertex Colors Are Authored In sRGB", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "CampSpriteUniversal2D"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _UseGlobalAmbient;
                float _VertexColorIsGamma;
            CBUFFER_END

            #include "Assets/DarkNights/Res/Shared/Lighting/LightingSurface.hlsl"
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            float4 _DNCampAmbient;
            float4 _DNCampLights[7];
            float4 _DNCampLightColors[7];
            float _DNLocalFillCount;
            float4 _DNLocalFillOrigins[4];
            float4 _DNLocalFillColors[4];

            Varyings vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.world = TransformObjectToWorld(input.positionOS).xy;
                o.uv = input.uv;
                // SpriteRenderer already converts its tint; authored Mesh colors need explicit decoding.
                half3 tint = lerp(input.color.rgb, SRGBToLinear(input.color.rgb), _VertexColorIsGamma);
                o.color = half4(tint, input.color.a) * _Color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (_DNLightingActive > .5)
                {
                    float3 fill = 0;
                    for (int n = 0; n < min(4, (int)_DNLocalFillCount); n++)
                    {
                        float distance = length((i.world - _DNLocalFillOrigins[n].xy) / max(float2(.001,.001), _DNLocalFillOrigins[n].zw));
                        float weight = pow(saturate(1 - distance), 1.1);
                        fill += weight * _DNLocalFillColors[n].rgb * _DNLocalFillColors[n].w;
                    }
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                    normalTS.xy *= unity_SpriteProps.xy;
                    float3 normal = normalize(float3(mul((float2x2)unity_ObjectToWorld,normalTS.xy),normalTS.z));
                    half4 shaded = DNShade(c, i.world, normal, SAMPLE_TEXTURE2D(_MaskTex,sampler_MaskTex,i.uv));
                    c.rgb = shaded.rgb + c.rgb * min(fill, float3(2,2,2));
                    c.rgb *= c.a; return c;
                }
                c.rgb *= lerp(float3(1,1,1), _DNCampAmbient.rgb, _UseGlobalAmbient);
                float3 light = 0;
                for (int n = 0; n < 7; n++)
                {
                    float radius = max(_DNCampLights[n].z, 0.0001);
                    float distance = length(i.world - _DNCampLights[n].xy) / radius;
                    float weight = distance < 0.3 ? lerp(0.85, 0.25, distance / 0.3) :
                        lerp(0.25, 0, saturate((distance - 0.3) / 0.7));
                    light += weight * _DNCampLightColors[n].rgb;
                }
                // Texture, tint, ambient and light are linear; illumination is added exactly once.
                c.rgb *= 1 + light / max(_DNCampAmbient.rgb, 0.001);
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "CampSpriteNormals"
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex vertNormal
            #pragma fragment fragNormal
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _UseGlobalAmbient;
                float _VertexColorIsGamma;
            CBUFFER_END
            Varyings vertNormal(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                // 分页地面 Mesh 没有作者法线／切线，统一使用二维表面的有效基向量。
                o.normalWS = TransformObjectToWorldDir(float3(0,0,-1));
                o.tangentWS = TransformObjectToWorldDir(float3(1,0,0));
                o.bitangentWS = TransformObjectToWorldDir(float3(0,1,0));
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 fragNormal(Varyings input) : SV_Target { return CommonNormalsFragment(input,input.color); }
            ENDHLSL
        }
    }
}
