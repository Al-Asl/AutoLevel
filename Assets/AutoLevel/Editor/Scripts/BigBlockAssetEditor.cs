using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AlaslTools;


namespace AutoLevel
{
    using static HandleEx;
    using static Directions;

    [CustomEditor(typeof(BigBlockAsset))]
    public class BigBlockAssetEditor : BaseRepoEntityEditor
    {
        enum EditMode
        {
            EditCell = 1,
            CellConnecting = 5,
            EditConnections = 2,
            ConnectionConnecting = 6,
        }

        public class SO : BaseSO<BigBlockAsset>
        {
            public bool overrideGroup;
            public int group;

            public bool overrideWeightGroup;
            public int weightGroup;

            public List<int> actionsGroups;
            public Array3D<SList<AssetBlock>> data;

            public SO(SerializedObject serializedObject) : base(serializedObject) { }
            public SO(Object target) : base(target) { }
        }

        private SO blockAsset;
        private Transform transform => blockAsset.target.transform;
        private Array3D<SList<AssetBlock>> data => blockAsset.data;
        private List<int> actionsGroups => blockAsset.actionsGroups;
        private Bounds bounds => new Bounds() { min = transform.position, max = transform.position + data.Size };
        private Bounds visibilityBounds => new Bounds() { min = transform.position, max = transform.position + new Vector3(data.Size.x, visibilityLevel, data.Size.z) };

        private Tool current;

        private HashedFlagList actionsList;

        private EditMode editMode = EditMode.EditCell;
        private Vector3Int connectingIndex;
        private int connectingDir;
        private bool connecting
        {
            get => ((int)editMode & 4) > 0;
            set => editMode = (EditMode)(value ? 4 | (int)editMode : ~4 & (int)editMode);
        }

        private Vector3Int highLightedIndex;
        private Vector3Int index;
        private int visibilityLevel;

        #region Callback

        protected override void OnEnable()
        {
            base.OnEnable();

            blockAsset = new SO(target);
            visibilityLevel = data.Size.y;
        }

        protected override void OnDisable()
        {
            Tools.current = current;
            blockAsset.Dispose();

            base.OnDisable();
        }

        protected override void Initialize()
        {
            if (data.Size.x < 0 || data.Size.y < 0 || data.Size.z < 0)
                DataResize(Vector3Int.one);

            actionsList = new HashedFlagList(
                repo.GetActionsGroupsNames(),
                actionsGroups,
                () => blockAsset.ApplyField(nameof(SO.actionsGroups)));

            if(GetGroupIndex(blockAsset.group) == -1)
                blockAsset.group = allGroups[0].GetHashCode();
            if (GetWeightGroupIndex(blockAsset.weightGroup) == -1)
                blockAsset.weightGroup = allWeightGroups[0].GetHashCode();

            GenerateConnections(activeConnections);
        }

        protected override void Update() { }

        protected override void InspectorGUI()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            GUILayout.BeginHorizontal();

