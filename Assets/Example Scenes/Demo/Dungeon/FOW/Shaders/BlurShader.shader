Shader "Hidden/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        ZWrite off
        ZTest always

        CGINCLUDE
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
            float4 _MainTex_TexelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
        ENDCG

        Pass // downsacle horizontal 
        {
            CGPROGRAM

            fixed4 frag(v2f input) : SV_Target
            {
                #if UNITY_UV_STARTS_AT_TOP
                    input.uv.y = 1 - input.uv.y;
                #endif
                        
                float3 color = 0.0;
                float offsets[] = {
                    -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
                };
                float weights[] = {
                    0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
                    0.19459459, 0.12162162, 0.05405405, 0.01621622
                };
                for (int i = 0; i < 9; i++) {
                    float offset = offsets[i] * 2.0 * _MainTex_TexelSize.x;
                    color += tex2D(_MainTex, input.uv + float2(offset, 0.0)).rgb * weights[i];
                }
                return float4(color, 1.0);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM

            fixed4 frag(v2f input) : SV_Target {
                #if UNITY_UV_STARTS_AT_TOP
                    input.uv.y = 1 - input.uv.y;
                #endif
                float3 color = 0.0;
                float offsets[] = {
                    -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
                };
                float weights[] = {
                    0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
                };
                for (int i = 0; i < 5; i++) {
                    float offset = offsets[i] * _MainTex_TexelSize.y;
                    color += tex2D(_MainTex,input.uv + float2(0.0, offset)).rgb * weights[i];
                }
                return float4(color, 1.0);
            }

            ENDCG
            }

            Pass //upsampling 
            {
                CGPROGRAM

                fixed4 frag(v2f input) : SV_Target {
                    #if UNITY_UV_STARTS_AT_TOP
                        input.uv.y = 1 - input.uv.y;
                    #endif
                    return tex2D(_MainTex,input.uv);
                }

            ENDCG
        }
    }
}
