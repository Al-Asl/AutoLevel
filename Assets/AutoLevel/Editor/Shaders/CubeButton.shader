Shader "Hidden/AutoLevel/CubeButton"
{
    Properties
    {
        _Color("Color",Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100
        Zwrite off
        ZTest always
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = smoothstep(0,1,abs(i.uv - 0.5));
                return (step(1 - max(uv.x,uv.y),0.51) + (uv.x+uv.y)*0.8)* _Color;
            }
            ENDCG
        }
    }
}
