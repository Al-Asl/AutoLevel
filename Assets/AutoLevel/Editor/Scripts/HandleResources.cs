using AlaslTools;
using UnityEditor;
using UnityEngine;

namespace AutoLevel
{
    public class HandleResources : System.IDisposable
    {
        public Material variant_mat                 { get; private set; }
        public Material color_cube_mat              { get; private set; }
        public Material button_cube_mat             { get; private set; }

        public Texture2D boundary_connect_icon                  { get; private set; }
        public Texture2D boundary_remove_icon                   { get; private set; }
        public Texture2D detatch_icon                           { get; private set; }
        public Texture2D weight_to_variant_icon                 { get; private set; }
        public Texture2D layer_to_variant_icon                  { get; private set; }
        public Texture2D remove_layer_icon                      { get; private set; }
        public Texture2D multi_connection_to_variant_icon       { get; private set; }
        public Texture2D banned_connection_to_variant_icon      { get; private set; }
        public Texture2D exclusive_connection_to_variant_icon   { get; private set; }
        public Texture2D remove_multi_connection_icon           { get; private set; }
        public Texture2D remove_banned_connection_icon          { get; private set; }
        public Texture2D remove_exclusive_connection_icon       { get; private set; }

        public GUIStyle gui_button_style
        {
            get
            {
                if(_gui_button_style == null)
                {
                    _gui_button_style = new GUIStyle(GUI.skin.button);
                    _gui_button_style.padding = new RectOffset(2,2,2,2);
                    _gui_button_style.fixedHeight = buttonSize;
                    _gui_button_style.fixedWidth = buttonSize;
                }
                return _gui_button_style;
            }
        }

        private GUIStyle _gui_button_style;

        private static int[] ColorCubeIds = new int[]
        {
            Shader.PropertyToID("_Left"),
            Shader.PropertyToID("_Down"),
            Shader.PropertyToID("_Back"),
            Shader.PropertyToID("_Right"),
            Shader.PropertyToID("_Up"),
            Shader.PropertyToID("_Front")
        };

        private string basePath;
        private const int buttonSize = 30;

        public void SetCubeColorMatSide(Color color, int d)
        {
            color_cube_mat.SetColor(ColorCubeIds[d], color);
        }

        public HandleResources()
        {
            variant_mat = new Material(Shader.Find("Hidden/AutoLevel/Variant"));
            variant_mat.hideFlags = HideFlags.HideAndDontSave;
            color_cube_mat = new Material(Shader.Find("Hidden/AutoLevel/ColorCube"));
            color_cube_mat.hideFlags = HideFlags.HideAndDontSave;
            button_cube_mat = new Material(Shader.Find("Hidden/AutoLevel/CubeButton"));
            button_cube_mat.hideFlags = HideFlags.HideAndDontSave;

            basePath = System.IO.Path.Combine(EditorHelper.GetAssemblyDirectory<LevelBuilderEditor>(), "Scripts", "Resources");

            boundary_connect_icon = GetTexture("boundary_connect.png");
            boundary_remove_icon = GetTexture("boundary_remove.png");

            detatch_icon = GetTexture("detatch.png");

            weight_to_variant_icon = GetTexture("weight_to_variant.png");
            layer_to_variant_icon = GetTexture("layer_to_variant.png");

            multi_connection_to_variant_icon = GetTexture("multi_connection_to_variant.png");
            banned_connection_to_variant_icon = GetTexture("banned_connection_to_variant.png");
            exclusive_connection_to_variant_icon = GetTexture("exclusive_connection_to_variant.png");

            remove_layer_icon = GetTexture("remove_layer.png");
            remove_multi_connection_icon = GetTexture("remove_multi_connection.png");
            remove_banned_connection_icon = GetTexture("remove_banned_connection.png");
            remove_exclusive_connection_icon = GetTexture("remove_exclusive_connection.png");
        }

        private Texture2D GetTexture(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>
                (System.IO.Path.Combine(basePath, name));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(variant_mat);
            Object.DestroyImmediate(color_cube_mat);
            Object.DestroyImmediate(button_cube_mat);
        }
    }

}