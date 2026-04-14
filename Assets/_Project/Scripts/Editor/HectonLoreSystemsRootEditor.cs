// ============================================================================
// HECTON-8 - HectonLoreSystemsRootEditor.cs
// Custom inspector for HectonLoreSystemsRoot.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [CustomEditor(typeof(HectonLoreSystemsRoot))]
    public sealed class HectonLoreSystemsRootEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            HectonLoreSystemsRoot root = (HectonLoreSystemsRoot)target;
            int found = root.GetFoundSystemCount();
            string missing = root.GetMissingSystemsSummary();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            MessageType statusType = found == HectonLoreSystemsRoot.ExpectedSystemCount
                ? MessageType.Info
                : MessageType.Warning;

            EditorGUILayout.HelpBox(
                $"Lore bootstrap status: {found}/{HectonLoreSystemsRoot.ExpectedSystemCount} systems present.\nMissing: {missing}",
                statusType);

            using (new EditorGUI.DisabledScope(root == null))
            {
                if (GUILayout.Button("Setup All Systems", GUILayout.Height(30)))
                {
                    Undo.RecordObject(root.gameObject, "Setup Lore Systems");
                    root.SetupAllSystems();
                    EditorUtility.SetDirty(root);
                }

                if (GUILayout.Button("Validate Systems", GUILayout.Height(24)))
                {
                    root.ValidateSystems();
                    EditorUtility.SetDirty(root);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "This inspector only reconciles child systems and reports missing ones.\n" +
                "It does not author content or patch the live world scene.",
                MessageType.None);
        }
    }
}
#endif
