using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public partial class BlocksRepo : MonoBehaviour
    {
        public class Runtime : System.IDisposable
        {
            private class BlockGOTemplate
            {
                public GameObject gameObject;
                public List<BlockAction> actions;

                public BlockGOTemplate(GameObject gameObject, List<BlockAction> actions)
                {
                    this.gameObject = gameObject;
                    this.actions = actions;
                }

                public GameObject Create()
                {
                    var go = Instantiate(gameObject);
                    go.name = gameObject.name;
                    go.SetActive(true);
                    go.transform.position = Vector3.zero;
                    AplyActions(go, actions);

                    var root = new GameObject(gameObject.name);
                    go.transform.SetParent(root.transform);
                    return root;
                }

                public void AplyActions(GameObject go, List<BlockAction> actions)
                {
                    foreach (var action in actions)
                        ActionsUtility.ApplyAction(go, action);
                }
            }

            private class BlockGOTemplateGenerator
            {
                private Dictionary<GameObject, GameObject> map =
                    new Dictionary<GameObject, GameObject>();
                private List<Component> components = new List<Component>();
                public GameObject root { get; private set; }

                public BlockGOTemplateGenerator()
                {
                    root = new GameObject("blocks_gameobjects");
                    root.hideFlags = HideFlags.HideAndDontSave;
                }

                public BlockGOTemplate Generate(GameObject original,List<BlockAction> actions)
                {
                    if (original == null)
                        return null;

                    if (map.ContainsKey(original))
                        return map[original] == null ? null : new BlockGOTemplate(map[original], actions);
                    else
                    {
                        var go = Instantiate(original);
                        go.name = original.name;

                        RemoveComponenet<BlockAsset>(go);
                        RemoveComponenet<MeshFilter>(go);
                        RemoveComponenet<MeshRenderer>(go);

                        Strip(go);
                        map[original] = go;

                        if (go == null)
                            return null;

                        go.hideFlags = HideFlags.HideAndDontSave;
                        go.SetActive(false);
                        go.transform.SetParent(root.transform);

                        return new BlockGOTemplate(go, actions);
                    }
                }

                private void Strip(GameObject go)
                {
                    if (go.transform.childCount == 0)
                    {
                        if (!HaveComponenets(go))
                        {
                            GameObject parent = null;
                            if (go.transform.parent != null && go.transform.parent.childCount == 1)
                                parent = go.transform.parent.gameObject;

                            GameObjectUtil.SafeDestroy(go);

                            if (parent != null)
                                Strip(parent);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < go.transform.childCount; i++)
                            Strip(go.transform.GetChild(i).gameObject);
                    }
                }

                private bool HaveComponenets(GameObject gameObject)
                {
                    gameObject.GetComponents(components);
                    return components.Count > 1;
                }

                private void RemoveComponenet<T>(GameObject gameObject) where T : Component
                {
                    var com = gameObject.GetComponent<T>();
                    if (com != null)
                        GameObjectUtil.SafeDestroy(com);
                }
            }

            private class BlockAssetGenerator
            {
                List<ActionsGroup> actionsGroups;
                Dictionary<int, int> actionsToIndex;

                public BlockAssetGenerator(List<ActionsGroup> actionsGroups, Dictionary<int, int> actionsToIndex)
                {
                    this.actionsGroups= actionsGroups;
                    this.actionsToIndex= actionsToIndex;
                }

                public void Generate(IEnumerable<BlockAsset> blockAssets, List<IBlock> outputList)
                {
                    foreach (var block in BlockAsset.GetBlocksEnum(blockAssets))
                    {
                        if (block.bigBlock != null)
                            continue;

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
                private List<ActionsGroup> actionsGroups;
                private Dictionary<int, int> actionsToIndex;
                private Dictionary<int, int> connectionsMap;
                private ConnectionsUtility.IDGenerator idGen;

                public BigBlockAssetGenerator(List<ActionsGroup> actionsGroups, 
                    Dictionary<int, int> actionsToIndex,
                    ConnectionsUtility.IDGenerator idGen)
                {
                    this.actionsGroups = actionsGroups;
                    this.actionsToIndex = actionsToIndex;
                    this.idGen = idGen;
                    connectionsMap = new Dictionary<int, int>();
                }

                public void Generate(IEnumerable<BigBlockAsset> assets, List<IBlock> blocks)
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

                        GenerateBlocks(bigBlock, new List<BlockAction>(), blocks);

                        //generate the states from the actions groups
                        foreach (var group in bigBlock.actionsGroups)
                        {
                            var actionsGroups = this.actionsGroups[actionsToIndex[group]].groupActions;

                            foreach (var actionsGroup in actionsGroups)
                            {
                                foreach (var conn in internalConnections)
                                    connectionsMap[conn] = idGen.GetNext();

                                GenerateBlocks(bigBlock, actionsGroup.actions, blocks);
                            }
                        }
                    }
                }

                private void GenerateBlocks(BigBlockAsset bigBlock, List<BlockAction> actions, List<IBlock> blocks)
                {
                    var srcData = bigBlock.data;
                    var dstData = new Array3D<SList<StandalnoeBlock>>(srcData.Size);
                    foreach (var index in SpatialUtil.Enumerate(srcData.Size))
                    {
                        var src = srcData[index];
                        if (src.IsEmpty) continue;

                        var dst = new SList<StandalnoeBlock>();
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
                            SetID(A, i, conn.Item3,map[id]);
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
                            blocks.Add(block);
                        }
                    }
                }

                private void SetID(SList<StandalnoeBlock> array, int index, int d, int id)
                {
                    var block = array[index];
                    var baseIds = block.baseIds;
                    baseIds[d] = id;
                    block.baseIds = baseIds;
                    array[index] = block;
                }
            }

            private Transform root;
            private Transform templates_root;
            private List<ActionsGroup> actionsGroups;

            private BiDirectionalList<int> BlocksHash;
            private List<BlockResources> Resources;
            private List<BlockGOTemplate> gameObjecstsTemplates = new List<BlockGOTemplate>();

            private List<int> groupStartIndex;

            private List<float> weights;
            private List<int>[] blocksPerWeightGroup;

            private BiDirectionalList<string> groups;
            private BiDirectionalList<string> weightGroups;

            public List<int[]>[] Connections { get; private set; }
            /// <summary>
            /// [d][ga][gb] => ga_countlist
            /// </summary>
            public int[][][][] groupCounter { get; private set; }

            public int BlocksCount => Resources.Count;
            public bool ContainsBlock(int hash) => BlocksHash.Contains(hash);
            public int GetBlockHash(int index) => BlocksHash[index];
            public int GetBlockIndex(int hash) => BlocksHash.GetIndex(hash);
            public BlockResources GetBlockResourcesByHash(int hash) => Resources[BlocksHash.GetIndex(hash)];
            public BlockResources GetBlockResources(int index) => Resources[index];
            public GameObject CreateGameObject(int index)
            {
                var template = gameObjecstsTemplates[index];
                return template != null ? template.Create() : null;
            }
            public IEnumerable<float> GetBlocksWeight() => weights;

            /// Groups ///
            public int GroupsCount => groups.Count;
            public string GetGroupName(int index) => groups[index];
            public bool ContainsGroup(string name) => groups.GetList().Contains(name);
            public int GetGroupHash(int index) => groups[index].GetHashCode();
            public int GetGroupIndex(string name) => groups.GetIndex(name);
            public int GetGroupIndex(int hash) => groups.GetList().FindIndex((e) => e.GetHashCode() == hash);

            /// Weight Groups ///
            public int WeightGroupsCount => weightGroups.Count;
            public string GetWeightGroupName(int index) => weightGroups[index];
            public bool ContainsWeightGroup(string name) => weightGroups.GetList().Contains(name);
            public int GetWeightGroupHash(int index) => weightGroups[index].GetHashCode();
            public int GetWeightGroupIndex(string name) => weightGroups.GetIndex(name);
            public IEnumerable<int> GetBlocksPerWeightGroup(int group) => blocksPerWeightGroup[group];

            public Vector2Int GetGroupRange(int index)
            {
                return new Vector2Int(groupStartIndex[index],
                    index == groupStartIndex.Count - 1 ? BlocksCount : groupStartIndex[index + 1]);
            }

            public Runtime(Transform root, List<string> GroupsNames, List<string> WeightGroupsNames, List<ActionsGroup> actionsGroups)
            {
                groups = new BiDirectionalList<string>(GroupsNames);
                weightGroups = new BiDirectionalList<string>(WeightGroupsNames);

                this.actionsGroups = actionsGroups;
                this.root = root;

                List<ConnectionsIds> BlockConnections = new List<ConnectionsIds>();

                GenerateBlockData(BlockConnections);
                GenerateConnections(BlockConnections);
                GenerateGroupCounter();
            }

            private void GenerateBlockData(List<ConnectionsIds> BlockConnections)
            {
                Resources = new List<BlockResources>();
                gameObjecstsTemplates = new List<BlockGOTemplate>();
                BlocksHash = new BiDirectionalList<int>();
                weights = new List<float>();
                blocksPerWeightGroup = new List<int>[WeightGroupsCount];
                for (int i = 0; i < WeightGroupsCount; i++)
                    blocksPerWeightGroup[i] = new List<int>();

                var actionsToIndex = new Dictionary<int, int>();
                for (int i = 0; i < actionsGroups.Count; i++)
                    actionsToIndex.Add(actionsGroups[i].name.GetHashCode(), i);

                List<IBlock> allBlocks = new List<IBlock>()
                {
                    //Built-in
                    new StandalnoeBlock( GetGroupHash(0), GetWeightGroupHash(0).GetHashCode() ,0,1f),
                    new StandalnoeBlock( GetGroupHash(1), GetWeightGroupHash(1).GetHashCode() ,255,1f),
                };

                var blockAssets = root.GetComponentsInChildren<BlockAsset>();
                var bigBlockAssets = root.GetComponentsInChildren<BigBlockAsset>();

                var blockAssetGenerator = new BlockAssetGenerator(actionsGroups, actionsToIndex);
                blockAssetGenerator.Generate(blockAssets, allBlocks);

                var idGen = ConnectionsUtility.CreateIDGenerator(BlockAsset.GetBlocksEnum(blockAssets));
                var bigBlockAssetGenerator = new BigBlockAssetGenerator(actionsGroups, actionsToIndex, idGen);
                bigBlockAssetGenerator.Generate(bigBlockAssets, allBlocks);

                Dictionary<int, int> groupHashToIndex = HashToIndexLookup(groups);
                allBlocks.Sort((a, b) => groupHashToIndex[a.group].CompareTo(groupHashToIndex[b.group]));

                groupStartIndex = new List<int>(GroupsCount);
                var lastGroup = -1;
                for(int i = 0; i < allBlocks.Count; i++)
                {
                    var block = allBlocks[i];
                    while(groupHashToIndex[block.group] != lastGroup)
                    {
                        groupStartIndex.Add(i);
                        lastGroup++;
                    }
                }
                while(groups.Count != groupStartIndex.Count)
                    groupStartIndex.Add(allBlocks.Count - 1);

                var templateGenerator = new BlockGOTemplateGenerator();
                templates_root = templateGenerator.root.transform;

                Dictionary<int, int> wgHashToIndex = HashToIndexLookup(weightGroups);

                for(int i = 0; i < allBlocks.Count; i++)
                {
                    var block = allBlocks[i];
                    BlocksHash.Add(block.GetHashCode());
                    Resources.Add(block.blockResources);
                    gameObjecstsTemplates.Add(templateGenerator.Generate(block.gameObject,block.actions));

                    BlockConnections.Add(block.compositeIds);
                    weights.Add(block.weight);
                    blocksPerWeightGroup[wgHashToIndex[block.weightGroup]].Add(i);
                }

            }
            private void GenerateConnections(List<ConnectionsIds> BlockConnections)
            {
                Profiling.StartTimer(generate_blocks_pk);
                Connections = ConnectionsUtility.GetAdjacencyList(BlockConnections);
                Profiling.LogAndRemoveTimer($"time to generate connections of {Resources.Count} ", generate_blocks_pk);
            }
            private void GenerateGroupCounter()
            {
                Profiling.StartTimer(groups_counter_pk);

                var gc = GroupsCount;
                groupCounter = new int[6][][][];

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
                        var group_a = GetGroupRange(i);
                        var counter_d_a = counter_d[i];
                        for (int j = 0; j < gc; j++)
                        {
                            var group_b = GetGroupRange(j);
                            var counter = new int[group_a.y - group_a.x];

                            for (int a = 0; a < counter.Length; a++)
                            {
                                var conn = conn_d[group_a.x + a];
                                var count = 0;
                                for (int b = group_b.x; b < group_b.y ; b++)
                                    if (conn.BinarySearch(b) != -1)
                                        count++;
                                counter[a] = count;
                            }

                            counter_d_a[j] = counter;
                        }
                    }
                }

                Profiling.LogAndRemoveTimer("time to generate groups counter ", groups_counter_pk);
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
                if (Resources == null)
                    return;

                for (int i = 0; i < Resources.Count; i++)
                {
                    var mesh = Resources[i].mesh;
                    if (mesh == null)
                        continue;

                    GameObjectUtil.SafeDestroy(mesh);
                }

                GameObjectUtil.SafeDestroy(templates_root.gameObject);
            }
        }
    }
}