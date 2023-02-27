using System;
using System.Collections.Generic;

namespace AlaslTools
{

    [Serializable]
    public struct ListSlice<T>
    {
        public int Count, Start;
        [UnityEngine.SerializeReference]
        List<T> list;

        public ListSlice(List<T> list, int start, int count)
        {
            this.list = list;
            this.Start = start;
            this.Count = count;
            if (Count > list.Count) Count = list.Count;
        }

        public ListSlice(List<T> list)
        {
            this.list = list;
            this.Start = 0;
            this.Count = list.Count;
        }

        public ListSlice<T> GetSlice(int start, int end)
        {
            return new ListSlice<T>(list, Start + start, end - start);
        }

        public List<T> GetList()
        {
            return list;
        }

        public T this[int i]
        {
            get
            {
                return list[i + Start];
            }
            set
            {
                list[i + Start] = value;
            }
        }

        public void Swap(int i, int j)
        {
            list.Swap(i, j);
        }

        public static implicit operator ListSlice<T>(List<T> list) => new ListSlice<T>(list);
    }

}