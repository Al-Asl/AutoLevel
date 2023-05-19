using AlaslTools;
using System;
using UnityEngine;

namespace AutoLevel
{

    public abstract class BaseLevelDataBuilder : IDisposable
    {
        public Transform root;

        protected LevelData levelData;
        protected BlocksRepo.Runtime repo;

        protected BaseLevelDataBuilder(
            LevelData           levelData,
            BlocksRepo.Runtime  blockRepo)
        {
            this.repo = blockRepo;
            this.levelData = levelData;
            root = new GameObject("root").transform;
        }

        public bool ShouldInclude(Vector3Int index, int layer)
        {
            for (int i = layer + 1; i < repo.LayersCount; i++)
            {
                var block = levelData.GetLayer(i).Blocks[index];

                if (block == 0) break;

                var placement = repo.GetBlockPlacement(repo.GetBlockIndex(block));

                if (i == layer + 1)
                    if (placement == BlockPlacement.ReplaceFirst)
                        return false;

                if (placement == BlockPlacement.Replace)
                    return false;
            }
            return true;
        }

        public void RebuildAll()
        {
            for (int i = 0; i < levelData.LayersCount; i++)
                Rebuild(new BoundsInt(Vector3Int.zero, levelData.size), i);
        }

        public void ClearAll()
        {
            for (int i = 0; i < levelData.LayersCount; i++)
                Clear(i);
        }

        public abstract void Rebuild(BoundsInt area, int layer);
        public abstract void Clear(int layer);

        public virtual void Dispose()
        {
            if (root != null)
                GameObjectUtil.SafeDestroy(root.gameObject);
        }
    }

}