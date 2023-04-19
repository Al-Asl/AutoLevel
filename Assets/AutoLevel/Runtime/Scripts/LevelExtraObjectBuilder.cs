using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class LevelExtraObjectBuilder : BaseLevelDataBuilder
    {
        private List<Array3D<GameObject>> layers;

        public LevelExtraObjectBuilder(LevelData levelData, BlocksRepo.Runtime repo)
            : base(levelData, repo)
        {
            layers = new List<Array3D<GameObject>>();
            for (int i = 0; i < levelData.LayersCount; i++)
                layers.Add(new Array3D<GameObject>(levelData.size));
        }

        public override void Rebuild(BoundsInt area, int layer)
        {
            var blocks = levelData.GetLayer(layer).Blocks;
            var objects = layers[layer];

            root.transform.position = levelData.position;
            foreach (var index in SpatialUtil.Enumerate(area.min, area.max))
            {
                var go = objects[index];
                if (go != null)
                    GameObjectUtil.SafeDestroy(go);

                var block_h = blocks[index];

                if (block_h != 0 && ShouldInclude(index, layer))
                {
                    go = repo.CreateGameObject(repo.GetBlockIndex(block_h));
                    if (go != null)
                    {
                        go.transform.SetParent(root.transform);
                        go.transform.localPosition = index;
                        objects[index] = go;
                    }
                }
            }
        }

        public override void Dispose()
        {
            GameObjectUtil.SafeDestroy(root);
        }
    }

}