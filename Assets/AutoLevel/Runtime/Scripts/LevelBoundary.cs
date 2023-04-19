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
            this.block = block;
            waveBlock = default;
        }
    }

    public interface ILevelBoundary
    {
        LevelBoundaryResult Evaluate(Vector3Int index);
    }

    public class GroupsBoundary : ILevelBoundary
    {
        private InputWaveCell iWave;

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
            return new LevelBoundaryResult(iWave);
        }
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
    }

    public class LevelBoundary : ILevelBoundary
    {
        private BoundsInt bounds;
        private LevelData level;
        private Array3D<InputWaveCell> wave;
        private ILevelBoundary fallBack;

        public LevelBoundary(LevelData level, Array3D<InputWaveCell> wave, ILevelBoundary fallBack)
        {
            this.level = level;
            this.wave = wave;
            bounds = level.bounds;
            this.fallBack = fallBack;
        }

        public LevelBoundaryResult Evaluate(Vector3Int index)
        {
            var localIndex = index - level.position;
            bool contain = bounds.Contains(index);

            if(contain && level.Blocks[localIndex] != 0)
                return new LevelBoundaryResult(level.Blocks[localIndex]);
            else if(contain && wave != null)
                return new LevelBoundaryResult(wave[localIndex]);
            else if(fallBack != null)
                return fallBack.Evaluate(index);
            else
                return new LevelBoundaryResult(InputWaveCell.AllGroups);
        }
    }

}