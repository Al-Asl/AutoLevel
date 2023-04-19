using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace AlaslTools
{

    public static class SpatialUtil
    {
        struct Enumerator2D : IEnumerator<Vector2Int>, IEnumerable<Vector2Int>
        {
            private Vector2Int start, size, index;

            public Enumerator2D(Vector2Int start, Vector2Int end)
            {
                this.start = start;
                size = end - start;
                index = new Vector2Int(-1, 0);
            }

            public Vector2Int Current => start + index;
            object IEnumerator.Current => Current;

            public void Dispose() { }

            public IEnumerator<Vector2Int> GetEnumerator()
            {
                return this;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public bool MoveNext()
            {
                if (++index.x == size.x)
                {
                    index.x = 0;
                    if (++index.y == size.y)
                        return false;
                }
                return true;
            }

            public void Reset()
            {
                index = new Vector2Int(-1, 0);
            }
        }

        struct Enumerator3D : IEnumerator<Vector3Int>, IEnumerable<Vector3Int>
        {
            private Vector3Int start, size, index;

            public Enumerator3D(Vector3Int start, Vector3Int end)
            {
                this.start = start;
                size = end - start;
                index = new Vector3Int(-1, 0, 0);
            }

            public Vector3Int Current => start + index;
            object IEnumerator.Current => Current;

            public void Dispose() { }

            public IEnumerator<Vector3Int> GetEnumerator()
            {
                return this;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public bool MoveNext()
            {
                if (++index.x == size.x)
                {
                    index.x = 0;
                    if (++index.y == size.y)
                    {
                        index.y = 0;
                        if (++index.z == size.z)
                            return false;
                    }
                }
                return true;
            }

            public void Reset()
            {
                index = new Vector3Int(-1, 0, 0);
            }
        }

        public static Vector2Int[] Parting(Vector2Int size) => Parting(size.x * size.y, Environment.ProcessorCount);

        public static Vector2Int[] Parting(Vector3Int size) => Parting(size.x * size.y * size.z, Environment.ProcessorCount);

        public static Vector2Int[] Parting(int size, int partions)
        {
            var result = new Vector2Int[partions];

            int l = size / partions;
            int r = size - l * partions;

            int last = 0;
            for (int p = 0; p < partions; p++)
            {
                var start = last;
                var end = start + l + (r-- > 0 ? 1 : 0);
                result[p] = new Vector2Int(start, end);
                last = end;
            }

            return result;
        }

        public static void ParallelEnumrate(Vector3Int size,Action<Vector3Int> excute)
        {
            var parts = Parting(size);

            var sxy = size.x * size.y;
            var sx = size.x;

            Parallel.For(0, parts.Length, (id) =>
            {
                var range = parts[id];

                for (int i = range.x; i < range.y; i++)
                    excute(Index1DTo3D(i,sx,sxy));
            });
        }

        public static ParallelLoopResult ParallelEnumrate<T>(Vector3Int size,Func<T> InitLocal,Action<Vector3Int, ParallelLoopState,T> excute)
        {
            var parts = Parting(size);

            var sxy = size.x * size.y;
            var sx = size.x;

            return Parallel.For(0, parts.Length, (id, state) =>
            {
                var range = parts[id];
                var l = InitLocal();

                for (int i = range.x; i < range.y; i++)
                    excute(Index1DTo3D(i, sx, sxy), state, l);
            });
        }

        public static int Index3DTo1D(Vector3Int i3, Vector3Int size)
        {
            return Index3DTo1D(i3, size.x, size.x * size.y);
        }

        public static int Index3DTo1D(Vector3Int i3, int sizex, int sizexy)
        {
            return i3.x + i3.y * sizex + i3.z * sizexy;
        }

        public static Vector3Int Index1DTo3D(int i, Vector3Int size)
        {
            return Index1DTo3D(i, size.x, size.x * size.y);
        }

        public static Vector3Int Index1DTo3D(int i, int sizex, int sizexy)
        {
            Vector3Int index = default;
            index.z = i / sizexy;
            int t = i - index.z * sizexy;
            index.y = t / sizex;
            index.x = t % sizex;
            return index;
        }

        public static int Index2DTo1D(Vector2Int i2, int sizex)
            => i2.x + i2.y * sizex;

        public static Vector2Int Index1DTo2D(int i, int sizex)
            => new Vector2Int(i % sizex, i / sizex);

        public static IEnumerable<(Vector3Int, Vector3Int, int)> EnumerateConnections(Vector3Int size)
        {
            for (int x = 0; x < size.x - 1; x++)
                foreach (var index in Enumerate(size.yz()))
                {
                    var i = index.nxy() + Vector3Int.right * x;
                    yield return (i, i + Vector3Int.right, 3);
                }
            for (int y = 0; y < size.y - 1; y++)
                foreach (var index in Enumerate(size.xz()))
                {
                    var i = index.xny() + Vector3Int.up * y;
                    yield return (i, i + Vector3Int.up, 4);
                }
            for (int z = 0; z < size.z - 1; z++)
                foreach (var index in Enumerate(size.xy()))
                {
                    var i = index.xyn() + Vector3Int.forward * z;
                    yield return (i, i + Vector3Int.forward, 5);
                }
        }

        public static IEnumerable<Vector3Int> Enumerate(Vector3Int start, Vector3Int end)
        {
            return new Enumerator3D(start, end);
        }

        public static IEnumerable<Vector3Int> Enumerate(Vector3Int size)
        {
            return new Enumerator3D(Vector3Int.zero, size);
        }

        public static IEnumerable<Vector3Int> Enumerate(BoundsInt bounds)
        {
            return new Enumerator3D(bounds.min, bounds.max);
        }

        public static IEnumerable<Vector3Int> Enumerate<T>(T[,,] array)
        {
            return new Enumerator3D(Vector3Int.zero, new Vector3Int(array.GetLength(2), array.GetLength(1), array.GetLength(0)));
        }

        public static IEnumerable<Vector2Int> Enumerate(Vector2Int start, Vector2Int end)
        {
            return new Enumerator2D(start, end);
        }

        public static IEnumerable<Vector2Int> Enumerate(Vector2Int size)
        {
            return new Enumerator2D(Vector2Int.zero, size);
        }

        public static void ItterateIntersection(BoundsInt oldBound, BoundsInt newBounds, System.Action<Vector3Int, Vector3Int> excute)
        {
            var istart = Vector3Int.Max(oldBound.min, newBounds.min);
            var iend = Vector3Int.Min(oldBound.max, newBounds.max);
            var isize = iend - istart;
            var diff = newBounds.min - oldBound.min;
            var src_offset = Vector3Int.Max(Vector3Int.zero, diff);
            var dist_offset = Vector3Int.Max(Vector3Int.zero, -diff);
            foreach (var index in Enumerate(isize))
                excute(index + dist_offset, index + src_offset);
        }
    }

}