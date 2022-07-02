using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BuilderSettingsSO : BaseSO<BuilderSettings>
{
    public BuilderSettingsSO(Object target) : base(target) { }
    public BuilderSettingsSO(SerializedObject serializedObject) : base(serializedObject) { }

    public BuilderSettings.Settings settings;
}

[CustomEditor(typeof(BuilderSettings))]
public class BuilderSettingsEditor : Editor
{
    private BuilderSettingsSO settingsSO;
    private BuilderSettings.Settings settings => settingsSO.settings;

    private void OnEnable()
    {
        settingsSO = new BuilderSettingsSO(target);
    }

    private void OnDisable()
    {
        settingsSO.Dispose();
    }

    public override void OnInspectorGUI()
    {
        GUILayout.BeginVertical(GUI.skin.box);

        EditorGUI.BeginChangeCheck();

        settings.GroupsToggle = GUILayout.Toggle(settings.GroupsToggle, "Show Groups", GUI.skin.button);

        EditorGUILayout.Space();

        settings.MaxIterations = EditorGUILayout.IntField("Max Iterations", settings.MaxIterations);
        settings.OverrideSolverSize = EditorGUILayout.Toggle("Override Solver Size",settings.OverrideSolverSize);
        if(settings.OverrideSolverSize)
            settings.SolverSize = EditorGUILayout.IntField("Solver Size", settings.SolverSize);

        settings.ExportSize = EditorGUILayout.IntField("Export Size", settings.ExportSize);

        GUILayout.EndVertical();

        if (EditorGUI.EndChangeCheck())
            settingsSO.Apply();
    }
}

public class BuilderSettings : ScriptableObject
{
    private static BuilderSettings instance;

    public static BuilderSettings GetSettings()
    {
        if (instance == null)
        {
            var scriptDir = System.IO.Path.Combine(EditorHelper.GetScriptDirectory<BuilderSettings>(), "Resources");
            var path = System.IO.Path.Combine(scriptDir, "Level Settings.asset");
            instance = AssetDatabase.LoadAssetAtPath<BuilderSettings>(path);
            if (instance == null)
            {
                instance = CreateInstance<BuilderSettings>();
                AssetDatabase.CreateAsset(instance, path);
            }
        }
        return instance;
    }

    [SerializeField]
    public Settings settings = new Settings()
    {
        GroupsToggle = false,
        MaxIterations = 10,
        OverrideSolverSize = false,
        SolverSize = 5,
        ExportSize = 5,
    };

    [System.Serializable]
    public class Settings
    {
        public bool GroupsToggle;
        [Space]
        public int MaxIterations;
        public bool OverrideSolverSize;
        public int SolverSize;
        [Space]
        public int ExportSize;
    }
}
