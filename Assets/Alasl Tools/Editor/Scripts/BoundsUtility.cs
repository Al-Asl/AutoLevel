using UnityEngine;
using UnityEditor;

namespace AlaslTools
{

    public static class BoundsUtility
    {
        public static Vector3 ClosestCornerToScreenPoint(Bounds bounds, Vector2 point)
        {
            return ClosestCornerToScreenPoint(GetCorners(bounds), point);
        }

        public static Vector3 ClosestCornerToScreenPoint(BoundsInt bounds, Vector2 point)
        {
            return ClosestCornerToScreenPoint(GetCorners(bounds), point);
        }

        public static Vector3 ClosestCornerToScreenPoint(Vector3[] corners, Vector2 point)
        {
            var d = float.MaxValue;
            int p = default;
            for (int i = 0; i < corners.Length; i++)
            {
                var cp = HandleUtility.WorldToGUIPoint(corners[i]);
                var cd = Vector2.Distance(cp, point);
                if (cd < d)
                {
                    d = cd;
                    p = i;
                }
            }
            return corners[p];
        }

        public static Vector3[] GetCorners(Bounds bounds)
        {
            return new Vector3[]
            {
                new Vector3(bounds.min.x,bounds.min.y,bounds.min.z),
                new Vector3(bounds.max.x,bounds.min.y,bounds.min.z),
                new Vector3(bounds.min.x,bounds.min.y,bounds.max.z),
                new Vector3(bounds.max.x,bounds.min.y,bounds.max.z),
                new Vector3(bounds.min.x,bounds.max.y,bounds.min.z),
                new Vector3(bounds.max.x,bounds.max.y,bounds.min.z),
                new Vector3(bounds.min.x,bounds.max.y,bounds.max.z),
                new Vector3(bounds.max.x,bounds.max.y,bounds.max.z)
            };
        }

        public static Vector3[] GetCorners(BoundsInt bounds)
        {
            return new Vector3[]
            {
                new Vector3(bounds.min.x,bounds.min.y,bounds.min.z),
                new Vector3(bounds.max.x,bounds.min.y,bounds.min.z),
                new Vector3(bounds.min.x,bounds.min.y,bounds.max.z),
                new Vector3(bounds.max.x,bounds.min.y,bounds.max.z),
                new Vector3(bounds.min.x,bounds.max.y,bounds.min.z),
                new Vector3(bounds.max.x,bounds.max.y,bounds.min.z),
                new Vector3(bounds.min.x,bounds.max.y,bounds.max.z),
                new Vector3(bounds.max.x,bounds.max.y,bounds.max.z)
            };
        }
    }

}