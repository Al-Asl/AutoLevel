using System;
using UnityEngine;

namespace AutoLevel
{

    [Serializable]
    public class Array3D<T>
    {
        public Vector3Int Size => size;

        [SerializeField]
        private Vector3Int size;
        [SerializeField]
        private T[] array;

        private int xy => size.x * size.y;

        public Array3D() : this(Vector3Int.zero) { }

        public Array3D(Vector3Int size)
        {
            this.size = size;
            array = new T[xy * size.z];
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
            get => array[i + j * size.x + k * xy];
            set => array[i + j * size.x + k * xy] = value;
        }

        public T this[Vector3Int i]
        {
            get => array[i.x + i.y * size.x + i.z * xy];
            set => array[i.x + i.y * size.x + i.z * xy] = value;
        }
    }

}