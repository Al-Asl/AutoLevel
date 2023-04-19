using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    /// <summary>
    /// the data structure used by the level solver to write it's result
    /// </summary>
    [System.Serializable]
    public class LevelData
    {
        public BoundsInt bounds
        {
            get => new BoundsInt(position, blocks.Size);
            set
            {
                if (blocks.Size != value.size)
                    blocks.Resize(value.size);

                position = value.min;
            }
        }

        public Array3D<int> Blocks => blocks;
        [SerializeField]
        public Vector3Int position;

        [SerializeField]
        private Array3D<int> blocks;

        public LevelData(BoundsInt bounds)
        {
            position = bounds.min;
            blocks = new Array3D<int>(bounds.size);
            foreach (var index in SpatialUtil.Enumerate(bounds.size))
                blocks[index.z, index.y, index.x] = 0;
        }
    }

}