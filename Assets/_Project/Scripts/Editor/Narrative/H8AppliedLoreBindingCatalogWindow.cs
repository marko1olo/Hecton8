#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Hecton8.Data;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class H8AppliedLoreBindingCatalogWindow : EditorWindow
    {
        private const string MenuPath = "Hecton8/Lore/Applied Lore Binding Catalog";
        private const string ValidateMenuPath = "Hecton8/Lore/Validate Applied Lore Bindings";
        private const string ApplyBacklogMenuPath = "Hecton8/Lore/Apply Applied Lore Target Backlog To Prefabs";
        private const string CreateTerminalAnchorMenuPath = "Hecton8/Lore/Create Applied Lore Terminal Anchor Prefab";
        private const string GenerateTerminalPrefabsMenuPath = "Hecton8/Lore/Generate Applied Lore Terminal Policy Prefabs";
        private const string ApplyScenePlacementMenuPath = "Hecton8/Lore/Apply Applied Lore Scene Placement Plan";
        private const string BindingMapFolder = "Docs/Lore/AppliedContent/binding_maps";
        private const string ManualBindingPolicyPath = "Docs/Lore/AppliedContent/binding_maps/RS001_RS010_manual_binding_policy.csv";
        private const string ScenePlacementPlanPath = "Docs/Lore/AppliedContent/binding_maps/RS001_RS010_scene_placement_plan.csv";
        private const string PacketCsvPath = "Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv";
        private const string StaticDataPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const string TerminalAnchorPrefabPath = "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab";
        private const string TerminalPolicyPrefabFolder = "Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals";
        private const string TerminalAnchorMaterialPath = "Assets/_Project/Art/Materials/MAT_Diegetic_HUD_V4_Projection.mat";
        private const string TerminalAnchorMeshPath = "Assets/_Project/Art/Meshes/M_Diegetic_HUD_V4_CurvedPanel.asset";
        private const string TerminalOsArrayPanelMaterialPath = "Assets/_Project/Art/Materials/MAT_AppliedLore_TerminalOS_ArrayPanel.mat";
        private const string TerminalOsArrayPanelShaderPath = "Assets/_Project/Art/Shaders/Hecton_TerminalTextureArrayPanel.shader";
        private const string TerminalOsBlitComputePath = "Assets/_Project/Art/Shaders/TerminalBlit.compute";
        private const string TerminalOsFontSdfPrimaryPath = "Assets/_Project/Art/Materials/Fonts/tekst_SDF.asset";
        private const string TerminalOsFontSdfFallbackPath = "Assets/_Project/Art/Materials/Fonts/NotoSans-Regular SDF.asset";
        private const string DefaultScenePlacementRootName = "__APPLIED_LORE_SCENE_PLACEMENT";
        private const string TerminalOsRuntimeObjectName = "__APPLIED_LORE_TERMINAL_OS_RUNTIME";
        private const int SchemaVersion = 1;

        private readonly List<BindingRow> _bindings = new List<BindingRow>(128);
        private readonly List<BindingRow> _filtered = new List<BindingRow>(128);
        private readonly HashSet<uint> _knownPacketHashes = new HashSet<uint>();

        private int _targetBacklogRows;
        private int _manualPolicyRows;
        private TextField _filterField;
        private Label _statusLabel;
        private Label _summaryLabel;
        private Label _detailLabel;
        private ScrollView _listView;
        private BindingRow _selected;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            H8AppliedLoreBindingCatalogWindow window = GetWindow<H8AppliedLoreBindingCatalogWindow>();
            window.titleContent = new GUIContent("Applied Lore Bindings");
            window.minSize = new Vector2(920f, 560f);
            window.ReloadCatalog();
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateBindingsMenu()
        {
            BindingValidationReport report = ValidateProjectBindings();
            Debug.Log(report.ToLogLine());
        }

        [MenuItem(ApplyBacklogMenuPath)]
        public static void ApplyTargetBacklogToPrefabsMenu()
        {
            PrefabBacklogApplyReport report = ApplyTargetBacklogToPrefabs();
            Debug.Log(report.ToLogLine());
        }

        [MenuItem(CreateTerminalAnchorMenuPath)]
        public static void CreateAppliedLoreTerminalAnchorPrefabMenu()
        {
            TerminalAnchorReport report = CreateAppliedLoreTerminalAnchorPrefab();
            Debug.Log(report.ToLogLine());
        }

        [MenuItem(GenerateTerminalPrefabsMenuPath)]
        public static void GenerateAppliedLoreTerminalPolicyPrefabsMenu()
        {
            TerminalPolicyPrefabReport report = GenerateAppliedLoreTerminalPolicyPrefabs();
            Debug.Log(report.ToLogLine());
        }

        [MenuItem(ApplyScenePlacementMenuPath)]
        public static void ApplyScenePlacementPlanMenu()
        {
            ScenePlacementReport report = ApplyScenePlacementPlanToOpenScene();
            Debug.Log(report.ToLogLine());
        }

        public static void ApplyScenePlacementPlanFromCommandLine()
        {
            try
            {
                bool scenesOpened = TryOpenScenePlacementPlanScenesForCommandLine();
                ScenePlacementReport report = scenesOpened
                    ? ApplyScenePlacementPlanToOpenScene()
                    : new ScenePlacementReport { PreflightAborted = true };
                Debug.Log(report.ToLogLine());

                bool success = scenesOpened && !HasScenePlacementApplyFailures(report);
                if (!success)
                    Debug.LogError("[AppliedLoreScenePlacement] Batch scene placement failed.");

                if (Application.isBatchMode)
                    EditorApplication.Exit(success ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogError("[AppliedLoreScenePlacement] Batch scene placement threw: " + exception.Message);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            Label title = new Label("Applied Lore Binding Catalog");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            rootVisualElement.Add(title);

            _summaryLabel = new Label();
            _summaryLabel.style.marginTop = 4f;
            rootVisualElement.Add(_summaryLabel);

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginTop = 8f;
            toolbar.Add(MakeButton("Reload", ReloadCatalog));
            toolbar.Add(MakeButton("Assign Existing", AssignSelectedToExistingComponent));
            toolbar.Add(MakeButton("Add + Assign", AddComponentAndAssignSelected));
            toolbar.Add(MakeButton("Apply Prefab Backlog", ApplyBacklogAndShow));
            toolbar.Add(MakeButton("Create Terminal Anchor", CreateTerminalAnchorAndShow));
            toolbar.Add(MakeButton("Generate Terminal Prefabs", GenerateTerminalPrefabsAndShow));
            toolbar.Add(MakeButton("Apply Scene Plan", ApplyScenePlacementAndShow));
            toolbar.Add(MakeButton("Validate Scene/Prefabs", ValidateAndShow));
            toolbar.Add(MakeButton("Copy Hash", CopySelectedHash));
            rootVisualElement.Add(toolbar);

            _filterField = new TextField("Filter");
            _filterField.style.marginTop = 6f;
            _filterField.RegisterValueChangedCallback(_ => RebuildFilteredList());
            rootVisualElement.Add(_filterField);

            _statusLabel = new Label("Idle");
            _statusLabel.style.marginTop = 6f;
            rootVisualElement.Add(_statusLabel);

            _detailLabel = new Label("Select a packet row.");
            _detailLabel.style.marginTop = 6f;
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_detailLabel);

            _listView = new ScrollView();
            _listView.style.flexGrow = 1f;
            _listView.style.marginTop = 8f;
            rootVisualElement.Add(_listView);

            ReloadCatalog();
        }

        private static Button MakeButton(string text, Action action)
        {
            Button button = new Button(action) { text = text };
            button.style.marginRight = 6f;
            return button;
        }

        private void ReloadCatalog()
        {
            _bindings.Clear();
            _filtered.Clear();
            _knownPacketHashes.Clear();
            _targetBacklogRows = 0;
            _manualPolicyRows = 0;

            LoadKnownPacketHashes(_knownPacketHashes);
            LoadBindingRows(_bindings);
            Dictionary<string, TargetBacklogRow> targetBacklog = new Dictionary<string, TargetBacklogRow>(64, StringComparer.Ordinal);
            _targetBacklogRows = LoadTargetBacklogRows(targetBacklog);
            MergeTargetBacklog(_bindings, targetBacklog);
            Dictionary<string, ManualBindingPolicyRow> manualPolicies = new Dictionary<string, ManualBindingPolicyRow>(64, StringComparer.Ordinal);
            _manualPolicyRows = LoadManualBindingPolicies(manualPolicies);
            MergeManualBindingPolicies(_bindings, manualPolicies);
            _bindings.Sort(CompareBindingRows);
            RebuildFilteredList();

            if (_summaryLabel != null)
            {
                _summaryLabel.text =
                    "Source: " + BindingMapFolder +
                    " | Packet CSV: " + PacketCsvPath +
                    " | Binary: " + StaticDataPath +
                    " | Schema: " + SchemaVersion +
                    " | Rows: " + _bindings.Count +
                    " | Target backlog: " + _targetBacklogRows +
                    " | Manual policy: " + _manualPolicyRows +
                    " | Known packet hashes: " + _knownPacketHashes.Count;
            }

            SetStatus("Catalog loaded.");
        }

        private void RebuildFilteredList()
        {
            _filtered.Clear();
            string filter = _filterField != null ? _filterField.value : string.Empty;
            bool hasFilter = !string.IsNullOrWhiteSpace(filter);

            for (int i = 0; i < _bindings.Count; i++)
            {
                BindingRow row = _bindings[i];
                if (!hasFilter || row.Contains(filter))
                    _filtered.Add(row);
            }

            RebuildVisualRows();
        }

        private void RebuildVisualRows()
        {
            if (_listView == null)
                return;

            _listView.Clear();
            for (int i = 0; i < _filtered.Count; i++)
            {
                BindingRow row = _filtered[i];
                Button rowButton = new Button(() => SelectRow(row))
                {
                    text = row.ToListText()
                };
                rowButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                rowButton.style.marginBottom = 2f;
                _listView.Add(rowButton);
            }
        }

        private void SelectRow(BindingRow row)
        {
            _selected = row;
            _detailLabel.text =
                row.PacketId + " / " + row.PacketHashHex + " / " + row.PacketHashUInt +
                "\nPrimary: " + row.PrimaryComponent + "." + row.PrimaryField +
                "\nSecondary: " + row.SecondaryComponent + "." + row.SecondaryField +
                "\nTarget hint: " + row.SuggestedWorldTarget +
                "\nPrimary target candidates: " + row.PrimaryTargetCandidates +
                "\nSecondary target candidates: " + row.SecondaryTargetCandidates +
                "\nUnity-safe action: " + row.UnitySafeAction +
                "\nManual policy: " + row.ManualPolicy +
                "\nRequired anchor: " + row.RequiredAnchorType +
                "\nTemplate prefab: " + row.ApprovedTemplatePrefab +
                "\nDiscovery id: " + row.DiscoveryId +
                "\nPlacement rule: " + row.PlacementRule +
                "\nUnlock: " + row.UnlockMoment +
                "\nNotes: " + row.Notes +
                "\nPolicy reason: " + row.ManualReason;
        }

        private void AssignSelectedToExistingComponent()
        {
            AssignSelected(addComponentIfMissing: false);
        }

        private void AddComponentAndAssignSelected()
        {
            AssignSelected(addComponentIfMissing: true);
        }

        private void AssignSelected(bool addComponentIfMissing)
        {
            if (!_selected.IsValid)
            {
                SetStatus("No packet row selected.");
                return;
            }

            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                SetStatus("No active GameObject selected.");
                return;
            }

            if (!_knownPacketHashes.Contains(_selected.PacketHashUInt))
            {
                SetStatus("Selected packet hash is not present in source CSV.");
                return;
            }

            if (TryAssignPrimaryOrSecondary(target, _selected, addComponentIfMissing, out string message))
            {
                SetStatus(message);
                return;
            }

            SetStatus(message);
        }

        private void CopySelectedHash()
        {
            if (!_selected.IsValid)
            {
                SetStatus("No packet row selected.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = _selected.PacketHashUInt.ToString(CultureInfo.InvariantCulture);
            SetStatus("Copied decimal packet hash: " + _selected.PacketHashUInt);
        }

        private void ValidateAndShow()
        {
            BindingValidationReport report = ValidateProjectBindings();
            SetStatus(report.ToLogLine());
            EditorUtility.DisplayDialog("Applied Lore Bindings", report.ToDialogText(), "OK");
        }

        private void ApplyBacklogAndShow()
        {
            PrefabBacklogApplyReport report = ApplyTargetBacklogToPrefabs();
            SetStatus(report.ToLogLine());
            EditorUtility.DisplayDialog("Applied Lore Prefab Backlog", report.ToDialogText(), "OK");
            ReloadCatalog();
        }

        private void CreateTerminalAnchorAndShow()
        {
            TerminalAnchorReport report = CreateAppliedLoreTerminalAnchorPrefab();
            SetStatus(report.ToLogLine());
            EditorUtility.DisplayDialog("Applied Lore Terminal Anchor", report.ToDialogText(), "OK");
            ReloadCatalog();
        }

        private void GenerateTerminalPrefabsAndShow()
        {
            TerminalPolicyPrefabReport report = GenerateAppliedLoreTerminalPolicyPrefabs();
            SetStatus(report.ToLogLine());
            EditorUtility.DisplayDialog("Applied Lore Terminal Policy Prefabs", report.ToDialogText(), "OK");
            ReloadCatalog();
        }

        private void ApplyScenePlacementAndShow()
        {
            ScenePlacementReport report = ApplyScenePlacementPlanToOpenScene();
            SetStatus(report.ToLogLine());
            EditorUtility.DisplayDialog("Applied Lore Scene Placement", report.ToDialogText(), "OK");
            ReloadCatalog();
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.text = text;
        }

        private static bool TryAssignPrimaryOrSecondary(
            GameObject target,
            BindingRow row,
            bool addComponentIfMissing,
            out string message)
        {
            if (TryAssignComponentField(target, row.PrimaryComponent, row.PrimaryField, row.PacketHashUInt, addComponentIfMissing, out message))
                return true;

            if (!string.IsNullOrWhiteSpace(row.SecondaryComponent) &&
                !string.IsNullOrWhiteSpace(row.SecondaryField) &&
                TryAssignComponentField(target, row.SecondaryComponent, row.SecondaryField, row.PacketHashUInt, addComponentIfMissing, out message))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(message))
                message = "No supported AppliedLore component field found on selected object.";
            return false;
        }

        private static bool TryAssignComponentField(
            GameObject target,
            string componentName,
            string fieldName,
            uint packetHash,
            bool addComponentIfMissing,
            out string message)
        {
            if (componentName == nameof(NarrativeDiscovery))
                return TryAssignSerializedField<NarrativeDiscovery>(target, fieldName, packetHash, addComponentIfMissing, out message);

            if (componentName == nameof(ScannableFragment))
                return TryAssignSerializedField<ScannableFragment>(target, fieldName, packetHash, addComponentIfMissing, out message);

            if (componentName == nameof(MessageTerminal))
            {
                if (addComponentIfMissing && target.GetComponent<MessageTerminal>() == null && target.GetComponent<Renderer>() == null)
                {
                    message = "MessageTerminal requires a concrete Renderer on the selected object.";
                    return false;
                }

                return TryAssignSerializedField<MessageTerminal>(target, fieldName, packetHash, addComponentIfMissing, out message);
            }

            message = "Unsupported authoring component: " + componentName;
            return false;
        }

        private static bool TryAssignSerializedField<T>(
            GameObject target,
            string fieldName,
            uint packetHash,
            bool addComponentIfMissing,
            out string message)
            where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null && addComponentIfMissing)
            {
                component = Undo.AddComponent<T>(target);
            }

            if (component == null)
            {
                message = "Missing component " + typeof(T).Name + " on " + target.name + ".";
                return false;
            }

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                message = typeof(T).Name + " has no serialized field " + fieldName + ".";
                return false;
            }

            if (property.propertyType != SerializedPropertyType.Integer)
            {
                message = typeof(T).Name + "." + fieldName + " is not an integer serialized field.";
                return false;
            }

            Undo.RecordObject(component, "Assign Applied Lore Packet");
            property.longValue = packetHash;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);

            Scene scene = component.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);

            message = "Assigned " + packetHash + " to " + typeof(T).Name + "." + fieldName + " on " + target.name + ".";
            return true;
        }

        private static BindingValidationReport ValidateProjectBindings()
        {
            HashSet<uint> known = new HashSet<uint>();
            LoadKnownPacketHashes(known);

            BindingValidationReport report = new BindingValidationReport();
            report.KnownPacketHashes = known.Count;
            ScanLoadedSceneComponents<NarrativeDiscovery>(known, ref report, "appliedLorePacketHash");
            ScanLoadedSceneComponents<MessageTerminal>(known, ref report, "appliedLorePacketHash");
            ScanLoadedSceneComponents<ScannableFragment>(
                known,
                ref report,
                "appliedLoreQuarterPacketHash",
                "appliedLoreHalfPacketHash",
                "appliedLoreFinalPacketHash");

            List<string> prefabPaths = new List<string>(128);
            CollectPrefabCandidatePaths(prefabPaths);
            report.PrefabsScanned = prefabPaths.Count;
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ScanPrefabComponents<NarrativeDiscovery>(prefab, known, ref report, "appliedLorePacketHash");
                ScanPrefabComponents<MessageTerminal>(prefab, known, ref report, "appliedLorePacketHash");
                ScanPrefabComponents<ScannableFragment>(
                    prefab,
                    known,
                    ref report,
                    "appliedLoreQuarterPacketHash",
                    "appliedLoreHalfPacketHash",
                    "appliedLoreFinalPacketHash");
            }

            return report;
        }

        private static void CollectPrefabCandidatePaths(List<string> destination)
        {
            Dictionary<string, TargetBacklogRow> targetBacklog = new Dictionary<string, TargetBacklogRow>(64, StringComparer.Ordinal);
            LoadTargetBacklogRows(targetBacklog);

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TargetBacklogRow row in targetBacklog.Values)
            {
                CollectPrefabCandidatePaths(row.PrimaryTargetCandidates, seen, destination);
                CollectPrefabCandidatePaths(row.SecondaryTargetCandidates, seen, destination);
            }

            CollectGeneratedTerminalPolicyPrefabPaths(seen, destination);
        }

        private static void CollectGeneratedTerminalPolicyPrefabPaths(HashSet<string> seen, List<string> destination)
        {
            string absoluteFolder = Path.Combine(Directory.GetCurrentDirectory(), TerminalPolicyPrefabFolder);
            if (!Directory.Exists(absoluteFolder))
                return;

            string[] paths = Directory.GetFiles(absoluteFolder, "PFB_AppliedLore_Terminal_*.prefab", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < paths.Length; i++)
            {
                string relativePath = paths[i].Replace('\\', '/');
                if (relativePath.StartsWith(Directory.GetCurrentDirectory().Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase))
                    relativePath = relativePath.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');

                if (seen.Add(relativePath))
                    destination.Add(relativePath);
            }
        }

        private static void CollectPrefabCandidatePaths(
            string candidates,
            HashSet<string> seen,
            List<string> destination)
        {
            if (string.IsNullOrWhiteSpace(candidates))
                return;

            string[] parts = candidates.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string candidate = parts[i].Trim();
                if (!candidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), candidate);
                if (!File.Exists(absolutePath) || !seen.Add(candidate))
                    continue;

                destination.Add(candidate);
            }
        }

        private static PrefabBacklogApplyReport ApplyTargetBacklogToPrefabs()
        {
            HashSet<uint> knownPacketHashes = new HashSet<uint>();
            LoadKnownPacketHashes(knownPacketHashes);

            List<BindingRow> bindings = new List<BindingRow>(128);
            LoadBindingRows(bindings);

            Dictionary<string, TargetBacklogRow> targetBacklog = new Dictionary<string, TargetBacklogRow>(64, StringComparer.Ordinal);
            int backlogRows = LoadTargetBacklogRows(targetBacklog);
            MergeTargetBacklog(bindings, targetBacklog);

            PrefabBacklogApplyReport report = new PrefabBacklogApplyReport
            {
                BacklogRows = backlogRows
            };
            List<string> prefabCandidates = new List<string>(8);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    BindingRow row = bindings[i];
                    if (string.IsNullOrWhiteSpace(row.UnitySafeAction))
                        continue;

                    report.RowsConsidered++;
                    if (!knownPacketHashes.Contains(row.PacketHashUInt))
                    {
                        report.UnknownHashes++;
                        continue;
                    }

                    prefabCandidates.Clear();
                    CollectExistingPrefabCandidates(row, prefabCandidates);
                    if (prefabCandidates.Count == 0)
                    {
                        report.SkippedNoPrefab++;
                        continue;
                    }

                    bool resolved = false;
                    string lastMessage = string.Empty;
                    for (int candidateIndex = 0; candidateIndex < prefabCandidates.Count; candidateIndex++)
                    {
                        string prefabPath = prefabCandidates[candidateIndex];
                        if (TryApplyBindingToPrefab(row, prefabPath, out bool changed, out string message))
                        {
                            report.PrefabsOpened++;
                            if (changed)
                            {
                                report.PrefabsChanged++;
                                report.BindingsApplied++;
                            }
                            else
                            {
                                report.AlreadyBound++;
                            }

                            resolved = true;
                            break;
                        }

                        lastMessage = message;
                    }

                    if (!resolved)
                    {
                        report.SkippedUnsupported++;
                        Debug.LogWarning("[AppliedLoreBindings] " + lastMessage);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return report;
        }

        private static TerminalAnchorReport CreateAppliedLoreTerminalAnchorPrefab()
        {
            TerminalAnchorReport report = new TerminalAnchorReport
            {
                PrefabPath = TerminalAnchorPrefabPath
            };

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerminalAnchorMeshPath);
            if (mesh == null)
            {
                Debug.LogError(
                    "[AppliedLoreTerminalAnchor] Missing required curved panel mesh. Refusing to save primitive fallback: " +
                    TerminalAnchorMeshPath);
                return report;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TerminalAnchorMaterialPath);
            if (material == null)
            {
                Debug.LogError(
                    "[AppliedLoreTerminalAnchor] Missing required diegetic HUD material. Refusing to save primitive fallback: " +
                    TerminalAnchorMaterialPath);
                return report;
            }

            string directory = Path.GetDirectoryName(TerminalAnchorPrefabPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            GameObject root = null;
            try
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                root.name = "PFB_AppliedLore_MessageTerminalAnchor";
                root.transform.localScale = new Vector3(1.2f, 0.08f, 0.72f);

                MeshFilter meshFilter = root.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = mesh;
                    report.UsedMesh = true;
                }

                Renderer renderer = root.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    report.UsedMaterial = true;
                }

                if (!report.UsedMesh || !report.UsedMaterial)
                {
                    Debug.LogError("[AppliedLoreTerminalAnchor] Primitive root could not bind required mesh/material. Save blocked.");
                    return report;
                }

                MessageTerminal terminal = root.AddComponent<MessageTerminal>();
                SerializedObject serialized = new SerializedObject(terminal);
                SerializedProperty packetHash = serialized.FindProperty("appliedLorePacketHash");
                if (packetHash != null && packetHash.propertyType == SerializedPropertyType.Integer)
                    packetHash.longValue = 0L;

                SerializedProperty statusRenderer = serialized.FindProperty("statusLightRenderer");
                if (statusRenderer != null && statusRenderer.propertyType == SerializedPropertyType.ObjectReference)
                    statusRenderer.objectReferenceValue = renderer;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TerminalAnchorPrefabPath, out bool success);
                report.CreatedOrUpdated = success;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            return report;
        }

        private static TerminalPolicyPrefabReport GenerateAppliedLoreTerminalPolicyPrefabs()
        {
            TerminalPolicyPrefabReport report = new TerminalPolicyPrefabReport();
            string anchorAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), TerminalAnchorPrefabPath);
            if (!File.Exists(anchorAbsolutePath))
            {
                report.MissingAnchor = true;
                return report;
            }

            List<BindingRow> bindings = new List<BindingRow>(128);
            LoadBindingRows(bindings);

            Dictionary<string, TargetBacklogRow> targetBacklog = new Dictionary<string, TargetBacklogRow>(64, StringComparer.Ordinal);
            LoadTargetBacklogRows(targetBacklog);
            MergeTargetBacklog(bindings, targetBacklog);

            Dictionary<string, ManualBindingPolicyRow> manualPolicies = new Dictionary<string, ManualBindingPolicyRow>(64, StringComparer.Ordinal);
            LoadManualBindingPolicies(manualPolicies);
            MergeManualBindingPolicies(bindings, manualPolicies);

            Directory.CreateDirectory(TerminalPolicyPrefabFolder);
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    BindingRow row = bindings[i];
                    if (row.ManualPolicy != "terminal_anchor_required" || row.PrimaryComponent != nameof(MessageTerminal))
                        continue;

                    report.PolicyRows++;
                    string outputPath = TerminalPolicyPrefabFolder + "/PFB_AppliedLore_Terminal_" + row.PacketId + ".prefab";
                    string outputAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), outputPath);
                    string sourcePath = File.Exists(outputAbsolutePath) ? outputPath : TerminalAnchorPrefabPath;
                    if (TrySaveTerminalPolicyPrefab(row, sourcePath, outputPath, out bool changed, out string message))
                    {
                        if (changed)
                            report.GeneratedOrUpdated++;
                        else
                            report.AlreadyCurrent++;
                    }
                    else
                    {
                        report.Failed++;
                        Debug.LogWarning("[AppliedLoreTerminalPolicyPrefabs] " + message);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return report;
        }

        private static bool TrySaveTerminalPolicyPrefab(
            BindingRow row,
            string sourcePath,
            string outputPath,
            out bool changed,
            out string message)
        {
            changed = false;
            message = string.Empty;
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(sourcePath);
                if (prefabRoot == null)
                {
                    message = "Failed to load terminal prefab source: " + sourcePath;
                    return false;
                }

                string expectedName = Path.GetFileNameWithoutExtension(outputPath);
                if (prefabRoot.name != expectedName)
                {
                    prefabRoot.name = expectedName;
                    changed = true;
                }

                MessageTerminal terminal = prefabRoot.GetComponentInChildren<MessageTerminal>(true);
                if (terminal == null)
                {
                    message = "Terminal prefab source has no MessageTerminal: " + sourcePath;
                    return false;
                }

                SerializedObject serialized = new SerializedObject(terminal);
                SerializedProperty property = serialized.FindProperty(row.PrimaryField);
                if (property == null || property.propertyType != SerializedPropertyType.Integer)
                {
                    message = "MessageTerminal missing integer field " + row.PrimaryField + ".";
                    return false;
                }

                if (unchecked((uint)property.longValue) != row.PacketHashUInt)
                {
                    property.longValue = row.PacketHashUInt;
                    changed = true;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), outputPath)))
                    changed = true;

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, outputPath);

                return true;
            }
            catch (Exception exception)
            {
                message = "Terminal prefab generation failed for " + row.PacketId + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryOpenScenePlacementPlanScenesForCommandLine()
        {
            List<ScenePlacementRow> rows = new List<ScenePlacementRow>(64);
            LoadScenePlacementRows(rows);
            HashSet<string> openedScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool openedAny = false;

            for (int i = 0; i < rows.Count; i++)
            {
                string scenePath = rows[i].ScenePath;
                if (string.IsNullOrWhiteSpace(scenePath) || !openedScenePaths.Add(scenePath))
                    continue;

                string absoluteScenePath = Path.Combine(Directory.GetCurrentDirectory(), scenePath);
                if (!File.Exists(absoluteScenePath))
                {
                    Debug.LogError("[AppliedLoreScenePlacement] Scene placement plan references missing scene: " + scenePath);
                    return false;
                }

                if (FindLoadedScene(scenePath, out _))
                {
                    openedAny = true;
                    continue;
                }

                OpenSceneMode mode = openedAny ? OpenSceneMode.Additive : OpenSceneMode.Single;
                Scene scene = EditorSceneManager.OpenScene(scenePath, mode);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Debug.LogError("[AppliedLoreScenePlacement] Failed to open scene placement scene: " + scenePath);
                    return false;
                }

                openedAny = true;
            }

            if (!openedAny)
                Debug.LogError("[AppliedLoreScenePlacement] Scene placement plan has no usable scene paths.");

            return openedAny;
        }

        private static bool HasScenePlacementApplyFailures(ScenePlacementReport report)
        {
            return report.PlanRows <= 0 ||
                   report.PreflightAborted ||
                   report.InvalidRows > 0 ||
                   report.DuplicateSceneOwners > 0 ||
                   report.DuplicateDiscoveryIds > 0 ||
                   report.UnknownHashes > 0 ||
                   report.SceneNotLoaded > 0 ||
                   report.MissingPrefabs > 0 ||
                   report.Conflicts > 0 ||
                   report.UnsupportedRows > 0 ||
                   report.SaveFailures > 0 ||
                   report.TerminalOsRuntimeMissingRenderers > 0 ||
                   report.TerminalOsRuntimeDuplicatePreviewIndices > 0;
        }

        private static ScenePlacementReport ApplyScenePlacementPlanToOpenScene()
        {
            HashSet<uint> knownPacketHashes = new HashSet<uint>();
            LoadKnownPacketHashes(knownPacketHashes);

            List<ScenePlacementRow> rows = new List<ScenePlacementRow>(64);
            LoadScenePlacementRows(rows);
            ScenePlacementReport report = new ScenePlacementReport
            {
                PlanRows = rows.Count
            };
            MarkDuplicateScenePlacementRows(rows, ref report);
            AssignTerminalPreviewIndices(rows);
            if (!TryValidateScenePlacementRowsBeforeMutation(rows, knownPacketHashes, ref report))
            {
                report.PreflightAborted = true;
                return report;
            }

            HashSet<string> dirtyScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows.Count; i++)
            {
                ScenePlacementRow row = rows[i];
                if (!row.IsValid)
                {
                    report.InvalidRows++;
                    continue;
                }

                report.RowsConsidered++;
                if (!knownPacketHashes.Contains(row.PacketHashUInt))
                {
                    report.UnknownHashes++;
                    continue;
                }

                if (!FindLoadedScene(row.ScenePath, out Scene scene))
                {
                    report.SceneNotLoaded++;
                    continue;
                }

                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.SourcePrefab);
                if (sourcePrefab == null)
                {
                    report.MissingPrefabs++;
                    continue;
                }

                GameObject root = FindOrCreatePlacementRoot(scene, row.PlacementRoot, out bool rootCreated);
                if (rootCreated)
                    report.RootsCreated++;

                GameObject instance = FindDirectChild(root.transform, row.ObjectName);
                bool instantiated = false;
                if (instance == null)
                {
                    UnityEngine.Object created = PrefabUtility.InstantiatePrefab(sourcePrefab, scene);
                    instance = created as GameObject;
                    if (instance == null)
                    {
                        report.UnsupportedRows++;
                        continue;
                    }

                    instance.name = row.ObjectName;
                    instance.transform.SetParent(root.transform, worldPositionStays: false);
                    instantiated = true;
                    report.Instantiated++;
                }
                else
                {
                    report.Reused++;
                }

                bool changed = instantiated || ApplyScenePlacementTransform(instance.transform, row);
                if (TryAssignScenePlacementComponent(instance, row, out bool componentChanged, out string message))
                {
                    changed |= componentChanged;
                    if (componentChanged)
                        report.Configured++;
                }
                else
                {
                    report.Conflicts++;
                    Debug.LogWarning("[AppliedLoreScenePlacement] " + message);
                    continue;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    dirtyScenePaths.Add(row.ScenePath);
                }
                else
                {
                    report.AlreadyCurrent++;
                }
            }

            EnsureTerminalOsRuntimeForLoadedScenes(rows, ref report, dirtyScenePaths);

            foreach (string scenePath in dirtyScenePaths)
            {
                if (!FindLoadedScene(scenePath, out Scene scene))
                    continue;

                if (EditorSceneManager.SaveScene(scene))
                    report.SavedScenes++;
                else
                    report.SaveFailures++;
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        private static void EnsureTerminalOsRuntimeForLoadedScenes(
            List<ScenePlacementRow> rows,
            ref ScenePlacementReport report,
            HashSet<string> dirtyScenePaths)
        {
            HashSet<string> processedRuntimeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                ScenePlacementRow row = rows[i];
                string runtimeKey = ScenePlacementRuntimeKey(row.ScenePath, row.PlacementRoot);
                if (!row.IsValid ||
                    !string.Equals(row.AuthoringComponent, nameof(MessageTerminal), StringComparison.Ordinal) ||
                    !processedRuntimeKeys.Add(runtimeKey) ||
                    !FindLoadedScene(row.ScenePath, out Scene scene))
                {
                    continue;
                }

                if (EnsureTerminalOsRuntimeForScene(scene, row.PlacementRoot, ref report))
                    dirtyScenePaths.Add(row.ScenePath);
            }
        }

        private static bool EnsureTerminalOsRuntimeForScene(
            Scene scene,
            string placementRootName,
            ref ScenePlacementReport report)
        {
            GameObject placementRoot = FindOrCreatePlacementRoot(scene, placementRootName, out bool rootCreated);
            if (rootCreated)
                report.RootsCreated++;

            List<MessageTerminal> terminals = new List<MessageTerminal>(64);
            placementRoot.GetComponentsInChildren<MessageTerminal>(true, terminals);
            if (terminals.Count == 0)
                return rootCreated;

            GameObject runtimeObject = FindDirectChild(placementRoot.transform, TerminalOsRuntimeObjectName);
            bool changed = rootCreated;
            bool runtimeObjectCreated = false;
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(TerminalOsRuntimeObjectName);
                runtimeObject.transform.SetParent(placementRoot.transform, worldPositionStays: false);
                runtimeObject.transform.localPosition = Vector3.zero;
                runtimeObject.transform.localRotation = Quaternion.identity;
                runtimeObject.transform.localScale = Vector3.one;
                runtimeObjectCreated = true;
                changed = true;
            }

            TerminalOsRuntime runtime = runtimeObject.GetComponent<TerminalOsRuntime>();
            if (runtime == null)
            {
                runtime = runtimeObject.AddComponent<TerminalOsRuntime>();
                changed = true;
            }
            if (runtimeObjectCreated)
                report.TerminalOsRuntimeCreated++;

            SerializedObject serialized = new SerializedObject(runtime);
            changed |= TrySetSerializedObject(serialized, "terminalBlitCompute", AssetDatabase.LoadAssetAtPath<ComputeShader>(TerminalOsBlitComputePath));
            changed |= TrySetSerializedObject(serialized, "fontSdfAtlas", LoadTerminalOsFontAtlasTexture());
            changed |= TrySetSerializedObject(serialized, "terminalArrayMaterial", LoadOrCreateTerminalOsArrayPanelMaterial());
            changed |= TrySetSerializedObject(serialized, "terminalPanelMesh", AssetDatabase.LoadAssetAtPath<Mesh>(TerminalAnchorMeshPath));
            changed |= TrySetTerminalOsRuntimeArrays(serialized, terminals, ref report);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (changed)
            {
                report.TerminalOsRuntimeConfigured++;
                EditorUtility.SetDirty(runtimeObject);
                EditorUtility.SetDirty(runtime);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            else
            {
                report.TerminalOsRuntimeAlreadyCurrent++;
            }

            return changed;
        }

        private static bool TrySetTerminalOsRuntimeArrays(
            SerializedObject serialized,
            List<MessageTerminal> terminals,
            ref ScenePlacementReport report)
        {
            int maxPreviewIndex = -1;
            int validTerminalCount = 0;
            HashSet<int> seenPreviewIndices = new HashSet<int>();
            for (int i = 0; i < terminals.Count; i++)
            {
                int previewIndex = ReadSerializedInt(terminals[i], "terminalOsPreviewIndex", -1);
                if (previewIndex < 0)
                    continue;

                if (!seenPreviewIndices.Add(previewIndex))
                {
                    report.TerminalOsRuntimeDuplicatePreviewIndices++;
                    continue;
                }

                maxPreviewIndex = Mathf.Max(maxPreviewIndex, previewIndex);
                validTerminalCount++;
            }

            if (maxPreviewIndex < 0 || validTerminalCount == 0)
                return false;

            Renderer[] renderers = new Renderer[maxPreviewIndex + 1];
            Transform[] transforms = new Transform[maxPreviewIndex + 1];
            int missingRenderers = 0;
            HashSet<int> assignedPreviewIndices = new HashSet<int>();
            for (int i = 0; i < terminals.Count; i++)
            {
                MessageTerminal terminal = terminals[i];
                int previewIndex = ReadSerializedInt(terminal, "terminalOsPreviewIndex", -1);
                if (previewIndex < 0 ||
                    previewIndex >= renderers.Length ||
                    !assignedPreviewIndices.Add(previewIndex))
                {
                    continue;
                }

                Renderer renderer = ReadSerializedObject<Renderer>(terminal, "statusLightRenderer");
                renderers[previewIndex] = renderer != null ? renderer : terminal.GetComponentInChildren<Renderer>(true);
                transforms[previewIndex] = terminal.transform;
                if (renderers[previewIndex] == null)
                    missingRenderers++;
            }

            bool changed = false;
            changed |= TrySetSerializedObjectArray(serialized, "terminalRenderers", renderers);
            changed |= TrySetSerializedObjectArray(serialized, "terminalTransforms", transforms);
            report.TerminalOsRuntimeTerminals += validTerminalCount;
            report.TerminalOsRuntimeMissingRenderers += missingRenderers;
            return changed;
        }

        private static int ReadSerializedInt(UnityEngine.Object target, string propertyName, int fallback)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Integer ? property.intValue : fallback;
        }

        private static T ReadSerializedObject<T>(UnityEngine.Object target, string propertyName)
            where T : UnityEngine.Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference
                ? property.objectReferenceValue as T
                : null;
        }

        private static Texture2D LoadTerminalOsFontAtlasTexture()
        {
            Texture2D texture = LoadFirstTextureAtPath(TerminalOsFontSdfPrimaryPath);
            return texture != null ? texture : LoadFirstTextureAtPath(TerminalOsFontSdfFallbackPath);
        }

        private static Texture2D LoadFirstTextureAtPath(string path)
        {
            Texture2D direct = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (direct != null)
                return direct;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Texture2D texture)
                    return texture;
            }

            return null;
        }

        private static Material LoadOrCreateTerminalOsArrayPanelMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TerminalOsArrayPanelMaterialPath);
            if (material != null)
                return material;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TerminalOsArrayPanelShaderPath);
            if (shader == null)
                shader = Shader.Find("HECTON/UI/Terminal TextureArray Panel");
            if (shader == null)
                return null;

            material = new Material(shader)
            {
                name = "MAT_AppliedLore_TerminalOS_ArrayPanel",
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, TerminalOsArrayPanelMaterialPath);
            return material;
        }

        private static void LoadScenePlacementRows(List<ScenePlacementRow> destination)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), ScenePlacementPlanPath);
            if (!File.Exists(path))
                return;

            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return;

                List<string> headers = ParseCsvLine(headerLine);
                Dictionary<string, int> headerMap = BuildHeaderMap(headers);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> fields = ParseCsvLine(line);
                    ScenePlacementRow row = new ScenePlacementRow
                    {
                        PacketId = GetField(headerMap, fields, "packet_id"),
                        PacketHashHex = GetField(headerMap, fields, "packet_hash_hex"),
                        ScenePath = GetField(headerMap, fields, "scene_path"),
                        PlacementRoot = GetField(headerMap, fields, "placement_root"),
                        ObjectName = GetField(headerMap, fields, "object_name"),
                        SourcePrefab = GetField(headerMap, fields, "source_prefab"),
                        AuthoringComponent = GetField(headerMap, fields, "authoring_component"),
                        SerializedField = GetField(headerMap, fields, "serialized_field"),
                        DiscoveryId = GetField(headerMap, fields, "discovery_id"),
                        DisplayName = GetField(headerMap, fields, "display_name"),
                        LocalPosition = GetField(headerMap, fields, "local_position"),
                        LocalEuler = GetField(headerMap, fields, "local_euler"),
                        LocalScale = GetField(headerMap, fields, "local_scale")
                    };

                    if (TryParseUInt(GetField(headerMap, fields, "packet_hash_decimal"), out uint packetHash))
                        row.PacketHashUInt = packetHash;
                    else if (TryParseUInt(row.PacketHashHex, out packetHash))
                        row.PacketHashUInt = packetHash;

                    if (string.IsNullOrWhiteSpace(row.PlacementRoot))
                        row.PlacementRoot = DefaultScenePlacementRootName;

                    destination.Add(row);
                }
            }
        }

        private static void MarkDuplicateScenePlacementRows(List<ScenePlacementRow> rows, ref ScenePlacementReport report)
        {
            Dictionary<string, int> sceneOwners = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> discoveryIds = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                ScenePlacementRow row = rows[i];
                if (!string.IsNullOrWhiteSpace(row.ScenePath) &&
                    !string.IsNullOrWhiteSpace(row.PlacementRoot) &&
                    !string.IsNullOrWhiteSpace(row.ObjectName))
                {
                    string sceneOwnerKey = row.ScenePath + "\n" + row.PlacementRoot + "\n" + row.ObjectName;
                    if (sceneOwners.ContainsKey(sceneOwnerKey))
                    {
                        row.DuplicateSceneOwner = true;
                        report.DuplicateSceneOwners++;
                    }
                    else
                    {
                        sceneOwners.Add(sceneOwnerKey, i);
                    }
                }

                if (string.Equals(row.AuthoringComponent, nameof(NarrativeDiscovery), StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(row.DiscoveryId))
                {
                    if (discoveryIds.ContainsKey(row.DiscoveryId))
                    {
                        row.DuplicateDiscoveryId = true;
                        report.DuplicateDiscoveryIds++;
                    }
                    else
                    {
                        discoveryIds.Add(row.DiscoveryId, i);
                    }
                }

                rows[i] = row;
            }
        }

        private static void AssignTerminalPreviewIndices(List<ScenePlacementRow> rows)
        {
            Dictionary<string, int> terminalIndicesByRuntime = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                ScenePlacementRow row = rows[i];
                row.TerminalPreviewIndex = -1;
                if (row.AuthoringComponent == nameof(MessageTerminal))
                {
                    string runtimeKey = ScenePlacementRuntimeKey(row.ScenePath, row.PlacementRoot);
                    if (!terminalIndicesByRuntime.TryGetValue(runtimeKey, out int terminalIndex))
                        terminalIndex = 0;

                    row.TerminalPreviewIndex = terminalIndex;
                    terminalIndicesByRuntime[runtimeKey] = terminalIndex + 1;
                }
                rows[i] = row;
            }
        }

        private static bool TryValidateScenePlacementRowsBeforeMutation(
            List<ScenePlacementRow> rows,
            HashSet<uint> knownPacketHashes,
            ref ScenePlacementReport report)
        {
            bool valid = true;
            for (int i = 0; i < rows.Count; i++)
            {
                ScenePlacementRow row = rows[i];
                if (!row.IsValid)
                {
                    report.InvalidRows++;
                    valid = false;
                    continue;
                }

                if (!knownPacketHashes.Contains(row.PacketHashUInt))
                {
                    report.UnknownHashes++;
                    valid = false;
                }

                if (!FindLoadedScene(row.ScenePath, out _))
                {
                    report.SceneNotLoaded++;
                    valid = false;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(row.SourcePrefab) == null)
                {
                    report.MissingPrefabs++;
                    valid = false;
                }
            }

            return valid;
        }

        private static string ScenePlacementRuntimeKey(string scenePath, string placementRoot)
        {
            return (scenePath ?? string.Empty) + "\n" +
                   (string.IsNullOrWhiteSpace(placementRoot) ? DefaultScenePlacementRootName : placementRoot);
        }

        private static bool FindLoadedScene(string scenePath, out Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            scene = default(Scene);
            return false;
        }

        private static GameObject FindOrCreatePlacementRoot(Scene scene, string rootName, out bool created)
        {
            created = false;
            if (string.IsNullOrWhiteSpace(rootName))
                rootName = DefaultScenePlacementRootName;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, rootName, StringComparison.Ordinal))
                    return root;
            }

            GameObject createdRoot = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(createdRoot, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            created = true;
            return createdRoot;
        }

        private static GameObject FindDirectChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child.gameObject;
            }

            return null;
        }

        private static bool ApplyScenePlacementTransform(Transform transform, ScenePlacementRow row)
        {
            bool changed = false;
            if (TryParseVector3Pipe(row.LocalPosition, out Vector3 position) && transform.localPosition != position)
            {
                transform.localPosition = position;
                changed = true;
            }

            if (TryParseVector3Pipe(row.LocalEuler, out Vector3 euler) && transform.localEulerAngles != euler)
            {
                transform.localEulerAngles = euler;
                changed = true;
            }

            if (TryParseVector3Pipe(row.LocalScale, out Vector3 scale) && transform.localScale != scale)
            {
                transform.localScale = scale;
                changed = true;
            }

            return changed;
        }

        private static bool TryParseVector3Pipe(string raw, out Vector3 value)
        {
            value = Vector3.zero;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string[] parts = raw.Split('|');
            if (parts.Length != 3)
                return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }

        private static bool TryAssignScenePlacementComponent(
            GameObject target,
            ScenePlacementRow row,
            out bool changed,
            out string message)
        {
            changed = false;
            if (row.AuthoringComponent == nameof(MessageTerminal))
            {
                MessageTerminal terminal = target.GetComponentInChildren<MessageTerminal>(true);
                if (terminal == null)
                {
                    message = "Scene placement target has no MessageTerminal: " + row.ObjectName;
                    return false;
                }

                return TryAssignSceneTerminal(terminal, row, out changed, out message);
            }

            if (row.AuthoringComponent == nameof(NarrativeDiscovery))
            {
                NarrativeDiscovery discovery = target.GetComponent<NarrativeDiscovery>();
                if (discovery == null)
                {
                    discovery = target.AddComponent<NarrativeDiscovery>();
                    changed = true;
                }

                SerializedObject serialized = new SerializedObject(discovery);
                SerializedProperty packetHash = serialized.FindProperty(row.SerializedField);
                if (packetHash == null || packetHash.propertyType != SerializedPropertyType.Integer)
                {
                    message = "NarrativeDiscovery missing integer field " + row.SerializedField + ".";
                    return false;
                }

                uint currentHash = unchecked((uint)packetHash.longValue);
                if (currentHash != 0u && currentHash != row.PacketHashUInt)
                {
                    message = "NarrativeDiscovery already has different packet hash " + currentHash + " on " + row.ObjectName + ".";
                    return false;
                }

                if (currentHash != row.PacketHashUInt)
                {
                    packetHash.longValue = row.PacketHashUInt;
                    changed = true;
                }

                changed |= TrySetSerializedString(serialized, "discoveryId", row.DiscoveryId);
                changed |= TrySetSerializedString(serialized, "displayName", row.DisplayName);
                changed |= TrySetSerializedString(serialized, "interactVerb", "Study");
                changed |= TrySetSerializedBool(serialized, "disableAfterDiscovery", false);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(discovery);
                PrefabUtility.RecordPrefabInstancePropertyModifications(discovery);
                message = "Configured NarrativeDiscovery " + row.PacketId + " on " + row.ObjectName + ".";
                return true;
            }

            message = "Unsupported scene placement component: " + row.AuthoringComponent;
            return false;
        }

        private static bool TryAssignSceneTerminal(
            MessageTerminal terminal,
            ScenePlacementRow row,
            out bool changed,
            out string message)
        {
            changed = false;
            SerializedObject serialized = new SerializedObject(terminal);
            SerializedProperty packetHash = serialized.FindProperty(row.SerializedField);
            if (packetHash == null || packetHash.propertyType != SerializedPropertyType.Integer)
            {
                message = "MessageTerminal missing integer field " + row.SerializedField + ".";
                return false;
            }

            uint currentHash = unchecked((uint)packetHash.longValue);
            if (currentHash != 0u && currentHash != row.PacketHashUInt)
            {
                message = "MessageTerminal already has different packet hash " + currentHash + " on " + row.ObjectName + ".";
                return false;
            }

            if (currentHash != row.PacketHashUInt)
            {
                packetHash.longValue = row.PacketHashUInt;
                changed = true;
            }

            if (row.TerminalPreviewIndex >= 0)
            {
                changed |= TrySetSerializedInt(serialized, "terminalOsPreviewIndex", row.TerminalPreviewIndex);
                changed |= TrySetSerializedUInt(serialized, "terminalOsPreviewHash", TerminalOsHash.HashIndex(row.TerminalPreviewIndex));
                changed |= TrySetSerializedEnum(serialized, "terminalOsPreviewSurface", (int)H8AppliedLoreSurface.Terminal);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(terminal);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terminal);
            message = "Configured MessageTerminal " + row.PacketId + " on " + row.ObjectName + ".";
            return true;
        }

        private static bool TryAssignScenePacketHash(
            Component component,
            string fieldName,
            uint packetHash,
            out bool changed,
            out string message)
        {
            changed = false;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                message = component.GetType().Name + " missing integer field " + fieldName + ".";
                return false;
            }

            uint current = unchecked((uint)property.longValue);
            if (current == packetHash)
            {
                message = component.GetType().Name + "." + fieldName + " already has " + packetHash + ".";
                return true;
            }

            if (current != 0u)
            {
                message = component.GetType().Name + "." + fieldName + " already has different packet hash " + current + ".";
                return false;
            }

            property.longValue = packetHash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            changed = true;
            message = "Assigned " + packetHash + " to " + component.GetType().Name + "." + fieldName + ".";
            return true;
        }

        private static bool TrySetSerializedInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
                return false;

            if (property.intValue == value)
                return false;

            property.intValue = value;
            return true;
        }

        private static bool TrySetSerializedUInt(SerializedObject serialized, string propertyName, uint value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
                return false;

            if (unchecked((uint)property.longValue) == value)
                return false;

            property.longValue = value;
            return true;
        }

        private static bool TrySetSerializedEnum(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                return false;

            if (property.enumValueIndex == value)
                return false;

            property.enumValueIndex = value;
            return true;
        }

        private static bool TrySetSerializedObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            if (value == null)
                return false;

            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue == value)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static bool TrySetSerializedObjectArray<T>(
            SerializedObject serialized,
            string propertyName,
            T[] values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return false;

            bool changed = property.arraySize != values.Length;
            if (property.arraySize != values.Length)
                property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == values[i])
                    continue;

                element.objectReferenceValue = values[i];
                changed = true;
            }

            return changed;
        }

        private static bool TrySetSerializedString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return false;

            string next = value ?? string.Empty;
            if (property.stringValue == next)
                return false;

            property.stringValue = next;
            return true;
        }

        private static bool TrySetSerializedBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
                return false;

            if (property.boolValue == value)
                return false;

            property.boolValue = value;
            return true;
        }

        private static void CollectExistingPrefabCandidates(BindingRow row, List<string> destination)
        {
            CollectExistingPrefabCandidates(row.PrimaryTargetCandidates, destination);
            CollectExistingPrefabCandidates(row.SecondaryTargetCandidates, destination);
        }

        private static void CollectExistingPrefabCandidates(string candidates, List<string> destination)
        {
            if (string.IsNullOrWhiteSpace(candidates))
                return;

            string[] parts = candidates.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string candidate = parts[i].Trim();
                if (!candidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), candidate);
                if (!File.Exists(absolutePath) || destination.Contains(candidate))
                    continue;

                destination.Add(candidate);
            }
        }

        private static bool TryApplyBindingToPrefab(BindingRow row, string prefabPath, out bool changed, out string message)
        {
            changed = false;
            message = string.Empty;

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    message = "Failed to load prefab contents: " + prefabPath;
                    return false;
                }

                GameObject targetObject = ResolvePrefabBindingTarget(prefabRoot, row);
                if (targetObject == null)
                {
                    message = "No safe prefab target for " + row.PacketId + " in " + prefabPath + ".";
                    return false;
                }

                if (!TryAssignPrefabComponentField(targetObject, row, out changed, out message))
                    return false;

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                message = (changed ? "Applied " : "Already bound ") + row.PacketId + " to " + prefabPath + ".";
                return true;
            }
            catch (Exception exception)
            {
                message = "Prefab binding failed for " + prefabPath + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static GameObject ResolvePrefabBindingTarget(GameObject prefabRoot, BindingRow row)
        {
            if (row.PrimaryComponent == nameof(ScannableFragment))
            {
                ScannableFragment existing = prefabRoot.GetComponentInChildren<ScannableFragment>(true);
                if (existing != null)
                    return existing.gameObject;

                Renderer renderer = prefabRoot.GetComponentInChildren<Renderer>(true);
                return renderer != null ? renderer.gameObject : null;
            }

            if (row.PrimaryComponent == nameof(MessageTerminal))
            {
                MessageTerminal existing = prefabRoot.GetComponentInChildren<MessageTerminal>(true);
                return existing != null ? existing.gameObject : null;
            }

            if (row.PrimaryComponent == nameof(NarrativeDiscovery))
            {
                NarrativeDiscovery existing = prefabRoot.GetComponentInChildren<NarrativeDiscovery>(true);
                return existing != null ? existing.gameObject : null;
            }

            return null;
        }

        private static bool TryAssignPrefabComponentField(GameObject targetObject, BindingRow row, out bool changed, out string message)
        {
            changed = false;
            if (row.PrimaryComponent == nameof(ScannableFragment))
                return TryAssignPrefabSerializedField<ScannableFragment>(targetObject, row.PrimaryField, row.PacketHashUInt, allowAddComponent: true, out changed, out message);

            if (row.PrimaryComponent == nameof(MessageTerminal))
                return TryAssignPrefabSerializedField<MessageTerminal>(targetObject, row.PrimaryField, row.PacketHashUInt, allowAddComponent: false, out changed, out message);

            if (row.PrimaryComponent == nameof(NarrativeDiscovery))
                return TryAssignPrefabSerializedField<NarrativeDiscovery>(targetObject, row.PrimaryField, row.PacketHashUInt, allowAddComponent: false, out changed, out message);

            message = "Unsupported prefab authoring component: " + row.PrimaryComponent;
            return false;
        }

        private static bool TryAssignPrefabSerializedField<T>(
            GameObject targetObject,
            string fieldName,
            uint packetHash,
            bool allowAddComponent,
            out bool changed,
            out string message)
            where T : Component
        {
            changed = false;
            T component = targetObject.GetComponent<T>();
            if (component == null && allowAddComponent)
                component = targetObject.AddComponent<T>();

            if (component == null)
            {
                message = "Missing component " + typeof(T).Name + " on " + targetObject.name + ".";
                return false;
            }

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                message = typeof(T).Name + " missing integer field " + fieldName + ".";
                return false;
            }

            if (unchecked((uint)property.longValue) == packetHash)
            {
                message = typeof(T).Name + "." + fieldName + " already has " + packetHash + ".";
                return true;
            }

            if (property.longValue != 0)
            {
                message = typeof(T).Name + "." + fieldName + " already has different packet hash " +
                          unchecked((uint)property.longValue) + ".";
                return false;
            }

            property.longValue = packetHash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            changed = true;
            message = "Assigned " + packetHash + " to " + typeof(T).Name + "." + fieldName + ".";
            return true;
        }

        private static void ScanLoadedSceneComponents<T>(
            HashSet<uint> knownPacketHashes,
            ref BindingValidationReport report,
            params string[] fields)
            where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null || EditorUtility.IsPersistent(component))
                    continue;

                Scene scene = component.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                report.SceneComponentsScanned++;
                InspectSerializedFields(component, knownPacketHashes, ref report, fields);
            }
        }

        private static void ScanPrefabComponents<T>(
            GameObject prefab,
            HashSet<uint> knownPacketHashes,
            ref BindingValidationReport report,
            params string[] fields)
            where T : Component
        {
            T[] components = prefab.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                report.PrefabComponentsScanned++;
                InspectSerializedFields(components[i], knownPacketHashes, ref report, fields);
            }
        }

        private static void InspectSerializedFields<T>(
            T component,
            HashSet<uint> knownPacketHashes,
            ref BindingValidationReport report,
            string[] fields)
            where T : Component
        {
            SerializedObject serialized = new SerializedObject(component);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(fields[i]);
                if (property == null || property.propertyType != SerializedPropertyType.Integer)
                    continue;

                uint value = unchecked((uint)property.longValue);
                if (value == 0u)
                    continue;

                report.BoundFields++;
                if (!knownPacketHashes.Contains(value))
                    report.UnknownHashes++;
            }
        }

        private static void LoadKnownPacketHashes(HashSet<uint> destination)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), PacketCsvPath);
            if (!File.Exists(path))
                return;

            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return;

                List<string> headers = ParseCsvLine(headerLine);
                int packetIdIndex = headers.IndexOf("packet_id");
                if (packetIdIndex < 0)
                    return;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> fields = ParseCsvLine(line);
                    if (packetIdIndex >= fields.Count)
                        continue;

                    string packetId = fields[packetIdIndex];
                    if (!string.IsNullOrWhiteSpace(packetId))
                        destination.Add(Fnv1a32(packetId));
                }
            }
        }

        private static void LoadBindingRows(List<BindingRow> destination)
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), BindingMapFolder);
            if (!Directory.Exists(folder))
                return;

            string[] files = Directory.GetFiles(folder, "*_runtime_binding_map.csv", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
                LoadBindingRowsFromFile(files[i], destination);
        }

        private static int LoadTargetBacklogRows(Dictionary<string, TargetBacklogRow> destination)
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), BindingMapFolder);
            if (!Directory.Exists(folder))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(folder, "*_scene_binding_targets.csv", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
                count += LoadTargetBacklogRowsFromFile(files[i], destination);

            return count;
        }

        private static int LoadManualBindingPolicies(Dictionary<string, ManualBindingPolicyRow> destination)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), ManualBindingPolicyPath);
            if (!File.Exists(path))
                return 0;

            int count = 0;
            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return 0;

                List<string> headers = ParseCsvLine(headerLine);
                Dictionary<string, int> headerMap = BuildHeaderMap(headers);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> fields = ParseCsvLine(line);
                    string packetId = GetField(headerMap, fields, "packet_id");
                    if (string.IsNullOrWhiteSpace(packetId))
                        continue;

                    destination[packetId] = new ManualBindingPolicyRow
                    {
                        ManualPolicy = GetField(headerMap, fields, "manual_policy"),
                        RequiredAnchorType = GetField(headerMap, fields, "required_anchor_type"),
                        ApprovedTemplatePrefab = GetField(headerMap, fields, "approved_template_prefab"),
                        DiscoveryId = GetField(headerMap, fields, "discovery_id"),
                        PlacementRule = GetField(headerMap, fields, "placement_rule"),
                        ManualReason = GetField(headerMap, fields, "reason")
                    };
                    count++;
                }
            }

            return count;
        }

        private static void LoadBindingRowsFromFile(string path, List<BindingRow> destination)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return;

                List<string> headers = ParseCsvLine(headerLine);
                Dictionary<string, int> headerMap = BuildHeaderMap(headers);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> fields = ParseCsvLine(line);
                    BindingRow row = new BindingRow
                    {
                        PacketId = GetField(headerMap, fields, "packet_id"),
                        PacketHashHex = GetField(headerMap, fields, "packet_hash_hex"),
                        ReleaseSet = GetField(headerMap, fields, "release_set"),
                        PrimaryComponent = GetField(headerMap, fields, "primary_component"),
                        PrimaryField = GetField(headerMap, fields, "primary_field"),
                        SecondaryComponent = GetField(headerMap, fields, "secondary_component"),
                        SecondaryField = GetField(headerMap, fields, "secondary_field"),
                        SuggestedWorldTarget = GetField(headerMap, fields, "suggested_world_target"),
                        UnlockMoment = GetField(headerMap, fields, "unlock_moment"),
                        Notes = GetField(headerMap, fields, "notes")
                    };

                    if (TryParseUInt(GetField(headerMap, fields, "packet_hash_uint"), out uint packetHash))
                        row.PacketHashUInt = packetHash;
                    else if (TryParseUInt(row.PacketHashHex, out packetHash))
                        row.PacketHashUInt = packetHash;

                    if (row.IsValid)
                        destination.Add(row);
                }
            }
        }

        private static int LoadTargetBacklogRowsFromFile(string path, Dictionary<string, TargetBacklogRow> destination)
        {
            int count = 0;
            using (StreamReader reader = new StreamReader(path))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return 0;

                List<string> headers = ParseCsvLine(headerLine);
                Dictionary<string, int> headerMap = BuildHeaderMap(headers);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> fields = ParseCsvLine(line);
                    string packetId = GetField(headerMap, fields, "packet_id");
                    if (string.IsNullOrWhiteSpace(packetId))
                        continue;

                    destination[packetId] = new TargetBacklogRow
                    {
                        PrimaryTargetCandidates = GetField(headerMap, fields, "primary_target_candidates"),
                        SecondaryTargetCandidates = GetField(headerMap, fields, "secondary_target_candidates"),
                        UnitySafeAction = GetField(headerMap, fields, "unity_safe_action")
                    };
                    count++;
                }
            }

            return count;
        }

        private static void MergeTargetBacklog(List<BindingRow> bindings, Dictionary<string, TargetBacklogRow> targetBacklog)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                BindingRow row = bindings[i];
                if (!targetBacklog.TryGetValue(row.PacketId, out TargetBacklogRow target))
                    continue;

                row.PrimaryTargetCandidates = target.PrimaryTargetCandidates;
                row.SecondaryTargetCandidates = target.SecondaryTargetCandidates;
                row.UnitySafeAction = target.UnitySafeAction;
                bindings[i] = row;
            }
        }

        private static void MergeManualBindingPolicies(List<BindingRow> bindings, Dictionary<string, ManualBindingPolicyRow> manualPolicies)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                BindingRow row = bindings[i];
                if (!manualPolicies.TryGetValue(row.PacketId, out ManualBindingPolicyRow policy))
                    continue;

                row.ManualPolicy = policy.ManualPolicy;
                row.RequiredAnchorType = policy.RequiredAnchorType;
                row.ApprovedTemplatePrefab = policy.ApprovedTemplatePrefab;
                row.DiscoveryId = policy.DiscoveryId;
                row.PlacementRule = policy.PlacementRule;
                row.ManualReason = policy.ManualReason;
                bindings[i] = row;
            }
        }

        private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
        {
            Dictionary<string, int> map = new Dictionary<string, int>(headers.Count, StringComparer.Ordinal);
            for (int i = 0; i < headers.Count; i++)
            {
                if (!map.ContainsKey(headers[i]))
                    map.Add(headers[i], i);
            }

            return map;
        }

        private static string GetField(Dictionary<string, int> headerMap, List<string> fields, string name)
        {
            if (!headerMap.TryGetValue(name, out int index))
                return string.Empty;
            if (index < 0 || index >= fields.Count)
                return string.Empty;
            return fields[index];
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> result = new List<string>(16);
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (c == ',' && !quoted)
                {
                    result.Add(UnescapeCsvCell(line, start, i - start));
                    start = i + 1;
                }
            }

            result.Add(UnescapeCsvCell(line, start, line.Length - start));
            return result;
        }

        private static string UnescapeCsvCell(string line, int start, int length)
        {
            if (length <= 0)
                return string.Empty;

            string value = line.Substring(start, length);
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
            return value;
        }

        private static bool TryParseUInt(string raw, out uint value)
        {
            value = 0u;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static uint Fnv1a32(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                hash ^= (byte)c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static int CompareBindingRows(BindingRow left, BindingRow right)
        {
            int release = string.CompareOrdinal(left.ReleaseSet, right.ReleaseSet);
            if (release != 0)
                return release;
            return string.CompareOrdinal(left.PacketId, right.PacketId);
        }

        private struct BindingRow
        {
            public string PacketId;
            public string PacketHashHex;
            public uint PacketHashUInt;
            public string ReleaseSet;
            public string PrimaryComponent;
            public string PrimaryField;
            public string SecondaryComponent;
            public string SecondaryField;
            public string SuggestedWorldTarget;
            public string UnlockMoment;
            public string Notes;
            public string PrimaryTargetCandidates;
            public string SecondaryTargetCandidates;
            public string UnitySafeAction;
            public string ManualPolicy;
            public string RequiredAnchorType;
            public string ApprovedTemplatePrefab;
            public string DiscoveryId;
            public string PlacementRule;
            public string ManualReason;

            public bool IsValid => !string.IsNullOrWhiteSpace(PacketId) && PacketHashUInt != 0u;

            public bool Contains(string filter)
            {
                return Contains(PacketId, filter) ||
                       Contains(ReleaseSet, filter) ||
                       Contains(PrimaryComponent, filter) ||
                       Contains(PrimaryField, filter) ||
                       Contains(SecondaryComponent, filter) ||
                       Contains(SecondaryField, filter) ||
                       Contains(SuggestedWorldTarget, filter) ||
                       Contains(PrimaryTargetCandidates, filter) ||
                       Contains(SecondaryTargetCandidates, filter) ||
                       Contains(UnitySafeAction, filter) ||
                       Contains(ManualPolicy, filter) ||
                       Contains(RequiredAnchorType, filter) ||
                       Contains(ApprovedTemplatePrefab, filter) ||
                       Contains(DiscoveryId, filter) ||
                       Contains(PlacementRule, filter) ||
                       Contains(ManualReason, filter) ||
                       Contains(UnlockMoment, filter) ||
                       Contains(Notes, filter);
            }

            public string ToListText()
            {
                string targetHint = !string.IsNullOrWhiteSpace(SuggestedWorldTarget)
                    ? SuggestedWorldTarget
                    : PrimaryTargetCandidates;
                return ReleaseSet + " | " + PacketId + " | " + PacketHashHex + " | " +
                       PrimaryComponent + "." + PrimaryField + " | " + targetHint;
            }

            private static bool Contains(string value, string filter)
            {
                return !string.IsNullOrEmpty(value) &&
                       value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private struct TargetBacklogRow
        {
            public string PrimaryTargetCandidates;
            public string SecondaryTargetCandidates;
            public string UnitySafeAction;
        }

        private struct ManualBindingPolicyRow
        {
            public string ManualPolicy;
            public string RequiredAnchorType;
            public string ApprovedTemplatePrefab;
            public string DiscoveryId;
            public string PlacementRule;
            public string ManualReason;
        }

        private struct ScenePlacementRow
        {
            public string PacketId;
            public string PacketHashHex;
            public uint PacketHashUInt;
            public string ScenePath;
            public string PlacementRoot;
            public string ObjectName;
            public string SourcePrefab;
            public string AuthoringComponent;
            public string SerializedField;
            public string DiscoveryId;
            public string DisplayName;
            public string LocalPosition;
            public string LocalEuler;
            public string LocalScale;
            public int TerminalPreviewIndex;
            public bool DuplicateSceneOwner;
            public bool DuplicateDiscoveryId;

            public bool IsValid =>
                !string.IsNullOrWhiteSpace(PacketId) &&
                PacketHashUInt != 0u &&
                !string.IsNullOrWhiteSpace(ScenePath) &&
                !string.IsNullOrWhiteSpace(ObjectName) &&
                !string.IsNullOrWhiteSpace(SourcePrefab) &&
                !string.IsNullOrWhiteSpace(AuthoringComponent) &&
                !string.IsNullOrWhiteSpace(SerializedField) &&
                !DuplicateSceneOwner &&
                !DuplicateDiscoveryId;
        }

        private struct ScenePlacementReport
        {
            public int PlanRows;
            public int RowsConsidered;
            public int SceneNotLoaded;
            public int MissingPrefabs;
            public int InvalidRows;
            public int DuplicateSceneOwners;
            public int DuplicateDiscoveryIds;
            public int UnknownHashes;
            public int RootsCreated;
            public int Instantiated;
            public int Reused;
            public int Configured;
            public int AlreadyCurrent;
            public int Conflicts;
            public int UnsupportedRows;
            public int SavedScenes;
            public int SaveFailures;
            public int TerminalOsRuntimeCreated;
            public int TerminalOsRuntimeConfigured;
            public int TerminalOsRuntimeAlreadyCurrent;
            public int TerminalOsRuntimeTerminals;
            public int TerminalOsRuntimeMissingRenderers;
            public int TerminalOsRuntimeDuplicatePreviewIndices;
            public bool PreflightAborted;

            public string ToLogLine()
            {
                return "[AppliedLoreScenePlacement] plan_rows=" + PlanRows +
                       " preflight_aborted=" + PreflightAborted +
                       " rows_considered=" + RowsConsidered +
                       " scene_not_loaded=" + SceneNotLoaded +
                       " missing_prefabs=" + MissingPrefabs +
                       " invalid_rows=" + InvalidRows +
                       " duplicate_scene_owners=" + DuplicateSceneOwners +
                       " duplicate_discovery_ids=" + DuplicateDiscoveryIds +
                       " unknown_hashes=" + UnknownHashes +
                       " roots_created=" + RootsCreated +
                       " instantiated=" + Instantiated +
                       " reused=" + Reused +
                       " configured=" + Configured +
                       " already_current=" + AlreadyCurrent +
                       " conflicts=" + Conflicts +
                       " unsupported_rows=" + UnsupportedRows +
                       " saved_scenes=" + SavedScenes +
                       " save_failures=" + SaveFailures +
                       " terminal_os_created=" + TerminalOsRuntimeCreated +
                       " terminal_os_configured=" + TerminalOsRuntimeConfigured +
                       " terminal_os_already_current=" + TerminalOsRuntimeAlreadyCurrent +
                       " terminal_os_terminals=" + TerminalOsRuntimeTerminals +
                       " terminal_os_missing_renderers=" + TerminalOsRuntimeMissingRenderers +
                       " terminal_os_duplicate_preview_indices=" + TerminalOsRuntimeDuplicatePreviewIndices;
            }

            public string ToDialogText()
            {
                return "Plan rows: " + PlanRows +
                       "\nPreflight aborted: " + PreflightAborted +
                       "\nRows considered: " + RowsConsidered +
                       "\nScene not loaded: " + SceneNotLoaded +
                       "\nMissing prefabs: " + MissingPrefabs +
                       "\nInvalid rows: " + InvalidRows +
                       "\nDuplicate scene owners: " + DuplicateSceneOwners +
                       "\nDuplicate discovery ids: " + DuplicateDiscoveryIds +
                       "\nUnknown hashes: " + UnknownHashes +
                       "\nRoots created: " + RootsCreated +
                       "\nInstantiated: " + Instantiated +
                       "\nReused: " + Reused +
                       "\nConfigured: " + Configured +
                       "\nAlready current: " + AlreadyCurrent +
                       "\nConflicts: " + Conflicts +
                       "\nUnsupported rows: " + UnsupportedRows +
                       "\nSaved scenes: " + SavedScenes +
                       "\nSave failures: " + SaveFailures +
                       "\nTerminalOS runtimes created: " + TerminalOsRuntimeCreated +
                       "\nTerminalOS runtimes configured: " + TerminalOsRuntimeConfigured +
                       "\nTerminalOS runtimes already current: " + TerminalOsRuntimeAlreadyCurrent +
                       "\nTerminalOS terminal bindings: " + TerminalOsRuntimeTerminals +
                       "\nTerminalOS missing renderers: " + TerminalOsRuntimeMissingRenderers +
                       "\nTerminalOS duplicate preview indices: " + TerminalOsRuntimeDuplicatePreviewIndices;
            }
        }

        private struct TerminalAnchorReport
        {
            public string PrefabPath;
            public bool CreatedOrUpdated;
            public bool UsedMesh;
            public bool UsedMaterial;

            public string ToLogLine()
            {
                return "[AppliedLoreTerminalAnchor] prefab=" + PrefabPath +
                       " created_or_updated=" + CreatedOrUpdated +
                       " used_mesh=" + UsedMesh +
                       " used_material=" + UsedMaterial;
            }

            public string ToDialogText()
            {
                return "Prefab: " + PrefabPath +
                       "\nCreated or updated: " + CreatedOrUpdated +
                       "\nUsed curved panel mesh: " + UsedMesh +
                       "\nUsed diegetic material: " + UsedMaterial;
            }
        }

        private struct TerminalPolicyPrefabReport
        {
            public int PolicyRows;
            public int GeneratedOrUpdated;
            public int AlreadyCurrent;
            public int Failed;
            public bool MissingAnchor;

            public string ToLogLine()
            {
                return "[AppliedLoreTerminalPolicyPrefabs] policy_rows=" + PolicyRows +
                       " generated_or_updated=" + GeneratedOrUpdated +
                       " already_current=" + AlreadyCurrent +
                       " failed=" + Failed +
                       " missing_anchor=" + MissingAnchor;
            }

            public string ToDialogText()
            {
                return "Terminal policy rows: " + PolicyRows +
                       "\nGenerated or updated: " + GeneratedOrUpdated +
                       "\nAlready current: " + AlreadyCurrent +
                       "\nFailed: " + Failed +
                       "\nMissing anchor: " + MissingAnchor;
            }
        }

        private struct PrefabBacklogApplyReport
        {
            public int BacklogRows;
            public int RowsConsidered;
            public int PrefabsOpened;
            public int PrefabsChanged;
            public int BindingsApplied;
            public int AlreadyBound;
            public int SkippedNoPrefab;
            public int SkippedUnsupported;
            public int UnknownHashes;

            public string ToLogLine()
            {
                return "[AppliedLorePrefabBacklog] backlog_rows=" + BacklogRows +
                       " rows_considered=" + RowsConsidered +
                       " prefabs_opened=" + PrefabsOpened +
                       " prefabs_changed=" + PrefabsChanged +
                       " bindings_applied=" + BindingsApplied +
                       " already_bound=" + AlreadyBound +
                       " skipped_no_prefab=" + SkippedNoPrefab +
                       " skipped_unsupported=" + SkippedUnsupported +
                       " unknown_hashes=" + UnknownHashes;
            }

            public string ToDialogText()
            {
                return "Backlog rows: " + BacklogRows +
                       "\nRows considered: " + RowsConsidered +
                       "\nPrefabs opened: " + PrefabsOpened +
                       "\nPrefabs changed: " + PrefabsChanged +
                       "\nBindings applied: " + BindingsApplied +
                       "\nAlready bound: " + AlreadyBound +
                       "\nSkipped, no prefab candidate: " + SkippedNoPrefab +
                       "\nSkipped, unsupported/manual target: " + SkippedUnsupported +
                       "\nUnknown hashes: " + UnknownHashes;
            }
        }

        private struct BindingValidationReport
        {
            public int KnownPacketHashes;
            public int SceneComponentsScanned;
            public int PrefabsScanned;
            public int PrefabComponentsScanned;
            public int BoundFields;
            public int UnknownHashes;

            public string ToLogLine()
            {
                return "[AppliedLoreBindings] known_hashes=" + KnownPacketHashes +
                       " scene_components=" + SceneComponentsScanned +
                       " prefabs=" + PrefabsScanned +
                       " prefab_components=" + PrefabComponentsScanned +
                       " bound_fields=" + BoundFields +
                       " unknown_hashes=" + UnknownHashes;
            }

            public string ToDialogText()
            {
                return "Known packet hashes: " + KnownPacketHashes +
                       "\nLoaded-scene components scanned: " + SceneComponentsScanned +
                       "\nPrefabs scanned: " + PrefabsScanned +
                       "\nPrefab components scanned: " + PrefabComponentsScanned +
                       "\nBound AppliedLore fields: " + BoundFields +
                       "\nUnknown hashes: " + UnknownHashes;
            }
        }
    }
}
#endif
