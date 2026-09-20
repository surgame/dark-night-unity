Shader "DarkNights/CavePixelRock"
{
    Properties
    {
        _MainTex ("DualGrid atlas", 2D) = "white" {}
        _RockTex ("Pixel rock", 2D) = "white" {}
        _CaveMap ("Read-only shapes", 2D) = "black" {}
        _CaveLight ("Occluded light", 2D) = "black" {}
        _OreMap ("Background minerals", 2D) = "black" {}
        _Background ("Background", Float) = 0
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
            #include "UnityCG.cginc"
            sampler2D _MainTex, _RockTex, _CaveMap, _CaveLight, _OreMap;
            float4x4 _MapWorldToLocal;
            float _Background;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; float2 uv:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv; o.p=mul(_MapWorldToLocal,mul(unity_ObjectToWorld,v.vertex)).xy; return o;
            }
            float4 data(float2 p)
            {
                float2 cell=floor(float2(p.x,-p.y)+.5);
                if(cell.x<0 || cell.x>=320 || cell.y<0 || cell.y>=192) return 0;
                return floor(tex2Dlod(_CaveMap,float4((cell+.5)/float2(320,192),0,0))*255+.5);
            }
            float solid(float2 p)
            {
                float4 c=data(p); if(c.r<.5)return 0;
                float shape=c.g; if(shape<.5)return 1;
                float2 f=frac(p+.5); bool ceiling=shape>6.5;
                if(ceiling) shape-=6;
                float edge=shape<1.5?f.x:shape<2.5?1-f.x:shape<3.5?f.x*.5:shape<4.5?.5+f.x*.5:shape<5.5?1-f.x*.5:.5-f.x*.5;
                return ceiling?step(edge,f.y):step(f.y,edge);
            }
            float3 rock(float2 p) { return tex2D(_RockTex,frac((floor(p*32)+.5)/256)).rgb; }
            float hash(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            float4 frag(Output i):SV_Target
            {
                float2 p=(floor(i.p*32)+.5)/32;
                float4 c=data(p); float2 uv=(float2(p.x,-p.y)+.5)/float2(320,192);
                float2 light=tex2D(_CaveLight,uv).rg;
                float2 ore=tex2D(_OreMap,uv).rg;
                float3 oreColor=lerp(float3(.018,.25,.4),float3(.6,.27,.03),ore.r);
                float3 illumination=float3(.20,.082,.021)*light.r*light.r + float3(.017,.10,.21)*light.g*light.g;
                float3 tex=rock(p*2);
                float2 tileUV=frac(i.uv*float2(16,32));
                float4 atlas=tex2D(_MainTex,i.uv);
                float variation=sin(tileUV.x*3.14159)*sin(tileUV.y*3.14159)*atlas.a*.12;
                tex=lerp(tex,atlas.rgb,variation); float sky=saturate((p.y+55)/22);
                if(_Background>.5)
                {
                    float3 color=float3(.002,.003,.006)+rock(p*.37+17)*.018+illumination*.22;
                    if(p.y>-43)
                    {
                        float horizon=-28+sin(p.x*.031)*6+sin(p.x*.12)*2;
                        color=lerp(float3(.018,.023,.036),float3(.065,.048,.059),saturate((p.y+42)/60));
                        if(p.y<horizon) color=float3(.023,.025,.035);
                        if(p.y<horizon-4+sin(p.x*.23)*4) color=float3(.014,.019,.024);
                    }
                    float2 f=frac(p+.5)-.5;
                    if(c.b==1 && abs(f.x)<.08 && f.y>-.35 && f.y<.24)
                        color=f.y>-.17&&f.y<.04?float3(1,.48,.07):float3(.075,.05,.025);
                    float2 shard=frac(p*2+floor(p.y)*.31);
                    float crystal=step(abs(shard.x-.5)*1.8+abs(shard.y-.5),.43);
                    color+=ore.g*crystal*oreColor;
                    return float4(color,1);
                }
                clip(solid(p)-.5);
                float cluster=hash(floor(p*5));
                float fringe=.08+cluster*.16, band=.3+cluster*.48, outer=.7+cluster*.65;
                float nearAir=1-min(min(solid(p+float2(fringe,0)),solid(p-float2(fringe,0))),min(solid(p+float2(0,fringe)),solid(p-float2(0,fringe))));
                float edge=1-min(min(solid(p+float2(band,0)),solid(p-float2(band,0))),min(solid(p+float2(0,band)),solid(p-float2(0,band))));
                float broad=1-min(min(solid(p+float2(outer,0)),solid(p-float2(outer,0))),min(solid(p+float2(0,outer)),solid(p-float2(0,outer))));
                float top=1-solid(p+float2(0,.25));
                float3 color=tex*(.006+broad*.18+edge*.7+sky*.2);
                color+=float3(.030,.017,.011)*edge+float3(.055,.032,.018)*nearAir*(.25+tex.r*15);
                color+=float3(.043,.032,.021)*top*(.2+tex.r*12);
                color+=illumination*(.4+tex*8);
                if(c.r==1)color*=float3(1.22,.89,.66);
                if(c.r==8)color*=.5;
                color+=ore.g*oreColor*(.16+.12*step(.72,hash(floor(p*12))));
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
