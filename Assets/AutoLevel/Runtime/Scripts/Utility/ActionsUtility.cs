using UnityEngine;
using System.Collections.Generic;
using AlaslTools;

namespace AutoLevel
{
    public enum BlockAction
    {
        RotateX,
        RotateY,
        RotateZ,
        MirrorX,
        MirrorY,
        MirrorZ,
        Flip
    }

    public static class ActionsUtility
    {
        private static readonly string[] action_prefix = { "_rx", "_ry", "_rz", "_mx", "_my", "_mz", "_f" };

        public static int GetActionsHash(List<BlockAction> actions)
        {
            var hash = new XXHash();
            for (int i = 0; i < actions.Count; i++)
                hash = hash.Append(actions[i]);
            return hash;
        }

        public static string GetActionPrefix(List<BlockAction> actions)
        {
            var builder = new System.Text.StringBuilder(actions.Count * 2);
            for (int i = 0; i < actions.Count; i++)
                builder.Append(action_prefix[(int)actions[i]]);
            return builder.ToString();
        }

        public static ConnectionsIds ApplyAction(ConnectionsIds sides, BlockAction action)
        {
            int temp = 0;
            switch (action)
            {
                case BlockAction.RotateX:
                    temp = sides.down;
                    sides.down = sides.forward;
                    sides.forward = sides.up;
                    sides.up = sides.backward;
                    sides.backward = temp;
                    break;
                case BlockAction.RotateY:
                    temp = sides.left;
                    sides.left = sides.backward;
                    sides.backward = sides.right;
                    sides.right = sides.forward;
                    sides.forward = temp;
                    break;
                case BlockAction.RotateZ:
                    temp = sides.left;
                    sides.left = sides.up;
                    sides.up = sides.right;
                    sides.right = sides.down;
                    sides.down = temp;
                    break;
                case BlockAction.MirrorX:
                    temp = sides.left;
                    sides.left = sides.right;
                    sides.right = temp;
                    break;
                case BlockAction.MirrorY:
                    temp = sides.down;
                    sides.down = sides.up;
                    sides.up = temp;
                    break;
                case BlockAction.MirrorZ:
                    temp = sides.backward;
                    sides.backward = sides.forward;
                    sides.forward = temp;
                    break;
            }
            return sides;
        }

        public static int ApplyAction(int fill, BlockAction action)
        {
            switch (action)
            {
                case BlockAction.RotateX:
                    return FillUtility.RotateFill(fill, Axis.x);
                case BlockAction.RotateY:
                    return FillUtility.RotateFill(fill, Axis.y);
                case BlockAction.RotateZ:
                    return FillUtility.RotateFill(fill, Axis.z);
                case BlockAction.MirrorX:
                    return FillUtility.MirrorFill(fill, Axis.x);
                case BlockAction.MirrorY:
                    return FillUtility.MirrorFill(fill, Axis.y);
                case BlockAction.MirrorZ:
                    return FillUtility.MirrorFill(fill, Axis.z);
                case BlockAction.Flip:
                    return FillUtility.FlipFill(fill);
            }
            return fill;
        }

        public static void ApplyAction(Mesh mesh, BlockAction action)
        {
            switch (action)
            {
                case BlockAction.RotateX:
                    MeshUtility.Rotate(mesh, Vector3.one * 0.5f, 90f, Axis.x);
                    break;
                case BlockAction.RotateY:
                    MeshUtility.Rotate(mesh, Vector3.one * 0.5f, 90f, Axis.y);
                    break;
                case BlockAction.RotateZ:
                    MeshUtility.Rotate(mesh, Vector3.one * 0.5f, 90f, Axis.z);
                    break;
                case BlockAction.MirrorX:
                    MeshUtility.Mirror(mesh, Vector3.one * 0.5f, Axis.x);
                    break;
                case BlockAction.MirrorY:
                    MeshUtility.Mirror(mesh, Vector3.one * 0.5f, Axis.y);
                    break;
                case BlockAction.MirrorZ:
                    MeshUtility.Mirror(mesh, Vector3.one * 0.5f, Axis.z);
                    break;
                case BlockAction.Flip:
                    MeshUtility.Flip(mesh);
                    break;
            }
        }

        public static void ApplyAction(GameObject go, BlockAction action)
        {
            Transform pivot = new GameObject("pivot").transform;
            go.transform.SetParent(pivot);

            switch (action)
            {
                case BlockAction.RotateX:
                    pivot.transform.RotateAround(Vector3.one * 0.5f, Vector3.right, 90);
                    break;
                case BlockAction.RotateY:
                    pivot.transform.RotateAround(Vector3.one * 0.5f, Vector3.up, 90);
                    break;
                case BlockAction.RotateZ:
                    pivot.transform.RotateAround(Vector3.one * 0.5f, Vector3.forward, 90);
                    break;
                case BlockAction.MirrorX:
                    {
                        pivot.transform.position = Vector3.right;
                        pivot.transform.localScale = new Vector3(-1, 1, 1);
                    }
                    break;
                case BlockAction.MirrorY:
                    {
                        pivot.transform.position = Vector3.up;
                        pivot.transform.localScale = new Vector3(1, -1, 1);
                    }
                    break;
                case BlockAction.MirrorZ:
                    {
                        pivot.transform.position = Vector3.forward;
                        pivot.transform.localScale = new Vector3(1, 1, -1);
                    }
                    break;
            }

            go.transform.SetParent(null);
            GameObjectUtil.SafeDestroy(pivot.gameObject);
        }
    }

}