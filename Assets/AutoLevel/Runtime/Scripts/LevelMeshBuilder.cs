using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    public class LevelMeshBuilder : BaseLevelDataBuilder
    {
        struct TileGroup
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Mesh mesh;
            public MeshCollider collider;
        }

        private int tilesPerGroup;

        private TileGroup[,,] groups;

        public LevelMeshBuilder(LevelData levelData,
        BlocksRepo.Runtime blockRepo, int tilesPerGroup = 5) : base(levelData, blockRepo)
        {
            this.tilesPerGroup = tilesPerGroup;
            var groupsSize = Vector3Int.CeilToInt(new Vector3(
                levelData.bounds.size.x * 1f / tilesPerGroup,
                levelData.bounds.size.y * 1f / tilesPerGroup,
                levelData.bounds.size.z * 1f / tilesPerGroup));
            groups = new TileGroup[groupsSize.z, groupsSize.y, groupsSize.x];

            foreach (var index in SpatialUtil.Enumerate(groupsSize))
            {
                var go = new GameObject($"group {index}");
                go.transform.SetParent(root);
                go.transform.localPosition = index * tilesPerGroup;
                var mesh = new Mesh();
                go.AddComponent<MeshFilter>().mesh = mesh;
                var group = new TileGroup()
                {
                    mesh = mesh,
                    transform = go.transform,
                    renderer = go.AddComponent<MeshRenderer>(),
                    collider = go.AddComponent<MeshCollider>()
                };
                groups[index.z, index.y, index.x] = group;
            }
        }

        public override void Rebuild(BoundsInt area)
        {
            root.transform.position = levelData.bounds.position;
            var gStart = area.min / tilesPerGroup;
            var gEnd = (area.max - Vector3Int.one) / tilesPerGroup + Vector3Int.one;
            foreach (var index in SpatialUtil.Enumerate(gStart, gEnd))
                RebuildGroup(index);
        }

        struct MeshInstance
        {
            public Mesh mesh;
            public Material material;
            public Vector3 offset;
        }

        void RebuildGroup(Vector3Int index)
        {
            var area = GetGroupBoundary(index);
            var group = groups[index.z, index.y, index.x];

            List<MeshInstance> meshes = new List<MeshInstance>(tilesPerGroup * tilesPerGroup * 2);
            Dictionary<Material, int> materialsHistogram = new Dictionary<Material, int>();

            foreach (var i in SpatialUtil.Enumerate(area.min, area.max))
            {
                var block_h = levelData.Blocks[i.z, i.y, i.x];
                if (block_h != -1)
                {
                    var block = repo.GetBlockResourcesByHash(block_h);
                    if (block.mesh != null)
                    {
                        if (materialsHistogram.ContainsKey(block.material))
                            materialsHistogram[block.material]++;
                        else
                            materialsHistogram.Add(block.material, 1);

                        meshes.Add(new MeshInstance()
                        {
                            mesh = block.mesh,
                            material = block.material,
                            offset = i - area.position
                        });
                    }
                }
            }
            int materialCount = materialsHistogram.Count;
            int meshCount = meshes.Count;

            Material[] materials = new Material[materialCount];
            var counter = 0;
            var preValue = 0;
            foreach (var item in materialsHistogram)
                materials[counter++] = item.Key;
            for (int i = 0; i < materialCount; i++)
            {
                var mat = materials[i];
                preValue += materialsHistogram[mat];
                materialsHistogram[mat] = preValue;
            }

            MeshInstance[] sortedMeshs = new MeshInstance[meshCount];
            for (int i = 0; i < meshCount; i++)
            {
                var mesh = meshes[i];
                var mIndex = --materialsHistogram[mesh.material];
                sortedMeshs[mIndex] = mesh;
            }

            CombineInstance[][] combineInstances = new CombineInstance[materialCount][];
            for (int i = 0; i < materialCount; i++)
            {
                int start = materialsHistogram[materials[i]];
                int end = i == materialCount - 1 ? meshes.Count : materialsHistogram[materials[i + 1]];
                var ci = new CombineInstance[end - start];
                for (int j = start; j < end; j++)
                {
                    var m = sortedMeshs[j];
                    ci[j - start] = new CombineInstance()
                    {
                        mesh = m.mesh,
                        transform = Matrix4x4.Translate(m.offset),
                        subMeshIndex = 0
                    };
                }
                combineInstances[i] = ci;
            }

            CombineInstance[] subMeshes = new CombineInstance[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                var m = new Mesh();
                m.CombineMeshes(combineInstances[i]);
                subMeshes[i] = new CombineInstance()
                {
                    mesh = m,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };
            }

            group.mesh.CombineMeshes(subMeshes, false);
            group.renderer.sharedMaterials = materials;

            for (int i = 0; i < materialCount; i++)
                GameObjectUtil.SafeDestroy(subMeshes[i].mesh);
        }

        BoundsInt GetGroupBoundary(Vector3Int index)
        {
            var start = index * tilesPerGroup;
            var end = start + Vector3Int.one * tilesPerGroup;
            end = Vector3Int.Min(end, levelData.bounds.size);
            return new BoundsInt(start, end - start);
        }

        public override void Dispose()
        {
            foreach (var index in SpatialUtil.Enumerate(groups))
                GameObjectUtil.SafeDestroy(groups[index.z, index.y, index.x].mesh);
            if(root != null)
                GameObjectUtil.SafeDestroy(root.gameObject);
        }
    }

}