using UnityEngine;
using System.Collections.Generic;
using AlaslTools;

namespace AutoLevel
{
    public enum BlockAction
    {
        RotateX = 0,
        RotateY = 1,
        RotateZ = 2,
        MirrorX = 3,
        MirrorY = 4,
        MirrorZ = 5,
        Flip    = 6
    }

    public static class ActionsUtility
    {
        public const int EMPTY_ACTIONS_HASH = 0;

        private static readonly string[] action_prefix = { "_rx", "_ry", "_rz", "_mx", "_my", "_mz", "_f" };

        private static int[][] faceActions = new int[][]
        {
           new int[] { 0, 2, 4, 3, 5, 1 }, //Rotate X
           new int[] { 5, 1, 0, 2, 4, 3 }, //Rotate Y
           new int[] { 1, 3, 2, 4, 0, 5 }, //Rotate Z
           new int[] { 3, 1, 2, 0, 4, 5 }, //Mirror X
           new int[] { 0, 4, 2, 3, 1, 5 }, //Mirror Y
           new int[] { 0, 1, 5, 3, 4, 2 }, //Mirror Z
           new int[] { 0, 1, 2, 3, 4, 5 }, //Flip
        };

        public static bool AreEquals(List<BlockAction> a, List<BlockAction>b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;

            return true;
        }

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

        public static int TransformFace(int d, IEnumerable<BlockAction> actions)
        {
            foreach (var action in actions)
                d = faceActions[(int)action][d];
            return d;
        }

        public static int TransformFace(int d, BlockAction action)
        {
            return faceActions[(int)action][d];
        }

        public static ConnectionsIds ApplyAction(ConnectionsIds sides, BlockAction action)
        {
            ConnectionsIds res = default;

            for (int d = 0; d < 6; d++)
                res[TransformFace(d, action)] = sides[d];

            return res;
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