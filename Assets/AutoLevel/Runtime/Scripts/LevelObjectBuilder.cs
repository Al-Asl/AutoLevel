using UnityEngine;
using AlaslTools;
using System.Collections.Generic;

namespace AutoLevel
{
    public class LevelObjectBuilder : BaseLevelDataBuilder
    {
        private List<Array3D<GameObject>> layers;
        private List<Transform> layersRoot;

        public LevelObjectBuilder(LevelData levelData,BlocksRepo.Runtime repo) 
            : base(levelData,repo) 
        {
            layers = new List<Array3D<GameObject>>();
            for (int i = 0; i < levelData.LayersCount; i++)
                layers.Add(new Array3D<GameObject>(levelData.size));

            layersRoot = new List<Transform>(levelData.LayersCount);
            for (int i = 0; i < levelData.LayersCount; i++)
            {
                var go = new GameObject($"layer {i}");
                go.transform.SetParent(root);
                layersRoot.Add(go.transform);
            }
        }

        public override void Rebuild(BoundsInt area, int layer)
        {
            var blocks = levelData.GetLayer(layer).Blocks;
            var objects = layers[layer];

            root.transform.position = levelData.bounds.position;

            foreach (var index in SpatialUtil.Enumerate(area))
            {
                var go = objects[index];
                if (go != null)
                    GameObjectUtil.SafeDestroy(go);

                var block = blocks[index];

                if (block == 0)
                    continue;

                var res = repo.GetBlockResources(repo.GetBlockIndex(block));
                if (res.mesh == null)
                    continue;

                if (!ShouldInclude(index, layer))
                    continue;

                go = new GameObject(res.mesh.name);
                go.transform.SetParent(layersRoot[layer]);
                go.AddComponent<MeshFilter>().sharedMesh = res.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = res.material;
                go.transform.localPosition = index;
                objects[index] = go;
            }
        }

        public override void Dispose()
        {
            GameObjectUtil.SafeDestroy(root);
        }
    }
}
