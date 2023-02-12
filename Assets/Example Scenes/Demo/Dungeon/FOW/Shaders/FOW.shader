Shader "Hidden/FOW"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
        SubShader
    {
        ZWrite off
        ZTest Always

        Pass
        {
            Name "VisionSrc"

            Blend one one

            ZClip off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./FOWINC.cginc"

            float frag(v2f i) : SV_Target
            {
                return step(length(i.vertex.xy - _VisionSource_SS.xy),_VisionSource_SS.z);
            }

            ENDCG
        }
        Pass
        {
            Name "Occluder"

            Blend one one
            Cull front

            CGPROGRAM

            #pragma vertex vert_occluder
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./FOWINC.cginc"

            float frag(v2f i) : SV_Target
            {
                return -step(length(i.vertex.xy - _VisionSource_SS.xy),_VisionSource_SS.z);
            }

            ENDCG
        }
        Pass
        {
            Name "PostProcess"

            Blend one one

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./FOWINC.cginc"
            #include "./Noise.cginc"

            float frag(v2f i) : SV_Target
            {
                #if UNITY_UV_STARTS_AT_TOP
                i.uv.y = 1 - i.uv.y;
                #endif
                float3 n = sdnoise(i.uv * _NoiseParam.x + _NoiseParam.y)* _NoiseParam.z;
                float s = tex2D(_MainTex, i.uv + n.yz);
                float v = step(0.00001,s);
                return (v * 2 - 1) * _BlendFactor;
            }

            ENDCG
        }
        Pass
        {
            Name "Sampling"

            CGPROGRAM
            #pragma vertex vert_sampling
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./FOWINC.cginc"

            struct v2f_sampling
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ray : TEXCOORD01;
            };

            sampler2D _FOW;
            float4x4 _View_I;
            float _FocalDistance;
            float _BaseLevel;

            sampler2D _CameraDepthTexture;

            v2f_sampling vert_sampling(appdata v)
            {
                v2f_sampling o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 ray = float3(float2(_ScreenParams.x / _ScreenParams.y, 1) * o.vertex.xy / _FocalDistance,1);
                o.ray = mul(_View_I, ray);
                return o;
            }

            fixed4 frag(v2f_sampling i) : SV_Target
            {
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv));
                float3 wpos = _WorldSpaceCameraPos + depth * i.ray;
                float2 uv = invlerp(_Bounds.xy , _Bounds.zw, wpos.xz);
                return tex2D(_MainTex, i.uv) * (saturate(tex2D(_FOW, uv).r) + _BaseLevel);
            }
            ENDCG
        }
        Pass
        {
            Name "Saturate"

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./FOWINC.cginc"

            float frag(v2f i) : SV_Target
            {
                #if UNITY_UV_STARTS_AT_TOP
                i.uv.y = 1 - i.uv.y;
                #endif
                return saturate(tex2D(_MainTex,i.uv).r);
            }

            ENDCG
        }
    }
}
