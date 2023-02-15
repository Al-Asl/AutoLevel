using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AutoLevel
{
    public class LevelGroupManager : BaseLevelGroupManager<LevelBuilder.Data>
    {
        public LevelGroupManager(bool useSolverMT = false) : base(useSolverMT) { }

        protected override LevelBuilder.Data GetBuilderData(LevelBuilder builder)
        => builder.data;
    }

    public abstract class BaseLevelGroupManager<T> : System.IDisposable
        where T : ILevelBuilderData
    {
        private List<HashSet<T>> builderGroups;
        private List<LevelBuilderUtlity.LevelGroupSolver> groupBuilders;
        private List<T> buildersData;
        private Dictionary<BlocksRepo, BlocksRepo.Runtime> repos;

        public BaseLevelGroupManager(bool useSolverMT = false)
        {
            var allBuilders = Object.FindObjectsOfType<LevelBuilder>();
            List<LevelBuilder> validBuilders = new List<LevelBuilder>();
            repos = new Dictionary<BlocksRepo, BlocksRepo.Runtime>();

            foreach (var builder in allBuilders)
            {
                var data = builder.data;

                if (data.BlockRepo == null)
                    continue;

                validBuilders.Add(builder);

                if (!repos.ContainsKey(data.BlockRepo))
                    repos.Add(data.BlockRepo, data.BlockRepo.CreateRuntime());
            }

            buildersData = new List<T>();
            foreach (var builder in validBuilders)
                buildersData.Add(GetBuilderData(builder));

            builderGroups = LevelBuilderUtlity.GroupBuilders(buildersData);

            groupBuilders = new List<LevelBuilderUtlity.LevelGroupSolver>(builderGroups.Count);
            foreach(var group in builderGroups)
            {
                var groupRepos = new List<BlocksRepo.Runtime>(group.Count);
                foreach(var builder in group)
                    groupRepos.Add(repos[builder.BlockRepo]);

                groupBuilders.Add(new LevelBuilderUtlity.LevelGroupSolver(group.Cast<ILevelBuilderData>(), groupRepos, useSolverMT));
            }
        }

        protected abstract T GetBuilderData(LevelBuilder builder);

        public int GroupCount => builderGroups.Count;
        public IEnumerable<T> GetBuilderGroup(int i) => builderGroups[i];
        public BlocksRepo.Runtime GetRepo(T builder) => repos[builder.BlockRepo];

        public void ClearGroup(int i)
        {
            foreach (var builder in builderGroups[i])
                LevelBuilderUtlity.ClearBuild(builder);
        }

        public bool Solve(int index)
        {
            return groupBuilders[index].Rebuild();
        }

        public bool Rebuild(int index, bool[] mask)
        {
            var buildersData = new List<LevelBuilder>();
            int i = 0;
            foreach (var builderData in builderGroups[index])
            {
                if (mask[i++])
                    buildersData.Add(builderData.Builder);
            }

            return groupBuilders[index].Rebuild(buildersData);
        }

        public void Dispose()
        {
            foreach (var repo in repos)
                repo.Value.Dispose();
        }
    }
}
