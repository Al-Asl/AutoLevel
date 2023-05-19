using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public class AutoLevelEditorUtility
    {
        public static void ExportMeshAndObjects(LevelBuilder builder, string path)
        {
            var repo = builder.data.BlockRepo.CreateRuntime();
            var partationSize = LevelEditorSettings.GetSettings().settings.ExportSize;
            ExportMeshAndObjects(builder.data, repo, path, partationSize);
            repo.Dispose();
        }

        public static void ExportMeshAndObjects(ILevelBuilderData builderData,BlocksRepo.Runtime repo,string path, int partationSize = 5)
        {
            using (LevelMeshBuilder mbuilder = new LevelMeshBuilder(builderData.LevelData , repo, partationSize))
            {
                mbuilder.RebuildAll();
                mbuilder.ObjectRoot.SetParent(null);
                ModelExporter.ExportObject(path+".fbx", mbuilder.root.gameObject);
                mbuilder.ObjectRoot.SetParent(mbuilder.root);
                PrefabUtility.SaveAsPrefabAsset(mbuilder.ObjectRoot.gameObject,path+".prefab");
            }
        }

        public static void ExportObjects(LevelBuilder builder, string path)
        {
            var repo = builder.data.BlockRepo.CreateRuntime();
            ExportObjects(builder.data, repo, path);
            repo.Dispose();
        }

        public static void ExportObjects(ILevelBuilderData builderData, BlocksRepo.Runtime repo, string path)
        {
            using (LevelObjectBuilder mbuilder = new LevelObjectBuilder(builderData.LevelData, repo))
            {
                mbuilder.RebuildAll();
                PrefabUtility.SaveAsPrefabAsset(mbuilder.root.gameObject, path + ".prefab");
            }
        }

        public static bool SideCullTest(Camera camera, Vector3 pos, int side)
        {
            return GeometryUtils.TriCullTest(camera, pos, pos + directionsU[side], pos + directionsV[side]);
        }
    }
}