using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AutoLevel
{
    public class LevelGroupManager<T> : System.IDisposable
        where T : ILevelBuilderData
    {
        private List<HashSet<T>> builderGroups;
        private List<LevelBuilderUtlity.LevelGroupBuilder> groupBuilders;
        private List<T> buildersData;
        private Dictionary<BlocksRepo, BlocksRepo.Runtime> repos;

        public LevelGroupManager(System.Func<LevelBuilder, T> Const)
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
                buildersData.Add(Const(builder));

            builderGroups = LevelBuilderUtlity.GroupBuilders(buildersData);

            groupBuilders = new List<LevelBuilderUtlity.LevelGroupBuilder>(builderGroups.Count);
            foreach(var group in builderGroups)
            {
                var groupRepos = new List<BlocksRepo.Runtime>(group.Count);
                foreach(var builder in group)
                    groupRepos.Add(repos[builder.BlockRepo]);

                groupBuilders.Add(new LevelBuilderUtlity.LevelGroupBuilder(group.Cast<ILevelBuilderData>(), groupRepos));
            }
        }

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
            var builders = new List<ILevelBuilderData>();
            int i = 0;
            foreach (var builder in builderGroups[index])
            {
                if (mask[i++])
                    builders.Add(builder);
            }

            return groupBuilders[index].Rebuild(builders);
        }

        public void Dispose()
        {
            foreach (var repo in repos)
                repo.Value.Dispose();
        }
    }
}
