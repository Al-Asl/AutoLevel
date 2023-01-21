using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using System.IO;

namespace AutoLevel
{
    public class LevelBuilderWindow : EditorWindow
    {
        [MenuItem("AutoLevel/Level Builder Window")]
        static void Open()
        {
            var window = CreateWindow<LevelBuilderWindow>("Level Builder Window");
        }

        private LevelEditorSettingsEditor SettingsEditor;

        private List<LevelBuilderEditor.SO> builders;
        private Dictionary<BlocksRepo,BlocksRepo.Runtime> repos;

        private List<HashSet<LevelBuilderEditor.SO>> builderGroups;
        private List<LevelDataDrawer> levelDataDrawers = new List<LevelDataDrawer>();
        private List<InputWaveDrawer> inputWaveDrawers = new List<InputWaveDrawer>();

        private Vector2 scrollPos;
        private int selectedGroup = -1;

        private string result = "";

        private bool[][] builderToggles;

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGUI;
            Undo.undoRedoPerformed += UndoRedoPerformed;

            var settings = LevelEditorSettings.GetSettings();
            SettingsEditor = (LevelEditorSettingsEditor)Editor.CreateEditor(settings, typeof(LevelEditorSettingsEditor));

            var allBuilders = FindObjectsOfType<LevelBuilder>();
            List<LevelBuilder> validBuilders = new List<LevelBuilder>();
            repos = new Dictionary<BlocksRepo, BlocksRepo.Runtime>();

            foreach(var builder in allBuilders)
            {
                var data = builder.data;

                if (data.BlockRepo == null)
                    continue;

                validBuilders.Add(builder);

                if (!repos.ContainsKey(data.BlockRepo))
                    repos.Add(data.BlockRepo, data.BlockRepo.CreateRuntime());
            }

            builders = new List<LevelBuilderEditor.SO>();
            foreach (var builder in validBuilders)
                builders.Add(new LevelBuilderEditor.SO(builder));

            builderGroups = LevelBuilderUtlity.GroupBuilders(builders);

            builderToggles = new bool[builderGroups.Count][];
            for (int i = 0; i < builderGroups.Count; i++)
            {
                builderToggles[i] = new bool[builderGroups[i].Count];
                builderToggles[i].Fill(() => true);
            }

            if (builderGroups.Count > 0)
            {
                SetSelectedGroup(0);
            }
        }

        private void UndoRedoPerformed()
        {
            ReDraw();
        }

        private void DuringSceneGUI(SceneView view)
        {
            if(SettingsEditor.Settings.GroupsToggle)
            foreach (var drawer in inputWaveDrawers)
                drawer.Draw();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Undo.undoRedoPerformed -= UndoRedoPerformed;

            DestroyImmediate(SettingsEditor);

            ClearDrawers();

            foreach(var gorup in builderGroups)
                foreach(var builder in gorup)
                    builder.Dispose();

            foreach (var repo in repos)
                repo.Value.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Space(5);

            SettingsEditor.Draw();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Builders Groups");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < builderGroups.Count; i++)
            {
                var builderGroup = builderGroups[i];

                GUILayout.BeginHorizontal();

                bool foldout = EditorGUILayout.BeginFoldoutHeaderGroup(selectedGroup == i,
                    $"{builderGroup.First().target.name} and {builderGroup.Count - 1} others");

                if(foldout)
                    SetSelectedGroup(i);

                GUILayout.EndHorizontal();

                if (foldout)
                {
                    int j = 0;
                    foreach(var builder in builderGroup)
                    {
                        GUILayout.BeginHorizontal();

                        builderToggles[i][j] = GUILayout.Toggle(builderToggles[i][j], "", GUILayout.Width(50));
                        EditorGUILayout.LabelField($"{builder.target.name}");

                        GUILayout.EndHorizontal();

                        j++;
                    }
                }

                EditorGUILayout.EndFoldoutHeaderGroup();

            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Rebuild"))
                Rebuild();

            if(GUILayout.Button("Clear"))
            {
                var group = builderGroups[selectedGroup];
                foreach(var builder in group)
                    LevelBuilderUtlity.ClearBuild(builder);
                ReDraw();
            }

            EditorGUILayout.EndHorizontal();

            if(GUILayout.Button("Save"))
            {
                var group = builderGroups[selectedGroup];
                foreach (var builder in group)
                    builder.ApplyLevelData();
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export Meshs"))
                ExportMeshes();

            if (GUILayout.Button("Export Objects"))
                ExportObjects();

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(result))
                EditorGUILayout.HelpBox(result,MessageType.Info);
        }

        private void Rebuild()
        {
            var builders = new List<ILevelBuilderData>();
            var toggles = builderToggles[selectedGroup];
            int i = 0;
            foreach (var builder in builderGroups[selectedGroup])
            {
                if (toggles[i++])
                    builders.Add(builder);
            }

            bool result = LevelBuilderUtlity.RebuildLevelGroup(
                builderGroups[selectedGroup], builders,
                (builder) => repos[builder.data.BlockRepo]);
            ReDraw();

            if (result)
                this.result = "build succeeded";
            else
                this.result = "build failed";
        }

        private void ExportMeshes()
        {
            var builders = builderGroups[selectedGroup];

            var path = EditorUtility.OpenFolderPanel("Mesh Export", Application.dataPath, "");
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var builder in builders)
            {
                var filePath = Path.Combine(path, builder.target.name + ".fbx");
                if (File.Exists(filePath))
                    File.Delete(filePath);

                AutoLevelEditorUtility.ExportMesh(builder.target, filePath);
            }
        }

        private void ExportObjects()
        {
            var builders = builderGroups[selectedGroup];

            var path = EditorUtility.OpenFolderPanel("Objects Export", Application.dataPath, "");
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var builder in builders)
            {
                var filePath = Path.Combine(path, builder.target.name + ".prefab");
                if (File.Exists(filePath))
                    File.Delete(filePath);

                AutoLevelEditorUtility.ExportObjects(builder.target, filePath);
            }
        }

        private void SetSelectedGroup(int index)
        {
            if(index != selectedGroup)
            {
                selectedGroup = index;
                ReDraw();
            }
        }

        private void ReDraw()
        {
            ClearDrawers();

            var buildersGroup = builderGroups[selectedGroup];

            foreach (var builder in buildersGroup)
            {
                levelDataDrawers.Add(new LevelDataDrawer(repos[builder.BlockRepo], builder));
                inputWaveDrawers.Add(new InputWaveDrawer(repos[builder.BlockRepo], builder));
            }
        }

        private void ClearDrawers()
        {
            for (int i = 0; i < levelDataDrawers.Count; i++)
            {
                levelDataDrawers[i].Dispose();
                inputWaveDrawers[i].Dispose();
            }
            levelDataDrawers.Clear();
            inputWaveDrawers.Clear();
        }
    }
}