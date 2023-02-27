using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public class HandleResources : System.IDisposable
    {
        public Material VariantMat { get; private set; }
        public Material ColorCubeMat { get; private set; }
        public Material ButtonCubeMat { get; private set; }

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
        }

        public void Dispose()
        {
            Object.DestroyImmediate(VariantMat);
            Object.DestroyImmediate(ColorCubeMat);
            Object.DestroyImmediate(ButtonCubeMat);
        }
    }

}