Shader "Hidden/DarkNights/TerrainRenderBench"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _TexelSnap ("Stable repeating texels", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _TexelSnap;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input v) { Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            half4 frag(Output v) : SV_Target
            {
                float2 uv = v.uv;
                if (_TexelSnap > 0) uv = (floor(uv * _TexelSnap + .001) + .5) / _TexelSnap;
                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
