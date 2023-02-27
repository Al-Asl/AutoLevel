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

            public static bool operator == (BlockSide a,BlockSide b)
            {
                return a.block.GetHashCode() == b.block.GetHashCode() && a.d == b.d;
            }

            public static bool operator !=(BlockSide a, BlockSide b)
            {
                return a.block.GetHashCode() != b.block.GetHashCode() || a.d != b.d;
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

        private Texture removeIcon;

        private List<GameObject> assetBlocksGO = new List<GameObject>();
        private Dictionary<int, Material> assetBlocksMat = new Dictionary<int, Material>();

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

            var basePath = System.IO.Path.Combine(EditorHelper.GetAssemblyDirectory<LevelBuilderEditor>(), "Scripts", "Resources");
            removeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                System.IO.Path.Combine(basePath, "BigBlockRemove.png"));

            SceneView.beforeSceneGui += BeforeSceneGUI;
        }

        protected virtual void OnDisable()
        {
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

            var blocks = GetBlocksIt(AssetType.BlockAsset | AssetType.BigBlockAssetAll);

            //sort by the closest
            var targetPos = ((MonoBehaviour)target).transform.position;
            blocks = blocks.OrderBy((block) => Vector3.Distance(block.Item2, targetPos));

            connections.Clear();
            ConnectionsUtility.GetConnectionsList(blocks.Select((block) => block.Item1), connections);

            int index = 0;
            for (int i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn.a.baseIds[conn.d] != 0)
                    connections.Swap(index++,i);
            }

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
                        sideIds = new ConnectionsIds()
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
                    var oList = so.data[index];
                    var nList = new SList<AssetBlock>();
                    for (int i = 0; i < oList.Count; i++)
                    {
                        var block = oList[i];
                        if (block.blockAsset != null && 
                            block.VariantIndex < block.blockAsset.variants.Count)
                                nList.Add(block);
                    }
                    if (oList.Count != nList.Count)
                    {
                        so.data[index] = nList;
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
                Debug.LogWarning("make the mesh readable, if you are generating in runtime");

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
                    if (rays[j].RayTriangleIntersect(v0, v1, v2,
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
        private void InitGroups()
        {
            var repoGroups = repo.GetAllGroupsNames();
            allGroups = new string[repoGroups.Count - 2];
            for (int i = 2; i < repoGroups.Count; i++)
                allGroups[i - 2] = repoGroups[i];

            repoGroups = repo.GetAllWeightGroupsNames();
            allWeightGroups = new string[repoGroups.Count - 2];
            for (int i = 2; i < repoGroups.Count; i++)
                allWeightGroups[i - 2] = repoGroups[i];
        }

        protected void DrawAssetsBlocks()
        {
            var blocks = GetBlocksIt(AssetType.BlockAsset).
                Where((block) => block.Item1.VariantIndex != 0).
                Concat(GetBlocksIt(AssetType.BigBlockAssetFirst));

            if(assetBlocksGO.Count != blocks.Count())
            {
                ClearAssetsBlocks();

                foreach(var block in blocks)
                {
                    var b = new GameObject(block.Item1.blockAsset.name);
                    b.hideFlags = HideFlags.HideAndDontSave;

                    b.AddComponent<MeshFilter>().sharedMesh = block.Item1.baseMesh;
                    b.AddComponent<MeshRenderer>().sharedMaterial = BlockUtility.GetMaterial(block.Item1.gameObject);
                    foreach(var action in block.Item1.actions)
                        ActionsUtility.ApplyAction(b, action);

                    var go = new GameObject(block.Item1.blockAsset.name);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    b.transform.SetParent(go.transform);

                    assetBlocksGO.Add(go);
                }
            }else
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
        protected void DoBlocksSelectionButtons(IEnumerable<MonoBehaviour> targets)
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

        protected enum AssetType { BlockAsset = 1 , BigBlockAssetFirst = 2 , BigBlockAssetAll = 4}
        protected IEnumerable<(AssetBlock, Vector3)> GetBlocksIt(AssetType assetType, bool includeInActive = false)
        {
            var assets = includeInActive ? allRepoEntities : activeRepoEntities;
            foreach(var asset in assets)
            {
                var bigBlock = asset is BigBlockAsset;
                if ((int)assetType > 1)
                {
                    if (assetType.HasFlag(AssetType.BigBlockAssetFirst))
                    {
                        if(bigBlock)
                        {
                            foreach (var block in AssetBlocksFirstIt((BigBlockAsset)asset))
                                yield return block;
                        }
                        else if (assetType.HasFlag(AssetType.BlockAsset))
                        {
                                foreach (var block in AssetBlocksIt((BlockAsset)asset))
                                    if (GetIndexInBigBlock(block.Item1).Item2 > 0)
                                        yield return block;
                        }
                    }
                    else
                    {
                        if (bigBlock)
                        {
                            foreach (var block in AssetBlocksAllIt((BigBlockAsset)asset))
                                yield return block;
                        }else if (assetType.HasFlag(AssetType.BlockAsset))
                        {
                            foreach (var block in AssetBlocksIt((BlockAsset)asset))
                                if (block.Item1.bigBlock == null)
                                    yield return block;
                        }
                    }
                }
                else if (!bigBlock && assetType == AssetType.BlockAsset)
                {
                    foreach (var block in AssetBlocksIt((BlockAsset)asset))
                        yield return block;
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
                if (!settings.DrawVariants && j > 0)
                    break;

                var block = new AssetBlock(j, blockAsset);
                yield return (block, GetPositionFromBlockAsset(block));
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
                    if (SideCullTest(pos, d))
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
            return Button.Draw<Cube3DD>();
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
            return Button.Draw<Quad3DD>();
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
            if ((AssetBlock)con.a == (AssetBlock)con.b)
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
            var id = con.a.baseIds[con.d];
            Handles.DrawBezier(s, e, s + st, e + et, color, null,id == 0 ? 2f : 4f);
        }

        protected void DrawBlockToBigBlockConnection(AssetBlock block)
        {
            //Draw the line

            {
                Handles.color = NiceColors.Saffron;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;
                Handles.DrawLine(GetPositionFromBlockAsset(block) + Vector3.one * 0.5f,
                                GetPositionFromBigBlockAsset(block) + Vector3.one * 0.5f, 3);
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }

            //Draw the outline

            {
                var pos = GetPositionFromBlockAsset(block) + Vector3.one * 0.5f;

                GetDrawCmd().
                    SetPrimitiveMesh(PrimitiveType.Cube).
                    SetMaterial(HandleEx.OutLineMaterial).
                    Scale(0.95f).Move(pos).Draw();

                GetDrawCmd().
                    SetPrimitiveMesh(PrimitiveType.Cube).
                    SetMaterial(HandleEx.OutLineMaterial).
                    SetColor(NiceColors.Saffron).
                    Move(pos).Draw(pass: 1);
            }
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
                .SetTexture(removeIcon)
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

        protected void DoSideConnection(BlockSide src, Vector3 position, System.Action OnConnect)
        {
            SceneView.RepaintAll();

            Handles.DrawDottedLine(
                GetSideCenter(position, src.d),
                HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f)
                , 4f);

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
                            writeOps.Add((new BlockSide(block,d), aId));
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
                        //every big block cell get one write
                        if (bigBlockWriteOp.FindIndex((item) =>
                        item.Item1.Item1 == block.bigBlock &&
                        item.Item1.Item2 == index) < 0)
                        {
                            bigBlockWriteOp.Add(((block.bigBlock, index), d, op.Item2));
                        }
                        writeOps.RemoveAt(i);
                    }
                }
            }

            foreach(var op in bigBlockWriteOp)
            {
                var bigblock = op.Item1.Item1;
                var index = op.Item1.Item2;
                var side = op.Item2;
                WriteBigBlockCellSide(bigblock, index, side, op.Item3);
            }

            foreach(var op in writeOps)
                WriteBlockSide((AssetBlock)op.Item1.block, op.Item1.d, op.Item2);

            Repaint();
        }

        protected void WriteBigBlockCellSide(BigBlockAsset bigBlock,Vector3Int index, int side, int id)
        {
            foreach (var block in bigBlock.data[index])
                WriteBlockSide(block, side, id);
        }

        protected void WriteBlockSide(AssetBlock block,int side,int id)
        {
            using (var so = new BlockAssetEditor.SO(block.blockAsset))
            {
                var variant = so.variants[block.VariantIndex];
                variant.sideIds[side] = id;
                so.variants[block.VariantIndex] = variant;
                so.ApplyField(nameof(BlockAssetEditor.SO.variants));
            }
        }

        protected bool IsConnected(BlockSide blockSide, IEnumerable<AssetBlock> allBlocks)
        {
            if (blockSide.id == 0) return false;

            var id = blockSide.id;
            foreach(var block in allBlocks)
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

        protected int GetGroupIndex(int group) => System.Array.FindIndex(allGroups, (g) => g.GetHashCode() == group);
        protected int GetWeightGroupIndex(int weightGroup) => System.Array.FindIndex(allWeightGroups, (g) => g.GetHashCode() == weightGroup);

        protected void DetachFromBigBlock(AssetBlock block)
        {
            if (block.blockAsset == null || block.bigBlock == null)
                return;

            using (var so = new BigBlockAssetEditor.SO(block.bigBlock))
            {
                var index = GetIndexInBigBlock(block);
                var list = so.data[index.Item1];
                list.RemoveAt(index.Item2);
                so.ApplyField(nameof(BigBlockAssetEditor.SO.data));
            }

            using (var so = new BlockAssetEditor.SO(block.blockAsset))
            {
                so.variants[block.VariantIndex].bigBlock = null;
                so.ApplyField(nameof(BlockAssetEditor.SO.variants));
            }
        }
        protected void AttachToBigBlock(Vector3Int index,BigBlockAsset bigBlock,AssetBlock block)
        {
            if (block.bigBlock != null)
                DetachFromBigBlock(block);

            using (var so = new BlockAssetEditor.SO(block.blockAsset))
            {
                so.variants[block.VariantIndex].bigBlock = bigBlock;
                so.ApplyField(nameof(BlockAssetEditor.SO.variants));
            }

            using (var so = new BigBlockAssetEditor.SO(bigBlock))
            {
                so.data[index].Add(block);
                so.ApplyField(nameof(BigBlockAssetEditor.SO.data));
            }
        }
        protected Color GetColor(int id)
        {
            return AlaslTools.ColorUtility.GetColor(new XXHash().Append(id));
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
        protected (Vector3Int,int) GetIndexInBigBlock(AssetBlock block)
        {
            var data = block.bigBlock.data;
            foreach (var index in SpatialUtil.Enumerate(data.Size))
            {
                var list = data[index];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == block)
                        return (index,i);
                }
                
            }
            throw new System.Exception($"can't find the block in it's big block! {block.blockAsset}");
        }
        protected bool SideCullTest(Vector3 pos, int side)
        {
            return AutoLevelEditorUtility.SideCullTest(camera, pos + origins[side], side);
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