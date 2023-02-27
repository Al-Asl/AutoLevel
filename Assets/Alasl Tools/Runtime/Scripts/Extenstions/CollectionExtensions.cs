using System.Collections.Generic;
using System;

namespace AlaslTools
{
    public static class CollectionExtensions
    {
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

        public static void Swap<T>(this T[] list, int a, int b)
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
        public static void Swap<T>(this List<T> list, int a, int b)
        {
            var temp = list[a];
            list[a] = list[b];
            list[b] = temp;
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
    }
}