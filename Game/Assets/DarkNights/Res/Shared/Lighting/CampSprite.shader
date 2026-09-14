Shader "Dark Nights/Camp Sprite"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
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
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

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

            float4 _DNCampAmbient;
            float4 _DNCampLights[7];
            float4 _DNCampLightColors[7];

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
    }
}
