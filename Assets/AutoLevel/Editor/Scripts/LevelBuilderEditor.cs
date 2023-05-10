using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using AlaslTools;

namespace AutoLevel
{
    using static HandleEx;

    [CustomEditor(typeof(LevelBuilder))]
    public partial class LevelBuilderEditor : Editor
    {

        private LevelEditorSettingsEditor       SettingsEditor;
        private LevelEditorSettings.Settings    settings => SettingsEditor.Settings;


        private SO                      builder;
        private BlocksRepo.Runtime[]    boundaryRepos;
        private BlocksRepo.Runtime      repo;
        private int                     currentLayer;

        private Tool                    current;
        private BoxBoundsHandle         handle = new BoxBoundsHandle();
        private HandleResources         handleRes;

        private HashedFlagList[]        groupsLists;

        private LevelDataDrawer[]       boundariesLevelDrawer;
        private LevelDataDrawer         levelDataDrawer;
        private InputWaveDrawer         inputWaveDrawer;

        private string Message;

        private bool connecting;
        private int connectingSide;

        private Array3D<InputWaveCell> inputWave => builder.inputWave;
        private BoundsInt selection { get => builder.selection; set => builder.SetSelection(value); }
        private BoundsInt levelBounds
        {
            get => builder.levelData.bounds;
            set
            {
                if (value != levelBounds)
                {
                    if (value.size != levelBounds.size)
                    {
                        var newBounds = value;
                        newBounds.size = Vector3Int.Max(Vector3Int.one, newBounds.size);

                        inputWave.Resize(newBounds.size);
                        foreach (var index in SpatialUtil.Enumerate(newBounds.size))
                            if (inputWave[index].Invalid()) inputWave[index] = InputWaveCell.AllGroups;
                        ApplyInputWave(new BoundsInt(Vector3Int.zero, newBounds.size));

                        builder.levelData.bounds = newBounds;
                        builder.ApplyAllLevelLayers();

                        levelDataDrawer?.Dispose();
                        levelDataDrawer = new LevelDataDrawer(builder.levelData, repo);
                    }
                    if (value.position != levelBounds.position)
                    {
                        builder.levelData.position = value.position;
                        builder.ApplyLevelPosition();
                    }
                    //to clamp selection to level bounds
                    selection = selection;

                    levelDataDrawer.RebuildAll();
                }
            }
        }

        #region Callback

        private void OnEnable()
        {
            builder = new SO(target);

            handleRes       = new HandleResources();
            var settings    = LevelEditorSettings.GetSettings();
            SettingsEditor  = (LevelEditorSettingsEditor)CreateEditor(settings, typeof(LevelEditorSettingsEditor));

            boundaryRepos           = new BlocksRepo.Runtime[6];
            boundariesLevelDrawer   = new LevelDataDrawer[6];

            Initialize();
            ReCreateBoundiesLevel();

            Undo.undoRedoPerformed += UndoCallback;
        }

        private void OnDisable()
        {
            repo?.Dispose();
            builder.Dispose();

            handleRes.Dispose();
            DestroyImmediate(SettingsEditor);

            levelDataDrawer?.Dispose();
            inputWaveDrawer?.Dispose();
            CleanBoundiesLevel();

            Tools.current = current;

            Undo.undoRedoPerformed -= UndoCallback;
        }

        private void UndoCallback()
        {
            levelDataDrawer?.RebuildAll();
            ReCreateBoundiesLevel();
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Active)]
        static void DrawGizmos(LevelBuilder builder, GizmoType gizmoType)
        {
            var data = builder.data;
            var position = data.LevelData.position;
            var size = (Vector3)data.LevelData.size;
            DrawBounds(new Bounds(position + size * 0.5f, size), Color.yellow);
        }

