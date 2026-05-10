// ============================================================================
// HECTON-8 — NarrativeDiscoveryAutoFillEditor.cs
// Editor utilita: avtozapolnenie NarrativeDiscovery iz ColonistLoreRegistry.
//
// Ispolzovanie:
//   Vybrat NarrativeDiscovery v inspektore →
//   Nazhat [Auto-Fill from Lore Registry]
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Interaction;
using Hecton8.Narrative;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [CustomEditor(typeof(NarrativeDiscovery))]
    public sealed class NarrativeDiscoveryAutoFillEditor : UnityEditor.Editor
    {
        private ColonistLoreRegistry _registry;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── Lore Auto-Fill ───────────────────────", EditorStyles.boldLabel);

            _registry = (ColonistLoreRegistry)EditorGUILayout.ObjectField(
                "Lore Registry", _registry, typeof(ColonistLoreRegistry), false);

            if (_registry == null)
            {
                EditorGUILayout.HelpBox("Naznachte ColonistLoreRegistry dlya avtozapolneniya.", MessageType.Info);
                return;
            }

            NarrativeDiscovery discovery = (NarrativeDiscovery)target;

            // Chitaem discoveryId cherez SerializedProperty
            SerializedProperty idProp = serializedObject.FindProperty("discoveryId");
            if (idProp == null) return;

            string currentId = idProp.stringValue;

            if (_registry.TryGetEntry(currentId, out LoreEntry entry))
            {
                EditorGUILayout.HelpBox(
                    $"Naydena zapis: {entry.displayName}\n" +
                    $"Tip: {entry.objectType}\n" +
                    $"Lokatsiya: {entry.locationHint}",
                    MessageType.Info);

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f);
                if (GUILayout.Button("▶  Auto-Fill from Registry", GUILayout.Height(28)))
                {
                    AutoFill(discovery, entry);
                }
                GUI.backgroundColor = Color.white;
            }
            else if (!string.IsNullOrEmpty(currentId))
            {
                EditorGUILayout.HelpBox(
                    $"ID '{currentId}' ne nayden v reestre.",
                    MessageType.Warning);
            }
        }

        private void AutoFill(NarrativeDiscovery discovery, LoreEntry entry)
        {
            serializedObject.Update();

            SerializedProperty displayNameProp = serializedObject.FindProperty("displayName");
            SerializedProperty verbProp = serializedObject.FindProperty("interactVerb");
            SerializedProperty linkedAudioLogProp = serializedObject.FindProperty("linkedAudioLog");

            if (displayNameProp != null)
                displayNameProp.stringValue = entry.displayName;

            if (linkedAudioLogProp != null)
                linkedAudioLogProp.objectReferenceValue = entry.linkedAudioLog;

            if (verbProp != null)
            {
                verbProp.stringValue = entry.objectType switch
                {
                    LoreObjectType.DataPad      => "Izuchit KPK",
                    LoreObjectType.AudioLog     => "Vosproizvesti zapis",
                    LoreObjectType.Blueprint    => "Izuchit chertezh",
                    LoreObjectType.PersonalItem => "Osmotret predmet",
                    LoreObjectType.Terminal     => "Otkryt terminal",
                    LoreObjectType.Wreckage     => "Osmotret oblomki",
                    _                           => "Izuchit"
                };
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(discovery);

            Debug.Log($"[NarrativeDiscovery] Auto-filled from registry: {entry.displayName}");
        }
    }
}
#endif
