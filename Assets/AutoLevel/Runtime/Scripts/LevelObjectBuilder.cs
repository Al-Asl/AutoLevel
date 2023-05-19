using UnityEngine;
using AlaslTools;
using System.Collections.Generic;

namespace AutoLevel
{
    public class LevelObjectBuilder : BaseLevelDataBuilder
    {
        private List<Transform[,,]> objectsPerLayer;

        private List<Transform> objectsLayerRoots;

        private System.Func<int, GameObject> CreateBlockFn;

        public void OverrideBlockCreation(System.Func<int, GameObject> CreateBlockFn)
        {
            this.CreateBlockFn = CreateBlockFn;
        }

        public LevelObjectBuilder(LevelData levelData, BlocksRepo.Runtime blockRepo) 
            : base(levelData, blockRepo)
        {
            var size = levelData.bounds.size;
            objectsLayerRoots = new List<Transform>();

            objectsPerLayer = new List<Transform[,,]>(levelData.LayersCount);

            for (int i = 0; i < levelData.LayersCount; i++)
            {
                var layer = new Transform[size.z, size.y, size.x];
                var layerTransform = new GameObject($"Layer {i}").transform;
                layerTransform.SetParent(root);

                objectsLayerRoots.Add(layerTransform);
                objectsPerLayer.Add(layer);
            }

            CreateBlockFn = Create;
        }

        public override void Clear(int layer)
        {
            var objects = objectsPerLayer[layer];

            foreach (var index in SpatialUtil.Enumerate(levelData.size))
            {
                var go = objects[index.z,index.y,index.x];
                if (go != null)
                    GameObjectUtil.SafeDestroy(go.gameObject);
            }
        }

        public override void Rebuild(BoundsInt area, int layer)
        {
            this.root.transform.position = levelData.position;

            var blocks = levelData.GetLayer(layer).Blocks;

            var root = objectsLayerRoots[layer];
            var objects = objectsPerLayer[layer];

            foreach (var index in SpatialUtil.Enumerate(area))
            {
                var obj = objects[index.z, index.y, index.x];

                if (obj != null)
                    GameObjectUtil.SafeDestroy(obj.gameObject);

                var block = blocks[index];

                if (block == 0 || !ShouldInclude(index, layer))
                    continue;

                var go = CreateBlockFn(repo.GetBlockIndex(block));

                if (go != null)
                {
                    go.transform.SetParent(root);
                    go.transform.localPosition = index;
                    objects[index.z, index.y, index.x] = go.transform;
                }

            }
        }

        private GameObject Create(int blockIndex)
        {
            return repo.CreateGameObject(blockIndex);
        }
    }
}
