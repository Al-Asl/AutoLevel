using UnityEngine;
using AlaslTools;

namespace AutoLevel.Examples
{

    public class BasicExample : MonoBehaviour
    {
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
                Rebuild(bounds);
        }

        void Rebuild(BoundsInt bounds)
        {
            //run the solver, this will return the number of iteration it took,
            //0 means the solver has failed
            var iterations = solver.Solve(bounds);
            if (iterations > 0)
            {
                //rebuild the mesh if the solver success
                meshBuilder.Rebuild(bounds);
            }
        }
    }

}