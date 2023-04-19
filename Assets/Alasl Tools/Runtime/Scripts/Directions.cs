using UnityEngine;

namespace AlaslTools
{

    public enum DirectionMask
    {
        None = 0,
        All = 63,
        Left = 1,
        Down = 2,
        Backward = 4,
        Right = 8,
        Up = 16,
        Forward = 32
    }

    public enum Direction
    {
        Left = 0,
        Down = 1,
        Backward = 2,
        Right = 3,
        Up = 4,
        Forward = 5
    }

    /// <summary>
    /// direction for Voxel workflow
    /// </summary>
    public static class Directions
    {
        public static readonly Vector3[] directions = new Vector3[]
        {
            new Vector3(-1,0,0),
            new Vector3(0,-1,0),
            new Vector3(0,0,-1),
            new Vector3(1,0,0),
            new Vector3(0,1,0),
            new Vector3(0,0,1),
        };

        public static readonly Vector3[] directionsU = new Vector3[]
        {
            new Vector3(0,0,-1),
            new Vector3(1,0,0),
            new Vector3(1,0,0),
            new Vector3(0,0,1),
            new Vector3(1,0,0),
            new Vector3(-1,0,0),
        };

        public static readonly Vector3[] directionsV = new Vector3[]
        {
            new Vector3(0,1,0),
            new Vector3(0,0,-1),
            new Vector3(0,1,0),
            new Vector3(0,1,0),
            new Vector3(0,0,1),
            new Vector3(0,1,0),
        };

        public static readonly Vector3[] origins = new Vector3[]
        {
            new Vector3(0,0.5f,0.5f),
            new Vector3(0.5f,0,0.5f),
            new Vector3(0.5f,0.5f,0),
            new Vector3(1f,0.5f,0.5f),
            new Vector3(0.5f,1f,0.5f),
            new Vector3(0.5f,0.5f,1f)
        };

        public static Vector3 GetSideCenter(Transform transform, int d) => GetSideCenter(transform.position, d);
        public static Vector3 GetSideCenter(Vector3 position, int d)
        {
            return position + origins[d];
        }

        public static Vector3 GetSideU(Transform transform, int d) => GetSideU(transform.position, d);
        public static Vector3 GetSideU(Vector3 position, int d)
        {
            return position + origins[d] + directionsU[d];
        }

        public static Vector3 GetSideV(Transform transform, int d) => GetSideV(transform.position, d);
        public static Vector3 GetSideV(Vector3 position, int d)
        {
            return position + origins[d] + directionsV[d];
        }

        public static readonly int[] opposite = { 3, 4, 5, 0, 1, 2 };

        public static readonly Vector3Int[] delta = new Vector3Int[]
        {
            new Vector3Int(-1,0,0),
            new Vector3Int(0,-1,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(1,0,0),
            new Vector3Int(0,1,0),
            new Vector3Int(0,0,1),
        };
    }
}