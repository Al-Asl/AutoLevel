using UnityEngine;
using UnityEditor;

namespace AlaslTools
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
        /// get the path for any given type assembly, the asmdef file name need to be 
        /// the same as the assembly name
        /// </summary>
        public static string GetAssemblyDirectory<T>()
        {
            var asseblyName = typeof(T).Assembly.GetName().Name;
            asseblyName += ".asmdef";
            var assemblies = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            for (int i = 0; i < assemblies.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(assemblies[i]);
                if (path.EndsWith(asseblyName))
                    return path.Remove(path.Length - asseblyName.Length);
            }
            throw new System.Exception("path not found!");
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
            for (int i = 0; i < scripts.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(scripts[i]);
                if (path.EndsWith(scriptName))
                    return path.Remove(path.Length - scriptName.Length);
            }
            throw new System.Exception("path not found!");
            
        }
    }

}