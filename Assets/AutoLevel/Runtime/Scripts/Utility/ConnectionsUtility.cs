using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public static class ConnectionsUtility
    {
        public static List<int[]>[] GetAdjacencyList(List<SideIds> connectionsIds)
        {
            var alist = new List<int[]>[6];
            for (int d = 0; d < 6; d++)
            {
                var list = new List<int[]>(connectionsIds.Count);
                list.Fill(connectionsIds.Count, () => null);
                alist[d] = list;
            }

            for (int i = 0; i < connectionsIds.Count; i++)
            {
                var block = connectionsIds[i];
                for (int d = 0; d < 6; d++)
                {
                    List<int> conn = new List<int>();
                    for (int k = 0; k < connectionsIds.Count; k++)
                    {
                        if (block[d] == connectionsIds[k][opposite[d]])
                            conn.Add(k);
                    }
                    alist[d][i] = conn.ToArray();
                }
            }

            return alist;
        }
        public static void GetConnectionsList<T>(IEnumerable<T> blocks, List<Connection> connections) where T : IBlock
        {
            var i = 0;
            foreach (var src in blocks)
            {
                foreach (var dst in blocks.Skip(i++))
                {
                    var dc = src.GetHashCode() == dst.GetHashCode() ? 3 : 6;
                    for (int d = 0; d < dc; d++)
                    {
                        if (src[d] == dst[opposite[d]])
                            connections.Add(new Connection(src, dst, d));
                    }
                }
            }
        }

        public static int GetNextId(List<int> list)
        {
            int next = list.Count + 1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != (i + 1))
                {
                    next = i + 1;
                    break;
                }
            }
            return next;
        }

        public static int GetAndUpdateNextId(LinkedList<int> ids)
        {
            var current = ids.First;
            var index = 1;
            while (current != null)
            {
                if (current.Value != index)
                {
                    ids.AddBefore(current, index);
                    return index;
                }
                index++;
                current = current.Next;
            }
            ids.AddLast(index);
            return index;
        }

        public static HashSet<int> GetInternalConnections<T>(Array3D<T> blocks) where T : IBlock
        {
            HashSet<Connection> connections = new HashSet<Connection>();
            BoundsInt bounds = new BoundsInt(Vector3Int.zero, blocks.Size);

            foreach (var index in SpatialUtil.Enumerate(blocks.Size))
            {
                var c = blocks[index.z, index.y, index.x];
                if (c.blockAsset == null)
                    continue;

                for (int d = 0; d < 6; d++)
                {
                    var i = index + delta[d];
                    if (bounds.Contains(i))
                    {
                        var n = blocks[i.z, i.y, i.x];
                        if (n.blockAsset != null)
                            connections.Add(new Connection(c, n, d));
                    }
                }
            }

            HashSet<int> result = new HashSet<int>();

            foreach (var con in connections)
                result.Add(con.a.baseIds[con.d]);

            return result;
        }

        public static List<int> GetListOfSortedIds<T>(IEnumerable<T> blocks) where T : IBlock
        {
            var list = new List<int>(GetListOfIds(blocks));
            list.Sort();
            return list;
        }

        private static HashSet<int> GetListOfIds<T>(IEnumerable<T> blocks) where T : IBlock
        {
            var res = new HashSet<int>();
            foreach (var block in blocks)
            {
                for (int i = 0; i < 6; i++)
                    res.Add(block.baseIds[i]);
            }
            res.Remove(0);
            return res;
        }

        private static HashSet<int> GetListOfIds(IEnumerable<IBlock> blocks, int d)
        {
            int od = opposite[d];
            var res = new HashSet<int>();
            foreach (var block in blocks)
            {
                var dId = block.baseIds[d];
                var odId = block.baseIds[od];
                res.Add(dId);
                res.Add(odId);
            }
            res.Remove(0);
            return res;
        }
    }

}