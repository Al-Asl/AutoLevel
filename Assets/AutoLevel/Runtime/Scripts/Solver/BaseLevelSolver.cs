using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public abstract class BaseLevelSolver
    {
        // Notations :
        //---------------------------------------------------
        // li level Index
        // wc wave cell | iwc input wave cell | lc level cell
        // n neighbor

        protected enum SolveStage
        {
            Fill,
            ValidateInteriorBoundary,
            ValidateExteriorBoundary,
            Propagate,
            Observe
        }
        protected class BaseSolverException : Exception { }
        protected class NoRepoException : BaseSolverException
        {
            public override string Message => "The Repo isn't assigned!";
        }
        protected class InvalidSolveBoundException : BaseSolverException
        {
            public override string Message => "invalid solving bounds!";
        }
        protected class ZeroVolumeException : BaseSolverException
        {
            public override string Message => "the volume size is zero!";
        }
        protected class BuildFailedException : BaseSolverException
        {
            public SolveStage stage;
            public Vector3Int index;

            public BuildFailedException(SolveStage stage,Vector3Int index)
            {
                this.stage = stage;
                this.index = index;
            }
        }

        protected enum Result
        {
            Success,
            Fail,
            Ongoing
        }
        protected struct Possibility
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

        protected BlocksRepo.Runtime repo;
        protected LevelData levelData;
        protected Array3D<InputWaveCell> inputWave;

        protected BoundsInt solveBounds;

        protected Vector3Int size;
        protected float[,,] weights;

        protected System.Random rand;

        protected IEnumerable<Vector3Int> SolverVolume => SpatialUtil.Enumerate(solveBounds.size);
        protected ILevelBoundary[] boundaries = new ILevelBoundary[6];

        protected List<float> blockWeights;
        protected List<float> groupWeights;

        protected float weightsSum;
        protected float blocksCount;
        protected int[] blocksCounter;

        protected static int fill_pk = "level_solver_fill".GetHashCode();
        protected static int interior_boundary_pk = "level_solver_interior_boundary".GetHashCode();
        protected static int exterior_boundar_pk = "level_solver_exterior_boundar".GetHashCode();
        protected static int propagate_pk = "level_solver_propagate".GetHashCode();
        protected static int observe_pk = "level_solver_observe".GetHashCode();
        protected static int fill_leveldata_pk = "level_solver_fill_leveldata".GetHashCode();

        private int threadID;

        public BaseLevelSolver(Vector3Int size)
        {
            this.size = size;
            weights = new float[size.z, size.y, size.x];
            blockWeights = new List<float>();
            groupWeights = new List<float>();
        }

        public int Solve(BoundsInt bounds, int iteration = 10, int seed = 0)
        {
            if (repo == null)
                throw new NoRepoException();

            solveBounds = bounds;

            if (solveBounds.size.x == 0 || solveBounds.size.y == 0 || solveBounds.size.z == 0)
                throw new ZeroVolumeException();

            if (solveBounds.size.x > size.x || solveBounds.size.y > size.y || solveBounds.size.z > size.z)
                throw new InvalidSolveBoundException();

            threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;

            if (seed == 0)
                rand = new System.Random((int)(DateTime.Now.Ticks % int.MaxValue));
            else
                rand = new System.Random(seed);

            blocksCount = bounds.size.x * bounds.size.y * bounds.size.z;

            for (int t = 0; t < iteration; t++)
            {

                var result = Result.Fail;

#if AUTOLEVEL_DEBUG
                try
                {
                    result = Run();
                }
                catch(BuildFailedException ex)
                {
                    Debug.LogError($"build failed at stage {ex.stage}");
                    LogWave(ex.index);
                    return 0;
                }
#else
                result = Run();
#endif

                if (result == Result.Success)
                {
                    Profiling.StartTimer(fill_leveldata_pk + threadID);
                    FillLevelData();
                    Profiling.LogAndRemoveTimer("time to fill level data :", fill_leveldata_pk + threadID);

                    return t + 1;
                }
            }
            return 0;
        }

        private Result Run()
        {
            BaseClear();

            Profiling.StartTimer(fill_pk + threadID);
            Fill();
            Profiling.PauseTimer(fill_pk + threadID);

            Profiling.StartTimer(interior_boundary_pk + threadID);
            ValidateInteriorBoundary();
            Profiling.PauseTimer(interior_boundary_pk + threadID);

            Profiling.StartTimer(exterior_boundar_pk + threadID);
            ValidateExteriorBoundary();
            Profiling.PauseTimer(exterior_boundar_pk + threadID);

            Profiling.StartTimer(observe_pk + threadID, true);
            Profiling.StartTimer(propagate_pk + threadID, true);
            while (true)
            {
                Profiling.ResumeTimer(propagate_pk + threadID);
                Propagate();
                Profiling.PauseTimer(propagate_pk + threadID);

                Profiling.ResumeTimer(observe_pk + threadID);
                var result = Observe();
                Profiling.PauseTimer(observe_pk + threadID);

                if (result != Result.Ongoing)
                {
                    Profiling.LogAndRemoveTimer("filling time", fill_pk + threadID);
                    Profiling.LogAndRemoveTimer("interior boundary", interior_boundary_pk + threadID);
                    Profiling.LogAndRemoveTimer("exterior boundary", exterior_boundar_pk + threadID);
                    Profiling.LogAndRemoveTimer("propagate", propagate_pk + threadID);
                    Profiling.LogAndRemoveTimer("observe", observe_pk + threadID);
                    return result;
                }
            }
        }

        protected abstract void FillLevelData();
        protected abstract void Clear();
        protected abstract void Fill();
        protected abstract void ValidateInteriorBoundary();
        protected abstract void ValidateExteriorBoundary();
        protected abstract void Propagate();
        protected abstract Result Observe();
        protected abstract void Ban(Possibility poss);
        protected abstract IEnumerable<int> EnumareteBlocksInWaveCell(Vector3Int index);

        public void SetRepo(BlocksRepo.Runtime repo)
        {
            this.repo = repo;

            blockWeights.Clear();
            blockWeights.AddRange(repo.GetBlocksWeight());
            GenerateGroupsWeights();
        }

        public void SetlevelData(LevelData levelData)
        {
            this.levelData = levelData;
        }

        public void SetInputWave(Array3D<InputWaveCell> inputWave)
        {
            if(inputWave != null)
            {
                if (inputWave.Size.x == 0 || inputWave.Size.y == 0 || inputWave.Size.z == 0)
                    throw new ZeroVolumeException();
            }

            this.inputWave = inputWave;
        }

        public void SetBoundary(ILevelBoundary boundary, Direction d)
        {
            boundaries[(int)d] = boundary;
        }

        public void SetGroupBoundary(string GroupName, Direction d)
        {
            boundaries[(int)d] = new GroupsBoundary(repo.GetGroupIndex(GroupName));
        }

        public void OverrideGroupsWeights(List<float> groupOverride)
        {
            if (repo == null)
                throw new NoRepoException();

            blockWeights.Clear();
            blockWeights.AddRange(repo.GetBlocksWeight());

            for (int i = 0; i < repo.WeightGroupsCount; i++)
            {
                var newValue = groupOverride[i];
                if (newValue < 0)
                    continue;

                var wgBlocks = repo.GetBlocksPerWeightGroup(i);
                foreach (var block in wgBlocks)
                    blockWeights[block] = newValue;
            }

            GenerateGroupsWeights();
        }

        private void GenerateGroupsWeights()
        {
            if (groupWeights == null)
                groupWeights = new List<float>();
            groupWeights.Clear();

            for (int i = 0; i < repo.GroupsCount; i++)
            {
                var groupRange = repo.GetGroupRange(i);
                var weight = 0f;
                for (int j = groupRange.x; j < groupRange.y; j++)
                    weight += blockWeights[j];
                groupWeights.Add(weight);
            }
        }

        private void BaseClear()
        {
            if (blocksCounter == null || blocksCounter.Length != repo.BlocksCount)
                blocksCounter = new int[repo.BlocksCount];
            blocksCounter.Fill(() => 0);

            for (int i = 0; i < repo.BlocksCount; i++)
                weightsSum += blockWeights[i];

            Clear();
        }

        protected int StablePick(int[] blocks)
        {
            float invBCount = 1f / blocksCount;
            float invWeightsSum = 1f / weightsSum;

            float sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i]; var weight = blockWeights[b];
                //exclude what reaches the threshold
                if ((blocksCounter[b] + 1) * invBCount < weight * invWeightsSum)
                    sum += weight;
            }

            var r = GetNextRand(rand) * sum;
            sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i]; var weight = blockWeights[b];
                if ((blocksCounter[b] + 1) * invBCount < weight * invWeightsSum)
                {
                    sum += weight;
                    if (sum > r)
                        return i;
                }
            }

            return Pick(blocks);
        }
        protected int Pick(int[] blocks)
        {
            float sum = 0;
            for (int i = 0; i < blocks.Length; i++)
                sum += blockWeights[blocks[i]];
            var r = GetNextRand(rand) * sum;
            sum = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                sum += blockWeights[blocks[i]];
                if (sum > r)
                    return i;
            }
            return blocks.Length - 1;
        }
        protected float GetNextRand(System.Random rand)
        {
            return rand.Next(0, 100000) * 0.00001f;
        }

        protected void ValidateExteriorSide(Vector3Int li, int d, ILevelBoundary picker
            , List<int> rlist, HashSet<int> rset)
        {
            var nli = levelData.position + li + delta[d];
            var result = picker.Evaluate(nli);
            switch (result.option)
            {
                case LevelBoundaryOption.Block:
                    ValidateWaveSide(li, result.block, d, rset);
                    break;
                case LevelBoundaryOption.Group:
                    ValidateWaveSide(li, result.waveBlock, d, rlist);
                    break;
            }
#if AUTOLEVEL_DEBUG
            var index = li - solveBounds.min;
            if (EnumareteBlocksInWaveCell(index).Count() == 0)
                throw new BuildFailedException(SolveStage.ValidateExteriorBoundary, index);
#endif
        }
        protected void ValidateInternalSide(Vector3Int li, int d
            , List<int> rlist, HashSet<int> rset)
        {
            Vector3Int nli = li + delta[d];
            var nlc = levelData.Blocks[nli.z, nli.y, nli.x];
            if (nlc == 0)
            {
                var iwc = inputWave == null ? InputWaveCell.AllGroups : inputWave[li.z, li.y, li.x];
                if (iwc.ContainAll)
                    return;

                ValidateWaveSide(li, iwc, d, rlist);
            }
            else
                ValidateWaveSide(li, nlc, d, rset);
#if AUTOLEVEL_DEBUG
            var index = li - solveBounds.min;
            if (EnumareteBlocksInWaveCell(index).Count() == 0)
                throw new BuildFailedException(SolveStage.ValidateInteriorBoundary, index);
#endif
        }


        private void ValidateWaveSide(Vector3Int li, InputWaveCell niwc, int d, List<int> rlist)
        {
            var index = li - solveBounds.min;
            var iwc = inputWave == null ? InputWaveCell.AllGroups : inputWave[li.z, li.y, li.x];

            if (iwc.ContainAll && niwc.ContainAll)
                return;

            int[] counter = new int[repo.BlocksCount];
            CountConnections(iwc, niwc, d, counter);

            rlist.Clear();
            foreach (var b in EnumareteBlocksInWaveCell(index))
            {
                if (counter[b] <= 0)
                    rlist.Add(b);
            }

            for (int i = 0; i < rlist.Count; i++)
                Ban(new Possibility(index, rlist[i]));
        }
        private void ValidateWaveSide(Vector3Int lc, int nlc, int d, HashSet<int> rset)
        {
            var index = lc - solveBounds.min;

            nlc = repo.GetBlockIndex(nlc);
            var neighborConn = repo.Connections[opposite[d]][nlc];

            rset.Clear();
            foreach (var b in EnumareteBlocksInWaveCell(index))
                rset.Add(b);

            rset.ExceptWith(neighborConn);
            foreach (var item in rset)
                Ban(new Possibility(index, item));

        }

        protected void CountConnections(InputWaveCell cellA, InputWaveCell cellB, int d, int[] output)
        {
            var groupCounter = repo.groupCounter[d];

            for (int group_a = 0; group_a < repo.GroupsCount; group_a++)
            {
                if (!cellA[group_a])
                    continue;

                var groupCounter_a = groupCounter[group_a];
                var groupRange = repo.GetGroupRange(group_a);

                for (int group_b = 0; group_b < repo.GroupsCount; group_b++)
                {
                    if (!cellB[group_b])
                        continue;

                    var counter = groupCounter_a[group_b];
                    for (int i = groupRange.x; i < groupRange.y; i++)
                        output[i] += counter[i - groupRange.x];
                }
            }
        }

        private bool VerifyWave()
        {
            foreach(var index in SolverVolume)
            {
                if( EnumareteBlocksInWaveCell(index).Count() == 0)
                {
                    Debug.Log("Verify wave failed!");
                    LogWave(index);
                    return false;
                }
            }
            return true;
        }

        protected void LogWave(Vector3Int index)
        {
            var leveIndex = solveBounds.min + index;
            var wPos = levelData.position + leveIndex;

            var root = new GameObject("Autolevel fail log");
            root.transform.position = wPos;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = wPos + Vector3.one * 0.5f;
            cube.transform.SetParent(root.transform);

#if UNITY_EDITOR
            if(UnityEditor.EditorApplication.isPlaying)
                cube.GetComponent<MeshRenderer>().material.color = Color.red;
#endif

            string message = "build failed \n";
            message += "input wave info:\n";
            if (inputWave != null)
            {
                message += $"center: ";
                var iwc = inputWave[leveIndex];
                for (int i = 0; i < repo.GroupsCount; i++)
                {
                    if (iwc[i])
                        message += $"{repo.GetGroupName(i)}, ";
                }
                message += "\n";

            }

            for (int d = 0; d < 6; d++)
            {
                var n = index + delta[d];
                var cell = new GameObject(n.ToString());
                cell.transform.SetParent(root.transform);
                cell.transform.localPosition = delta[d];
                if (OnBoundary(n))
                    continue;
                foreach (var block in EnumareteBlocksInWaveCell(n))
                {
                    var res = repo.GetBlockResources(block);
                    var poss = new GameObject();

                    if (res.mesh != null)
                    {
                        poss.name = res.mesh.name;
                        poss.AddComponent<MeshFilter>().sharedMesh = res.mesh;
                        poss.AddComponent<MeshRenderer>().sharedMaterial = res.material;
                    }
                    else
                    {
                        var solid = repo.GetGroupRange(repo.GetGroupIndex(BlocksRepo.SOLID_GROUP)).x;
                        var empty = repo.GetGroupRange(repo.GetGroupIndex(BlocksRepo.EMPTY_GROUP)).x;

                        if (block == solid)
                            poss.name = BlocksRepo.SOLID_GROUP;
                        else if (block == empty)
                            poss.name = BlocksRepo.EMPTY_GROUP;
                    }

                    poss.transform.SetParent(cell.transform, false);
                }

                if(inputWave != null)
                {
                    message += $"{(Direction)d}: ";
                    var iwc = inputWave[leveIndex + delta[d]];
                    for (int i = 0; i < repo.GroupsCount; i++)
                    {
                        if (iwc[i])
                            message += $"{repo.GetGroupName(i)}, ";
                    }
                    message += "\n";
                }
            }

            Debug.Log(message, cube);
        }

        protected bool OnBoundary(Vector3Int index) => OnBoundary(index.x, index.y, index.z, Vector3Int.zero, solveBounds.size);
        protected bool OnBoundary(int x, int y, int z, Vector3Int start, Vector3Int end)
        {
            return (x < start.x || y < start.y || z < start.z ||
                x >= end.x || y >= end.y || z >= end.z);
        }
    }
}