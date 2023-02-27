using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{

    [CustomEditor(typeof(LevelEditorSettings))]
    public class LevelEditorSettingsEditor : Editor
    {
        public class SO : BaseSO<LevelEditorSettings>
        {
            public SO(Object target) : base(target) { }
            public SO(SerializedObject serializedObject) : base(serializedObject) { }

            public LevelEditorSettings.Settings settings;
        }

        private SO settingsSO;
        public LevelEditorSettings.Settings Settings => settingsSO.settings;

        public void Apply() => settingsSO.Apply();
        public void Draw()
        {
            EditorGUI.BeginChangeCheck();
            var settingsExpand = settingsSO.GetFieldExpand(nameof(SO.settings));
            settingsExpand = EditorGUILayout.BeginFoldoutHeaderGroup(settingsExpand, "Settings");

            if (EditorGUI.EndChangeCheck())
                settingsSO.SetFieldExpand(nameof(SO.settings), settingsExpand);

            GUILayout.BeginVertical(GUI.skin.box);
            if (settingsExpand)
                OnInspectorGUI();
            GUILayout.EndVertical();

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void OnEnable()
        {
            settingsSO = new SO(target);
        }

        private void OnDisable()
        {
            settingsSO.Dispose();
        }

        public override void OnInspectorGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            EditorGUI.BeginChangeCheck();

            Settings.GroupsToggle = GUILayout.Toggle(Settings.GroupsToggle, "Show Groups", GUI.skin.button);

            EditorGUILayout.Space();

            Settings.MaxIterations = EditorGUILayout.IntField("Max Iterations", Settings.MaxIterations);
            Settings.ExportSize = EditorGUILayout.IntField("Export Size", Settings.ExportSize);

            GUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
                settingsSO.Apply();
        }
    }

    public class LevelEditorSettings : ScriptableObject
    {
        private static LevelEditorSettings instance;

        public static LevelEditorSettings GetSettings()
        {
            if (instance == null)
            {
                var scriptDir = System.IO.Path.Combine(EditorHelper.GetAssemblyDirectory<LevelEditorSettings>(),"Scripts" ,"Resources");
                var path = System.IO.Path.Combine(scriptDir, "Level Settings.asset");
                instance = AssetDatabase.LoadAssetAtPath<LevelEditorSettings>(path);
                if (instance == null)
                {
                    instance = CreateInstance<LevelEditorSettings>();
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
            ExportSize = 5,
        };

        [System.Serializable]
        public class Settings
        {
            public bool GroupsToggle;
            [Space]
            public int MaxIterations;
            [Space]
            public int ExportSize;
            [Space]
            public int Tool;
        }
    }

}