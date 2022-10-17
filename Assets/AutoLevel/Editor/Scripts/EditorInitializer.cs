using UnityEngine;
using UnityEditor;

namespace AutoLevel
{

    public static class EditorInitializer
    {
        [MenuItem("GameObject/AutoLevel/Blocks Repo", priority = 16)]
        public static void CreateRepo()
        {
            GameObject repo_go = new GameObject("Blocks Repo");
            repo_go.AddComponent<BlocksRepo>();
            Undo.RegisterCreatedObjectUndo(repo_go, "Create Repo");
            Selection.activeObject = repo_go;
        }

        [MenuItem("GameObject/AutoLevel/Builder", priority = 15)]
        public static void CreateBuilder()
        {
            GameObject builder_go = new GameObject("Builder");
            builder_go.AddComponent<LevelBuilder>();
            Undo.RegisterCreatedObjectUndo(builder_go, "Create Builder");
            Selection.activeObject = builder_go;
        }

        [MenuItem("GameObject/AutoLevel/BigBlock", priority = 17)]
        public static void CreateBigBlock()
        {
            GameObject bigblock_go = new GameObject("bigBlock");
            bigblock_go.AddComponent<BigBlockAsset>();
            Undo.RegisterCreatedObjectUndo(bigblock_go, "Create BigBlock");
            Selection.activeObject = bigblock_go;
        }
    }

}