// ============================================================================
// HECTON-8 — NarrativeDiscoveryAutoFillEditor.cs
// Editor утилита: автозаполнение NarrativeDiscovery из ColonistLoreRegistry.
//
// Использование:
//   Выбрать NarrativeDiscovery в инспекторе →
//   Нажать [Auto-Fill from Lore Registry]
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
                EditorGUILayout.HelpBox("Назначьте ColonistLoreRegistry для автозаполнения.", MessageType.Info);
                return;
            }

            NarrativeDiscovery discovery = (NarrativeDiscovery)target;

            // Читаем discoveryId через SerializedProperty
            SerializedProperty idProp = serializedObject.FindProperty("discoveryId");
            if (idProp == null) return;

            string currentId = idProp.stringValue;

            if (_registry.TryGetEntry(currentId, out LoreEntry entry))
            {
                EditorGUILayout.HelpBox(
                    $"Найдена запись: {entry.displayName}\n" +
                    $"Тип: {entry.objectType}\n" +
                    $"Локация: {entry.locationHint}",
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
                    $"ID '{currentId}' не найден в реестре.",
                    MessageType.Warning);
            }
        }

        private void AutoFill(NarrativeDiscovery discovery, LoreEntry entry)
        {
            serializedObject.Update();

            SerializedProperty displayNameProp = serializedObject.FindProperty("displayName");
            SerializedProperty verbProp = serializedObject.FindProperty("interactVerb");

            if (displayNameProp != null)
                displayNameProp.stringValue = entry.displayName;

            if (verbProp != null)
            {
                verbProp.stringValue = entry.objectType switch
                {
                    LoreObjectType.DataPad      => "Изучить КПК",
                    LoreObjectType.AudioLog     => "Воспроизвести запись",
                    LoreObjectType.Blueprint    => "Изучить чертёж",
                    LoreObjectType.PersonalItem => "Осмотреть предмет",
                    LoreObjectType.Terminal     => "Открыть терминал",
                    LoreObjectType.Wreckage     => "Осмотреть обломки",
                    _                           => "Изучить"
                };
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(discovery);

            Debug.Log($"[NarrativeDiscovery] Auto-filled from registry: {entry.displayName}");
        }
    }
}
#endif
