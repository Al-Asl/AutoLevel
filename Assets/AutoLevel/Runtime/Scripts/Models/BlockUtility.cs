using AlaslTools;

namespace AutoLevel
{

    public static class BlockUtility
    {
        public static bool IsActive(IBlock block)
        {
            if (!block.hasGameObject) return true;
            if (block.gameObject.activeInHierarchy) return true;
            if (block.bigBlock != null && block.bigBlock.gameObject.activeInHierarchy) return true;
            return false;
        }

        public static int GetSideCompositeId(IBlock block, int side)
        {
            return new XXHash().
                Append(block.baseIds[side]).
                Append(block.layerSettings.PartOfBaseLayer ? FillUtility.GetSide(block.fill, side) : 0);
        }

        public static ConnectionsIds GetCompositeIds(IBlock block)
        {
            var ids = new ConnectionsIds();
            for (int d = 0; d < 6; d++)
                ids[d] = GetSideCompositeId(block, d);
            return ids;
        }

        public static int GenerateHash(IBlock block)
        {
            return new XXHash(1).
                //Append(mesh != null ? block.baseMesh.name.GetHashCode() : 0).
                Append(block.hasGameObject ? block.gameObject.name.GetHashCode() : 0).
                Append(block.compositeIds.GetHashCode()).
                Append(ActionsUtility.GetActionsHash(block.actions)).
                Append(block.layerSettings.layer);
        }
    }

}