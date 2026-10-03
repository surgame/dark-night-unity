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
        _ArrivalOpacity ("Local journey reveal", Range(0,1)) = 1
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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _RockSurface, _CaveLight;
            float4x4 _MapWorldToLocal;
            float _Background, _Ambient, _ArrivalOpacity;
            struct Input { float4 vertex:POSITION; };
            struct Output { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.p=mul(_MapWorldToLocal,mul(unity_ObjectToWorld,v.vertex)).xy; return o;
            }
            float3 lit(float3 color,float2 light)
            {
                return color*min(1.15,_Ambient+light.r*.65+light.g*.4)+
                    float3(.083,.036,.007)*light.r+float3(.009,.036,.06)*light.g;
            }
            float4 frag(Output i):SV_Target
            {
                float2 cell=float2(i.p.x,-i.p.y)+.5;
                float2 uv=(floor(cell*8)+.5)/float2(2560,1536);
                float4 rock=tex2D(_RockSurface,uv);
                float2 light=tex2D(_CaveLight,cell/float2(320,192)).rg;
                if(_Background<.5 || _Background>1.5)
                {
                    clip(rock.a-.5); return float4(lit(rock.rgb,light),_ArrivalOpacity);
                }
                // 独立地下工作台的固定底板；航程使用分层素材，不启用该底板。
                float3 color=GammaToLinearSpace(float3(40,31,23)/255);
                color=lerp(color,rock.rgb,step(.5,rock.a));
                return float4(lit(color,light),_ArrivalOpacity);
            }
            ENDHLSL
        }
    }
}
