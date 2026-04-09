// ============================================================================
// HECTON-8 — HectonLoreSystemsRootEditor.cs
// Кастомный инспектор для HectonLoreSystemsRoot.
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

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── Actions ──────────────────────────────", EditorStyles.boldLabel);

            HectonLoreSystemsRoot root = (HectonLoreSystemsRoot)target;

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f);
            if (GUILayout.Button("▶  Setup All Systems", GUILayout.Height(32)))
            {
                root.SetupAllSystems();
                EditorUtility.SetDirty(root.gameObject);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Нажмите 'Setup All Systems' чтобы создать все лорные системы как дочерние объекты.\n" +
                "После создания назначьте ссылки в инспекторе каждой системы.\n\n" +
                "Системы создаются только если их нет — безопасно нажимать повторно.",
                MessageType.Info);
        }
    }
}
#endif
