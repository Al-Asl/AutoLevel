using System;
using System.Collections.Generic;

namespace AutoLevel
{
    public enum BlocksResolve
    {
        Matching,
        AllToAll
    }

    public enum BlockPlacement
    {
        Add,
        Replace,
        ReplaceFirst
    }

    [Serializable]
    public class LayerSettings
    {
        public bool PartOfBaseLayer => layer == 0;
        public bool HasDependencies => dependencies.Count != 0;

        public LayerSettings(int layer)
        {
            this.layer      = layer;
            dependencies    = new List<AssetBlock>();
            resolve         = BlocksResolve.Matching;
            placement       = BlockPlacement.Add;
        }

        public LayerSettings(LayerSettings settings)
        {
            layer           = settings.layer;
            dependencies    = new List<AssetBlock>(settings.dependencies);
            resolve         = settings.resolve;
            placement       = settings.placement;
        }

        public int                  layer;
        public List<AssetBlock>     dependencies;
        public BlocksResolve        resolve;
        public BlockPlacement       placement;
    }
}
