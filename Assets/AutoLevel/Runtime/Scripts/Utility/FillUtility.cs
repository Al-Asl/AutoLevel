using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    public static class FillUtility
    {
        public static readonly Vector3[] nodes = new Vector3[]
        {
            new Vector3(0,0,0),
            new Vector3(1,0,0),
            new Vector3(0,0,1),
            new Vector3(1,0,1),
            new Vector3(0,1,0),
            new Vector3(1,1,0),
            new Vector3(0,1,1),
            new Vector3(1,1,1)
        };

        public static int GetSide(int code, int side)
        {
            if (side % 3 == 0)
                code = MirrorFill(RotateFill(RotateFill(code, Axis.y), Axis.x), Axis.x);
            else if (side % 3 == 2)
                code = RotateFill(MirrorFill(code, Axis.z), Axis.x);

            if (side >= 3)
                code = MirrorFill(code, Axis.y);

            return code & 0xF;
        }

        public static int MirrorFill(int fill, Axis axis)
        {
            switch (axis)
            {
                case Axis.x:
                    return (fill & 0x55) << 1 | (fill & 0xaa) >> 1;
                case Axis.y:
                    return (fill & 0xf) << 4 | (fill & 0xf0) >> 4;
                case Axis.z:
                    return (fill & 0x33) << 2 | (fill & 0xcc) >> 2;
            }

            return fill;
        }

        public static int RotateFill(int fill, Axis axis)
        {
            switch (axis)
            {
                case Axis.x:
                    return (0x3 & fill) << 4 | (0xc & fill) >> 2 |
                         (0x30 & fill) << 2 | (0xc0 & fill) >> 4;
                case Axis.y:
                    return (0x11 & fill) << 2 | (0x22 & fill) >> 1 |
                        (0x44 & fill) << 1 | (0x88 & fill) >> 2;
                case Axis.z:
                    return (0x5 & fill) << 1 | (0xa & fill) << 4 |
                         (0x50 & fill) >> 4 | (0xa0 & fill) >> 1;
            }
            return fill;
        }

        public static int FlipFill(int fill)
        {
            return ~fill & 255;
        }
    }

}