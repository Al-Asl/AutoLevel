#ifndef SNOISE
#define SNOISE

//
// Description : Array and textureless GLSL 2D simplex noise function.
//      Author : Ian McEwan, Ashima Arts.
//  Maintainer : stegu
//     Lastmod : 20110822 (ijm)
//     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
//               Distributed under the MIT License. See LICENSE file.
//               https://github.com/ashima/webgl-noise
//               https://github.com/stegu/webgl-noise
// 

float3 mod289(float3 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float2 mod289(float2 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float3 permute(float3 x) {
    return mod289(((x * 34.0) + 10.0) * x);
}

float snoise(float2 v)
{
    const float4 C = float4(0.211324865405187,  // (3.0-sqrt(3.0))/6.0
        0.366025403784439,  // 0.5*(sqrt(3.0)-1.0)
        -0.577350269189626,  // -1.0 + 2.0 * C.x
        0.024390243902439); // 1.0 / 41.0
// First corner
    float2 i = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    // Other corners
    float2 i1;
    //i1.x = step( x0.y, x0.x ); // x0.x > x0.y ? 1.0 : 0.0
    //i1.y = 1.0 - i1.x;
    i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    // x0 = x0 - 0.0 + 0.0 * C.xx ;
    // x1 = x0 - i1 + 1.0 * C.xx ;
    // x2 = x0 - 1.0 + 2.0 * C.xx ;
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0))
        + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    // Gradients: 41 points uniformly over a line, mapped onto a diamond.
    // The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    // Normalise gradients implicitly by scaling m
    // Approximation of: m *= inversesqrt( a0*a0 + h*h );
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    // Compute final noise value at P
    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
}

// Hashed 2-D gradients with an extra rotation.
// (The constant 0.0243902439 is 1/41)
float2 rgrad2(float2 p, float rot) {
#if 0
    // Map from a line to a diamond such that a shift maps to a rotation.
    float u = permute(permute(p.x) + p.y) * 0.0243902439 + rot; // Rotate by shift
    u = 4.0 * frac(u) - 2.0;
    // (This vector could be normalized, exactly or approximately.)
    return float2(abs(u) - 1.0, abs(abs(u + 1.0) - 2.0) - 1.0);
#else
    // For more isotropic gradients, sin/cos can be used instead.
    float u = permute(permute(p.x) + p.y) * 0.0243902439 + rot; // Rotate by shift
    u = frac(u) * 6.28318530718; // 2*pi
    return float2(cos(u), sin(u));
#endif
}

//
// 2-D non-tiling simplex noise with rotating gradients and analytical derivative.
// The first component of the 3-element return vector is the noise value,
// and the second and third components are the x and y partial derivatives.
//
float3 srdnoise(float2 pos, float rot) {
    // Offset y slightly to hide some rare artifacts
    pos.y += 0.001;
    // Skew to hexagonal grid
    float2 uv = float2(pos.x + pos.y * 0.5, pos.y);

    float2 i0 = floor(uv);
    float2 f0 = frac(uv);
    // Traversal order
    float2 i1 = (f0.x > f0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);

    // Unskewed grid points in (x,y) space
    float2 p0 = float2(i0.x - i0.y * 0.5, i0.y);
    float2 p1 = float2(p0.x + i1.x - i1.y * 0.5, p0.y + i1.y);
    float2 p2 = float2(p0.x + 0.5, p0.y + 1.0);

    // Integer grid point indices in (u,v) space
    i1 = i0 + i1;
    float2 i2 = i0 + float2(1.0, 1.0);

    // Vectors in unskewed (x,y) coordinates from
    // each of the simplex corners to the evaluation point
    float2 d0 = pos - p0;
    float2 d1 = pos - p1;
    float2 d2 = pos - p2;

    float3 x = float3(p0.x, p1.x, p2.x);
    float3 y = float3(p0.y, p1.y, p2.y);
    float3 iuw = x + 0.5 * y;
    float3 ivw = y;

    // Avoid precision issues in permutation
    iuw = mod289(iuw);
    ivw = mod289(ivw);

    // Create gradients from indices
    float2 g0 = rgrad2(float2(iuw.x, ivw.x), rot);
    float2 g1 = rgrad2(float2(iuw.y, ivw.y), rot);
    float2 g2 = rgrad2(float2(iuw.z, ivw.z), rot);

    // Gradients dot vectors to corresponding corners
    // (The derivatives of this are simply the gradients)
    float3 w = float3(dot(g0, d0), dot(g1, d1), dot(g2, d2));

    // Radial weights from corners
    // 0.8 is the square of 2/sqrt(5), the distance from
    // a grid point to the nearest simplex boundary
    float3 t = 0.8 - float3(dot(d0, d0), dot(d1, d1), dot(d2, d2));

    // Partial derivatives for analytical gradient computation
    float3 dtdx = -2.0 * float3(d0.x, d1.x, d2.x);
    float3 dtdy = -2.0 * float3(d0.y, d1.y, d2.y);

    // Set influence of each surflet to zero outside radius sqrt(0.8)
    if (t.x < 0.0) {
        dtdx.x = 0.0;
        dtdy.x = 0.0;
        t.x = 0.0;
    }
    if (t.y < 0.0) {
        dtdx.y = 0.0;
        dtdy.y = 0.0;
        t.y = 0.0;
    }
    if (t.z < 0.0) {
        dtdx.z = 0.0;
        dtdy.z = 0.0;
        t.z = 0.0;
    }

    // Fourth power of t (and third power for derivative)
    float3 t2 = t * t;
    float3 t4 = t2 * t2;
    float3 t3 = t2 * t;

    // Final noise value is:
    // sum of ((radial weights) times (gradient dot vector from corner))
    float n = dot(t4, w);

    // Final analytical derivative (gradient of a sum of scalar products)
    float2 dt0 = float2(dtdx.x, dtdy.x) * 4.0 * t3.x;
    float2 dn0 = t4.x * g0 + dt0 * w.x;
    float2 dt1 = float2(dtdx.y, dtdy.y) * 4.0 * t3.y;
    float2 dn1 = t4.y * g1 + dt1 * w.y;
    float2 dt2 = float2(dtdx.z, dtdy.z) * 4.0 * t3.z;
    float2 dn2 = t4.z * g2 + dt2 * w.z;

    return 11.0 * float3(n, dn0 + dn1 + dn2);
}

//
// 2-D non-tiling simplex noise with fixed gradients and analytical derivative.
// This function is implemented as a wrapper to "srdnoise",
// at the minimal cost of three extra additions.
//
float3 sdnoise(float2 pos) {
    return srdnoise(pos, 0.0);
}

#endif