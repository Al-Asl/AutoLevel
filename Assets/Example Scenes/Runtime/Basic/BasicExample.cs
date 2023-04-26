using UnityEngine;
using AlaslTools;

namespace AutoLevel.Examples
{

    public class BasicExample : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("zero means the seed is random")]
        public int seed;
        [SerializeField]
        public BlocksRepo repo;
        [SerializeField]
        public BoundsInt bounds;

        private BlocksRepo.Runtime runtimeRepo;
        private LevelMeshBuilder meshBuilder;
        private LevelData levelData;
        private LevelSolver solver;

        private void OnEnable()
        {
            //generate blocks connections, variants and other configuration
            BlocksRepo.Runtime runtimeRepo = repo.CreateRuntime();

            //a container for the solver result
            levelData = new LevelData(bounds);
            meshBuilder = new LevelMeshBuilder(levelData, runtimeRepo);

            solver = new LevelSolver(bounds.size);
            solver.SetRepo(runtimeRepo);
            solver.SetlevelData(levelData);

            //set the bottom boundary
            solver.SetGroupBoundary(BlocksRepo.SOLID_GROUP, Direction.Down);
        }

        private void OnDisable()
        {
            runtimeRepo.Dispose();
            meshBuilder.Dispose();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
                Rebuild();
        }

        void Rebuild()
        {
            //run the solver, return true on success
            if (solver.SolveAll(seed: seed))
            {
                //rebuild the mesh if the solver success
                meshBuilder.RebuildAll();
            }
        }
    }

}