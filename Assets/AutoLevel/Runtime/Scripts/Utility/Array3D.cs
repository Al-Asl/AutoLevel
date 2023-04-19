using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{
    /// <summary>
    /// used as a list wrapper for serializing nested arrays
    /// </summary>
    [Serializable]
    public class SList<T> : IEnumerable<T>
    {
        public bool IsEmpty => list.Count == 0;
        public int Count => list.Count;

        [SerializeField]
        private List<T> list;

        public SList()
        {
            list = new List<T>();
        }

        public void Add(T item) => list.Add(item);
        public void RemoveAt(int index) => list.RemoveAt(index);
        public void Clear() => list.Clear();
        public void Fill(int count, Func<T> cons) => list.Fill(count, cons);

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)list).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public T this[int i]
        {
            get => list[i];
            set => list[i] = value;
        }
    }

    [Serializable]
    public class Array3D<T>
    {
        public Vector3Int Size => size;

        [SerializeField]
        private Vector3Int size;
        [SerializeField]
        private T[] array;

        private int sizexy => size.x * size.y;

        public Array3D() : this(Vector3Int.zero) { }

        public Array3D(Vector3Int size)
        {
            this.size = size;
            array = new T[sizexy * size.z];
        }

        public void Resize(Vector3Int newSize)
        {
            if (size.x < 0 || size.y < 0 || size.z < 0)
                throw new Exception($"the size {newSize} is not valid!");

            var newArray = new T[newSize.x * newSize.y * newSize.z];

            SpatialUtil.ItterateIntersection(
                new BoundsInt() { size = size },
                new BoundsInt() { size = newSize }, (dst, src) =>
                {
                    newArray[dst.x + dst.y * newSize.x + dst.z * newSize.x * newSize.y] = this[src];
                }
            );

            size = newSize; array = newArray;
        }

        public T this[int k, int j, int i]
        {
            get => array[i + j * size.x + k * sizexy];
            set => array[i + j * size.x + k * sizexy] = value;
        }

        public T this[Vector3Int i]
        {
            get => array[SpatialUtil.Index3DTo1D(i, size.x, sizexy)];
            set => array[SpatialUtil.Index3DTo1D(i, size.x, sizexy)] = value;
        }
    }

}