using UnityEditor;
using UnityEngine;
using H8PrefabRegistry = global::Hecton8.Core.Bridge.H8PrefabRegistry;
using H8PrefabRegistryRuntimeBinder = global::Hecton8.Core.Bridge.H8PrefabRegistryRuntimeBinder;

#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
#endif

namespace Hecton8.Core.Bridge.EditorTools
{
    public sealed class H8PrefabRegistryWindow : EditorWindow
    {
        private H8PrefabRegistry registry;
        private Vector2 scroll;

        [MenuItem("Hecton-8/Bridge/Prefab Registry Binder")]
        public static void Open()
        {
            GetWindow<H8PrefabRegistryWindow>("Prefab Binder");
        }

        private void OnGUI()
        {
            registry = (H8PrefabRegistry)EditorGUILayout.ObjectField("Registry", registry, typeof(H8PrefabRegistry), false);
            DrawDropZone();

            if (registry == null)
                return;

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Hashes"))
                {
                    Undo.RecordObject(registry, "Rebuild Prefab Registry Hashes");
                    registry.RebuildAllHashes();
                    EditorUtility.SetDirty(registry);
                }

                if (GUILayout.Button("Bind Runtime Vault"))
                    H8PrefabRegistryRuntimeBinder.Bind(registry, Hecton8.Core.GlobalRegistry.DataVault);
            }

            EditorGUILayout.LabelField("Entries", registry.EntryCount.ToString());
            EditorGUILayout.LabelField("VRAM Estimate MB", (registry.EstimateTotalVramBytes() >> 20).ToString());

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < registry.EntryCount; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField("Prefab", entry.Prefab, typeof(GameObject), false);
                    EditorGUILayout.LabelField("HashID", entry.HashID.ToString());
                    EditorGUILayout.LabelField("LoreHash", entry.LoreHash.ToString());
                    EditorGUILayout.LabelField("AcousticHash", entry.AcousticSignatureHash.ToString());
                    EditorGUILayout.LabelField("1D LUT Hash", entry.OneDimensionalLutHash.ToString());
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDropZone()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop prefabs here");
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
                return;
            }

            if (evt.type != EventType.DragPerform || registry == null)
                return;

            DragAndDrop.AcceptDrag();
            Undo.RecordObject(registry, "Drop Prefabs Into H8 Registry");
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                GameObject prefab = DragAndDrop.objectReferences[i] as GameObject;
                if (prefab == null)
                    continue;

#if UNITY_ADDRESSABLES_EXIST
                string path = AssetDatabase.GetAssetPath(prefab);
                string guid = AssetDatabase.AssetPathToGUID(path);
                AssetReferenceGameObject reference = string.IsNullOrEmpty(guid) ? null : new AssetReferenceGameObject(guid);
                registry.AddOrUpdateAddressablePrefab(prefab, reference);
#else
                registry.AddOrUpdatePrefab(prefab);
#endif
            }

            EditorUtility.SetDirty(registry);
            evt.Use();
        }
    }

    [CustomEditor(typeof(H8PrefabRegistry))]
    public sealed class H8PrefabRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            H8PrefabRegistry registry = (H8PrefabRegistry)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("VRAM Cost Meter", (registry.EstimateTotalVramBytes() >> 20) + " MB");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Binder"))
                    H8PrefabRegistryWindow.Open();
                if (GUILayout.Button("Bind Runtime Vault"))
                    H8PrefabRegistryRuntimeBinder.Bind(registry, Hecton8.Core.GlobalRegistry.DataVault);
            }
        }
    }
}