        public override void OnInspectorGUI()
        {
            // settings //

            SettingsEditor.Draw();

            // repo //

            EditorGUILayout.Space();

            var repo = (BlocksRepo)EditorGUILayout.ObjectField(builder.blockRepo, typeof(BlocksRepo), true);

            SetRepo(repo);

            if (repo == null)
            {
                EditorGUILayout.HelpBox("assign BlockRepo to start editing", MessageType.Error);
                return;
            }

            // bounds //

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            var tool = GUILayout.Toolbar(settings.Tool, new string[] { "Level", "Selection" , "Connecting" });

            if(tool != settings.Tool)
            {
                settings.Tool = tool;
                SettingsEditor.Apply();
            }

            if (settings.Tool == 0)
                levelBounds = DrawBoundsGUI(levelBounds);
            else if (settings.Tool == 1)
                selection = DrawBoundsGUI(selection);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            EditorGUILayout.Space();

            // layers

            BoundiesSettingsGUI();

            EditorGUILayout.Space();

            GroupSettingsGUI();

            EditorGUILayout.Space();

            if(this.repo.LayersCount > 1)
                LayerControlGUI();

            // execution //

            if (GUILayout.Button("Clear"))
            {
                if(builder.rebuild_bottom_layers_editor_only)
                {
                    builder.levelData.ClearAllLayers();
                    builder.ApplyAllLevelLayers();
                    levelDataDrawer.Clear();
                }
                else
                {
                    builder.levelData.GetLayer(currentLayer).Clear();
                    builder.ApplyLevelLayer(currentLayer);
                    levelDataDrawer.RebuildAll();
                }
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(builder.useMutliThreadedSolver, "MT", GUI.skin.button,GUILayout.Width(35)) !=
                builder.useMutliThreadedSolver)
            {
                builder.useMutliThreadedSolver = !builder.useMutliThreadedSolver;
                builder.ApplyField(nameof(SO.useMutliThreadedSolver));
            }

            if (GUILayout.Button("Rebuild All"))
                Rebuild(levelBounds);

            if (GUILayout.Button("Rebuild Selection"))
                Rebuild(selection);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Export Mesh"))
                ExportMesh();

            if(GUILayout.Button("Export Objects"))
                ExportGameObjects();

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(Message))
                EditorGUILayout.HelpBox(Message, MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (repo == null)
                return;

            if (settings.GroupsToggle)
                inputWaveDrawer.Draw();

            GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(GridMaterial).
                Scale(selection.size).
                Move(selection.center).Draw();

            HandleTools();

            if (settings.Tool == 1)
                DoContextMenu();
            else if (settings.Tool == 2)
                DoConnectingControls();
        }

        #endregion

