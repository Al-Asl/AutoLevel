using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Formats.Fbx.Exporter;
using Object = UnityEngine.Object;

namespace AutoLevel
{
    using static HandleEx;

    [CustomEditor(typeof(LevelBuilder))]
    public class LevelBuilderEditor : Editor
    {
        public class SO : BaseSO<LevelBuilder>
        {
            public BlocksRepo blockRepo;

            public List<LevelBuilder.GroupSettings> groupsSettings;

            public LevelBuilder.BoundarySettings boundarySettings;

            public BoundsInt selection;
            public LevelData levelData;
            public Array3D<InputWaveCell> inputWave;

            public SO(Object target) : base(target)
            {
            }

            public void SetSelection(BoundsInt bounds)
            {
                bounds.position = Vector3Int.Min(levelData.bounds.max - Vector3Int.one, bounds.position);
                bounds.size = Vector3Int.Max(Vector3Int.one, bounds.size);
                bounds.ClampToBounds(levelData.bounds);

                selection = bounds;
                ApplyField(nameof(selection));
            }
        }

        private SO builder;
        private BlocksRepo.Runtime repo;
        List<LevelBuilder.GroupSettings> groupsSettings     => builder.groupsSettings;
        private Array3D<int> levelBlocks                    => builder.levelData.Blocks;
        private Array3D<InputWaveCell> inputWave            => builder.inputWave;
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
                        inputWave.Resize(value.size);
                        foreach (var index in SpatialUtil.Enumerate(value.size))
                            if (inputWave[index].Invalid()) inputWave[index] = InputWaveCell.AllGroups;
                        ApplyInputWave();