            blockAsset.overrideGroup = GUILayout.Toggle(blockAsset.overrideGroup, "Override Group", GUI.skin.button , GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            if(blockAsset.overrideGroup)
            {
                int group = GetGroupIndex(blockAsset.group);
                group = EditorGUILayout.Popup("", group, allGroups);
                blockAsset.group = allGroups[group].GetHashCode();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            blockAsset.overrideWeightGroup = GUILayout.Toggle(blockAsset.overrideWeightGroup, "Override Weight Group", GUI.skin.button, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            if (blockAsset.overrideWeightGroup)
            {
                int weightGroup = GetWeightGroupIndex(blockAsset.weightGroup);
                weightGroup = EditorGUILayout.Popup("", weightGroup, allWeightGroups);
                blockAsset.weightGroup = allWeightGroups[weightGroup].GetHashCode();
            }

            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                blockAsset.ApplyField(nameof(SO.overrideGroup));
                blockAsset.ApplyField(nameof(SO.group));
                blockAsset.ApplyField(nameof(SO.overrideWeightGroup));
                blockAsset.ApplyField(nameof(SO.weightGroup));
            }

            EditorGUILayout.Space();

            actionsList.Draw(actionsGroups);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            var size = EditorGUILayout.Vector3IntField("Size", data.Size);
            if (EditorGUI.EndChangeCheck())
                DataResize(size);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Selected");

            EditorGUILayout.Space();

            var list = data[index];
            foreach (var block in list)
            {
                EditorGUI.indentLevel += 1;

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginDisabledGroup(true);
                var preWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 70;
                EditorGUILayout.ObjectField("Variant", block.blockAsset, typeof(BlockAsset), true);
                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.IntField("", block.VariantIndex);
                EditorGUIUtility.labelWidth = preWidth;
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("x", GUILayout.Width(25)))
                    DetachBlock(block);

                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel -= 1;
            }

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var pwidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;

            index.x = EditorGUILayout.IntField("", index.x, GUILayout.Width(30));
            index.y = EditorGUILayout.IntField("", index.y, GUILayout.Width(30));
            index.z = EditorGUILayout.IntField("", index.z, GUILayout.Width(30));

            index = Vector3Int.Max(Vector3Int.zero, index);
            index = Vector3Int.Min(data.Size - Vector3Int.one, index);

            EditorGUIUtility.labelWidth = pwidth;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        protected override void SceneGUI()
        {
            if (cancelDown)
                connecting = false;

            DrawAssetsBlocks();

            if (connecting)
                DrawAssetsBlocksSides(activeRepoEntities.Where((asset) => asset != blockAsset.target));
            else
                DoBlocksSelectionButtons(activeRepoEntities.Where((asset) => asset != blockAsset.target));

            DrawLinesToCellsBlocks();

            DrawConnections(activeConnections);

            if (editMode == EditMode.EditCell)
                DrawGrid();

            DrawBounds(bounds, Color.yellow);

            if ((int)(editMode & EditMode.EditCell) > 0)
                DoCellEditing();

            if ((int)(editMode & EditMode.EditConnections) > 0)
                DoConnectionEditing();

            if (editMode == EditMode.EditCell)
                DoVisibilityHandle();

            HandleTool();

            DoContextMenu();
        }

        #endregion

        private void DoVisibilityHandle()
        {
            var visibilityHandleDraw = GetDrawCmd().
                        SetPrimitiveMesh(PrimitiveType.Cube).
                        SetMaterial(MaterialType.UI).
                        SetColor(Color.green).Scale(0.25f).
                        Move(transform.position + Vector3.up * visibilityLevel + ((Vector3)data.Size).xz().xny());

            Drag.SetAll(visibilityHandleDraw);

            Drag.normal.SetColor(NiceColors.Pistachio);
            Drag.hover.SetColor(NiceColors.Saffron);
            Drag.active.SetColor(NiceColors.ImperialRed);

            if (Drag.Draw<CubeD>())
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                var xy = new Plane(Vector3.forward, transform.position + data.Size.xz().xny());
                var yz = new Plane(Vector3.right, transform.position + data.Size.xz().xny());
                float t;
                Vector3 point = default;
                if (xy.Raycast(ray, out t))
                    point = ray.GetPoint(t);
                else if (yz.Raycast(ray, out t))
                    point = ray.GetPoint(t);

                visibilityLevel = Mathf.RoundToInt(point.y - transform.position.y);
                visibilityLevel = Mathf.Clamp(visibilityLevel, 1, data.Size.y);
            }
        }
        private void DrawGrid()
        {
            GetDrawCmd().
               SetPrimitiveMesh(PrimitiveType.Cube).
               SetMaterial(HandleEx.GridMaterial).
               Scale(visibilityBounds.size).
               Move(visibilityBounds.size * 0.5f + transform.position).
               Draw();
        }
        private void DoCellEditing()
        {
            if (connecting)
                DoCellConnecting();
            else
                DoCellPicking();

            GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(handleRes.ButtonCubeMat).
                Move(GetCellCenterPosition(index)).
                SetColor(NiceColors.CarrotRrange).
                Draw();
        }
        private void DoCellPicking()
        {
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (visibilityBounds.IntersectRay(ray, out var t))
            {
                var index = Vector3Int.FloorToInt(ray.GetPoint(t) - transform.position);
                index = Vector3Int.Max(Vector3Int.zero, index);
                index = Vector3Int.Min(new Vector3Int(data.Size.x, visibilityLevel, data.Size.z) - Vector3Int.one, index);

                if (index != highLightedIndex)
                {
                    SceneView.RepaintAll();
                    highLightedIndex = index;
                }
            }

            var buttonDraw = GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(handleRes.ButtonCubeMat).
                Move(GetCellCenterPosition(highLightedIndex));

            Button.SetAll(buttonDraw);
            Button.normal.SetColor(NiceColors.DarkCyan);
            Button.hover.SetColor(NiceColors.Cerulean);
            Button.active.SetColor(NiceColors.Cerulean);

            if (Button.Draw<CubeD>())
            {
                index = highLightedIndex;
                connecting = true;
                Repaint();
            }
        }
        private void DoCellConnecting()
        {
            SceneView.RepaintAll();

            Handles.DrawDottedLine(
            index + Vector3.one * 0.5f + transform.position,
            HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f)
            , 1f);

