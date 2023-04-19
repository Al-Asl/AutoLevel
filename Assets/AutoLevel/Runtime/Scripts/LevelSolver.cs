using System;
using System.Collections.Generic;
using UnityEngine;
using static Directions;

[Serializable]
public class InputWaveBlock
{
    //still not sure if it's ok to change it to mask
    public List<int> groups = new List<int>();
}

public class LevelSolver
{
    class GroupInteractionHandler
    {
        private BlocksRepo repo;

        private BlocksRepo.GroupsEnumerator groupsEnum;
        private int[] tempA;

        public GroupInteractionHandler(BlocksRepo repo)
        {
            this.repo = repo;
            tempA = new int[repo.BlocksCount];
            groupsEnum = repo.GetNRGroupsEnumerator(null);
        }

        public void Process(List<int> groupsA, List<int> groupsB, int d, int[] output)
        {
            tempA.Fill(() => 0);
            groupsEnum.groups = groupsA;

            for (int a = 0; a < groupsA.Count; a++)
            {
                var g_a_index = repo.GroupsHash.GetIndex(groupsA[a]);
                var g_a_counter = repo.groupCounter[d][g_a_index];
                var blocks_a = repo.BlocksPerGroup[g_a_index];

                for (int b = 0; b < groupsB.Count; b++)
                {
                    var g_b_index = repo.GroupsHash.GetIndex(groupsB[b]);
                    var g_a_b_counter = g_a_counter[g_b_index];
                    for (int c = 0; c < g_a_b_counter.Length; c++)
                    {
                        tempA[blocks_a[c]] += g_a_b_counter[c];
                    }
                }

                //guard against duplicates in group a
                for (int i = 0; i < blocks_a.Count; i++)
                {
                    var b = blocks_a[i];
                    output[b] = Mathf.Max(output[b], tempA[b]);
                    tempA[b] = 0;
                }
            }

            //guard against duplicates in group b
            foreach (var b in repo.GetGroupsEnumerator(groupsB))
                tempA[b]++;
            var od = opposite[d];
            foreach (var b in groupsEnum)
            {
                var c = tempA[b];
                if (c > 0)
                {
                    if (c > 1)
                    {
                        var conn = repo.Connections[od][b];
                        for (int v = 0; v < conn.Length; v++)
                            output[conn[v]] -= c - 1;
                    }
                    tempA[c] = 0;
                }
            }
            groupsEnum.Reset();
        }
    }

    enum Result
    {
        Success,
        Fail,
        Ongoing
    }
    struct Possibility
    {
        public Vector3Int index;
        public int block;

        public Possibility(Vector3Int index, int block)
        {
            this.index = index;
            this.block = block;
        }

        public override string ToString() =>
            $"index : {index}, block index : {block}";
    }

    public BlocksRepo repo;
    public LevelData levelData;
    public Array3D<InputWaveBlock> inputWave;

    private BoundsInt solveBounds;

    private Vector3Int size;
    private Dictionary<int, int[]>[,,] wave;
    private float[,,] weights;
    private Stack<Possibility> stack;
    private GroupInteractionHandler interactionHandler;
    private IEnumerable<Vector3Int> SolverVolume => SpatialUtil.Enumerate(solveBounds.size);

    private float weightsSum;
    private float blocksCount;
    private int[] blocksCounter;

    public LevelSolver(Vector3Int size)
    {
        this.size = size;
        wave = new Dictionary<int, int[]>[size.z, size.y, size.x];
        weights = new float[size.z, size.y, size.x];
        stack = new Stack<Possibility>(size.x);

        foreach (var index in SpatialUtil.Enumerate(size))
            wave[index.z, index.y, index.x] = new Dictionary<int, int[]>();
    }

    public int Solve(BoundsInt bounds, int iteration = 10)
    {
        solveBounds = bounds;
        blocksCount = bounds.size.x* bounds.size.y* bounds.size.z;
        interactionHandler = new GroupInteractionHandler(repo);

        for (int t = 0; t < iteration; t++)
        {
            if (Run() == Result.Success)
            {
                foreach(var index in SolverVolume)
                {
                    var e = wave[index.z, index.y, index.x].GetEnumerator();
                    e.MoveNext();
                    var lIndex = index + solveBounds.min;
                    levelData.Blocks[lIndex.z, lIndex.y, lIndex.x] = repo.BlocksHash[e.Current.Key];
                }
                return t + 1;
            }
        }
        return 0;
    }

    private void Clear()
    {
        foreach(var index in SolverVolume)
            wave[index.z, index.y, index.x].Clear();
        stack.Clear();

        if (blocksCounter == null || blocksCounter.Length != repo.Resources.Count)
            blocksCounter = new int[repo.Resources.Count];
        blocksCounter.Fill(()=>0);
        for (int i = 0; i < repo.Weights.Count; i++)
            weightsSum += repo.Weights[i];
    }