                        builder.levelData.bounds = value;
                        builder.ApplyField(nameof(SO.levelData));
                    }
                    if (value.position != levelBounds.position)
                    {
                        builder.levelData.position = value.position;
                        builder.ApplyField(nameof(SO.levelData));
                    }
                    //to clamp selection to level bounds
                    selection = selection;
                    ReCreateLevel();
                }
            }
        }


        private Editor SettingsEditor;
        private LevelEditorSettingsEditor.SO settingsSO;
        private LevelEditorSettings.Settings settings => settingsSO.settings;

        private HandleResources handleRes;


        private Tool current;
        private int targetBounds = 1;
        private BoxBoundsHandle handle = new BoxBoundsHandle();

        private HashedFlagList[] groupsLists;
        private BlocksRepo.Runtime[] boundaryRepos;
        private Transform[] boundaryRoots;

        private Transform root;
        private Texture3D texture;

        private string Result;

        #region Callback

        private void OnEnable()
        {
            builder = new SO(target);

            handleRes = new HandleResources();
            var settings = LevelEditorSettings.GetSettings();
            SettingsEditor = CreateEditor(settings, typeof(LevelEditorSettingsEditor));
            settingsSO = new LevelEditorSettingsEditor.SO(settings);

            boundaryRepos = new BlocksRepo.Runtime[6];
            boundaryRoots = new Transform[6];

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

            if (root != null)
                DestroyImmediate(root.gameObject);
            if (texture != null)
                DestroyImmediate(texture);
            CleanBoundiesLevel();

            Tools.current = current;

            Undo.undoRedoPerformed -= UndoCallback;
        }

        private void UndoCallback()
        {
            ReCreateLevel();
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Active)]
        static void DrawGizmos(LevelBuilder builder, GizmoType gizmoType)
        {
            using (var so = new SerializedObject(builder))
            {
                var levelData = so.FindProperty(nameof(SO.levelData));
                var position = levelData.FindPropertyRelative(nameof(LevelData.position)).vector3IntValue;
                var size = (Vector3)levelData.FindPropertyRelative("blocks").FindPropertyRelative("size").vector3IntValue;
                DrawBounds(new Bounds(position + size*0.5f,size), Color.yellow);
            }
        }

        public override void OnInspectorGUI()
        {
            // settings //

            settingsSO.Update();

            EditorGUI.BeginChangeCheck();
            var settingsExpand = settingsSO.GetFieldExpand(nameof(LevelEditorSettingsEditor.SO.settings));
            settingsExpand = EditorGUILayout.BeginFoldoutHeaderGroup(settingsExpand, "Settings");

            if (EditorGUI.EndChangeCheck())
                settingsSO.SetFieldExpand(nameof(LevelEditorSettingsEditor.SO.settings), settingsExpand);

            GUILayout.BeginVertical(GUI.skin.box);
            if (settingsExpand)
                SettingsEditor.OnInspectorGUI();
            GUILayout.EndVertical();

            EditorGUILayout.EndFoldoutHeaderGroup();

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

            targetBounds = GUILayout.Toolbar(targetBounds, new string[] { "Level", "Selection" });

            if (targetBounds == 0)
                levelBounds = DrawBoundsGUI(levelBounds);
            else if (targetBounds == 1)
                selection = DrawBoundsGUI(selection);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            // boundary //

            EditorGUILayout.Space();

            BoundiesSettingsGUI();

            // groups //

            EditorGUILayout.Space();

            GroupSettingsGUI();

            EditorGUILayout.Space();

            // excution //

            if (GUILayout.Button("Clear"))
            {
                foreach (var index in SpatialUtil.Enumerate(levelBlocks.Size))
                    levelBlocks[index.z, index.y, index.x] = 0;
                builder.ApplyField(nameof(SO.levelData));
                DestroyImmediate(root.gameObject, false);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();

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

            if (!string.IsNullOrEmpty(Result))
                EditorGUILayout.HelpBox(Result, MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (repo == null)
                return;

            if (settings.GroupsToggle)
                DrawInputWave();

            GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(handleRes.GridMat).
                Scale(selection.size).
                Move(selection.center).Draw();

            HandleTools();

            if (targetBounds == 1)
                DoContextMenu();
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

                if (root != null)
                    DestroyImmediate(root.gameObject);

                Initialize();
            }
        }
        private void Initialize()
        {
            if (builder.blockRepo == null)
                return;

            repo = builder.blockRepo.CreateRuntime();

            IntegrityCheck(builder.target, this.repo);
            builder.Update();

            RegenrateInputWaveTexture();
            ReCreateLevel();

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
                bool Apply = false;
                foreach (var index in SpatialUtil.Enumerate(so.levelData.Blocks.Size))
                {
                    var b = so.levelData.Blocks[index.z, index.y, index.x];
                    if (b != 0 && !repo.ContainsBlock(b))
                    {
                        so.levelData.Blocks[index.z, index.y, index.x] = 0;
                        Apply = true;
                    }
                }
                if (Apply)
                    so.ApplyField(nameof(SO.levelData));

                bool groupChanged = false;

                if (repo.GroupsCount != so.groupsSettings.Count)
                    groupChanged = true;
                else
                {
                    for (int i = 0; i < repo.GroupsCount; i++)
                    {
                        var gh = repo.GetGroupHash(i);
                        if (gh != so.groupsSettings[i].hash)
                        {
                            groupChanged = true;
                            break;
                        }
                    }
                }

                if (groupChanged)
                {
                    if (so.groupsSettings.Count != 0)
                        Debug.Log($"adjusting to change in the Repo groups {levelBuilder.gameObject.name}");

                    var map = new List<int>();
                    var newSettings = new List<LevelBuilder.GroupSettings>();
                    for (int i = 0; i < repo.GroupsCount; i++)
                    {
                        var hash = repo.GetGroupHash(i);
                        var oldIndex = so.groupsSettings.FindIndex((a) => a.hash == hash);
                        map.Add(oldIndex);
                        newSettings.Add(oldIndex != -1 ? so.groupsSettings[oldIndex] :
                            new LevelBuilder.GroupSettings() { hash = hash, Weight = 1 });
                    }
                    so.groupsSettings = newSettings;

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

                    so.ApplyField(nameof(SO.groupsSettings));
                    so.ApplyField(nameof(SO.inputWave));
                }

            }
        }
        private void ReCreateBoundiesLevel()
        {
            var boundarySettings = builder.boundarySettings;

            for (int d = 0; d < 6; d++)
            {
                var levelBoundary = boundarySettings.levelBoundary[d];
                if (levelBoundary != null)
                {
                    BlocksRepo.Runtime repo = null;
                    using (var level = new SO(levelBoundary))
                    {
                        if (level.blockRepo != null)
                        {
                            repo = level.blockRepo.CreateRuntime();
                            boundaryRepos[d] = repo;
                        }
                    }

                    if (repo == null)
                        continue;

                    IntegrityCheck(levelBoundary, boundaryRepos[d]);

                    using (var level = new SO(levelBoundary))
                        ReCreateLevel(repo, level.levelData, ref boundaryRoots[d]);
                }
            }
        }
        private void CleanBoundiesLevel()
        {
            for (int d = 0; d < 6; d++)
            {
                boundaryRepos[d]?.Dispose();
                boundaryRepos[d] = null;
                if (boundaryRoots[d] != null)
                    DestroyImmediate(boundaryRoots[d].gameObject);
            }
        }



        private void Rebuild(BoundsInt bounds)
        {
            var sb = new System.Text.StringBuilder();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            bounds.position -= levelBounds.position;

            var weightOverride = new List<float>();
            foreach (var g in groupsSettings)
                weightOverride.Add(g.overridWeight ? g.Weight : -1);
            repo.OverrideGroupsWeights(weightOverride);

            var solver = new LevelSolver(bounds.size);
            solver.SetlevelData(builder.levelData);
            solver.SetRepo(repo);
            solver.SetInputWave(builder.inputWave);
            for (int d = 0; d < 6; d++)
                SetSolverBoundary(solver, d);

            int c = solver.Solve(bounds, settings.MaxIterations);

            if (c > 0)
            {
                sb.AppendLine($"build succeeded ,completion time {watch.ElapsedTicks / 10000f} ms ,iteration {c} ,number of blocks {repo.BlocksCount}");
                builder.ApplyField(nameof(SO.levelData));

                watch.Restart();

                ReCreateLevel();

                sb.AppendLine($"time to rebuild {watch.ElapsedTicks / 10000f} ms");
            }
            else
                sb.Append("build failed");

            Result = sb.ToString();
        }
        private void SetSolverBoundary(LevelSolver solver, int d)
        {
            var levelBuilder = builder.boundarySettings.levelBoundary[d];
            var groups = builder.boundarySettings.groupsBoundary[d].groups;

            GroupsBoundary groupBoundary = null;
            if(groups.Count > 0)
            {
                var groupsIndices = new List<int>();
                foreach (var g in groups)
                    groupsIndices.Add(repo.GetGroupIndex(g));
                groupBoundary = new GroupsBoundary(new InputWaveCell(groupsIndices));
            }

            if (levelBuilder != null)
                using (var so = new SO(levelBuilder))
                {
                    solver.SetBoundary(new LevelBoundary(so.levelData, so.inputWave, groupBoundary), (Direction)d);
                }
            else
                solver.SetBoundary(groupBoundary, (Direction)d);
        }
        private void ReCreateLevel() => ReCreateLevel(repo, builder.levelData, ref root);
        private void ReCreateLevel(BlocksRepo.Runtime repo, LevelData data, ref Transform root)
        {
            if (repo == null)
                return;

            if (root != null)
                DestroyImmediate(root.gameObject);

            root = new GameObject("level_root").transform;
            root.position = data.position;
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;

            foreach (var index in SpatialUtil.Enumerate(data.bounds.size))
            {
                var hash = data.Blocks[index.z, index.y, index.x];
                if (hash != 0 && repo.ContainsBlock(hash))
                {
                    var block = repo.GetBlockResourcesByHash(hash);
                    if (block.mesh != null)
                    {
                        var go = new GameObject();
                        go.hideFlags = HideFlags.HideAndDontSave;
                        go.AddComponent<MeshFilter>().sharedMesh = block.mesh;
                        go.AddComponent<MeshRenderer>().sharedMaterial = block.material;
                        go.transform.SetParent(root.transform);
                        go.transform.localPosition = index;
                    }
                }
            }
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
            var expand = builder.GetFieldExpand(nameof(SO.groupsSettings));
            expand = EditorGUILayout.BeginFoldoutHeaderGroup(expand, "Group Settings");

            if(EditorGUI.EndChangeCheck())
                builder.SetFieldExpand(nameof(SO.groupsSettings), expand);

            if (expand)
            {
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.LabelField(repo.GetGroupName(i));

                    var setting = groupsSettings[i];

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
                builder.ApplyField(nameof(SO.groupsSettings));
        }



        private void RegenrateInputWaveTexture()
        {
            if (texture != null && (
                texture.width != inputWave.Size.x ||
                texture.height != inputWave.Size.y ||
                texture.depth != inputWave.Size.z))
                DestroyImmediate(texture, false);
            if (texture == null)
            {
                texture = new Texture3D(inputWave.Size.x, inputWave.Size.y, inputWave.Size.z, TextureFormat.ARGB32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            Color emptyGroupClr = new Color(1, 1, 1, 1);
            Color solidGroupsClr = new Color(0, 1f, 0, 1f);
            Color fullGroupsClr = new Color(0, 0, 0, 0);

            foreach (var i in SpatialUtil.Enumerate(inputWave.Size))
            {
                var iWave = inputWave[i.z, i.y, i.x];

                Color col;

                if (iWave.ContainAll)
                    col = fullGroupsClr;
                else if (iWave.GroupsCount(repo.GroupsCount) == 1)
                {
                    var gi = iWave.GroupsEnum(repo.GroupsCount).First();
                    if (gi == 0)
                        col = emptyGroupClr;
                    else if (gi == 1)
                        col = solidGroupsClr;
                    else
                        col = ColorUtility.GetColor(repo.GetGroupHash(gi));
                }
                else
                {
                    var h = new XXHash();
                    foreach (var gi in iWave.GroupsEnum(repo.GroupsCount))
                        h = h.Append(repo.GetGroupHash(gi));
                    col = ColorUtility.GetColor(h);
                }

                texture.SetPixel(i.x, i.y, i.z, col);
            }
            texture.Apply();
        }
        private void DrawInputWave()
        {
            int size = Mathf.Max(inputWave.Size.x, inputWave.Size.y, inputWave.Size.z);
            Handles.matrix = Matrix4x4.TRS(builder.levelData.bounds.center, Quaternion.identity, size * Vector3.one);
            Handles.DrawTexture3DVolume(texture, 0.5f, 5, FilterMode.Point);
            Handles.matrix = Matrix4x4.identity;
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
                    if (targetBounds == 0)
                        levelBounds = DoMoveBoundsHandle(levelBounds);
                    else
                        selection = DoMoveBoundsHandle(selection);
                    break;
                case Tool.Scale:
                    if (targetBounds == 0)
                        levelBounds = DoScaleBoundsHandle(levelBounds);
                    else
                        selection = DoScaleBoundsHandle(selection);
                    break;
                case Tool.Rect:
                    if (targetBounds == 0)
                        levelBounds = DoEditBoundsHandle(levelBounds);
                    else
                        selection = DoEditBoundsHandle(selection);
                    break;
            }
        }
        BoundsInt DoMoveBoundsHandle(BoundsInt bounds)
        {
            bounds.position = MathUtility.RoundToInt(
                Handles.PositionHandle(bounds.position, Quaternion.identity));

            DrawBounds(bounds, Color.cyan);

            return bounds;
        }
        BoundsInt DoScaleBoundsHandle(BoundsInt bounds)
        {
            bounds.size = MathUtility.RoundToInt(
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
                        HandleUtility.AddControl(controlID, default(CubeD).Distance(cmd));
                }
            };
            handle.DrawHandle();

            var min = handle.center - handle.size / 2;
            var max = handle.center + handle.size / 2;

            bounds.min = MathUtility.RoundToInt(min);
            bounds.max = MathUtility.RoundToInt(max);

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
                        ApplyInputWave();
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
                        ApplyInputWave();
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
                    ApplyInputWave();
                });
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    var index = i;
                    menu.AddItem(new GUIContent(repo.GetGroupName(i)), false, () =>
                    {
                        Set(inputWave, repo, bounds, index);
                        ApplyInputWave();
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



        private void ApplyInputWave()
        {
            builder.ApplyField(nameof(SO.inputWave));
            RegenrateInputWaveTexture();
        }
    }

}