using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AutoLevel
{
    public partial class BlocksRepo : MonoBehaviour
    {
        public class GroupsEnumerator : IEnumerator<int>, IEnumerable<int>
        {
            protected IEnumerator<int> groups;
            protected Runtime repo;

            protected List<int> groupBlocks;
            protected int blockIndex = -1;

            public GroupsEnumerator(InputWaveCell wave, Runtime repo)
            {
                this.repo = repo;
                groups = null;
                SetWave(wave);
            }

            public void SetWave(InputWaveCell wave) => groups = wave.GroupsEnum(repo.GroupsCount).GetEnumerator();

            public int Current => groupBlocks[blockIndex];
            object IEnumerator.Current => Current;

            public void Dispose() { }

            public IEnumerator<int> GetEnumerator() => this;

            public virtual bool MoveNext()
            {
                while (groupBlocks == null || ++blockIndex >= groupBlocks.Count)
                {
                    if (!groups.MoveNext())
                        return false;
                    groupBlocks = repo.BlocksPerGroup[groups.Current];
                    blockIndex = -1;
                }
                return true;
            }

            public virtual void Reset()
            {
                blockIndex = -1;
                groupBlocks = null;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new System.NotImplementedException();
            }
        }

        class GroupsDistinctEnumerator : GroupsEnumerator
        {
            bool[] occurrence;

            public GroupsDistinctEnumerator(InputWaveCell wave, Runtime repo) : base(wave, repo)
            {
                occurrence = new bool[repo.BlocksCount];
            }

            public override bool MoveNext()
            {
                while (groupBlocks == null || ++blockIndex >= groupBlocks.Count)
                {
                    if (!groups.MoveNext())
                        return false;
                    groupBlocks = repo.BlocksPerGroup[groups.Current];
                    blockIndex = -1;
                }
                if (occurrence[Current])
                    return MoveNext();
                occurrence[Current] = true;
                return true;
            }

            public override void Reset()
            {
                base.Reset();
                occurrence.Fill(() => false);
            }
        }

        public class Runtime : System.IDisposable
        {
            private class GameObjectTemplate
            {
                public GameObject gameObject;
                public List<BlockAction> actions;

                public GameObjectTemplate(GameObject gameObject, List<BlockAction> actions)
                {
                    this.gameObject = gameObject;
                    this.actions = actions;
                }

                public GameObject Create()
                {
                    if (gameObject != null)
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
                    else
                        return null;
                }

                public void AplyActions(GameObject go, List<BlockAction> actions)
                {
                    foreach (var action in actions)
                        ActionsUtility.ApplyAction(go, action);
                }


            }

            private class BlocksGameObjectGenerator
            {
                private Dictionary<GameObject, GameObject> map =
                    new Dictionary<GameObject, GameObject>();
                private List<Component> components = new List<Component>();
                public GameObject root { get; private set; }

                public BlocksGameObjectGenerator()
                {
                    root = new GameObject("blocks_gameobjects");
                    root.hideFlags = HideFlags.HideAndDontSave;
                }

                public GameObject Generate(GameObject original)
                {
                    if (original == null)
                        return null;

                    if (map.ContainsKey(original))
                        return map[original];
                    else
                    {
                        var go = Instantiate(original);
                        go.name = original.name;

                        RemoveComponenet<BlockAsset>(go);
                        RemoveComponenet<MeshFilter>(go);
                        RemoveComponenet<MeshRenderer>(go);

                        Strip(go);
                        if (go != null)
                        {
                            map[original] = go;
                            go.hideFlags = HideFlags.HideAndDontSave;
                            go.SetActive(false);
                            go.transform.SetParent(root.transform);
                        }

                        return go;
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

                            SafeDestroy(go);

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
                        SafeDestroy(com);
                }
            }

            private class BlockAssetGenerator
            {
                List<ActionsGroup> actionsGroups;
                Dictionary<int, int> actionsToIndex;

                public BlockAssetGenerator(List<ActionsGroup> actionsGroups)
                {
                    this.actionsGroups= actionsGroups;

                    actionsToIndex = new Dictionary<int, int>();
                    for (int i = 0; i < actionsGroups.Count; i++)
                        actionsToIndex.Add(actionsGroups[i].name.GetHashCode(), i);
                }

                public void Generate(IEnumerable<BlockAsset> assets, List<IBlock> blocks)
                {
                    foreach (var block in BlockAsset.GetBlocksEnum(assets))
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
                                var group = this.actionsGroups[actionsToIndex[groupHash]];

                                foreach (var actionsGroup in group.groupActions)
                                {
                                    var newBlock = block.CreateCopy();
                                    newBlock.ApplyActions(actionsGroup.actions);
                                    blocks.Add(newBlock);
                                }
                            }
                        }
                        blocks.Add(block);
                    }
                }
            }

            private class BigBlockAssetGenerator
            {
                private List<ActionsGroup> actionsGroups;
                private Dictionary<int, int> actionsToIndex;
                private Dictionary<int, int> connectionsMap;
                private LinkedList<int> ids;

                public BigBlockAssetGenerator(List<ActionsGroup> actionsGroups, LinkedList<int> ids)
                {
                    this.actionsGroups = actionsGroups;
                    this.ids = ids;
                    connectionsMap = new Dictionary<int, int>();

                    actionsToIndex = new Dictionary<int, int>();
                    for (int i = 0; i < actionsGroups.Count; i++)
                        actionsToIndex.Add(actionsGroups[i].name.GetHashCode(), i);
                }

                public void Generate(IEnumerable<BigBlockAsset> assets, List<IBlock> blocks)
                {

                    foreach (var bigBlock in assets)
                    {
                        var data = bigBlock.data;
                        connectionsMap.Clear();
                        var internalConnections = ConnectionsUtility.GetInternalConnections(data);

                        //generate the default big block
                        foreach (var conn in internalConnections)
                            connectionsMap[conn] = ConnectionsUtility.GetAndUpdateNextId(ids);
                        GenerateBlocksFromBigBlockAsset(data, new List<BlockAction>(), blocks);

                        //generate the states from the actions groups
                        foreach (var group in bigBlock.actionsGroups)
                        {
                            var actionsGroups = this.actionsGroups[actionsToIndex[group]].groupActions;

                            foreach (var actionsGroup in actionsGroups)
                            {
                                foreach (var conn in internalConnections)
                                    connectionsMap[conn] = ConnectionsUtility.GetAndUpdateNextId(ids);

                                GenerateBlocksFromBigBlockAsset(data, actionsGroup.actions,blocks);
                            }
                        }
                    }
                }

                private void GenerateBlocksFromBigBlockAsset (Array3D<AssetBlock> bigBlock,List<BlockAction> actions, List<IBlock> blocks)
                {
                    var count = bigBlock.Size.x * bigBlock.Size.y * bigBlock.Size.z;
                    var bounds = new BoundsInt(Vector3Int.zero, bigBlock.Size);

                    Dictionary<(Vector3Int, int), int> IdsMap = new Dictionary<(Vector3Int, int), int>();

                    foreach (var index in SpatialUtil.Enumerate(bigBlock.Size))
                    {
                        var block = bigBlock[index.z, index.y, index.x];

                        if (block.blockAsset == null)
                            continue;

                        var newBlock = block.CreateCopy();
                        var baseIds = newBlock.baseIds;
                        for (int d = 0; d < 6; d++)
                        {
                            var code = baseIds[d];
                            if (code == 0)
                            {
                                var nIndex = index + Directions.delta[d];
                                if (bounds.Contains(nIndex) && bigBlock[nIndex].blockAsset != null)
                                {
                                    if (IdsMap.ContainsKey((index, d)))
                                        baseIds[d] = IdsMap[(index, d)];
                                    else
                                    {
                                        var newId = ConnectionsUtility.GetAndUpdateNextId(ids);
                                        IdsMap[(nIndex, Directions.opposite[d])] = newId;
                                        baseIds[d] = newId;
                                    }
                                }
                            }
                            else if (connectionsMap.ContainsKey(code))
                                baseIds[d] = connectionsMap[code];
                        }
                        newBlock.weight /= count;
                        newBlock.baseIds = baseIds;
                        newBlock.ApplyActions(actions);
                        blocks.Add(newBlock);
                    }
                }
            }

            private Transform root;
            private Transform templates_root;
            private List<ActionsGroup> actionsGroups;

            private BiDirectionalList<int> BlocksHash;
            private List<BlockResources> Resources;
            private List<GameObjectTemplate> gameObjecststemplates = new List<GameObjectTemplate>();

            private List<float> Weights;
            private List<float> baseWeights;


            private List<string> GroupsNames;
            private BiDirectionalList<int> groupsHash;
            public List<List<int>> BlocksPerGroup { get; private set; }
            public List<float> groupWeights { get; private set; }


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
            public GameObject CreateGameObject(int index) => gameObjecststemplates[index].Create();
            public float GetBlockWeight(int blockIndex) => Weights[blockIndex];

            public int GroupsCount => GroupsNames.Count;
            public string GetGroupName(int index) => GroupsNames[index];
            public bool ContainsGroup(int hash) => groupsHash.Contains(hash);
            public int GetGroupHash(int index) => groupsHash[index];
            public int GetGroupIndex(int hash) => groupsHash.GetIndex(hash);
            public int GetGroupIndex(string name) => groupsHash.GetIndex(name.GetHashCode());
            public IEnumerable<int> GetAllGroups() => groupsHash;

            public GroupsEnumerator GetGroupsEnumerable(InputWaveCell wave) => new GroupsEnumerator(wave, this);
            public GroupsEnumerator GetDistinctGroupsEnumerable() => new GroupsDistinctEnumerator(default, this);

            public Runtime(Transform root, List<string> GroupsNames, List<ActionsGroup> actionsGroups)
            {
                this.GroupsNames = GroupsNames;
                this.actionsGroups = actionsGroups;
                this.root = root;

                List<SideIds> BlockConnections = new List<SideIds>();

                GenerateGroupData();
                GenerateBlockData(BlockConnections);
                GenerateConnections(BlockConnections);
                GenerateGroupCounter();
                GenerateGroupsWeights();
            }

            public void OverrideGroupsWeights(List<float> groupOverride)
            {
                for (int i = 0; i < Weights.Count; i++)
                    Weights[i] = baseWeights[i];

                MinGroupsWeightsOverride(groupOverride);

                GenerateGroupsWeights();
            }
            private void MinGroupsWeightsOverride(List<float> groupOverride)
            {
                for (int i = 0; i < GroupsCount; i++)
                {
                    var weight = groupOverride[i];
                    if (weight < 0) continue;
                    var blocks = BlocksPerGroup[i];
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        var b = blocks[j];
                        Weights[b] = float.MaxValue;
                    }
                }

                for (int i = 0; i < GroupsCount; i++)
                {
                    var weight = groupOverride[i];
                    if (weight < 0) continue;
                    var blocks = BlocksPerGroup[i];
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        var b = blocks[j];
                        Weights[b] = Mathf.Min(Weights[b], weight);
                    }
                }
            }
            private void AvargeGroupsWeightsOverride(List<float> groupOverride)
            {
                for (int i = 0; i < GroupsCount; i++)
                {
                    var weight = groupOverride[i];
                    if (weight < 0) continue;
                    var blocks = BlocksPerGroup[i];
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        var b = blocks[j];
                        Weights[b] = 0;
                    }
                }

                int[] setCounter = new int[BlocksCount];
                for (int i = 0; i < GroupsCount; i++)
                {
                    var weight = groupOverride[i];
                    if (weight < 0) continue;
                    var blocks = BlocksPerGroup[i];
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        var b = blocks[j];
                        setCounter[b]++;
                        Weights[b] += weight;
                    }
                }
                for (int i = 0; i < BlocksCount; i++)
                {
                    var set = setCounter[i];
                    if (set > 1)
                        Weights[i] /= setCounter[i];
                }
            }

            private void GenerateGroupData()
            {
                groupsHash = new BiDirectionalList<int>();
                for (int i = 0; i < GroupsNames.Count; i++)
                    groupsHash.Add(GroupsNames[i].GetHashCode());
            }
            private void GenerateBlockData(List<SideIds> BlockConnections)
            {
                Resources = new List<BlockResources>();
                gameObjecststemplates = new List<GameObjectTemplate>();
                BlocksHash = new BiDirectionalList<int>();
                baseWeights = new List<float>();
                BlocksPerGroup = new List<List<int>>();
                for (int i = 0; i < GroupsCount; i++)
                    BlocksPerGroup.Add(new List<int>());

                var actionsToIndex = new Dictionary<int, int>();
                for (int i = 0; i < actionsGroups.Count; i++)
                    actionsToIndex.Add(actionsGroups[i].name.GetHashCode(), i);

                List<IBlock> allBlocks = new List<IBlock>()
                {
                    //Built-in
                    new StandalnoeBlock(new List<int>(){ groupsHash[0] },0,1f),
                    new StandalnoeBlock(new List<int>(){ groupsHash[1] },255,1f),
                };
                var baseBlocks = BlockAsset.GetBlocksEnum(root.GetComponentsInChildren<BlockAsset>());

                var blockAssetGenerator = new BlockAssetGenerator(actionsGroups);
                blockAssetGenerator.Generate(root.GetComponentsInChildren<BlockAsset>(), allBlocks);

                var ids = new LinkedList<int>(ConnectionsUtility.GetListOfSortedIds(baseBlocks));
                var bigBlockAssetGenerator = new BigBlockAssetGenerator(actionsGroups, ids);
                bigBlockAssetGenerator.Generate(root.GetComponentsInChildren<BigBlockAsset>(),allBlocks);

                var gameObjectsGenerator = new BlocksGameObjectGenerator();
                templates_root = gameObjectsGenerator.root.transform;

                foreach (var block in allBlocks)
                {
                    BlocksHash.Add(block.GetHashCode());
                    Resources.Add(block.blockResources);
                    gameObjecststemplates.Add(
                        new GameObjectTemplate(gameObjectsGenerator.Generate(block.gameObject),
                        block.actions));
                    BlockConnections.Add(block.compositeIds);
                    baseWeights.Add(block.weight);

                    var groups = block.groups;
                    for (int j = 0; j < groups.Count; j++)
                        BlocksPerGroup[groupsHash.GetIndex(groups[j])].Add(Resources.Count - 1);
                }

                Weights = new List<float>();
                Weights.AddRange(baseWeights);
            }
            private void GenerateConnections(List<SideIds> BlockConnections)
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
                        var group_a = BlocksPerGroup[i];
                        var counter_d_a = counter_d[i];
                        for (int j = 0; j < gc; j++)
                        {
                            var group_b = BlocksPerGroup[j];
                            var counter = new int[group_a.Count];

                            for (int a = 0; a < group_a.Count; a++)
                            {
                                var conn = conn_d[group_a[a]];
                                var count = 0;
                                for (int b = 0; b < group_b.Count; b++)
                                    if (conn.BinarySearch(group_b[b]) != -1)
                                        count++;
                                counter[a] = count;
                            }

                            counter_d_a[j] = counter;
                        }
                    }
                }

                Profiling.LogAndRemoveTimer("time to generate groups counter ", groups_counter_pk);
            }
            private void GenerateGroupsWeights()
            {
                if (groupWeights == null)
                    groupWeights = new List<float>();
                groupWeights.Clear();

                for (int i = 0; i < GroupsCount; i++)
                {
                    var group = BlocksPerGroup[i];
                    var weight = 0f;
                    for (int j = 0; j < group.Count; j++)
                        weight += Weights[group[j]];
                    groupWeights.Add(weight);
                }
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

                    SafeDestroy(mesh);
                }

                SafeDestroy(templates_root.gameObject);
            }

            private static void SafeDestroy<T>(T target) where T : Object
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                    DestroyImmediate(target, false);
                else
#endif
                    Destroy(target);
            }
        }
    }
}