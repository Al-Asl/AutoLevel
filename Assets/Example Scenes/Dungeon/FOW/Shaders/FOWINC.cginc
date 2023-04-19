#ifndef FOWINC
#define FOWINC

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
float4 _MainTex_TexelSize;

float4 _Bounds;
float4 _VisionSource_WS;
float4 _VisionSource_SS;
float4 _FOWParams;
// (scale, offset, magnitude)
float4 _NoiseParam;
float _BlendFactor;

float2 invlerp(float2 a, float2 b, float2 v)
{
    return saturate((v - a) / (b - a));
}

v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

v2f vert_occluder(appdata v)
{
    v2f o = (v2f)0;
    float4 wpos = mul(UNITY_MATRIX_M, v.vertex);
    float3 delta = wpos - _VisionSource_WS;
    float d = max(0,exp(delta.y) - 1)* _FOWParams.x + _FOWParams.y;
    delta.y = 0;
    wpos.xyz += normalize(delta) * d ;
    o.vertex = mul(UNITY_MATRIX_VP, wpos);
    return o;
}

#endif