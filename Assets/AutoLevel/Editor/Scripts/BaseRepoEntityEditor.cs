using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using AlaslTools;

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

        protected static InitializeCommand initializeCommand = new InitializeCommand();

        protected HandleResources handleRes;

        private bool        settingsToggle;
        protected Editor    settingsEditor;
        protected RepoEntitiesEditorSettings.Settings   settings => settingsSO.settings;
        protected RepoEntitiesEditorSettingsSO          settingsSO;

        protected List<Connection>      activeConnections;
        protected List<MonoBehaviour>   activeRepoEntities;
        protected List<MonoBehaviour>   allRepoEntities;
        protected BlocksRepoSO          repo;

        protected IEnumerable<BlockAsset> activeBlockAssets => activeRepoEntities.Where((e) => e is BlockAsset).Cast<BlockAsset>();
        protected IEnumerable<BlockAsset> allBlockAssets => allRepoEntities.Where((e) => e is BlockAsset).Cast<BlockAsset>();

        private bool        isInitialized;
        private const float cancelThreshold = 0.15f;
        private double      cancelStart;
        protected bool      cancelDown;

        protected bool      exclusiveConnectionKey;
        protected bool      bannConnectionKey;
        protected bool      additonalConnectionKey;

        private Camera camera;
        private Plane[] planes;

        protected static Color BlockSideNormalColor = Color.white * 0.35f;
        protected static Color BlockSideHoverColor = Color.white * 0.7f;
        protected static Color BlockSideActiveColor = Color.white * 0.7f;

        private List<GameObject>            assetBlocksGO = new List<GameObject>();
        private Dictionary<int, Material>   assetBlocksMat = new Dictionary<int, Material>();

        protected string[] allGroups;
        protected string[] allWeightGroups;

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
            repo.Dispose();

            settingsSO.Dispose();
            handleRes.Dispose();
            DestroyImmediate(settingsEditor);

            foreach (var pair in assetBlocksMat)
                DestroyImmediate(pair.Value, false);
            assetBlocksMat.Clear();

            ClearAssetsBlocks();

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

            if (ec.isKey && ec.type == EventType.KeyDown && ec.keyCode == settings.banConnectionKey)
                bannConnectionKey = true;
            if (ec.isKey && ec.type == EventType.KeyUp && ec.keyCode == settings.banConnectionKey)
                bannConnectionKey = false;

            if (ec.isKey && ec.type == EventType.KeyDown && ec.keyCode == settings.exclusiveConnectionKey)
                exclusiveConnectionKey = true;
            if (ec.isKey && ec.type == EventType.KeyUp && ec.keyCode == settings.exclusiveConnectionKey)
                exclusiveConnectionKey = false;
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

        #region Initialize

        private void UpdateReferencesAndIntialize()
        {
            var preRepoComp = repo != null ? repo.target : null;
            var repoComp = ((Component)target).GetComponentInParent<BlocksRepo>();

            if (repoComp != null)
            {
                if (!isInitialized || preRepoComp != repoComp)
                {
                    repo = new BlocksRepoSO(repoComp);
                    BlocksRepoSO.GetRepoEntities(repo.target ,out allRepoEntities,out activeRepoEntities);
                    BlocksRepoSO.ChildrenIntegrityCheck(repo.target);
                    InitGroups();
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

        protected void GenerateConnections(List<Connection> connections)
        {
            if (repo == null)
                return;

            var blocks = GetBlocksIt(AssetType.AllBlockAssetAndBigBlockAsset).ToList();

            //sort by the closest
            var targetPos = ((MonoBehaviour)target).transform.position;
            blocks.Sort((a,b) => Vector3.Distance(a.Item2, targetPos).CompareTo(
                Vector3.Distance(b.Item2, targetPos)));

            connections.Clear();
            foreach(var conn in ConnectionsUtility.GetConnectionsList(
                repo.useFilling ? blocks.Select((block) => block.Item1.compositeIds) :
                blocks.Select((block) => block.Item1.baseIds)))
            {
                connections.Add(new Connection(blocks[conn.Item1].Item1, blocks[conn.Item2].Item1, conn.Item3));
            }

            int index = 0;
            for (int i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn.a.id != 0)
                    connections.Swap(index++,i);
            }

        }

        private void InitGroups()
        {
            var repoGroups = repo.target.GetAllGroupsNames();
            allGroups = new string[repoGroups.Count - 2];
            for (int i = 2; i < repoGroups.Count; i++)
                allGroups[i - 2] = repoGroups[i];

            repoGroups = repo.target.GetAllWeightGroupsNames();
            allWeightGroups = new string[repoGroups.Count - 2];
            for (int i = 2; i < repoGroups.Count; i++)
                allWeightGroups[i - 2] = repoGroups[i];
        }

        #endregion

        protected int GetGroupIndex(int group) => System.Array.FindIndex(allGroups, (g) => g.GetHashCode() == group);
        protected int GetWeightGroupIndex(int weightGroup) => System.Array.FindIndex(allWeightGroups, (g) => g.GetHashCode() == weightGroup);
        protected void ShowConnectionsTutorial()
        {
            if (!settings.ShowedConnectionTutorial)
            {
                SceneView.lastActiveSceneView.ShowNotification(
                        new GUIContent($"Hold the {settings.banConnectionKey} key to ban connection\n" +
                        $"Hold the {settings.exclusiveConnectionKey} key to make an exclusive connection\n" +
                        $"Hold the Shift key to select any face\n " +
                        $"Use right click to cancel"), 8f);
                settings.ShowedConnectionTutorial = true;
                settingsSO.Apply();
            }
        }

        #region Queries

        protected enum AssetType 
        {
            /// <summary>
            /// the first variant from each block asset
            /// </summary>
            BlockAssetFirst,
            /// <summary>
            /// all the variants from each block asset
            /// </summary>
            BlockAsset ,
            /// <summary>
            /// the first asset block from each cell of each big block
            /// </summary>
            BigBlockAssetFirst  ,
            /// <summary>
            /// all the variants from each block asset which not a part of the big block,
            /// and all the asset blocks in each cell of each big block
            /// </summary>
            AllBlockAssetAndBigBlockAsset
        }

        protected IEnumerable<(AssetBlock, Vector3)> GetBlocksIt(AssetType assetType, bool includeInActive = false)
        {
            var assets = includeInActive ? allRepoEntities : activeRepoEntities;
            foreach(var asset in assets)
            {
                var bigBlock = asset is BigBlockAsset;

                switch (assetType)
                {
                    case AssetType.BlockAssetFirst:
                        if (!bigBlock)
                        {
                            var blockAsset = (BlockAsset)asset;
                            if (blockAsset.variants.Count > 0)
                            {
                                var block = new AssetBlock(0, blockAsset);
                                yield return (block, GetPositionFromBlockAsset(block));
                            }
                        }
                        break;
                    case AssetType.BlockAsset:
                        if (!bigBlock)
                        {
                            foreach (var block in AssetBlocksIt((BlockAsset)asset))
                                yield return block;
                        }
                        break;
                    case AssetType.BigBlockAssetFirst:
                        if (bigBlock)
                        {
                            foreach (var block in AssetBlocksFirstIt((BigBlockAsset)asset))
                                yield return block;
                        }
                        break;
                    case AssetType.AllBlockAssetAndBigBlockAsset:
                        if (bigBlock)
                        {
                            foreach (var block in AssetBlocksAllIt((BigBlockAsset)asset))
                                yield return block;
                        }
                        else
                        {
                            foreach (var block in AssetBlocksIt((BlockAsset)asset))
                                if (block.Item1.bigBlock == null)
                                    yield return block;
                        }
                        break;
                }
            }
        }
        private IEnumerable<(AssetBlock, Vector3)> AssetBlocksFirstIt(BigBlockAsset bigBlock)
        {
            foreach (var index in SpatialUtil.Enumerate(bigBlock.data.Size))
            {
                var list = bigBlock.data[index];
                if (!list.IsEmpty)
                    yield return (list[0], GetPositionFromBigBlockAsset(list[0]));
            }
        }
        private IEnumerable<(AssetBlock, Vector3)> AssetBlocksAllIt(BigBlockAsset bigBlock)
        {
            foreach (var index in SpatialUtil.Enumerate(bigBlock.data.Size))
            {
                var list = bigBlock.data[index];
                for (int i = 0; i < list.Count; i++)
                {
                    var block = list[i];
                    yield return (block, GetPositionFromBigBlockAsset(block));
                }
            }
        }
        protected IEnumerable<(AssetBlock, Vector3)> AssetBlocksIt(BlockAsset blockAsset)
        {
            for (int j = 0; j < blockAsset.variants.Count; j++)
            {
                var block = new AssetBlock(j, blockAsset);
                yield return (block, GetPositionFromBlockAsset(block));
            }
        }

        /// <summary>
        /// iterate the external sides with back culling
        /// </summary>
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
        /// <summary>
        /// iterate the external sides with back culling
        /// </summary>
        protected IEnumerable<(BlockSide, Vector3)> BlockSidesIt(AssetBlock block, Vector3 blockPosition)
        {
            for (int d = 0; d < 6; d++)
                if (BackCulling(blockPosition, d))
                    yield return (new BlockSide(block, d), blockPosition);
        }
        /// <summary>
        /// iterate the external sides with back culling
        /// </summary>
        protected IEnumerable<(BlockSide, Vector3)> BigBlockSidesIt(BigBlockAsset bigBlock)
        {
            BoundsInt bounds = new BoundsInt()
            { min = Vector3Int.zero, max = bigBlock.data.Size };

            foreach (var index in SpatialUtil.Enumerate(bigBlock.data.Size))
            {
                if (bigBlock.data[index].IsEmpty) continue;
                var block = bigBlock.data[index][0];
                var pos = GetPositionFromBigBlockAsset(block);
                foreach (var d in BigBlockExtSideIt(bigBlock,index))
                {
                    if (BackCulling(pos, d))
                        yield return (new BlockSide(block, d), pos);
                }
            }

        }
        protected IEnumerable<int> BigBlockExtSideIt(BigBlockAsset bigBlock,Vector3Int index)
        {
            BoundsInt bounds = new BoundsInt() { min = Vector3Int.zero, max = bigBlock.data.Size };
            for (int d = 0; d < 6; d++)
            {
                var n = index + delta[d];
                if (!bounds.Contains(n) || bigBlock.data[n].IsEmpty)
                    yield return d;
            }
        }

        protected IEnumerable<Connection> GetBannedConnectionsIt(AssetBlock block)
        {
            foreach(var con in repo.bannedConnections)
                if(con.Contain(block))
                    yield return con;
        }

        protected IEnumerable<Connection> GetExclusiveConnectionsIt(AssetBlock block)
        {
            foreach (var con in repo.exclusiveConnections)
                if (con.a.block == block)
                    yield return con;
        }

        #endregion

        #region Drawing

        protected void DrawConnections()
        {
            DrawDefaultConnections();
            DrawBannedConnections();
            DrawExclusiveConnections();
        }
        protected void DrawDefaultConnections()
        {
            var count = Mathf.Min(settings.MaxConnectionsDrawCount, activeConnections.Count);
            for (int i = 0; i < count; i++)
                DrawDefaultConnection(activeConnections[i]);
        }
        protected void DrawBannedConnections()
        {
            foreach (var conn in repo.bannedConnections)
                if(BlockUtility.IsActive(conn.a.block) && BlockUtility.IsActive(conn.b.block))
                    DrawConnection(conn, Color.black, 8f);
        }
        protected void DrawExclusiveConnections()
        {
            foreach (var conn in repo.exclusiveConnections)
                if (BlockUtility.IsActive(conn.a.block) && BlockUtility.IsActive(conn.b.block))
                {
                    var center = GetBlockPosition(conn.a.block) + origins[conn.a.d];
                    GetDrawCmd().SetPrimitiveMesh(PrimitiveType.Sphere).SetColor(Color.white).Scale(0.1f).Move(center).Draw();

                    DrawConnection(conn, Color.white, 8f);
                }
        }
        protected void DrawDefaultConnection(Connection con)
        {
            if (con.a.block == con.b.block)
            {
                if (settings.DrawSelfConnections)
                    DrawConnection(con, Color.cyan, 4f);
            }
            else
                DrawConnection(con, GetColor(con.a.id), con.a.id == 0 ? 2f : 4f);
        }

        protected void DrawBlockToBigBlockConnection(AssetBlock block)
        {
            DrawBlockOutlineConnection(
                GetPositionFromBigBlockAsset(block),
                GetPositionFromBlockAsset(block),
                NiceColors.Saffron, block.bigBlock.gameObject.activeInHierarchy);
        }

        // draw variants and big blocks
        protected void DrawAssetsBlocks()
        {
            var blocks = GetBlocksIt(AssetType.BlockAsset).
                Where((block) => block.Item1.VariantIndex != 0).
                Concat(GetBlocksIt(AssetType.BigBlockAssetFirst));

            if (assetBlocksGO.Count != blocks.Count())
            {
                ClearAssetsBlocks();

                foreach (var block in blocks)
                {
                    var go = Instantiate(block.Item1.gameObject);

                    go.name = block.Item1.gameObject.name;
                    go.hideFlags = HideFlags.HideAndDontSave;

                    go.RemoveComponent<BlockAsset>();
                    go.transform.Reset();

                    foreach (var action in block.Item1.actions)
                        ActionsUtility.ApplyAction(go, action);

                    var p = new GameObject(go.name);
                    p.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(p.transform, true);

                    assetBlocksGO.Add(p);
                }
            }
            else
            {
                int i = 0;
                foreach (var block in blocks)
                    assetBlocksGO[i++].transform.position = block.Item2;
            }
        }
        private void ClearAssetsBlocks()
        {
            for (int i = 0; i < assetBlocksGO.Count; i++)
                GameObjectUtil.SafeDestroy(assetBlocksGO[i]);
            assetBlocksGO.Clear();
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

        #endregion

        #region Controls

        protected void DoBlocksSelectionButtons(IEnumerable<MonoBehaviour> targets, int layer = 0 /* ignore any layer beneath it */)
        {
            foreach (var target in targets)
            {
                if (target is BlockAsset)
                {
                    foreach (var blockItem in AssetBlocksIt((BlockAsset)target))
                        if (blockItem.Item1.layerSettings.layer >= layer && DoBlockButton(blockItem.Item1, blockItem.Item2))
                        {
                            Selection.activeGameObject = blockItem.Item1.gameObject;
                            initializeCommand.Set(blockItem.Item1.VariantIndex.ToString());
                        }
                }
                else if (target is BigBlockAsset)
                {
                    var bigBlock = (BigBlockAsset)target;

                    if (!GeometryUtility.TestPlanesAABB(planes, GetBigBlockBounds(bigBlock)))
                        continue;

                    foreach (var sideItem in BigBlockSidesIt(bigBlock))
                    {
                        var d = sideItem.Item1.d;
                        BlockSideDC(GetSideCenter(sideItem.Item2, d), directions[d], 1f).
                        SetColor(GetColor(sideItem.Item1.id) * BlockSideNormalColor).Draw();
                    }

                    var buttonDC = GetDrawCmd().SetPrimitiveMesh(PrimitiveType.Cube).SetMaterial(MaterialType.UI).
                        Scale(bigBlock.data.Size).Move(bigBlock.transform.position + ((Vector3)bigBlock.data.Size) * 0.5f);

                    Button.SetAll(buttonDC);
                    Button.normal.SetColor(new Color());
                    Button.hover.SetColor(new Color().SetAlpha(BlockSideActiveColor.a));
                    Button.active.SetColor(BlockSideActiveColor);
                    if (Button.Draw<Cube3DD>())
                        Selection.activeGameObject = bigBlock.gameObject;
                }
            }
        }

        protected void DoSideConnection(BlockSide src, Vector3 position, System.Action OnConnect)
        {
            SceneView.RepaintAll();

            Handles.color = bannConnectionKey ? Color.black : exclusiveConnectionKey ? Color.white : Color.green;
            Handles.DrawAAPolyLine(8f,GetSideCenter(position, src.d),
                HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f));
            Handles.color = Color.white;

            if (Event.current.shift || bannConnectionKey || exclusiveConnectionKey)
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                float mdist = float.MaxValue;
                bool partOfBigBlock = false;
                AssetBlock targetBlock = default;
                BigBlockAsset bigBlockAsset = null;

                foreach (var entity in activeRepoEntities)
                {
                    if (entity is BlockAsset)
                    {
                        foreach (var blockItem in AssetBlocksIt((BlockAsset)entity))
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
                    else if (entity is BigBlockAsset)
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

                if (!partOfBigBlock)
                {
                    if (targetBlock.Valid)
                        foreach (var sideItem in BlockSidesIt(targetBlock, GetPositionFromBlockAsset(targetBlock)))
                        {
                            if (DoBlockSideButton(sideItem.Item1, sideItem.Item2, 0.9f))
                            {
                                HandleConnection(src, sideItem.Item1);
                                OnConnect?.Invoke();
                            }
                        }
                }
                else
                {
                    foreach (var sideItem in BigBlockSidesIt(bigBlockAsset))
                    {
                        if (DoBlockSideButton(sideItem.Item1, sideItem.Item2, 0.9f))
                        {
                            HandleConnection(src, sideItem.Item1);
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
                    if (side.d == od && (!repo.useFilling || GetSide(side.block.fill, od) == GetSide(src.block.fill, src.d)))
                        if (DoBlockSideButton(side, sideItem.Item2, 0.9f))
                        {
                            HandleConnection(src, side);
                            OnConnect?.Invoke();
                        }
                }

            }
        }
        private void HandleConnection(BlockSide src, BlockSide dst)
        {
            if (bannConnectionKey)
                BanConnection(src, dst);
            else if (exclusiveConnectionKey)
                ExclusiveConnection(src, dst);
            else
                MakeConnection(src, dst);
        }

        protected bool DoBlockDetachButton(AssetBlock block)
        {
            var bounds = new Bounds();
            bounds.min = GetPositionFromBlockAsset(block);
            bounds.max = bounds.min + Vector3.one;
            var screenAnch = new Vector2(SceneView.lastActiveSceneView.position.size.x, 0);
            var pos = BoundsUtility.ClosestCornerToScreenPoint(bounds, screenAnch);

            var cmd = GetDrawCmd()
                .SetColor(Color.white)
                .SetMaterial(MaterialType.UI)
                .SetTexture(handleRes.detatch_icon)
                .LookAtCamera()
                .Scale(0.25f)
                .Move(pos);

            Button.SetAll(cmd);
            Button.hover.SetColor(Color.yellow);
            Button.active.SetColor(Color.red);
            if (Button.Draw<QuadD>())
            {
                DetachFromBigBlock(block);
                return true;
            }
            return false;
        }

        protected bool DoBlockButton(AssetBlock block, Vector3 position)
        {
            position += Vector3.one * 0.5f;
            if (!CubeFrustumCulling(position))
                return false;

            DrawCommand dcmd = BlockSidesDC(block, position);
            Button.SetAll(dcmd);
            Button.normal.SetColor(BlockSideNormalColor);
            Button.hover.SetColor(BlockSideHoverColor);
            Button.active.SetColor(BlockSideActiveColor);
            return Button.Draw<Cube3DD>();
        }
        protected bool DoBlockSideButton(BlockSide blockSide, float size, float alpha = 1f) => DoBlockSideButton(blockSide, GetPositionFromBlockAsset(blockSide.block), size, alpha);
        protected bool DoBlockSideButton(BlockSide blockSide, Vector3 position, float size, float alpha = 1f)
        {
            var center = GetSideCenter(position, blockSide.d);
            var normal = directions[blockSide.d];

            if (!PlaneFrustumCulling(center, normal))
                return false;

            var draw = BlockSideDC(center, normal, size).SetColor(GetColor(blockSide.id));
            Button.SetAll(draw);
            Button.normal.color *= BlockSideNormalColor * alpha;
            Button.hover.color *= BlockSideHoverColor * alpha;
            Button.active.color *= BlockSideActiveColor * alpha;
            return Button.Draw<Quad3DD>();
        }

        #endregion

        #region DrawPrimitive

        protected void DrawConnection(Connection con, Color color, float thickness)
        {
            var s = GetBlockPosition(con.a.block) + origins[con.a.d];
            var st = directions[con.a.d];
            var e = GetBlockPosition(con.b.block) + origins[con.b.d];
            var et = directions[con.b.d];
            Handles.DrawBezier(s, e, s + st, e + et, color, null, thickness);
        }

        protected void DrawBlockOutlineConnection(Vector3 a, Vector3 b, Color color, bool drawLine = true)
        {
            b += Vector3.one * 0.5f;
            a += Vector3.one * 0.5f;

            //Draw the line
            if(drawLine)
            {
                Handles.color = color;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;
                Handles.DrawLine(a, b, 3);
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }

            //Draw the outline

            {
                GetDrawCmd().
                    SetPrimitiveMesh(PrimitiveType.Cube).
                    SetMaterial(OutLineMaterial).
                    Scale(0.95f).Move(b).Draw();

                GetDrawCmd().
                    SetPrimitiveMesh(PrimitiveType.Cube).
                    SetMaterial(OutLineMaterial).
                    SetColor(color).
                    Move(b).Draw(pass: 1);
            }
        }

        protected DrawCommand BlockSidesDC(IBlock block, Vector3 position)
        {
            for (int d = 0; d < 6; d++)
                handleRes.SetCubeColorMatSide(GetColor(block.baseIds[d]), d);

            var dcmd = GetDrawCmd().
                SetPrimitiveMesh(PrimitiveType.Cube).
                SetMaterial(handleRes.color_cube_mat).
                Scale(1f).
                Move(position);
            return dcmd;
        }
        protected DrawCommand BlockSideDC(Vector3 center, Vector3 normal, float size)
        {
            return GetDrawCmd().Scale(size).LookAt(-normal).Move(center);
        }

        #endregion

        #region BlockUtility
 
        protected void ExclusiveConnection(BlockSide src, BlockSide dst)
        {
            var conn = new Connection(src, dst);

            if (repo.exclusiveConnections.Contains(conn))
                RemoveExclusiveConnection(conn);
            else
                AddExclusiveConnection(conn);

            Repaint();
        }
        protected void RemoveExclusiveConnection(Connection conn)
        {
            repo.exclusiveConnections.Remove(conn);
            repo.ApplyField(nameof(BlocksRepoSO.exclusiveConnections));
        }
        protected void AddExclusiveConnection(Connection conn)
        {
            repo.exclusiveConnections.Add(conn);
            repo.ApplyField(nameof(BlocksRepoSO.exclusiveConnections));
        }
        protected void BanConnection(BlockSide src, BlockSide dst)
        {
            var conn = new Connection(src, dst);

            if (repo.bannedConnections.Contains(conn))
                RemoveBannnedConnection(conn);
            else
                AddBannedConnection(conn);

            Repaint();
        }
        protected void RemoveBannnedConnection(Connection conn)
        {
            repo.bannedConnections.Remove(conn);
            repo.ApplyField(nameof(BlocksRepoSO.bannedConnections));
        }
        protected void AddBannedConnection(Connection conn)
        {
            repo.bannedConnections.Add(conn);
            repo.ApplyField(nameof(BlocksRepoSO.bannedConnections));
        }
        protected void MakeConnection(BlockSide src, BlockSide dst)
        {
            List<(BlockSide, int)> writeOps = new List<(BlockSide, int)>();

            var allBlocks = BlockAsset.GetBlocksEnum(allBlockAssets);

            var aId = src.id;
            var bId = dst.id;
            bool isAConnected = IsConnected(src, allBlocks);
            bool isBConnected = IsConnected(dst, allBlocks); ;

            if (!isAConnected && !isBConnected)
            {
                var next = ConnectionsUtility.CreateIDGenerator(allBlocks).GetNext();
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
                    for (int d = 0; d < 6; d++)
                    {
                        if (block.baseIds[d] == bId)
                            writeOps.Add((new BlockSide(block, d), aId));
                    }
                }
            }

            var bigBlockWriteOp = new List<((BigBlockAsset, Vector3Int), int, int)>();

            for (int i = writeOps.Count - 1; i >= 0; i--)
            {
                var op = writeOps[i];
                var block = op.Item1.block;
                var d = op.Item1.d;

                if (block.bigBlock != null)
                {
                    var index = GetIndexInBigBlock((AssetBlock)block).Item1;
                    var bounds = new BoundsInt(Vector3Int.zero, block.bigBlock.data.Size);

                    //only for external side
                    if (!bounds.Contains(index + delta[d]))
                    {
                        //every big block side get one write
                        if (bigBlockWriteOp.FindIndex((item) =>
                        item.Item1.Item1 == block.bigBlock &&
                        item.Item1.Item2 == index && item.Item2 == d) < 0)
                        {
                            bigBlockWriteOp.Add(((block.bigBlock, index), d, op.Item2));
                        }
                        writeOps.RemoveAt(i);
                    }
                }
            }

            foreach (var op in bigBlockWriteOp)
            {
                var bigblock = op.Item1.Item1;
                var index = op.Item1.Item2;
                var side = op.Item2;
                WriteBigBlockCellSide(bigblock, index, side, op.Item3);
            }

            foreach (var op in writeOps)
                WriteBlockSide(op.Item1.block, op.Item1.d, op.Item2);

            Repaint();
        }
        protected bool IsConnected(BlockSide blockSide, IEnumerable<AssetBlock> allBlocks)
        {
            if (blockSide.id == 0) return false;

            var id = blockSide.id;
            foreach (var block in allBlocks)
            {
                for (int d = 0; d < 6; d++)
                {
                    if (id == block.baseIds[d] &&
                        (new BlockSide(block, d) != blockSide))
                        return true;
                }
            }

            return false;
        }
        protected void WriteBigBlockCellSide(BigBlockAsset bigBlock, Vector3Int index, int side, int id)
        {
            foreach (var block in bigBlock.data[index])
                WriteBlockSide(block, side, id);
        }

        protected void WriteBlockSide(AssetBlock block, int side, int id)
        {
            using (var so = new BlockAssetSO(block.blockAsset))
            {
                var variant = so.variants[block.VariantIndex];
                variant.sideIds[side] = id;
                so.variants[block.VariantIndex] = variant;
                so.ApplyField(nameof(BlockAssetSO.variants));
            }
        }
        protected void DetachFromBigBlock(AssetBlock block)
        {
            if (!block.hasGameObject || block.bigBlock == null)
                return;

            using (var so = new BigBlockAssetSO(block.bigBlock))
            {
                var index = GetIndexInBigBlock(block);
                var list = so.data[index.Item1];
                list.RemoveAt(index.Item2);
                so.ApplyField(nameof(BigBlockAssetSO.data));
            }

            using (var so = new BlockAssetSO(block.blockAsset))
            {
                so.variants[block.VariantIndex].bigBlock = null;
                so.ApplyField(nameof(BlockAssetSO.variants));
            }
        }
        protected void AttachToBigBlock(Vector3Int index,BigBlockAsset bigBlock,AssetBlock block)
        {
            if (block.bigBlock != null)
                DetachFromBigBlock(block);

            using (var so = new BlockAssetSO(block.blockAsset))
            {
                so.variants[block.VariantIndex].bigBlock = bigBlock;
                so.ApplyField(nameof(BlockAssetSO.variants));
            }

            using (var so = new BigBlockAssetSO(bigBlock))
            {
                so.data[index].Add(block);
                so.ApplyField(nameof(BigBlockAssetSO.data));
            }
        }

        protected Vector3 GetBlockPosition(IBlock block)
        {
            if (block.bigBlock != null && block.bigBlock.gameObject.activeInHierarchy)
                return GetPositionFromBigBlockAsset(block);
            else
                return GetPositionFromBlockAsset(block);
        }
        protected Vector3 GetPositionFromBigBlockAsset(IBlock block)
        {
            var assetBlock = (AssetBlock)block;
            if (block is AssetBlock)
            {
                return GetIndexInBigBlock(assetBlock).Item1 + assetBlock.bigBlock.transform.position;
            }
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
        protected (Vector3Int, int) GetIndexInBigBlock(AssetBlock block)
        {
            var data = block.bigBlock.data;
            foreach (var index in SpatialUtil.Enumerate(data.Size))
            {
                var list = data[index];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == block)
                        return (index, i);
                }

            }
            throw new System.Exception($"can't find the block in it's big block! {block.blockAsset}");
        }

        #endregion

        #region Utility

        protected Color GetColor(int id)
        {
            return AlaslTools.ColorUtility.GetColor(new XXHash().Append(id));
        }

        protected bool BackCulling(Vector3 pos, int side)
        {
            return AutoLevelEditorUtility.SideCullTest(camera, pos + origins[side], side);
        }
        protected bool CubeFrustumCulling(Vector3 pos)
        {
            Bounds b = new Bounds() { size = Vector3.one, center = pos };
            return GeometryUtility.TestPlanesAABB(planes, b);
        }
        protected bool PlaneFrustumCulling(Vector3 pos, Vector3 normal)
        {
            Bounds b = new Bounds() { center = pos, size = -0.95f * MathUtility.Abs(normal) + Vector3.one };
            return GeometryUtility.TestPlanesAABB(planes, b);
        }
        protected Bounds GetBigBlockBounds(BigBlockAsset bigBlockAsset)
        {
            var size = (Vector3)bigBlockAsset.data.Size;
            return new Bounds(bigBlockAsset.transform.position + size * 0.5f, size);
        }

        #endregion
    }
}