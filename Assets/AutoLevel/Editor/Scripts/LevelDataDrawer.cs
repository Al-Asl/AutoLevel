using UnityEngine;

namespace AutoLevel
{
    public class LevelDataDrawer : LevelObjectBuilder
    {
        public LevelDataDrawer(LevelData levelData, BlocksRepo.Runtime blockRepo) : base(levelData, blockRepo)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            RebuildAll();
        }
    }
}