using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{
    public interface IBlock
    {
        int this[int d] { get; }

        bool hasGameObject { get; }
        GameObject gameObject { get; }
        Transform transform { get; }
        BlockAsset blockAsset { get; }
        BigBlockAsset bigBlock { get; }

        int group { get; }
        int weightGroup { get; }

        List<BlockAction> actions { get; }
        Mesh baseMesh { get; }
        BlockResources blockResources { get; }

        ConnectionsIds compositeIds { get; }
        ConnectionsIds baseIds { get; }
        int fill { get; }
        float weight { get; }

        LayerSettings layerSettings { get; }

        StandaloneBlock CreateCopy();
    }

    /// <summary>
    /// represent block by referencing it's variant
    /// </summary>
    [System.Serializable]
    public struct AssetBlock : IBlock
    {
        public bool Valid                       => m_blockAsset != null && m_variantindex < m_blockAsset.variants.Count;
        public BlockAsset.VariantDesc Variant   => m_blockAsset.variants[m_variantindex];
        public int VariantIndex                 => m_variantindex;

        [SerializeField]
        private BlockAsset                          m_blockAsset;
        [SerializeField]
        private int                                 m_variantindex;

        public AssetBlock(int variantindex, BlockAsset blockAsset)
        {
            m_variantindex = variantindex;
            m_blockAsset = blockAsset;
        }
        public int this[int d] => BlockUtility.GetSideCompositeId(this, d);

        public bool hasGameObject           => m_blockAsset != null;
        public GameObject gameObject        => m_blockAsset.gameObject;
        public Transform transform          => gameObject.transform;
        public BlockAsset blockAsset        => m_blockAsset;
        public BigBlockAsset bigBlock       => Variant.bigBlock;

        public int group                    => m_blockAsset.group;
        public int weightGroup              => m_blockAsset.weightGroup;

        public List<BlockAction> actions    => Variant.actions;
        public Mesh baseMesh                => BlockUtility.GetMesh(gameObject);
        public BlockResources blockResources
        {
            get
            {
                var mesh = baseMesh;
                if (mesh != null)
                    mesh = BlockUtility.GenerateMesh(baseMesh, Variant.actions);

                return new BlockResources()
                {
                    mesh = mesh,
                    material = BlockUtility.GetMaterial(gameObject)
                };
            }
        }

        public ConnectionsIds compositeIds  => BlockUtility.GetCompositeIds(this);
        public ConnectionsIds baseIds       => Variant.sideIds;
        public int fill                     => Variant.fill;
        public float weight                 => Variant.weight;

        public LayerSettings layerSettings  => Variant.layerSettings;

        public override int GetHashCode()
        {
            return BlockUtility.GenerateHash(this);
        }
        public override bool Equals(object obj)
        {
            return obj is AssetBlock @ref &&
                  VariantIndex == @ref.m_variantindex && blockAsset == @ref.blockAsset;
        }
        public static bool operator ==(AssetBlock a, AssetBlock b)
        {
            return a.m_variantindex == b.m_variantindex && a.blockAsset == b.blockAsset;
        }
        public static bool operator !=(AssetBlock a, AssetBlock b)
        {
            return !(a == b);
        }

        public StandaloneBlock CreateCopy()
        {
            return new StandaloneBlock(
                new List<BlockAction>(Variant.actions), gameObject, bigBlock,
                group, weightGroup, baseIds, fill, weight, layerSettings);
        }
    }

    /// <summary>
    /// represent a stand alone block, a connection to block asset is optional
    /// </summary>
    public class StandaloneBlock : IBlock
    {
        private BigBlockAsset m_bigBlock;

        public StandaloneBlock(
            List<BlockAction> actions, GameObject gameObject, BigBlockAsset bigBlock,
            int group, int weightGroup, ConnectionsIds baseIds, int fill, float weight,
            LayerSettings layerSettings)
        {
            this.actions        = new List<BlockAction>(actions);
            this.gameObject     = gameObject;
            this.m_bigBlock     = bigBlock;
            this.group          = group;
            this.weightGroup    = weightGroup;
            this.baseIds        = baseIds;
            this.fill           = fill;
            this.weight         = weight;
            this.layerSettings  = new LayerSettings(layerSettings);
        }
        public StandaloneBlock(int group,int weightGroup, int fill, float weight,int layer)
        {
            this.actions        = new List<BlockAction>();
            this.gameObject     = null;
            this.baseIds        = default;
            this.m_bigBlock     = null;
            this.weightGroup    = weightGroup;
            this.group          = group;
            this.fill           = fill;
            this.weight         = weight;
            this.layerSettings  = new LayerSettings(layer);
        }

        public int this[int d] => BlockUtility.GetSideCompositeId(this, d);

        public bool hasGameObject => gameObject != null;
        public GameObject gameObject { get; set; }
        public Transform transform => gameObject.transform;
        public BlockAsset blockAsset => gameObject.GetComponent<BlockAsset>();
        public BigBlockAsset bigBlock => m_bigBlock;

        public int group { get; set; }
        public int weightGroup { get; set; }

        public List<BlockAction> actions { get; set; }
        public Mesh baseMesh => BlockUtility.GetMesh(gameObject);
        public BlockResources blockResources
        {
            get
            {
                var mesh = baseMesh;
                if (mesh != null)
                    mesh = BlockUtility.GenerateMesh(baseMesh, actions);

                return new BlockResources()
                {
                    mesh = mesh,
                    material = BlockUtility.GetMaterial(gameObject)
                };
            }
        }

        public ConnectionsIds compositeIds => BlockUtility.GetCompositeIds(this);
        public ConnectionsIds baseIds { get; set; }
        public int fill { get; set; }
        public float weight { get; set; }

        public LayerSettings layerSettings { get; private set; }

        public override int GetHashCode()
        {
            return BlockUtility.GenerateHash(this);
        }
        public override bool Equals(object obj)
        {
            return obj is StandaloneBlock @ref &&
                  @ref.GetHashCode() == GetHashCode();
        }
        public static bool operator ==(StandaloneBlock a, StandaloneBlock b)
        {
            return a.GetHashCode() == b.GetHashCode();
        }
        public static bool operator !=(StandaloneBlock a, StandaloneBlock b)
        {
            return !(a == b);
        }

        public StandaloneBlock CreateCopy()
        {
            return new StandaloneBlock(
                new List<BlockAction>(actions), gameObject, m_bigBlock,
                group, weightGroup, baseIds, fill, weight, layerSettings);
        }

        public void ApplyAction(BlockAction action)
        {
            actions.Add(action);
            fill = ActionsUtility.ApplyAction(fill, action);
            baseIds = ActionsUtility.ApplyAction(baseIds, action);
        }
        public void ApplyActions(List<BlockAction> actions)
        {
            foreach (var action in actions)
                ApplyAction(action);
        }
    }

}