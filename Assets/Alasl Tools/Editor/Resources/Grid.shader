Shader "Hidden/AlaslTools/Grid"
{
    Properties
    {
        _LineWidth("Line Width",Float) = 20
    }
    SubShader
    {
         Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100
        Zwrite off
        ZTest Less
        Blend srcAlpha oneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            float _LineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(UNITY_MATRIX_M, v.vertex);
                o.normal = mul((float3x3)UNITY_MATRIX_M,v.normal);
                o.uv = v.uv;
                return o;
            }

            //iquilezles.org/www/articles/filterableprocedurals/filterableprocedurals.htm
            float filteredGrid(float2 p)
            {
                float2 w = fwidth(p);
                float2 a = p + 0.5 * w;
                float2 b = p - 0.5 * w;
                float2 i = (floor(a) + min(frac(a) * _LineWidth, 1.0) -
                    floor(b) - min(frac(b) * _LineWidth, 1.0)) / (_LineWidth * w);
                return (1.0 - i.x) * (1.0 - i.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = abs(normalize(i.normal));
                float3 wpos = i.wpos + 0.5 / _LineWidth;
                float2 p = n.x > n.y && n.x > n.z ? wpos.yz : n.y > n.z ? wpos.xz : wpos.xy;
                return float4(0,0,0,1 - filteredGrid(p) + 0.2);
            }
            ENDCG
        }
    }
}
