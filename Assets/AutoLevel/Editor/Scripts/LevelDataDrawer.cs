using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class LevelDataDrawer : System.IDisposable
    {
        Transform root;
        ILevelBuilderData builderData;
        BlocksRepo.Runtime repo;

        public LevelDataDrawer(BlocksRepo.Runtime repo, ILevelBuilderData builderData)
        {
            this.builderData = builderData;
            this.repo = repo;
            Recreate();
        }

        public void Clear()
        {
            if (root != null)
                Object.DestroyImmediate(root.gameObject);
        }

        public void Recreate()
        {
            if (repo == null)
                return;

            Clear();

            root = new GameObject("level_root").transform;
            root.position = builderData.LevelData.position;
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;

            foreach (var index in SpatialUtil.Enumerate(builderData.LevelData.bounds.size))
            {
                var hash = builderData.LevelData.Blocks[index.z, index.y, index.x];
                if (hash != 0 && repo.ContainsBlock(hash))
                {
                    var block = repo.GetBlockResourcesByHash(hash);
                    if (block.mesh != null)
                    {
                        var go = new GameObject();
                        go.hideFlags = HideFlags.HideAndDontSave;
                        go.AddComponent<MeshFilter>().sharedMesh = block.mesh;
                        go.AddComponent<MeshRenderer>().sharedMaterial = block.material;
                        go.transform.SetParent(root.transform);
                        go.transform.localPosition = index;
                    }
                }
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}