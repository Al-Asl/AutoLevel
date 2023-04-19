using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public class LevelSolverMT : BaseLevelSolver
    {
        interface IPropagter
        {
            void Clear();
            void Propagate();
            void Ban(Possibility poss);
        }
        
        class PropagterMT : IPropagter
        {
            private LevelSolverMT solver;

            public int stackCount;
            public int[] cellsPerConfig = new int[27];
            private ConcurrentDictionary<int, Stack<int>>[] BannedPerConfig = new ConcurrentDictionary<int, Stack<int>>[27];
            private ConcurrentStack<(int, Stack<int>)> stack = new ConcurrentStack<(int, Stack<int>)>();

            public PropagterMT(LevelSolverMT solver)
            {
                this.solver = solver;

                for (int i = 0; i < 27; i++)
                    BannedPerConfig[i] = new ConcurrentDictionary<int, Stack<int>>();
            }

            public void FillStack(Stack<Possibility> stack)
            {
                for (int i = 0; i < 27; i++)
                {
                    foreach (var cell in BannedPerConfig[i])
                        foreach (var block in cell.Value)
                            stack.Push(new Possibility(GetPos(cell.Key), block));
                }
            }

            public void Clear()
            {
                stackCount = 0;
                cellsPerConfig.Fill(() => 0);

                stack.Clear();
                for (int i = 0; i < 27; i++)
                    BannedPerConfig[i].Clear();
            }

            public void Propagate()
            {
                while(true)
                {
                    int maxIndex = -1;
                    int max = 0;
                    for (int i = 0; i < 27; i++)
                    {
                        if (cellsPerConfig[i] > max)
                        {
                            max = cellsPerConfig[i];
                            maxIndex = i;
                        }
                    }

                    if (maxIndex == -1)
                        return;

                    //TODO : need a better way that factor for cell per configuration
                    if (stackCount < 30000)
                        return;

                    var bannedInConfig = BannedPerConfig[maxIndex];
                    foreach (var cell in bannedInConfig)
                    {
                        stack.Push((cell.Key, cell.Value));
                        stackCount -= cell.Value.Count;
                    }
                    bannedInConfig.Clear();
                    cellsPerConfig[maxIndex] = 0;

                    Parallel.ForEach(stack, (cell) =>
                    {
                        var pos = GetPos(cell.Item1);
                        while (cell.Item2.Count > 0)
                            solver.Propagate(new Possibility(pos, cell.Item2.Pop()));
                    });
                }
            }

            public void Ban(Possibility poss)
            {
                Interlocked.Increment(ref stackCount);

                solver.weights[poss.index.z, poss.index.y, poss.index.x] -= solver.blockWeights[poss.block];
                var wc = solver.wave[poss.index.z, poss.index.y, poss.index.x];
                wc.Remove(poss.block);
                if (wc.Count == 1)
                    Interlocked.Increment(ref solver.blocksCounter[wc.First().Key]);

                var index = GetIndex(poss.index);
                var cIndex = GetConfig(poss.index);
                var bannedInConfig = BannedPerConfig[cIndex];

                if (!bannedInConfig.ContainsKey(index))
                {
                    bannedInConfig[index] = new Stack<int>();
                    Interlocked.Increment(ref cellsPerConfig[cIndex]);
                }

                bannedInConfig[index].Push(poss.block);
            }

            private Vector3Int GetPos(int i) => SpatialUtil.Index1DTo3D(i, solver.solveBounds.size);
            private int GetIndex(Vector3Int pos) => SpatialUtil.Index3DTo1D(pos, solver.solveBounds.size);
            private int GetConfig(Vector3Int pos) => (pos.z % 3) * 9 + (pos.y % 3) * 3 + pos.x % 3;

            
        }

        class PropagterST : IPropagter
        {
            public Stack<Possibility> stack;

            private LevelSolverMT solver;

            public PropagterST(LevelSolverMT solver)
            {
                this.solver = solver;
                stack = new Stack<Possibility>();
            }

            public void Clear()
            {
                stack.Clear();
            }

            public void Propagate()
            {
                while (stack.Count > 0)
                    solver.Propagate(stack.Pop());
            }

            public void Ban(Possibility poss)
            {
                solver.weights[poss.index.z, poss.index.y, poss.index.x] -= solver.blockWeights[poss.block];
                var wc = solver.wave[poss.index.z, poss.index.y, poss.index.x];
                wc.Remove(poss.block);
                if (wc.Count == 1)
                    solver.blocksCounter[wc.First().Key]++;
                stack.Push(poss);
            }
        }

        public struct WaveCellPoss
        {
            public int this[int d]
            {
                get
                {
                    switch (d)
                    {
                        case 0:
                            return left;
                        case 1:
                            return down;
                        case 2:
                            return backward;
                        case 3:
                            return right;
                        case 4:
                            return up;
                        case 5:
                            return forward;
                    }
                    throw new System.IndexOutOfRangeException($"{d}");
                }
                set
                {
                    switch (d)
                    {
                        case 0:
                            left = value;
                            break;
                        case 1:
                            down = value;
                            break;
                        case 2:
                            backward = value;
                            break;
                        case 3:
                            right = value;
                            break;
                        case 4:
                            up = value;
                            break;
                        case 5:
                            forward = value;
                            break;
                    }
                }
            }

            public int left;
            public int down;
            public int backward;
            public int right;
            public int up;
            public int forward;
        }

        private IPropagter propagter;
        private PropagterST propagterST;
        private PropagterMT propagterMT;

        protected Dictionary<int, WaveCellPoss>[,,] wave;

        public LevelSolverMT(Vector3Int size) : base(size)
        {
            propagterST = new PropagterST(this);
            propagterMT = new PropagterMT(this);

            wave = new Dictionary<int, WaveCellPoss>[size.z, size.y, size.x];
            foreach (var index in SpatialUtil.Enumerate(size))
                wave[index.z, index.y, index.x] = new Dictionary<int, WaveCellPoss>();
        }

        protected override void FillLevelData()
        {
            SpatialUtil.ParallelEnumrate(solveBounds.size, (index) =>
            {
                levelData.Blocks[index + solveBounds.min] =
                    repo.GetBlockHash(wave[index.z, index.y, index.x].First().Key);
            });
        }

        protected override void Clear()
        {
            propagterST.Clear();
            propagterMT.Clear();

            foreach (var index in SolverVolume)
                wave[index.z, index.y, index.x].Clear();

            propagter = propagterMT;
        }

        protected override void Fill()
        {

            if (inputWave == null)
            {
                Parallel.ForEach(SolverVolume, (index) =>
                {
                    weights[index.z, index.y, index.x] = weightsSum;
                    var wc = wave[index.z, index.y, index.x];
                    for (int b = 0; b < repo.BlocksCount; b++)
                    {
                        var conn = new WaveCellPoss();
                        for (int d = 0; d < 6; d++)
                            conn[opposite[d]] = repo.Connections[d][b].Length;
                        wc[b] = conn;
                    }
                });
            }
            else
            {
                Parallel.ForEach(SolverVolume, (index) =>
                {
                    var li = index + solveBounds.min;
                    var iwc = inputWave[li.z, li.y, li.x];

                    weights[index.z, index.y, index.x] =
                        iwc.GroupsEnum(repo.GroupsCount).Sum((g) => groupWeights[g]);
                });


                SpatialUtil.ParallelEnumrate(size, () =>
                {
                    var counter = new int[6][];
                    for (int j = 0; j < 6; j++)
                        counter[j] = new int[repo.BlocksCount];
                    return counter;
                },
                (index, state, counter) =>
                {
                    var li = index + solveBounds.min;
                    var iwc = inputWave[li.z, li.y, li.x];
                    var wc = wave[index.z, index.y, index.x];

                    for (int d = 0; d < 6; d++)
                    {
                        var ni = index + delta[d];

                        if (OnBoundary(ni))
                        {
                            counter[d].Fill(() => 1);
                            continue;
                        }

                        counter[d].Fill(() => 0);

                        var nli = li + delta[d];
                        var niwc = inputWave[nli.z, nli.y, nli.x];

                        CountConnections(iwc, niwc, d, counter[d]);
                    }

                    for (int group = 0; group < repo.GroupsCount; group++)
                    {
                        if (!iwc[group])
                            continue;

                        var range = repo.GetGroupRange(group);

                        for (int b = range.x; b < range.y; b++)
                        {
                            var c = new WaveCellPoss();
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
                    }
                });
            }
        }

        protected override void ValidateInteriorBoundary()
        {
            var levelSize = levelData.bounds.size;
            var rlist = new List<int>();
            var rset = new HashSet<int>();

            var d = (int)Direction.Right;
            if (solveBounds.max.x < levelSize.x)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateInternalSide(i.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d, rlist, rset);

            d = (int)Direction.Left;
            if (solveBounds.min.x > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateInternalSide(i.nxy() + Vector3Int.right * solveBounds.min.x, d, rlist, rset);

            d = (int)Direction.Up;
            if (solveBounds.max.y < levelSize.y)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateInternalSide(i.xny() + Vector3Int.up * (solveBounds.max.y - 1), d, rlist, rset);

            d = (int)Direction.Down;
            if (solveBounds.min.y > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateInternalSide(i.xny() + Vector3Int.up * solveBounds.min.y, d, rlist, rset);

            d = (int)Direction.Forward;
            if (solveBounds.max.z < levelSize.z)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateInternalSide(i.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d, rlist, rset);

            d = (int)Direction.Backward;
            if (solveBounds.min.z > 0)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateInternalSide(i.xyn() + Vector3Int.forward * solveBounds.min.z, d, rlist, rset);
        }

        protected override void ValidateExteriorBoundary()
        {
            var levelSize = levelData.bounds.size;

            int threads = System.Environment.ProcessorCount;
            var partions = SpatialUtil.Parting(solveBounds.size.y * solveBounds.size.z, threads);

            Parallel.For(0, threads, (p) =>
            {
                var rlist = new List<int>();
                var rset = new HashSet<int>();

                var range = partions[p];

                var d = (int)Direction.Right;
                var picker = boundaries[d];
                if (solveBounds.max.x == levelSize.x && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.yz() + SpatialUtil.Index1DTo2D(i, solveBounds.size.y);
                        ValidateExteriorSide(index.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d, picker, rlist, rset);
                    }

                d = (int)Direction.Left;
                picker = boundaries[d];
                if (solveBounds.min.x == 0 && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.yz() + SpatialUtil.Index1DTo2D(i, solveBounds.size.y);
                        ValidateExteriorSide(index.nxy(), d, picker, rlist, rset);
                    }
            });

            partions = SpatialUtil.Parting(solveBounds.size.x * solveBounds.size.z, threads);

            Parallel.For(0, threads, (p) =>
            {
                var rlist = new List<int>();
                var rset = new HashSet<int>();

                var range = partions[p];

                var d = (int)Direction.Up;
                var picker = boundaries[d];
                if (solveBounds.max.y == levelSize.y && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.xz() + SpatialUtil.Index1DTo2D(i, solveBounds.size.x);
                        ValidateExteriorSide(index.xny() + Vector3Int.up * (solveBounds.max.y - 1), d, picker, rlist, rset);
                    }

                d = (int)Direction.Down;
                picker = boundaries[d];
                if (solveBounds.min.y == 0 && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.xz() + SpatialUtil.Index1DTo2D(i, solveBounds.size.x);
                        ValidateExteriorSide(index.xny(), d, picker, rlist, rset);
                    }

            });

            partions = SpatialUtil.Parting(solveBounds.size.x * solveBounds.size.y, threads);

            Parallel.For(0, threads, (p) =>
            {
                var rlist = new List<int>();
                var rset = new HashSet<int>();

                var range = partions[p];

                var d = (int)Direction.Forward;
                var picker = boundaries[d];
                if (solveBounds.max.z == levelSize.z && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.xy() + SpatialUtil.Index1DTo2D(i, solveBounds.size.x);
                        ValidateExteriorSide(index.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d, picker, rlist, rset);
                    }

                d = (int)Direction.Backward;
                picker = boundaries[d];
                if (solveBounds.min.z == 0 && picker != null)
                    for (int i = range.x; i < range.y; i++)
                    {
                        var index = solveBounds.min.xy() + SpatialUtil.Index1DTo2D(i, solveBounds.size.x);
                        ValidateExteriorSide(index.xyn(), d, picker, rlist, rset);
                    }

            });
        }

        protected override void Propagate()
        {
            propagter.Propagate();

            //the multi threaded propagation only run once
            if(propagter == propagterMT)
            {
                propagterMT.FillStack(propagterST.stack);
                propagter = propagterST;
            }
        }

        private bool Propagate(Possibility poss)
        {
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

                    var counter = nwc[block];
                    counter[d]--;
                    nwc[block] = counter;
                    if (counter[d] == 0)
                        Ban(new Possibility(ni, block));
                }
            }
            return true;
        }

        protected override Result Observe()
        {
            float minEntropy = float.MaxValue;
            Vector3Int minIndex = -Vector3Int.one;


            foreach (var index in SolverVolume)
            {
                int pCount = wave[index.z, index.y, index.x].Count;
                if (pCount == 0)
                {
                    return Result.Fail;
                }

                if (pCount > 1)
                {
                    float entropy = Mathf.Log(weights[index.z, index.y, index.x]) +
                        1E-4f * GetNextRand(rand);
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

        protected override void Ban(Possibility poss)
        {
            propagter.Ban(poss);
        }

        protected override IEnumerable<int> EnumareteBlocksInWaveCell(Vector3Int index)
        {
            return wave[index.z, index.y, index.x].Select((pair) => pair.Key);
        }
    }
}