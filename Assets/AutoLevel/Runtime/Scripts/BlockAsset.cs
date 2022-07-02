using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockData
{
    public BlockResources resource;
    public BlockConnection connections;
    public float weight;
}

[AddComponentMenu("AutoLevel/BlockAsset")] [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BlockAsset : MonoBehaviour
{
    [SerializeField]
    public List<int> groups = new List<int>();
    [SerializeField]
    public List<BlockVariant> variants = new List<BlockVariant>();

    public BlockData GetBlockData(BlockVariant variant) =>  GetBlockData(gameObject, variant);
    public int GetBlockDataHash(BlockVariant variant) => GetBlockDataHash(gameObject, variant);

    private static Mesh GenerateMesh(Mesh mesh, List<VariantAction> actions)
    {
        var m = Instantiate(mesh);
        m.hideFlags = HideFlags.DontUnloadUnusedAsset;

        for (int i = 0; i < actions.Count; i++)
            MeshUtility.ApplyAction(actions[i], m);
        m.RecalculateBounds();
        m.RecalculateTangents();

        return m;
    }

    private static Mesh GetMesh(GameObject gameObject)
    {
        var mf = gameObject == null ? null : gameObject.GetComponent<MeshFilter>();
        return mf == null ? null : mf.sharedMesh;
    }

    public static int GetBlockDataHash(GameObject gameObject, BlockVariant variant)
    {
        var mesh = GetMesh(gameObject);
        var meshHash = mesh == null ? 0 : mesh.GetHashCode();
        return new XXHash().Append(meshHash).Append(variant.GetHashCode());
    }

    public static BlockData GetBlockData(GameObject gameObject,BlockVariant variant)
    {
        var mr = gameObject == null ? null : gameObject.GetComponent<MeshRenderer>();

        var connections = new BlockConnection();
        for (int k = 0; k < 6; k++)
            connections[k] = variant[k];

        Mesh mesh = GetMesh(gameObject);
        Mesh meshVariant = null;
        if (mesh != null)
        {
            meshVariant = GenerateMesh(mesh, variant.actions);
            meshVariant.name = meshVariant.name + variant.GetActionPrefix();
        }

        return new BlockData()
        {
            resource = new BlockResources()
            {
                mesh = meshVariant,
                material = mr == null ? null  : mr.sharedMaterial,
            },
            connections = connections,
            weight = variant.weight
        };
    }

    public static void IterateVariants(ListSlice<BlockAsset> blockAssets, System.Action<VariantRef> excute
        , bool includeInactive = true)
    {
        for (int i = 0; i < blockAssets.Count; i++)
        {
            var block = blockAssets[i];
            if (!includeInactive && !block.gameObject.activeSelf)
                continue;
            var variantsCount = block.variants.Count;
            for (int j = 0; j < variantsCount; j++)
                excute(new VariantRef(block, j));
        }
    }

    public static void IterateVariants(BlockAsset[] blockAssets, System.Action<VariantRef> excute
    , bool includeInactive = true)
    {
        for (int i = 0; i < blockAssets.Length; i++)
        {
            var block = blockAssets[i];
            if (!includeInactive && !block.gameObject.activeSelf)
                continue;
            var variantsCount = block.variants.Count;
            for (int j = 0; j < variantsCount; j++)
                excute(new VariantRef(block, j));
        }
    }
}