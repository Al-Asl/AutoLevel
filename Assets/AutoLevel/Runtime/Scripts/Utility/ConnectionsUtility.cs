using System;
using System.Collections.Generic;
using UnityEngine;
using static Directions;

public struct VariantRef
{
    public int index;
    public BlockAsset blockAsset;
    public Vector3 position => blockAsset.transform.position + variant.position_editor_only;
    public BlockVariant variant => blockAsset.variants[index];
    public BlockConnection connections => variant.connections;

    public VariantRef(BlockAsset blockAsset, int index)
    {
        this.index = index;
        this.blockAsset = blockAsset;
    }

    public override bool Equals(object obj)
    {
        return obj is VariantRef @ref &&
               index == @ref.index && blockAsset == @ref.blockAsset;
    }

    public override int GetHashCode()
    {
        return new XXHash().Append(blockAsset.GetHashCode()).Append(index);
    }

    public static bool operator ==(VariantRef a, VariantRef b)
    {
        return a.index == b.index && a.blockAsset == b.blockAsset;
    }

    public static bool operator !=(VariantRef a, VariantRef b)
    {
        return !(a == b);
    }
}
public class Connection
{
    public VariantRef a, b;
    public int d;

    public Connection(VariantRef a, VariantRef b, int d)
    {
        this.a = a;
        this.b = b;
        this.d = d;
    }

    public override int GetHashCode()
    {
        return new XXHash().Append(a.GetHashCode()).Append(b.GetHashCode()).Append(d);
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

    public bool Contain(VariantRef varRef)
    {
        return a == varRef || b == varRef;
    }
}

public static class ConnectionsUtility
{
    public static List<int[]>[] GetAdjacencyList(List<BlockConnection> connectionsIds)
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
    public static void GetConnectionsList(List<VariantRef> variants,List<Connection> connections)
    {
        for (int i = 0; i < variants.Count; i++)
        {
            var src = variants[i];
            for (int j = i; j < variants.Count; j++)
            {
                var dest = variants[j];
                var dc = src == dest ? 3 : 6;
                for (int d = 0; d < dc; d++)
                {
                    if (src.variant[d] == dest.variant[opposite[d]])
                        connections.Add(new Connection(src, dest, d));
                }
            }
        }
    }

    public static int GetNextId(HashSet<int> hashes)
    {
        var list = new List<int>(hashes);
        list.Sort();
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

    public static HashSet<int> GetListOfIds(List<BlockAsset> blockAssets, int d)
    {
        int od = opposite[d];
        var res = new HashSet<int>();
        BlockAsset.IterateVariants(blockAssets, (varRef) =>
        {
            var dId = varRef.connections[d];
            var odId = varRef.connections[od];
            res.Add(dId);
            res.Add(odId);
        });
        res.Remove(0);
        return res;
    }

    public static HashSet<int> GetListOfIds(List<BlockAsset> blockAssets)
    {
        var res = new HashSet<int>();
        BlockAsset.IterateVariants(blockAssets, (varRef) =>
        {
            for (int i = 0; i < 6; i++)
                res.Add(varRef.connections[i]);
        });
        res.Remove(0);
        return res;
    }
}