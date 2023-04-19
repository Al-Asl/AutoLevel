using System.Collections.Generic;
using UnityEngine;
using AlaslTools;
using System.Collections;

namespace AutoLevel
{
    [System.Serializable]
    public class LevelLayer
    {
        public Array3D<int> Blocks => blocks;
        public bool Valid { get => valid; set => valid = value; }

        [SerializeField]
        private Array3D<int> blocks;
        [SerializeField]
        private bool valid;

        public LevelLayer(Vector3Int size)
        {
            blocks = new Array3D<int>(size);
            Clear();
        }

        public void RunValidation()
        {
            valid = true;
            foreach (var index in SpatialUtil.Enumerate(blocks.Size))
                if(blocks[index.z, index.y, index.x] == 0)
                {
                    valid = false;
                    break;
                }
        }

        public void Clear()
        {
            foreach (var index in SpatialUtil.Enumerate(blocks.Size))
                blocks[index.z, index.y, index.x] = 0;
            valid = false;
        }
    }

    /// <summary>
    /// the data structure used by the level solver to write it's result
    /// </summary>
    [System.Serializable]
    public class LevelData : IEnumerable<LevelLayer>
    {
        public BoundsInt bounds
        {
            get => new BoundsInt(position, size);
            set
            {
                if (size != value.size)
                    Resize(value.size);

                position = value.min;
            }
        }

        public Vector3Int size => layers[0].Blocks.Size;

        [SerializeField]
        public Vector3Int position;

        [SerializeField]
        private List<LevelLayer> layers = new List<LevelLayer>();

        public int LayersCount => layers.Count;

        public LevelLayer GetLayer(int index) => layers[index];

        public void PushLayer() => layers.Add(new LevelLayer(size));

        public LevelLayer PopLayer()
        {
            var layer = layers[layers.Count - 1];
            layers.RemoveAt(layers.Count - 1);
            return layer;
        }

        public void ClearAllLayers()
        {
            foreach (var layer in layers)
                layer.Clear();
        }

        public void Resize(Vector3Int size)
        {
            foreach (var layer in layers)
                layer.Blocks.Resize(size);
        }

        public void SetLayerCount(int count)
        {
            if (LayersCount != count)
            {
                var delta = count - LayersCount;
                for (int i = 0; i < Mathf.Abs(delta); i++)
                    if (delta > 0)
                        PushLayer();
                    else
                        PopLayer();
            }
        }

        public IEnumerator<LevelLayer> GetEnumerator() => layers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        public LevelData(BoundsInt bounds)
        {
            position = bounds.min;
            layers.Add(new LevelLayer(bounds.size));
        }
    }

}