Shader "Hidden/AlaslTools/Handle/Standard"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color",Color) = (1,1,1,1)
        _ZTest("ZTest",Float) = 0
        _ZWrite("ZWrite",Float) = 0
        _SrcBlend("Src Blend",Float) = 1
        _DstBlend("Dst Blend",Float) = 10
    }
    SubShader
    {
        Zwrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
