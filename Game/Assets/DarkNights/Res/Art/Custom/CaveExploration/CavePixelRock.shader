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
            float3 rock(float2 p) { return tex2D(_RockTex,frac((floor(p*8)+.5)/256)).rgb; }
            float exposed(float2 p,float radius)
            {
                return 1-min(min(solid(p+float2(radius,0)),solid(p-float2(radius,0))),
                    min(solid(p+float2(0,radius)),solid(p-float2(0,radius))));
            }
            float depthPixels(float2 p)
            {
                if(exposed(p,25.0/8.0)<.5)return 26;
                if(exposed(p,7.0/8.0)>.5)return exposed(p,2.0/8.0)>.5?2:7;
                if(exposed(p,16.0/8.0)>.5)return 16;
                return 25;
            }
            float3 materialTint(float material)
            {
                if(material<1.5)return float3(1.08,.98,.86);
                if(material<2.5)return float3(.92,.97,1.03);
                if(material<3.5)return float3(.76,.80,.88);
                if(material<4.5)return float3(1.10,.88,.72);
                if(material<5.5)return float3(.86,.93,1.00);
                if(material<6.5)return float3(1.14,1.02,.76);
                if(material<7.5)return float3(.88,.98,.80);
                return float3(.58,.61,.68);
            }
            float4 frag(Output i):SV_Target
            {
                float2 p=(floor(i.p*8)+.5)/8;
                float4 c=data(p); float2 uv=(float2(p.x,-p.y)+.5)/float2(320,192);
                float2 light=tex2D(_CaveLight,uv).rg;
                float2 ore=tex2D(_OreMap,uv).rg;
                float3 oreColor=lerp(float3(.018,.25,.4),float3(.6,.27,.03),ore.r);
                float3 illumination=float3(.20,.082,.021)*light.r*light.r + float3(.017,.10,.21)*light.g*light.g;
                float3 tex=rock(p);
                float4 atlas=tex2D(_MainTex,i.uv);
                tex=lerp(tex,atlas.rgb,atlas.a*.08);
                if(_Background>.5)
                {
                    float3 color=float3(.002,.003,.005)+rock(p*.37+17)*.014+illumination*.18;
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
                float depth=depthPixels(p);
                float factor=.1+.95*exp(-max(0,depth-2)/7);
                float blend=depth<=1?.15:depth<=2?.58:.94;
                float3 core=float3(.0030,.0027,.0024);
                float3 color=depth>25?core:lerp(tex,core+tex*factor,blend);
                float top=1-solid(p+float2(0,1.0/8.0));
                color+=float3(.040,.021,.009)*top*(.25+tex*2.5);
                color*=materialTint(c.r);
                color+=illumination*(.12+factor*.46+tex*1.3);
                color+=ore.g*oreColor*(.12+.08*step(.55,tex.r));
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
