using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelData
{
    public BoundsInt bounds
    {
        get => new BoundsInt(start, end - start);
        set
        {
            if (end - start != value.size)
            {
                var old = blocks;
                blocks = new Array3D<int>(value.size);

                SpatialUtil.ItterateIntersection(bounds, value, (idist, isrc) =>
                {
                    blocks[idist.z, idist.y, idist.x] = old[isrc.z, isrc.y, isrc.x];
                });
            }
            start = value.min;
            end = value.max;
        }
    }
    public Array3D<int> Blocks => blocks;

    [SerializeField]
    private Vector3Int start, end;
    [SerializeField]
    private Array3D<int> blocks;

    public LevelData(BoundsInt bounds)
    {
        start = bounds.min;
        end = bounds.max;
        blocks = new Array3D<int>(bounds.size);
        foreach (var index in SpatialUtil.Enumerate(bounds.size))
            blocks[index.z, index.y, index.x] = -1;
    }
}