    private void Fill()
    {
        if(inputWave == null ||inputWave.Size.x == 0 ||
            inputWave.Size.y == 0 || inputWave.Size.z == 0)
        {
            var weight = 0f;
            for (int i = 0; i < repo.Resources.Count; i++)
                weight += repo.Weights[i];

            foreach (var index in SolverVolume)
                weights[index.z, index.y, index.x] = weight;

            foreach(var index in SolverVolume)
            {
                var b = wave[index.z, index.y, index.x];
                for (int w = 0; w < repo.Resources.Count; w++)
                {
                    var conn = new int[6];
                    for (int d = 0; d < 6; d++)
                        conn[opposite[d]] = repo.Connections[d][w].Length;
                    b[w] = conn;
                }
            }
        }
        else
        {
            var AllGroupsList = new List<int>();
            AllGroupsList.AddRange(repo.GroupsHash);

            int[][] counter = new int[6][];
            for (int i = 0; i < 6; i++)
                counter[i] = new int[repo.BlocksCount];

            var groupsEnum = repo.GetNRGroupsEnumerator(null);

            foreach (var index in SolverVolume)
            {
                var lindex = index + solveBounds.min;
                var groups = inputWave[lindex.z, lindex.y, lindex.x].groups;
                if (groups.Count == 0)
                    groups = AllGroupsList;

                float weight = 0;
                for (int g = 0; g < groups.Count; g++)
                    weight += repo.groupWeights[repo.GroupsHash.GetIndex(groups[g])];
                weights[index.z, index.y, index.x] = weight;
            }


            foreach(var index in SolverVolume)
            {
                var lindex = index + solveBounds.min;
                var groups = inputWave[lindex.z, lindex.y, lindex.x].groups;
                if (groups.Count == 0)
                    groups = AllGroupsList;
                var outputWave = wave[index.z, index.y, index.x];

                groupsEnum.groups = groups;

                for (int d = 0; d < 6; d++)
                {
                    counter[d].Fill(() => 0);

                    var nIndex = index + delta[d];
                    if(OnBoundary(nIndex))
                    {
                        for (int b = 0; b < repo.BlocksCount; b++)
                            counter[d][b] = 1;
                        continue;
                    }
                    var nlIndex = lindex + delta[d];
                    var ngroups = inputWave[nlIndex.z, nlIndex.y, nlIndex.x].groups;
                    if (ngroups.Count == 0)
                        ngroups = AllGroupsList;

                    interactionHandler.Process(groups,ngroups, d, counter[d]);
                }

                foreach (var b in groupsEnum)
                {
                    int[] c = new int[6];
                    bool ban = false;
                    for (int d = 0; d < 6; d++)
                    {
                        var count = counter[d][b];
                        if (count == 0)
                        {
                            ban = true;
                            break;
                        }
                        c[opposite[d]] = count;
                    }

                    if (ban)
                        Ban(new Possibility(index, b));
                    else
                        outputWave[b] = c;
                }
                groupsEnum.Reset();
            }
        }
    }

    private Result Run()
    {
        Clear();
        Profiling.StartTimer("solver_fill");
        Fill();
        Profiling.LogAndRemoveTimer("filling time", "solver_fill");
        ValidateBoundary();

        while (true)
        {
            Propagate();
            var result = Observe();
            if (result != Result.Ongoing)
                return result;
        }
    }

    private Result Observe()
    {
        float minEntropy = 1E+3f;
        Vector3Int minIndex = -Vector3Int.one;

        foreach(var index in SolverVolume)
        {
            int posCount = wave[index.z, index.y, index.x].Count;
            float weight = weights[index.z, index.y, index.x];
            if (posCount == 0)
            {
                //TODO : Log State
                return Result.Fail;
            }

            float entropy = Mathf.Log(weight);
            if (posCount > 1 && entropy <= minEntropy)
            {
                float noise = 1E-4f * UnityEngine.Random.value;
                if (entropy + noise < minEntropy)
                {
                    minEntropy = entropy + noise;
                    minIndex = index;
                }
            }
        }

        if (minIndex == -Vector3Int.one)
            return Result.Success;

        var blocksSet = wave[minIndex.z, minIndex.y, minIndex.x];
        int[] blocks = new int[blocksSet.Count];
        {
            int i = 0;
            foreach (var index in blocksSet)
                blocks[i++] = index.Key;
        }
        var remain = StablePick(blocks);

        for (int i = 0; i < blocks.Length; i++)
        {
            if (i != remain)
                Ban(new Possibility(minIndex, blocks[i]));
        }

        return Result.Ongoing;
    }

