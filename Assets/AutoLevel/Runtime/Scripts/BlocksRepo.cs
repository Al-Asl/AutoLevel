using UnityEngine;
using System.Collections.Generic;
using System.Collections;


[System.Serializable]
public struct BlockResources
{
    public Mesh mesh;
    public Material material;
}

[System.Serializable]
public class VariantGroup
{
    [System.Serializable]
    public class GroupActions { public List<VariantAction> actions = new List<VariantAction>(); }

#if UNITY_EDITOR
    public string name;
#endif

    public List<GroupActions> groupActions = new List<GroupActions>();
    public List<BlockAsset> targets = new List<BlockAsset>();
}

[AddComponentMenu("AutoLevel/Blocks Repo")]
public class BlocksRepo : MonoBehaviour
{
    [SerializeField]
    public List<string> blockGroups = new List<string>();
    [SerializeField]
    private List<VariantGroup> variantGroups = new List<VariantGroup>();

    public int BlocksCount => Resources.Count;
    public List<int[]>[] Connections { get; private set; }
    public BiDirectionalList<int> BlocksHash { get; private set; }
    public List<BlockResources> Resources { get; private set; }
    public List<BlockConnection> BlockConnections { get; private set; }
    public List<List<int>> BlocksPerGroup { get; private set; }
    public List<float> Weights { get; private set; }
    private List<float> baseWeights;

    public int GroupsCount => GroupsNames.Count;
    /// <summary>
    /// [d][ga][gb] => ga_countlist
    /// </summary>
    public int[][][][] groupCounter { get; private set; }
    public List<string> GroupsNames { get; private set; }
    public BiDirectionalList<int> GroupsHash { get; set; }
    public List<float> groupWeights { get; private set; }

    public const string EMPTY_GROUP = "Empty";
    public const string SOLID_GROUP = "Solid";
    public const string BASE_GROUP = "Base";

    static int groups_counter_tk = "block_repo_groups_counter".GetHashCode();
    static int generate_blocks_tk = "block_repo_generate_blocks".GetHashCode();

    private void OnEnable()
    {
        Generate();
    }

    private void OnDisable()
    {
        Clear();
    }

    public void Regenerate()
    {
        Clear();
        Generate();
    }
    public void Generate()
    {
        GenerateGroupData();
        GenerateBlockData();
        GenerateConnections();
        GenerateGroupCounter();
        GenerateGroupsWeights();
    }
    public void Clear()
    {
        ClearBlockData();
    }

    public BlockResources GetBlock(int hash) => Resources[BlocksHash.GetIndex(hash)];

    public void OverrideGroupWeights(List<float> groupOverride)
    {
        for (int i = 0; i < Weights.Count; i++)
            Weights[i] = baseWeights[i];

        int[] setCounter = new int[BlocksCount];
        for (int i = 0; i < GroupsCount; i++)
        {
            var weight = groupOverride[i];
            if (weight < 0) continue;
            var blocks = BlocksPerGroup[i];
            for (int j = 0; j < blocks.Count; j++)
            {
                var b = blocks[j];
                var set = setCounter[b];
                if(set++ == 0)
                    Weights[b] = weight;
                else
                    Weights[b] = (weight + Weights[b]*(set - 1))/set;
                setCounter[b] = set;
            }
        }
        GenerateGroupsWeights();
    }

    public void IterateGroup(int groupHash, System.Action<int> excute)
    {
        var group = BlocksPerGroup[GroupsHash.GetIndex(groupHash)];
        for (int j = 0; j < group.Count; j++)
            excute(group[j]);
    }
    public void IterateGroups(List<int> groups,System.Action<int> excute)
    {
        for (int i = 0; i < groups.Count; i++)
            IterateGroup(groups[i], excute);
    }
    public GroupsEnumerator GetGroupsEnumerator(List<int> groups) => new GroupsEnumerator(groups, this);
    public GroupsEnumerator GetNRGroupsEnumerator(List<int> groups) => new GroupsEnumeratorNR(groups, this);