        private void SetRepo(BlocksRepo repo)
        {
            if (repo != builder.blockRepo)
            {
                if (this.repo != null)
                    this.repo.Dispose();

                builder.blockRepo = repo;
                builder.ApplyField(nameof(SO.blockRepo));

                Initialize();
            }
        }
        private void Initialize()
        {
            if (builder.blockRepo == null)
                return;

            BlocksRepoSO.IntegrityCheckAndAllChildren(builder.BlockRepo);
            repo = builder.blockRepo.CreateRuntime();

            IntegrityCheck(builder.target, this.repo);
            builder.Update();

            currentLayer = builder.levelData.LayersCount - 1;
            for (int i = 1; i < builder.levelData.LayersCount; i++)
                if (!builder.levelData.GetLayer(i).Valid)
                { currentLayer = i - 1; break; }

            if (inputWaveDrawer != null)
                inputWaveDrawer.Dispose();
            inputWaveDrawer = new InputWaveDrawer(repo,builder);

            if (levelDataDrawer != null)
                levelDataDrawer.Dispose();
            levelDataDrawer = new LevelDataDrawer(builder.levelData, repo);

            groupsLists = new HashedFlagList[6];
            for (int d = 0; d < 6; d++)
            {
                groupsLists[d] = new HashedFlagList(
                builder.blockRepo.GetAllGroupsNames(),
                builder.boundarySettings.groupsBoundary[d].groups,
                null);
            }
        }
        private void IntegrityCheck(LevelBuilder levelBuilder, BlocksRepo.Runtime repo)
        {
            using (var so = new SO(levelBuilder))
            {

                if(so.levelData.LayersCount != repo.LayersCount)
                {
                    so.levelData.SetLayerCount(repo.LayersCount);

                    so.ApplyAllLevelLayers();
                }

                for (int i = 0; i < so.levelData.LayersCount; i++)
                {
                    var layer = so.levelData.GetLayer(i);
                    bool applyLayer = false;

                    foreach (var index in SpatialUtil.Enumerate(layer.Blocks.Size))
                    {
                        var b = layer.Blocks[index.z, index.y, index.x];
                        if (b != 0 && !repo.ContainsBlock(b))
                        {
                            layer.Blocks[index.z, index.y, index.x] = 0;
                            layer.Valid = false;
                            applyLayer = true;
                        }
                    }

                    if (applyLayer)
                        so.ApplyLevelLayer(i);
                }

                bool groupChanged = false;

                if (repo.WeightGroupsCount != so.groupsWeights.Count)
                    groupChanged = true;
                else
                {
                    for (int i = 0; i < repo.WeightGroupsCount; i++)
                    {
                        var gh = repo.GetWeightGroupHash(i);
                        if (gh != so.groupsWeights[i].hash)
                        {
                            groupChanged = true;
                            break;
                        }
                    }
                }

                if (groupChanged)
                {
                    if (so.groupsWeights.Count != 0)
                        Debug.Log($"adjusting to change in the Repo groups {levelBuilder.gameObject.name}");

                    var map = new List<int>();
                    var newSettings = new List<LevelBuilder.GroupSettings>();
                    for (int i = 0; i < repo.WeightGroupsCount; i++)
                    {
                        var hash = repo.GetWeightGroupHash(i);
                        var oldIndex = so.groupsWeights.FindIndex((a) => a.hash == hash);
                        map.Add(oldIndex);
                        newSettings.Add(oldIndex != -1 ? so.groupsWeights[oldIndex] :
                            new LevelBuilder.GroupSettings() { hash = hash, Weight = 1 });
                    }
                    so.groupsWeights = newSettings;

                    foreach (var index in SpatialUtil.Enumerate(so.inputWave.Size))
                    {
                        var iw = so.inputWave[index];
                        var niw = new InputWaveCell();
                        bool all = true;
                        for (int i = 0; i < map.Count; i++)
                        {
                            var v = map[i];
                            if (v > -1)
                            {
                                niw[i] = iw[v];
                                all &= iw[v];
                            }
                            else
                                niw[i] = true;
                        }
                        so.inputWave[index] = (all || niw.Invalid()) ? InputWaveCell.AllGroups : niw;
                    }

                    so.ApplyField(nameof(SO.groupsWeights));
                    so.ApplyInputWave();
                }

            }
        }

