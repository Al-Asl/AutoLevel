using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class LevelObjectBuilder : BaseLevelDataBuilder
    {
        private Array3D<GameObject> objects;

        public LevelObjectBuilder(LevelData levelData,BlocksRepo.Runtime repo) 
            : base(levelData,repo) 
        {
            objects = new Array3D<GameObject>(levelData.Blocks.Size);
        }

        public override void Rebuild(BoundsInt area)
        {
            root.transform.position = levelData.bounds.position;

            foreach (var index in SpatialUtil.Enumerate(area))
            {
                var go = objects[index];
                if (go != null)
                    GameObjectUtil.SafeDestroy(go);

                var res = repo.GetBlockResources(repo.GetBlockIndex(levelData.Blocks[index]));
                if (res.mesh == null)
                    continue;

                go = new GameObject(res.mesh.name);
                go.transform.SetParent(root);
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