    private void GenerateGroupData()
    {
        GroupsNames = new List<string>()
        {
            EMPTY_GROUP,
            SOLID_GROUP,
            BASE_GROUP
        };
        GroupsNames.AddRange(blockGroups);

        GroupsHash = new BiDirectionalList<int>();
        for (int i = 0; i < GroupsNames.Count; i++)
            GroupsHash.Add(GroupsNames[i].GetHashCode());
    }
    private void GenerateBlockData()
    {
        var assets = GetComponentsInChildren<BlockAsset>();

        Resources = new List<BlockResources>();
        BlocksHash = new BiDirectionalList<int>();
        BlockConnections = new List<BlockConnection>();
        baseWeights = new List<float>();
        BlocksPerGroup = new List<List<int>>();
        for (int i = 0; i < GroupsCount; i++)
            BlocksPerGroup.Add(new List<int>());

        List<(GameObject, BlockVariant, List<int>)> AllVariants = new List<(GameObject, BlockVariant, List<int>)>()
        {
            //Built-in
            (
            null,
            new BlockVariant()
            {
                fill = 0,
                weight = 1f
            },
            new List<int>(){ GroupsHash[0] }),
            (
            null,
            new BlockVariant()
            {
                fill = 255,
                weight = 1f
            },
            new List<int>(){ GroupsHash[1] }),
        };
        //from assets
        BlockAsset.IterateVariants(assets, (varRef) =>
        {
            AllVariants.Add((varRef.blockAsset.gameObject, varRef.variant, varRef.blockAsset.groups));
        });
        //from variant group
        for (int i = 0; i < variantGroups.Count; i++)
        {
            var varGroup = variantGroups[i];
            var targets = varGroup.targets;
            for (int j = 0; j < varGroup.groupActions.Count; j++)
            {
                var actions = varGroup.groupActions[j].actions;
                BlockAsset.IterateVariants(targets, (varRef) =>
                {
                    var asset = varRef.blockAsset;
                    var variant = new BlockVariant(varRef.variant);
                    variant.ApplyActions(actions);

                    AllVariants.Add((varRef.blockAsset.gameObject, variant, varRef.blockAsset.groups));

                }, includeInactive: false);
            }
        }

        for (int i = 0; i < AllVariants.Count; i++)
        {
            var item = AllVariants[i];
            var blockData = BlockAsset.GetBlockData(item.Item1, item.Item2);
            var hash = BlockAsset.GetBlockDataHash(item.Item1, item.Item2);

            BlocksHash.Add(hash);
            Resources.Add(blockData.resource);
            BlockConnections.Add(blockData.connections);
            baseWeights.Add(blockData.weight);

            var groups = item.Item3;
            for (int j = 0; j < groups.Count; j++)
                BlocksPerGroup[GroupsHash.GetIndex(groups[j])].Add(i);
        }

        Weights = new List<float>();
        Weights.AddRange(baseWeights);
    }
    private void GenerateConnections()
    {
        Profiling.StartTimer(generate_blocks_tk);
        Connections = ConnectionsUtility.GetAdjacencyList(BlockConnections);
        Profiling.LogAndRemoveTimer($"time to generate connections of {Resources.Count} ", generate_blocks_tk);
    }
    private void GenerateGroupCounter()
    {
        Profiling.StartTimer(groups_counter_tk);

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

        Profiling.LogAndRemoveTimer("time to generate groups counter ", groups_counter_tk);
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

    private void ClearBlockData()
    {
        if (Resources == null)
            return;

        for (int i = 0; i < Resources.Count; i++)
        {
            var mesh = Resources[i].mesh;
            if (mesh == null)
                continue;

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                DestroyImmediate(mesh);
            else
#endif
                Destroy(mesh);
        }
    }

    class GroupsEnumeratorNR : GroupsEnumerator
    {
        bool[] occurrence;

        public GroupsEnumeratorNR(List<int> groups, BlocksRepo repo) : base(groups, repo)
        {
            occurrence = new bool[repo.BlocksCount];
        }

        public override bool MoveNext()
        {
            while (groupBlocks == null || ++blockIndex >= groupBlocks.Count)
            {
                if (++groupIndex >= groups.Count)
                    return false;
                groupBlocks = repo.BlocksPerGroup[repo.GroupsHash.GetIndex(groups[groupIndex])];
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

    public class GroupsEnumerator : IEnumerator<int> , IEnumerable<int>
    {
        public List<int> groups;
        protected BlocksRepo repo;

        protected List<int> groupBlocks;
        protected int groupIndex = -1;
        protected int blockIndex = -1;

        public GroupsEnumerator(List<int> groups, BlocksRepo repo)
        {
            this.groups = groups;
            this.repo = repo;
        }

        public int Current => groupBlocks[blockIndex];
        object IEnumerator.Current => Current;

        public void Dispose() { }

        public IEnumerator<int> GetEnumerator() => this;

        public virtual bool MoveNext()
        {
            while(groupBlocks == null || ++blockIndex >= groupBlocks.Count)
            {
                if (++groupIndex >= groups.Count)
                    return false;
                groupBlocks = repo.BlocksPerGroup[repo.GroupsHash.GetIndex(groups[groupIndex])];
                blockIndex = -1;
            }
            return true;
        }

        public virtual void Reset()
        {
            groupIndex = -1;
            blockIndex = -1;
            groupBlocks = null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }
}