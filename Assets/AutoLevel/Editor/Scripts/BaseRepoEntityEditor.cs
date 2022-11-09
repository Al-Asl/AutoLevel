using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace AutoLevel
{
    using static HandleEx;
    using static Directions;
    using static FillUtility;

    public abstract class BaseRepoEntityEditor : Editor
    {
        protected class InitializeCommand
        {
            public void Set(string data)
            {
                consume = false;
                this.data = data;
            }

            public bool Read(out string data)
            {
                data = this.data;
                this.data = null;
                var res = consume;
                consume = true;
                return !res;
            }

            private bool consume = true;
            private string data;
        }

        protected struct BlockSide
        {
            public int id => block.baseIds[d];
            public int compsiteId => block[d];

            public IBlock block;
            public int d;

            public BlockSide(IBlock block, int d)
            {
                this.block = block;
                this.d = d;
            }
        }

        protected static InitializeCommand initializeCommand = new InitializeCommand();

        protected HandleResources handleRes;

        private bool settingsToggle;
        protected Editor settingsEditor;
        protected RepoEntitiesEditorSettings.Settings settings => settingsSO.settings;
        protected RepoEntitiesEditorSettingsSO settingsSO;

        protected List<Connection> activeConnections;
        protected List<MonoBehaviour> activeRepoEntities;
        protected List<MonoBehaviour> allRepoEntities;
        protected BlocksRepo repo;

        protected IEnumerable<BlockAsset> activeBlockAssets => activeRepoEntities.Where((e) => e is BlockAsset).Cast<BlockAsset>();
        protected IEnumerable<BlockAsset> allBlockAssets => allRepoEntities.Where((e) => e is BlockAsset).Cast<BlockAsset>();

        private bool isInitialized;
        private const float cancelThreshold = 0.15f;
        private double cancelStart;
        protected bool shiftHold;
        protected bool cancelDown;

        private Camera camera;
        private Plane[] planes;

        protected static Color BlockSideNormalColor = Color.white * 0.35f;
        protected static Color BlockSideHoverColor = Color.white * 0.7f;
        protected static Color BlockSideActiveColor = Color.white * 0.7f;

        abstract protected void Initialize();
        abstract protected void Update();
        abstract protected void SceneGUI();
        abstract protected void InspectorGUI();

        #region Callback

        protected virtual void OnEnable()
        {
            handleRes = new HandleResources();
            settingsEditor = CreateEditor(RepoEntitiesEditorSettings.GetSettings());
            settingsSO = new RepoEntitiesEditorSettingsSO(RepoEntitiesEditorSettings.GetSettings());

            activeConnections = new List<Connection>();

            SceneView.beforeSceneGui += BeforeSceneGUI;
        }

        protected virtual void OnDisable()
        {
            settingsSO.Dispose();
            handleRes.Dispose();
            DestroyImmediate(settingsEditor);

            SceneView.beforeSceneGui -= BeforeSceneGUI;
        }

        private void BeforeSceneGUI(SceneView scene)
        {
            var ec = Event.current;

            cancelDown = false;

            if (ec.type == EventType.MouseDown && ec.button == 1)
                cancelStart = EditorApplication.timeSinceStartup;
            else if (ec.type == EventType.MouseUp && ec.button == 1)
                if ((float)(EditorApplication.timeSinceStartup - cancelStart) < cancelThreshold)
                    cancelDown = true;

            if (ec.type == EventType.KeyDown && ec.shift)
                shiftHold = true;
            else if (ec.type == EventType.KeyUp && !ec.shift)
                shiftHold = false;
        }

        public sealed override void OnInspectorGUI()
        {
            UpdateReferencesAndIntialize();

            if (repo == null)
            {
                EditorGUILayout.HelpBox("block asset need to be nested " +
                    "under a BlockRepo in the hierarchy!", MessageType.Error);
                return;
            }

            settingsToggle = EditorGUILayout.BeginFoldoutHeaderGroup(settingsToggle, "Settings");

            if (settingsToggle)
            {
                EditorGUI.BeginChangeCheck();

                GUILayout.BeginVertical(GUI.skin.box);
                settingsEditor.OnInspectorGUI();
                GUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    settingsSO.Update();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            InspectorGUI();
        }

        protected void OnSceneGUI()
        {
            UpdateReferencesAndIntialize();

            if (repo == null)
                return;

            UpdateCameraParams();
            SceneGUI();
        }

        #endregion

        private void UpdateReferencesAndIntialize()
        {
            repo = ((Component)target).GetComponentInParent<BlocksRepo>();

            if (repo != null)
            {
                if (!isInitialized)
                {
                    GetRepoEntities();
                    IntegrityCheck();
                    Initialize();
                    isInitialized = true;
                }

                Update();
            }
        }
        private void UpdateCameraParams()
        {
            camera = SceneView.lastActiveSceneView.camera;
            planes = GeometryUtility.CalculateFrustumPlanes(camera);
        }
        protected void GetRepoEntities()
        {
            var allTransforms = repo.GetComponentsInChildren<Transform>(true);
            var allBlockAssets = allTransforms.Select((t) => t.GetComponent<BlockAsset>()).Where((asset) => asset != null);
            var allBigBlockAssets = allTransforms.Select((t) => t.GetComponent<BigBlockAsset>()).Where((asset) => asset != null);

            allRepoEntities = new List<MonoBehaviour>(allBlockAssets.Cast<MonoBehaviour>().Concat(allBigBlockAssets));
            activeRepoEntities = new List<MonoBehaviour>(allRepoEntities.Where((e) => e.gameObject.activeInHierarchy));
        }
        protected void GenerateConnections(List<Connection> connections)
        {
            if (repo == null)
                return;

            //from block assets
            var blocks = AssetsBlocksIt(activeBlockAssets).Select((item) => item.Item1).Where((block) => block.bigBlock == null);
            //from big block assets
            blocks = blocks.Concat(AssetsBlocksIt(activeRepoEntities.Where((asset => asset is BigBlockAsset))).Select((item) => item.Item1));

            //sort by the closeset
            var targetPos = ((MonoBehaviour)target).transform.position;
            blocks = blocks.OrderBy((block) => Vector3.Distance(GetBlockPosition(block), targetPos));

            connections.Clear();
            ConnectionsUtility.GetConnectionsList(blocks, connections);
        }
        protected void IntegrityCheck()
        {
            foreach (var entity in allRepoEntities)
            {
                if (entity is BlockAsset)
                    IntegrityCheck((BlockAsset)entity);
                else if (entity is BigBlockAsset)
                    IntegrityCheck((BigBlockAsset)entity);
            }
        }
        protected void IntegrityCheck(BlockAsset blockAsset)
        {
            if (blockAsset.variants.Count == 0)
                using (var asset = new BlockAssetEditor.SO(blockAsset))
                {
                    asset.variants.Add(new BlockAsset.VariantDesc()
                    {
                        fill = GenerateFill(asset.target.gameObject),
                        sideIds = new SideIds()
                    });
                    asset.ApplyField(nameof(BlockAssetEditor.SO.variants));
                }
        }
        protected void IntegrityCheck(BigBlockAsset blockAsset)
        {
            using (var so = new BigBlockAssetEditor.SO(blockAsset))
            {
                bool apply = false;
                foreach (var index in SpatialUtil.Enumerate(so.data.Size))
                {
                    var block = so.data[index];
                    if (block.blockAsset != null)
                        if (block.VariantIndex >= block.blockAsset.variants.Count)
                        {
                            so.data[index] = default;
                            apply = true;
                        }
                }
                if (apply)
                    so.ApplyField(nameof(BigBlockAssetEditor.SO.data));
            }
        }
        private int GenerateFill(GameObject gameObject)
        {
            Mesh mesh = BlockUtility.GetMesh(gameObject);

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

        protected void DrawAssetsBlocks(IEnumerable<MonoBehaviour> targets)
        {
            foreach (var asset in targets)
            {
                if (asset is BlockAsset)
                    foreach (var blockItem in AssetBlocksIt((BlockAsset)asset).Where((b) => b.Item1.VariantIndex != 0))
                        BlockDC(blockItem.Item1).Move(blockItem.Item2).Draw();
                else if (asset is BigBlockAsset)
                    foreach (var blockItem in AssetBlocksIt((BigBlockAsset)asset))
                        BlockDC(blockItem.Item1).Move(blockItem.Item2).Draw();
            }
        }
        protected DrawCommand BlockDC(IBlock block)
        {
            var material = BlockUtility.GetMaterial(block.gameObject);
            var mesh = block.baseMesh;

            if (material != null)
            {
                var mainTex = material.GetTexture("_MainTex");
                handleRes.VariantMat.SetTexture("_MainTex", mainTex);
            }

            var cmd = GetDrawCmd().SetMesh(mesh).SetMaterial(handleRes.VariantMat);
            int revert = 0; int flips = 0;

            for (int k = 0; k < block.actions.Count; k++)
            {
                var ac = block.actions[k];
                var pivot = Vector3.one * 0.5f;
                switch (ac)
                {
                    case BlockAction.RotateX:
                        cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.right));
                        break;
                    case BlockAction.RotateY:
                        cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.up));
                        break;
                    case BlockAction.RotateZ:
                        cmd.RotateAround(pivot, Quaternion.AngleAxis(90f, Vector3.forward));
                        break;
                    case BlockAction.MirrorX:
                        cmd = cmd.Move(-pivot).Scale(new Vector3(-1, 1, 1)).Move(pivot);
                        revert++;
                        break;
                    case BlockAction.MirrorY:
                        cmd = cmd.Move(-pivot).Scale(new Vector3(1, -1, 1)).Move(pivot);
                        revert++;
                        break;
                    case BlockAction.MirrorZ:
                        cmd = cmd.Move(-pivot).Scale(new Vector3(1, 1, -1)).Move(pivot);
                        revert++;
                        break;
                    case BlockAction.Flip:
                        revert++;
                        flips++;
                        break;
                }
            }

            handleRes.VariantMat.SetFloat("_NormalMulti", flips % 2 > 0 ? -1 : 1);
            if (revert % 2 > 0)
                handleRes.VariantMat.SetFloat("_Cull", 1);
            else
                handleRes.VariantMat.SetFloat("_Cull", 2);

            return cmd;
        }

        protected void DrawAssetsBlocksSides(IEnumerable<MonoBehaviour> targets)
        {
            foreach (var sideItem in AssetsBlocksSidesIt(targets))
            {
                var d = sideItem.Item1.d;
                BlockSideDC(GetSideCenter(sideItem.Item2, d), directions[d], 1f).
                SetColor(GetColor(sideItem.Item1.id) * BlockSideNormalColor).Draw();
            }
        }
        protected void DrawAssetsButtonsSides(IEnumerable<MonoBehaviour> targets)
        {
            foreach (var target in targets)
            {
                if (target is BlockAsset)
                {
                    foreach (var blockItem in AssetBlocksIt((BlockAsset)target))
                        if (BlockButton(blockItem.Item1, blockItem.Item2))
                        {
                            Selection.activeGameObject = blockItem.Item1.gameObject;
                            initializeCommand.Set(blockItem.Item1.VariantIndex.ToString());
                        }
                }
                else if (target is BigBlockAsset)
                {
                    var bigBlock = (BigBlockAsset)target;
                    foreach (var sideItem in BigBlockSidesIt(bigBlock))
                    {
                        var d = sideItem.Item1.d;
                        BlockSideDC(GetSideCenter(sideItem.Item2, d), directions[d], 1f).
                        SetColor(GetColor(sideItem.Item1.id) * BlockSideNormalColor).Draw();
                    }

                    if (!GeometryUtility.TestPlanesAABB(planes, GetBigBlockBounds(bigBlock)))
                        continue;

                    var buttonDC = GetDrawCmd().SetPrimitiveMesh(PrimitiveType.Cube).SetMaterial(MaterialType.UI).
                        Scale(bigBlock.data.Size).Move(bigBlock.transform.position + ((Vector3)bigBlock.data.Size) * 0.5f);

                    Button.SetAll(buttonDC);
                    Button.normal.SetColor(new Color());
                    Button.hover.SetColor(BlockSideHoverColor);
                    Button.active.SetColor(BlockSideActiveColor);
                    if (Button.Draw<CubeD>())
                        Selection.activeGameObject = bigBlock.gameObject;
                }
            }
        }

        protected IEnumerable<(BlockSide, Vector3)> AssetsBlocksSidesIt(IEnumerable<MonoBehaviour> targets)
        {
            foreach (var asset in targets)
            {
                if (asset is BlockAsset)
                    foreach (var blockItem in AssetBlocksIt((BlockAsset)asset))
                        foreach (var sideItem in BlockSidesIt(blockItem.Item1, blockItem.Item2))
                            yield return sideItem;
                else if (asset is BigBlockAsset)
                    foreach (var item in BigBlockSidesIt((BigBlockAsset)asset))
                        yield return item;
            }
        }
        protected IEnumerable<(AssetBlock, Vector3)> AssetsBlocksIt(IEnumerable<MonoBehaviour> targets)
        {
            foreach (var asset in targets)
            {
                if (asset is BlockAsset)
                    foreach (var item in AssetBlocksIt((BlockAsset)asset))
                        yield return item;
                else if (asset is BigBlockAsset)
                    foreach (var item in AssetBlocksIt((BigBlockAsset)asset))
                        yield return item;
            }
        }
        protected IEnumerable<(AssetBlock, Vector3)> AssetBlocksIt(BigBlockAsset bigBlock)
        {
            foreach (var index in SpatialUtil.Enumerate(bigBlock.data.Size))
            {
                var block = bigBlock.data[index];
                if (block.blockAsset != null)
                    yield return (block, GetPositionFromBigBlockAsset(block));
            }
        }
        protected IEnumerable<(AssetBlock, Vector3)> AssetBlocksIt(BlockAsset blockAsset)
        {
            for (int j = 0; j < blockAsset.variants.Count; j++)
            {
                if (!settings.DrawVariants && j > 0)
                    break;

                var block = new AssetBlock(j, blockAsset);
                yield return (block, GetPositionFromBlockAsset(block));
            }
        }
        protected IEnumerable<(BlockSide, Vector3)> BigBlockSidesIt(BigBlockAsset bigBlock)
        {
            BoundsInt bounds = new BoundsInt()
            { min = Vector3Int.zero, max = bigBlock.data.Size };

            foreach (var index in SpatialUtil.Enumerate(bigBlock.data.Size))
            {
                var block = bigBlock.data[index];
                if (block.blockAsset == null)
                    continue;

                var pos = GetPositionFromBigBlockAsset(block);
                for (int d = 0; d < 6; d++)
                {
                    var n = index + delta[d];
                    if (SideCullTest(pos, d) && (!bounds.Contains(n) || bigBlock.data[n].blockAsset == null))
                        yield return (new BlockSide(block, d), pos);
                }
            }

        }
        protected IEnumerable<(BlockSide, Vector3)> BlockSidesIt(AssetBlock block, Vector3 blockPosition)
        {
            for (int d = 0; d < 6; d++)
                if (SideCullTest(blockPosition, d))
                    yield return (new BlockSide(block, d), blockPosition);
        }

        protected bool BlockButton(AssetBlock block, Vector3 position)
        {
            position += Vector3.one * 0.5f;
            if (!CubeFrustumTest(position))
                return false;

            DrawCommand dcmd = BlockSidesDC(block, position);
            Button.SetAll(dcmd);
            Button.normal.SetColor(BlockSideNormalColor);
            Button.hover.SetColor(BlockSideHoverColor);
            Button.active.SetColor(BlockSideActiveColor);
            return Button.Draw<CubeD>();
        }
        protected bool BlockSideButton(BlockSide blockSide, float size, float alpha = 1f) => BlockSideButton(blockSide, GetPositionFromBlockAsset(blockSide.block), size, alpha);
        protected bool BlockSideButton(BlockSide blockSide, Vector3 position, float size, float alpha = 1f)
        {
            var center = GetSideCenter(position, blockSide.d);
            var normal = directions[blockSide.d];

            if (!PlaneFrustumTest(center, normal))
                return false;

            var draw = BlockSideDC(center, normal, size).SetColor(GetColor(blockSide.id));
            Button.SetAll(draw);
            Button.normal.color *= BlockSideNormalColor * alpha;
            Button.hover.color *= BlockSideHoverColor * alpha;
            Button.active.color *= BlockSideActiveColor * alpha;
            return Button.Draw<QuadD>();
        }

        protected DrawCommand BlockSidesDC(IBlock block, Vector3 position)
        {
            for (int d = 0; d < 6; d++)
                handleRes.SetCubeColorMatSide(GetColor(block.baseIds[d]), d);

            var dcmd = GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(handleRes.ColorCubeMat).
                Scale(1f).
                Move(position);
            return dcmd;
        }
        protected DrawCommand BlockSideDC(Vector3 center, Vector3 normal, float size)
        {
            return GetDrawCmd().Scale(size).LookAt(-normal).Move(center);
        }

        protected void DrawConnections(List<Connection> connections)
        {
            var count = Mathf.Min(settings.MaxConnectionsDrawCount, connections.Count);
            for (int i = 0; i < count; i++)
                DrawConnection(connections[i]);
        }
        protected void DrawConnection(Connection con)
        {
            if (con.a == con.b)
            {
                if (settings.DrawSelfConnections)
                    Draw(con, Color.cyan);
            }
            else
                Draw(con, GetColor(con.a.baseIds[con.d]));
        }
        protected void Draw(Connection con, Color color)
        {
            var s = GetBlockPosition(con.a) + origins[con.d];
            var st = directions[con.d];
            var e = GetBlockPosition(con.b) + origins[opposite[con.d]];
            var et = directions[opposite[con.d]];
            Handles.DrawBezier(s, e, s + st, e + et, color, null, 2f);
        }

        protected void DoSideConnection(BlockSide src, Vector3 position, System.Action OnConnect)
        {
            SceneView.RepaintAll();

            Handles.DrawDottedLine(
                GetSideCenter(position, src.d),
                HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f)
                , 1f);

            if (shiftHold)
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                float mdist = float.MaxValue;
                bool partOfBigBlock = false;
                AssetBlock targetBlock = default;
                BigBlockAsset bigBlockAsset = null;

                foreach(var entity in activeRepoEntities)
                {
                    if(entity is BlockAsset)
                    {
                        foreach(var blockItem in AssetBlocksIt((BlockAsset)entity))
                        {
                            var bounds = new Bounds(blockItem.Item2 + Vector3.one * 0.5f, Vector3.one);
                            if (bounds.IntersectRay(ray, out var t))
                                if (t < mdist)
                                {
                                    mdist = t;
                                    partOfBigBlock = false;
                                    targetBlock = blockItem.Item1;
                                }
                        }
                    }
                    else if(entity is BigBlockAsset)
                    {
                        var bounds = GetBigBlockBounds((BigBlockAsset)entity);
                        if (bounds.IntersectRay(ray, out var t))
                            if (t < mdist)
                            {
                                mdist = t;
                                partOfBigBlock = true;
                                bigBlockAsset = (BigBlockAsset)entity;
                            }
                    }
                }

                if(!partOfBigBlock)
                {
                    if (targetBlock.blockAsset != null)
                        foreach (var sideItem in BlockSidesIt(targetBlock, GetPositionFromBlockAsset(targetBlock)))
                        {
                            if (BlockSideButton(sideItem.Item1, sideItem.Item2, 0.9f))
                            {
                                MakeConnection(src, sideItem.Item1);
                                OnConnect?.Invoke();
                            }
                        }
                }else
                {
                    foreach(var sideItem in BigBlockSidesIt(bigBlockAsset))
                    {
                        if (BlockSideButton(sideItem.Item1, sideItem.Item2, 0.9f))
                        {
                            MakeConnection(src, sideItem.Item1);
                            OnConnect?.Invoke();
                        }
                    }
                }

            }
            else
            {
                var od = opposite[src.d];
                foreach (var sideItem in AssetsBlocksSidesIt(activeRepoEntities))
                {
                    var side = sideItem.Item1;
                    if (side.d == od && GetSide(side.block.fill, od) == GetSide(src.block.fill, src.d))
                        if (BlockSideButton(side, sideItem.Item2, 0.9f))
                        {
                            MakeConnection(src, side);
                            OnConnect?.Invoke();
                        }
                }

            }
        }
        protected void MakeConnection(BlockSide src, BlockSide dst)
        {
            List<(BlockSide, int)> writeOps = new List<(BlockSide, int)>();

            var allBlocks = BlockAsset.GetBlocksEnum(allBlockAssets);
            var hashes = ConnectionsUtility.GetListOfSortedIds(allBlocks);
            var allConnections = new List<Connection>();
            ConnectionsUtility.GetConnectionsList(allBlocks, allConnections);

            var aId = src.id;
            var bId = dst.id;
            bool isAConnected = aId != 0 && allConnections.FindIndex((con) => con.a[con.d] == src.compsiteId) > -1;
            bool isBConnected = bId != 0 && allConnections.FindIndex((con) => con.a[con.d] == dst.compsiteId) > -1;

            if (!isAConnected && !isBConnected)
            {
                var next = ConnectionsUtility.GetNextId(hashes);
                writeOps.Add((src, next));
                writeOps.Add((dst, next));
            }
            else if (!isAConnected)
            {
                writeOps.Add((src, bId));
            }
            else if (!isBConnected)
            {
                writeOps.Add((dst, aId));
            }
            else
            {
                foreach (var block in allBlocks)
                {
                    if (block.baseIds[src.d] == bId)
                        writeOps.Add((new BlockSide(block, src.d), aId));
                    if (block.baseIds[dst.d] == bId)
                        writeOps.Add((new BlockSide(block, dst.d), aId));
                }
            }

            for (int i = 0; i < writeOps.Count; i++)
            {
                var op = writeOps[i];
                var block = (AssetBlock)op.Item1.block;
                var so = new BlockAssetEditor.SO(block.blockAsset);
                var variant = so.variants[block.VariantIndex];
                variant.sideIds[op.Item1.d] = op.Item2;
                so.variants[block.VariantIndex] = variant;
                so.Apply();
                so.Dispose();
            }
            Repaint();
        }

        protected Color GetColor(int id)
        {
            return ColorUtility.GetColor(new XXHash().Append(id));
        }
        protected Vector3 GetBlockPosition(IBlock block)
        {
            if (block.bigBlock != null)
                return GetPositionFromBigBlockAsset(block);
            else
                return GetPositionFromBlockAsset(block);
        }
        protected Vector3 GetPositionFromBigBlockAsset(IBlock block)
        {
            var assetBlock = (AssetBlock)block;
            if (block is AssetBlock)
                return GetIndexInBigBlock(assetBlock) + assetBlock.bigBlock.transform.position;
            else
                return block.transform.position;
        }
        protected Vector3 GetPositionFromBlockAsset(IBlock block)
        {
            if (block is AssetBlock)
            {
                var assetBlock = (AssetBlock)block;
                return block.transform.position + assetBlock.Variant.position_editor_only;
            }
            else
                return block.transform.position;
        }
        protected Vector3Int GetIndexInBigBlock(AssetBlock block)
        {
            var data = block.bigBlock.data;
            foreach (var index in SpatialUtil.Enumerate(data.Size))
            {
                if (data[index] == block)
                    return index;
            }
            throw new System.Exception($"can't find the block in it's big block! {block.blockAsset}");
        }
        protected bool SideCullTest(Vector3 pos, int side)
        {
            var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
            var c = vp.MultiplyPoint(GetSideCenter(pos, side));
            var u = vp.MultiplyPoint(GetSideU(pos, side));
            var v = vp.MultiplyPoint(GetSideV(pos, side));
            Vector3 clipSpaceNormal = Vector3.Cross((u - c), (v - c));
            return clipSpaceNormal.z >= 0;
        }
        protected bool CubeFrustumTest(Vector3 pos)
        {
            Bounds b = new Bounds() { size = Vector3.one, center = pos };
            return GeometryUtility.TestPlanesAABB(planes, b);
        }
        protected bool PlaneFrustumTest(Vector3 pos, Vector3 normal)
        {
            Bounds b = new Bounds() { center = pos, size = -0.95f * MathUtility.Abs(normal) + Vector3.one };
            return GeometryUtility.TestPlanesAABB(planes, b);
        }
        protected Bounds GetBigBlockBounds(BigBlockAsset bigBlockAsset)
        {
            var size = (Vector3)bigBlockAsset.data.Size;
            return new Bounds(bigBlockAsset.transform.position + size * 0.5f, size);
        }
    }

}