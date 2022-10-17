using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AutoLevel
{

    public class HashedFlagList
    {
        private List<string> allFlags;
        private Dictionary<int, string> hashToFlag;

        private ReorderableList reorderableList;
        private System.Action OnChange;

        public int MinListSize = 0;

        public HashedFlagList(List<string> allFlags, List<int> list, System.Action OnChange)
        {
            this.OnChange = OnChange;

            this.allFlags = allFlags;
            hashToFlag = new Dictionary<int, string>();
            for (int i = 0; i < allFlags.Count; i++)
            {
                var o = allFlags[i];
                hashToFlag[o.GetHashCode()] = o;
            }

            //integrity check
            if (list != null)
                for (int i = list.Count - 1; i > -1; i--)
                    if (!hashToFlag.ContainsKey(list[i]))
                        list.RemoveAt(i);

            OnChange?.Invoke();

            CreateReordable();
        }

        private void CreateReordable()
        {
            reorderableList = new ReorderableList(null, typeof(int));
            reorderableList.draggable = false;
            reorderableList.headerHeight = 0;
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                EditorGUI.LabelField(rect, hashToFlag[(int)reorderableList.list[index]]);
            };
            reorderableList.onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < allFlags.Count; i++)
                {
                    var g = allFlags[i].GetHashCode();
                    if (!list.list.Contains(g))
                    {
                        menu.AddItem(new GUIContent(allFlags[i]), false, () =>
                        {
                            list.list.Add(g);
                            OnChange?.Invoke();
                        });
                    }
                }
                menu.DropDown(buttonRect);
            };
            reorderableList.onRemoveCallback += (list) =>
            {
                if (list.list.Count > MinListSize)
                {
                    list.list.RemoveAt(list.index);
                    OnChange?.Invoke();
                }
            };
        }

        public void Draw(List<int> list)
        {
            reorderableList.list = list;
            reorderableList.DoLayoutList();
        }
    }

}