    int StablePick(int[] blocks)
    {
        float invBCount = 1f / blocksCount;
        float invWeightsSum = 1f / weightsSum;

        float sum = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            var weight = repo.Weights[b];
            if (blocksCounter[b] * invBCount < weight * invWeightsSum)
                sum += repo.Weights[blocks[i]];
        }
        var r = UnityEngine.Random.value * sum;
        sum = 0;
        var lastIndex = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            var weight = repo.Weights[b];
            if (blocksCounter[b] * invBCount < weight * invWeightsSum)
            {
                sum += weight;
                lastIndex = i;
                if (sum > r)
                    return i;
            }
        }
        return lastIndex;
    }

    int Pick(int[] blocks)
    {
        float sum = 0;
        for (int i = 0; i < blocks.Length; i++)
            sum += repo.Weights[blocks[i]];
        var r = UnityEngine.Random.value * sum;
        sum = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            sum += repo.Weights[blocks[i]];
            if (sum > r)
                return i;
        }
        return blocks.Length - 1;
    }

    private void Propagate()
    {
        while (stack.Count > 0)
        {
            var poss = stack.Pop();

            for (int d = 0; d < 6; d++)
            {
                var neighbor = poss.index + delta[d];
                if (OnBoundary(neighbor)) continue;

                int[] conn = repo.Connections[d][poss.block];
                var connCounter = wave[neighbor.z, neighbor.y, neighbor.x];

                for (int i = 0; i < conn.Length; i++)
                {
                    int neighborBlock = conn[i];

                    if (!connCounter.ContainsKey(neighborBlock))
                        continue;

                    int[] counter = connCounter[neighborBlock];

                    counter[d]--;
                    if (counter[d] == 0)
                        Ban(new Possibility(neighbor, neighborBlock));
                }
            }
        }
    }

    private void ValidateBoundary()
    {
        var levelSize = levelData.bounds.size;

        var d = (int)Direction.Right;
        if (solveBounds.max.x < levelSize.x)
            foreach(var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                ValidateBoundaryBlock(i.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d);

        d = (int)Direction.Left;
        if (solveBounds.min.x > 0)
            foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                ValidateBoundaryBlock(i.nxy() + Vector3Int.right * solveBounds.min.x, d);

        d = (int)Direction.Up;
        if (solveBounds.max.y < levelSize.y)
            foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                ValidateBoundaryBlock(i.xny() + Vector3Int.up * (solveBounds.max.y - 1), d);

        d = (int)Direction.Down;
        if (solveBounds.min.y > 0)
            foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                ValidateBoundaryBlock(i.xny() + Vector3Int.up * solveBounds.min.y, d);

        d = (int)Direction.Forward;
        if (solveBounds.max.z < levelSize.z )
            foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                ValidateBoundaryBlock(i.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d);

        d = (int)Direction.Backward;
        if (solveBounds.min.z > 0 )
            foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                ValidateBoundaryBlock(i.xyn() + Vector3Int.forward * solveBounds.min.z, d);
    }
    private void ValidateBoundaryBlock(Vector3Int levelIndex, int d)
    {
        Vector3Int neighbor = levelIndex + delta[d];
        var neighborBlock = levelData.Blocks[neighbor.z, neighbor.y, neighbor.x];
        var index = levelIndex - solveBounds.min;
        var w = wave[index.z, index.y, index.x];

        if (neighborBlock == -1)
        {
            var ngroups = inputWave[neighbor.z, neighbor.y, neighbor.x].groups;
            if (ngroups.Count == 0)
                return;

            var groups = inputWave[levelIndex.z, levelIndex.y, levelIndex.x].groups;
            int[] counter = new int[repo.BlocksCount];
            interactionHandler.Process(groups, ngroups, d,counter);

            List<int> toBan = new List<int>();
            foreach(var item in w)
            {
                if (counter[item.Key] < 0)
                    toBan.Add(item.Key);
            }

            for (int i = 0; i < toBan.Count; i++)
                Ban(new Possibility(index, toBan[i]));
        }
        else
        {
            neighborBlock = repo.BlocksHash.GetIndex(neighborBlock);
            var neighborConn = repo.Connections[opposite[d]][neighborBlock];

            
            var set = new HashSet<int>();
            foreach (var pair in w)
                set.Add(pair.Key);

            set.ExceptWith(neighborConn);
            foreach (var item in set)
                Ban(new Possibility(index, item));
        }
    }

    private void Ban(Possibility pos)
    {
        weights[pos.index.z, pos.index.y, pos.index.x] -= repo.Weights[pos.block];
        var w = wave[pos.index.z, pos.index.y, pos.index.x];
        w.Remove(pos.block);
        if(w.Count == 1)
        {
            foreach (var pair in w)
                blocksCounter[pair.Key]++;
        }
        stack.Push(pos);
    }

    private bool OnBoundary(Vector3Int index) => OnBoundary(index.x, index.y, index.z, Vector3Int.zero, solveBounds.size);
    private bool OnBoundary(int x, int y, int z, Vector3Int start, Vector3Int end)
    {
        return (x < start.x || y < start.y || z < start.z ||
            x >= end.x || y >= end.y || z >= end.z);
    }
}