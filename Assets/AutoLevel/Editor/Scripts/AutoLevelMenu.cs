using UnityEditor;
using UnityEngine;
using System.IO;

namespace AutoLevel
{
    public static class AutoLevelMenu 
    {
        [MenuItem("AutoLevel/Export All Meshes")]
        public static void ExportMeshes()
        {
            var builders = Object.FindObjectsOfType<LevelBuilder>();

            var path = EditorUtility.OpenFolderPanel("Mesh Export", Application.dataPath,"");
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var builder in builders)
            {
                var filePath = Path.Combine(path, builder.name + ".fbx");
                if(File.Exists(filePath))
                    File.Delete(filePath);

                AutoLevelEditorUtility.ExportMesh(builder, filePath);
            }
        }

        [MenuItem("AutoLevel/Export All Objects")]
        public static void ExportObjects()
        {
            var builders = Object.FindObjectsOfType<LevelBuilder>();

            var path = EditorUtility.OpenFolderPanel("Objects Export", Application.dataPath, "");
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var builder in builders)
            {
                var filePath = Path.Combine(path, builder.name + ".prefab");
                if (File.Exists(filePath))
                    File.Delete(filePath);

                AutoLevelEditorUtility.ExportObjects(builder, filePath);
            }
        }
    }
}