using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class LevelExtraObjectBuilder : BaseLevelDataBuilder
    {
        private Array3D<GameObject> objects;

        public LevelExtraObjectBuilder(LevelData levelData, BlocksRepo.Runtime repo)
            : base(levelData, repo)
        {
            objects = new Array3D<GameObject>(levelData.Blocks.Size);
        }

        public override void Rebuild(BoundsInt area)
        {
            root.transform.position = levelData.position;
            foreach (var index in SpatialUtil.Enumerate(area.min, area.max))
            {
                var go = objects[index];
                if (go != null)
                    GameObjectUtil.SafeDestroy(go);

                var block_h = levelData.Blocks[index];
                if (block_h != 0)
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