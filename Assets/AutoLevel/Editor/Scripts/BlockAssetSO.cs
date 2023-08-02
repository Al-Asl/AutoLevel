using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    using static FillUtility;

    public class BlockAssetSO : BaseSO<BlockAsset>
    {
        public int                          group;
        public int                          weightGroup;

        public List<int>                    actionsGroups;
        public List<BlockAsset.VariantDesc> variants;

        public BlockAssetSO(SerializedObject serializedObject) : base(serializedObject) { IntegrityCheck(this); }
        public BlockAssetSO(Object target) : base(target) { IntegrityCheck(this); }

        public static void IntegrityCheck(BlockAsset blockAsset) 
        {  var so = new BlockAssetSO(blockAsset); so.Dispose(); }

        //some times the unity method fail, not sure why!
        private static T GetComponentInParent<T>(Transform transform) 
            where T : Component
        {
            var comp = transform.GetComponent<T>();
            if (comp != null)
                return comp;
            else
            {
                if (transform.parent == null)
                    return null;
                else
                    return GetComponentInParent<T>(transform.parent);
            }
        }

        private static void IntegrityCheck(BlockAssetSO so)
        {
            var repo = GetComponentInParent<BlocksRepo>(so.target.transform);
            if (repo == null)
            {
                Debug.LogError("Integrity check failed, the asset is not a child of a repo!");
                return;
            }

            if (so.variants.Count == 0)
            {
                var infos = MeshCombiner.GetRenderers(so.target.transform);
                Mesh mesh = infos.Count > 0 ? infos[0].mesh : null;

                so.variants.Add(new BlockAsset.VariantDesc()
                {
                    fill = GenerateFill(mesh),
                    sideIds = new ConnectionsIds()
                });
                so.ApplyField(nameof(variants));
            }

            var groupNames = repo.GetAllGroupsNames();

            if (so.group == 0 || groupNames.FindIndex((name) => name.GetHashCode() == so.group) == -1)
            {
                //adding the base group
                so.group = groupNames[2].GetHashCode();
                so.ApplyField(nameof(group));
            }

            var weightGroupNames = repo.GetAllWeightGroupsNames();

            if (so.weightGroup == 0 || weightGroupNames.FindIndex((name) => name.GetHashCode() == so.weightGroup) == -1)
            {
                //adding the base group
                so.weightGroup = weightGroupNames[2].GetHashCode();
                so.ApplyField(nameof(weightGroup));
            }
        }
    }
}