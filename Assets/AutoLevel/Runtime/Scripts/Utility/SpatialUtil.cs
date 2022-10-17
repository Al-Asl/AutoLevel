using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
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