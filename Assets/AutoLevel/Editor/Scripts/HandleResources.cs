using AlaslTools;
using UnityEditor;
using UnityEngine;

namespace AutoLevel
{
    public class HandleResources : System.IDisposable
    {
        public Material VariantMat              { get; private set; }
        public Material ColorCubeMat            { get; private set; }
        public Material ButtonCubeMat           { get; private set; }
        public Texture2D connectingIcon         { get; private set; }
        public Texture2D removeConnectionIcon   { get; private set; }

        private static int[] ColorCubeIds = new int[]
        {
        Shader.PropertyToID("_Left"),
        Shader.PropertyToID("_Down"),
        Shader.PropertyToID("_Back"),
        Shader.PropertyToID("_Right"),
        Shader.PropertyToID("_Up"),
        Shader.PropertyToID("_Front")
        };

        public void SetCubeColorMatSide(Color color, int d)
        {
            ColorCubeMat.SetColor(ColorCubeIds[d], color);
        }

        public HandleResources()
        {
            VariantMat = new Material(Shader.Find("Hidden/AutoLevel/Variant"));
            VariantMat.hideFlags = HideFlags.HideAndDontSave;
            ColorCubeMat = new Material(Shader.Find("Hidden/AutoLevel/ColorCube"));
            ColorCubeMat.hideFlags = HideFlags.HideAndDontSave;
            ButtonCubeMat = new Material(Shader.Find("Hidden/AutoLevel/CubeButton"));
            ButtonCubeMat.hideFlags = HideFlags.HideAndDontSave;

            var basePath = System.IO.Path.Combine(EditorHelper.GetAssemblyDirectory<LevelBuilderEditor>(), "Scripts", "Resources");

            connectingIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                System.IO.Path.Combine(basePath, "LevelBuilderConnecting.png"));
            removeConnectionIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                System.IO.Path.Combine(basePath, "LevelBuilderRemove.png"));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(VariantMat);
            Object.DestroyImmediate(ColorCubeMat);
            Object.DestroyImmediate(ButtonCubeMat);
        }
    }

}