using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
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
        private VisualElement entriesContainer;
        private Label entriesLabel;
        private Label vramLabel;
        private Label validationLabel;

        [MenuItem("Hecton-8/Bridge/Prefab Registry Binder")]
        public static void Open()
        {
            GetWindow<H8PrefabRegistryWindow>("Prefab Binder");
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            ObjectField registryField = new ObjectField("Registry")
            {
                objectType = typeof(H8PrefabRegistry),
                allowSceneObjects = false,
                value = registry
            };
            registryField.RegisterValueChangedCallback(evt =>
            {
                registry = evt.newValue as H8PrefabRegistry;
                RefreshEntries();
            });
            root.Add(registryField);

            VisualElement dropZone = new VisualElement();
            dropZone.style.height = 58f;
            dropZone.style.marginTop = 6f;
            dropZone.style.marginBottom = 6f;
            dropZone.style.borderTopWidth = 1f;
            dropZone.style.borderBottomWidth = 1f;
            dropZone.style.borderLeftWidth = 1f;
            dropZone.style.borderRightWidth = 1f;
            dropZone.style.alignItems = Align.Center;
            dropZone.style.justifyContent = Justify.Center;
            dropZone.Add(new Label("Drop prefabs here"));
            dropZone.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropZone.RegisterCallback<DragPerformEvent>(OnDragPerform);
            root.Add(dropZone);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginBottom = 6f;
            root.Add(buttons);

            Button rebuildButton = new Button(RebuildHashes)
            {
                text = "Rebuild Hashes"
            };
            Button bindButton = new Button(BindRuntimeVault)
            {
                text = "Bind Runtime Vault"
            };
            buttons.Add(rebuildButton);
            buttons.Add(bindButton);

            entriesLabel = new Label();
            vramLabel = new Label();
            validationLabel = new Label();
            root.Add(entriesLabel);
            root.Add(vramLabel);
            root.Add(validationLabel);

            ScrollView scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            entriesContainer = scrollView;
            root.Add(scrollView);
            RefreshEntries();
        }

        private void RefreshEntries()
        {
            if (entriesLabel == null || vramLabel == null || validationLabel == null || entriesContainer == null)
                return;

            entriesContainer.Clear();
            if (registry == null)
            {
                entriesLabel.text = "Entries: 0";
                vramLabel.text = "VRAM Estimate MB: 0";
                validationLabel.text = "Validation: no registry";
                return;
            }

            entriesLabel.text = "Entries: " + registry.EntryCount.ToString(CultureInfo.InvariantCulture);
            vramLabel.text = "VRAM Estimate MB: " + (registry.EstimateTotalVramBytes() >> 20).ToString(CultureInfo.InvariantCulture);
            validationLabel.text = BuildValidationSummary(registry);
            for (int i = 0; i < registry.EntryCount; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null)
                {
                    Box nullBox = new Box();
                    nullBox.style.marginTop = 4f;
                    nullBox.style.marginBottom = 4f;
                    Label nullLabel = new Label("Null entry slot: " + i.ToString(CultureInfo.InvariantCulture));
                    nullLabel.style.color = Color.yellow;
                    nullBox.Add(nullLabel);
                    entriesContainer.Add(nullBox);
                    continue;
                }

                Box box = new Box();
                box.style.marginTop = 4f;
                box.style.marginBottom = 4f;
                ObjectField prefabField = new ObjectField("Prefab")
                {
                    objectType = typeof(GameObject),
                    allowSceneObjects = false,
                    value = entry.Prefab
                };
                prefabField.SetEnabled(false);
                box.Add(prefabField);
                box.Add(new Label("HashID: " + entry.HashID.ToString(CultureInfo.InvariantCulture)));
                box.Add(new Label("LoreHash: " + entry.LoreHash.ToString(CultureInfo.InvariantCulture)));
                box.Add(new Label("AcousticHash: " + entry.AcousticSignatureHash.ToString(CultureInfo.InvariantCulture)));
                box.Add(new Label("1D LUT Hash: " + entry.OneDimensionalLutHash.ToString(CultureInfo.InvariantCulture)));
                entriesContainer.Add(box);
            }
        }

        private static string BuildValidationSummary(H8PrefabRegistry registry)
        {
            if (registry == null)
                return "Validation: no registry";

            string runtimeCount = registry.ValidationRuntimeBindableCount.ToString(CultureInfo.InvariantCulture);
            if (!registry.HasValidationErrors)
                return "Validation: OK, runtime bindable " + runtimeCount;

            string nullRows = registry.ValidationNullEntryCount.ToString(CultureInfo.InvariantCulture);
            string firstNull = registry.ValidationFirstNullEntryIndex.ToString(CultureInfo.InvariantCulture);
            string duplicateRows = registry.ValidationDuplicateHashCount.ToString(CultureInfo.InvariantCulture);
            string firstDuplicate = registry.ValidationFirstDuplicateHashIndex.ToString(CultureInfo.InvariantCulture);
            return "Validation: ERRORS, runtime bindable " + runtimeCount +
                   ", null rows " + nullRows +
                   " first " + firstNull +
                   ", duplicate hashes " + duplicateRows +
                   " first " + firstDuplicate;
        }

        private void RebuildHashes()
        {
            if (registry == null)
                return;

            Undo.RecordObject(registry, "Rebuild Prefab Registry Hashes");
            registry.RebuildAllHashes();
            EditorUtility.SetDirty(registry);
            RefreshEntries();
        }

        private void BindRuntimeVault()
        {
            if (registry == null)
                return;

            if (!H8PrefabRegistryRuntimeBinder.Bind(
                registry,
                Hecton8.Core.GlobalRegistry.DataVault,
                H8PrefabRegistryRuntimeBinder.ResolveRuntimeRegistryForBinding()))
            {
                Debug.LogError("[H8Bridge] Prefab registry bind failed. Fix duplicate prefab hashes or wait for DataVault allocation fences to clear.");
            }
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (registry == null || !HasDraggedPrefab())
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (registry == null || !HasDraggedPrefab())
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
            RefreshEntries();
            evt.StopPropagation();
        }

        private static bool HasDraggedPrefab()
        {
            Object[] references = DragAndDrop.objectReferences;
            for (int i = 0; i < references.Length; i++)
            {
                if (references[i] is GameObject)
                    return true;
            }

            return false;
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
            EditorGUILayout.LabelField("Validation", registry.HasValidationErrors ? "ERRORS" : "OK");
            EditorGUILayout.LabelField("Runtime Bindable", registry.ValidationRuntimeBindableCount.ToString(CultureInfo.InvariantCulture));
            if (registry.HasValidationErrors)
            {
                EditorGUILayout.LabelField("Null Rows", registry.ValidationNullEntryCount + " first " + registry.ValidationFirstNullEntryIndex);
                EditorGUILayout.LabelField("Duplicate Hash Rows", registry.ValidationDuplicateHashCount + " first " + registry.ValidationFirstDuplicateHashIndex);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Binder"))
                    H8PrefabRegistryWindow.Open();
                if (GUILayout.Button("Bind Runtime Vault"))
                    TryBindRuntimeVault(registry);
            }
        }

        private static void TryBindRuntimeVault(H8PrefabRegistry registry)
        {
            if (registry == null)
                return;

            if (!H8PrefabRegistryRuntimeBinder.Bind(
                registry,
                Hecton8.Core.GlobalRegistry.DataVault,
                H8PrefabRegistryRuntimeBinder.ResolveRuntimeRegistryForBinding()))
            {
                Debug.LogError("[H8Bridge] Prefab registry bind failed. Fix duplicate prefab hashes or wait for DataVault allocation fences to clear.");
            }
        }
    }
}
