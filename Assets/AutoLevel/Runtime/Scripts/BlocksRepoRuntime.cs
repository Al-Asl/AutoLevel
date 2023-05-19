using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class BlockGOBlueprint
    {
        public GameObject gameObject;
        public List<BlockAction> actions;

        public BlockGOBlueprint(GameObject gameObject, List<BlockAction> actions)
        {
            this.gameObject = gameObject;
            this.actions = actions;
        }

        public GameObject Create()
        {
            if (gameObject == null)
                return null;

            var go = Object.Instantiate(gameObject);
            go.RemoveComponent<BlockAsset>();

            go.transform.SetParent(null);
            go.transform.Reset();
            AplyActions(go);

            var p = new GameObject(go.name);
            go.transform.SetParent(p.transform, true);

            return p;
        }

        private void AplyActions(GameObject go)
        {
            foreach (var action in actions)
                ActionsUtility.ApplyAction(go, action);
        }
    }

    public class BaseRepoException : System.Exception { }

    public class MissingLayersException : BaseRepoException 
    {
        private int layer;

        public MissingLayersException(int layer)
        {
            this.layer = layer;
        }

        public override string Message => $"there is no blocks in layer number {layer}";
    }

    public partial class BlocksRepo : MonoBehaviour
    {
        public class Runtime : System.IDisposable
        {
            private class BlockAssetGenerator
            {
                List<ActionsGroup> actionsGroups;
                Dictionary<int, int> actionsToIndex;

                public BlockAssetGenerator(List<ActionsGroup> actionsGroups, Dictionary<int, int> actionsToIndex)
                {
                    this.actionsGroups= actionsGroups;
                    this.actionsToIndex= actionsToIndex;
                }

                public void Generate(
                    IEnumerable<BlockAsset>     blockAssets, 
                    List<IBlock>                outputList,
                    VariantByActionsGroups      varaintsByAG)
                {

                    foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                    {
                        if (block.bigBlock != null)
                            continue;

                        varaintsByAG.AddFirstVariant(block.GetHashCode());

                        //apply Actions Groups
                        var actionsGroups = block.blockAsset.actionsGroups;

                        if (actionsGroups.Count > 0)
                        {
                            foreach (var groupHash in actionsGroups)
                            {
                                if (!actionsToIndex.ContainsKey(groupHash))
                                    continue;
                                var ActionsGroup = this.actionsGroups[actionsToIndex[groupHash]];

                                foreach (var GroupActions in ActionsGroup.groupActions)
                                {
                                    var newBlock = block.CreateCopy();
                                    newBlock.ApplyActions(GroupActions.actions);
                                    varaintsByAG.AddBlockVariant(block.GetHashCode(), newBlock.GetHashCode(), GroupActions.actions);
                                    outputList.Add(newBlock);
                                }
                            }
                        }

                        outputList.Add(block);
                    }
                }
            }

            private class BigBlockAssetGenerator
            {
                private List<ActionsGroup>              actionsGroups;
                private Dictionary<int, int>            actionsToIndex;
                private Dictionary<int, int>            connectionsMap;
                private ConnectionsUtility.IDGenerator  idGen;

                public BigBlockAssetGenerator(List<ActionsGroup> actionsGroups, 
                    Dictionary<int, int> actionsToIndex,
                    ConnectionsUtility.IDGenerator idGen)
                {
                    this.actionsGroups = actionsGroups;
                    this.actionsToIndex = actionsToIndex;
                    this.idGen = idGen;
                    connectionsMap = new Dictionary<int, int>();
                }

                public void Generate(
                    IEnumerable<BigBlockAsset>  assets,
                    List<IBlock>                blocks,
                    VariantByActionsGroups      variantByAG)
                {

                    foreach (var bigBlock in assets)
                    {
                        var data = bigBlock.data;

                        connectionsMap.Clear();
                        foreach (var conn in SpatialUtil.EnumerateConnections(data.Size))
                        {
                            var A = data[conn.Item1];
                            var B = data[conn.Item2];

                            if (A.IsEmpty || B.IsEmpty) continue;

                            var od = Directions.opposite[conn.Item3];

                            foreach (var id in A.Select((block) => block.baseIds[conn.Item3]).Intersect
                            (B.Select((block) => block.baseIds[od])))
                            {
                                if (id != 0 && !connectionsMap.ContainsKey(id))
                                    connectionsMap[id] = idGen.GetNext();
                            }
                        }

                        var internalConnections = new List<int>(connectionsMap.Select((pair) => pair.Key));

                        GenerateBlocks(bigBlock, new List<BlockAction>(), blocks, variantByAG);

                        //generate the states from the actions groups
                        foreach (var group in bigBlock.actionsGroups)
                        {
                            var actionsGroups = this.actionsGroups[actionsToIndex[group]].groupActions;

                            foreach (var actionsGroup in actionsGroups)
                            {
                                foreach (var conn in internalConnections)
                                    connectionsMap[conn] = idGen.GetNext();

                                GenerateBlocks(bigBlock, actionsGroup.actions, blocks, variantByAG);
                            }
                        }
                    }
                }

                private void GenerateBlocks(
                    BigBlockAsset               bigBlock,
                    List<BlockAction>           actions,
                    List<IBlock>                blocks,
                    VariantByActionsGroups      variantByAG)
                {

                    var srcData = bigBlock.data;
                    var dstData = new Array3D<SList<StandaloneBlock>>(srcData.Size);
                    foreach (var index in SpatialUtil.Enumerate(srcData.Size))
                    {
                        var src = srcData[index];
                        if (src.IsEmpty) continue;

                        var dst = new SList<StandaloneBlock>();
                        for (int i = 0; i < src.Count; i++)
                            dst.Add(src[i].CreateCopy());

                        dstData[index] = dst;
                    }

                    var map = new Dictionary<int, int>();
                    foreach (var conn in SpatialUtil.EnumerateConnections(srcData.Size))
                    {
                        var A = dstData[conn.Item1];
                        var B = dstData[conn.Item2];

                        if (A == null || A.IsEmpty || B == null || B.IsEmpty) continue;

                        var od = Directions.opposite[conn.Item3];
                        
                        map.Clear();
                        foreach (var id in A.Select((block) => block.baseIds[conn.Item3]).Intersect
                            (B.Select((block) => block.baseIds[od])))
                        {
                            if (id == 0)
                                map[id] = idGen.GetNext();
                            else
                                map[id] = connectionsMap[id];
                        }

                        for (int i = 0; i < A.Count; i++)
                        {
                            var id = A[i].baseIds[conn.Item3];
                            if(map.ContainsKey(id))
                            SetID(A, i, conn.Item3, map[id]);
                        }

                        for (int i = 0; i < B.Count; i++)
                        {
                            var id = B[i].baseIds[od];
                            if (map.ContainsKey(id))
                                SetID(B, i, od, map[id]);
                        }
                    }

                    var count = dstData.Size.x * dstData.Size.y * dstData.Size.z;

                    foreach (var index in SpatialUtil.Enumerate(dstData.Size))
                    {
                        var list = dstData[index];
                        if (list == null || list.IsEmpty)
                            continue;

                        for (int i = 0; i < list.Count; i++)
                        {
                            var block = list[i];

                            block.ApplyActions(actions);

                            if (bigBlock.overrideGroup)
                                block.group = bigBlock.group;

                            if (bigBlock.overrideWeightGroup)
                                block.weightGroup = bigBlock.weightGroup;
                            block.weight /= count;

                            variantByAG.AddBlockVariant(srcData[index][i].GetHashCode(), block.GetHashCode(), actions);
                            blocks.Add(block);
                        }
                    }
                }

                private void SetID(SList<StandaloneBlock> array, int index, int d, int id)
                {
                    var block = array[index];
                    var baseIds = block.baseIds;
                    baseIds[d] = id;
                    block.baseIds = baseIds;
                    array[index] = block;
                }
            }

            private class BlockGroupComparer : IComparer<IBlock>
            {
                private Dictionary<int, int> groupHashToIndex;

                public BlockGroupComparer(Dictionary<int, int> groupHashToIndex)
                {
                    this.groupHashToIndex = groupHashToIndex;
                }

                public int Compare(IBlock a, IBlock b)
                {
                    return groupHashToIndex[a.group].CompareTo(groupHashToIndex[b.group]);
                }
            }

            private class VariantByActionsGroups
            {
                private Dictionary<int, List<(int, List<BlockAction>)>> variants;

                public VariantByActionsGroups()
                {
                    variants = new Dictionary<int, List<(int, List<BlockAction>)>>();
                }

                public void AddBlockVariant(int blockHash, int variantHash, List<BlockAction> actions)
                {
                    if (!variants.ContainsKey(blockHash))
                        variants[blockHash] = new List<(int, List<BlockAction>)>();

                    variants[blockHash].Add((variantHash, actions));
                }

                public void AddFirstVariant(int blockHash)
                {
                    variants[blockHash] = new List<(int, List<BlockAction>)> () { (blockHash, new List<BlockAction>()) };
                }

                public List<(int, List<BlockAction>)> GetVariants(int blockHash) => variants[blockHash];
            }

            private class BlocksDependencyProcessor
            {
                private Dictionary<int, List<(int, BlocksResolve)>>     baseUpperBlocks;

                public BlocksDependencyProcessor()
                {
                   baseUpperBlocks      = new Dictionary<int, List<(int, BlocksResolve)>>();
                }

                public void AddUpperBlock(int blockHash,int upperBlockHash, BlocksResolve resolve = BlocksResolve.Matching)
                {
                    if (!baseUpperBlocks.ContainsKey(blockHash))
                        baseUpperBlocks[blockHash] = new List<(int, BlocksResolve)>();

                    baseUpperBlocks[blockHash].Add((upperBlockHash, resolve));
                }

                public Dictionary<int, List<int>> GenerateUpperBlocks(
                    BiDirectionalList<int> indexToHash,
                    VariantByActionsGroups variantByAG)
                {
                    var upperBlocks = new Dictionary<int, List<int>>();

                    foreach (var pair in baseUpperBlocks)
                    {
                        var bBlocks = variantByAG.GetVariants(pair.Key);

                        foreach (var uKey in pair.Value)
                        {
                            var uBlocks = variantByAG.GetVariants(uKey.Item1);

                            foreach (var bBlock in bBlocks)
                                foreach (var uBlock in uBlocks)
                                    if (uKey.Item2 == BlocksResolve.AllToAll || 
                                        ActionsUtility.AreEquals(bBlock.Item2, uBlock.Item2))
                                    {
                                        if (!upperBlocks.ContainsKey(indexToHash.GetIndex(bBlock.Item1)))
                                            upperBlocks[indexToHash.GetIndex(bBlock.Item1)] = new List<int>();
                                        upperBlocks[indexToHash.GetIndex(bBlock.Item1)].Add(indexToHash.GetIndex(uBlock.Item1));
                                    }
                        }
                    }

                    return upperBlocks;
                }
            }

            public class RepoMeshResource : System.IDisposable
            {
                private Runtime                         repo; 
                private List<GameObject>                gameObjects;
                private List<MeshCombiner.RendererInfo> renderers;
                private GameObject                      root;

                public RepoMeshResource(Runtime repo)
                {
                    this.repo = repo;
                    gameObjects = new List<GameObject>();
                    renderers = new List<MeshCombiner.RendererInfo>();

                    root = new GameObject("repo_mesh_resouece");
                    root.hideFlags = HideFlags.HideAndDontSave;

                    for (int i = 0; i < repo.Blueprints.Count; i++)
                    {
                        var bp = repo.Blueprints[i];

                        if(bp.gameObject == null)
                        {
                            renderers.Add(new MeshCombiner.RendererInfo());
                            gameObjects.Add(null);
                            continue;
                        }

                        var go = Instantiate(bp.gameObject);
                        DestroyImmediate(go.GetComponent<BlockAsset>(), false);

                        var info = MeshCombiner.Combine(MeshCombiner.GetAndRemoveRenderers(go.transform));

                        Strip(go.transform);

                        if(go != null)
                        {
                            go.hideFlags = HideFlags.HideAndDontSave;
                            go.name = bp.gameObject.name;
                            go.transform.Reset();
                            go.transform.SetParent(root.transform);
                            go.SetActive(false);
                        }

                        foreach(var action in bp.actions)
                            ActionsUtility.ApplyAction(info.mesh, action);
                        info.mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
#if AUTOLEVEL_DEBUG
                        info.mesh.name += ActionsUtility.GetActionPrefix(actions);
#endif

                        //TODO : share game objects and materials
                        renderers.Add(info);
                        gameObjects.Add(go);
                    } 
                }

                void Strip(Transform target)
                {
                    for (int i = target.childCount - 1; i > -1; i--)
                        Strip(target.GetChild(i));

                    if (target.childCount == 0 && !target.gameObject.HaveComponents())
                        DestroyImmediate(target.gameObject, false);
                }

                public GameObject GetGameObject(int index)
                {
                    var original = gameObjects[index];

                    if (original == null)
                        return null;

                    var go = Instantiate(original);
                    go.name = original.name;
                    go.SetActive(true);
                    go.hideFlags = HideFlags.None;

                    var p = new GameObject(go.name);
                    var actions = repo.Blueprints[index].actions;
                    foreach (var action in actions)
                        ActionsUtility.ApplyAction(go, action);

                    go.transform.SetParent(p.transform);

                    return p;
                }

                public MeshCombiner.RendererInfo GetRendererInfo(int index)
                {
                    return renderers[index];
                }

                public void Dispose()
                {
                    foreach (var info in renderers)
                        GameObjectUtil.SafeDestroy(info.mesh);

                    GameObjectUtil.SafeDestroy(root);
                }
            }

            private BlocksRepo                  repo;
            private List<ActionsGroup>          actionsGroups;

            private BiDirectionalList<int>      BlocksHash;
            private List<BlockGOBlueprint>      Blueprints;
            private RepoMeshResource            MeshResource;

            private List<int>                   layerStartIndex;
            private List<List<int>>             groupStartIndex;

            private List<float>                 weights;
            private List<int>[]                 blocksPerWeightGroup;

            private BiDirectionalList<string>   groups;
            private BiDirectionalList<string>   weightGroups;

            public int                          LayersCount { get; private set; }
            private Dictionary<int, List<int>>  upperBlocks;
            private List<BlockPlacement>        blocksPlacement;

            public IEnumerable<int> GetUpperLayerBlocks(int block)
            {
                if(upperBlocks.ContainsKey(block))
                    return upperBlocks[block];
                else
                {
                    return new int[] {
                        GetGroupRange(GetGroupIndex(EMPTY_GROUP), GetBlockLayer(block) + 1).x
                    };
                }
            }
            public BlockPlacement GetBlockPlacement(int block) => blocksPlacement[block];
            public int GetBlockLayer(int block)
            {
                for (int layer = 0; layer < LayersCount; layer++)
                    if (block < GetLayerRange(layer).y)
                        return layer;
                throw new System.Exception($"repo doesn't contain block with index of {block}");
            }
            public Vector2Int GetLayerRange(int layer) => GetLayerRange(layer, BlocksCount);
            private Vector2Int GetLayerRange(int layer, int blocksCount)
            {
                return new Vector2Int(layerStartIndex[layer],
                    layer == layerStartIndex.Count - 1 ? blocksCount : layerStartIndex[layer + 1]);
            }

            public List<List<int>>[] Connections { get; private set; }
            /// <summary>
            /// [d][ga][gb] => ga_countlist
            /// </summary>
            public int[][][][] groupCounter { get; private set; }

            public int BlocksCount => Blueprints.Count;
            public bool ContainsBlock(int hash) => BlocksHash.Contains(hash);
            public int GetBlockHash(int index) => BlocksHash[index];
            public int GetBlockIndex(int hash) => BlocksHash.GetIndex(hash);
            public RepoMeshResource GetMeshResource()
            {
                if (MeshResource == null)
                    MeshResource = new RepoMeshResource(this);
                return MeshResource;
            }
            public GameObject GetBlockAsset(int index) => Blueprints[index].gameObject;
            public GameObject CreateGameObject(int index) => Blueprints[index].Create();
            public IEnumerable<float> GetBlocksWeight() => weights;

            /// Groups ///
            public int GroupsCount => groups.Count;
            public string GetGroupName(int index) => groups[index];
            public bool ContainsGroup(string name) => groups.GetList().Contains(name);
            public int GetGroupHash(int index) => groups[index].GetHashCode();
            public int GetGroupIndex(string name) => groups.GetIndex(name);
            public int GetGroupIndex(int hash) => groups.GetList().FindIndex((e) => e.GetHashCode() == hash);
            public Vector2Int GetGroupRange(int index, int layer)
            {
                return new Vector2Int(
                    groupStartIndex[layer][index],
                    index == groupStartIndex[layer].Count - 1 ?
                    GetLayerRange(layer).y :
                    groupStartIndex[layer][index + 1]);
            }

            /// Weight Groups ///
            public int WeightGroupsCount => weightGroups.Count;
            public string GetWeightGroupName(int index) => weightGroups[index];
            public bool ContainsWeightGroup(string name) => weightGroups.GetList().Contains(name);
            public int GetWeightGroupHash(int index) => weightGroups[index].GetHashCode();
            public int GetWeightGroupIndex(string name) => weightGroups.GetIndex(name);
            public IEnumerable<int> GetBlocksPerWeightGroup(int group) => blocksPerWeightGroup[group];

            static int groups_counter_pk = "block_repo_groups_counter".GetHashCode();
            static int generate_blocks_pk = "block_repo_generate_blocks".GetHashCode();

            public Runtime(BlocksRepo repo, List<string> GroupsNames, List<string> WeightGroupsNames, List<ActionsGroup> actionsGroups)
            {
                groups = new BiDirectionalList<string>(GroupsNames);
                weightGroups = new BiDirectionalList<string>(WeightGroupsNames);

                this.actionsGroups = actionsGroups;
                this.repo = repo;

                List<ConnectionsIds> BlockConnections       = new List<ConnectionsIds>();
                List<(int, int, int)> BannedConnections     = new List<(int, int, int)>();
                List<(int, int, int)> ExclusiveConnections  = new List<(int, int, int)>();

                GenerateBlockData(BlockConnections, BannedConnections, ExclusiveConnections);
                GenerateConnections(BlockConnections, BannedConnections, ExclusiveConnections);
                GenerateGroupCounter();
            }

            private void GenerateBlockData(
                List<ConnectionsIds>    BlockConnections,
                List<(int, int, int)>   BannedConnections,
                List<(int, int, int)>   ExclusiveConnections)
            {

                var actionsToIndex = new Dictionary<int, int>();
                for (int i = 0; i < actionsGroups.Count; i++)
                    actionsToIndex.Add(actionsGroups[i].name.GetHashCode(), i);

                var blockAssets = repo.GetComponentsInChildren<BlockAsset>();
                var bigBlockAssets = new List<BigBlockAsset>(repo.GetComponentsInChildren<BigBlockAsset>());

                foreach(var block in BlockAsset.GetBlocksEnum(blockAssets))
                    if (block.bigBlock != null && !bigBlockAssets.Contains(block.bigBlock))
                        bigBlockAssets.Add(block.bigBlock);

                LayersCount = GetLayersCount(blockAssets);

                List<IBlock> allBlocks = new List<IBlock>();

                //Empty and Solid
                allBlocks.Add(new StandaloneBlock(GetGroupHash(1), GetWeightGroupHash(1).GetHashCode(), 255, 1f, 0));
                for (int i = 0; i < LayersCount; i++)
                    allBlocks.Add(new StandaloneBlock(GetGroupHash(0), GetWeightGroupHash(0).GetHashCode(), 0, 1f, i));

                var blocksDP = new BlocksDependencyProcessor();
                var variantsByAG = new VariantByActionsGroups();

                if (LayersCount > 1)
                {
                    //Solid Base Layer
                    blocksDP.AddUpperBlock(allBlocks[0].GetHashCode(), allBlocks[2].GetHashCode());
                    variantsByAG.AddFirstVariant(allBlocks[0].GetHashCode());
                    //Empty Layers
                    for (int i = 0; i < LayersCount; i++)
                    {
                        if (i < LayersCount - 1)
                            blocksDP.AddUpperBlock(allBlocks[i + 1].GetHashCode(), allBlocks[i + 2].GetHashCode());
                        variantsByAG.AddFirstVariant(allBlocks[i + 1].GetHashCode());
                    }
                }

                foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                {
                    var layerSettings = block.layerSettings;

                    if (layerSettings.PartOfBaseLayer)
                        continue;

                    if (!layerSettings.HasDependencies)
                        blocksDP.AddUpperBlock(allBlocks[layerSettings.layer].GetHashCode(), block.GetHashCode());
                    else
                        foreach (var depBlock in layerSettings.dependencies)
                            blocksDP.AddUpperBlock(depBlock.GetHashCode(), block.GetHashCode(), layerSettings.resolve);
                }

                var blockAssetGenerator = new BlockAssetGenerator(actionsGroups, actionsToIndex);
                blockAssetGenerator.Generate(blockAssets, allBlocks, variantsByAG);

                var idGen = ConnectionsUtility.CreateIDGenerator(BlockAsset.GetBlocksEnum(blockAssets));
                var bigBlockAssetGenerator = new BigBlockAssetGenerator(actionsGroups, actionsToIndex, idGen);
                bigBlockAssetGenerator.Generate(bigBlockAssets, allBlocks, variantsByAG);

                layerStartIndex = new List<int>();
                allBlocks.Sort((a, b) => a.layerSettings.layer.CompareTo(b.layerSettings.layer));

                var currentLayer = 0;
                layerStartIndex.Add(currentLayer);
                for (int i = 0; i < allBlocks.Count; i++)
                {
                    var block = allBlocks[i];
                    if (block.layerSettings.layer != currentLayer)
                    {
                        currentLayer++;
                        layerStartIndex.Add(i);
                        if (block.layerSettings.layer != currentLayer)
                            throw new MissingLayersException(currentLayer);
                    }
                }

                var groupHashToIndex = HashToIndexLookup(groups);
                var groupComparer = new BlockGroupComparer(groupHashToIndex);
                for (int layer = 0; layer < LayersCount; layer++)
                {
                    var range = GetLayerRange(layer, allBlocks.Count);
                    allBlocks.Sort(range.x, range.y - range.x, groupComparer);
                }

                groupStartIndex = new List<List<int>>();
                groupStartIndex.Fill(LayersCount);
                for (int layer = 0; layer < LayersCount; layer++)
                {
                    var list = groupStartIndex[layer];
                    var range = GetLayerRange(layer, allBlocks.Count);
                    var lastGroup = -1;
                    for (int i = range.x; i < range.y; i++)
                    {
                        var block = allBlocks[i];
                        while (groupHashToIndex[block.group] != lastGroup)
                        {
                            list.Add(i);
                            lastGroup++;
                        }
                    }
                    while (groups.Count != list.Count)
                        list.Add(range.y);
                }

                Dictionary<int, int> wgHashToIndex = HashToIndexLookup(weightGroups);

                Blueprints = new List<BlockGOBlueprint>();
                BlocksHash = new BiDirectionalList<int>();
                weights = new List<float>(allBlocks.Count);
                blocksPlacement = new List<BlockPlacement>(allBlocks.Count);
                blocksPerWeightGroup = new List<int>[WeightGroupsCount];

                for (int i = 0; i < WeightGroupsCount; i++)
                    blocksPerWeightGroup[i] = new List<int>();

                for (int i = 0; i < allBlocks.Count; i++)
                {
                    var block = allBlocks[i];
                    BlocksHash.Add(block.GetHashCode());
                    Blueprints.Add(new BlockGOBlueprint(block.gameObject, block.actions));

                    BlockConnections.Add(repo.useFilling ? block.compositeIds : block.baseIds);
                    weights.Add(block.weight);
                    blocksPerWeightGroup[wgHashToIndex[block.weightGroup]].Add(i);

                    blocksPlacement.Add(block.layerSettings.placement);
                }

                GenerateCustomConnections(repo.bannedConnections, variantsByAG, BannedConnections);
                GenerateCustomConnections(repo.exclusiveConnections, variantsByAG, ExclusiveConnections);

                upperBlocks = blocksDP.GenerateUpperBlocks(BlocksHash, variantsByAG);
            }

            private void GenerateCustomConnections(
                List<Connection>        connections,
                VariantByActionsGroups  variantsByAG,
                List<(int, int, int)>   output)
            {
                foreach (var conn in connections)
                {
                    if (!BlockUtility.IsActive(conn.a.block) || !BlockUtility.IsActive(conn.b.block)) continue;

                    var a_vars = variantsByAG.GetVariants(conn.a.block.GetHashCode());
                    var b_vars = variantsByAG.GetVariants(conn.b.block.GetHashCode());
                    foreach (var a_var in a_vars)
                    {
                        var a_side = ActionsUtility.TransformFace(conn.a.d, a_var.Item2);
                        foreach (var b_var in b_vars)
                        {
                            var b_side = ActionsUtility.TransformFace(conn.b.d, b_var.Item2);
                            if (a_side == Directions.opposite[b_side])
                                output.Add((a_side, BlocksHash.GetIndex(a_var.Item1), BlocksHash.GetIndex(b_var.Item1)));
                        }
                    }
                }
            }

            private void GenerateConnections(
                List<ConnectionsIds>    BlockConnections,
                List<(int, int, int)>   BannedConnections,
                List<(int, int, int)>   ExclusiveConnections)
            {
                Profiling.StartTimer(generate_blocks_pk);

                Connections = ConnectionsUtility.GetAdjacencyList(BlockConnections);

                Dictionary<int, HashSet<int>[]> exclusiveTarget = new Dictionary<int, HashSet<int>[]>();

                foreach (var conn in ExclusiveConnections)
                {
                    var d = conn.Item1;

                    if (!exclusiveTarget.ContainsKey(conn.Item2))
                        exclusiveTarget[conn.Item2] = new HashSet<int>[6];
                    if (exclusiveTarget[conn.Item2][d] == null)
                        exclusiveTarget[conn.Item2][d] = new HashSet<int>(Connections[d][conn.Item2]);
                }

                foreach(var target in exclusiveTarget)
                    for (int d = 0; d < 6; d++)
                        if (target.Value[d] != null) Connections[d][target.Key].Clear();

                foreach (var conn in ExclusiveConnections)
                {
                    var d = conn.Item1;
                    var od = Directions.opposite[conn.Item1];

                    if (exclusiveTarget[conn.Item2][d].Contains(conn.Item3))
                    {
                        Connections[d][conn.Item2].Add(conn.Item3);
                        if (!Connections[od][conn.Item3].Contains(conn.Item2))
                            Connections[od][conn.Item3].Add(conn.Item2);
                        exclusiveTarget[conn.Item2][d].Remove(conn.Item3);
                    }
                }

                foreach(var pair in exclusiveTarget)
                {
                    for (int d = 0; d < 6; d++)
                    {
                        var od = Directions.opposite[d];
                        if (pair.Value[d] != null)
                            foreach(var block in pair.Value[d])
                                Connections[od][block].Remove(pair.Key);
                    }
                }

                foreach (var conn in BannedConnections)
                {
                    var d = conn.Item1;
                    var od = Directions.opposite[conn.Item1];

                    Connections[d][conn.Item2].Remove(conn.Item3);
                    Connections[od][conn.Item3].Remove(conn.Item2);
                }

                Profiling.LogAndRemoveTimer($"time to generate connections of {Blueprints.Count} ", generate_blocks_pk);

            }

            private void GenerateGroupCounter()
            {
                Profiling.StartTimer(groups_counter_pk);

                groupCounter = GenerateGroupCounter(0);

                Profiling.LogAndRemoveTimer("time to generate groups counter ", groups_counter_pk);
            }
            
            private int[][][][] GenerateGroupCounter(int layer)
            {
                var gc = GroupsCount;
                var groupCounter = new int[6][][][];

                for (int d = 0; d < 6; d++)
                {
                    var g = new int[gc][][];
                    for (int i = 0; i < gc; i++)
                        g[i] = new int[gc][];
                    groupCounter[d] = g;
                }

                for (int d = 0; d < 6; d++)
                {
                    var conn_d = Connections[d];
                    var counter_d = groupCounter[d];
                    for (int i = 0; i < gc; i++)
                    {
                        var group_a = GetGroupRange(i, layer);
                        var counter_d_a = counter_d[i];
                        for (int j = 0; j < gc; j++)
                        {
                            var group_b = GetGroupRange(j, layer);
                            var counter = new int[group_a.y - group_a.x];

                            for (int a = 0; a < counter.Length; a++)
                            {
                                var conn = conn_d[group_a.x + a];
                                var count = 0;
                                for (int b = group_b.x; b < group_b.y; b++)
                                    if (conn.BinarySearch(b) > -1)
                                        count++;
                                counter[a] = count;
                            }

                            counter_d_a[j] = counter;
                        }
                    }
                }

                return groupCounter;
            }

            private Dictionary<int, int> HashToIndexLookup<T>(IEnumerable<T> list)
            {
                var result = new Dictionary<int, int>();
                int i = 0;
                foreach (var item in list)
                    result[item.GetHashCode()] = i++;
                return result;
            }

            public void Dispose()
            {
                MeshResource?.Dispose();
            }
        }
    }
}