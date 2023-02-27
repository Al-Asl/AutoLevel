using System.Collections.Generic;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    public class BigBlockAsset : MonoBehaviour
    {
        [SerializeField]
        public bool overrideGroup;
        [SerializeField]
        public int group;

        [SerializeField]
        public bool overrideWeightGroup;
        [SerializeField]
        public int weightGroup;

        [SerializeField]
        public List<int> actionsGroups;
        [SerializeField]
        public Array3D<SList<AssetBlock>> data = new Array3D<SList<AssetBlock>>(Vector3Int.one);
    }
}