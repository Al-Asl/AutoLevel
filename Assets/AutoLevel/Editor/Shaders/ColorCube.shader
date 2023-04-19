Shader "Hidden/AutoLevel/ColorCube"
{
    Properties
    {
        _Color("Color",Color) = (1,1,1,1)
        _Left("Left",Color) = (1,1,1,1)
        _Down("Down",Color) = (1,1,1,1)
        _Back("Back",Color) = (1,1,1,1)
        _Right("Right",Color) = (1,1,1,1)
        _Up("Up",Color) = (1,1,1,1)
        _Front("Front",Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Zwrite off
        ZTest Less
        Blend srcAlpha oneMinusSrcAlpha
        LOD 100

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD01;
            };

            float4 _Color;
            float4 _Left;
            float4 _Down;
            float4 _Back;
            float4 _Right;
            float4 _Up;
            float4 _Front;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldDir(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                return(
                    saturate(-n.x * _Left) +
                    saturate(-n.y * _Down) +
                    saturate(-n.z * _Back) +
                    saturate(n.x * _Right) +
                    saturate(n.y * _Up) +
                    saturate(n.z * _Front))*_Color;
            }
            ENDCG
        }
    }
}
