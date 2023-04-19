# Auto-Level

<img src="documentation/images/autolevel.gif" width="900px"/>

Free procural level generator based on WFC algorithm for unity.

The target of this package is to create a procural level generator that is controllable, easy to use, and fast to some extent by leveraging the power of the WFC. WFC is a powerful algorithm in terms of procural generation. However, it does not give the user control over the generation process.

## Features
* Runtime WFC solver
* Editor tools, build level inside the editor
* Set the weight for individual Blocks or Block Group
* Control the generation process, and constrain volume to groups of Blocks
* Constrain boundary to a single group or another builder boundary
* Ability to rebuild a given section of the level
* Block variants and Block group variants
* Big block support
* Mesh builder that supports multiple materials input, with the ability to subdivide the level into chunks
* fbx export
* Child objects export

Complete C# source code is provided.

## Usage

* Create the `Blocks Repo` by clicking 'GameObject/AutoLevel/Blocks Repo'
* Place Blocks under the `Blocks Repo` in the hierarchy. Make sure that the mesh is read/write enabled in the import settings, and the Blocks are in the range (0,0,0) (1,1,1), and their pivot is in the bottom back left corner.
* Add the `Block Asset` component to the Blocks
* Select a Block and start making connections in the scene view. Remember to change the editing mode to connection. After the connections are made, the `Blocks Repo` is ready to use

<img src="documentation/images/blocksRepo.png" width="900px"/>

* Create a `Level Builder` by clicking 'GameObject/AutoLevel/Builder'
* Assign the `Blocks Repo` to the builder
* Use the selection handle to set block groups over the level. The level inspector provides a toggle to switch between controlling the level or selection bounds. Bounds can be controlled via inspector or by using scene handles. Toggle between the different handles using w,r,t
* Hit the rebuild button
* Hit `Export Mesh` to export the result to fbx file

Keep in mind that the Building performance depends on two factors the size of the builder and the number of blocks produced by the repo. You can get better performance by only building where needed using multiple builders. Also, using constraints can reduce the building time significantly.

<img src="documentation/images/levelBuilder.png" width="900px"/>

### Runtime Example

```csharp
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
```

**FILLING**

When selecting a `Block Asset,` there is an option called filling in the scene view context menu dropdown. This will show handles to edit the block filling, red for empty and green for fill, similar to the marching cubes algorithm.

The filling has two roles. First, the connections will only be made to blocks with similar side patterns. Second, `Level Builder` can use that information to define the level rooms. The builder contains two built-in groups, the 'Empty' and 'Solid' groups, and you can use them to define the rooms and walls.

## PERFORMACE

* Regarding memory, reducing memory usage can be done by switching to a dictionary instead of arrays. This also helped with performance.
* There is a considerable cost when using a block group. This cost has been reduced significantly by creating a lookup table for group interaction.

## WHAT NEXT
* Fine Block connection control
* Multithredead solving