using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class EditorGUIScheduler
{
    private List<System.Action> afterNextRepaint = new List<System.Action>();
    private bool repaintPass;

    public void Schedule(System.Action action)
    {
        afterNextRepaint.Add(action);
    }

    public void OnSceneGUI()
    {
        if (repaintPass && afterNextRepaint.Count > 0)
        {
            for (int i = 0; i < afterNextRepaint.Count; i++)
                afterNextRepaint[i]();
            afterNextRepaint.Clear();
        }
        repaintPass = Event.current.type == EventType.Repaint && afterNextRepaint.Count > 0;
    }
}

public static class EditorHelper
{
    public static Mesh GetPrimitiveMesh(PrimitiveType meshType)
    {
        var go = GameObject.CreatePrimitive(meshType);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(go);
        return mesh;
    }

    /// <summary>
    /// get a path relative to the asset folder
    /// </summary>
    public static string GetLocalpath(string fullPath)
    {
        var projectPathLength = Application.dataPath.Length - 6;
        return fullPath.Substring(projectPathLength, fullPath.Length - projectPathLength);
    }

    /// <summary>
    /// get the path for any given class, the class file name need to be 
    /// the same as the class name
    /// </summary>
    public static string GetScriptDirectory<T>() where T : Object
    {
        var scriptName = typeof(T).Name;
        scriptName += ".cs";
        var scripts = AssetDatabase.FindAssets("t:script");
        var path = "";
        for (int i = 0; i < scripts.Length; i++)
        {
            var p = AssetDatabase.GUIDToAssetPath(scripts[i]);
            if (p.EndsWith(scriptName))
                path = p;
        }
        return path.Remove(path.Length - scriptName.Length);
    }
}