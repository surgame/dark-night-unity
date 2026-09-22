Shader "DarkNights/CaveStrata"
{
    Properties
    {
        _MainTex ("AnyRuleD geometry atlas", 2D) = "white" {}
        _RockSurface ("Current rock surface", 2D) = "black" {}
        _CaveMap ("Read-only cells", 2D) = "black" {}
        _CaveLight ("Soft radial light", 2D) = "black" {}
        _OreMap ("Background minerals", 2D) = "black" {}
        _Background ("Background pass", Float) = 0
        _StrataNear ("Frozen near rock", 2D) = "black" {}
        _StrataMiddle ("Frozen middle rock", 2D) = "black" {}
        _StrataDeep ("Frozen deep rock", 2D) = "black" {}
        _StrataPage ("Page origin and size", Vector) = (0,0,32,32)
        _StrataEnabled ("Near middle deep enabled", Vector) = (0,0,0,0)
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
            sampler2D _RockSurface, _CaveMap, _CaveLight, _OreMap, _StrataNear, _StrataMiddle, _StrataDeep;
            float4 _StrataPage, _StrataEnabled;
            float4x4 _MapWorldToLocal;
            float _Background;
            struct Input { float4 vertex:POSITION; };
            struct Output { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.p=mul(_MapWorldToLocal,mul(unity_ObjectToWorld,v.vertex)).xy; return o;
            }
            float3 author(float3 bytes) { return GammaToLinearSpace(bytes/255); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                float4 h=frac(sin(float4(dot(a,float2(127.1,311.7)),dot(a+float2(1,0),float2(127.1,311.7)),
                    dot(a+float2(0,1),float2(127.1,311.7)),dot(a+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
            }
            float3 lit(float3 color, float2 light, float row)
            {
                float ambient=lerp(1,.36,smoothstep(43,52,row));
                return color*min(1.15,ambient+light.r*.65+light.g*.4)+
                    float3(.083,.036,.007)*light.r+float3(.009,.036,.06)*light.g;
            }
            float4 frag(Output i):SV_Target
            {
                float2 cell=float2(i.p.x,-i.p.y)+.5;
                float2 pixel=floor(cell*8), uv=(pixel+.5)/float2(2560,1536);
                float2 light=tex2D(_CaveLight,cell/float2(320,192)).rg;
                if(_Background<.5)
                {
                    float4 rock=tex2D(_RockSurface,uv); clip(rock.a-.5);
                    return float4(lit(rock.rgb,light,cell.y),1);
                }
                float t=smoothstep(0,43,cell.y), d=smoothstep(43,52.5,cell.y);
                float3 color=author(lerp(lerp(float3(55,45,51),float3(93,67,69),t),float3(23,21,26),d));
                float n=noise(pixel/float2(26,23))*.8+noise(pixel/float2(8,10)+91)*.2;
                float shade=n>.66?.73:n>.36?.63:.53;
                color=lerp(color,author(lerp(float3(38,29,20),float3(70,53,38),shade)*.87),smoothstep(43,44.25,cell.y));
                if(_StrataEnabled.w>.5)
                {
                    float2 pageUV=(cell-_StrataPage.xy)/_StrataPage.zw;
                    float4 deep=tex2D(_StrataDeep,pageUV), middle=tex2D(_StrataMiddle,pageUV), near=tex2D(_StrataNear,pageUV);
                    color=lerp(color,deep.rgb,deep.a*_StrataEnabled.z);
                    color=lerp(color,middle.rgb,middle.a*_StrataEnabled.y);
                    color=lerp(color,near.rgb,near.a*_StrataEnabled.x);
                }
                // 在完整背景页上合成外轮廓，允许轮廓越过原 AnyRuleD 网格边界。
                float4 surface=tex2D(_RockSurface,uv);
                color=lerp(color,surface.rgb,step(.5,surface.a));
                color=lit(color,light,cell.y);
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
