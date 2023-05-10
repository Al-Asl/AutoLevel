using AlaslTools;

namespace AutoLevel
{
    [System.Serializable]
    public struct BlockSide
    {
        public int id => block.baseIds[d];
        public int compsiteId => block[d];

        public AssetBlock block;
        public int d;

        public BlockSide(AssetBlock block, int d)
        {
            this.block = block;
            this.d = d;
        }

        public static bool operator ==(BlockSide a, BlockSide b)
        {
            return a.block == b.block && a.d == b.d;
        }

        public static bool operator !=(BlockSide a, BlockSide b)
        {
            return a.block != b.block || a.d != b.d;
        }

        public override bool Equals(object obj)
        {
            if (obj is BlockSide)
                return ((BlockSide)obj) == this;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return new XXHash().Append(block.GetHashCode()).Append(d);
        }
    }
}
