using AlaslTools;

namespace AutoLevel
{

    [System.Serializable]
    public struct ConnectionsIds
    {
        public int left;
        public int down;
        public int backward;
        public int right;
        public int up;
        public int forward;

        public override int GetHashCode()
        {
            return new XXHash().Append(left).Append(down).Append(backward).Append(right).Append(up).Append(forward);
        }

        public int this[int dir]
        {
            get
            {
                switch (dir)
                {
                    case 0:
                        return left;
                    case 1:
                        return down;
                    case 2:
                        return backward;
                    case 3:
                        return right;
                    case 4:
                        return up;
                    case 5:
                        return forward;
                }
                throw new System.Exception();
            }
            set
            {
                switch (dir)
                {
                    case 0:
                        left = value;
                        return;
                    case 1:
                        down = value;
                        return;
                    case 2:
                        backward = value;
                        return;
                    case 3:
                        right = value;
                        return;
                    case 4:
                        up = value;
                        return;
                    case 5:
                        forward = value;
                        return;
                }
                throw new System.Exception();
            }
        }
    }

}