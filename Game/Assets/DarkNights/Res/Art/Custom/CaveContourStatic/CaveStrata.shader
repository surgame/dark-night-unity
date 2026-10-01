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
        _SurfaceSky ("Surface skyline enabled", Float) = 0
        _SurfaceSkyline ("Frozen first solid pixel per column", 2D) = "black" {}
        _SurfaceSettings ("Weathered depth, entrance depth, underground ambient", Vector) = (1.25,9,.36,0)
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
            sampler2D _SurfaceSkyline;
            float4 _StrataPage, _StrataEnabled;
            float4 _SurfaceSettings;
            float4x4 _MapWorldToLocal;
            float _Background;
            float _SurfaceSky;
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
            float2 surfaceHeights(float2 uv)
            {
                float4 bytes=round(tex2D(_SurfaceSkyline,float2(uv.x,.5))*255);
                return float2(bytes.r+bytes.g*256,bytes.b+bytes.a*256)/8;
            }
            float3 weathered(float3 color,float depth)
            {
                float amount=_SurfaceSky>.5 && _SurfaceSettings.x>0 ? 1-smoothstep(0,_SurfaceSettings.x,max(0,depth)) : 0;
                return lerp(color,author(float3(106,93,72)),amount*.28);
            }
            float3 lit(float3 color, float2 light, float row, float referenceRow)
            {
                float ambient=lerp(1,.36,smoothstep(43,52,row));
                if(_SurfaceSky>.5) ambient=lerp(1,_SurfaceSettings.z,smoothstep(0,_SurfaceSettings.y,max(0,row-referenceRow)));
                return color*min(1.15,ambient+light.r*.65+light.g*.4)+
                    float3(.083,.036,.007)*light.r+float3(.009,.036,.06)*light.g;
            }
            float4 frag(Output i):SV_Target
            {
                float2 cell=float2(i.p.x,-i.p.y)+.5;
                float2 pixel=floor(cell*8), uv=(pixel+.5)/float2(2560,1536);
                float2 heights=_SurfaceSky>.5 ? surfaceHeights(uv) : float2(43,43);
                float2 light=tex2D(_CaveLight,cell/float2(320,192)).rg;
                if(_Background<.5)
                {
                    float4 rock=tex2D(_RockSurface,uv); clip(rock.a-.5);
                    return float4(lit(weathered(rock.rgb,cell.y-heights.x),light,cell.y,heights.y),1);
                }
                // 地表模式按该列冻结地表计算深度；独立洞穴预览仍保留原背景配色合同。
                float displayRow=_SurfaceSky>.5 ? 43+cell.y-heights.y : cell.y;
                float t=smoothstep(0,43,displayRow), d=smoothstep(43,52.5,displayRow);
                float3 color=author(lerp(lerp(float3(55,45,51),float3(93,67,69),t),float3(23,21,26),d));
                float n=noise(pixel/float2(26,23))*.8+noise(pixel/float2(8,10)+91)*.2;
                float shade=n>.66?.73:n>.36?.63:.53;
                color=lerp(color,author(lerp(float3(38,29,20),float3(70,53,38),shade)*.87),smoothstep(43,44.25,displayRow));
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
                color=lerp(color,weathered(surface.rgb,cell.y-heights.x),step(.5,surface.a));
                color=lit(color,light,cell.y,heights.y);
                float opacity=1;
                if(_SurfaceSky>.5)
                {
                    // 当前首个实心像素上方露天空；实际岩壁轮廓优先遮挡，岩檐下保留地下背景。
                    opacity=max(step(heights.x*8,pixel.y),step(.5,surface.a));
                }
                return float4(color,opacity);
            }
            ENDHLSL
        }
    }
}
