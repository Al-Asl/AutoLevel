using UnityEngine;
using UnityEditor;

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
}