Shader "Hidden/DarkNights/Editor/TerrainDesign"
{
    Properties
    {
        _MainTex ("Structure attributes", 2D) = "black" {}
        _ShapeTable ("Shared shape endpoints", 2D) = "black" {}
        _ShowMinerals ("Show hidden minerals", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _ShapeTable;
            float _ShowMinerals;

            float4 frag(v2f_img input) : SV_Target
            {
                float2 position = float2(input.uv.x, 1 - input.uv.y) * float2(320, 192) + .5;
                float2 cell = floor(position), local = frac(position);
                if (cell.x >= 320 || cell.y >= 192) return float4(.035, .055, .065, 1);
                float4 data = floor(tex2D(_MainTex, (float2(cell.x, 191 - cell.y) + .5) / float2(320, 192)) * 255 + .5);
                float4 shape = tex2D(_ShapeTable, float2((data.g + .5) / 13, .5));
                float edge = lerp(shape.r, shape.g, local.x);
                float solid = data.r > 0 && (shape.a > .5 || (shape.b > .5 ? 1 - local.y >= edge : 1 - local.y <= edge));
                float3 color = solid ? float3(.75, .77, .74) : float3(.035, .055, .065);
                if (solid)
                {
                    color *= .96 + data.r * .008;
                    if (fmod(data.b, 2) > .5 && frac((local.x + local.y) * 3) > .75)
                        color = float3(.43, .35, .55);
                    else if (data.b >= 2 && local.x < .12 && local.y < .12)
                        color = float3(.38, .48, .35);
                }
                if (data.a > 0 && (_ShowMinerals > .5 || solid < .5))
                {
                    float2 p = local - .5;
                    bool mark = data.a == 1 ? max(abs(p.x), abs(p.y)) < .34 :
                        data.a == 2 ? abs(p.x) + abs(p.y) < .43 :
                        data.a == 3 ? abs(p.x) < .14 || abs(p.y) < .14 :
                        data.a == 4 ? abs(p.y) < .14 : dot(p,p) < .13;
                    float3 ore = data.a == 1 ? float3(.25,.62,.78) :
                        data.a == 2 ? float3(.93,.65,.13) :
                        data.a == 3 ? float3(.8,.34,.16) :
                        data.a == 4 ? float3(.65,.73,.85) : float3(.32,.85,.65);
                    if (mark && (solid < .5 || frac((local.x + local.y) * 4) < .68))
                        color = lerp(color, ore, solid > .5 ? .8 : 1);
                }
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
