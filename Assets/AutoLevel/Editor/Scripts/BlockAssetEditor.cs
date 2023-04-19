using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static HandleEx;
using static FillUtility;
using static Directions;
using UnityEditorInternal;

[CustomEditor(typeof(BlockAsset))]
public class BlockAssetEditor : Editor
{
    class BlockAssetSO : BaseSO<BlockAsset>
    {
        public List<int> groups;
        public List<BlockVariant> variants;

        public BlockAssetSO(SerializedObject serializedObject) : base(serializedObject) { }

        public BlockAssetSO(Object target) : base(target) { }
    }

    private BlockAssetSO blockAsset;
    private List<BlockVariant> variants => blockAsset.variants;
    private List<int> groups => blockAsset.groups;
    private Transform transform => blockAsset.target.transform;

    private bool settingsToggle;
    private Editor settingsEditor;
    private BlockAssetEditorSettingsSO settingsSO;
    private BlockAssetEditorSettings.Settings settings => settingsSO.settings;

    private Mesh mesh;
    private BlocksRepo repo;
    private List<BlockAsset> allBlockAssets;
    private List<BlockAsset> activeBlockAssets;
    private List<Connection> visableConnections = new List<Connection>();

    private Material variantMat;
    private Material colorCubeMat;

    [SerializeField]
    private int selected;
    private Vector3 selectedPosition => transform.position + variants[selected].position_editor_only;

    private Rect contextMenuRect;

    private ReorderableList variantsReordable;
    private bool variantsReordableChanged;
    private int actionIndex;

    private bool connecting;
    private int connectingDir;

    const float cancelThreshold = 0.15f;
    double cancelStart;

    static int[] colors = new int[]
    {
            Shader.PropertyToID("_Left"),
            Shader.PropertyToID("_Down"),
            Shader.PropertyToID("_Back"),
            Shader.PropertyToID("_Right"),
            Shader.PropertyToID("_Up"),
            Shader.PropertyToID("_Front")
    };

    private List<string> allGroups;
    private Dictionary<int, string> GrouphashToIndex;

    private void OnEnable()
    {
        blockAsset = new BlockAssetSO(target);
        UpdateReferencesAndIntialize();

        variantMat = new Material(Shader.Find("Hidden/AutoLevel/Variant"));
        variantMat.hideFlags = HideFlags.HideAndDontSave;
        colorCubeMat = new Material(Shader.Find("Hidden/AutoLevel/ColorCube"));
        colorCubeMat.hideFlags = HideFlags.HideAndDontSave;

        CreateVariantsReordable();

        settingsEditor = CreateEditor(BlockAssetEditorSettings.GetSettings());
        settingsSO = new BlockAssetEditorSettingsSO(BlockAssetEditorSettings.GetSettings());

        SceneView.beforeSceneGui += BeforeSceneGUI;
    }

    private void OnDisable()
    {
        blockAsset.Dispose();
        settingsSO.Dispose();

        DestroyImmediate(variantMat);
        DestroyImmediate(colorCubeMat);
        DestroyImmediate(settingsEditor);

        SceneView.beforeSceneGui -= BeforeSceneGUI;
    }

    private void BeforeSceneGUI(SceneView scene)
    {
        var ec = Event.current;
        if (ec.type == EventType.MouseDown && ec.button == 1)
        {
            cancelStart = EditorApplication.timeSinceStartup;
        }else if(ec.type == EventType.MouseUp && ec.button == 1)
        {
            if ((float)(EditorApplication.timeSinceStartup - cancelStart) < cancelThreshold)
                connecting = false;
        }
    }

