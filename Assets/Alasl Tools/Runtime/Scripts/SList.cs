using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlaslTools
{
    /// <summary>
    /// used as a list wrapper for serializing nested arrays
    /// </summary>
    [Serializable]
    public class SList<T> : IEnumerable<T> , IList<T>
    {
        public bool IsEmpty => list.Count == 0;
        public int Count => list.Count;
        public bool IsReadOnly => false;

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
        public int IndexOf(T item) => list.IndexOf(item);
        public void Insert(int index, T item) => list.Insert(index, item);
        public bool Contains(T item) => list.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
        public bool Remove(T item) => list.Remove(item);

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
}