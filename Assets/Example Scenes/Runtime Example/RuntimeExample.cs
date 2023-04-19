using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeExample : MonoBehaviour
{
    [SerializeField]
    public BlocksRepo repo;
    [SerializeField]
    public BoundsInt bounds;

    private LevelData levelData;
    private LevelMeshBuilder meshBuilder;
    private LevelSolver solver;

    private void OnEnable()
    {
        //generate blocks connections, variants and other configuration
        repo.Generate();

        //a container for the solver result
        levelData = new LevelData(bounds);
        meshBuilder = new LevelMeshBuilder(levelData,repo);

        solver = new LevelSolver(bounds.size);
        solver.repo = repo;
        solver.levelData = levelData;
    }

    private void OnDisable()
    {
        repo.Clear();
        meshBuilder.Dispose();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            Rebuild();
    }

    void Rebuild()
    {
        //solve the level, this will return the number of iteration it took,
        //0 means the solver has failed
        var iterations = solver.Solve(bounds);
        if (iterations > 0)
        {
            //rebuild the mesh if the solver success
            meshBuilder.Rebuild(bounds);
        }
    }
}
