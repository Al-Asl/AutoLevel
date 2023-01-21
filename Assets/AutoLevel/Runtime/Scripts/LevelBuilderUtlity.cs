using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoLevel
{
    public static class LevelBuilderUtlity
    {
        class LevelGroupBuilder
        {
            private Stack<ILevelBuilderData> toBuild = new Stack<ILevelBuilderData>();
            private LinkedList<(ILevelBuilderData, HashSet<LevelBuilder>)> buildersDep;
            private Dictionary<LevelBuilder, ILevelBuilderData> BuilderToData;
            private readonly object toBuildLock = new object();
            private CancellationTokenSource source;
            private Func<LevelBuilder, BlocksRepo.Runtime> repoSolver;

            private const int iterations = 3;

            public bool Rebuild(
                IEnumerable<ILevelBuilderData> builders,
                IEnumerable<ILevelBuilderData> targetBuilders,
                System.Func<LevelBuilder, BlocksRepo.Runtime> GetRepo)
            {
                BuilderToData = new Dictionary<LevelBuilder, ILevelBuilderData>();
                foreach (var builder in builders)
                    BuilderToData[builder.Builder] = builder;

                repoSolver = GetRepo;

                for (int i = 0; i < iterations; i++)
                {
                    if (Run(targetBuilders))
                        return true;
                }
                return false;
            }

            private bool Run(IEnumerable<ILevelBuilderData> builders)
            {
                GenerateBuildersDeps(builders);

                source = new CancellationTokenSource();

                while (buildersDep.Count > 0)
                {
                    if (toBuild.Count == 0)
                        Enqueue();

                    Build();

                    if (source.IsCancellationRequested)
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
                        var builderData = toBuild.Pop();

                        tasks[i] = Task.Run(() =>
                        {
                            if (!source.IsCancellationRequested)
                            {
                                var watch = System.Diagnostics.Stopwatch.StartNew();

                                var bounds = new BoundsInt(Vector3Int.zero, builderData.LevelData.Blocks.Size);
                                var solver = new LevelSolver(bounds.size);
                                UpdateLevelSolver(builderData, repoSolver(builderData.Builder), solver);
                                var itr = solver.Solve(bounds);

                                if (itr > 0)
                                {
                                    lock (toBuildLock)
                                    {
                                        Submit(builderData);
                                    }
                                    Build();
                                }
                                else
                                    source.Cancel();
                            }
                        });
                    }
                }

                Task.WaitAll(tasks);
            }

            public void UpdateLevelSolver(ILevelBuilderData builderData, BlocksRepo.Runtime repo, LevelSolver solver)
            {
                var levelData = builderData.LevelData;

                var weightOverride = new List<float>();
                foreach (var g in builderData.GroupsWeights)
                    weightOverride.Add(g.overridWeight ? g.Weight : -1);

                solver.SetRepo(repo);
                solver.OverrideGroupsWeights(weightOverride);
                solver.SetlevelData(levelData);
                solver.SetInputWave(builderData.InputWave);

                SetSolverBoundary(solver, repo, builderData, (builder) => BuilderToData[builder]);
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

            private void GenerateBuildersDeps(IEnumerable<ILevelBuilderData> builders)
            {
                buildersDep = new LinkedList<(ILevelBuilderData, HashSet<LevelBuilder>)>();
                var set = new HashSet<LevelBuilder>(builders.Select((b)=>b.Builder));

                foreach (var builder in builders)
                {
                    var dep = new HashSet<LevelBuilder>();

                    var boundariesLevel = builder.BoundarySettings.levelBoundary;
                    for (int d = 0; d < 6; d++)
                    {
                        var boundaryLevel = boundariesLevel[d];
                        if (boundaryLevel != null && set.Contains(boundaryLevel))
                            dep.Add(boundaryLevel);
                    }

                    buildersDep.AddLast((builder, dep));
                }
            }

            private void Enqueue()
            {
                if (buildersDep.Count == 0)
                    return;

                var builders = buildersDep.ToList();
                builders.Sort((a, b) => a.Item2.Count.CompareTo(b.Item2.Count));
                buildersDep = new LinkedList<(ILevelBuilderData, HashSet<LevelBuilder>)>(builders);

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
            IEnumerable<ILevelBuilderData> targetBuilders,
            Func<LevelBuilder, BlocksRepo.Runtime> GetRepo)
        {
            return new LevelGroupBuilder().Rebuild(allBuilders, targetBuilders, GetRepo);
        }

        public static void ClearBuild(ILevelBuilderData builderData)
        {
            var levelBlocks = builderData.LevelData.Blocks;
            foreach (var index in SpatialUtil.Enumerate(levelBlocks.Size))
                levelBlocks[index.z, index.y, index.x] = 0;
        }

        public static void UpdateLevelSolver(ILevelBuilderData builderData, BlocksRepo.Runtime repo, LevelSolver solver)
        {
            var levelData = builderData.LevelData;

            var weightOverride = new List<float>();
            foreach (var g in builderData.GroupsWeights)
                weightOverride.Add(g.overridWeight ? g.Weight : -1);

            solver.SetRepo(repo);
            solver.OverrideGroupsWeights(weightOverride);
            solver.SetlevelData(levelData);
            solver.SetInputWave(builderData.InputWave);
            SetSolverBoundary(solver, repo, builderData , (builder)=> builder.data);
        }

        private static void SetSolverBoundary(
            LevelSolver solver, BlocksRepo.Runtime repo, 
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