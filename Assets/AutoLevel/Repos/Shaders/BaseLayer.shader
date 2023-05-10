Shader "Custom/BaseLayer"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "black" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        #define N 40.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

            //iquilezles.org/www/articles/filterableprocedurals/filterableprocedurals.htm
        float filteredGrid(float2 p)
        {
            float2 w = fwidth(p);
            float2 a = p + 0.5 * w;
            float2 b = p - 0.5 * w;
            float2 i = (floor(a) + min(frac(a) * N, 1.0) -
                floor(b) - min(frac(b) * N, 1.0)) / (N * w);
            return (1.0 - i.x) * (1.0 - i.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(normalize(IN.worldNormal));
            float3 wpos = IN.worldPos + 0.5/N;
            float2 p = n.x > n.y && n.x > n.z ? wpos.yz : n.y > n.z ? wpos.xz : wpos.xy;

            // Albedo comes from a texture tinted by color
            float4 t = tex2D(_MainTex, IN.uv_MainTex);
            float3 grid = 1 - filteredGrid(p);
            o.Albedo = lerp((_Color.rgb + grid), t.rgb, t.a);
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
