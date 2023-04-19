using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AutoLevel
{
    [Serializable]
    public struct InputWaveCell
    {
        public static InputWaveCell AllGroups => new InputWaveCell() { groups = int.MaxValue };

        public bool this[int index]
        {
            get => ((1 << index) & groups) > 0;
            set => groups = value ? (groups | 1 << index) : (groups & ~(1 << index));
        }

        [SerializeField]
        public int groups;

        public InputWaveCell(List<int> groups)
        {
            this.groups = 0;
            foreach (var g in groups)
                this[g] = true;
        }

        public bool Invalid() => groups == 0;
        public int GroupsCount(int groupCount) => GroupsEnum(groupCount).Count();
        public bool ContainAll => groups == int.MaxValue;

        public static bool operator ==(InputWaveCell a, InputWaveCell b)
        {
            return a.groups == b.groups;
        }

        public static bool operator !=(InputWaveCell a, InputWaveCell b)
        {
            return !(a == b);
        }

        struct GroupEnumerator : IEnumerator<int>, IEnumerable<int>
        {
            private int groupCount;
            private InputWaveCell inputWave;

            private int index;

            public GroupEnumerator(int groupCount, InputWaveCell inputWave)
            {
                this.groupCount = groupCount;
                this.inputWave = inputWave;
                index = 0;
            }

            public int Current => index - 1;

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                while (index < groupCount)
                {
                    if (inputWave[index++])
                        return true;
                }
                return false;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public IEnumerator<int> GetEnumerator() => this;

            IEnumerator IEnumerable.GetEnumerator() => this;
        }

        public IEnumerable<int> GroupsEnum(int groupCount)
        {
            return new GroupEnumerator(groupCount, this);
        }
    }
}
