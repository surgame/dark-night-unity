Shader "Dark Nights/Camp Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UseGlobalAmbient ("Apply Ambient To Static Mesh", Float) = 0
        _VertexColorIsGamma ("Mesh Vertex Colors Are Authored In sRGB", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };
            sampler2D _MainTex;
            fixed4 _Color;
            float _UseGlobalAmbient;
            float _VertexColorIsGamma;
            float4 _DNCampAmbient;
            float4 _DNCampLights[7];
            float4 _DNCampLightColors[7];
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // SpriteRenderer already converts its tint; authored Mesh colors need explicit decoding.
                float3 tint = lerp(v.color.rgb, GammaToLinearSpace(v.color.rgb), _VertexColorIsGamma);
                o.color = float4(tint, v.color.a) * _Color;
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
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
            ENDCG
        }
    }
}
