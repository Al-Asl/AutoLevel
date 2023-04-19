using System.Collections.Generic;
using UnityEngine;

namespace AlaslTools
{
    public class GameObjectUtil
    {
        public static Mesh GetPrimitiveMesh(PrimitiveType meshType)
        {
            var go = GameObject.CreatePrimitive(meshType);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            SafeDestroy(go);
            return mesh;
        }

        public static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                Object.DestroyImmediate(obj, false);
            else
#endif
                Object.Destroy(obj);

        }
    }
}
