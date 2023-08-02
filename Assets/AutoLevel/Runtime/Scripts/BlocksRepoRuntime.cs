using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
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
            public struct LayerPartioner
            {
                private List<int> layerStartIndex;
                private int BlocksCount;

                public LayerPartioner(List<int> layerStartIndex, int BlocksCount)
                {
                    this.layerStartIndex = layerStartIndex;
                    this.BlocksCount = BlocksCount;
                }

                public Vector2Int GetRange(int layer)
                {
                    return new Vector2Int(layerStartIndex[layer],
                        layer == layerStartIndex.Count - 1 ? BlocksCount : layerStartIndex[layer + 1]);
                }
            }

            private class BigBlockAssetGenerator
            {
                private StagingContext context;
                private Dictionary<int, int> connectionsMap;
                private ConnectionsUtility.IDGenerator idGen;

                public BigBlockAssetGenerator(
                    StagingContext context,
                    ConnectionsUtility.IDGenerator idGen)
                {
                    this.idGen = idGen;
                    this.context = context;
                    connectionsMap = new Dictionary<int, int>();
                }

                public void Generate(IEnumerable<BigBlockAsset> assets)
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

                        GenerateBlocks(bigBlock, new List<BlockAction>());

                        //generate the states from the actions groups
                        foreach (var group in bigBlock.actionsGroups)
                        {
                            var actionsGroups = context.GetActionsGroup(group).groupActions;

                            foreach (var actionsGroup in actionsGroups)
                            {
                                foreach (var conn in internalConnections)
                                    connectionsMap[conn] = idGen.GetNext();

                                GenerateBlocks(bigBlock, actionsGroup.actions);
                            }
                        }
                    }
                }

                private void GenerateBlocks(BigBlockAsset bigBlock, List<BlockAction> actions)
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
                            if (map.ContainsKey(id))
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

                            context.AddBlock(srcData[index][i].GetHashCode(), block, actions);
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

            public class RepoMeshResource : System.IDisposable
            {
                private Runtime                         repo; 
                private List<GameObject>                gameObjects;
                private List<MeshCombiner.RendererInfo> renderers;
                private GameObject                      root;

                public RepoMeshResource(Runtime repo)
                {
                    this.repo   = repo;
                    gameObjects = new List<GameObject>();
                    renderers   = new List<MeshCombiner.RendererInfo>();

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
                        info.mesh.name += ActionsUtility.GetActionPrefix(bp.actions);
#endif

                        //TODO : share game objects and materials
                        renderers.Add(info);
                        gameObjects.Add(go);
                    } 
                }

                private void Strip(Transform target)
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

            private BiDirectionalList<int>      BlocksHash;
            private List<BlockGOBlueprint>      Blueprints;
            private RepoMeshResource            MeshResource;

            private LayerPartioner              layerPartioner;
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
            public Vector2Int GetLayerRange(int layer) => layerPartioner.GetRange(layer);

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

            public Runtime(
                BlocksRepo          repo,
                List<string>        GroupsNames,
                List<string>        WeightGroupsNames,
                List<ActionsGroup>  actionsGroups)
            {
                this.repo = repo;
                LayersCount = GetLayersCount(repo.GetComponentsInChildren<BlockAsset>());

                var stagingContext = new StagingContext(LayersCount, GroupsNames, WeightGroupsNames, actionsGroups);

                Stage(stagingContext);

                var connectingContext = stagingContext.BuildConnectingContext(repo.useFilling);

                Connect(connectingContext);

                Submit(connectingContext);

                GenerateGroupCache();
            }

            private void Stage(StagingContext context)
            {
                var blockAssets = repo.GetComponentsInChildren<BlockAsset>();

                //// Creating Built-in blocks ////

                var emptyBlocks = new List<StandaloneBlock>();
                var solidBlocks = new List<StandaloneBlock>();

                for (int i = 0; i < LayersCount; i++)
                {
                    var block = new StandaloneBlock(SOLID_GROUP.GetHashCode(), SOLID_GROUP.GetHashCode(), 255, i);
                    context.AddBlock(block);
                    solidBlocks.Add(block);
                }

                for (int i = 0; i < LayersCount; i++)
                {
                    var block = new StandaloneBlock(EMPTY_GROUP.GetHashCode(), EMPTY_GROUP.GetHashCode(), 0, i);
                    context.AddBlock(block);
                    emptyBlocks.Add(block);
                }

                // connecting between layers
                for (int i = 0; i < LayersCount - 1; i++)
                {
                    context.AddUpperBlock(emptyBlocks[i].GetHashCode(), emptyBlocks[i + 1].GetHashCode());
                    context.AddUpperBlock(solidBlocks[i].GetHashCode(), solidBlocks[i + 1].GetHashCode());
                }

                //// Creating Blocks from block assets ////

                foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                {
                    if (block.bigBlock != null) continue;

                    context.AddBlockAndVariants(block);
                }

                // query big blocks
                var bigBlockAssets = new List<BigBlockAsset>(repo.GetComponentsInChildren<BigBlockAsset>());
                foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                    if (block.bigBlock != null && !bigBlockAssets.Contains(block.bigBlock))
                        bigBlockAssets.Add(block.bigBlock);

                // generate blocks from big blocks
                var idGen = ConnectionsUtility.CreateIDGenerator(BlockAsset.GetBlocksEnum(blockAssets));
                var bigBlockAssetGenerator = new BigBlockAssetGenerator(context, idGen);
                bigBlockAssetGenerator.Generate(bigBlockAssets);

                // connecting between layers
                foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                {
                    var layerSettings = block.layerSettings;

                    if (layerSettings.layer == 0)
                        continue;

                    if (!block.layerSettings.HasDependencies)
                        context.AddUpperBlock(emptyBlocks[layerSettings.layer - 1].GetHashCode(),block.GetHashCode(),BlocksResolve.AllToAll);
                    else
                        foreach (var depBlock in layerSettings.dependencies)
                            if (layerSettings.layer - depBlock.layerSettings.layer == 1)
                                context.AddUpperBlock(depBlock.GetHashCode(), block.GetHashCode(), layerSettings.resolve);
                }

                //// Creating filler blocks between layers ////

                var authoringBlocks = context.QueryAuthoringBlocks();

                foreach(var authorBlock in authoringBlocks)
                {
                    var block = context.GetFirstVariant(authorBlock);

                    int layer = block.layerSettings.layer;
                    if (layer == LayersCount - 1)
                        continue;

                    if (context.HasUpperBlocks(authorBlock))
                        continue;

                    var preBlock = block;
                    var preBlockHash = authorBlock;

                    while(++layer != LayersCount)
                    {
                        var newBlock = preBlock.CreateCopy();
                        var newBlockHash = newBlock.GetHashCode();
                        newBlock.layerSettings.layer = layer;
                        context.AddBlockAndVariants(newBlock);
                        context.AddUpperBlock(preBlockHash, newBlockHash);
                        preBlock = newBlock;
                        preBlockHash = newBlockHash;
                    }
                }

                foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                {
                    var layerSettings = block.layerSettings;

                    if (layerSettings.layer < 2)
                        continue;

                    foreach (var depBlock in layerSettings.dependencies)
                        if (layerSettings.layer - depBlock.layerSettings.layer > 1)
                            TraceAndConnect(context, depBlock, block);
                }
            }

            private void TraceAndConnect(StagingContext context,IBlock block,IBlock targetBlock)
            {
                if(targetBlock.layerSettings.layer - block.layerSettings.layer == 1)
                {
                    context.AddUpperBlock(block.GetHashCode(), targetBlock.GetHashCode());
                    return;
                }
                foreach(var ublock in context.GetUpperBlocksEnum(block.GetHashCode()))
                    TraceAndConnect(context, context.GetBlock(ublock), targetBlock);
            }

            private void Connect(ConnectingContext context)
            {
                ConnectOnTheSameLayer(context);

                ConnectBetweenLayers(context);
            }

            private void ConnectOnTheSameLayer(ConnectingContext context)
            {
                /// Base connections ///

                Profiling.StartTimer(generate_blocks_pk);

                Connections = ConnectionsUtility.GetAdjacencyList(context.BlocksConnections);

                Dictionary<int, List<int>> preConnections = new Dictionary<int, List<int>>();

                /// Exclusive connections ///

                var ExclusiveConnections = GenerateCustomConnections(repo.exclusiveConnections, context);

                foreach (var conn in ExclusiveConnections)
                {
                    var d = conn.Item1;
                    var od = Directions.opposite[d];
                    var id = new XXHash().Append(conn.Item2).Append(conn.Item1);
                    var connections = Connections[d][conn.Item2];

                    foreach (var preConn in connections)
                        Connections[od][preConn].Remove(conn.Item2);
                    Connections[d][conn.Item2] = new List<int>();

                    if (!preConnections.ContainsKey(id))
                        preConnections[id] = connections;
                }

                foreach (var conn in ExclusiveConnections)
                {
                    var d = conn.Item1;
                    var od = Directions.opposite[d];
                    var id = new XXHash().Append(conn.Item2).Append(conn.Item1);

                    if (preConnections[id].Contains(conn.Item3))
                    {
                        Connections[d][conn.Item2].SortedInsertion(conn.Item3);
                        Connections[od][conn.Item3].SortedInsertion(conn.Item2);
                    }
                }

                /// Banned connections ///

                var BannedConnections = GenerateCustomConnections(repo.bannedConnections, context);

                foreach (var conn in BannedConnections)
                {
                    var d = conn.Item1;
                    var od = Directions.opposite[conn.Item1];

                    Connections[d][conn.Item2].Remove(conn.Item3);
                    Connections[od][conn.Item3].Remove(conn.Item2);
                }

                Profiling.LogAndRemoveTimer($"time to generate connections for {context.blocks.Count} ", generate_blocks_pk);
            }

            private void ConnectBetweenLayers(ConnectingContext context)
            {
                upperBlocks = new Dictionary<int, List<int>>();

                foreach (var pair in context.baseUpperBlocks)
                {
                    if (!context.HasAgVariants(pair.Key))
                        continue;

                    var bBlocks = context.GetAGVariants(pair.Key, true);

                    foreach (var uKey in pair.Value)
                    {
                        if (!context.HasAgVariants(uKey.Item1))
                            continue;

                        var uBlocks = context.GetAGVariants(uKey.Item1, true);

                        foreach (var bBlock in bBlocks)
                            foreach (var uBlock in uBlocks)
                                if (uKey.Item2 == BlocksResolve.AllToAll ||
                                    ActionsUtility.AreEquals(bBlock.Item2, uBlock.Item2))
                                {
                                    if (!upperBlocks.ContainsKey(context.BlocksHash.GetIndex(bBlock.Item1)))
                                        upperBlocks[context.BlocksHash.GetIndex(bBlock.Item1)] = new List<int>();
                                    upperBlocks[context.BlocksHash.GetIndex(bBlock.Item1)].Add(context.BlocksHash.GetIndex(uBlock.Item1));
                                }
                    }
                }
            }

            private void Submit(ConnectingContext context)
            {
                Blueprints = new List<BlockGOBlueprint>();
                weights = new List<float>(context.blocks.Count);
                blocksPlacement = new List<BlockPlacement>(context.blocks.Count);
                blocksPerWeightGroup = new List<int>[context.weightGroups.Count];

                for (int i = 0; i < context.weightGroups.Count; i++)
                    blocksPerWeightGroup[i] = new List<int>();

                for (int i = 0; i < context.blocks.Count; i++)
                {
                    var block = context.blocks[i];
                    Blueprints.Add(new BlockGOBlueprint(block.gameObject, block.actions));

                    weights.Add(block.weight);
                    int weightGroupIndex = context.weightGroups.GetList().FindIndex((g) => g.GetHashCode() == block.weightGroup);
                    blocksPerWeightGroup[weightGroupIndex].Add(i);

                    blocksPlacement.Add(block.layerSettings.placement);
                }

                BlocksHash = context.BlocksHash;

                groups = context.groups;
                weightGroups = context.weightGroups;

                layerPartioner = context.layerPartioner;
                groupStartIndex = context.groupStartIndex;

            }

            private void GenerateGroupCache()
            {
                Profiling.StartTimer(groups_counter_pk);

                groupCounter = GenerateGroupCounter(0);

                Profiling.LogAndRemoveTimer("time to generate groups counter ", groups_counter_pk);
            }

            private List<(int, int, int)> GenerateCustomConnections(List<Connection> connections, ConnectingContext context)
            {
                var output = new List<(int, int, int)>();

                foreach (var conn in connections)
                {
                    if (!BlockUtility.IsActive(conn.a.block) || !BlockUtility.IsActive(conn.b.block)) continue;

                    var a_vars = context.GetAGVariants(conn.a.block.GetHashCode());
                    var b_vars = context.GetAGVariants(conn.b.block.GetHashCode());

                    foreach (var a_var in a_vars)
                    {
                        var a_side = ActionsUtility.TransformFace(conn.a.d, a_var.Item2);
                        foreach (var b_var in b_vars)
                        {
                            var b_side = ActionsUtility.TransformFace(conn.b.d, b_var.Item2);
                            if (a_side == Directions.opposite[b_side])
                                output.Add((a_side, context.BlocksHash.GetIndex(a_var.Item1),
                                    context.BlocksHash.GetIndex(b_var.Item1)));
                        }
                    }
                }

                return output;
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

            public void Dispose()
            {
                MeshResource?.Dispose();
            }
        }
    }
}