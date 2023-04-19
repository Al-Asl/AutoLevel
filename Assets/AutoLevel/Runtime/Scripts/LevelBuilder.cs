using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public interface ILevelBuilderData
    {
        LevelBuilder Builder { get; }
        BlocksRepo BlockRepo { get; }
        List<LevelBuilder.GroupSettings> GroupsWeights { get; }
        LevelBuilder.BoundarySettings BoundarySettings { get; }
        LevelData LevelData { get; }
        Array3D<InputWaveCell> InputWave { get; }
    }

    [AddComponentMenu("AutoLevel/Level Builder")]
    public class LevelBuilder : MonoBehaviour
    {
        [System.Serializable]
        public class GroupSettings
        {
            public int hash;
            public bool overridWeight;
            public float Weight;
        }

        [System.Serializable]
        public class GroupBoundaryEntry
        {
            public List<int> groups = new List<int>();
        }

        [System.Serializable]
        public class BoundarySettings
        {
            public GroupBoundaryEntry[] groupsBoundary = new GroupBoundaryEntry[6];
            public LevelBuilder[] levelBoundary = new LevelBuilder[6];
        }

        public struct Data : ILevelBuilderData
        {
            public Data(LevelBuilder builder)
            {
                Builder = builder;
            }

            public LevelBuilder Builder                 { get; set; }
            public bool UseMutliThreadedSolver          => Builder.useMutliThreadedSolver;
            public BlocksRepo BlockRepo                 => Builder.blockRepo;
            public List<GroupSettings> GroupsWeights    => Builder.groupsWeights;
            public BoundarySettings BoundarySettings    => Builder.boundarySettings;
            public LevelData LevelData                  => Builder.levelData;
            public Array3D<InputWaveCell> InputWave     => Builder.inputWave;
        }

        public Data data => new Data(this);

        const int k_start_size = 5;

        [SerializeField]
        private BlocksRepo blockRepo;

        [SerializeField]
        private List<GroupSettings> groupsWeights = new List<GroupSettings>();
        [SerializeField]
        private BoundarySettings boundarySettings = new BoundarySettings();

        [SerializeField]
        private bool useMutliThreadedSolver;
        [SerializeField]
        private BoundsInt selection = new BoundsInt(Vector3Int.zero, Vector3Int.one);
        [SerializeField]
        private LevelData levelData = new LevelData(new BoundsInt(Vector3Int.zero, Vector3Int.one * k_start_size));
        [SerializeField]
        private Array3D<InputWaveCell> inputWave = new Array3D<InputWaveCell>(Vector3Int.one * k_start_size);

#if UNITY_EDITOR
        [SerializeField]
        private bool rebuild_bottom_layers_editor_only;
#endif

        private BaseLevelSolver solver;
        private BlocksRepo.Runtime repo;
        private LevelMeshBuilder meshBuilder;

        [ContextMenu("Rebuild")]
        public bool Rebuild()
        {
            for (int i = 0; i < levelData.LayersCount; i++)
            {
                if (!Rebuild(new BoundsInt(Vector3Int.zero, levelData.size), i))
                    return false;
            }
            return true;
        }

        public bool Rebuild(BoundsInt region,int layer)
        {
            if(solver == null)
            {
                if(useMutliThreadedSolver)
                    solver = new LevelSolverMT(levelData.size);
                else
                    solver = new LevelSolver(levelData.size);

                repo = blockRepo.CreateRuntime();
                meshBuilder = new LevelMeshBuilder(levelData, repo);
            }
            LevelBuilderUtlity.UpdateLevelSolver(data, repo, solver);
            var itr = solver.Solve(region, layer);
            if (itr == 0)
                return false;
            else
            {
                meshBuilder.Rebuild(region, layer);
                return true;
            }
        }

        private void OnDisable()
        {
            repo?.Dispose();
            meshBuilder?.Dispose();
        }

    }
}