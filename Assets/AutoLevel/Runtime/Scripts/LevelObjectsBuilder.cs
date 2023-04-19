using System;
using UnityEngine;

namespace AutoLevel
{
    public class LevelObjectsBuilder : IDisposable
    {

        private LevelData levelData;
        private BlocksRepo.Runtime repo;
        private GameObject[,,] gameObjects;
        public GameObject root;

        public LevelObjectsBuilder(LevelData levelData,
        BlocksRepo.Runtime repo)
        {
            this.repo = repo;
            this.levelData = levelData;
            var size = levelData.Blocks.Size;
            gameObjects = new GameObject[size.z, size.y, size.x];
            root = new GameObject("root");
            root.transform.position = levelData.position;
        }

        public void Rebuild(BoundsInt area)
        {
            foreach (var i in SpatialUtil.Enumerate(area.min, area.max))
            {
                var go = gameObjects[i.z, i.y, i.x];
                if (go != null)
                    SafeDestroy(go);

                var block_h = levelData.Blocks[i];
                if (block_h != 0)
                {
                    go = repo.CreateGameObject(repo.GetBlockIndex(block_h));
                    if (go != null)
                    {
                        go.transform.SetParent(root.transform);
                        go.transform.localPosition = i;
                    }
                }
            }
        }

        public void Dispose()
        {
            SafeDestroy(root);
        }

        private void SafeDestroy(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEngine.Object.DestroyImmediate(obj, false);
            else
#endif
                UnityEngine.Object.Destroy(obj);

        }
    }

}