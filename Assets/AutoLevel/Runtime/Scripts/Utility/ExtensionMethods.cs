using UnityEngine;
using System;
using System.Collections.Generic;

namespace AutoLevel
{
    public enum Axis
    {
        x, y, z
    }

    public static class ExtensionMethods
    {
        public static Vector4 ToVector4(this Vector3 vec, float w)
            => new Vector4(vec.x, vec.y, vec.z, w);
        public static Vector3 xyn(this Vector2 v) => new Vector3(v.x, v.y, 0);
        public static Vector3Int xyn(this Vector2Int v) => new Vector3Int(v.x, v.y, 0);
        public static Vector3 nxy(this Vector2 v) => new Vector3(0, v.x, v.y);
        public static Vector3Int nxy(this Vector2Int v) => new Vector3Int(0, v.x, v.y);
        public static Vector3 xny(this Vector2 v) => new Vector3(v.x, 0, v.y);
        public static Vector3Int xny(this Vector2Int v) => new Vector3Int(v.x, 0, v.y);
        public static Vector2 xy(this Vector3 v) => new Vector2(v.x, v.y);
        public static Vector2Int xy(this Vector3Int v) => new Vector2Int(v.x, v.y);
        public static Vector2 yz(this Vector3 v) => new Vector2(v.y, v.z);
        public static Vector2Int yz(this Vector3Int v) => new Vector2Int(v.y, v.z);
        public static Vector2 xz(this Vector3 v) => new Vector2(v.x, v.z);
        public static Vector2Int xz(this Vector3Int v) => new Vector2Int(v.x, v.z);
        public static Vector2Int Swap(this Vector2Int v)
        {
            var temp = v.x;
            v.x = v.y;
            v.y = temp;
            return v;
        }

        public static Matrix4x4 Rotate(this Matrix4x4 matrix, Vector3 pivot, float angle, Axis axis)
        {
            Vector3 rotation = Vector3.zero;
            rotation[(int)axis] = angle;
            return Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(rotation)) * Matrix4x4.Translate(-pivot) * matrix;
        }
        public static Matrix4x4 Mirorr(this Matrix4x4 matrix, Vector3 pivot, Axis axis)
        {
            Vector3 scale = Vector3.one;
            scale[(int)axis] = -1;
            return Matrix4x4.Translate(pivot) * Matrix4x4.Scale(scale) * Matrix4x4.Translate(-pivot) * matrix;
        }

        public static Color SetAlpha(this Color color, float a)
        {
            color.a = a;
            return color;
        }

        public static int BinarySearch<T>(this T[] array, T value) where T : IComparable<T>
        {
            if (array.Length == 0)
                return -1;
            return BinarySearch(array, value, 0, array.Length);
        }
        private static int BinarySearch<T>(T[] array, T value, int start, int end) where T : IComparable<T>
        {
            int size = end - start;

            int midIndex = start + size / 2;
            int res = value.CompareTo(array[midIndex]);

            if (res == 0) return midIndex;

            if (size == 1)
                return -1;

            if (res > 0)
                return BinarySearch(array, value, midIndex, end);
            else
                return BinarySearch(array, value, start, midIndex);
        }

        public static void Swap<T>(T[] list, int a, int b)
        {
            var temp = list[a];
            list[a] = list[b];
            list[b] = temp;
        }
        public static void Fill<T>(this T[] array, int length, Func<T> constructor)
        {
            for (int i = 0; i < length; i++)
                array[i] = constructor();
        }
        public static void Fill<T>(this T[] array, Func<T> constructor)
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = constructor();
        }
        public static void Fill<T>(this List<T> list, int count, Func<T> constructor)
        {
            list.Clear();
            for (int i = 0; i < count; i++)
                list.Add(constructor());
        }
        public static void Fill<T>(this List<T> list, int count) where T : new()
        {
            list.Clear();
            for (int i = 0; i < count; i++)
                list.Add(new T());
        }
        public static void Swap<T>(this List<T> list, int a, int b)
        {
            var temp = list[a];
            list[a] = list[b];
            list[b] = temp;
        }
    }

}