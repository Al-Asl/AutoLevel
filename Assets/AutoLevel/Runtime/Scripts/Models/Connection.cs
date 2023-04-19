using AlaslTools;

namespace AutoLevel
{
    using static Directions;

    public class Connection
    {
        public IBlock a, b;
        public int d;

        public Connection(IBlock a, IBlock b, int d)
        {
            this.a = a;
            this.b = b;
            this.d = d;
        }

        public override int GetHashCode()
        {
            var ha = a.GetHashCode(); var hb = b.GetHashCode();
            if (ha > hb)
                return new XXHash().Append(a.GetHashCode()).Append(b.GetHashCode()).Append(d);
            else
                return new XXHash().Append(b.GetHashCode()).Append(a.GetHashCode()).Append(opposite[d]);
        }

        public override bool Equals(object obj)
        {
            if (obj is Connection)
            {
                return (Connection)obj == this;
            }
            else
                return false;
        }

        public static bool operator ==(Connection a, Connection b)
        {
            return (a.a == b.a && a.b == b.b && a.d == b.d) ||
                (a.a == b.b && a.b == b.a && a.d == opposite[b.d]);
        }

        public static bool operator !=(Connection a, Connection b)
        {
            return !(a == b);
        }

        public bool Contain(IBlock block)
        {
            return a == block || b == block;
        }
    }

}