using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Formats.Fbx.Exporter;
using Object = UnityEngine.Object;
using static HandleEx;

[CustomEditor(typeof(LevelBuilder))]
public class LevelBuilderEditor : Editor
{
    class LevelBuilderSO : BaseSO<LevelBuilder>
    {
        public BlocksRepo blockRepo;

        public GroupsSettings groupsSettings;

        public BoundsInt selection;
        public LevelData levelData;
        public Array3D<InputWaveBlock> inputWave;

        public LevelBuilderSO(Object target) : base(target)
        {
        }

        public void SetLevelBounds(BoundsInt bounds)
        {
            if (bounds == levelData.bounds)
                return;

            SetInputWave(bounds);

            levelData.bounds = bounds;
            ApplyField(nameof(levelData));
            SetSelection(selection);
        }

        private void SetInputWave(BoundsInt bounds)
        {
            Array3D<InputWaveBlock> newInputWave = new Array3D<InputWaveBlock>(bounds.size);
            SpatialUtil.ItterateIntersection(levelData.bounds, bounds, (idst, isrc) =>
            {
                newInputWave[idst.z, idst.y, idst.x] = inputWave[isrc.z, isrc.y, isrc.x];
            });
            foreach(var index in SpatialUtil.Enumerate(bounds.size))
            {
                if (newInputWave[index.z, index.y, index.x] == null)
                    newInputWave[index.z, index.y, index.x] = new InputWaveBlock();
            }

            inputWave = newInputWave;
            ApplyField(nameof(inputWave));
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

    private LevelBuilderSO builder;

    private BlocksRepo repo => builder.blockRepo;

    private GroupsSettings groupsSettings => builder.groupsSettings;

    private BoundsInt selection { get => builder.selection; set => builder.SetSelection(value); }

    private BoundsInt levelBounds { get => builder.levelData.bounds; set => builder.SetLevelBounds(value); }
    private Array3D<int> levelBlocks => builder.levelData.Blocks;
    private Array3D<InputWaveBlock> inputWave => builder.inputWave;

    private bool settingsToggle;
    private Editor SettingsEditor;
    private BuilderSettings.Settings settings;

    private string Result;

    private BoxBoundsHandle handle = new BoxBoundsHandle();

    private int toolTarget = 1;
    private Tool current;

    private Transform root;
    private Texture3D texture;
    private Material gridMat;

    private void OnEnable()
    {
        gridMat = new Material(Shader.Find("Hidden/AutoLevel/Grid"));

        builder = new LevelBuilderSO(target);
        repo?.Regenerate();

        IntegrityCheck();
        CreateLevel();

        Undo.undoRedoPerformed += UndoCallback;

        SettingsEditor = CreateEditor(BuilderSettings.GetSettings(), typeof(BuilderSettingsEditor));
        settings = BuilderSettings.GetSettings().settings;
    }

    private void OnDisable()
    {
        Tools.current = current;
        builder.Dispose();
        repo?.Clear();

        Undo.undoRedoPerformed -= UndoCallback;

        if (root != null)
            DestroyImmediate(root.gameObject);

        DestroyImmediate(gridMat);
        DestroyImmediate(SettingsEditor);

        if (texture != null)
            DestroyImmediate(texture);
    }

    private void UndoCallback()
    {
        CreateLevel();
    }

    public override void OnInspectorGUI()
    {
        settingsToggle = EditorGUILayout.BeginFoldoutHeaderGroup(settingsToggle, "Settings");

        GUILayout.BeginVertical(GUI.skin.box);
        if (settingsToggle)
            SettingsEditor.OnInspectorGUI();
        GUILayout.EndVertical();

        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        builder.blockRepo = (BlocksRepo)EditorGUILayout.ObjectField(builder.blockRepo, typeof(BlocksRepo), true);

        if (EditorGUI.EndChangeCheck())
        {
            if (repo != null)
            {
                repo.Regenerate();

                IntegrityCheck();
                builder.ApplyField(nameof(LevelBuilderSO.blockRepo));
            }
        }

        if (repo == null)
        {
            EditorGUILayout.HelpBox("assign BlockRepo to start editing", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();

        toolTarget = GUILayout.Toolbar(toolTarget, new string[] { "Level", "Selection" });

        if (toolTarget == 0)
            levelBounds = DrawBoundsGUI(levelBounds);
        else if (toolTarget == 1)
            selection = DrawBoundsGUI(selection);

        EditorGUILayout.Space();

        GroupSettingsGUI();

        EditorGUILayout.Space();

        if (GUILayout.Button("Clear"))
        {
            foreach (var index in SpatialUtil.Enumerate(levelBlocks.Size))
                levelBlocks[index.z, index.y, index.x] = -1;
            builder.ApplyField(nameof(LevelBuilderSO.levelData));
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

        if(GUILayout.Button("Export"))
        {
            LevelMeshBuilder mbuilder = new LevelMeshBuilder(builder.levelData, repo, settings.ExportSize);
            mbuilder.Rebuild(new BoundsInt(Vector3Int.zero, builder.levelData.bounds.size));
            ExportMesh(mbuilder.root.gameObject);
            mbuilder.Dispose();
        }

        EditorGUILayout.Space();

        if (!string.IsNullOrEmpty(Result))
            EditorGUILayout.HelpBox(Result, MessageType.Info);
    }

    private void OnSceneGUI()
    {
        if (Tools.current != Tool.None)
        {
            current = Tools.current;
            Tools.current = Tool.None;
        }

        if (repo == null)
            return;

        if (settings.GroupsToggle)
            DrawInputWave();

        Handles.color = Color.yellow;
        DrawBounds(levelBounds);

        GetDrawCmd().
            SetPrimitiveMesh(PrimitiveType.Cube).
            SetMaterial(gridMat).
            Scale(selection.size).
            Move(selection.center).Draw();

        switch(current)
        {
            case Tool.Move:
                if (toolTarget == 0)
                    levelBounds = MoveBoundsHandle(levelBounds);
                else
                    selection = MoveBoundsHandle(selection);
                break;
            case Tool.Scale:
                if (toolTarget == 0)
                    levelBounds = ScaleBoundsHandle(levelBounds);
                else
                    selection = ScaleBoundsHandle(selection);
                break;
            case Tool.Rect:
                if (toolTarget == 0)
                    levelBounds = EditBoundsHandle(levelBounds);
                else
                    selection = EditBoundsHandle(selection);
                break;
        }

        if(toolTarget == 1)
            DrawContextMenu();
    }

    private void IntegrityCheck()
    {
        if (repo == null)
            return;

        bool Apply = false;

        foreach(var index in SpatialUtil.Enumerate(levelBounds.size))
        {
            var b = levelBlocks[index.z, index.y, index.x];
            if (b != -1 && !repo.BlocksHash.Contains(b))
            {
                levelBlocks[index.z,index.y,index.x] = -1;
                Apply = true;
            }
        }
        if (Apply)
            builder.ApplyField(nameof(LevelBuilderSO.levelData));

        Apply = false;
        foreach(var index in SpatialUtil.Enumerate(levelBounds.size))
        {
            var groups = inputWave[index.z, index.y, index.x].groups;
            for (int g = groups.Count - 1; g > -1; g--)
            {
                if (!repo.GroupsHash.Contains(groups[g]))
                {
                    groups.RemoveAt(g);
                    Apply = true;
                }
            }
        }
        if (Apply)
            builder.ApplyField(nameof(LevelBuilderSO.inputWave));

        var settings = new List<GroupSettings>();
        var oldSettings = groupsSettings.groups;
        for (int i = 0; i < repo.GroupsCount; i++)
        {
            var hash = repo.GroupsHash[i];
            var oldIndex = oldSettings.FindIndex((a) => a.hash == hash);
            settings.Add(oldIndex != -1 ? oldSettings[oldIndex] :
                new GroupSettings() { hash = hash, Weight = 1 });
        }
        groupsSettings.groups = settings;
        builder.ApplyField(nameof(LevelBuilderSO.groupsSettings));
    }

    private void Rebuild(BoundsInt bounds)
    {
        var sb = new System.Text.StringBuilder();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var weightOverride = new List<float>();
        foreach (var g in groupsSettings.groups)
            weightOverride.Add(g.overridWeight ? g.Weight : -1);
        repo.OverrideGroupWeights(weightOverride);

        foreach (var index in SpatialUtil.Enumerate(bounds))
            levelBlocks[index.z, index.y, index.x] = -1;

        var solver = new LevelSolver(bounds.size);
        solver.levelData = builder.levelData;
        solver.repo = repo;
        solver.inputWave = builder.inputWave;
        int c = solver.Solve(bounds, settings.MaxIterations);

        if (c > 0)
            sb.AppendLine($"build succeeded ,completion time {watch.ElapsedTicks / 10000f} ms ,iteration {c}");
        else
            sb.Append("build failed");

        builder.ApplyField(nameof(LevelBuilderSO.levelData));

        watch.Restart();

        CreateLevel();

        if (c > 0)
            sb.AppendLine($"time to rebuild {watch.ElapsedTicks / 10000f} ms");

        Result = sb.ToString();
    }

    private void CreateLevel()
    {
        if (repo == null)
            return;

        if (root != null)
            DestroyImmediate(root.gameObject);
        root = new GameObject("level_root").transform;
        root.position =levelBounds.min;
        root.gameObject.hideFlags = HideFlags.HideAndDontSave;

        foreach(var index in SpatialUtil.Enumerate(levelBounds.size))
        {
            var hash = levelBlocks[index.z, index.y, index.x];
            if (hash != -1)
            {
                var block = repo.GetBlock(hash);
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

    private string ExportMesh(GameObject target)
    {
        var path = EditorUtility.SaveFilePanel("Mesh Export", Application.dataPath, "level ", "fbx");
        if (string.IsNullOrEmpty(path))
            return "";
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);

        return ModelExporter.ExportObject(path, target);
    }

    private void GroupSettingsGUI()
    {
        var settings = builder.groupsSettings;

        EditorGUI.BeginChangeCheck();

        settings.toggle = EditorGUILayout.BeginFoldoutHeaderGroup(settings.toggle, "Group Settings");

        if(settings.toggle)
        {
            for (int i = 0; i < repo.GroupsCount; i++)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.LabelField(builder.blockRepo.GroupsNames[i], GUILayout.Width(50));

                var setting = settings.groups[i];

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
            builder.ApplyField(nameof(LevelBuilderSO.groupsSettings));
    }

    BoundsInt DrawBoundsGUI(BoundsInt bounds)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        bounds.position = EditorGUILayout.Vector3IntField("Position", bounds.position);
        bounds.size = EditorGUILayout.Vector3IntField("Size", bounds.size);
        GUILayout.EndVertical();
        return bounds;
    }

    BoundsInt MoveBoundsHandle(BoundsInt bounds)
    {
        bounds.position = MathUtility.RoundToInt(
            Handles.PositionHandle(bounds.position, Quaternion.identity));

        DrawBounds(bounds);

        return bounds;
    }

    BoundsInt ScaleBoundsHandle(BoundsInt bounds)
    {
        bounds.size = MathUtility.RoundToInt(
            Handles.ScaleHandle(bounds.size,
            bounds.position, Quaternion.identity, 2f));

        DrawBounds(bounds);

        return bounds;
    }

    BoundsInt EditBoundsHandle(BoundsInt bounds)
    {
        handle.axes = PrimitiveBoundsHandle.Axes.All;
        handle.wireframeColor = Color.yellow;

        handle.center = bounds.center;
        handle.size = bounds.size;

        handle.midpointHandleDrawFunction = (controlID, position, rotation, size, eventType) =>
        {
            float threshold = 0.95f;
            var forward = rotation * Vector3.back;
            var cameraForward = SceneView.lastActiveSceneView.camera.transform.forward;
            bool draw = true;
            bool passive = false;
            if (Mathf.Abs(cameraForward.x) > threshold ||
                Mathf.Abs(cameraForward.y) > threshold ||
                Mathf.Abs(cameraForward.z) > threshold)
            {
                draw = Mathf.Abs(Vector3.Dot(forward, cameraForward)) < threshold;
            }
            else if (Vector3.Dot(forward, cameraForward) < 0)
                passive = true;

            Color color = Color.yellow;
            color *= passive ? 0.4f : 1f;

            if (draw)
            {
                var cmd = GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(MaterialType.UI).
                Scale(0.3f).Rotate(Quaternion.Inverse(rotation)).Move(position);
                float dist = default(CubeD).Distance(cmd);

                if (eventType == EventType.Repaint)
                    if (GUIUtility.hotControl == controlID)
                        cmd.SetColor(Color.green).Draw();
                    else
                        cmd.SetColor(color).Draw();
                else if (eventType == EventType.Layout)
                    HandleUtility.AddControl(controlID, dist);
            }
        };
        handle.DrawHandle();

        var min = handle.center - handle.size / 2;
        var max = handle.center + handle.size / 2;

        bounds.min = MathUtility.RoundToInt(min);
        bounds.max = MathUtility.RoundToInt(max);

        return bounds;
    }

    private static void DrawBounds(BoundsInt bounds)
    {
        var min = bounds.min;
        var max = bounds.max;
        Handles.color = Color.yellow;

        Handles.DrawAAPolyLine(2f,
            new Vector3(min.x, min.y, min.z), 
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, min.z));
        Handles.DrawAAPolyLine(2f,
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(min.x, min.y, max.z));
        Handles.DrawAAPolyLine(2f,
           new Vector3(min.x, min.y, min.z),
           new Vector3(min.x, min.y, max.z),
           new Vector3(max.x, min.y, max.z),
           new Vector3(max.x, min.y, min.z));
        Handles.DrawAAPolyLine(2f,
           new Vector3(min.x, max.y, min.z),
           new Vector3(min.x, max.y, max.z),
           new Vector3(max.x, max.y, max.z),
           new Vector3(max.x, max.y, min.z));
    }

    private void DrawInputWave()
    {
        var inputWave = builder.inputWave;
        int size = Mathf.Max(inputWave.Size.x, inputWave.Size.y, inputWave.Size.z);
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

        foreach(var i in SpatialUtil.Enumerate(inputWave.Size))
        {
            var groups = inputWave[i.z, i.y, i.x].groups;
            Color col = new Color();
            if (groups.Count == 0)
                col = Color.white;
            else if (groups.Count == 1)
            {
                var h = groups[0];
                var gi = repo.GroupsHash.GetIndex(h);
                if (gi == 0)
                    col = new Color(0, 0, 0, 0);
                else if (gi == 1)
                    col = Color.green;
                else
                    col = ColorUtility.GetColor(h);
            }
            else
            {
                var h = new XXHash();
                for (int g = 0; g < groups.Count; g++)
                    h = h.Append(groups[g]);
                col = ColorUtility.GetColor(h);
            }
            texture.SetPixel(i.x, i.y, i.z, col);
        }
        texture.Apply();

        Handles.matrix = Matrix4x4.TRS(builder.levelData.bounds.center, Quaternion.identity, size * Vector3.one);
        Handles.DrawTexture3DVolume(texture, 0.5f, 5, FilterMode.Point);
        Handles.matrix = Matrix4x4.identity;
    }

    private void DrawContextMenu()
    {
        Handles.BeginGUI();

        var p = BoundsUtility.ClosestCornerToScreenPoint(selection,new Vector2(SceneView.lastActiveSceneView.position.width,0));
        Rect rect = new Rect(HandleUtility.WorldToGUIPoint(p) + Vector2.down * 25f, new Vector2(150, 25));
        GUILayout.BeginArea(rect, GUI.skin.box);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Add"))
        {
            var menu = new GenericMenu();
            for (int i = 0; i < repo.GroupsCount; i++)
            {
                var index = i;
                menu.AddItem(new GUIContent(repo.GroupsNames[i]), false, () =>
                {
                    Add(inputWave, repo, selection, index);
                    builder.ApplyField(nameof(LevelBuilderSO.inputWave));
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
                menu.AddItem(new GUIContent(repo.GroupsNames[i]), false, () =>
                {
                    Remove(inputWave, repo, selection, index);
                    builder.ApplyField(nameof(LevelBuilderSO.inputWave));
                });
            }
            menu.ShowAsContext();
        }

        if (GUILayout.Button("Set"))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("All"), false, () =>
            {
                Set(inputWave, repo, selection, -1);
                builder.ApplyField(nameof(LevelBuilderSO.inputWave));
            });
            for (int i = 0; i < repo.GroupsCount; i++)
            {
                var index = i;
                menu.AddItem(new GUIContent(repo.GroupsNames[i]), false, () =>
                {
                    Set(inputWave, repo, selection, index);
                    builder.ApplyField(nameof(LevelBuilderSO.inputWave));
                });
            }
            menu.ShowAsContext();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        Handles.EndGUI();
    }

    public static void Remove(
    Array3D<InputWaveBlock> inputWave, BlocksRepo repo,
    BoundsInt bounds, int groupIndex)
    {
        var hash = repo.GroupsHash[groupIndex];
        foreach (var index in SpatialUtil.Enumerate(bounds))
        {
            var groups = inputWave[index.z, index.y, index.x].groups;
            if(groups.Contains(hash))
            {
                if (groups.Count == 1)
                    continue;

                if (groups.Count == 0)
                {
                    for (int i = 0; i < repo.GroupsCount; i++)
                    {
                        var h = repo.GroupsHash[i];
                        if (h != hash)
                            groups.Add(h);
                    }
                }
                else
                    groups.Remove(hash);
            }
        }
    }

    public static void Add(
        Array3D<InputWaveBlock> inputWave, BlocksRepo repo,
        BoundsInt bounds, int groupIndex)
    {
        var hash = repo.GroupsHash[groupIndex];
        foreach (var index in SpatialUtil.Enumerate(bounds))
        {
            var groups = inputWave[index.z, index.y, index.x].groups;
            if (!groups.Contains(hash))
            {
                if (groups.Count == repo.GroupsCount)
                    groups.Clear();
                else
                    groups.Add(hash);
            }
        }
    }

    public static void Set(
        Array3D<InputWaveBlock> inputWave, BlocksRepo repo,
        BoundsInt bounds, int groupIndex)
    {
        if (groupIndex >= 0)
        {
            var hash = repo.GroupsHash[groupIndex];
            foreach (var index in SpatialUtil.Enumerate(bounds))
            {
                var groups = inputWave[index.z, index.y, index.x].groups;
                groups.Clear();
                groups.Add(hash);
            }

        }else
        {
            foreach (var index in SpatialUtil.Enumerate(bounds))
                inputWave[index.z, index.y, index.x].groups.Clear();
        }
    }

    private IEnumerable<Vector3Int> SelectionEnumerator => SpatialUtil.Enumerate(selection.min, selection.max);
}