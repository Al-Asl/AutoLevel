using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public static class LevelBuilderUtlity
    {
        public class LevelGroupSolver
        {
            class BuilderResource
            {
                public ILevelBuilderData builderData;
                public BlocksRepo.Runtime repo;
                public BaseLevelSolver solver;
            }

            private Dictionary<LevelBuilder, BuilderResource> BuildersResources;

            private readonly object toBuildLock = new object();
            private Stack<LevelBuilder> toBuild = new Stack<LevelBuilder>();

            private LinkedList<(LevelBuilder, HashSet<LevelBuilder>)> buildersDep;

            private CancellationTokenSource cancelSource;
            private int iterations = 1;

            public LevelGroupSolver(IEnumerable<ILevelBuilderData> buildersData, IEnumerable<BlocksRepo.Runtime> repos, bool useSolverMT = false, int iterations = 1)
            {
                this.iterations = iterations;

                BuildersResources = new Dictionary<LevelBuilder, BuilderResource>();
                var reposEnum = repos.GetEnumerator();
                foreach (var builderData in buildersData) {
                    reposEnum.MoveNext();

                    BaseLevelSolver solver = null;
                    if (useSolverMT)
                        solver = new LevelSolverMT(builderData.LevelData.Blocks.Size);
                    else
                        solver = new LevelSolver(builderData.LevelData.Blocks.Size);

                    BuildersResources[builderData.Builder] = new BuilderResource()
                    {
                        builderData = builderData,
                        repo = reposEnum.Current,
                        solver = solver
                    };
                }
            }

            public bool Rebuild()
            {
                for (int i = 0; i < iterations; i++)
                {
                    if (Run(BuildersResources.Select((pair) => pair.Key)))
                        return true;
                }
                return false;
            }

            public bool Rebuild(IEnumerable<LevelBuilder> targetBuilders)
            {
                for (int i = 0; i < iterations; i++)
                {
                    if (Run(targetBuilders))
                        return true;
                }
                return false;
            }

            private bool Run(IEnumerable<LevelBuilder> builders)
            {
                GenerateBuildersDep(builders);

                cancelSource = new CancellationTokenSource();

                while (buildersDep.Count > 0)
                {
                    if (toBuild.Count == 0)
                        Enqueue();

                    Build();

                    if (cancelSource.IsCancellationRequested)
                        return false;
                }

                return true;
            }

            void Build()
            {
                if (toBuild.Count == 0)
                    return;

                var tasks = new Task[toBuild.Count];

                lock(toBuildLock)
                {
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        var builderResource = BuildersResources[toBuild.Pop()];
                        var token = cancelSource.Token;
                        var time = (int)(DateTime.Now.Ticks % int.MaxValue);

                        tasks[i] = Task.Run(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                var bounds = new BoundsInt(Vector3Int.zero, builderResource.builderData.LevelData.Blocks.Size);
                                UpdateSolver(builderResource);
                                var itr = builderResource.solver.Solve(bounds, 1, i + time);

                                if (itr > 0)
                                {
                                    lock (toBuildLock)
                                    {
                                        Submit(builderResource.builderData);
                                    }
                                    Build();
                                }
                                else
                                    cancelSource.Cancel();
                            }
                        });
                    }
                }

                Task.WaitAll(tasks);
            }

            private void UpdateSolver(BuilderResource builderResource)
            {
                var builderData = builderResource.builderData;
                var solver = builderResource.solver;
                var repo = builderResource.repo;

                var levelData = builderData.LevelData;

                var weightOverride = new List<float>();
                foreach (var g in builderData.GroupsWeights)
                    weightOverride.Add(g.overridWeight ? g.Weight : -1);

                solver.SetRepo(repo);
                solver.OverrideGroupsWeights(weightOverride);
                solver.SetlevelData(levelData);
                solver.SetInputWave(builderData.InputWave);

                SetSolverBoundary(solver, repo, builderData, (builder) => BuildersResources[builder].builderData);
            }

            private void Submit(ILevelBuilderData builderData)
            {
                var node = buildersDep.First;
                while (node != null)
                {
                    var n = node;
                    node = node.Next;

                    var deps = n.Value.Item2;
                    deps.Remove(builderData.Builder);

                    if (deps.Count == 0)
                    {
                        toBuild.Push(n.Value.Item1);
                        buildersDep.Remove(n);
                    }
                }
            }

            private void GenerateBuildersDep(IEnumerable<LevelBuilder> builders)
            {
                buildersDep = new LinkedList<(LevelBuilder, HashSet<LevelBuilder>)>();
                var allBuilders = new HashSet<LevelBuilder>(builders);

                foreach (var builder in builders)
                {
                    var dep = new HashSet<LevelBuilder>();

                    var boundariesLevel = builder.data.BoundarySettings.levelBoundary;
                    for (int d = 0; d < 6; d++)
                    {
                        var boundaryLevel = boundariesLevel[d];
                        if (boundaryLevel != null && allBuilders.Contains(boundaryLevel))
                            dep.Add(boundaryLevel);
                    }

                    buildersDep.AddLast((builder, dep));
                }
            }

            private void Enqueue()
            {
                if (buildersDep.Count == 0)
                    return;

                //TODO : use merge sort for linked list
                var builders = buildersDep.ToList();
                builders.Sort((a, b) => a.Item2.Count.CompareTo(b.Item2.Count));
                buildersDep = new LinkedList<(LevelBuilder, HashSet<LevelBuilder>)>(builders);

                var node = buildersDep.First;
                int depCount = node.Value.Item2.Count;
                do
                {
                    var n = node;
                    node = node.Next;
                    toBuild.Push(n.Value.Item1);
                    buildersDep.Remove(n);
                }
                while (node != null && node.Value.Item2.Count == depCount);
            }
        }

        public static bool RebuildLevelGroup( 
            IEnumerable<ILevelBuilderData> allBuilders,
            IEnumerable<BlocksRepo.Runtime> repos,
            IEnumerable<ILevelBuilderData> targetBuilders)
        {
            var groupBuilder = new LevelGroupSolver(allBuilders,repos);
            return groupBuilder.Rebuild(targetBuilders.Select((builderData)=>builderData.Builder));
        }

        public static void ClearBuild(ILevelBuilderData builderData)
        {
            var levelBlocks = builderData.LevelData.Blocks;
            foreach (var index in SpatialUtil.Enumerate(levelBlocks.Size))
                levelBlocks[index.z, index.y, index.x] = 0;
        }

        public static void UpdateLevelSolver(ILevelBuilderData builderData, BlocksRepo.Runtime repo, BaseLevelSolver solver)
        {
            var levelData = builderData.LevelData;

            var weightOverride = new List<float>();
            foreach (var g in builderData.GroupsWeights)
                weightOverride.Add(g.overridWeight ? g.Weight : -1);

            solver.SetRepo(repo);
            solver.SetlevelData(levelData);
            solver.OverrideGroupsWeights(weightOverride);
            solver.SetInputWave(builderData.InputWave);
            SetSolverBoundary(solver, repo, builderData , (builder)=> builder.data);
        }

        private static void SetSolverBoundary(
            BaseLevelSolver solver, BlocksRepo.Runtime repo, 
            ILevelBuilderData builderData,Func<LevelBuilder,ILevelBuilderData>  GetBuilderData)
        {
            for (int d = 0; d < 6; d++)
            {
                var boundaryBuilder = builderData.BoundarySettings.levelBoundary[d];
                var groups = builderData.BoundarySettings.groupsBoundary[d].groups;

                GroupsBoundary groupBoundary = null;
                if (groups.Count > 0)
                {
                    var groupsIndices = new List<int>();
                    foreach (var g in groups)
                        groupsIndices.Add(repo.GetGroupIndex(g));
                    groupBoundary = new GroupsBoundary(new InputWaveCell(groupsIndices));
                }

                if (boundaryBuilder != null)
                {
                    var data = GetBuilderData(boundaryBuilder);
                    solver.SetBoundary(new LevelBoundary(data.LevelData, data.InputWave, groupBoundary), (Direction)d);
                }
                else
                    solver.SetBoundary(groupBoundary, (Direction)d);
            }
        }

        public static List<HashSet<T>> GroupBuilders<T>(IEnumerable<T> builders) where T : ILevelBuilderData
        {
            var adjacencyList = new Dictionary<T, HashSet<T>>();

            foreach (var builder in builders)
                adjacencyList[builder] = new HashSet<T>();

            foreach (var builder in builders)
            {
                var bSettings = builder.BoundarySettings;
                for (int d = 0; d < 6; d++)
                {
                    var boundaryLevel = bSettings.levelBoundary[d];
                    if (boundaryLevel != null)
                    {
                        var boundaryLevelSO = builders.First((b) => b.Builder == boundaryLevel);
                        adjacencyList[builder].Add(boundaryLevelSO);
                        adjacencyList[boundaryLevelSO].Add(builder);
                    }
                }
            }

            var buildersGroups = new List<HashSet<T>>();

            while (adjacencyList.Count > 0)
            {
                var builderGroups = new HashSet<T>();
                FillBuilderGroup(adjacencyList.First().Key, adjacencyList, builderGroups);

                foreach (var builder in builderGroups)
                    adjacencyList.Remove(builder);

                buildersGroups.Add(builderGroups);
            }

            return buildersGroups;
        }

        private static void FillBuilderGroup<T>(
            T target,
            Dictionary<T, HashSet<T>> adjacencyList,
            HashSet<T> buildersGroup) where T : ILevelBuilderData
        {
            if (target == null || buildersGroup.Contains(target))
                return;

            buildersGroup.Add(target);

            foreach (var builder in adjacencyList[target])
                FillBuilderGroup(builder, adjacencyList, buildersGroup);
        }

    }
}