        public static void LayerControlGUI(ref int layer,ref bool rebuildBottomLayers)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box,GUILayout.Height(45));

            EditorGUILayout.BeginVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();

            rebuildBottomLayers = GUILayout.Toggle(rebuildBottomLayers, new GUIContent("R", "Rebuild all lower layer as well"), GUI.skin.button, GUILayout.Width(25));

            var preWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 50;
            GUI.enabled = false;
            EditorGUILayout.IntField("Layer", layer);
            GUI.enabled = true;
            EditorGUIUtility.labelWidth = preWidth;

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();

            if (GUILayout.Button("UP", GUILayout.Height(18)))
                layer++;

            if (GUILayout.Button("Down", GUILayout.Height(18)))
                layer--;

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void LayerControlGUI()
        {
            int layer = currentLayer;
            bool rebuildBottomLayers = builder.rebuild_bottom_layers_editor_only;

            LayerControlGUI(ref layer, ref rebuildBottomLayers);

            if(layer > currentLayer)
            {
                if (layer < repo.LayersCount)
                {
                    if (!builder.levelData.GetLayer(currentLayer).Valid)
                        Message = "you need to build the layer fully before editing upper layer!";
                    else
                        currentLayer++;
                }
            }
            else if ( layer < currentLayer)
            {
                if (layer > 0)
                {
                    builder.levelData.GetLayer(currentLayer).Clear();
                    builder.ApplyLevelLayer(currentLayer);
                    levelDataDrawer.RebuildAll();
                    currentLayer--;
                }
            }

            if(rebuildBottomLayers != builder.rebuild_bottom_layers_editor_only)
            {
                builder.rebuild_bottom_layers_editor_only = rebuildBottomLayers;
                builder.ApplyField(nameof(SO.rebuild_bottom_layers_editor_only));
            }
        }

        private void ReCreateBoundiesLevel()
        {
            CleanBoundiesLevel();

            var boundarySettings = builder.boundarySettings;

            for (int d = 0; d < 6; d++)
            {
                var boundaryLevel = boundarySettings.levelBoundary[d];

                if (boundaryLevel == null)
                    continue;

                var data = boundaryLevel.data;

                if (data.BlockRepo == null)
                    continue;

                BlocksRepoSO.IntegrityCheckAndAllChildren(data.BlockRepo);
                boundaryRepos[d] = data.BlockRepo.CreateRuntime();

                IntegrityCheck(boundaryLevel, boundaryRepos[d]);

                boundariesLevelDrawer[d] = new LevelDataDrawer(boundaryLevel.data.LevelData, boundaryRepos[d]);
            }
        }
        private void CleanBoundiesLevel()
        {
            for (int d = 0; d < 6; d++)
            {
                boundaryRepos[d]?.Dispose();
                boundaryRepos[d] = null;
                boundariesLevelDrawer[d]?.Dispose();
                boundariesLevelDrawer[d] = null;
            }
        }

        private void Rebuild(BoundsInt bounds)
        {
            var sb = new System.Text.StringBuilder();
            bounds.position -= levelBounds.position;

            if (!builder.rebuild_bottom_layers_editor_only)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var count = Rebuild(bounds, currentLayer);
                if (count > 0)
                {
                    sb.AppendLine($"build succeeded ,completion time {watch.ElapsedTicks / 10000f} ms, number of iterations {count}, number of blocks {repo.BlocksCount}");
                    builder.ApplyLevelLayer(bounds,currentLayer);
                    levelDataDrawer.RebuildAll();
                }
                else
                    sb.Append($"build failed!");
            }
            else
            {
                int count = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool passed = true;
                for (int i = 0; i < currentLayer + 1; i++)
                {
                    var c = Rebuild(bounds, i);
                    count += c;
                    if(c == 0)
                    {
                        passed = false;
                        sb.Append($"build failed at layer {i}");
                        break;
                    }
                }
                if(passed)
                {
                    sb.AppendLine($"build succeeded ,completion time {watch.ElapsedTicks / 10000f} ms, number of iterations {count}, number of blocks {repo.BlocksCount}");
                    for (int i = 0; i < currentLayer + 1; i++)
                        builder.ApplyLevelLayer(bounds, i);
                    levelDataDrawer.RebuildAll();
                }
            }

            Message = sb.ToString();
        }

        private int Rebuild(BoundsInt bounds, int layer)
        {
            BaseLevelSolver solver;

            if (builder.useMutliThreadedSolver)
                solver = new LevelSolverMT(bounds.size);
            else
                solver = new LevelSolver(bounds.size);

            LevelBuilderUtlity.UpdateLevelSolver(builder, repo, solver);

            return solver.Solve(bounds, layer, settings.MaxIterations);
        }

        private void ExportMesh()
        {
            var path = EditorUtility.SaveFilePanelInProject("Mesh Export", "level ", "fbx", "Mesh Export");
            if (string.IsNullOrEmpty(path))
                return;
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            AutoLevelEditorUtility.ExportMesh(builder, repo, path,settings.ExportSize);
        }
        private void ExportGameObjects()
        {
            var path = EditorUtility.SaveFilePanelInProject("Objects Export", "level objects ", "prefab", "Objects Export");
            if (string.IsNullOrEmpty(path))
                return;
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            AutoLevelEditorUtility.ExportObjects(builder, repo, path);
        }


        BoundsInt DrawBoundsGUI(BoundsInt bounds)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            bounds.position = EditorGUILayout.Vector3IntField("Position", bounds.position);
            bounds.size = EditorGUILayout.Vector3IntField("Size", bounds.size);
            GUILayout.EndVertical();
            return bounds;
        }
        private void BoundiesSettingsGUI()
        {
            var settings = builder.boundarySettings;

            EditorGUI.BeginChangeCheck();
            var expand = builder.GetFieldExpand(nameof(SO.boundarySettings));
            expand = EditorGUILayout.BeginFoldoutHeaderGroup(expand, "Boundary Settings");

            if (EditorGUI.EndChangeCheck())
                builder.SetFieldExpand(nameof(SO.boundarySettings), expand);

            var style = GUI.skin.label;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            int[] directions = new int[] { 3, 0, 4, 1, 5, 2 };

            EditorGUI.BeginChangeCheck();

            if (expand)
            {
                for (int i = 0; i < 6; i++)
                {
                    var d = directions[i];

                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.LabelField(((Direction)d).ToString(), style);

                    EditorGUILayout.Space();

                    settings.levelBoundary[d] = (LevelBuilder)EditorGUILayout.ObjectField("Level", settings.levelBoundary[d], typeof(LevelBuilder),true);
                    groupsLists[d].Draw(settings.groupsBoundary[d].groups);

                    EditorGUILayout.EndVertical();
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                builder.ApplyField(nameof(SO.boundarySettings));
                ReCreateBoundiesLevel();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        private void GroupSettingsGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginChangeCheck();
            var expand = builder.GetFieldExpand(nameof(SO.groupsWeights));
            expand = EditorGUILayout.BeginFoldoutHeaderGroup(expand, "Group Settings");

            if(EditorGUI.EndChangeCheck())
                builder.SetFieldExpand(nameof(SO.groupsWeights), expand);

            if (expand)
            {
                for (int i = 0; i < repo.WeightGroupsCount; i++)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.LabelField(repo.GetWeightGroupName(i));

                    var setting = builder.groupsWeights[i];

                    EditorGUILayout.BeginHorizontal();

                    var pwidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 120;
                    setting.overridWeight = EditorGUILayout.Toggle("Override Weight", setting.overridWeight);

                    if (setting.overridWeight)
                        setting.Weight = EditorGUILayout.FloatField("Weight", setting.Weight);

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();

                    EditorGUIUtility.labelWidth = pwidth;
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
                builder.ApplyField(nameof(SO.groupsWeights));
        }

        private void HandleTools()
        {
            if (Tools.current != Tool.None)
            {
                current = Tools.current;
                Tools.current = Tool.None;
            }

            switch (current)
            {
                case Tool.Move:
                    if (settings.Tool == 0)
                        levelBounds = DoMoveBoundsHandle(levelBounds);
                    else
                        selection = DoMoveBoundsHandle(selection);
                    break;
                case Tool.Scale:
                    if (settings.Tool == 0)
                        levelBounds = DoScaleBoundsHandle(levelBounds);
                    else
                        selection = DoScaleBoundsHandle(selection);
                    break;
                case Tool.Rect:
                    if (settings.Tool == 0)
                        levelBounds = DoEditBoundsHandle(levelBounds);
                    else
                        selection = DoEditBoundsHandle(selection);
                    break;
            }
        }
        BoundsInt DoMoveBoundsHandle(BoundsInt bounds)
        {
            bounds.position = Vector3Int.RoundToInt(
                Handles.PositionHandle(bounds.position, Quaternion.identity));

            DrawBounds(bounds, Color.cyan);

            return bounds;
        }
        BoundsInt DoScaleBoundsHandle(BoundsInt bounds)
        {
            bounds.size = Vector3Int.RoundToInt(
                Handles.ScaleHandle(bounds.size,
                bounds.position, Quaternion.identity, 2f));

            DrawBounds(bounds, Color.cyan);

            return bounds;
        }
        BoundsInt DoEditBoundsHandle(BoundsInt bounds)
        {
            handle.axes = PrimitiveBoundsHandle.Axes.All;
            handle.wireframeColor = Color.cyan;

            handle.center = bounds.center;
            handle.size = bounds.size;

            handle.midpointHandleDrawFunction = (controlID, position, rotation, size, eventType) =>
            {
                var forward = rotation * Vector3.back;
                var cameraForward = SceneView.lastActiveSceneView.camera.transform.forward;

                //if the camera is perpendicular to one of the three plans then stop drawing on that axis
                var draw = true; var passive = false;
                var v = Vector3.Dot(forward, cameraForward);
                if (Math.Abs(v) > 0.95f)
                    draw = false;
                else if (v < 0)
                    passive = true;

                Color color = Color.yellow;
                color *= passive ? 0.4f : 1f;

                if (draw)
                {
                    var cmd = GetDrawCmd().
                    SetPrimitiveMesh(PrimitiveType.Cube).
                    SetMaterial(MaterialType.UI).
                    Scale(0.3f).Rotate(Quaternion.Inverse(rotation)).Move(position);

                    if (eventType == EventType.Repaint)
                        if (GUIUtility.hotControl == controlID)
                            cmd.SetColor(Color.green).Draw();
                        else
                            cmd.SetColor(color).Draw();
                    else if (eventType == EventType.Layout)
                        HandleUtility.AddControl(controlID, default(CubeD).Distance(cmd, controlID));
                }
            };
            handle.DrawHandle();

            var min = handle.center - handle.size / 2;
            var max = handle.center + handle.size / 2;

            bounds.min = Vector3Int.RoundToInt(min);
            bounds.max = Vector3Int.RoundToInt(max);

            return bounds;
        }

        private void DoContextMenu()
        {
            Handles.BeginGUI();

            var p = BoundsUtility.ClosestCornerToScreenPoint(selection, new Vector2(SceneView.lastActiveSceneView.position.width, 0));
            Rect rect = new Rect(HandleUtility.WorldToGUIPoint(p) + Vector2.down * 25f, new Vector2(150, 25));
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.BeginHorizontal();

            var bounds = selection;
            bounds.position -= levelBounds.position;

            if (GUILayout.Button("Add"))
            {
                var menu = new GenericMenu();
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    var index = i;
                    menu.AddItem(new GUIContent(repo.GetGroupName(i)), false, () =>
                    {
                        Add(inputWave, repo, bounds, index);
                        ApplyInputWave(bounds);
                    });
                }
                menu.ShowAsContext();
            }

            if (GUILayout.Button("Remove"))
            {
                var menu = new GenericMenu();
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    var index = i;
                    menu.AddItem(new GUIContent(repo.GetGroupName(i)), false, () =>
                    {
                        Remove(inputWave, repo, bounds, index);
                        ApplyInputWave(bounds);
                    });
                }
                menu.ShowAsContext();
            }

            if (GUILayout.Button("Set"))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("All"), false, () =>
                {
                    Set(inputWave, repo, bounds, -1);
                    ApplyInputWave(bounds);
                });
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    var index = i;
                    menu.AddItem(new GUIContent(repo.GetGroupName(i)), false, () =>
                    {
                        Set(inputWave, repo, bounds, index);
                        ApplyInputWave(bounds);
                    });
                }
                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            Handles.EndGUI();
        }
        public static void Remove(
        Array3D<InputWaveCell> inputWave, BlocksRepo.Runtime repo,
        BoundsInt bounds, int groupIndex)
        {
            foreach (var index in SpatialUtil.Enumerate(bounds))
            {
                var iwave = inputWave[index.z, index.y, index.x];
                if (iwave.GroupsCount(repo.GroupsCount) == 1)
                    continue;
                iwave[groupIndex] = false;
                inputWave[index.z, index.y, index.x] = iwave;
            }
        }
        public static void Add(
            Array3D<InputWaveCell> inputWave, BlocksRepo.Runtime repo,
            BoundsInt bounds, int groupIndex)
        {
            foreach (var index in SpatialUtil.Enumerate(bounds))
            {
                var iWave = inputWave[index.z, index.y, index.x];
                iWave[groupIndex] = true;
                inputWave[index.z, index.y, index.x] = iWave;
            }
        }
        public static void Set(
            Array3D<InputWaveCell> inputWave, BlocksRepo.Runtime repo,
            BoundsInt bounds, int groupIndex)
        {

            if (groupIndex > -1)
            {
                foreach (var index in SpatialUtil.Enumerate(bounds))
                {
                    var iWave = new InputWaveCell();
                    iWave[groupIndex] = true;
                    inputWave[index.z, index.y, index.x] = iWave;
                }

            }
            else
                foreach (var index in SpatialUtil.Enumerate(bounds))
                    inputWave[index.z, index.y, index.x] = InputWaveCell.AllGroups;
        }

        private void DoConnectingControls()
        {
            var camera = SceneView.lastActiveSceneView.camera;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            var cmd = GetDrawCmd().
                        SetPrimitiveMesh(PrimitiveType.Quad).
                        SetMaterial(MaterialType.UI).
                        SetColor(NiceColors.PictonBlue).
                        SetTexture(handleRes.boundary_connect_icon).
                        Scale(2f);

            if (!connecting)
            {
                for (int d = 0; d < 6; d++)
                {
                    Vector3 pos = GetSideCenter(levelBounds, d);
                    var normal = Directions.directions[d];

                    if (!AutoLevelEditorUtility.SideCullTest(camera, pos, d))
                        continue;

                    if (builder.boundarySettings.levelBoundary[d] != null)
                    {
                        var btnCmd = cmd;
                        btnCmd.SetTexture(handleRes.boundary_remove_icon);
                        btnCmd.SetColor(NiceColors.ImperialRed);
                        btnCmd.LookAt(-normal);
                        btnCmd.Move(pos);

                        Button.SetAll(btnCmd);
                        Button.hover.SetColor(NiceColors.ImperialRed * 0.7f);
                        Button.active.SetColor(NiceColors.ImperialRed * 0.9f);

                        if (Button.Draw<QuadD>())
                        {
                            var builder = this.builder.boundarySettings.levelBoundary[d];

                            using(var so = new SO(builder))
                            {
                                var delta = Vector3Int.zero;
                                delta[d % 3] = so.levelData.bounds.size[d % 3] * (d < 3 ? -1 : 1);
                                so.levelData.position += delta;

                                so.ApplyLevelPosition();
                            }

                            Connect(null, d);
                        }
                    }
                    else
                    {
                        var btnCmd = cmd;
                        btnCmd.LookAt(-normal);
                        btnCmd.Move(pos);

                        Button.SetAll(btnCmd);
                        Button.hover.SetColor(NiceColors.PictonBlue * 0.7f);
                        Button.active.SetColor(NiceColors.PictonBlue * 0.9f);

                        if (Button.Draw<QuadD>())
                        {
                            connecting = true;
                            connectingSide = d;
                        }
                    }
                }
            }
            else
            {
                if(Event.current.type == EventType.MouseDown && Event.current.button == 1)
                {
                    connecting = false;
                }

                var pos = GetSideCenter(levelBounds, connectingSide);

                SceneView.RepaintAll();
                Handles.DrawDottedLine(
                    pos,
                    HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f)
                    , 4f);

                foreach (var builder in FindObjectsOfType<LevelBuilder>().
                    Where((builder) => builder.gameObject.activeInHierarchy).
                    Where((builder) => builder != this.builder.target))
                {
                    var boundsInt = builder.data.LevelData.bounds;
                    var bounds = new Bounds();
                    bounds.size = boundsInt.size;
                    bounds.center = boundsInt.position + bounds.size*0.5f;

                    if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                        continue;

                    var btnCmd = GetDrawCmd().
                        SetPrimitiveMesh(PrimitiveType.Cube).
                        Scale(bounds.size).
                        Move(bounds.center).
                        SetColor(NiceColors.Coral.SetAlpha(0.35f));

                    Button.SetAll(btnCmd);
                    Button.hover.SetColor(NiceColors.Orange.SetAlpha(0.4f));

                    if (Button.Draw<Cube3DD>())
                    {
                        Connect(builder, connectingSide);
                        connecting = false;
                    }
                }
            }
        }

        private void Connect(LevelBuilder target, int d)
        {
            builder.boundarySettings.levelBoundary[d] = target;
            builder.ApplyField(nameof(SO.boundarySettings));

            if(target != null)
            {
                using (var so = new SO(target))
                {
                    var a = levelBounds.position[d % 3] + (d > 2 ? 1 : 0) * levelBounds.size[d % 3];
                    d = Directions.opposite[d];
                    var b = so.levelData.bounds.position[d % 3] + (d > 2 ? 1 : 0) * so.levelData.bounds.size[d % 3];
                    var delta = Vector3Int.zero;
                    delta[d % 3] = a - b;

                    so.levelData.position += delta;

                    so.ApplyLevelPosition();
                }
            }

            ReCreateBoundiesLevel();
        }

        private Vector3 GetSideCenter(BoundsInt bounds, int d)
        {
            var pos = Directions.GetSideCenter(Vector3.zero, d);
            pos.Scale(bounds.size);
            pos += bounds.position;
            return pos;
        }

        private void ApplyInputWave(BoundsInt bounds)
        {
            builder.ApplyInputWave(bounds);
            inputWaveDrawer.Regenrate();
        }
    }
}