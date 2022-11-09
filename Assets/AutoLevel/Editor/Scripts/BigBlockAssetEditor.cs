using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;


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
            public List<int> actionsGroups;
            public Array3D<AssetBlock> data;

            public SO(SerializedObject serializedObject) : base(serializedObject) { }
            public SO(Object target) : base(target) { }
        }

        private SO blockAsset;
        private Transform transform => blockAsset.target.transform;
        private Array3D<AssetBlock> data => blockAsset.data;
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
            blockAsset = new SO(target);
            visibilityLevel = data.Size.y;

            base.OnEnable();
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
            {
                data.Resize(Vector3Int.Max(Vector3Int.one, data.Size));
                blockAsset.ApplyField(nameof(SO.data));
            }

            actionsList = new HashedFlagList(
                repo.GetActionsGroupsNames(),
                actionsGroups,
                () => blockAsset.ApplyField(nameof(SO.actionsGroups)));

            GenerateConnections(activeConnections);
        }

        protected override void Update() { }

        protected override void InspectorGUI()
        {
            EditorGUILayout.Space();

            actionsList.Draw(actionsGroups);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            var size = EditorGUILayout.Vector3IntField("Size", data.Size);
            if (EditorGUI.EndChangeCheck())
            {
                data.Resize(Vector3Int.Max(Vector3Int.one, size));
                blockAsset.ApplyField(nameof(SO.data));
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Selected");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(true);

            EditorGUI.indentLevel += 1;

            var block = data[index];
            EditorGUILayout.ObjectField("Asset", block.blockAsset, typeof(BlockAsset), true);
            EditorGUILayout.IntField("Variant", block.VariantIndex);

            EditorGUI.indentLevel -= 1;

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Remove"))
            {
                var b = data[index];
                DetachBlock(b);

                data[index] = new AssetBlock();
                blockAsset.ApplyField(nameof(SO.data));
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

            DrawAssetsBlocks(activeRepoEntities);

            if (connecting)
                DrawAssetsBlocksSides(activeRepoEntities.Where((asset) => asset != blockAsset.target));
            else
                DrawAssetsButtonsSides(activeRepoEntities.Where((asset) => asset != blockAsset.target));

            DrawConnections(activeConnections);

            if (editMode == EditMode.EditCell)
            {
                DoVisibilityHandle();
                DrawGrid();
            }

            DrawBounds(bounds, Color.yellow);

            if ((int)(editMode & EditMode.EditCell) > 0)
                DoCellEditing();

            if ((int)(editMode & EditMode.EditConnections) > 0)
                DoConnectionEditing();

            HandleTool();

            DoContextMenu();
        }

        #endregion

        private void DoVisibilityHandle()
        {
            var visibilityHandleDraw = GetDrawCmd().
                        SetPrimitiveMesh(PrimitiveType.Cube).
                        SetMaterial(MaterialType.OpaqueShaded).
                        SetColor(Color.green).Scale(0.25f).
                        Move(transform.position + Vector3.up * visibilityLevel + ((Vector3)data.Size).xz().xny());

            Drag.SetAll(visibilityHandleDraw);

            Drag.normal.SetColor(Color.green);
            Drag.hover.SetColor(Color.yellow);
            Drag.active.SetColor(Color.cyan);

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
               SetMaterial(handleRes.GridMat).
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
                SetColor(Color.cyan).
                Draw();
        }
        private void DoCellPicking()
        {
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (visibilityBounds.IntersectRay(ray, out var t))
            {
                var index = MathUtility.FloorToInt(ray.GetPoint(t) - transform.position);
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
            Button.normal.SetColor(Color.yellow);
            Button.hover.SetColor(Color.yellow);
            Button.active.SetColor(Color.green);

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
            foreach (var blockItem in AssetsBlocksIt(activeRepoEntities.Where((e) => e != blockAsset.target)))
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
                        if (data[index].blockAsset != null)
                            DetachBlock(data[index]);

                        data[index] = block;
                        blockAsset.ApplyField(nameof(SO.data));

                        using (var asset = new BlockAssetEditor.SO(block.blockAsset))
                        {
                            asset.variants[block.VariantIndex].bigBlock = blockAsset.target;
                            asset.ApplyField(nameof(BlockAssetEditor.SO.variants));
                        }

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
                    var block = data[i];
                    if (block.blockAsset == null)
                        sides[d] = -1;
                    else
                        sides[d] = FillUtility.GetSide(block.fill, opposite[d]);
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
                    new BlockSide(data[connectingIndex], connectingDir),
                    GetPositionFromBigBlockAsset(data[connectingIndex]),
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
                        connectingIndex = GetIndexInBigBlock((AssetBlock)sideItem.Item1.block);
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
                var size = MathUtility.RoundToInt(
                Handles.ScaleHandle(data.Size,
                transform.position, Quaternion.identity, 2f));
                size = Vector3Int.Max(Vector3Int.one, size);

                foreach (var index in SpatialUtil.Enumerate(data.Size))
                {
                    var block = data[index];
                    if (block.blockAsset != null &&
                        (index.x >= size.x || index.y >= size.y || index.z >= size.z))
                        DetachBlock(block);
                }

                if (size != data.Size)
                {
                    data.Resize(size);
                    blockAsset.ApplyField(nameof(SO.data));
                }
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

        private Vector3 GetCellCenterPosition(Vector3Int index)
        {
            return index + Vector3.one * 0.5f + transform.position;
        }
        private void DetachBlock(AssetBlock block)
        {
            if (block.bigBlock == null)
                return;

            using (var asset = new SO(block.bigBlock))
            {
                asset.data[GetIndexInBigBlock(block)] = new AssetBlock();
                asset.ApplyField(nameof(SO.data));
            }

            using (var asset = new BlockAssetEditor.SO(block.blockAsset))
            {
                asset.variants[block.VariantIndex].bigBlock = null;
                asset.ApplyField(nameof(BlockAssetEditor.SO.variants));
            }

            blockAsset.Update();
        }
    }

}