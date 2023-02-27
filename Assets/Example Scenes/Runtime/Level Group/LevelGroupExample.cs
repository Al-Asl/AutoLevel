using UnityEngine;
using System.Collections.Generic;
using AlaslTools;

namespace AutoLevel.Examples
{
    // building a group of level builder at runtime
    public class LevelGroupExample : MonoBehaviour
    {
        private LevelGroupManager groupManager;
        private List<BaseLevelDataBuilder> builders;

        private void OnEnable()
        {
            // level group manager will find all the level builder in the scene and group the
            // connected builders together
            groupManager = new LevelGroupManager();

            // creating a LevelDataBuilder for each builder in the target group
            builders = new List<BaseLevelDataBuilder>();
            foreach (var builder in groupManager.GetBuilderGroup(0))
                builders.Add(new LevelObjectBuilder(builder.LevelData, groupManager.GetRepo(builder)));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                // solve all the level builder for the group with index 0, once the solver succeed
                // we run the builders
                if (groupManager.Solve(0))
                {
                    foreach (var builder in builders)
                        builder.Rebuild();
                }
                else
                    Debug.Log("build failed");
            }
        }

        // don't forget to clean up
        private void OnDisable()
        {
            foreach (var builder in builders)
                builder.Dispose();
            groupManager.Dispose();
        }
    }

}