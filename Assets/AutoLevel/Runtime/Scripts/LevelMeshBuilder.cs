using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    public class LevelMeshBuilder : BaseLevelDataBuilder
    {
        public Transform MeshRoot   => mesh_root;
        public Transform ObjectRoot => levelObjectBuilder.root;

        struct TileGroup
        {
            public Transform transform;
            public Mesh mesh;
            public MeshRenderer renderer;
            public MeshCollider collider;
        }

        private int tilesPerGroup;

        private List<TileGroup[,,]> meshesPerLayer;
        private Transform           mesh_root;

        private bool                buildObjects = true;
        private LevelObjectBuilder  levelObjectBuilder;

        public void EnableObjects(bool enable)
        {
            levelObjectBuilder.root.gameObject.SetActive(enable);
            buildObjects = enable;
        }

        public LevelMeshBuilder(LevelData levelData,
        BlocksRepo.Runtime blockRepo, int tilesPerGroup = 5) : base(levelData, blockRepo)
        {
            var size = levelData.bounds.size;
            this.tilesPerGroup = tilesPerGroup;
            var groupsSize = Vector3Int.CeilToInt(new Vector3(
                size.x * 1f / tilesPerGroup,
                size.y * 1f / tilesPerGroup,
                size.z * 1f / tilesPerGroup));

            meshesPerLayer      = new List<TileGroup[,,]>(levelData.LayersCount);
            mesh_root       = new GameObject("mesh_root").transform;
            mesh_root.SetParent(root);

            for (int i = 0; i < levelData.LayersCount; i++)
            {
                var layer = new TileGroup[groupsSize.z, groupsSize.y, groupsSize.x];
                var layerTransform = new GameObject($"Layer {i}").transform;
                layerTransform.transform.SetParent(mesh_root);

                foreach (var index in SpatialUtil.Enumerate(groupsSize))
                {
                    var go = new GameObject($"group {index}");
                    go.transform.SetParent(layerTransform);
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

                    layer[index.z, index.y, index.x] = group;
                }

                meshesPerLayer.Add(layer);
            }

            levelObjectBuilder = new LevelObjectBuilder(levelData, blockRepo);
            levelObjectBuilder.OverrideBlockCreation((blockIndex) =>
            {
                var go = repo.GetMeshResource().GetGameObject(blockIndex);

                if (go == null)
                    return null;

                var p = new GameObject(go.name);
                go.transform.SetParent(p.transform, true);

                return p;
            });

            levelObjectBuilder.root.gameObject.name =  "objects_root";
            levelObjectBuilder.root.SetParent(root);
        }

        public override void Rebuild(BoundsInt area, int layer)
        {
            mesh_root.transform.position = levelData.position;

            RebuildMesh(area, layer);
            if (buildObjects)
                levelObjectBuilder.Rebuild(area, layer);
        }

        private void RebuildMesh(BoundsInt area, int layer)
        {
            var gStart = area.min / tilesPerGroup;
            var gEnd = (area.max - Vector3Int.one) / tilesPerGroup + Vector3Int.one;
            foreach (var index in SpatialUtil.Enumerate(gStart, gEnd))
                RebuildGroup(index, layer);
        }

        private void RebuildGroup(Vector3Int index, int layer)
        {
            var area = GetGroupBoundary(index);
            var group = meshesPerLayer[layer][index.z, index.y, index.x];
            var blocks = levelData.GetLayer(layer).Blocks;

            List<MeshCombiner.RendererInfo> infos = new List<MeshCombiner.RendererInfo>();
            var resourceManager = repo.GetMeshResource();

            foreach (var i in SpatialUtil.Enumerate(area.min, area.max))
            {
                var block = blocks[i.z, i.y, i.x];
                if (block != 0 && ShouldInclude(index, layer))
                {
                    var info = resourceManager.GetRendererInfo(repo.GetBlockIndex(block));

                    if (info.mesh != null)
                    {
                        info.matrix = Matrix4x4.Translate(i - area.position);
                        infos.Add(info);
                    }
                }
            }

            var result = MeshCombiner.RendererInfo.Create();

            result.mesh = group.mesh;
            MeshCombiner.Combine(infos,result);

            group.renderer.materials = result.materials.ToArray();
        }

        public override void Clear(int layer)
        {
            var meshes = meshesPerLayer[layer];

            foreach(var index in SpatialUtil.Enumerate(meshes))
                meshes[index.z, index.y, index.x].mesh.Clear();

            levelObjectBuilder.Clear(layer);
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
            if (root != null)
                GameObjectUtil.SafeDestroy(root.gameObject);

            for (int i = 0; i < meshesPerLayer.Count; i++)
            {
                var layer = meshesPerLayer[i];
                foreach (var index in SpatialUtil.Enumerate(layer))
                    GameObjectUtil.SafeDestroy(layer[index.z, index.y, index.x].mesh);
            }

            levelObjectBuilder?.Dispose();
        }
    }

}