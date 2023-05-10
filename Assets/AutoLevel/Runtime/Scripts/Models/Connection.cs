using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    [System.Serializable]
    public class Connection
    {
        public bool Valid   => a.block.Valid && b.block.Valid;
        public int d        => a.d;

        public BlockSide a, b;

        public Connection(AssetBlock a, AssetBlock b, int d)
        {
            this.a = new BlockSide(a, d);
            this.b = new BlockSide(b, opposite[d]);
        }

        public Connection(BlockSide a, BlockSide b)
        {
            this.a = a;
            this.b = b;
        }

        public override int GetHashCode()
        {
            var ha = a.GetHashCode(); var hb = b.GetHashCode();
            if (ha > hb)
                return new XXHash().Append(a.GetHashCode()).Append(b.GetHashCode()).Append(a.d);
            else
                return new XXHash().Append(b.GetHashCode()).Append(a.GetHashCode()).Append(b.d);
        }

        public override bool Equals(object obj)
        {
            if (obj is Connection)
                return (Connection)obj == this;
            else
                return false;
        }

        public static bool operator ==(Connection a, Connection b)
        {
            return (a.a == b.a && a.b == b.b) || (a.b == b.a && a.a == b.b);
        }

        public static bool operator !=(Connection a, Connection b)
        {
            return !(a == b);
        }

        public bool Contain(AssetBlock block)
        {
            return a.block == block || b.block == block;
        }

        public bool Contain(BlockAsset block)
        {
            return a.block.blockAsset == block || b.block.blockAsset == block;
        }
    }

}