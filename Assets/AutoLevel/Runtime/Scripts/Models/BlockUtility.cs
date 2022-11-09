using UnityEngine;
using System.Collections.Generic;

namespace AutoLevel
{

    public static class BlockUtility
    {
        public static Mesh GetMesh(GameObject gameObject)
        {
            var mf = gameObject == null ? null : gameObject.GetComponent<MeshFilter>();
            return mf == null ? null : mf.sharedMesh;
        }

        public static Material GetMaterial(GameObject gameObject)
        {
            var mr = gameObject == null ? null : gameObject.GetComponent<MeshRenderer>();
            return mr == null ? null : mr.sharedMaterial;
        }

        public static Mesh GenerateMesh(Mesh mesh, List<BlockAction> actions)
        {
            var m = Object.Instantiate(mesh);
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;

            for (int i = 0; i < actions.Count; i++)
                ActionsUtility.ApplyAction(m, actions[i]);
            m.RecalculateBounds();
            m.RecalculateTangents();

            return m;
        }

        public static int GetSideCompositeId(IBlock block, int side) => new XXHash().Append(block.baseIds[side]).Append(FillUtility.GetSide(block.fill, side));

        public static SideIds GetCompositeIds(IBlock block)
        {
            var ids = new SideIds();
            for (int d = 0; d < 6; d++)
                ids[d] = GetSideCompositeId(block, d);
            return ids;
        }

        public static int GenerateHash(IBlock block, List<BlockAction> actions)
        {
            var mesh = block.baseMesh;
            if (mesh == null)
                return new XXHash().
                Append(block.compositeIds.GetHashCode()).
                Append(ActionsUtility.GetActionsHash(actions));
            else
                return new XXHash().Append(block.baseMesh.name.GetHashCode()).
                Append(block.compositeIds.GetHashCode()).
                Append(ActionsUtility.GetActionsHash(actions));
        }
    }

}