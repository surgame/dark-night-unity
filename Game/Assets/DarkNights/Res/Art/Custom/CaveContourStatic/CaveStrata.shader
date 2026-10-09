Shader "DarkNights/CaveStrata"
{
    Properties
    {
        _MainTex ("AnyRuleD geometry atlas", 2D) = "white" {}
        _RockSurface ("Current rock surface", 2D) = "black" {}
        _CaveMap ("Read-only cells", 2D) = "black" {}
        _CaveLight ("Soft radial light", 2D) = "black" {}
        _OreMap ("Background minerals", 2D) = "black" {}
        _Background ("Foreground / legacy wall / contour", Float) = 0
        _Ambient ("Uniform authored-rock illumination", Float) = .65
        // 旧独立天际线诊断的数据槽；正式渲染不读取，不产生入口过渡。
        _SurfaceSky ("Legacy diagnostic slot", Float) = 0
        _SurfaceSkyline ("Legacy diagnostic data", 2D) = "black" {}
        _SurfaceSettings ("Legacy diagnostic settings", Vector) = (1.25,9,.36,0)
    }
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Assets/DarkNights/Res/Shared/Lighting/LightingSurface.hlsl"
            sampler2D _RockSurface, _CaveLight;
            float4x4 _MapWorldToLocal;
            float _Background, _Ambient;
            struct Input { float4 vertex:POSITION; };
            struct Output { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; float2 world:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex=TransformObjectToHClip(v.vertex.xyz);
                o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.p=mul(_MapWorldToLocal,mul(unity_ObjectToWorld,v.vertex)).xy; return o;
            }
            float3 lit(float3 color,float2 light,float2 world,float3 normal)
            {
                if (_DNLightingActive>.5) return DNShade(float4(color,1),world,normal,float4(1,1,1,1)).rgb;
                return color*min(1.15,_Ambient+light.r*.65+light.g*.4)+
                    float3(.083,.036,.007)*light.r+float3(.009,.036,.06)*light.g;
            }
            float4 frag(Output i):SV_Target
            {
                float2 cell=float2(i.p.x,-i.p.y)+.5;
                float2 uv=(floor(cell*8)+.5)/float2(2560,1536);
                float4 rock=tex2D(_RockSurface,uv);
                float dx=dot(tex2D(_RockSurface,uv+float2(1.0/2560,0)).rgb-tex2D(_RockSurface,uv-float2(1.0/2560,0)).rgb,float3(.2126,.7152,.0722));
                float dy=dot(tex2D(_RockSurface,uv+float2(0,1.0/1536)).rgb-tex2D(_RockSurface,uv-float2(0,1.0/1536)).rgb,float3(.2126,.7152,.0722));
                float3 normal=normalize(float3(-dx*2,dy*2,1));
                float2 light=tex2D(_CaveLight,cell/float2(320,192)).rg;
                if(_Background<.5 || _Background>1.5)
                {
                    clip(rock.a-.5); return float4(lit(rock.rgb,light,i.world,normal),1);
                }
                // 独立地下工作台的固定底板；航程使用分层素材，不启用该底板。
                float3 color=SRGBToLinear(float3(40,31,23)/255);
                color=lerp(color,rock.rgb,step(.5,rock.a));
                return float4(lit(color,light,i.world,normal),1);
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
            #define DN_NORMAL_STRATA
            #include "Assets/DarkNights/Res/Shared/Lighting/TerrainNormals.hlsl"
            ENDHLSL
        }
    }
}
