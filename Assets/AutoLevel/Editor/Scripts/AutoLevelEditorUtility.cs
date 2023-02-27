using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public class AutoLevelEditorUtility
    {
        public static void ExportMesh(LevelBuilder builder, string path)
        {
            var so = new LevelBuilderEditor.SO(builder);
            var repo = so.blockRepo.CreateRuntime();
            var partationSize = LevelEditorSettings.GetSettings().settings.ExportSize;
            ExportMesh(so, repo, path, partationSize);
            so.Dispose();
            repo.Dispose();
        }

        public static void ExportObjects(LevelBuilder builder, string path)
        {
            var so = new LevelBuilderEditor.SO(builder);
            var repo = so.blockRepo.CreateRuntime();
            ExportObjects(so, repo, path);
            so.Dispose();
            repo.Dispose();
        }

        public static void ExportMesh(LevelBuilderEditor.SO builder,BlocksRepo.Runtime repo,string path, int partationSize = 5)
        {
            using (LevelMeshBuilder mbuilder = new LevelMeshBuilder(builder.levelData, repo, partationSize))
            {
                mbuilder.Rebuild(new BoundsInt(Vector3Int.zero, builder.levelData.bounds.size));
                ModelExporter.ExportObject(path, mbuilder.root.gameObject);
            }
        }

        public static void ExportObjects(LevelBuilderEditor.SO builder, BlocksRepo.Runtime repo, string path)
        {
            using (var objectsBuilder = new LevelExtraObjectBuilder(builder.levelData, repo))
            {
                var bounds = builder.levelData.bounds;
                bounds.position = Vector3Int.zero;
                objectsBuilder.Rebuild(bounds);
                PrefabUtility.SaveAsPrefabAsset(objectsBuilder.root.gameObject, path);
            }
        }

        public static bool SideCullTest(Camera camera, Vector3 pos, int side)
        {
            return GeometryUtils.TriCullTest(camera, pos, pos + directionsU[side], pos + directionsV[side]);
        }
    }
}