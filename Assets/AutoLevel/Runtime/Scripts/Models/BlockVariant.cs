using System;
using System.Collections.Generic;
using UnityEngine;

public enum VariantAction
{
    RotateX,
    RotateY,
    RotateZ,
    MirrorX,
    MirrorY,
    MirrorZ,
    Flip
}

[Serializable]
public class BlockVariant
{
    public int this[int dir] => new XXHash().Append(connections[dir]).Append(FillUtility.GetSide(fill, dir));

#if UNITY_EDITOR
    [HideInInspector, SerializeField]
    public Vector3 position_editor_only;
#endif

    public int fill = 0;
    public List<VariantAction> actions = new List<VariantAction>();
    public BlockConnection connections = new BlockConnection();
    public float weight = 1f;

    private static readonly string[] action_prefix = { "_rx", "_ry", "_rz", "_mx", "_my", "_mz", "_f" };

    public BlockVariant()
    {
        actions = new List<VariantAction>();
    }

    public BlockVariant(BlockVariant other)
    {
#if UNITY_EDITOR
        position_editor_only = other.position_editor_only;
#endif
        actions = new List<VariantAction>(other.actions);
        fill = other.fill;
        connections = other.connections;
    }

    public override int GetHashCode()
    {
        var conn = new BlockConnection();
        for (int i = 0; i < 6; i++)
            conn[i] = this[i];
        return new XXHash().Append(GetActionHash()).Append(conn.GetHashCode());
    }

    public void ApplyActions(List<VariantAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
            ApplyAction(actions[i]);
    }

    public void ApplyAction(VariantAction action)
    {
        actions.Add(action);
        fill = FillUtility.ApplyAction(fill, action);
        connections.ApplyAction(action);
    }

    private int GetActionHash()
    {
        var hash = new XXHash();
        for (int i = 0; i < actions.Count; i++)
            hash = hash.Append(actions[i]);
        return hash;
    }

    public string GetActionPrefix()
    {
        var builder = new System.Text.StringBuilder(actions.Count * 2);
        for (int i = 0; i < actions.Count; i++)
            builder.Append(action_prefix[(int)actions[i]]);
        return builder.ToString();
    }
}