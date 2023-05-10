using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static FillUtility;

    public class BlockAssetSO : BaseSO<BlockAsset>
    {
        public int                          group;
        public int                          weightGroup;

        public List<int>                    actionsGroups;
        public List<BlockAsset.VariantDesc> variants;

        public BlockAssetSO(SerializedObject serializedObject) : base(serializedObject) { IntegrityCheck(this); }
        public BlockAssetSO(Object target) : base(target) { IntegrityCheck(this); }

        public static void IntegrityCheck(BlockAsset blockAsset) 
        {  var so = new BlockAssetSO(blockAsset); so.Dispose(); }

        private static T GetComponentInParent<T>(Transform transform) 
            where T : Component
        {
            var comp = transform.GetComponent<T>();
            if (comp != null)
                return comp;
            else
            {
                if (transform.parent == null)
                    return null;
                else
                    return GetComponentInParent<T>(transform.parent);
            }
        }

        private static void IntegrityCheck(BlockAssetSO so)
        {
            var repo = GetComponentInParent<BlocksRepo>(so.target.transform);
            if (repo == null)
            {
                Debug.LogError("Integrity check failed, the asset is not a child of a repo!");
                return;
            }

            if (so.variants.Count == 0)
            {
                so.variants.Add(new BlockAsset.VariantDesc()
                {
                    fill = GenerateFill(so.target.gameObject),
                    sideIds = new ConnectionsIds()
                });
                so.ApplyField(nameof(variants));
            }

            var groupNames = repo.GetAllGroupsNames();

            if (so.group == 0 || groupNames.FindIndex((name) => name.GetHashCode() == so.group) == -1)
            {
                //adding the base group
                so.group = groupNames[2].GetHashCode();
                so.ApplyField(nameof(group));
            }

            var weightGroupNames = repo.GetAllWeightGroupsNames();

            if (so.weightGroup == 0 || weightGroupNames.FindIndex((name) => name.GetHashCode() == so.weightGroup) == -1)
            {
                //adding the base group
                so.weightGroup = weightGroupNames[2].GetHashCode();
                so.ApplyField(nameof(weightGroup));
            }
        }

        private static int GenerateFill(GameObject gameObject)
        {
            Mesh mesh = BlockUtility.GetMesh(gameObject);

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