    public override void OnInspectorGUI()
    {
        UpdateReferencesAndIntialize();

        if (repo == null)
        {
            EditorGUILayout.HelpBox("block asset need to be nested under a BlockRepo in the hierarchy!", MessageType.Error);
            return;
        }

        settingsToggle = EditorGUILayout.BeginFoldoutHeaderGroup(settingsToggle, "Settings");
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (settingsToggle)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            settingsEditor.OnInspectorGUI();
            GUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        settings.GroupToggle = EditorGUILayout.BeginFoldoutHeaderGroup(settings.GroupToggle, "Groups");
        EditorGUILayout.EndFoldoutHeaderGroup();
        if (EditorGUI.EndChangeCheck())
            settingsSO.Apply();

        if (settings.GroupToggle)
        {
            DrawGroupGUI();
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("Variant Settings");

        EditorGUILayout.Space();

        var variant = variants[selected];

        variantsReordable.list = variant.actions;
        variantsReordable.DoLayoutList();

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        variant.connections.right = EditorGUILayout.IntField("Right", variant.connections.right);
        variant.connections.left = EditorGUILayout.IntField("Left", variant.connections.left);
        variant.connections.up = EditorGUILayout.IntField("Up", variant.connections.up);
        variant.connections.down = EditorGUILayout.IntField("Down", variant.connections.down);
        variant.connections.forward = EditorGUILayout.IntField("Front", variant.connections.forward);
        variant.connections.backward = EditorGUILayout.IntField("Back", variant.connections.backward);
        EditorGUILayout.Space();
        variant.weight = EditorGUILayout.FloatField("Weight", variant.weight);

        if (EditorGUI.EndChangeCheck() || variantsReordableChanged)
        {
            blockAsset.Apply();
            SceneView.RepaintAll();
            variantsReordableChanged = false;
        }

        EditorGUILayout.Space();

        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Connection To Variants"))
            ApplyConnectionsToVariants();

        if (GUILayout.Button("Weight To Variants"))
            ApplyWeightToVariants();
    }

    private void OnSceneGUI()
    {
        UpdateReferencesAndIntialize();

        if (repo == null)
            return;

        GenerateConnections();
        DrawConnections();

        if (settings.DrawVariants)
        {
            DrawVariants();
            DrawVariantsMoveHandle();
        }
        if(!connecting)
            DrawVariantsButtons();

        if (settings.EditMode == BlockEditMode.Connection)
            DrawConnectionControls();

        Handles.DrawWireCube(selectedPosition + Vector3.one * 0.5f, Vector3.one);

        if (settings.EditMode == BlockEditMode.Fill)
            FillControls();

        ContextMenu();
    }

    private void UpdateReferencesAndIntialize()
    {
        GetRepo();
        InitGroups();
        GetBlockAssets();

        blockAsset.Update();
        if (variants.Count == 0)
        {
            variants.Add(new BlockVariant()
            {
                fill = GenerateFill(transform),
                connections = new BlockConnection()
            });
            blockAsset.Apply();
            SetSelectedVariant(0);
        }

        if(repo != null)
        for (int i = 1; i < activeBlockAssets.Count; i++)
            if(activeBlockAssets[i].variants.Count == 0)
                using(var asset = new BlockAssetSO(activeBlockAssets[i]))
                {
                    asset.variants.Add(new BlockVariant()
                    {
                        fill = GenerateFill(asset.target.transform),
                        connections = new BlockConnection()
                    });
                    asset.Apply();
                }
    }
    private Mesh GetMesh(Transform transform)
    {
        var mf = transform.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }
    private void GetRepo()
    {
        repo = blockAsset.target.GetComponentInParent<BlocksRepo>();
    }
    private void GetBlockAssets()
    {
        if (repo == null || allBlockAssets != null)
            return;

        allBlockAssets = new List<BlockAsset>(repo.GetComponentsInChildren<BlockAsset>(true));
        allBlockAssets.Swap(0, allBlockAssets.FindIndex((e) => e == blockAsset.target));

        activeBlockAssets = new List<BlockAsset>(repo.GetComponentsInChildren<BlockAsset>());
        if (blockAsset.target.gameObject.activeSelf)
            activeBlockAssets.Swap(0, activeBlockAssets.FindIndex((e) => e == blockAsset.target));
        else
            activeBlockAssets.Insert(0, blockAsset.target);

    }

    private void InitGroups()
    {
        if (repo == null)
            return;

        if (allGroups == null)
        {
            allGroups = new List<string>(repo.blockGroups);
            allGroups.Add(BlocksRepo.BASE_GROUP);
            GrouphashToIndex = new Dictionary<int, string>();
            for (int i = 0; i < allGroups.Count; i++)
                GrouphashToIndex[allGroups[i].GetHashCode()] = allGroups[i];

            if (groups.Count == 0)
            {
                groups.Add(BlocksRepo.BASE_GROUP.GetHashCode());
            }
            else
            {
                for (int i = groups.Count - 1; i > -1; i--)
                    if (!GrouphashToIndex.ContainsKey(groups[i]))
                        groups.RemoveAt(i);
            }
            blockAsset.ApplyField(nameof(BlockAssetSO.groups));
        }
    }
    private void DrawGroupGUI()
    {
        GUILayout.BeginVertical();
        EditorGUILayout.Space(5);

        for (int i = 0; i < groups.Count; i++)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.Space(10);
            GUILayout.Label(GrouphashToIndex[groups[i]]);
            if(GUILayout.Button("X",GUILayout.Width(25)) && groups.Count > 1)
            {
                groups.RemoveAt(i);
                blockAsset.ApplyField(nameof(BlockAssetSO.groups));
                break;
            }
            GUILayout.EndHorizontal();
        }   

        EditorGUILayout.Space(5);
        GUILayout.EndVertical();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if(GUILayout.Button("+", GUILayout.Width(30)))
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < allGroups.Count; i++)
            {
                var g = allGroups[i].GetHashCode();
                if(!groups.Contains(g))
                {
                    menu.AddItem(new GUIContent(allGroups[i]), false, () =>
                    {
                        blockAsset.groups.Add(g);
                        blockAsset.ApplyField(nameof(BlockAssetSO.groups));
                    });
                }
            }
            menu.ShowAsContext();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawVariants()
    {
        for (int i = 0; i < activeBlockAssets.Count; i++)
        {
            var block = activeBlockAssets[i];
            var mr = block.GetComponent<MeshRenderer>();
            var mf = block.GetComponent<MeshFilter>();
            if (mr == null || mf.sharedMesh == null)
                continue;

            var mesh = mf.sharedMesh;
            if(mr.sharedMaterial != null)
            {
                var mainTex = mr.sharedMaterial.GetTexture("_MainTex");
                variantMat.SetTexture("_MainTex", mainTex);
            }

            for (int j = 1; j < block.variants.Count; j++)
            {
                var variant = block.variants[j];
                var cmd = GetDrawCmd().SetMesh(mesh).SetMaterial(variantMat);
                int revert = 0;
                int flips = 0;
                for (int k = 0; k < variant.actions.Count; k++)
                {
                    var ac = variant.actions[k];
                    var pivot = Vector3.one * 0.5f;
                    switch (ac)
                    {
                        case VariantAction.RotateX:
                            cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.right));
                            break;
                        case VariantAction.RotateY:
                            cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.up));
                            break;
                        case VariantAction.RotateZ:
                            cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.forward));
                            break;
                        case VariantAction.MirrorX:
                            cmd = cmd.Move(-pivot).Scale(new Vector3(-1, 1, 1)).Move(pivot);
                            revert++;
                            break;
                        case VariantAction.MirrorY:
                            cmd = cmd.Move(-pivot).Scale(new Vector3(1, -1, 1)).Move(pivot);
                            revert++;
                            break;
                        case VariantAction.MirrorZ:
                            cmd = cmd.Move(-pivot).Scale(new Vector3(1, 1, -1)).Move(pivot);
                            revert++;
                            break;
                        case VariantAction.Flip:
                            revert++;
                            flips++;
                            break;
                    }
                }

                cmd.Move(block.transform.position + variant.position_editor_only);
                variantMat.SetFloat("_NormalMulti", flips % 2 > 0 ? -1 : 1);
                if (revert % 2 > 0)
                    variantMat.SetFloat("_Cull", 1);
                else
                    variantMat.SetFloat("_Cull", 2);

                cmd.Draw();
            }
        }
    }
    private void DrawVariantsButtons()
    {
        int c = 0;
        for (int i = 1; i < activeBlockAssets.Count; i++)
        {
            var asset = activeBlockAssets[i];
            c = settings.DrawVariants ? asset.variants.Count : 1;
            for (int j = 0; j < c; j++)
                VariantColorCubeCmd(new VariantRef(asset, j)).SetColor(Color.white * 0.35f).Draw();
        }

        c = settings.DrawVariants ? variants.Count : 1;
        for (int i = 0; i < c; i++)
        {
            var varRef = new VariantRef(blockAsset.target, i);
            if (i != selected)
            {
                if (VariantButton(varRef))
                    SetSelectedVariant(i);
            }
            else
                VariantColorCubeCmd(varRef).SetColor(Color.white * 0.2f).Draw();
        }
    }
    private void DrawVariantsMoveHandle()
    {
        EditorGUI.BeginChangeCheck();
        foreach(var variant in variants)
        {
            variant.position_editor_only = Handles.DoPositionHandle(transform.position + variant.position_editor_only, Quaternion.identity) - transform.position;
        }
        if (EditorGUI.EndChangeCheck())
            blockAsset.Apply();
    }
    private bool VariantButton(VariantRef varRef)
    {
        DrawCommand dcmd = VariantColorCubeCmd(varRef);

        Button.SetAll(dcmd);

        Button.normal.SetColor(Color.white.SetAlpha(0.35f));
        Button.hover.SetColor(Color.white.SetAlpha(0.7f));
        Button.active.SetColor(Color.white.SetAlpha(0.7f));
        return Button.Draw<CubeD>();
    }
    private DrawCommand VariantColorCubeCmd(VariantRef varRef)
    {
        for (int d = 0; d < 6; d++)
        {
            colorCubeMat.SetColor(colors[d], GetColor(varRef,d));
        }

        var size = 1f;
        var dcmd = GetDrawCmd().
            SetPrimitiveMesh(PrimitiveType.Cube).
            SetMaterial(colorCubeMat).
            Scale(size).
            Move(varRef.position + Vector3.one * 0.5f);
        return dcmd;
    }

    private void ContextMenu()
    {
        Handles.BeginGUI();

        var buttonSize = 20f;
        var offset = 15;

        Bounds bounds = new Bounds(selectedPosition + Vector3.one * 0.5f, Vector3.one);
        var position = BoundsUtility.ClosestCornerToScreenPoint(bounds, new Vector2(SceneView.lastActiveSceneView.position.width, 0));

        var size = new Vector2(130, buttonSize + 5);
        contextMenuRect = new Rect(HandleUtility.WorldToGUIPoint(position) - Vector2.up * size.y
            + new Vector2(offset, -offset), size);

        GUILayout.BeginArea(contextMenuRect, GUI.skin.box);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("+",GUILayout.Width(buttonSize)))
        {
            GenericMenu addVariantMenu = new GenericMenu();
            for (int i = 0; i < 7; i++)
            {
                var action = (VariantAction)i;
                addVariantMenu.AddItem(new GUIContent(action.ToString()), false, () =>
                {
                    var variant = new BlockVariant(variants[selected]);
                    variant.ApplyAction(action);
                    variant.position_editor_only += Vector3.forward * 2f;
                    variants.Add(variant);

                    SetSelectedVariant(variants.Count - 1);
                    addVariantMenu = null;
                    blockAsset.Apply();
                });
                addVariantMenu.ShowAsContext();
            }
        }

        EditorGUI.BeginDisabledGroup(selected == 0);

        if (GUILayout.Button("-", GUILayout.Width(buttonSize)))
        {
            variants.RemoveAt(selected);
            SetSelectedVariant(Mathf.Clamp(selected,0, variants.Count - 1));
            blockAsset.Apply();
        }

        EditorGUI.EndDisabledGroup();


        settingsSO.Update();

        if(GUILayout.Button(settings.EditMode.ToString()))
        {
            GenericMenu menu = new GenericMenu();
            var names = System.Enum.GetNames(typeof(BlockEditMode));
            for (int i = 0; i < names.Length; i++)
            {
                var value = (BlockEditMode)i;
                menu.AddItem(new GUIContent(names[i]), false, () =>
                {
                    settings.EditMode = value;
                    settingsSO.Apply();
                });
            }
            menu.ShowAsContext();
        }

        settingsSO.Apply();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        Handles.EndGUI();

        if (Event.current.type == EventType.Layout)
        {
            if (contextMenuRect.Contains(Event.current.mousePosition))
            {
                var id = GUIUtility.GetControlID("Context".GetHashCode(), FocusType.Keyboard); ;
                HandleUtility.AddControl(id, -1);
            }

        }
    }
    private void SetSelectedVariant(int index)
    {
        if(index != selected)
        {
            var so = new SerializedObject(this);
            so.FindProperty(nameof(selected)).intValue = index;
            so.ApplyModifiedProperties();
        }
    }

    private void CreateVariantsReordable()
    {
        variantsReordable = new ReorderableList(variants[selected].actions, typeof(VariantAction));
        this.variantsReordable.index = actionIndex;
        variantsReordable.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Actions");
        variantsReordable.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            rect.position += Vector2.up * 2.5f;
            var variant = variants[selected];
            EditorGUI.BeginChangeCheck();
            variant.actions[index] = (VariantAction)EditorGUI.EnumPopup(rect, variant.actions[index]);
            variantsReordableChanged = EditorGUI.EndChangeCheck();
        };
        variantsReordable.onSelectCallback = (list) =>
        {
            actionIndex = list.index;
        };
        variantsReordable.onRemoveCallback = (list) =>
        {
            list.list.RemoveAt(list.index);
            variantsReordableChanged = true;
        };
        variantsReordable.onAddCallback = (list) =>
        {
            list.list.Add(VariantAction.RotateX);
            variantsReordableChanged = true;
        };
    }

    private void ApplyConnectionsToVariants()
    {
        var src = variants[selected].connections;
        for (int i = 0; i < variants.Count; i++)
        {
            if (i == selected)
                continue;
            var dst = variants[i];
            dst.connections = src;
            for (int j = 0; j < dst.actions.Count; j++)
                dst.connections.ApplyAction(dst.actions[j]);
        }
        blockAsset.Apply();
    }
    private void ApplyFillingToVariants()
    {
        var src = variants[selected].fill;
        for (int i = 0; i < variants.Count; i++)
        {
            if (i == selected)
                continue;
            var dst = variants[i];
            int filling = src;
            for (int j = 0; j < dst.actions.Count; j++)
                filling = ApplyAction(filling, dst.actions[j]);
            dst.fill = filling;
        }
        blockAsset.Apply();
    }
    private void ApplyWeightToVariants()
    {
        var src = variants[selected].weight;
        for (int i = 0; i < variants.Count; i++)
        {
            if (i == selected)
                continue;
            variants[i].weight = src;
        }
        blockAsset.Apply();
    }

    private void FillControls()
    {
        var variant = variants[selected];
        int fill = variant.fill;
        var pos = transform.position + variant.position_editor_only;
        bool didChange = false;

        for (int i = 0; i < 8; i++)
        {
            if ((fill & (1 << i)) > 0)
            {
                if (!SphereToggleHandle(true, pos + nodes[i]))
                {
                    fill &= ~(1 << i);
                    didChange = true;
                }
            }
            else
            {
                if (SphereToggleHandle(false, pos + nodes[i]))
                {
                    fill |= (1 << i);
                    didChange = true;
                }
            }
        }
        variant.fill = fill;

        if (didChange)
        {
            blockAsset.Apply();
            ApplyFillingToVariants();
        }
    }
    private int GenerateFill(Transform transform)
    {
        Mesh mesh = GetMesh(transform);

        if (mesh == null)
            return 0;

        if (!mesh.isReadable)
            Debug.Log("making the mesh readable help with filling setup");

        int fill = 0;
        var verts = new List<Vector3>();
        var indices = new List<int>();
        mesh.GetVertices(verts);
        mesh.GetIndices(indices, 0);

        float e = 0.0001f;
        Ray[] rays = new Ray[]
        {
            new Ray(nodes[1] + new Vector3(0.1f,e,e),Vector3.left),
            new Ray(nodes[3] + new Vector3(0.1f,e,-e),Vector3.left),
            new Ray(nodes[5] + new Vector3(0.1f,-e,e),Vector3.left),
            new Ray(nodes[7] + new Vector3(0.1f,-e,-e),Vector3.left),

            new Ray(nodes[4] + new Vector3(e,0.1f,e),Vector3.down),
            new Ray(nodes[5] + new Vector3(-e,0.1f,e),Vector3.down),
            new Ray(nodes[6] + new Vector3(e,0.1f,-e),Vector3.down),
            new Ray(nodes[7] + new Vector3(-e,0.1f,-e),Vector3.down),

            new Ray(nodes[2] + new Vector3(e,e,0.1f),Vector3.back),
            new Ray(nodes[3] + new Vector3(-e,e,0.1f),Vector3.back),
            new Ray(nodes[6] + new Vector3(e,-e,0.1f),Vector3.back),
            new Ray(nodes[7] + new Vector3(-e,-e,0.1f),Vector3.back),
        };
        Vector2Int[] rayNodes = new Vector2Int[]
        {
            new Vector2Int(1,0),
            new Vector2Int(3,2),
            new Vector2Int(5,4),
            new Vector2Int(7,6),
            new Vector2Int(4,0),
            new Vector2Int(5,1),
            new Vector2Int(6,2),
            new Vector2Int(7,3),
            new Vector2Int(2,0),
            new Vector2Int(3,1),
            new Vector2Int(6,4),
            new Vector2Int(7,5),
        };
        bool[] rayHit = new bool[12];
        for (int i = 0; i < indices.Count; i += 3)
        {
            var v0 = verts[indices[i]];
            var v1 = verts[indices[i + 1]];
            var v2 = verts[indices[i + 2]];
            float t = 0;
            Vector3 normal = Vector3.zero;
            Vector3 bary = Vector3.zero;

            for (int j = 0; j < rays.Length; j++)
            {
                if (MeshUtility.RayTriangleIntersect(rays[j], v0, v1, v2,
                    ref t, ref bary, ref normal))
                {
                    rayHit[j] = true;
                    int bitShift;
                    if (Vector3.Dot(rays[j].direction, normal) > 0)
                        bitShift = rayNodes[j].x;
                    else
                        bitShift = rayNodes[j].y;
                    fill |= 1 << bitShift;
                }
            }
        }

        System.Action validateNeighbour = () =>
        {
            for (int i = 0; i < rayNodes.Length; i++)
            {
                if (!rayHit[i])
                {
                    var ind = rayNodes[i];
                    if (
                        ((fill & (1 << ind.x)) > 0) ||
                        ((fill & (1 << ind.y)) > 0))
                    {
                        fill |= 1 << ind.x;
                        fill |= 1 << ind.y;
                    }
                }
            }
        };
        validateNeighbour();
        validateNeighbour();

        return fill;
    }
    bool SphereToggleHandle(bool value, Vector3 position, float size = 0.1f)
    {
        var draw = GetDrawCmd().SetPrimitiveMesh(PrimitiveType.Sphere).Scale(size).Move(position);
        Button.SetAll(draw);
        Button.normal.SetColor(value ? Color.green : Color.red);
        Button.hover.SetColor(Color.yellow);
        Button.active.SetColor(Color.yellow);
        bool pressed = Button.Draw<SphereD>();
        return pressed ? !value : value;
    }

    private void DrawConnections()
    {
        var count = Mathf.Min(settings.MaxConnectionsDrawCount, visableConnections.Count);
        for (int i = 0; i < count; i++)
            DrawConnection(visableConnections[i]);
    }
    private void DrawConnection(Connection con)
    {
        if (con.a == con.b)
        {
            if (settings.DrawSelfConnections)
                Draw(con, Color.cyan);
        }
        else
        {
            var hash = new XXHash().Append(con.a.variant.connections[con.d]);
            Draw(con, ColorUtility.GetColor(hash));
        }
    }
    private void Draw(Connection con, Color color)
    {
        var s = con.a.position + origins[con.d];
        var st = directions[con.d];
        var e = con.b.position + origins[opposite[con.d]];
        var et = directions[opposite[con.d]];
        Handles.DrawBezier(s, e, s + st, e + et, color, null, 1f);
    }
    private void DrawConnectionControls()
    {
        var target = new VariantRef(blockAsset.target, selected);
        if (!connecting)
        {
            DrawButtonForEachSide(target, (d) =>
            {
                connecting = true;
                connectingDir = d;
            });
        }
        else
        {
            SceneView.RepaintAll();

            Handles.DrawDottedLine(
                GetSideCenter(target.position, connectingDir),
                HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f)
                , 1f);

            var availableVariants = GetPossibleConnections(target, connectingDir);
            foreach (var varRef in availableVariants)
            {
                if (VariantButton(varRef))
                {
                    MakeConnection(new Connection(target, varRef, connectingDir));
                    connecting = false;
                }
            }
        }
    }
    private void MakeConnection(Connection connection)
    {
        List<(VariantRef, int, int)> writeOps = new List<(VariantRef, int, int)>();

        int d = connection.d;
        int od = opposite[d];
        var hashes = ConnectionsUtility.GetListOfIds(allBlockAssets);

        var aId = connection.a.connections[d];
        var bId = connection.b.connections[opposite[d]];
        bool isAConnected = hashes.Contains(aId);
        bool isBConnected = hashes.Contains(bId);

        if (!isAConnected && !isBConnected)
        {
            var next = ConnectionsUtility.GetNextId(hashes);
            writeOps.Add((connection.a, d, next));
            writeOps.Add((connection.b, od, next));
        }
        else if (!isAConnected)
        {
            writeOps.Add((connection.a, d, bId));
        }
        else if (!isBConnected)
        {
            writeOps.Add((connection.b, od, aId));
        }
        else
        {
            //create new connection
            //var next = ConnectionsUtility.GetNextId(hashes);
            //writeOps.Add((connection.a, d, next));
            //writeOps.Add((connection.b, od, next));

            BlockAsset.IterateVariants(allBlockAssets, (varRef) =>
            {
                if (varRef.connections[d] == bId)
                    writeOps.Add((varRef, d, aId));
                if (varRef.connections[od] == bId)
                    writeOps.Add((varRef, od, aId));
            });
        }

        for (int i = 0; i < writeOps.Count; i++)
        {
            var op = writeOps[i];
            var so = new BlockAssetSO(op.Item1.blockAsset);
            var variant = so.variants[op.Item1.index];
            variant.connections[op.Item2] = op.Item3;
            so.variants[op.Item1.index] = variant;
            so.Apply();
            so.Dispose();
        }
        Repaint();
    }
    private HashSet<VariantRef> GetPossibleConnections(VariantRef targetRef, int d)
    {
        var hash = GetSide(targetRef.variant.fill, d);
        var od = opposite[d];

        var res = new HashSet<VariantRef>();

        BlockAsset.IterateVariants(activeBlockAssets, (varRef) =>
        {
            if (hash == GetSide(varRef.variant.fill, od))
                res.Add(varRef);
        });

        return res;
    }
    private void DrawButtonForEachSide(VariantRef variantRef, System.Action<int> OnClick)
    {
        for (int d = 0; d < 6; d++)
            if (CullTest(variantRef.position, d))
            {
                if (SideButton(GetSideCenter(variantRef.position, d), directions[d], 0.9f, GetColor(variantRef,d),0.7f))
                    OnClick.Invoke(d);
            }
    }
    private bool CullTest(Vector3 pos, int side)
    {
        var camera = SceneView.lastActiveSceneView.camera;
        var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
        var c = vp.MultiplyPoint(GetSideCenter(pos, side));
        var u = vp.MultiplyPoint(GetSideU(pos, side));
        var v = vp.MultiplyPoint(GetSideV(pos, side));
        Vector3 clipSpaceNormal = Vector3.Cross((u - c), (v - c));
        return clipSpaceNormal.z >= 0;
    }
    bool SideButton(Vector3 center, Vector3 normal, float size, Color color,float alpha = 0.2f)
    {
        var draw = GetDrawCmd().Scale(size).LookAt(-normal).Move(center);
        Button.SetAll(draw);
        Button.normal.SetColor(color * Color.white.SetAlpha(alpha));
        Button.hover.SetColor(color * Color.white.SetAlpha(alpha + 0.2f));
        Button.active.SetColor(color * Color.white.SetAlpha(alpha + 0.4f));
        return Button.Draw<QuadD>();
    }
    private void GenerateConnections()
    {
        if (repo == null)
            return;


        List<VariantRef> variants = new List<VariantRef>();
        if (settings.DrawVariants)
            BlockAsset.IterateVariants(activeBlockAssets, (varRef) => variants.Add(varRef));
        else
            foreach (var block in activeBlockAssets)
                variants.Add(new VariantRef(block, 0));

        visableConnections.Clear();
        ConnectionsUtility.GetConnectionsList(variants, visableConnections);
    }

    private Color GetColor(VariantRef varRef, int d)
    {
        var hash = new XXHash().Append(varRef.variant.connections[d]);
        return ColorUtility.GetColor(hash);
    }
}