using System.Collections.Generic;
using System.Linq;
using AlaslTools;

namespace AutoLevel
{
    using static Directions;
    public static class ConnectionsUtility
    {
        public class IDGenerator
        {
            private LinkedList<int> ids;
            private LinkedListNode<int> current;
            private int index;

            public IDGenerator(IEnumerable<int> sortedIds)
            {
                this.ids = new LinkedList<int>(sortedIds);

                current = ids.First;
                index = 1;
            }

            public int GetNext()
            {
                while (current != null)
                {
                    if(current.Value != index)
                    {
                        ids.AddBefore(current,index++);
                        return index - 1;
                    }
                    index++;
                    current = current.Next;
                }
                return index++;
            }
        }
        public static List<int[]>[] GetAdjacencyList(List<ConnectionsIds> connectionsIds)
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
        public static IDGenerator CreateIDGenerator<T>(IEnumerable<T> blocks) where T : IBlock
        {
            var set = new HashSet<int>();
            foreach (var block in blocks)
            {
                for (int i = 0; i < 6; i++)
                    set.Add(block.baseIds[i]);
            }
            set.Remove(0);
            
            return new IDGenerator(set.OrderBy((x) => x));
        }
        private static IDGenerator CreateIDGenerator(IEnumerable<IBlock> blocks, int d)
        {
            int od = opposite[d];
            var set = new HashSet<int>();
            foreach (var block in blocks)
            {
                var dId = block.baseIds[d];
                var odId = block.baseIds[od];
                set.Add(dId);
                set.Add(odId);
            }
            set.Remove(0);
            return new IDGenerator(set.OrderBy((x) => x));
        }
    }
}