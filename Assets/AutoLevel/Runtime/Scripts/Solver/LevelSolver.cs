using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public class LevelSolver : BaseLevelSolver
    {
        private Stack<Possibility> stack;
        protected Dictionary<int, int[]>[,,] wave;

        private RingBuffer<Possibility> bannedElements;
        private List<Choice> choices;
        private int banCounter;

        class Choice
        {
            public int blocksCount;
            public int offset;
            public Vector3Int index;
            public List<int> blocks = new List<int>();
        }

        public LevelSolver(Vector3Int size) : base(size)
        {
            bannedElements = new RingBuffer<Possibility>(100);
            choices = new List<Choice>();

            stack = new Stack<Possibility>(size.x);
            wave = new Dictionary<int, int[]>[size.z, size.y, size.x];
            foreach (var index in SpatialUtil.Enumerate(size))
                wave[index.z, index.y, index.x] = new Dictionary<int, int[]>();
        }

        protected override void FillLevelData()
        {
            foreach (var index in SolverVolume)
            {
                layer.Blocks[index + solveBounds.min] =
                    repo.GetBlockHash(wave[index.z, index.y, index.x].First().Key);
            }
        }

        protected override void Clear()
        {
            banCounter = 0;
            bannedElements.Clear();
            choices.Clear();

            foreach (var index in SolverVolume)
                wave[index.z, index.y, index.x].Clear();
            stack.Clear();
        }

        protected override void Fill()
        {
            if(layerIndex != 0)
            {
                foreach (var index in SolverVolume)
                {
                    var blocks = repo.GetUpperLayerBlocks(repo.GetBlockIndex(preLayer.Blocks[index]));
                    weights[index.z, index.y, index.x] = blocks.Sum((b) => blockWeights[b]);
                }

                int[][] counter = new int[6][];
                for (int i = 0; i < 6; i++)
                    counter[i] = new int[repo.BlocksCount];

                foreach (var index in SolverVolume)
                {
                    FillUpperCell(index, counter);

#if AUTOLEVEL_DEBUG
                    if (wave[index.z, index.y, index.x].Count == 0)
                        throw new BuildFailedException(SolveStage.Fill, index);
#endif
                }

            }
            else if (inputWave != null)
            {
                foreach (var index in SolverVolume)
                {
                    var li = index + solveBounds.min;
                    var iwc = inputWave[li.z, li.y, li.x];

                    weights[index.z, index.y, index.x] =
                        iwc.GroupsEnum(repo.GroupsCount).Sum((g) => groupWeights[g]);
                }

                int[][] counter = new int[6][];
                for (int i = 0; i < 6; i++)
                    counter[i] = new int[repo.BlocksCount];

                foreach (var index in SolverVolume)
                {
                    FillCell(index, counter);
#if AUTOLEVEL_DEBUG
                    if (wave[index.z,index.y,index.x].Count == 0)
                        throw new BuildFailedException(SolveStage.Fill, index);
#endif
                }
            }
            else
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
                            conn[opposite[d]] = repo.Connections[d][b].Count;
                        wc[b] = conn;
                    }
#if AUTOLEVEL_DEBUG
            if (wc.Count == 0)
                throw new BuildFailedException(SolveStage.Fill, index);
#endif
                }
            }

        }

        private void FillCell(Vector3Int index, int[][] counter)
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

                var range = repo.GetGroupRange(group, layerIndex);

                for (int b = range.x; b < range.y; b++)
                {
                    var c = new int[6];
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
        }

        private void FillUpperCell(Vector3Int index, int[][] counter)
        {
            var li = index + solveBounds.min;
            var wc = wave[index.z, index.y, index.x];
            var iwc = repo.GetUpperLayerBlocks(repo.GetBlockIndex(preLayer.Blocks[li]));

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
                var niwc = repo.GetUpperLayerBlocks(repo.GetBlockIndex(preLayer.Blocks[nli]));

                CountConnections(iwc, niwc, d, counter[d]);
            }

            foreach(var b in iwc)
            {
                var c = new int[6];
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

            var rlist = new List<int>();
            var rset = new HashSet<int>();

            var d = (int)Direction.Right;
            var picker = boundaries[d];
            if (solveBounds.max.x == levelSize.x && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateExteriorSide(i.nxy() + Vector3Int.right * (solveBounds.max.x - 1), d, picker, rlist, rset);

            d = (int)Direction.Left;
            picker = boundaries[d];
            if (solveBounds.min.x == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.yz(), solveBounds.max.yz()))
                    ValidateExteriorSide(i.nxy(), d, picker, rlist, rset);

            d = (int)Direction.Up;
            picker = boundaries[d];
            if (solveBounds.max.y == levelSize.y && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateExteriorSide(i.xny() + Vector3Int.up * (solveBounds.max.y - 1), d, picker, rlist, rset);

            d = (int)Direction.Down;
            picker = boundaries[d];
            if (solveBounds.min.y == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xz(), solveBounds.max.xz()))
                    ValidateExteriorSide(i.xny(), d, picker, rlist, rset);

            d = (int)Direction.Forward;
            picker = boundaries[d];
            if (solveBounds.max.z == levelSize.z && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateExteriorSide(i.xyn() + Vector3Int.forward * (solveBounds.max.z - 1), d, picker, rlist, rset);

            d = (int)Direction.Backward;
            picker = boundaries[d];
            if (solveBounds.min.z == 0 && picker != null)
                foreach (var i in SpatialUtil.Enumerate(solveBounds.min.xy(), solveBounds.max.xy()))
                    ValidateExteriorSide(i.xyn(), d, picker, rlist, rset);
        }

        protected override void Propagate()
        {
            while (stack.Count > 0)
            {
                var poss = stack.Pop();

                for (int d = 0; d < 6; d++)
                {
                    var ni = poss.index + delta[d];
                    if (OnBoundary(ni)) continue;

                    var blocks = repo.Connections[d][poss.block];
                    var nwc = wave[ni.z, ni.y, ni.x];

                    foreach (var block in blocks)
                    {
                        if (!nwc.ContainsKey(block))
                            continue;

                        int[] counter = nwc[block];

                        counter[d]--;
                        if (counter[d] == 0)
                        {
                            var p = new Possibility(ni, block);
                            AddBanToBackTrace(p);
                            Ban(p);
                        }

                    #if AUTOLEVEL_DEBUG
                        if (nwc.Count == 0)
                            throw new BuildFailedException(SolveStage.Propagate,ni);
                    #endif

                    }
                }
            }
        }

        protected override Result Observe()
        {
            float minEntropy = float.MaxValue;
            Vector3Int minIndex = -Vector3Int.one;
            int[] blocks = null;

            foreach (var index in SolverVolume)
            {
                int pCount = wave[index.z, index.y, index.x].Count;
                if (pCount == 0)
                {
#if AUTOLEVEL_DEBUG
                    throw new BuildFailedException(SolveStage.Observe, index);
#else

                    if(BackTrace(out var Choice))
                    {
                        AddChoiceToBackTrace(Choice);
                        ApplyChoise(Choice, GetBlocks(Choice.index));

                        return Result.Ongoing;
                    }

                    return Result.Fail;
#endif
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

            blocks = GetBlocks(minIndex);
            var remain = StablePick(blocks);

#if AUTOLEVEL_DEBUG
            choices_debug.Enqueue(new Possibility(minIndex, blocks[remain]));
#endif
            var choise = new Possibility(minIndex, blocks[remain]);
            AddChoiceToBackTrace(choise);
            ApplyChoise(choise, blocks);

            return Result.Ongoing;
        }

        private void ApplyChoise(Possibility choise,int[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
                if (blocks[i] != choise.block)
                {
                    var p = new Possibility(choise.index, blocks[i]);
                    AddBanToBackTrace(p);
                    Ban(p);
                }
        }

        private int[] GetBlocks(Vector3Int index)
        {
            var wc = wave[index.z, index.y, index.x];
            int[] blocks = new int[wc.Count];
            {
                int i = 0;
                foreach (var block in wc)
                    blocks[i++] = block.Key;
            }
            return blocks;
        }

        protected override void Ban(Possibility poss)
        {
            weights[poss.index.z, poss.index.y, poss.index.x] -= blockWeights[poss.block];
            var wc = wave[poss.index.z, poss.index.y, poss.index.x];
            wc.Remove(poss.block);
            if (wc.Count == 1)
                blocksCounter[wc.First().Key]++;
            stack.Push(poss);
        }

        protected override IEnumerable<int> EnumareteBlocksInWaveCell(Vector3Int index)
        {
            return wave[index.z, index.y, index.x].Select((pair) => pair.Key);
        }

        private bool BackTrace(out Possibility p)
        {
            p = default;
            RemoveUnvalidChoices();
            if (choices.Count == 0)
                return false;

            var c = choices[choices.Count - 1];

            //back propagation
            {
                var size = banCounter - c.offset;
                var toCount = new List<Possibility>(size);

                int i = 0; Possibility poss;
                while (bannedElements.TryPop(out poss))
                {
                    if ( i++ == size)
                        break;

                    toCount.Add(poss);
                    Unban(poss);
                }
                for (int j = 0; j < toCount.Count; j++)
                    Recount(toCount[j]);
                banCounter = c.offset;
            }

            //picking new choice
            var wc = wave[c.index.z, c.index.y, c.index.x];
            var result = new List<int>();
            foreach (var b in wc)
                if (!c.blocks.Contains(b.Key))
                    result.Add(b.Key);
            p = new Possibility() { index = c.index, block = result[StablePick(result)] };

            return true;
        }

        private void Unban(Possibility poss)
        {
            weights[poss.index.z, poss.index.y, poss.index.x] += blockWeights[poss.block];
            var wc = wave[poss.index.z, poss.index.y, poss.index.x];
            if (wc.Count == 1)
                blocksCounter[wc.First().Key]--;
            wc[poss.block] = new int[6];
        }

        private void Recount(Possibility poss)
        {
            var wc = wave[poss.index.z, poss.index.y, poss.index.x];

            for (int d = 0; d < 6; d++)
            {
                var ni = poss.index + delta[d];
                if (OnBoundary(ni))
                {
                    wc[poss.block][opposite[d]] = 1;
                    continue;
                }

                var blocks = repo.Connections[d][poss.block];
                var nwc = wave[ni.z, ni.y, ni.x];

                foreach (var block in blocks)
                {
                    if (!nwc.ContainsKey(block))
                        continue;

                    wc[poss.block][opposite[d]]++;
                }
            }
        }

        private void AddChoiceToBackTrace(Possibility p)
        {
            var index = choices.FindIndex((c) => c.index == p.index);
            if (index == -1)
            {
                choices.Add(new Choice()
                {
                    index = p.index,
                    blocksCount = wave[p.index.z, p.index.y, p.index.x].Count,
                    offset = banCounter,
                    blocks = new List<int>() { p.block }
                });
            }
            else
                choices[index].blocks.Add(p.block);
        }

        private void RemoveUnvalidChoices()
        {
            for (int i = choices.Count - 1; i > -1; i--)
            {
                var choice = choices[i];
                if (banCounter - choice.offset > bannedElements.Count ||
                    choice.blocks.Count == choice.blocksCount)
                    choices.RemoveAt(i);
            }
        }

        private void AddBanToBackTrace(Possibility p)
        {
            bannedElements.Push(p);
            banCounter++;
        }

        private Dictionary<int, int[]>[,,] CopyWave()
        {
            var copy = new Dictionary<int, int[]>[size.z, size.y, size.x];

            foreach (var index in SolverVolume)
            {
                var wc = wave[index.z, index.y, index.x];
                var cwc = new Dictionary<int, int[]>();
                foreach (var pair in wc)
                {
                    var cc = new int[6];
                    for (int d = 0; d < 6; d++)
                        cc[d] = pair.Value[d];
                    cwc[pair.Key] = cc;
                }
                copy[index.z, index.y, index.x] = cwc;
            }

            return copy;
        }

        private bool CompareWave(Dictionary<int, int[]>[,,] a, Dictionary<int, int[]>[,,] b)
        {
            bool pass = true;

            foreach (var index in SolverVolume)
            {
                var wc = a[index.z, index.y, index.x];
                var cwc = b[index.z, index.y, index.x];
                foreach (var pair in wc)
                {
                    if (!cwc.ContainsKey(pair.Key))
                    {
                        Debug.Log($"at {index}, key not found {pair.Key}");
                        pass = false;
                    }

                    for (int d = 0; d < 6; d++)
                    {
                        if (cwc[pair.Key][d] != pair.Value[d])
                        {
                            Debug.Log($"at {index}, at d {d} block {pair.Key}, {cwc[pair.Key][d]} vs {pair.Value[d]}");
                            pass = false;
                        }
                    }
                }
            }
            return pass;
        }
    }
}