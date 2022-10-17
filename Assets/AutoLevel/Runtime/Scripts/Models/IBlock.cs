using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{

    public interface IBlock
    {
        int this[int d] { get; }

        GameObject gameObject { get; }
        Transform transform { get; }
        BlockAsset blockAsset { get; }
        BigBlockAsset bigBlock { get; }
        List<int> groups { get; }
        List<BlockAction> actions { get; }

        BlockResources blockResources { get; }
        Mesh baseMesh { get; }

        SideIds compositeIds { get; }
        SideIds baseIds { get; }
        int fill { get; }
        float weight { get; }

        StandalnoeBlock CreateCopy();
    }

    [System.Serializable]
    public struct AssetBlock : IBlock
    {
        public BlockAsset.VariantDesc Variant => m_blockAsset.variants[m_variantindex];
        public int VariantIndex => m_variantindex;

        [SerializeField]
        private int m_variantindex;
        [SerializeField]
        private BlockAsset m_blockAsset;
        [SerializeField]
        private int hashCode;

        public AssetBlock(int variantindex, BlockAsset blockAsset)
        {
            m_variantindex = variantindex;
            m_blockAsset = blockAsset;
            hashCode = 0;
            hashCode = BlockUtility.GenerateHash(this, Variant.actions);
        }

        public override int GetHashCode()
        {
            return hashCode;
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

        public int this[int d] => BlockUtility.GetSideCompositeId(this, d);

        public GameObject gameObject => m_blockAsset.gameObject;
        public Transform transform => gameObject.transform;
        public BlockAsset blockAsset => m_blockAsset;
        public BigBlockAsset bigBlock => Variant.bigBlock;
        public List<int> groups => m_blockAsset.groups;
        public List<BlockAction> actions => Variant.actions;
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
        public Mesh baseMesh => BlockUtility.GetMesh(gameObject);

        public SideIds compositeIds => BlockUtility.GetCompositeIds(this);

        public SideIds baseIds => Variant.sideIds;
        public int fill => Variant.fill;
        public float weight => Variant.weight;

        public StandalnoeBlock CreateCopy()
        {
            return new StandalnoeBlock(
                new List<BlockAction>(Variant.actions), gameObject, bigBlock,
                groups, baseIds, fill, weight);
        }
    }

    public struct StandalnoeBlock : IBlock
    {
        private BigBlockAsset m_bigBlock;

        public StandalnoeBlock(
            List<BlockAction> actions, GameObject gameObject, BigBlockAsset bigBlock, List<int> groups,
            SideIds baseIds, int fill, float weight)
        {
            this.actions = new List<BlockAction>(actions);
            this.gameObject = gameObject;
            this.m_bigBlock = bigBlock;
            this.groups = groups;
            this.baseIds = baseIds;
            this.fill = fill;
            this.weight = weight;
        }
        public StandalnoeBlock(List<int> groups, int fill, float weight)
        {
            this.actions = new List<BlockAction>();
            this.gameObject = null;
            this.baseIds = default;
            this.m_bigBlock = null;
            this.groups = groups;
            this.fill = fill;
            this.weight = weight;
        }

        public override int GetHashCode()
        {
            return BlockUtility.GenerateHash(this, actions);
        }
        public override bool Equals(object obj)
        {
            return obj is StandalnoeBlock @ref &&
                  @ref.GetHashCode() == GetHashCode();
        }
        public static bool operator ==(StandalnoeBlock a, StandalnoeBlock b)
        {
            return a.GetHashCode() == b.GetHashCode();
        }
        public static bool operator !=(StandalnoeBlock a, StandalnoeBlock b)
        {
            return !(a == b);
        }

        public int this[int d] => BlockUtility.GetSideCompositeId(this, d);

        public GameObject gameObject { get; set; }
        public Transform transform => gameObject.transform;
        public BlockAsset blockAsset => gameObject.GetComponent<BlockAsset>();
        public BigBlockAsset bigBlock => m_bigBlock;
        public List<int> groups { get; set; }
        public List<BlockAction> actions { get; set; }

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
        public Mesh baseMesh => BlockUtility.GetMesh(gameObject);

        public SideIds compositeIds => BlockUtility.GetCompositeIds(this);

        public SideIds baseIds { get; set; }
        public int fill { get; set; }
        public float weight { get; set; }

        public StandalnoeBlock CreateCopy()
        {
            return new StandalnoeBlock(
                new List<BlockAction>(actions), gameObject, m_bigBlock,
                groups, baseIds, fill, weight);
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