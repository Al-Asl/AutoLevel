using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using AlaslTools;

namespace AutoLevel.Examples
{
    // build an infinite level on the z axis
    public class InfiniteBuilderExample : MonoBehaviour
    {
        private class Tile
        {
            public LevelData leveData;
            public Array3D<InputWaveCell> wave;
            public BaseLevelDataBuilder dataBuilder;
            public int pos = int.MinValue;

            public Tile(Vector3Int size, BlocksRepo.Runtime runtimeRepo)
            {
                leveData = new LevelData(new BoundsInt(Vector3Int.zero, size));
                dataBuilder = new LevelObjectBuilder(leveData, runtimeRepo);
                wave = new Array3D<InputWaveCell>(size);
            }

            public void Dispose()
            {
                dataBuilder.Dispose();
            }
        }

        [System.Serializable]
        public class GroupWeightOverride
        {
            public string name;
            public float value;
        }

        [Tooltip("max solver iterations")]
        public int maxIterations = 10;
        public bool RebuildAsync;
        public bool UseMultiThreadedSolver;
        [Space]
        public BlocksRepo repo;
        public List<string> excludedGroups;
        public List<GroupWeightOverride> weightsOverride;
        public Vector3Int size;
        [Space]
        public Transform target;

        private Tile A, B;

        // this example is a linear level builder, so it only need one solver
        private BaseLevelSolver solver;
        private readonly object solverLock = new object();

        private BlocksRepo.Runtime runtimeRepo;

        // the solved level tile ready to be presented by game objects
        private Stack<Tile> toBuild = new Stack<Tile>();
        private readonly object toBuildLock = new object();

        // distance in world unit between each tile
        private int step;
        private int xoffset;

        private void OnEnable()
        {
            runtimeRepo = repo.CreateRuntime();

            if (UseMultiThreadedSolver)
                solver = new LevelSolverMT(size);
            else
                solver = new LevelSolver(size);

            solver.SetRepo(runtimeRepo);
            for (int d = 0; d < 6; d++)
                if (d != (int)Direction.Forward)
                    solver.SetGroupBoundary("Solid", (Direction)d);
            solver.OverrideGroupsWeights(GetWeightGroupOverride());

            step = size.z;
            xoffset = -size.x / 2;

            A = new Tile(size, runtimeRepo);
            B = new Tile(size, runtimeRepo);
        }

        // override the group weight
        private List<float> GetWeightGroupOverride()
        {
            List<float> groupWeightsOverride = new List<float>();
            for (int i = 0; i < runtimeRepo.WeightGroupsCount; i++)
                // the override with -1 means no override
                groupWeightsOverride.Add(-1);
            foreach (var item in weightsOverride)
                groupWeightsOverride[runtimeRepo.GetWeightGroupIndex(item.name)] = item.value;
            return groupWeightsOverride;
        }

        private void Update()
        {
            // using the target position to decide what tile to build and the other as dependency
            int targetPos = Mathf.FloorToInt((target.position.z + step * 0.5f) / step);
            if (targetPos % 2 == 0)
                Run(targetPos, A, B);
            else
                Run(targetPos, B, A);

            // build the presentation for the ready tiles
            lock (toBuildLock)
            {
                while (toBuild.Count > 0)
                {
                    var tile = toBuild.Pop();
                    tile.dataBuilder.Rebuild();
                }
            }
        }

        private void Run(int pos, Tile tile, Tile preTile)
        {
            if (pos == tile.pos)
                return;

            Debug.Log($"rebuild At position {pos}");

            FillInputWave(tile.wave, preTile.wave);

            if (RebuildAsync)
            {
                // solve on a separate thread
                Task.Run(() =>
                {
                    lock (solverLock)
                    {
                        Solve(pos, tile, preTile);
                    }
                });
            }
            else
                Solve(pos, tile, preTile);

            tile.pos = pos;
        }

        private void Solve(int pos, Tile tile, Tile preTile)
        {
            tile.leveData.position = new Vector3Int(xoffset, 0, pos * size.z);
            solver.SetlevelData(tile.leveData);
            solver.SetInputWave(tile.wave);
            solver.SetBoundary(new LevelBoundary(preTile.leveData, null, null), Direction.Backward);

            var itr = solver.Solve(new BoundsInt(Vector3Int.zero, size), maxIterations);
            if (itr > 0)
            {
                lock (toBuildLock)
                {
                    toBuild.Push(tile);
                }
            }
            else
                Debug.LogError("build failed!");
        }

