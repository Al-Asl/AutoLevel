using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{

    [AddComponentMenu("AutoLevel/BlockAsset")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BlockAsset : MonoBehaviour
    {
        [Serializable]
        public class VariantDesc
        {
            public BigBlockAsset bigBlock;
            public List<BlockAction> actions = new List<BlockAction>();
            public ConnectionsIds sideIds = new ConnectionsIds();
            public int fill = 0;
            public float weight = 1f;

#if UNITY_EDITOR
            [HideInInspector, SerializeField]
            public Vector3 position_editor_only;
#endif

            public VariantDesc()
            {
                actions = new List<BlockAction>();
            }

            public VariantDesc(VariantDesc other)
            {
#if UNITY_EDITOR
                position_editor_only = other.position_editor_only;
#endif
                actions = new List<BlockAction>(other.actions);
                fill = other.fill;
                sideIds = other.sideIds;
            }
        }

        [SerializeField]
        public int group;
        [SerializeField]
        public int weightGroup;
        [SerializeField]
        public List<int> actionsGroups = new List<int>();
        [SerializeField]
        public List<VariantDesc> variants = new List<VariantDesc>();

        public static IEnumerable<AssetBlock> GetBlocksEnum(IEnumerable<BlockAsset> assets, bool includeInactive = true)
        {
            foreach (var asset in assets)
                for (int i = 0; i < asset.variants.Count; i++)
                    if (asset.gameObject.activeInHierarchy || includeInactive)
                        yield return new AssetBlock(i, asset);
        }
    }

}