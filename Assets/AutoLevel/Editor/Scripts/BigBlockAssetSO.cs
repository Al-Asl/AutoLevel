using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class BigBlockAssetSO : BaseSO<BigBlockAsset>
    {
        public int          blockLayer;

        public bool         overrideGroup;
        public int          group;

        public bool         overrideWeightGroup;
        public int          weightGroup;

        public List<int>    actionsGroups;
        public Array3D<SList<AssetBlock>> data;

        public BigBlockAssetSO(SerializedObject serializedObject) : base(serializedObject) { IntegrityCheck(this); }
        public BigBlockAssetSO(Object target) : base(target) { IntegrityCheck(this); }

        public static void IntegrityCheck(BigBlockAsset blockAsset) 
        { var so = new BigBlockAssetSO(blockAsset); so.Dispose(); }

        private static void IntegrityCheck(BigBlockAssetSO so)
        {
            bool apply = false;
            foreach (var index in SpatialUtil.Enumerate(so.data.Size))
            {
                var oList = so.data[index];
                var nList = new SList<AssetBlock>();
                for (int i = 0; i < oList.Count; i++)
                {
                    var block = oList[i];
                    if (block.Valid)
                        nList.Add(block);
                }
                if (oList.Count != nList.Count)
                {
                    so.data[index] = nList;
                    apply = true;
                }
            }
            if (apply)
                so.ApplyField(nameof(data));
        }
    }
}