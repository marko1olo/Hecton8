#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Hecton8.UI.Editor
{
    /// <summary>
    /// Custom editor for SettingsPanelAnimator.
    /// Provides utility buttons for auto-setup of CanvasGroups.
    /// </summary>
    [CustomEditor(typeof(SettingsPanelAnimator))]
    public sealed class SettingsPanelAnimatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Setup CanvasGroups"))
            {
                AutoSetupCanvasGroups();
            }

            if (GUILayout.Button("Clear All CanvasGroups"))
            {
                ClearCanvasGroups();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Auto-Setup will:\n" +
                "1. Find all child UI elements\n" +
                "2. Create CanvasGroups for animation groups\n" +
                "3. Assign references to animator\n\n" +
                "Expected hierarchy:\n" +
                "- Header (TMP_Text or GameObject)\n" +
                "- PresetButtons (Container with 4 buttons)\n" +
                "- SettingsRows (Container with N rows)\n" +
                "- ActionButtons (Container with Apply/Cancel)",
                MessageType.Info);
        }

        private void AutoSetupCanvasGroups()
        {
            SettingsPanelAnimator animator = (SettingsPanelAnimator)target;
            GameObject root = animator.gameObject;

            Undo.RecordObject(animator, "Auto-Setup CanvasGroups");

            // Find header
            Transform header = root.transform.Find("Header");
            if (header != null)
            {
                CanvasGroup headerGroup = GetOrAddCanvasGroup(header.gameObject);
                SerializedObject so = new SerializedObject(animator);
                so.FindProperty("headerGroup").objectReferenceValue = headerGroup;
                so.ApplyModifiedProperties();
                Debug.Log($"[SettingsPanelAnimatorEditor] Header CanvasGroup assigned: {header.name}");
            }

            // Find preset buttons
            Transform presetContainer = root.transform.Find("PresetButtons");
            if (presetContainer != null)
            {
                CanvasGroup[] presetGroups = new CanvasGroup[presetContainer.childCount];
                for (int i = 0; i < presetContainer.childCount; i++)
                {
                    Transform child = presetContainer.GetChild(i);
                    presetGroups[i] = GetOrAddCanvasGroup(child.gameObject);
                }

                SerializedObject so = new SerializedObject(animator);
                SerializedProperty presetProp = so.FindProperty("presetButtonGroups");
                presetProp.arraySize = presetGroups.Length;
                for (int i = 0; i < presetGroups.Length; i++)
                {
                    presetProp.GetArrayElementAtIndex(i).objectReferenceValue = presetGroups[i];
                }
                so.ApplyModifiedProperties();
                Debug.Log($"[SettingsPanelAnimatorEditor] Preset button CanvasGroups assigned: {presetGroups.Length}");
            }

            // Find settings rows
            Transform settingsContainer = root.transform.Find("SettingsRows");
            if (settingsContainer != null)
            {
                CanvasGroup[] settingsGroups = new CanvasGroup[settingsContainer.childCount];
                for (int i = 0; i < settingsContainer.childCount; i++)
                {
                    Transform child = settingsContainer.GetChild(i);
                    settingsGroups[i] = GetOrAddCanvasGroup(child.gameObject);
                }

                SerializedObject so = new SerializedObject(animator);
                SerializedProperty settingsProp = so.FindProperty("settingsRowGroups");
                settingsProp.arraySize = settingsGroups.Length;
                for (int i = 0; i < settingsGroups.Length; i++)
                {
                    settingsProp.GetArrayElementAtIndex(i).objectReferenceValue = settingsGroups[i];
                }
                so.ApplyModifiedProperties();
                Debug.Log($"[SettingsPanelAnimatorEditor] Settings row CanvasGroups assigned: {settingsGroups.Length}");
            }

            // Find action buttons
            Transform actionsContainer = root.transform.Find("ActionButtons");
            if (actionsContainer != null)
            {
                CanvasGroup actionsGroup = GetOrAddCanvasGroup(actionsContainer.gameObject);
                SerializedObject so = new SerializedObject(animator);
                so.FindProperty("actionButtonsGroup").objectReferenceValue = actionsGroup;
                so.ApplyModifiedProperties();
                Debug.Log($"[SettingsPanelAnimatorEditor] Action buttons CanvasGroup assigned: {actionsContainer.name}");
            }

            EditorUtility.SetDirty(animator);
            Debug.Log("[SettingsPanelAnimatorEditor] Auto-setup complete!");
        }

        private void ClearCanvasGroups()
        {
            SettingsPanelAnimator animator = (SettingsPanelAnimator)target;

            Undo.RecordObject(animator, "Clear CanvasGroups");

            SerializedObject so = new SerializedObject(animator);
            so.FindProperty("headerGroup").objectReferenceValue = null;
            so.FindProperty("presetButtonGroups").ClearArray();
            so.FindProperty("settingsRowGroups").ClearArray();
            so.FindProperty("actionButtonsGroup").objectReferenceValue = null;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(animator);
            Debug.Log("[SettingsPanelAnimatorEditor] CanvasGroups cleared.");
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(go);
            }
            return group;
        }
    }
}
#endif
