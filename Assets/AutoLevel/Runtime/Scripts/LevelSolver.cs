using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{
    using static Directions;

    [Serializable]
    public struct InputWaveCell
    {
        public static InputWaveCell AllGroups => new InputWaveCell() { groups = int.MaxValue };

        public bool this[int index]
        {
            get => ((1 << index) & groups) > 0;
            set => groups = value ? (groups | 1 << index) : (groups & ~(1 << index));
        }

        [SerializeField]
        private int groups;

        public InputWaveCell(List<int> groups)
        {
            this.groups = 0;
            foreach (var g in groups)
                this[g] = true;
        }

        public bool Invalid() => groups == 0;
        public int GroupsCount(int groupCount) => GroupsEnum(groupCount).Count();
        public bool ContainAll => groups == int.MaxValue;

        public IEnumerable<int> GroupsEnum(int groupCount) {
            for (int i = 0; i < groupCount; i++)
                if (this[i])
                    yield return i;
        }
    }

    public class LevelSolver
    {
        // Notations :
        //---------------------------------------------------
        // li level Index
        // wc wave cell | iwc input wave cell | lc level cell
        // n neighbor

        /// <summary>
        /// calculate the interaction between two collections of groups at a given direction
        /// </summary>
        class GroupInteractionHandler
        {
            private BlocksRepo.Runtime repo;

            private BlocksRepo.GroupsEnumerator groupsEnum;
            private int[] tempA;

            public GroupInteractionHandler(BlocksRepo.Runtime repo)
            {
                this.repo = repo;
                tempA = new int[repo.BlocksCount];
                groupsEnum = repo.GetDistinctGroupsEnumerable();
            }

            public void Process(InputWaveCell cellA, InputWaveCell cellB, int d, int[] output)
            {
                tempA.Fill(() => 0);
                groupsEnum.SetWave(cellA);

                foreach (var g_a_index in cellA.GroupsEnum(repo.GroupsCount))
                {
                    var g_a_counter = repo.groupCounter[d][g_a_index];
                    var blocks_a = repo.BlocksPerGroup[g_a_index];

                    foreach (var g_b_index in cellB.GroupsEnum(repo.GroupsCount))
                    {
                        var g_a_b_counter = g_a_counter[g_b_index];
                        for (int c = 0; c < g_a_b_counter.Length; c++)
                            tempA[blocks_a[c]] += g_a_b_counter[c];
                    }

                    for (int i = 0; i < blocks_a.Count; i++)
                    {
                        var b = blocks_a[i];
                        //guard against duplicates in `a` groups
                        output[b] = tempA[b];
                        tempA[b] = 0;
                    }
                }

                //guard against duplicates in `b` groups
                foreach (var b in repo.GetGroupsEnumerable(cellB))
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

        private BlocksRepo.Runtime repo;
        private LevelData levelData;
        private Array3D<InputWaveCell> inputWave;

        private BoundsInt solveBounds;

        private Vector3Int size;
        private Dictionary<int, int[]>[,,] wave;
        private float[,,] weights;
        private Stack<Possibility> stack;
        private GroupInteractionHandler interactionHandler;
        private IEnumerable<Vector3Int> SolverVolume => SpatialUtil.Enumerate(solveBounds.size);
        private System.Random rand;
        private ILevelBoundary[] boundaries = new ILevelBoundary[6];


        private float weightsSum;
        private float blocksCount;
        private int[] blocksCounter;

        private static int fill_pk = "level_solver_fill".GetHashCode();
        private static int interior_boundary_pk = "level_solver_interior_boundary".GetHashCode();
        private static int exterior_boundar_pk = "level_solver_exterior_boundar".GetHashCode();
        private static int propagate_pk = "level_solver_propagate".GetHashCode();
        private static int observe_pk = "level_solver_observe".GetHashCode();

        public LevelSolver(Vector3Int size)
        {
            this.size = size;
            wave = new Dictionary<int, int[]>[size.z, size.y, size.x];
            weights = new float[size.z, size.y, size.x];
            stack = new Stack<Possibility>(size.x);

            foreach (var index in SpatialUtil.Enumerate(size))
                wave[index.z, index.y, index.x] = new Dictionary<int, int[]>();
        }

        public void SetRepo(BlocksRepo.Runtime repo)
        {
            this.repo = repo;
            interactionHandler = new GroupInteractionHandler(repo);
        }

        public void SetlevelData(LevelData levelData)
        {
            this.levelData = levelData;
        }

        public void SetInputWave(Array3D<InputWaveCell> inputWave)
        {
            if (inputWave == null)
                return;

            if (inputWave.Size.x == 0 || inputWave.Size.y == 0 || inputWave.Size.z == 0)
                throw new System.Exception("input wave volume is zero");

            this.inputWave = inputWave;
        }

        public void SetBoundary(ILevelBoundary boundary, Direction d)
        {
            boundaries[(int)d] = boundary;
        }


        public int Solve(BoundsInt bounds, int iteration = 10 , int seed = 0)
        {
            rand = new System.Random(seed + (int)DateTime.Now.Ticks);
            solveBounds = bounds;
            if (solveBounds.size.x > size.x || solveBounds.size.y > size.y || solveBounds.size.z > size.z)
                throw new System.Exception("solving bounds need to be smaller than solver size");

            blocksCount = bounds.size.x * bounds.size.y * bounds.size.z;

            for (int t = 0; t < iteration; t++)
            {
                if (Run() == Result.Success)
                {
                    foreach (var index in SolverVolume)
                    {
                        levelData.Blocks[index + solveBounds.min] =
                            repo.GetBlockHash(wave[index.z, index.y, index.x].First().Key);
                    }
                    return t + 1;
                }
            }
            return 0;
        }

        private void Clear()
        {
            foreach (var index in SolverVolume)
                wave[index.z, index.y, index.x].Clear();
            stack.Clear();

            if (blocksCounter == null || blocksCounter.Length != repo.BlocksCount)
                blocksCounter = new int[repo.BlocksCount];
            blocksCounter.Fill(() => 0);

            for (int i = 0; i < repo.BlocksCount; i++)
                weightsSum += repo.GetBlockWeight(i);
        }

        private void Fill()
        {
            if (inputWave == null)
            {
                foreach (var index in SolverVolume)
                    weights[index.z, index.y, index.x] = weightsSum;

                foreach (var index in SolverVolume)
                {
                    var wc = wave[index.z, index.y, index.x];
                    for (int b = 0; b < repo.BlocksCount; b++)
                    {
                        var conn = new int[6];
                        for (int d = 0; d < 6; d++)
                            conn[opposite[d]] = repo.Connections[d][b].Length;
                        wc[b] = conn;
                    }
                }
            }
            else
            {
                int[][] counter = new int[6][];
                for (int i = 0; i < 6; i++)
                    counter[i] = new int[repo.BlocksCount];

                var groupsEnum = repo.GetDistinctGroupsEnumerable();

                foreach (var index in SolverVolume)
                {
                    var li = index + solveBounds.min;
                    var iwc = inputWave[li.z, li.y, li.x];

                    weights[index.z, index.y, index.x] =
                        iwc.GroupsEnum(repo.GroupsCount).Sum((g) => repo.groupWeights[g]);
                }


                foreach (var index in SolverVolume)
                {
                    var li = index + solveBounds.min;
                    var iwc = inputWave[li.z, li.y, li.x];

                    var wc = wave[index.z, index.y, index.x];

                    groupsEnum.SetWave(iwc);

                    for (int d = 0; d < 6; d++)
                    {
                        counter[d].Fill(() => 0);

                        var ni = index + delta[d];
                        if (OnBoundary(ni))
                        {
                            for (int b = 0; b < repo.BlocksCount; b++)
                                counter[d][b] = 1;
                            continue;
                        }

                        var nli = li + delta[d];
                        var niwc = inputWave[nli.z, nli.y, nli.x];

                        interactionHandler.Process(iwc, niwc, d, counter[d]);
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
                            wc[b] = c;
                    }
                    groupsEnum.Reset();
                }
            }
        }

        private Result Run()
        {
            Clear();

            Profiling.StartTimer(fill_pk);
            Fill();
            Profiling.PauseTimer(fill_pk);

            Profiling.StartTimer(interior_boundary_pk);
            ValidateInteriorBoundary();
            Profiling.PauseTimer(interior_boundary_pk);

            Profiling.StartTimer(exterior_boundar_pk);
            ValidateExteriorBoundary();
            Profiling.PauseTimer(exterior_boundar_pk);

            Profiling.StartTimer(observe_pk, true);
            Profiling.StartTimer(propagate_pk, true);
            while (true)
            {
                Profiling.ResumeTimer(propagate_pk);
                Propagate();
                Profiling.PauseTimer(propagate_pk);

                Profiling.ResumeTimer(observe_pk);
                var result = Observe();
                Profiling.PauseTimer(observe_pk);

                if (result != Result.Ongoing)
                {
                    Profiling.LogAndRemoveTimer("filling time", fill_pk);
                    Profiling.LogAndRemoveTimer("interior boundary", interior_boundary_pk);
                    Profiling.LogAndRemoveTimer("exterior boundary", exterior_boundar_pk);
                    Profiling.LogAndRemoveTimer("propagate", propagate_pk);
                    Profiling.LogAndRemoveTimer("observe", observe_pk);
                    return result;
                }
            }
        }

        private Result Observe()
        {
            float minEntropy = float.MaxValue;
            Vector3Int minIndex = -Vector3Int.one;


            foreach (var index in SolverVolume)
            {
                int pCount = wave[index.z, index.y, index.x].Count;
                if (pCount == 0)
                {
                    //TODO : Log State
                    return Result.Fail;
                }

                if (pCount > 1)
                {
                    float entropy = Mathf.Log(weights[index.z, index.y, index.x]) +
                        1E-4f * GetNextRand();
                    if (entropy < minEntropy)
                    {
                        minEntropy = entropy;
                        minIndex = index;
                    }
                }
            }

            if (minIndex == -Vector3Int.one)
                return Result.Success;

            var wc = wave[minIndex.z, minIndex.y, minIndex.x];
            int[] blocks = new int[wc.Count];
            {
                int i = 0;
                foreach (var index in wc)
                    blocks[i++] = index.Key;
            }
            var remain = StablePick(blocks);

            for (int i = 0; i < blocks.Length; i++)
                if (i != remain)
                    Ban(new Possibility(minIndex, blocks[i]));

            return Result.Ongoing;
        }

        int StablePick(int[] blocks)
        {
            float invBCount = 1f / blocksCount;
            float invWeightsSum = 1f / weightsSum;

            float sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i]; var weight = repo.GetBlockWeight(b);
                //exclude what reaches the threshold
                if ((blocksCounter[b] + 1) * invBCount < weight * invWeightsSum)
                    sum += weight;
            }

            var r = UnityEngine.Random.value * sum;
            sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i]; var weight = repo.GetBlockWeight(b);
                if ((blocksCounter[b] + 1) * invBCount < weight * invWeightsSum)
                {
                    sum += weight;
                    if (sum > r)
                        return i;
                }
            }

            return Pick(blocks);
        }
        int Pick(int[] blocks)
        {
            float sum = 0;
            for (int i = 0; i < blocks.Length; i++)
                sum += repo.GetBlockWeight(blocks[i]);
            var r = GetNextRand() * sum;
            sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                sum += repo.GetBlockWeight(blocks[i]);
                if (sum > r)
                    return i;
            }
            return blocks.Length - 1;
        }

        float GetNextRand()
        {
            return rand.Next(0, 100000) * 0.00001f;
        }

        private void Propagate()
        {
            while (stack.Count > 0)
            {
                var poss = stack.Pop();

                for (int d = 0; d < 6; d++)
                {
                    var ni = poss.index + delta[d];
                    if (OnBoundary(ni)) continue;

                    int[] blocks = repo.Connections[d][poss.block];
                    var nwc = wave[ni.z, ni.y, ni.x];

                    foreach (var block in blocks)
                    {
                        if (!nwc.ContainsKey(block))
                            continue;

                        int[] counter = nwc[block];

                        counter[d]--;
                        if (counter[d] == 0)
                            Ban(new Possibility(ni, block));
                    }
                }
            }
        }

        private void ValidateInteriorBoundary()
        {
            var levelSize = levelData.bounds.size;

            var d = (int)Direction.Right;
            if (solveBounds.max.x < levelSize.x)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateInternalSide(i.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d);

            d = (int)Direction.Left;
            if (solveBounds.min.x > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateInternalSide(i.nxy() + Vector3Int.right * solveBounds.min.x, d);

            d = (int)Direction.Up;
            if (solveBounds.max.y < levelSize.y)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateInternalSide(i.xny() + Vector3Int.up * (solveBounds.max.y - 1), d);

            d = (int)Direction.Down;
            if (solveBounds.min.y > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateInternalSide(i.xny() + Vector3Int.up * solveBounds.min.y, d);

            d = (int)Direction.Forward;
            if (solveBounds.max.z < levelSize.z)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateInternalSide(i.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d);

            d = (int)Direction.Backward;
            if (solveBounds.min.z > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateInternalSide(i.xyn() + Vector3Int.forward * solveBounds.min.z, d);
        }
        private void ValidateExteriorBoundary()
        {
            var levelSize = levelData.bounds.size;

            var d = (int)Direction.Right;
            var picker = boundaries[d];
            if (solveBounds.max.x == levelSize.x && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateExteriorSide(i.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d, picker);

            d = (int)Direction.Left;
            picker = boundaries[d];
            if (solveBounds.min.x == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateExteriorSide(i.nxy(), d, picker);

            d = (int)Direction.Up;
            picker = boundaries[d];
            if (solveBounds.max.y == levelSize.y && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateExteriorSide(i.xny() + Vector3Int.up * (solveBounds.max.y - 1), d, picker);

            d = (int)Direction.Down;
            picker = boundaries[d];
            if (solveBounds.min.y == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateExteriorSide(i.xny(), d, picker);

            d = (int)Direction.Forward;
            picker = boundaries[d];
            if (solveBounds.max.z == levelSize.z && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateExteriorSide(i.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d, picker);

            d = (int)Direction.Backward;
            picker = boundaries[d];
            if (solveBounds.min.z == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateExteriorSide(i.xyn(), d, picker);
        }
        private void ValidateExteriorSide(Vector3Int li, int d, ILevelBoundary picker)
        {
            var nli = levelData.position + li + delta[d];
            var result = picker.Evaluate(nli);
            switch (result.option)
            {
                case LevelBoundaryOption.Block:
                    ValidateWaveSide(li, result.block, d);
                    break;
                case LevelBoundaryOption.Group:
                    ValidateWaveSide(li, result.waveBlock, d);
                    break;
            }
        }
        private void ValidateInternalSide(Vector3Int li, int d)
        {
            Vector3Int nli = li + delta[d];
            var nlc = levelData.Blocks[nli.z, nli.y, nli.x];

            if (nlc == 0)
            {
                var iwc = inputWave == null ? InputWaveCell.AllGroups : inputWave[li.z, li.y, li.x];
                if (iwc.ContainAll)
                    return;

                ValidateWaveSide(li, iwc, d);
            }
            else
                ValidateWaveSide(li, nlc, d);
        }

        private void ValidateWaveSide(Vector3Int li, InputWaveCell niwc, int d)
        {
            var index = li - solveBounds.min;
            var wc = wave[index.z, index.y, index.x];
            var iwc = inputWave == null ? InputWaveCell.AllGroups : inputWave[li.z, li.y, li.x];

            if (iwc.ContainAll && niwc.ContainAll)
                return;

            int[] counter = new int[repo.BlocksCount];
            interactionHandler.Process(iwc, niwc, d, counter);

            List<int> toBan = new List<int>();
            foreach (var item in wc)
            {
                if (counter[item.Key] <= 0)
                    toBan.Add(item.Key);
            }

            for (int i = 0; i < toBan.Count; i++)
                Ban(new Possibility(index, toBan[i]));
        }
        private void ValidateWaveSide(Vector3Int lc, int nlc, int d)
        {
            var index = lc - solveBounds.min;
            var w = wave[index.z, index.y, index.x];

            nlc = repo.GetBlockIndex(nlc);
            var neighborConn = repo.Connections[opposite[d]][nlc];

            var set = new HashSet<int>();
            foreach (var pair in w)
                set.Add(pair.Key);

            set.ExceptWith(neighborConn);
            foreach (var item in set)
                Ban(new Possibility(index, item));
        }

        private void Ban(Possibility poss)
        {
            weights[poss.index.z, poss.index.y, poss.index.x] -= repo.GetBlockWeight(poss.block);
            var wc = wave[poss.index.z, poss.index.y, poss.index.x];
            wc.Remove(poss.block);
            if (wc.Count == 1)
                blocksCounter[wc.First().Key]++;
            stack.Push(poss);
        }

        private bool OnBoundary(Vector3Int index) => OnBoundary(index.x, index.y, index.z, Vector3Int.zero, solveBounds.size);
        private bool OnBoundary(int x, int y, int z, Vector3Int start, Vector3Int end)
        {
            return (x < start.x || y < start.y || z < start.z ||
                x >= end.x || y >= end.y || z >= end.z);
        }
    }

}