using System.Collections;
using System.Collections.Generic;

namespace AutoLevel
{

    public class BiDirectionalList<T> : IEnumerable<T>
    {
        private List<T> forward;
        private Dictionary<T, int> reverse;

        public T this[int i]
        {
            get => forward[i];
            set
            {
                reverse.Remove(forward[i]);
                forward[i] = value;
                reverse[value] = i;
            }
        }

        public int GetIndex(T value) => reverse[value];

        public bool Contains(T value) => reverse.ContainsKey(value);

        public BiDirectionalList()
        {
            forward = new List<T>();
            reverse = new Dictionary<T, int>();
        }

        public BiDirectionalList(List<T> forward)
        {
            this.forward = forward;
            reverse = new Dictionary<T, int>(forward.Count);
            for (int i = 0; i < forward.Count; i++)
                reverse[forward[i]] = i;
        }

        public void Add(T element)
        {
            forward.Add(element);
            reverse[element] = forward.Count - 1;
        }

        public void Clear()
        {
            forward.Clear();
            reverse.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return forward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)forward).GetEnumerator();
        }
    }

}