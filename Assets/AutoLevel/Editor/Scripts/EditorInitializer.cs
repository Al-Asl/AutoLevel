using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AutoLevel
{

    public static class EditorInitializer
    {

        private static bool DebugOn;
        private const string DEBUG_DEFINE = "AUTOLEVEL_DEBUG";
        private const string DEBUG_MENU_PATH = "AutoLevel/Enable Debug";

        [MenuItem(DEBUG_MENU_PATH, isValidateFunction:true)]
        public static bool ToggleDebugValidate()
        {
            DebugOn = false;
            PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,out var defines);
            foreach (var define in defines)
            {
                if (define == DEBUG_DEFINE)
                {
                    DebugOn = true;
                    break;
                }
            }
            Menu.SetChecked(DEBUG_MENU_PATH, DebugOn);
            return true;
        }

        [MenuItem(DEBUG_MENU_PATH)]
        public static void ToggleDebug()
        {
            PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, out var defines);
            var list = new List<string>(defines);
            if (DebugOn)
                list.Remove(DEBUG_DEFINE);
            else
                list.Add(DEBUG_DEFINE);
            defines = list.ToArray();
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines);
        }

        [MenuItem("AutoLevel/Level Builder Window")]
        public static void OpenLeveBuilderWindow()
        {
            EditorWindow.CreateWindow<LevelBuilderWindow>("Level Builder Window");
        }

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