        // here we do our custom input
        void FillInputWave(Array3D<InputWaveCell> wave, Array3D<InputWaveCell> preWave)
        {
            // first we clear the constrains, and exclude the excludedGroups
            var inputWave = InputWaveCell.AllGroups;
            foreach (var groub in excludedGroups)
                inputWave[runtimeRepo.GetGroupIndex(groub)] = false;
            foreach (var index in SpatialUtil.Enumerate(wave.Size))
                wave[index] = inputWave;

            // next we set the front and back boundary to the base group
            // the base group is granted not to fail, that's how it's customized
            SetInputWaveBoundary(wave);
            // next we create a continuous path from front to back, and we make sure
            // to be connected with previous tile
            FillInputWaveWithPath(wave, preWave);
            // finally we create two random room
            FillInputWaveWithRooms(wave);
        }

        void SetInputWaveBoundary(Array3D<InputWaveCell> wave)
        {
            var baseWave = GetWave(BlocksRepo.BASE_GROUP);

            foreach (var index in SpatialUtil.Enumerate(wave.Size.xy()))
                wave[index.xyn()] = baseWave;

            foreach (var index in SpatialUtil.Enumerate(wave.Size.xy()))
                wave[index.xyn() + Vector3Int.forward * (wave.Size.z - 1)] = baseWave;
        }

        private void FillInputWaveWithPath(Array3D<InputWaveCell> wave, Array3D<InputWaveCell> preWave)
        {
            var emptyWaveCell = GetWave(BlocksRepo.EMPTY_GROUP);

            Vector2Int cIndex = default;
            for (int i = 1; i < preWave.Size.x - 1; i++)
            {
                if (preWave[new Vector3Int(i, 1, preWave.Size.z - 1)] == emptyWaveCell)
                {
                    cIndex.x = i;
                    break;
                }
            }
            if (cIndex.x == 0)
                cIndex.x = Random.Range(1, wave.Size.x - 2);

            wave[cIndex.xny() + Vector3Int.up] = emptyWaveCell;
            bool vertical = true;

            while (cIndex.y != wave.Size.z - 1)
            {
                if (vertical)
                {
                    var amount = Random.Range(2, wave.Size.z / 2);
                    while (amount > 0)
                    {
                        if (cIndex.y == wave.Size.z - 1)
                            break;

                        cIndex.y++;
                        wave[cIndex.xny() + Vector3Int.up] = emptyWaveCell;
                        amount--;
                    }
                }
                else
                {
                    var amount = Random.Range(1, wave.Size.x / 2);

                    int dir;
                    if (cIndex.x == 1)
                        dir = 1;
                    else if (cIndex.x == wave.Size.x - 2)
                        dir = -1;
                    else
                        dir = Random.value >= 0.5f ? 1 : -1;

                    while (amount > 0)
                    {
                        cIndex.x += dir;
                        wave[cIndex.xny() + Vector3Int.up] = emptyWaveCell;
                        amount--;

                        if (cIndex.x == 1 || cIndex.x == wave.Size.x - 2)
                            break;
                    }
                }
                vertical = !vertical;
            }
        }

        void FillInputWaveWithRooms(Array3D<InputWaveCell> wave)
        {
            var emptyWaveCell = GetWave(BlocksRepo.EMPTY_GROUP);

            for (int i = 0; i < 2; i++)
            {
                var size = new Vector2Int(
                    Random.Range(2, wave.Size.x / 2),
                    Random.Range(2, wave.Size.z / 2));
                var pos = new Vector2Int(
                    Random.Range(1, wave.Size.x - size.x - 2),
                    Random.Range(2, wave.Size.z - size.y - 2));
                foreach (var index in SpatialUtil.Enumerate(pos, pos + size))
                    wave[index.xny() + Vector3Int.up] = emptyWaveCell;
            }
        }

        // get input wave cell for a single group
        private InputWaveCell GetWave(string name)
        {
            var wave = new InputWaveCell();
            wave[runtimeRepo.GetGroupIndex(name)] = true;
            return wave;
        }

        // don't forget to clean up
        private void OnDisable()
        {
            A.Dispose();
            B.Dispose();
            runtimeRepo.Dispose();
        }
    }

}