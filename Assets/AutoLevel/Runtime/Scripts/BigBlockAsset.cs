using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{

    public class BigBlockAsset : MonoBehaviour
    {
        [SerializeField]
        public List<int> actionsGroups;
        [SerializeField]
        public Array3D<AssetBlock> data = new Array3D<AssetBlock>(Vector3Int.one);
    }
}