            int[] sides = GetCellFillSides(index);
            foreach (var blockItem in GetBlocksIt(AssetType.BlockAsset))
            {
                var draw = true;
                for (int d = 0; d < 6; d++)
                {
                    if (sides[d] == -1)
                        continue;
                    if (sides[d] != FillUtility.GetSide(blockItem.Item1.fill, d))
                    {
                        draw = false;
                        break;
                    }
                }
                if (draw)
                {
                    if (BlockButton(blockItem.Item1, blockItem.Item2))
                    {
                        var block = blockItem.Item1;

                        DetachBlock(block);
                        AttachBlock(index,block);

                        editMode = EditMode.EditCell;
                        Repaint();
                    }
                }
            }
        }
        private int[] GetCellFillSides(Vector3Int index)
        {
            BoundsInt dataBounds = new BoundsInt() { min = Vector3Int.zero, max = data.Size };
            int[] sides = new int[6];
            for (int d = 0; d < 6; d++)
            {
                var i = index + delta[d];
                if (dataBounds.Contains(i))
                {
                    var list = data[i];
                    if (list.IsEmpty)
                        sides[d] = -1;
                    else
                        sides[d] = FillUtility.GetSide(list[0].fill, opposite[d]);
                }
                else
                    sides[d] = -1;
            }

            return sides;
        }

        private void DoConnectionEditing()
        {
            if (connecting)
                DoSideConnection(
                    new BlockSide(data[connectingIndex][0], connectingDir),
                    GetPositionFromBigBlockAsset(data[connectingIndex][0]),
                    () =>
                    {
                        connecting = false;
                        GenerateConnections(activeConnections);
                    });
            else
                foreach (var sideItem in BigBlockSidesIt(blockAsset.target))
                {
                    if (BlockSideButton(sideItem.Item1, sideItem.Item2, 0.9f))
                    {
                        connectingIndex = GetIndexInBigBlock((AssetBlock)sideItem.Item1.block).Item1;
                        connectingDir = sideItem.Item1.d;
                        connecting = true;
                    }
                }
        }

        private void HandleTool()
        {
            if (Tools.current != Tool.None)
            {
                current = Tools.current;
                if (Tools.current != Tool.Move)
                    Tools.current = Tool.None;
            }

            if (current == Tool.Scale)
            {
                var size = Vector3Int.RoundToInt(
                Handles.ScaleHandle(data.Size,
                transform.position, Quaternion.identity, 2f));
                size = Vector3Int.Max(Vector3Int.one, size);

                if (size != data.Size)
                    DataResize(size);
            }
        }

        private void DoContextMenu()
        {

            Handles.BeginGUI();

            EditorGUI.BeginDisabledGroup(connecting);

            var buttonSize = 20f;
            var offset = 5;

            var position = BoundsUtility.ClosestCornerToScreenPoint(bounds, new Vector2(SceneView.lastActiveSceneView.position.width, 0));

            var size = new Vector2(110, buttonSize + 5);
            var contextMenuRect = new Rect(HandleUtility.WorldToGUIPoint(position) - Vector2.up * size.y
                + new Vector2(offset, -offset), size);

            GUILayout.BeginArea(contextMenuRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(editMode.ToString()))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent(EditMode.EditCell.ToString()), false,
                () =>
                {
                    editMode = EditMode.EditCell;
                });
                menu.AddItem(new GUIContent(EditMode.EditConnections.ToString()), false,
                () =>
                {
                    editMode = EditMode.EditConnections;
                });
                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            EditorGUI.EndDisabledGroup();

            Handles.EndGUI();

            if (Event.current.type == EventType.Layout)
            {
                if (contextMenuRect.Contains(Event.current.mousePosition))
                {
                    var id = GUIUtility.GetControlID("Context".GetHashCode(), FocusType.Keyboard); ;
                    HandleUtility.AddControl(id, 0);
                }
            }
        }

        private void DrawLinesToCellsBlocks()
        {
            foreach (var index in SpatialUtil.Enumerate(data.Size))
            {
                foreach(var block in data[index])
                {
                    if (block.gameObject.activeInHierarchy)
                    {
                        DrawBlockToBigBlockConnection(block);
                        if(DoBlockDetachButton(block))
                            blockAsset.Update();
                    }
                }
            }
        }

        private Vector3 GetCellCenterPosition(Vector3Int index)
        {
            return index + Vector3.one * 0.5f + transform.position;
        }
        private void DetachBlock(AssetBlock block)
        {
            DetachFromBigBlock(block);
            blockAsset.Update();
        }
        private void AttachBlock(Vector3Int index,AssetBlock block)
        {
            AttachToBigBlock(index, blockAsset.target, block);

            var list = blockAsset.target.data[index];
            if (list.Count > 1)
            {
                foreach(var d in BigBlockExtSideIt(blockAsset.target,index))
                    WriteBlockSide(block, d, list[0].baseIds[d]);
            }

            blockAsset.Update();
        }

        private void DataResize(Vector3Int size)
        {
            foreach (var index in SpatialUtil.Enumerate(data.Size))
                foreach (var block in data[index])
                {
                    if (index.x >= size.x || index.y >= size.y || index.z >= size.z)
                        DetachBlock(block);
                }
            data.Resize(Vector3Int.Max(Vector3Int.one, size));
            foreach (var index in SpatialUtil.Enumerate(data.Size))
                if (data[index] == null) data[index] = new SList<AssetBlock>();
            blockAsset.ApplyField(nameof(SO.data));
            SceneView.RepaintAll();
        }
    }

}