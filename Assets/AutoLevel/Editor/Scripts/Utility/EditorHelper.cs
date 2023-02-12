using UnityEngine;
using UnityEditor;

namespace AutoLevel
{

    public static class EditorHelper
    {
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

}