using UnityEngine;
using AlaslTools;
using System.Collections.Generic;

namespace AutoLevel
{

    public static class FillUtility
    {
        public static readonly Vector3[] nodes = new Vector3[]
        {
            new Vector3(0,0,0),
            new Vector3(1,0,0),
            new Vector3(0,0,1),
            new Vector3(1,0,1),
            new Vector3(0,1,0),
            new Vector3(1,1,0),
            new Vector3(0,1,1),
            new Vector3(1,1,1)
        };

        public static int GetSide(int code, int side)
        {
            if (side % 3 == 0)
                code = MirrorFill(RotateFill(RotateFill(code, Axis.y), Axis.x), Axis.x);
            else if (side % 3 == 2)
                code = RotateFill(MirrorFill(code, Axis.z), Axis.x);

            if (side >= 3)
                code = MirrorFill(code, Axis.y);

            return code & 0xF;
        }

        public static int MirrorFill(int fill, Axis axis)
        {
            switch (axis)
            {
                case Axis.x:
                    return (fill & 0x55) << 1 | (fill & 0xaa) >> 1;
                case Axis.y:
                    return (fill & 0xf) << 4 | (fill & 0xf0) >> 4;
                case Axis.z:
                    return (fill & 0x33) << 2 | (fill & 0xcc) >> 2;
            }

            return fill;
        }

        public static int RotateFill(int fill, Axis axis)
        {
            switch (axis)
            {
                case Axis.x:
                    return (0x3 & fill) << 4 | (0xc & fill) >> 2 |
                         (0x30 & fill) << 2 | (0xc0 & fill) >> 4;
                case Axis.y:
                    return (0x11 & fill) << 2 | (0x22 & fill) >> 1 |
                        (0x44 & fill) << 1 | (0x88 & fill) >> 2;
                case Axis.z:
                    return (0x5 & fill) << 1 | (0xa & fill) << 4 |
                         (0x50 & fill) >> 4 | (0xa0 & fill) >> 1;
            }
            return fill;
        }

        public static int FlipFill(int fill)
        {
            return ~fill & 255;
        }

        public static int GenerateFill(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            if (!mesh.isReadable)
                Debug.LogWarning("make the mesh readable, if you are generating in runtime");

            int fill = 0;
            var verts = new List<Vector3>();
            var indices = new List<int>();
            mesh.GetVertices(verts);
            mesh.GetIndices(indices, 0);

            float e = 0.0001f;
            Ray[] rays = new Ray[]
            {
            new Ray(nodes[1] + new Vector3(0.1f,e,e),Vector3.left),
            new Ray(nodes[3] + new Vector3(0.1f,e,-e),Vector3.left),
            new Ray(nodes[5] + new Vector3(0.1f,-e,e),Vector3.left),
            new Ray(nodes[7] + new Vector3(0.1f,-e,-e),Vector3.left),

            new Ray(nodes[4] + new Vector3(e,0.1f,e),Vector3.down),
            new Ray(nodes[5] + new Vector3(-e,0.1f,e),Vector3.down),
            new Ray(nodes[6] + new Vector3(e,0.1f,-e),Vector3.down),
            new Ray(nodes[7] + new Vector3(-e,0.1f,-e),Vector3.down),

            new Ray(nodes[2] + new Vector3(e,e,0.1f),Vector3.back),
            new Ray(nodes[3] + new Vector3(-e,e,0.1f),Vector3.back),
            new Ray(nodes[6] + new Vector3(e,-e,0.1f),Vector3.back),
            new Ray(nodes[7] + new Vector3(-e,-e,0.1f),Vector3.back),
            };
            Vector2Int[] rayNodes = new Vector2Int[]
            {
            new Vector2Int(1,0),
            new Vector2Int(3,2),
            new Vector2Int(5,4),
            new Vector2Int(7,6),
            new Vector2Int(4,0),
            new Vector2Int(5,1),
            new Vector2Int(6,2),
            new Vector2Int(7,3),
            new Vector2Int(2,0),
            new Vector2Int(3,1),
            new Vector2Int(6,4),
            new Vector2Int(7,5),
            };
            bool[] rayHit = new bool[12];
            for (int i = 0; i < indices.Count; i += 3)
            {
                var v0 = verts[indices[i]];
                var v1 = verts[indices[i + 1]];
                var v2 = verts[indices[i + 2]];
                float t = 0;
                Vector3 normal = Vector3.zero;
                Vector3 bary = Vector3.zero;

                for (int j = 0; j < rays.Length; j++)
                {
                    if (rays[j].TriangleIntersection(v0, v1, v2,
                        ref t, ref bary, ref normal))
                    {
                        rayHit[j] = true;
                        int bitShift;
                        if (Vector3.Dot(rays[j].direction, normal) > 0)
                            bitShift = rayNodes[j].x;
                        else
                            bitShift = rayNodes[j].y;
                        fill |= 1 << bitShift;
                    }
                }
            }

            System.Action validateNeighbour = () =>
            {
                for (int i = 0; i < rayNodes.Length; i++)
                {
                    if (!rayHit[i])
                    {
                        var ind = rayNodes[i];
                        if (
                            ((fill & (1 << ind.x)) > 0) ||
                            ((fill & (1 << ind.y)) > 0))
                        {
                            fill |= 1 << ind.x;
                            fill |= 1 << ind.y;
                        }
                    }
                }
            };
            validateNeighbour();
            validateNeighbour();

            return fill;
        }
    }

}