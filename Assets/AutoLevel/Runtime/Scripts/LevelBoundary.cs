using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    public enum LevelBoundaryOption
    {
        Nothing,
        Block,
        Group
    }

    public struct LevelBoundaryResult
    {
        public LevelBoundaryOption option;
        public int block;
        public InputWaveCell waveBlock;

        public LevelBoundaryResult(InputWaveCell iwave)
        {
            option = LevelBoundaryOption.Group;
            this.waveBlock = iwave;
            block = 0;
        }

        public LevelBoundaryResult(int block) : this()
        {
            option = LevelBoundaryOption.Block;
            waveBlock = default;
            this.block = block;
        }
    }

    public interface ILevelBoundary
    {
        public void SetLayer(int index);
        LevelBoundaryResult Evaluate(Vector3Int index);
    }

    public class GroupsBoundary : ILevelBoundary
    {
        private InputWaveCell iWave;
        private int layer;

        public GroupsBoundary(InputWaveCell iWave)
        {
            this.iWave = iWave;
        }

        public GroupsBoundary(int group)
        {
            var iWave = new InputWaveCell();
            iWave[group] = true;
            this.iWave = iWave;
        }

        public LevelBoundaryResult Evaluate(Vector3Int index)
        {
            return layer > 0 ? new LevelBoundaryResult() : new LevelBoundaryResult(iWave);
        }

        public void SetLayer(int index) { this.layer = index; }
    }

    public class BlockBoundary : ILevelBoundary
    {
        private int block;

        public BlockBoundary(int block)
        {
            this.block = block;
        }

        public LevelBoundaryResult Evaluate(Vector3Int index)
        {
            return new LevelBoundaryResult(block);
        }

        public void SetLayer(int index) { }
    }

    public class LevelBoundary : ILevelBoundary
    {
        private BoundsInt bounds;
        private LevelData level;
        private Array3D<InputWaveCell> wave;
        private ILevelBoundary fallBack;
        private LevelLayer layer;
        private int layerIndex;

        public LevelBoundary(LevelData level, Array3D<InputWaveCell> wave, ILevelBoundary fallBack)
        {
            this.level = level;
            this.wave = wave;
            bounds = level.bounds;
            this.fallBack = fallBack;
        }

        public void SetLayer(int index) {
            layerIndex = index;
            layer = level.GetLayer(index);
            fallBack?.SetLayer(index);
        }

        public LevelBoundaryResult Evaluate(Vector3Int index)
        {
            var localIndex = index - level.position;
            bool contain = bounds.Contains(index);

            if(contain)
            {
                var blockIndex = layer.Blocks[localIndex];
                if (blockIndex != 0)
                    return new LevelBoundaryResult(blockIndex);
                else if (layerIndex == 0)
                    if(wave != null)
                        return new LevelBoundaryResult(wave[localIndex]);
                else
                {
                    // fall-back to block from bottom layer!
                }
            }

            if (fallBack != null)
                return fallBack.Evaluate(index);
            else
                return new LevelBoundaryResult();
        }
    }

}