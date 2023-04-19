using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.IO;
using AlaslTools;

namespace AutoLevel
{
    public class EditorLevelGroupManager : BaseLevelGroupManager<LevelBuilderEditor.SO>
    {
        public EditorLevelGroupManager(bool useSolverMT) : base(useSolverMT) { }

        protected override LevelBuilderEditor.SO GetBuilderData(LevelBuilder builder)
        => new LevelBuilderEditor.SO(builder);
    }

    public class LevelBuilderWindow : EditorWindow
    {
        private LevelEditorSettingsEditor SettingsEditor;

        private EditorLevelGroupManager groupManager;
        private List<LevelDataDrawer>   levelDataDrawers = new List<LevelDataDrawer>();
        private List<InputWaveDrawer>   inputWaveDrawers = new List<InputWaveDrawer>();

        private Vector2     scrollPos;
        private int         selectedGroup = -1;
        private bool        useSolverMT;
        private int         layer;
        private bool        rebuildBottomLayers;

        private string Message = "";

        private bool[][] builderToggles;

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGUI;
            Undo.undoRedoPerformed += UndoRedoPerformed;

            var settings = LevelEditorSettings.GetSettings();
            SettingsEditor = (LevelEditorSettingsEditor)Editor.CreateEditor(settings, typeof(LevelEditorSettingsEditor));

            groupManager = new EditorLevelGroupManager(false);

            builderToggles = new bool[groupManager.GroupCount][];
            for (int i = 0; i < groupManager.GroupCount; i++)
            {
                builderToggles[i] = new bool[groupManager.GetBuilderGroup(i).Count()];
                builderToggles[i].Fill(() => true);
            }

            if (groupManager.GroupCount > 0)
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
            SceneView.duringSceneGui    -= DuringSceneGUI;
            Undo.undoRedoPerformed      -= UndoRedoPerformed;

            DestroyImmediate(SettingsEditor);

            ClearDrawers();

            groupManager.Dispose();
            for (int i = 0; i < groupManager.GroupCount; i++)
                foreach (var builder in groupManager.GetBuilderGroup(i))
                    builder.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Space(5);

            SettingsEditor.Draw();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Builders Groups");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < groupManager.GroupCount; i++)
            {
                var builderGroup = groupManager.GetBuilderGroup(i);

                GUILayout.BeginHorizontal();

                bool foldout = EditorGUILayout.BeginFoldoutHeaderGroup(selectedGroup == i,
                    $"{builderGroup.First().Builder.name} and {builderGroup.Last().Builder.name} others");

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

            if(GetRepoOfFirstBuilder(selectedGroup).LayersCount > 1)
                LayerGUI();

            if (GUILayout.Toggle(useSolverMT, "Use Multi Thread", GUI.skin.button) != useSolverMT)
            {
                useSolverMT = !useSolverMT;
                groupManager.Dispose();
                groupManager = new EditorLevelGroupManager(useSolverMT);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Rebuild"))
                Rebuild();

            if(GUILayout.Button("Clear"))
            {
                if(rebuildBottomLayers)
                    for (int i = 0; i < layer; i++)
                        groupManager.ClearGroupLayer(selectedGroup, i);
                else
                    groupManager.ClearGroupLayer(selectedGroup, layer);

                ReDraw();
            }

            EditorGUILayout.EndHorizontal();

            if(GUILayout.Button("Save"))
            {
                var group = groupManager.GetBuilderGroup(selectedGroup);
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

            if (!string.IsNullOrEmpty(Message))
                EditorGUILayout.HelpBox(Message,MessageType.Info);
        }

        private BlocksRepo.Runtime GetRepoOfFirstBuilder(int group)
        {
            var builder = groupManager.GetBuilderGroup(group).First();
            return groupManager.GetRepo(builder);
        }

        private void LayerGUI()
        {
            int layer = this.layer;

            LevelBuilderEditor.LayerControlGUI(ref layer, ref rebuildBottomLayers);

            if (layer > this.layer)
            {
                if (layer < GetRepoOfFirstBuilder(selectedGroup).LayersCount)
                {
                    if (groupManager.GetBuilderGroup(selectedGroup).
                        Any((builder)=> !builder.levelData.GetLayer(this.layer).Valid))
                        Message = "you need to build the layer fully before editing upper layer!";
                    else
                        this.layer++;
                }
            }
            else if (layer < this.layer)
            {
                if (layer > 0)
                {
                    groupManager.ClearGroupLayer(this.layer, selectedGroup);
                    ReDraw();
                    this.layer--;
                }
            }
        }

        private void Rebuild()
        {
            bool result = true;

            if(rebuildBottomLayers)
            {
                for (int i = 0; i <= layer; i++)
                    if (!groupManager.Rebuild(selectedGroup, i, builderToggles[selectedGroup]))
                    {
                        result = false;
                        break;
                    }
            }
            else
                result = groupManager.Rebuild(selectedGroup, layer, builderToggles[selectedGroup]);

            ReDraw();

            if (result)
                Message = "build succeeded";
            else
                Message = "build failed";
        }

        private void ExportMeshes()
        {
            var builders = groupManager.GetBuilderGroup(selectedGroup);

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
            var builders = groupManager.GetBuilderGroup(selectedGroup);

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
                layer = Mathf.Min(layer,GetRepoOfFirstBuilder(selectedGroup).LayersCount - 1);
                ReDraw();
            }
        }

        private void ReDraw()
        {
            ClearDrawers();

            var buildersGroup = groupManager.GetBuilderGroup(selectedGroup);

            foreach (var builder in buildersGroup)
            {
                levelDataDrawers.Add(new LevelDataDrawer(builder.levelData, groupManager.GetRepo(builder)));
                inputWaveDrawers.Add(new InputWaveDrawer(groupManager.GetRepo(builder), builder));
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