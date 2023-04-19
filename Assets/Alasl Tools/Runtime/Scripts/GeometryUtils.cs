using UnityEngine;

namespace AlaslTools
{
    public static class GeometryUtils
    {
        public static bool TriCullTest(Camera camera,Vector3 c, Vector3 u , Vector3 v)
        {
            var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
            c = vp.MultiplyPoint(c);
            u = vp.MultiplyPoint(u);
            v = vp.MultiplyPoint(v);
            Vector3 clipSpaceNormal = Vector3.Cross((u - c), (v - c));
            return clipSpaceNormal.z >= 0;
        }

        public static bool RayTriangleIntersect(this Ray ray, Vector3 a, Vector3 b, Vector3 c
            , ref float t, ref Vector3 bary, ref Vector3 normal)
        {
            Vector3 ba = b - a;
            Vector3 ca = c - a;

            normal = Vector3.Cross(ba, ca);
            float areaABC = normal.magnitude;
            normal.Normalize();

            float ndr = Vector3.Dot(normal, ray.direction);
            if (Mathf.Abs(ndr) < 0.00001f)
                return false;

            float d = Vector3.Dot(normal, a);
            t = (-Vector3.Dot(ray.origin, normal) + d) / ndr;
            if (t < 0) return false;

            Vector3 p = ray.GetPoint(t);

            float areaPBC = Vector3.Dot(normal, Vector3.Cross((b - c), (b - p)));
            bary.x = areaPBC / areaABC;
            if (bary.x < 0)
                return false;

            float areaPCA = Vector3.Dot(normal, Vector3.Cross((c - a), (c - p)));
            bary.y = areaPCA / areaABC;
            if (bary.y < 0)
                return false;

            bary.z = 1.0f - bary.x - bary.y;
            if (bary.z < 0)
                return false;

            return true;
        }
        public static bool RayIntersectQuad(this Ray ray, Vector3[] points, ref float dist, ref Vector3 normal)
        {
            dist = float.MaxValue;
            bool didHit = false;

            float t = 0; Vector3 b = default; Vector3 n = default;
            if (ray.RayTriangleIntersect(points[0], points[1], points[2]
                , ref t, ref b, ref n))
            {
                didHit = true;
                dist = t;
                normal = n;
            }
            if (ray.RayTriangleIntersect(points[0], points[2], points[3]
                , ref t, ref b, ref n))
            {
                didHit = true;
                dist = t;
                normal = n;
            }

            return didHit;
        }
    }
}