using UnityEditor;
using UnityEngine;

public enum BlockEditMode 
{
    None,
    Fill,
    Connection
}

public class BlockAssetEditorSettingsSO : BaseSO<BlockAssetEditorSettings>
{
    public BlockAssetEditorSettings.Settings settings;

    public BlockAssetEditorSettingsSO(Object target) : base(target) { }

    public BlockAssetEditorSettingsSO(SerializedObject serializedObject) : base(serializedObject) { }
}

public class BlockAssetEditorSettings : ScriptableObject
{
    public static BlockAssetEditorSettings GetSettings()
    {
        var scriptDir = System.IO.Path.Combine(EditorHelper.GetScriptDirectory<BlockAssetEditorSettings>(), "Resources");
        var path = System.IO.Path.Combine(scriptDir, "Block Settings.asset");
        var settings = AssetDatabase.LoadAssetAtPath<BlockAssetEditorSettings>(path);
        if (settings == null)
        {
            settings = CreateInstance<BlockAssetEditorSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }
        return settings;
    }

    [System.Serializable]
    public class Settings
    {
        [HideInInspector]
        public bool GroupToggle;

        public bool DrawSelfConnections;
        public int MaxConnectionsDrawCount;
        public BlockEditMode EditMode;
        public bool DrawVariants;
    }

    [SerializeField]
    public Settings settings =
        new Settings()
        {
            DrawSelfConnections = true,
            MaxConnectionsDrawCount = 30,
            DrawVariants = true
        };
}