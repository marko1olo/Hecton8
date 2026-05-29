using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Hecton8.Editor.ModdingSDK
{
    /// <summary>
    /// Editor-only facade over the public external starter kit.
    /// </summary>
    public sealed class ExternalStarterKitWorkbenchWindow : EditorWindow
    {
        private const string ExternalStarterKitRoot = "ModdingSDK/ExternalStarterKit";
        private const string RuntimeBoundary =
            "Runtime API: envelope-only. This Workbench edits starter-kit files and runs validation tools; it does not enable managed DLL, Harmony, BepInEx, loose AssetBundle, PNG, or localization runtime ingress.";
        private const int ToolSummaryLineCount = 16;
        private const int MaxFreshnessScanFiles = 512;
        private const int MaxGraphPreviewNodes = 256;
        private const int MaxGraphPreviewBytes = 1048576;
        private const int MaxAllowedOpcodePreviewRows = 512;
        private const int MaxAllowedOpcodePreviewBytes = 1048576;
        private const int MaxAuthoringDataPreviewBytes = 1048576;
        private const int MaxSettingsPreviewRows = 128;
        private const int MaxLocalePreviewStrings = 512;
        private const int MaxAssetPreviewEntries = 512;
        private static readonly string[] RequiredStarterFiles =
        {
            "README.md",
            "Docs/capabilities.md",
            "h8mod.ps1",
            "mod.h8manifest.json",
            "mod.json",
            "Content/README.md",
            "Content/Assets/README.md",
            "Content/assets.h8manifest.json",
            "Graphs/main.h8graph.json",
            "Tables/settings.h8table.json",
            "Locales/en.h8loc.json",
            "Generated/README.md",
            "Reports/README.md",
            "Reference/README.md",
            "Reference/allowed_opcodes.csv",
            "Reference/kernel_tuning_profiles.csv",
            "Schemas/assets.schema.json",
            "Schemas/h8graph.schema.json",
            "Schemas/h8mod.authoring.schema.json",
            "Schemas/locale.schema.json",
            "Schemas/runtime.mod.schema.json",
            "Schemas/settings_table.schema.json",
            "Tools/README.md",
            "Tools/apply_asset_entry_snippet.ps1",
            "Tools/apply_graph_node_snippet.ps1",
            "Tools/apply_locale_entry_snippet.ps1",
            "Tools/apply_settings_row_snippet.ps1",
            "Tools/build_review_manifest.ps1",
            "Tools/build_submission_package.ps1",
            "Tools/configure_manifest_contract.ps1",
            "Tools/create_asset_entry_snippet.ps1",
            "Tools/create_locale_entry_snippet.ps1",
            "Tools/create_graph_node_snippet.ps1",
            "Tools/create_settings_row_snippet.ps1",
            "Tools/list_allowed_opcodes.ps1",
            "Tools/prepare_mod.ps1",
            "Tools/set_mod_identity.ps1",
            "Tools/validate_structure.ps1",
            ".vscode/settings.json",
            ".vscode/tasks.json"
        };

        private Vector2 _scrollPosition;
        private string _modId = string.Empty;
        private string _displayName = string.Empty;
        private string _author = string.Empty;
        private string _version = string.Empty;
        private string _starterHealthSummary = "Starter kit health not loaded.";
        private string _starterHealthDetails = string.Empty;
        private string _capabilityMatrixSummary = "Capability Matrix not loaded.";
        private string _capabilityMatrixDetails = string.Empty;
        private string _graphContractPreviewSummary = "Graph Contract Preview not loaded.";
        private string _graphContractPreviewDetails = string.Empty;
        private string _authoringDataPreviewSummary = "Authoring Data Preview not loaded.";
        private string _authoringDataPreviewDetails = string.Empty;
        private string _graphNodeSnippetId = "node.spawn_item";
        private string _graphNodeSnippetOpcode = "SpawnItem";
        private string[] _graphOpcodePopupLabels = { "SpawnItem (0x3A3DA9C4)" };
        private string[] _graphOpcodePopupValues = { "SpawnItem" };
        private int _graphOpcodePopupIndex;
        private string _graphNodeParametersJson = "{\n}";
        private bool _graphNodeDisabled;
        private bool _graphNodeReplaceExisting;
        private string _settingsRowSnippetId = "setting.example_toggle";
        private string _settingsRowSnippetKind = "bool";
        private string _settingsRowSnippetDefault = "false";
        private string _localeEntrySnippetKey = "text.example_line";
        private string _localeEntrySnippetValue = "Your localized text";
        private string _assetEntrySnippetId = "asset.example_blob";
        private string _assetEntrySnippetKind = "data_blob";
        private string _assetEntrySnippetPath = "Content/Assets/example.bytes";
        private string _assetEntrySnippetCrc32 = "00000000";
        private long _assetEntrySnippetBytes;
        private bool _assetEntryReplaceExisting;
        private int _manifestCapabilityPopupIndex;
        private int _manifestCapabilityActionPopupIndex;
        private int _manifestMaxEnvelopesPerFrame = -1;
        private long _manifestMaxAssetBytes = -1L;
        private static readonly string[] ManifestCapabilityLabels =
        {
            "Command Graph Draft",
            "Settings Table",
            "English Locale",
            "Content Asset Manifest",
            "Review Submission Package"
        };
        private static readonly string[] ManifestCapabilityValues =
        {
            "cap.graph.command_draft",
            "cap.settings.table",
            "cap.locale.en",
            "cap.content.asset_manifest",
            "cap.review.submission_package"
        };
        private static readonly string[] ManifestCapabilityActionLabels =
        {
            "Enable",
            "Disable",
            "No Change"
        };
        private static readonly string[] ManifestCapabilityActionValues =
        {
            "enable",
            "disable",
            "unchanged"
        };
        private static readonly string[] AssetKindLabels =
        {
            "Data Blob (.json/.bytes/.bin)",
            "Raw Texture (.png/.jpg/.jpeg/.webp)",
            "Audio Clip (.wav/.ogg)"
        };
        private static readonly string[] AssetKindValues =
        {
            "data_blob",
            "raw_texture",
            "audio_clip"
        };
        private int _assetKindPopupIndex;
        private string _reviewSummary = "Review manifest not loaded.";
        private string _reviewFreshnessSummary = "Review freshness not loaded.";
        private string _submissionSummary = "Submission package not loaded.";
        private string _submissionPackageRelativePath = string.Empty;
        private string _toolSummary = string.Empty;
        private bool _toolSummaryIsError;
        private bool _starterHealthHasMissingFiles;
        private bool _capabilityMatrixWarning;
        private bool _graphContractPreviewWarning;
        private bool _authoringDataPreviewWarning;
        private bool _reviewFreshnessWarning;
        private bool _submissionWarning;
        private readonly object _toolOutputLock = new object();
        private DiagnosticsProcess _runningToolProcess;
        private StringBuilder _runningToolStdout;
        private StringBuilder _runningToolStderr;
        private string _runningToolName = string.Empty;
        private bool _runningToolReloadAfterSuccess;
        private bool _runningToolCompleted;
        private int _runningToolExitCode;

        [MenuItem("Hecton/Modding/External Starter Kit Workbench")]
        public static void ShowWindow()
        {
            ExternalStarterKitWorkbenchWindow window = GetWindow<ExternalStarterKitWorkbenchWindow>("HECTON Starter Workbench");
            window.minSize = new Vector2(720f, 520f);
            window.Reload();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollRunningTool;
            if (_runningToolProcess == null)
                return;

            try
            {
                if (!_runningToolProcess.HasExited)
                    _runningToolProcess.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ExternalStarterKitWorkbenchWindow] Tool cleanup failed: " + exception.Message);
            }
            finally
            {
                DisposeRunningTool();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("HECTON-8 External Starter Kit Workbench", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(RuntimeBoundary, MessageType.Warning);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawRootActions();
            EditorGUILayout.Space(10f);
            DrawStarterHealth();
            EditorGUILayout.Space(10f);
            DrawCapabilityMatrix();
            EditorGUILayout.Space(10f);
            DrawGraphContractPreview();
            EditorGUILayout.Space(10f);
            DrawAuthoringDataPreview();
            EditorGUILayout.Space(10f);
            DrawManifestContract();
            EditorGUILayout.Space(10f);
            DrawAuthoringSnippets();
            EditorGUILayout.Space(10f);
            DrawContentAssetSnippet();
            EditorGUILayout.Space(10f);
            DrawGraphNodeSnippet();
            EditorGUILayout.Space(10f);
            DrawIdentityEditor();
            EditorGUILayout.Space(10f);
            DrawValidationActions();
            EditorGUILayout.Space(10f);
            DrawFileActions();
            EditorGUILayout.Space(10f);
            DrawStatus();
            EditorGUILayout.EndScrollView();
        }

        private void DrawRootActions()
        {
            EditorGUILayout.LabelField("Starter Kit", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(ResolveProjectPath(ExternalStarterKitRoot), EditorStyles.textField, GUILayout.Height(18f));

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open SDK Hub", GUILayout.Height(24f)))
                    ModdingSdkHubWindow.ShowWindow();

                if (GUILayout.Button("Create/Refresh Starter Kit", GUILayout.Height(24f)))
                    CreateOrRefreshStarterKit();

                if (GUILayout.Button("Open Starter Folder", GUILayout.Height(24f)))
                    RevealRelativePath(ExternalStarterKitRoot);

                if (GUILayout.Button("Reload", GUILayout.Height(24f)))
                    Reload();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawStarterHealth()
        {
            EditorGUILayout.LabelField("Starter Health", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _starterHealthSummary,
                _starterHealthHasMissingFiles ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_starterHealthDetails))
                EditorGUILayout.TextArea(_starterHealthDetails, GUILayout.MinHeight(72f));
        }

        private void DrawCapabilityMatrix()
        {
            EditorGUILayout.LabelField("Capability Matrix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _capabilityMatrixSummary,
                _capabilityMatrixWarning ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_capabilityMatrixDetails))
                EditorGUILayout.TextArea(_capabilityMatrixDetails, GUILayout.MinHeight(96f));

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Capabilities Guide", GUILayout.Height(24f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Docs/capabilities.md");

                if (GUILayout.Button("Open Allowed Opcodes", GUILayout.Height(24f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Reference/allowed_opcodes.csv");
            }
        }

        private void DrawGraphContractPreview()
        {
            EditorGUILayout.LabelField("Graph Contract Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _graphContractPreviewSummary,
                _graphContractPreviewWarning ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_graphContractPreviewDetails))
                EditorGUILayout.TextArea(_graphContractPreviewDetails, GUILayout.MinHeight(72f));
        }

        private void DrawAuthoringDataPreview()
        {
            EditorGUILayout.LabelField("Authoring Data Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _authoringDataPreviewSummary,
                _authoringDataPreviewWarning ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_authoringDataPreviewDetails))
                EditorGUILayout.TextArea(_authoringDataPreviewDetails, GUILayout.MinHeight(72f));
        }

        private void DrawManifestContract()
        {
            EditorGUILayout.LabelField("Manifest Contract", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configures mod.h8manifest.json Capabilities and Budgets through a bounded offline tool. Capabilities are review metadata from a public allowlist, not runtime rights; budgets are capped and cannot be lowered below the current graph or asset manifest requirements.",
                MessageType.Info);

            _manifestCapabilityPopupIndex = EditorGUILayout.Popup(
                "Capability",
                Mathf.Clamp(_manifestCapabilityPopupIndex, 0, ManifestCapabilityLabels.Length - 1),
                ManifestCapabilityLabels);
            _manifestCapabilityActionPopupIndex = EditorGUILayout.Popup(
                "Capability Action",
                Mathf.Clamp(_manifestCapabilityActionPopupIndex, 0, ManifestCapabilityActionLabels.Length - 1),
                ManifestCapabilityActionLabels);
            _manifestMaxEnvelopesPerFrame = EditorGUILayout.IntField("Max Envelopes Per Frame", _manifestMaxEnvelopesPerFrame);
            _manifestMaxAssetBytes = EditorGUILayout.LongField("Max Asset Bytes", _manifestMaxAssetBytes);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Manifest Contract + Validate", GUILayout.Height(28f)))
                    ConfigureManifestContract();

                if (GUILayout.Button("Open Manifest Contract Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/configure_manifest_contract.ps1");

                if (GUILayout.Button("Open Authoring Manifest", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/mod.h8manifest.json");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawGraphNodeSnippet()
        {
            EditorGUILayout.LabelField("Graph Node Snippet", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Graph Opcode Picker loads Reference/allowed_opcodes.csv, Parameters JSON is validated by the snippet tool, and Apply inserts the node through a bounded offline tool. Apply rejects duplicates unless Replace Existing is enabled, raises the starter graph/manifest budget to one envelope if needed, validates after write, and restores the previous files on failure.",
                MessageType.Info);

            DrawGraphOpcodePicker();
            _graphNodeSnippetId = EditorGUILayout.TextField("Node Id", _graphNodeSnippetId);
            _graphNodeSnippetOpcode = EditorGUILayout.TextField("Opcode Alias/Hex", _graphNodeSnippetOpcode);
            _graphNodeDisabled = EditorGUILayout.Toggle("Create Disabled Node", _graphNodeDisabled);
            _graphNodeReplaceExisting = EditorGUILayout.Toggle("Replace Existing On Apply", _graphNodeReplaceExisting);
            EditorGUILayout.LabelField("Parameters JSON", EditorStyles.miniBoldLabel);
            _graphNodeParametersJson = EditorGUILayout.TextArea(_graphNodeParametersJson, GUILayout.MinHeight(54f));

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Node Snippet", GUILayout.Height(28f)))
                    GenerateGraphNodeSnippet();

                if (GUILayout.Button("Open Generated Snippet", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Generated/graph_node_snippet.json");

                if (GUILayout.Button("Open Snippet Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/create_graph_node_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Node Snippet", GUILayout.Height(28f)))
                    ApplyGraphNodeSnippet();

                if (GUILayout.Button("Open Graph", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Graphs/main.h8graph.json");

                if (GUILayout.Button("Open Graph Apply Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/apply_graph_node_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawGraphOpcodePicker()
        {
            EditorGUILayout.LabelField("Graph Opcode Picker", EditorStyles.miniBoldLabel);
            if (_graphOpcodePopupLabels == null || _graphOpcodePopupLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("Opcode picker unavailable. Open Reference/allowed_opcodes.csv or run List Graph Opcodes.", MessageType.Warning);
                return;
            }

            int safeIndex = Mathf.Clamp(_graphOpcodePopupIndex, 0, _graphOpcodePopupLabels.Length - 1);
            EditorGUI.BeginChangeCheck();
            safeIndex = EditorGUILayout.Popup("Allowed Opcode", safeIndex, _graphOpcodePopupLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _graphOpcodePopupIndex = safeIndex;
                _graphNodeSnippetOpcode = _graphOpcodePopupValues[safeIndex];
            }
        }

        private void DrawAuthoringSnippets()
        {
            EditorGUILayout.LabelField("Authoring Snippets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates Generated/settings_row_snippet.json and Generated/locale_entry_snippet.json, then applies them through bounded offline tools. Apply rejects duplicates unless the CLI -Replace switch is explicit, validates after write, and restores the previous file on failure.",
                MessageType.Info);

            EditorGUILayout.LabelField("Settings Row Snippet", EditorStyles.miniBoldLabel);
            _settingsRowSnippetId = EditorGUILayout.TextField("Setting Id", _settingsRowSnippetId);
            _settingsRowSnippetKind = EditorGUILayout.TextField("Kind", _settingsRowSnippetKind);
            _settingsRowSnippetDefault = EditorGUILayout.TextField("Default", _settingsRowSnippetDefault);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Setting Snippet", GUILayout.Height(28f)))
                    GenerateSettingsRowSnippet();

                if (GUILayout.Button("Open Setting Snippet", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Generated/settings_row_snippet.json");

                if (GUILayout.Button("Open Settings Snippet Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/create_settings_row_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Setting Snippet", GUILayout.Height(28f)))
                    ApplySettingsRowSnippet();

                if (GUILayout.Button("Open Settings Table", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tables/settings.h8table.json");

                if (GUILayout.Button("Open Settings Apply Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/apply_settings_row_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Locale Entry Snippet", EditorStyles.miniBoldLabel);
            _localeEntrySnippetKey = EditorGUILayout.TextField("Locale Key", _localeEntrySnippetKey);
            _localeEntrySnippetValue = EditorGUILayout.TextField("Value", _localeEntrySnippetValue);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Locale Snippet", GUILayout.Height(28f)))
                    GenerateLocaleEntrySnippet();

                if (GUILayout.Button("Open Locale Snippet", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Generated/locale_entry_snippet.json");

                if (GUILayout.Button("Open Locale Snippet Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/create_locale_entry_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Locale Snippet", GUILayout.Height(28f)))
                    ApplyLocaleEntrySnippet();

                if (GUILayout.Button("Open Locale Table", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Locales/en.h8loc.json");

                if (GUILayout.Button("Open Locale Apply Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/apply_locale_entry_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawContentAssetSnippet()
        {
            EditorGUILayout.LabelField("Content Asset Snippet", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates Generated/asset_entry_snippet.json from a file under Content/Assets/, then applies it through a bounded offline tool. Apply computes and verifies Crc32/Bytes against the file, rejects duplicate asset ids unless Replace Existing is enabled, raises MaxAssetBytes when needed, validates after write, and restores previous files on failure. Runtime loading remains review/bake only.",
                MessageType.Info);

            DrawAssetKindPicker();
            _assetEntrySnippetId = EditorGUILayout.TextField("Asset Id", _assetEntrySnippetId);
            _assetEntrySnippetKind = EditorGUILayout.TextField("Kind", _assetEntrySnippetKind);
            _assetEntrySnippetPath = EditorGUILayout.TextField("Path", _assetEntrySnippetPath);
            _assetEntrySnippetCrc32 = EditorGUILayout.TextField("Crc32", _assetEntrySnippetCrc32);
            _assetEntrySnippetBytes = EditorGUILayout.LongField("Bytes (-1 auto)", _assetEntrySnippetBytes);
            _assetEntryReplaceExisting = EditorGUILayout.Toggle("Replace Existing On Apply", _assetEntryReplaceExisting);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Asset Snippet", GUILayout.Height(28f)))
                    GenerateAssetEntrySnippet();

                if (GUILayout.Button("Open Asset Snippet", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Generated/asset_entry_snippet.json");

                if (GUILayout.Button("Open Asset Snippet Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/create_asset_entry_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Asset Snippet", GUILayout.Height(28f)))
                    ApplyAssetEntrySnippet();

                if (GUILayout.Button("Open Asset Manifest", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Content/assets.h8manifest.json");

                if (GUILayout.Button("Open Assets Folder", GUILayout.Height(28f)))
                    RevealRelativePath("ModdingSDK/ExternalStarterKit/Content/Assets");

                if (GUILayout.Button("Open Asset Apply Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/apply_asset_entry_snippet.ps1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawAssetKindPicker()
        {
            int safeIndex = Mathf.Clamp(_assetKindPopupIndex, 0, AssetKindLabels.Length - 1);
            EditorGUI.BeginChangeCheck();
            safeIndex = EditorGUILayout.Popup("Asset Kind", safeIndex, AssetKindLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _assetKindPopupIndex = safeIndex;
                _assetEntrySnippetKind = AssetKindValues[safeIndex];
            }
        }

        private void DrawIdentityEditor()
        {
            EditorGUILayout.LabelField("Package Identity", EditorStyles.boldLabel);
            _modId = EditorGUILayout.TextField("Id", _modId);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            _author = EditorGUILayout.TextField("Author", _author);
            _version = EditorGUILayout.TextField("Version", _version);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Identity + Validate", GUILayout.Height(28f)))
                    ApplyIdentity();

                if (GUILayout.Button("Open Identity Tool", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tools/set_mod_identity.ps1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawValidationActions()
        {
            EditorGUILayout.LabelField("Validation And Review", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(IsToolRunning);
            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate + Build Review", GUILayout.Height(28f)))
                    RunStarterTool("Tools/prepare_mod.ps1", string.Empty, true);

                if (GUILayout.Button("Build Submission Package", GUILayout.Height(28f)))
                    RunStarterTool("Tools/build_submission_package.ps1", string.Empty, true);

                if (GUILayout.Button("Validate Structure Only", GUILayout.Height(28f)))
                    RunStarterTool("Tools/validate_structure.ps1", string.Empty, true);
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("List Graph Opcodes", GUILayout.Height(28f)))
                    RunStarterTool("Tools/list_allowed_opcodes.ps1", string.Empty, false);

                if (GUILayout.Button("Open Submission Package", GUILayout.Height(28f)))
                    OpenSubmissionPackage();

                if (GUILayout.Button("Open Root Launcher", GUILayout.Height(28f)))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/h8mod.ps1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawFileActions()
        {
            EditorGUILayout.LabelField("Authoring Files", EditorStyles.boldLabel);

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Authoring Manifest"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/mod.h8manifest.json");

                if (GUILayout.Button("Runtime Manifest"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/mod.json");

                if (GUILayout.Button("Capabilities Guide"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Docs/capabilities.md");
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Command Graph"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Graphs/main.h8graph.json");

                if (GUILayout.Button("Settings Table"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tables/settings.h8table.json");

                if (GUILayout.Button("Locale"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Locales/en.h8loc.json");

                if (GUILayout.Button("Review Manifest"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Reports/review_manifest.json");
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Submission Package"))
                    OpenSubmissionPackage();

                if (GUILayout.Button("Generated Folder"))
                    RevealRelativePath("ModdingSDK/ExternalStarterKit/Generated");
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("VS Code Settings"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/.vscode/settings.json");

                if (GUILayout.Button("VS Code Tasks"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/.vscode/tasks.json");
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Docs And Contracts", EditorStyles.boldLabel);

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("File Contract"))
                    OpenRelativePath("Docs/Modding/External_Starter_Kit_File_Contract.md");

                if (GUILayout.Button("API Spec"))
                    OpenRelativePath("Docs/Modding/Mod_API_Specification.md");

                if (GUILayout.Button("Authoring Plan"))
                    OpenRelativePath("Docs/Modding/SDK_Authoring_Interface_Plan.md");

                if (GUILayout.Button("Runtime Playbook"))
                    OpenRelativePath("Docs/Modding/Runtime_Verification_Playbook.md");
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Review Summary", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_reviewSummary, MessageType.Info);

            EditorGUILayout.LabelField("Review Freshness", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_reviewFreshnessSummary, _reviewFreshnessWarning ? MessageType.Warning : MessageType.Info);

            EditorGUILayout.LabelField("Submission Package", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_submissionSummary, _submissionWarning ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_toolSummary))
            {
                EditorGUILayout.LabelField("Last Tool Output", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_toolSummary, _toolSummaryIsError ? MessageType.Error : MessageType.Info);
            }

            if (IsToolRunning)
                EditorGUILayout.HelpBox("Tool running: " + _runningToolName, MessageType.Info);
        }

        private void ApplyIdentity()
        {
            string arguments =
                " -Id " + QuoteArgument(_modId) +
                " -DisplayName " + QuoteArgument(_displayName) +
                " -Author " + QuoteArgument(_author) +
                " -Version " + QuoteArgument(_version);
            RunStarterTool("Tools/set_mod_identity.ps1", arguments, true);
        }

        private void GenerateGraphNodeSnippet()
        {
            string arguments =
                " -Id " + QuoteArgument(_graphNodeSnippetId) +
                " -Opcode " + QuoteArgument(_graphNodeSnippetOpcode) +
                " -ParametersJson " + QuoteArgument(_graphNodeParametersJson);
            if (_graphNodeDisabled)
                arguments += " -Disabled";

            RunStarterTool("Tools/create_graph_node_snippet.ps1", arguments, false);
        }

        private void ApplyGraphNodeSnippet()
        {
            string arguments = _graphNodeReplaceExisting ? " -Replace" : string.Empty;
            RunStarterTool("Tools/apply_graph_node_snippet.ps1", arguments, true);
        }

        private void GenerateSettingsRowSnippet()
        {
            string arguments =
                " -Id " + QuoteArgument(_settingsRowSnippetId) +
                " -Kind " + QuoteArgument(_settingsRowSnippetKind) +
                " -Default " + QuoteArgument(_settingsRowSnippetDefault);
            RunStarterTool("Tools/create_settings_row_snippet.ps1", arguments, false);
        }

        private void GenerateLocaleEntrySnippet()
        {
            string arguments =
                " -Key " + QuoteArgument(_localeEntrySnippetKey) +
                " -Value " + QuoteArgument(_localeEntrySnippetValue);
            RunStarterTool("Tools/create_locale_entry_snippet.ps1", arguments, false);
        }

        private void ApplySettingsRowSnippet()
        {
            RunStarterTool("Tools/apply_settings_row_snippet.ps1", string.Empty, true);
        }

        private void ApplyLocaleEntrySnippet()
        {
            RunStarterTool("Tools/apply_locale_entry_snippet.ps1", string.Empty, true);
        }

        private void GenerateAssetEntrySnippet()
        {
            string arguments =
                " -Id " + QuoteArgument(_assetEntrySnippetId) +
                " -Kind " + QuoteArgument(_assetEntrySnippetKind) +
                " -Path " + QuoteArgument(_assetEntrySnippetPath) +
                " -Crc32 " + QuoteArgument(_assetEntrySnippetCrc32) +
                " -Bytes " + _assetEntrySnippetBytes.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            RunStarterTool("Tools/create_asset_entry_snippet.ps1", arguments, false);
        }

        private void ApplyAssetEntrySnippet()
        {
            string arguments = _assetEntryReplaceExisting ? " -Replace" : string.Empty;
            RunStarterTool("Tools/apply_asset_entry_snippet.ps1", arguments, true);
        }

        private void ConfigureManifestContract()
        {
            int capabilityIndex = Mathf.Clamp(_manifestCapabilityPopupIndex, 0, ManifestCapabilityValues.Length - 1);
            int actionIndex = Mathf.Clamp(_manifestCapabilityActionPopupIndex, 0, ManifestCapabilityActionValues.Length - 1);
            string arguments =
                " -Capability " + QuoteArgument(ManifestCapabilityValues[capabilityIndex]) +
                " -CapabilityState " + QuoteArgument(ManifestCapabilityActionValues[actionIndex]) +
                " -MaxEnvelopesPerFrame " + _manifestMaxEnvelopesPerFrame.ToString(global::System.Globalization.CultureInfo.InvariantCulture) +
                " -MaxAssetBytes " + _manifestMaxAssetBytes.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            RunStarterTool("Tools/configure_manifest_contract.ps1", arguments, true);
        }

        private void CreateOrRefreshStarterKit()
        {
            ModdingSdkHubWindow.CreateExternalStarterKit();
            Reload();
        }

        private void Reload()
        {
            LoadStarterHealth();
            LoadIdentity();
            LoadCapabilityMatrix();
            LoadGraphContractPreview();
            LoadGraphOpcodePicker();
            LoadAuthoringDataPreview();
            LoadReviewSummary();
            LoadSubmissionSummary();
            Repaint();
        }

        private void LoadGraphOpcodePicker()
        {
            string allowedOpcodesPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/Reference/allowed_opcodes.csv");
            if (!File.Exists(allowedOpcodesPath))
            {
                SetDefaultGraphOpcodePicker();
                return;
            }

            try
            {
                AllowedGraphOpcodeSet allowedOpcodes = LoadAllowedGraphOpcodes(allowedOpcodesPath);
                if (allowedOpcodes.Choices.Count == 0)
                {
                    SetDefaultGraphOpcodePicker();
                    return;
                }

                string[] labels = new string[allowedOpcodes.Choices.Count];
                string[] values = new string[allowedOpcodes.Choices.Count];
                int selectedIndex = 0;
                for (int i = 0; i < allowedOpcodes.Choices.Count; i++)
                {
                    AllowedGraphOpcodeChoice choice = allowedOpcodes.Choices[i];
                    labels[i] = choice.Label;
                    values[i] = choice.Value;
                    if (string.Equals(choice.Value, _graphNodeSnippetOpcode, StringComparison.Ordinal) ||
                        string.Equals(choice.Hex, _graphNodeSnippetOpcode, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }

                _graphOpcodePopupLabels = labels;
                _graphOpcodePopupValues = values;
                _graphOpcodePopupIndex = selectedIndex;
            }
            catch
            {
                SetDefaultGraphOpcodePicker();
            }
        }

        private void SetDefaultGraphOpcodePicker()
        {
            _graphOpcodePopupLabels = new[] { "SpawnItem (0x3A3DA9C4)" };
            _graphOpcodePopupValues = new[] { "SpawnItem" };
            _graphOpcodePopupIndex = 0;
        }

        private void LoadStarterHealth()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            int presentCount = 0;
            long totalBytes = 0L;
            DateTime newestWrite = DateTime.MinValue;
            StringBuilder details = new StringBuilder(512);

            for (int i = 0; i < RequiredStarterFiles.Length; i++)
            {
                string relativePath = RequiredStarterFiles[i];
                string fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    details.Append("MISSING ").Append(relativePath).AppendLine();
                    continue;
                }

                FileInfo info = new FileInfo(fullPath);
                presentCount++;
                totalBytes += info.Length;
                if (info.LastWriteTime > newestWrite)
                    newestWrite = info.LastWriteTime;

                details.Append("OK ").Append(relativePath).Append(" (").Append(info.Length).Append(" bytes)").AppendLine();
            }

            int missingCount = RequiredStarterFiles.Length - presentCount;
            _starterHealthHasMissingFiles = missingCount > 0;
            StringBuilder summary = new StringBuilder(256);
            summary.Append("Required files: ").Append(presentCount).Append("/").Append(RequiredStarterFiles.Length);
            summary.AppendLine().Append("Starter bytes: ").Append(totalBytes);
            if (newestWrite > DateTime.MinValue)
                summary.AppendLine().Append("Newest starter file: ").Append(newestWrite.ToString("yyyy-MM-dd HH:mm:ss"));
            if (missingCount > 0)
                summary.AppendLine().Append("Missing required files: ").Append(missingCount).Append(". Use Create/Refresh Starter Kit.");
            else
                summary.AppendLine().Append("Missing required files: 0. Run Validate Structure Only for contract proof.");

            _starterHealthSummary = summary.ToString();
            _starterHealthDetails = details.ToString();
        }

        private void LoadCapabilityMatrix()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            string authoringPath = Path.Combine(rootPath, "mod.h8manifest.json");
            string runtimePath = Path.Combine(rootPath, "mod.json");
            string graphPath = Path.Combine(rootPath, "Graphs", "main.h8graph.json");
            string assetsPath = Path.Combine(rootPath, "Content", "assets.h8manifest.json");
            string settingsPath = Path.Combine(rootPath, "Tables", "settings.h8table.json");
            string localePath = Path.Combine(rootPath, "Locales", "en.h8loc.json");
            string guidePath = Path.Combine(rootPath, "Docs", "capabilities.md");
            string allowedOpcodesPath = Path.Combine(rootPath, "Reference", "allowed_opcodes.csv");

            if (!File.Exists(authoringPath))
            {
                _capabilityMatrixSummary = "Capability Matrix unavailable: missing mod.h8manifest.json.";
                _capabilityMatrixDetails = string.Empty;
                _capabilityMatrixWarning = true;
                return;
            }

            try
            {
                AuthoringManifest manifest = JsonUtility.FromJson<AuthoringManifest>(File.ReadAllText(authoringPath));
                if (manifest == null)
                {
                    _capabilityMatrixSummary = "Capability Matrix failed: mod.h8manifest.json parsed to null.";
                    _capabilityMatrixDetails = string.Empty;
                    _capabilityMatrixWarning = true;
                    return;
                }

                int declaredCapabilityCount = manifest.Capabilities != null ? manifest.Capabilities.Length : 0;
                int allowedOpcodeTokenCount = 0;
                int allowedOpcodeAliasCount = 0;
                if (File.Exists(allowedOpcodesPath))
                {
                    AllowedGraphOpcodeSet allowedOpcodes = LoadAllowedGraphOpcodes(allowedOpcodesPath);
                    allowedOpcodeTokenCount = allowedOpcodes.HexCount;
                    allowedOpcodeAliasCount = allowedOpcodes.AliasCount;
                }

                bool guideMissing = !File.Exists(guidePath);
                bool runtimeMissing = !File.Exists(runtimePath);
                bool graphMissing = !File.Exists(graphPath);
                bool assetsMissing = !File.Exists(assetsPath);
                bool settingsMissing = !File.Exists(settingsPath);
                bool localeMissing = !File.Exists(localePath);
                bool opcodeListMissing = !File.Exists(allowedOpcodesPath);

                StringBuilder summary = new StringBuilder(384);
                summary.Append("Runtime rights: envelope-only command requests after SDK/review approval.");
                summary.AppendLine().Append("Supported authoring surfaces: graph, settings, locale, content manifest, review/submission package.");
                summary.AppendLine().Append("Allowed graph opcode hex tokens: ").Append(allowedOpcodeTokenCount);
                summary.Append(" / aliases: ").Append(allowedOpcodeAliasCount);
                summary.AppendLine().Append("Declared authoring capabilities: ").Append(declaredCapabilityCount);
                summary.AppendLine().Append("Budget MaxEnvelopesPerFrame: ").Append(manifest.Budgets != null ? manifest.Budgets.MaxEnvelopesPerFrame : 0);
                summary.AppendLine().Append("Budget MaxAssetBytes: ").Append(manifest.Budgets != null ? manifest.Budgets.MaxAssetBytes : 0L);

                StringBuilder details = new StringBuilder(1024);
                details.AppendLine("SUPPORTED NOW");
                details.AppendLine("- Package identity and dependency metadata: mod.h8manifest.json + mod.json.");
                details.AppendLine("- Command graph draft: Graphs/main.h8graph.json, bounded by Reference/allowed_opcodes.csv and MaxEnvelopesPerFrame.");
                details.AppendLine("- Settings table draft: Tables/settings.h8table.json, validated before review handoff.");
                details.AppendLine("- Locale draft: Locales/en.h8loc.json, validated before review handoff.");
                details.AppendLine("- Content manifest draft: Content/assets.h8manifest.json plus Content/Assets/, generated/applied by asset entry tools; review/approval only before runtime use.");
                details.AppendLine("- Review and submission artifacts: Reports/review_manifest.json and Generated/<mod-id>_submission.zip.");
                details.AppendLine();
                details.AppendLine("NOT PUBLIC RIGHTS");
                details.AppendLine("- Managed gameplay DLLs, Harmony, BepInEx, arbitrary Unity scripts, direct GameObject/ScriptableObject/material mutation.");
                details.AppendLine("- Loose AssetBundle, PNG, audio, localization, or save-file runtime ingestion from the starter folder.");
                details.AppendLine("- New hot SignalBus lanes, GlobalRegistry polling routes, direct save/world/inventory authority, or frame callbacks.");
                details.AppendLine();
                details.AppendLine("CURRENT FILE STATE");
                details.Append("- Capabilities guide: ").Append(guideMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Runtime manifest: ").Append(runtimeMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Command graph: ").Append(graphMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Content manifest: ").Append(assetsMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Settings table: ").Append(settingsMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Locale table: ").Append(localeMissing ? "MISSING" : "OK").AppendLine();
                details.Append("- Opcode reference: ").Append(opcodeListMissing ? "MISSING" : "OK").AppendLine();

                if (declaredCapabilityCount == 0)
                    details.AppendLine("Capabilities array is empty. This is valid for the starter skeleton; add capability ids only when a review-owned contract exists.");
                else
                    details.Append("Declared capabilities: ").Append(string.Join(", ", manifest.Capabilities)).AppendLine();

                _capabilityMatrixWarning =
                    guideMissing ||
                    runtimeMissing ||
                    graphMissing ||
                    assetsMissing ||
                    settingsMissing ||
                    localeMissing ||
                    opcodeListMissing;
                _capabilityMatrixSummary = summary.ToString();
                _capabilityMatrixDetails = details.ToString();
            }
            catch (Exception exception)
            {
                _capabilityMatrixSummary = "Capability Matrix failed: " + exception.Message;
                _capabilityMatrixDetails = string.Empty;
                _capabilityMatrixWarning = true;
            }
        }

        private void LoadGraphContractPreview()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            string graphPath = Path.Combine(rootPath, "Graphs", "main.h8graph.json");
            string allowedOpcodesPath = Path.Combine(rootPath, "Reference", "allowed_opcodes.csv");
            if (!File.Exists(graphPath))
            {
                _graphContractPreviewSummary = "Graph Contract Preview unavailable: missing Graphs/main.h8graph.json.";
                _graphContractPreviewDetails = string.Empty;
                _graphContractPreviewWarning = true;
                return;
            }

            if (!File.Exists(allowedOpcodesPath))
            {
                _graphContractPreviewSummary = "Graph Contract Preview unavailable: missing Reference/allowed_opcodes.csv.";
                _graphContractPreviewDetails = string.Empty;
                _graphContractPreviewWarning = true;
                return;
            }

            try
            {
                if (new FileInfo(graphPath).Length > MaxGraphPreviewBytes)
                {
                    _graphContractPreviewSummary = "Graph Contract Preview unavailable: Graphs/main.h8graph.json exceeds preview byte cap.";
                    _graphContractPreviewDetails = string.Empty;
                    _graphContractPreviewWarning = true;
                    return;
                }

                if (new FileInfo(allowedOpcodesPath).Length > MaxAllowedOpcodePreviewBytes)
                {
                    _graphContractPreviewSummary = "Graph Contract Preview unavailable: Reference/allowed_opcodes.csv exceeds preview byte cap.";
                    _graphContractPreviewDetails = string.Empty;
                    _graphContractPreviewWarning = true;
                    return;
                }

                GraphDocument graph = JsonUtility.FromJson<GraphDocument>(File.ReadAllText(graphPath));
                if (graph == null)
                {
                    _graphContractPreviewSummary = "Graph Contract Preview failed: graph JSON parsed to null.";
                    _graphContractPreviewDetails = string.Empty;
                    _graphContractPreviewWarning = true;
                    return;
                }

                int budgetMax = LoadGraphBudgetMax();
                AllowedGraphOpcodeSet allowedOpcodes = LoadAllowedGraphOpcodes(allowedOpcodesPath);
                GraphNode[] nodes = graph.Nodes ?? new GraphNode[0];
                Dictionary<string, bool> nodeIds = new Dictionary<string, bool>(Math.Max(1, nodes.Length), StringComparer.Ordinal);
                StringBuilder details = new StringBuilder(512);
                int opcodeNodeCount = 0;
                int invalidOpcodeCount = 0;
                int duplicateNodeIdCount = 0;
                int missingFieldCount = 0;

                if (!string.Equals(graph.Runtime, "envelope-only", StringComparison.Ordinal))
                {
                    details.Append("INVALID Runtime: ").Append(graph.Runtime ?? string.Empty).AppendLine();
                    missingFieldCount++;
                }

                for (int i = 0; i < nodes.Length && i < MaxGraphPreviewNodes; i++)
                {
                    GraphNode node = nodes[i];
                    if (node == null)
                    {
                        details.Append("INVALID null node at index ").Append(i).AppendLine();
                        missingFieldCount++;
                        continue;
                    }

                    string nodeId = node.Id ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(nodeId))
                    {
                        details.Append("INVALID missing node Id at index ").Append(i).AppendLine();
                        missingFieldCount++;
                    }
                    else if (nodeIds.ContainsKey(nodeId))
                    {
                        details.Append("INVALID duplicate node Id: ").Append(nodeId).AppendLine();
                        duplicateNodeIdCount++;
                    }
                    else
                    {
                        nodeIds.Add(nodeId, true);
                    }

                    string opcode = node.Opcode ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(opcode))
                    {
                        details.Append("INVALID missing Opcode for node: ").Append(string.IsNullOrWhiteSpace(nodeId) ? "<missing-id>" : nodeId).AppendLine();
                        missingFieldCount++;
                        continue;
                    }

                    opcodeNodeCount++;
                    string opcodeToken = NormalizeGraphOpcodeToken(opcode.Trim());
                    if (!allowedOpcodes.Tokens.Contains(opcodeToken))
                    {
                        details.Append("INVALID opcode ").Append(opcode).Append(" on node ").Append(string.IsNullOrWhiteSpace(nodeId) ? "<missing-id>" : nodeId).AppendLine();
                        invalidOpcodeCount++;
                    }
                }

                bool nodeScanCapped = nodes.Length > MaxGraphPreviewNodes;
                if (nodeScanCapped)
                    details.Append("Graph preview capped at ").Append(MaxGraphPreviewNodes).Append(" nodes. Run Validate Structure Only.").AppendLine();

                if (opcodeNodeCount > 0 && graph.MaxEnvelopesPerFrame < 1)
                    details.Append("INVALID MaxEnvelopesPerFrame must be >= 1 when opcode nodes exist.").AppendLine();

                if (budgetMax >= 0 && graph.MaxEnvelopesPerFrame > budgetMax)
                    details.Append("INVALID MaxEnvelopesPerFrame exceeds authoring budget: ").Append(graph.MaxEnvelopesPerFrame).Append(" > ").Append(budgetMax).AppendLine();

                if (opcodeNodeCount == 0)
                    details.Append("Empty graph emits no runtime packets.").AppendLine();

                details.Append("Allowed opcode hex tokens: ").Append(allowedOpcodes.HexCount).AppendLine();
                details.Append("Allowed opcode aliases: ").Append(allowedOpcodes.AliasCount).AppendLine();

                StringBuilder summary = new StringBuilder(256);
                summary.Append("Runtime: ").Append(graph.Runtime ?? string.Empty);
                summary.AppendLine().Append("Nodes: ").Append(nodes.Length);
                summary.AppendLine().Append("Nodes scanned: ").Append(Math.Min(nodes.Length, MaxGraphPreviewNodes)).Append("/").Append(MaxGraphPreviewNodes);
                summary.AppendLine().Append("MaxEnvelopesPerFrame: ").Append(graph.MaxEnvelopesPerFrame);
                if (budgetMax >= 0)
                    summary.Append(" / authoring budget ").Append(budgetMax);
                summary.AppendLine().Append("Invalid opcodes: ").Append(invalidOpcodeCount);
                summary.AppendLine().Append("Duplicate node IDs: ").Append(duplicateNodeIdCount);
                summary.AppendLine().Append("Missing/invalid fields: ").Append(missingFieldCount);

                _graphContractPreviewWarning =
                    nodeScanCapped ||
                    invalidOpcodeCount > 0 ||
                    duplicateNodeIdCount > 0 ||
                    missingFieldCount > 0 ||
                    (opcodeNodeCount > 0 && graph.MaxEnvelopesPerFrame < 1) ||
                    (budgetMax >= 0 && graph.MaxEnvelopesPerFrame > budgetMax);
                _graphContractPreviewSummary = summary.ToString();
                _graphContractPreviewDetails = details.ToString();
            }
            catch (Exception exception)
            {
                _graphContractPreviewSummary = "Graph Contract Preview failed: " + exception.Message;
                _graphContractPreviewDetails = string.Empty;
                _graphContractPreviewWarning = true;
            }
        }

        private void LoadSubmissionSummary()
        {
            _submissionPackageRelativePath = string.Empty;
            string generatedPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/Generated");
            if (!Directory.Exists(generatedPath))
            {
                _submissionSummary = "Submission package unavailable: Generated folder is missing.";
                _submissionWarning = true;
                return;
            }

            try
            {
                FileInfo newestPackage = null;
                foreach (string packagePath in Directory.EnumerateFiles(generatedPath, "*_submission.zip", SearchOption.TopDirectoryOnly))
                {
                    FileInfo info = new FileInfo(packagePath);
                    if (newestPackage == null || info.LastWriteTime > newestPackage.LastWriteTime)
                        newestPackage = info;
                }

                if (newestPackage == null)
                {
                    _submissionSummary = "Submission package missing. Run Build Submission Package.";
                    _submissionWarning = true;
                    return;
                }

                string reviewPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/Reports/review_manifest.json");
                bool reviewExists = File.Exists(reviewPath);
                bool staleAgainstReview = reviewExists && File.GetLastWriteTime(reviewPath) > newestPackage.LastWriteTime.AddSeconds(1.0);
                _submissionPackageRelativePath = "ModdingSDK/ExternalStarterKit/Generated/" + newestPackage.Name;

                StringBuilder builder = new StringBuilder(256);
                builder.Append("Package: Generated/").Append(newestPackage.Name);
                builder.AppendLine().Append("Bytes: ").Append(newestPackage.Length);
                builder.AppendLine().Append("Written: ").Append(newestPackage.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                if (!reviewExists)
                    builder.AppendLine().Append("Review manifest missing. Run Validate + Build Review, then Build Submission Package.");
                else if (staleAgainstReview)
                    builder.AppendLine().Append("Package is older than Reports/review_manifest.json. Run Build Submission Package.");
                else
                    builder.AppendLine().Append("Package freshness: current against review manifest.");

                _submissionSummary = builder.ToString();
                _submissionWarning = !reviewExists || staleAgainstReview;
            }
            catch (Exception exception)
            {
                _submissionSummary = "Submission package status failed: " + exception.Message;
                _submissionWarning = true;
            }
        }

        private void LoadAuthoringDataPreview()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            string settingsPath = Path.Combine(rootPath, "Tables", "settings.h8table.json");
            string localePath = Path.Combine(rootPath, "Locales", "en.h8loc.json");
            string assetsPath = Path.Combine(rootPath, "Content", "assets.h8manifest.json");
            StringBuilder details = new StringBuilder(512);
            int invalidCount = 0;

            if (!File.Exists(settingsPath))
            {
                _authoringDataPreviewSummary = "Authoring Data Preview unavailable: missing Tables/settings.h8table.json.";
                _authoringDataPreviewDetails = string.Empty;
                _authoringDataPreviewWarning = true;
                return;
            }

            if (!File.Exists(localePath))
            {
                _authoringDataPreviewSummary = "Authoring Data Preview unavailable: missing Locales/en.h8loc.json.";
                _authoringDataPreviewDetails = string.Empty;
                _authoringDataPreviewWarning = true;
                return;
            }

            if (!File.Exists(assetsPath))
            {
                _authoringDataPreviewSummary = "Authoring Data Preview unavailable: missing Content/assets.h8manifest.json.";
                _authoringDataPreviewDetails = string.Empty;
                _authoringDataPreviewWarning = true;
                return;
            }

            try
            {
                if (new FileInfo(settingsPath).Length > MaxAuthoringDataPreviewBytes)
                {
                    _authoringDataPreviewSummary = "Authoring Data Preview unavailable: Tables/settings.h8table.json exceeds preview byte cap.";
                    _authoringDataPreviewDetails = string.Empty;
                    _authoringDataPreviewWarning = true;
                    return;
                }

                if (new FileInfo(localePath).Length > MaxAuthoringDataPreviewBytes)
                {
                    _authoringDataPreviewSummary = "Authoring Data Preview unavailable: Locales/en.h8loc.json exceeds preview byte cap.";
                    _authoringDataPreviewDetails = string.Empty;
                    _authoringDataPreviewWarning = true;
                    return;
                }

                if (new FileInfo(assetsPath).Length > MaxAuthoringDataPreviewBytes)
                {
                    _authoringDataPreviewSummary = "Authoring Data Preview unavailable: Content/assets.h8manifest.json exceeds preview byte cap.";
                    _authoringDataPreviewDetails = string.Empty;
                    _authoringDataPreviewWarning = true;
                    return;
                }

                SettingsTableDocument settings = JsonUtility.FromJson<SettingsTableDocument>(File.ReadAllText(settingsPath));
                LocaleDocument locale = JsonUtility.FromJson<LocaleDocument>(File.ReadAllText(localePath));
                AssetManifestDocument assets = JsonUtility.FromJson<AssetManifestDocument>(File.ReadAllText(assetsPath));
                SettingsRow[] rows = settings != null && settings.Rows != null ? settings.Rows : new SettingsRow[0];
                AssetEntry[] assetEntries = assets != null && assets.Assets != null ? assets.Assets : new AssetEntry[0];
                HashSet<string> settingIds = new HashSet<string>(StringComparer.Ordinal);
                int duplicateSettings = 0;
                int invalidSettingRows = 0;
                int invalidSettingKinds = 0;
                int invalidAssetEntries = 0;
                int duplicateAssets = 0;
                int missingAssetFiles = 0;
                long contentBytes = 0L;
                HashSet<string> assetIds = new HashSet<string>(StringComparer.Ordinal);

                if (settings == null || !string.Equals(settings.Schema, "hecton8.settings_table.draft.v1", StringComparison.Ordinal))
                {
                    details.Append("INVALID settings Schema must be hecton8.settings_table.draft.v1.").AppendLine();
                    invalidCount++;
                }

                for (int i = 0; i < rows.Length && i < MaxSettingsPreviewRows; i++)
                {
                    SettingsRow row = rows[i];
                    if (row == null)
                    {
                        details.Append("INVALID null settings row at index ").Append(i).AppendLine();
                        invalidSettingRows++;
                        continue;
                    }

                    string rowId = row.Id ?? string.Empty;
                    if (!IsCanonicalAuthoringKey(rowId))
                    {
                        details.Append("INVALID settings row Id at index ").Append(i).Append(": ").Append(rowId).AppendLine();
                        invalidSettingRows++;
                    }
                    else if (!settingIds.Add(rowId))
                    {
                        details.Append("INVALID duplicate settings row Id: ").Append(rowId).AppendLine();
                        duplicateSettings++;
                    }

                    string kind = row.Kind ?? string.Empty;
                    if (!IsSupportedSettingKind(kind))
                    {
                        details.Append("INVALID settings Kind for ").Append(string.IsNullOrWhiteSpace(rowId) ? "<missing-id>" : rowId).Append(": ").Append(kind).AppendLine();
                        invalidSettingKinds++;
                    }
                }

                bool settingsScanCapped = rows.Length > MaxSettingsPreviewRows;
                if (settingsScanCapped)
                    details.Append("Settings preview capped at ").Append(MaxSettingsPreviewRows).Append(" rows. Run Validate Structure Only.").AppendLine();

                if (assets == null || !string.Equals(assets.Schema, "hecton8.assets.draft.v1", StringComparison.Ordinal))
                {
                    details.Append("INVALID asset manifest Schema must be hecton8.assets.draft.v1.").AppendLine();
                    invalidCount++;
                }

                for (int i = 0; i < assetEntries.Length && i < MaxAssetPreviewEntries; i++)
                {
                    AssetEntry asset = assetEntries[i];
                    if (asset == null)
                    {
                        details.Append("INVALID null asset entry at index ").Append(i).AppendLine();
                        invalidAssetEntries++;
                        continue;
                    }

                    string assetId = asset.Id ?? string.Empty;
                    if (!IsCanonicalAuthoringKey(assetId))
                    {
                        details.Append("INVALID asset Id at index ").Append(i).Append(": ").Append(assetId).AppendLine();
                        invalidAssetEntries++;
                    }
                    else if (!assetIds.Add(assetId))
                    {
                        details.Append("INVALID duplicate asset Id: ").Append(assetId).AppendLine();
                        duplicateAssets++;
                    }

                    string kind = asset.Kind ?? string.Empty;
                    string path = asset.Path ?? string.Empty;
                    if (!IsSupportedAssetKind(kind))
                    {
                        details.Append("INVALID asset Kind for ").Append(string.IsNullOrWhiteSpace(assetId) ? "<missing-id>" : assetId).Append(": ").Append(kind).AppendLine();
                        invalidAssetEntries++;
                        continue;
                    }

                    if (!IsSafeAssetPath(path, kind))
                    {
                        details.Append("INVALID asset Path for ").Append(string.IsNullOrWhiteSpace(assetId) ? "<missing-id>" : assetId).Append(": ").Append(path).AppendLine();
                        invalidAssetEntries++;
                        continue;
                    }

                    string fullAssetPath = Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fullAssetPath))
                    {
                        details.Append("MISSING asset file for ").Append(assetId).Append(": ").Append(path).AppendLine();
                        missingAssetFiles++;
                        continue;
                    }

                    long fileBytes = new FileInfo(fullAssetPath).Length;
                    contentBytes += fileBytes;
                    if (asset.Bytes != fileBytes)
                    {
                        details.Append("INVALID asset Bytes for ").Append(assetId).Append(": manifest ").Append(asset.Bytes).Append(", file ").Append(fileBytes).AppendLine();
                        invalidAssetEntries++;
                    }
                }

                bool assetsScanCapped = assetEntries.Length > MaxAssetPreviewEntries;
                if (assetsScanCapped)
                    details.Append("Asset preview capped at ").Append(MaxAssetPreviewEntries).Append(" entries. Run Validate Structure Only.").AppendLine();

                int localeStringCount = 0;
                int invalidLocaleKeys = 0;
                int invalidLocaleValues = 0;
                bool localeScanCapped = false;
                if (locale == null || !string.Equals(locale.Schema, "hecton8.locale.draft.v1", StringComparison.Ordinal))
                {
                    details.Append("INVALID locale Schema must be hecton8.locale.draft.v1.").AppendLine();
                    invalidCount++;
                }

                string localeId = locale != null ? locale.Locale ?? string.Empty : string.Empty;
                if (!IsLocaleCode(localeId))
                {
                    details.Append("INVALID Locale code: ").Append(localeId).AppendLine();
                    invalidCount++;
                }

                ValidateLocaleStringsPreview(
                    File.ReadAllText(localePath),
                    details,
                    out localeStringCount,
                    out invalidLocaleKeys,
                    out invalidLocaleValues,
                    out localeScanCapped);

                invalidCount += invalidSettingRows + duplicateSettings + invalidSettingKinds + invalidAssetEntries + duplicateAssets + missingAssetFiles + invalidLocaleKeys + invalidLocaleValues;
                StringBuilder summary = new StringBuilder(256);
                summary.Append("Settings rows: ").Append(rows.Length).Append("/").Append(MaxSettingsPreviewRows);
                summary.AppendLine().Append("Invalid settings rows: ").Append(invalidSettingRows);
                summary.AppendLine().Append("Duplicate settings IDs: ").Append(duplicateSettings);
                summary.AppendLine().Append("Invalid settings kinds: ").Append(invalidSettingKinds);
                summary.AppendLine().Append("Content assets: ").Append(assetEntries.Length).Append("/").Append(MaxAssetPreviewEntries);
                summary.AppendLine().Append("Missing content files: ").Append(missingAssetFiles);
                summary.AppendLine().Append("Invalid content entries: ").Append(invalidAssetEntries + duplicateAssets);
                summary.AppendLine().Append("Content bytes: ").Append(contentBytes);
                summary.AppendLine().Append("Locale: ").Append(localeId);
                summary.AppendLine().Append("Locale strings: ").Append(localeStringCount).Append("/").Append(MaxLocalePreviewStrings);
                summary.AppendLine().Append("Invalid locale keys: ").Append(invalidLocaleKeys);
                summary.AppendLine().Append("Invalid locale values: ").Append(invalidLocaleValues);

                if (details.Length == 0)
                    details.Append("Settings/content/locale preview found no visible contract issues. Run Validate Structure Only for CRC/default type proof.").AppendLine();

                _authoringDataPreviewWarning = invalidCount > 0 || settingsScanCapped || assetsScanCapped || localeScanCapped;
                _authoringDataPreviewSummary = summary.ToString();
                _authoringDataPreviewDetails = details.ToString();
            }
            catch (Exception exception)
            {
                _authoringDataPreviewSummary = "Authoring Data Preview failed: " + exception.Message;
                _authoringDataPreviewDetails = string.Empty;
                _authoringDataPreviewWarning = true;
            }
        }

        private int LoadGraphBudgetMax()
        {
            string authoringPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/mod.h8manifest.json");
            if (!File.Exists(authoringPath))
                return -1;

            AuthoringManifest manifest = JsonUtility.FromJson<AuthoringManifest>(File.ReadAllText(authoringPath));
            return manifest != null && manifest.Budgets != null ? manifest.Budgets.MaxEnvelopesPerFrame : -1;
        }

        private static AllowedGraphOpcodeSet LoadAllowedGraphOpcodes(string allowedOpcodesPath)
        {
            AllowedGraphOpcodeSet result = new AllowedGraphOpcodeSet();
            int lineCount = 0;
            foreach (string line in File.ReadLines(allowedOpcodesPath))
            {
                lineCount++;
                if (lineCount > MaxAllowedOpcodePreviewRows)
                    throw new InvalidDataException("Reference/allowed_opcodes.csv exceeds preview row cap.");

                string text = line ?? string.Empty;
                string comment = string.Empty;
                int commentIndex = text.IndexOf('#');
                if (commentIndex >= 0)
                {
                    comment = text.Substring(commentIndex + 1).Trim();
                    text = text.Substring(0, commentIndex).Trim();
                }
                else
                {
                    text = text.Trim();
                }

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (!IsGraphOpcodeHexToken(text))
                    throw new InvalidDataException("Reference/allowed_opcodes.csv contains invalid opcode token: " + text);

                string hexToken = NormalizeGraphOpcodeToken(text);
                if (!result.Tokens.Add(hexToken))
                    throw new InvalidDataException("Reference/allowed_opcodes.csv contains duplicate opcode token: " + hexToken);

                result.HexCount++;
                string alias = string.Empty;

                if (!string.IsNullOrWhiteSpace(comment))
                {
                    string[] commentParts = comment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (commentParts.Length > 0 && IsGraphOpcodeAlias(commentParts[0]))
                    {
                        alias = commentParts[0];
                        if (!result.Tokens.Add(alias))
                            throw new InvalidDataException("Reference/allowed_opcodes.csv contains duplicate opcode alias: " + alias);

                        result.AliasCount++;
                    }
                }

                AllowedGraphOpcodeChoice choice = new AllowedGraphOpcodeChoice();
                choice.Hex = hexToken;
                choice.Value = string.IsNullOrWhiteSpace(alias) ? hexToken : alias;
                choice.Label = string.IsNullOrWhiteSpace(alias) ? hexToken : alias + " (" + hexToken + ")";
                result.Choices.Add(choice);
            }

            if (result.HexCount == 0)
                throw new InvalidDataException("Reference/allowed_opcodes.csv has no allowed graph opcodes.");

            return result;
        }

        private static string NormalizeGraphOpcodeToken(string opcode)
        {
            if (opcode.Length > 2 &&
                opcode[0] == '0' &&
                (opcode[1] == 'x' || opcode[1] == 'X'))
            {
                return opcode.ToUpperInvariant();
            }

            return opcode;
        }

        private static bool IsGraphOpcodeHexToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length < 3 ||
                value.Length > 10 ||
                value[0] != '0' ||
                (value[1] != 'x' && value[1] != 'X'))
            {
                return false;
            }

            for (int i = 2; i < value.Length; i++)
            {
                char current = value[i];
                bool isHex =
                    (current >= '0' && current <= '9') ||
                    (current >= 'a' && current <= 'f') ||
                    (current >= 'A' && current <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        private static bool IsGraphOpcodeAlias(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]))
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                char current = value[i];
                if (!char.IsLetterOrDigit(current) && current != '_')
                    return false;
            }

            return true;
        }

        private static bool IsCanonicalAuthoringKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Regex.IsMatch(value, "^[a-z0-9]+([._-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
        }

        private static bool IsSupportedSettingKind(string value)
        {
            switch (value)
            {
                case "bool":
                case "int":
                case "float":
                case "string":
                case "enum":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSupportedAssetKind(string value)
        {
            switch (value)
            {
                case "data_blob":
                case "raw_texture":
                case "audio_clip":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSafeAssetPath(string value, string kind)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value != value.Trim() ||
                Path.IsPathRooted(value) ||
                value.StartsWith("../", StringComparison.Ordinal) ||
                value.Contains("/../") ||
                value.Contains("..") ||
                !value.StartsWith("Content/Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            string extension = Path.GetExtension(value).ToLowerInvariant();
            switch (kind)
            {
                case "data_blob":
                    return extension == ".json" || extension == ".bytes" || extension == ".bin";
                case "raw_texture":
                    return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp";
                case "audio_clip":
                    return extension == ".wav" || extension == ".ogg";
                default:
                    return false;
            }
        }

        private static bool IsLocaleCode(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Regex.IsMatch(value, "^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.CultureInvariant);
        }

        private static void ValidateLocaleStringsPreview(
            string localeJson,
            StringBuilder details,
            out int stringCount,
            out int invalidKeys,
            out int invalidValues,
            out bool scanCapped)
        {
            stringCount = 0;
            invalidKeys = 0;
            invalidValues = 0;
            scanCapped = false;

            if (!TryExtractJsonObjectBody(localeJson, "Strings", out string stringsBody))
            {
                details.Append("INVALID locale Strings object is missing.").AppendLine();
                invalidValues++;
                return;
            }

            int index = 0;
            while (index < stringsBody.Length)
            {
                SkipJsonWhitespace(stringsBody, ref index);
                if (index >= stringsBody.Length)
                    break;

                if (stringsBody[index] == ',')
                {
                    index++;
                    continue;
                }

                if (!TryReadJsonString(stringsBody, ref index, out string key))
                {
                    details.Append("INVALID locale Strings entry key near index ").Append(index).AppendLine();
                    invalidKeys++;
                    break;
                }

                SkipJsonWhitespace(stringsBody, ref index);
                if (index >= stringsBody.Length || stringsBody[index] != ':')
                {
                    details.Append("INVALID locale Strings entry missing ':' for key ").Append(key).AppendLine();
                    invalidValues++;
                    break;
                }

                index++;
                SkipJsonWhitespace(stringsBody, ref index);
                if (!TryReadJsonString(stringsBody, ref index, out string value))
                {
                    details.Append("INVALID locale value for key ").Append(key).AppendLine();
                    invalidValues++;
                    break;
                }

                stringCount++;
                if (!IsCanonicalAuthoringKey(key))
                {
                    details.Append("INVALID locale key: ").Append(key).AppendLine();
                    invalidKeys++;
                }

                if (string.IsNullOrWhiteSpace(value) || value.Trim() != value)
                {
                    details.Append("INVALID locale value text for key ").Append(key).AppendLine();
                    invalidValues++;
                }

                if (stringCount > MaxLocalePreviewStrings)
                {
                    scanCapped = true;
                    stringCount = MaxLocalePreviewStrings;
                    details.Append("Locale preview capped at ").Append(MaxLocalePreviewStrings).Append(" strings. Run Validate Structure Only.").AppendLine();
                    return;
                }
            }
        }

        private static bool TryExtractJsonObjectBody(string json, string propertyName, out string body)
        {
            body = string.Empty;
            string marker = "\"" + propertyName + "\"";
            int propertyIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (propertyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(':', propertyIndex + marker.Length);
            if (colonIndex < 0)
                return false;

            int openIndex = json.IndexOf('{', colonIndex + 1);
            if (openIndex < 0)
                return false;

            bool inString = false;
            bool escaping = false;
            int depth = 0;
            for (int i = openIndex; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                    }
                    else if (current == '\\')
                    {
                        escaping = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current != '}')
                    continue;

                depth--;
                if (depth == 0)
                {
                    body = json.Substring(openIndex + 1, i - openIndex - 1);
                    return true;
                }
            }

            return false;
        }

        private static void SkipJsonWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static bool TryReadJsonString(string text, ref int index, out string value)
        {
            value = string.Empty;
            if (index >= text.Length || text[index] != '"')
                return false;

            index++;
            StringBuilder builder = new StringBuilder(64);
            bool escaping = false;
            while (index < text.Length)
            {
                char current = text[index++];
                if (escaping)
                {
                    builder.Append(current);
                    escaping = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                builder.Append(current);
            }

            return false;
        }

        private void LoadIdentity()
        {
            string authoringPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/mod.h8manifest.json");
            if (!File.Exists(authoringPath))
            {
                _modId = string.Empty;
                _displayName = string.Empty;
                _author = string.Empty;
                _version = string.Empty;
                return;
            }

            try
            {
                AuthoringManifest manifest = JsonUtility.FromJson<AuthoringManifest>(File.ReadAllText(authoringPath));
                _modId = manifest.Id ?? string.Empty;
                _displayName = manifest.DisplayName ?? string.Empty;
                _author = manifest.Author ?? string.Empty;
                _version = manifest.Version ?? string.Empty;
                if (manifest.Budgets != null)
                {
                    _manifestMaxEnvelopesPerFrame = manifest.Budgets.MaxEnvelopesPerFrame;
                    _manifestMaxAssetBytes = manifest.Budgets.MaxAssetBytes;
                }
            }
            catch (Exception exception)
            {
                _toolSummary = "Identity load failed: " + exception.Message;
                _toolSummaryIsError = true;
            }
        }

        private void LoadReviewSummary()
        {
            string reviewPath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/Reports/review_manifest.json");
            if (!File.Exists(reviewPath))
            {
                _reviewSummary = "Review manifest missing. Run Validate + Build Review.";
                _reviewFreshnessSummary = "Review freshness unavailable: Reports/review_manifest.json is missing. Run Validate + Build Review.";
                _reviewFreshnessWarning = true;
                return;
            }

            try
            {
                ReviewManifest review = JsonUtility.FromJson<ReviewManifest>(File.ReadAllText(reviewPath));
                ReviewIdentity identity = review.Identity ?? new ReviewIdentity();
                StringBuilder builder = new StringBuilder(256);
                builder.Append("Id: ").Append(!string.IsNullOrWhiteSpace(identity.Id) ? identity.Id : review.RootId);
                builder.AppendLine().Append("Name: ").Append(identity.DisplayName ?? string.Empty);
                builder.AppendLine().Append("Author: ").Append(identity.Author ?? string.Empty);
                builder.AppendLine().Append("Version: ").Append(identity.Version ?? string.Empty);
                builder.AppendLine().Append("Files: ").Append(review.FileCount);
                builder.AppendLine().Append("Total bytes: ").Append(review.TotalBytes);
                _reviewSummary = builder.ToString();
                LoadReviewFreshness(reviewPath);
            }
            catch (Exception exception)
            {
                _reviewSummary = "Review manifest parse failed: " + exception.Message;
                _reviewFreshnessSummary = "Review freshness unavailable: review manifest parse failed.";
                _reviewFreshnessWarning = true;
            }
        }

        private void LoadReviewFreshness(string reviewPath)
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            try
            {
                DateTime reviewWrite = File.GetLastWriteTime(reviewPath);
                if (!TryFindNewestReviewSource(rootPath, out string newestRelativePath, out DateTime newestSourceWrite, out int scannedFileCount, out bool scanCapped))
                {
                    _reviewFreshnessSummary = "Review freshness unavailable: no source files found.";
                    _reviewFreshnessWarning = true;
                    return;
                }

                bool stale = newestSourceWrite > reviewWrite.AddSeconds(1.0);
                StringBuilder builder = new StringBuilder(256);
                builder.Append("Report written: ").Append(reviewWrite.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine().Append("Newest source: ").Append(newestSourceWrite.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine().Append("Newest source file: ").Append(newestRelativePath);
                builder.AppendLine().Append("Source files scanned: ").Append(scannedFileCount).Append("/").Append(MaxFreshnessScanFiles);
                if (scanCapped)
                    builder.AppendLine().Append("Freshness scan capped. Run Validate + Build Review.");
                else if (stale)
                    builder.AppendLine().Append("Report is stale. Run Validate + Build Review.");
                else
                    builder.AppendLine().Append("Report freshness: current.");

                _reviewFreshnessSummary = builder.ToString();
                _reviewFreshnessWarning = stale || scanCapped;
            }
            catch (Exception exception)
            {
                _reviewFreshnessSummary = "Review freshness failed: " + exception.Message;
                _reviewFreshnessWarning = true;
            }
        }

        private static bool TryFindNewestReviewSource(
            string rootPath,
            out string newestRelativePath,
            out DateTime newestSourceWrite,
            out int scannedFileCount,
            out bool scanCapped)
        {
            newestRelativePath = string.Empty;
            newestSourceWrite = DateTime.MinValue;
            scannedFileCount = 0;
            scanCapped = false;

            if (!Directory.Exists(rootPath))
                return false;

            foreach (string sourcePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = ToStarterRelativePath(rootPath, sourcePath);
                if (!IsReviewSourcePath(relativePath))
                    continue;

                scannedFileCount++;
                if (scannedFileCount > MaxFreshnessScanFiles)
                {
                    scanCapped = true;
                    scannedFileCount = MaxFreshnessScanFiles;
                    break;
                }

                DateTime sourceWrite = File.GetLastWriteTime(sourcePath);
                if (sourceWrite <= newestSourceWrite)
                    continue;

                newestSourceWrite = sourceWrite;
                newestRelativePath = relativePath;
            }

            return newestSourceWrite > DateTime.MinValue;
        }

        private static bool IsReviewSourcePath(string relativePath)
        {
            return !relativePath.StartsWith("Generated/", StringComparison.OrdinalIgnoreCase) &&
                   !relativePath.StartsWith("Reports/", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenSubmissionPackage()
        {
            if (string.IsNullOrWhiteSpace(_submissionPackageRelativePath))
            {
                RevealRelativePath("ModdingSDK/ExternalStarterKit/Generated");
                return;
            }

            OpenRelativePath(_submissionPackageRelativePath);
        }

        private static string ToStarterRelativePath(string rootPath, string fullPath)
        {
            string normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedFull = Path.GetFullPath(fullPath);
            if (normalizedFull.Length > normalizedRoot.Length &&
                normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedFull.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            }

            return normalizedFull.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private void RunStarterTool(string scriptRelativePath, string extraArguments, bool reloadAfterSuccess)
        {
            if (IsToolRunning)
            {
                _toolSummary = "Tool already running: " + _runningToolName;
                _toolSummaryIsError = false;
                Repaint();
                return;
            }

            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            string scriptPath = Path.Combine(rootPath, scriptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptPath))
            {
                _toolSummary = "Missing starter tool: " + scriptPath;
                _toolSummaryIsError = true;
                Repaint();
                return;
            }

            try
            {
                DiagnosticsProcessStartInfo startInfo = new DiagnosticsProcessStartInfo
                {
                    FileName = ResolvePowerShellExecutable(),
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(scriptPath) + " -Root " + QuoteArgument(rootPath) + extraArguments,
                    WorkingDirectory = rootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                DiagnosticsProcess process = new DiagnosticsProcess
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                _runningToolStdout = new StringBuilder(1024);
                _runningToolStderr = new StringBuilder(1024);
                _runningToolName = scriptRelativePath;
                _runningToolReloadAfterSuccess = reloadAfterSuccess;
                _runningToolCompleted = false;
                _runningToolExitCode = -1;
                _runningToolProcess = process;

                process.OutputDataReceived += (sender, args) => AppendToolOutput(_runningToolStdout, args.Data);
                process.ErrorDataReceived += (sender, args) => AppendToolOutput(_runningToolStderr, args.Data);
                process.Exited += (sender, args) => MarkToolCompleted();

                if (!process.Start())
                {
                    _toolSummary = "Tool process did not start.";
                    _toolSummaryIsError = true;
                    DisposeRunningTool();
                    Repaint();
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _toolSummary = "Tool running: " + scriptRelativePath;
                _toolSummaryIsError = false;
                EditorApplication.update -= PollRunningTool;
                EditorApplication.update += PollRunningTool;
                Repaint();
            }
            catch (Exception exception)
            {
                _toolSummary = "Tool launch failed: " + exception.Message;
                _toolSummaryIsError = true;
                DisposeRunningTool();
                Debug.LogError("[ExternalStarterKitWorkbenchWindow] Tool launch failed: " + exception);
            }
        }

        private bool IsToolRunning
        {
            get { return _runningToolProcess != null; }
        }

        private void AppendToolOutput(StringBuilder builder, string line)
        {
            if (builder == null || line == null)
                return;

            lock (_toolOutputLock)
            {
                builder.AppendLine(line);
            }
        }

        private void MarkToolCompleted()
        {
            DiagnosticsProcess process = _runningToolProcess;
            if (process == null)
                return;

            try
            {
                _runningToolExitCode = process.ExitCode;
            }
            catch
            {
                _runningToolExitCode = -1;
            }

            _runningToolCompleted = true;
        }

        private void PollRunningTool()
        {
            if (!_runningToolCompleted)
                return;

            EditorApplication.update -= PollRunningTool;

            string stdout;
            string stderr;
            lock (_toolOutputLock)
            {
                stdout = _runningToolStdout != null ? _runningToolStdout.ToString() : string.Empty;
                stderr = _runningToolStderr != null ? _runningToolStderr.ToString() : string.Empty;
            }

            int exitCode = _runningToolExitCode;
            bool reloadAfterSuccess = _runningToolReloadAfterSuccess;
            _toolSummary = BuildToolSummary(exitCode, stdout, stderr);
            _toolSummaryIsError = exitCode != 0;
            DisposeRunningTool();

            if (reloadAfterSuccess && exitCode == 0)
                Reload();
            else
                Repaint();
        }

        private void DisposeRunningTool()
        {
            EditorApplication.update -= PollRunningTool;
            if (_runningToolProcess != null)
            {
                _runningToolProcess.Dispose();
                _runningToolProcess = null;
            }

            _runningToolStdout = null;
            _runningToolStderr = null;
            _runningToolName = string.Empty;
            _runningToolReloadAfterSuccess = false;
            _runningToolCompleted = false;
            _runningToolExitCode = -1;
        }

        private static string BuildToolSummary(int exitCode, string stdout, string stderr)
        {
            string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + global::System.Environment.NewLine + stderr;
            if (string.IsNullOrWhiteSpace(output))
                return "Tool exit code: " + exitCode + global::System.Environment.NewLine + "No output.";

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int firstLine = Math.Max(0, lines.Length - ToolSummaryLineCount);
            StringBuilder builder = new StringBuilder(512);
            builder.Append("Tool exit code: ").Append(exitCode);
            for (int i = firstLine; i < lines.Length; i++)
                builder.AppendLine().Append(lines[i]);

            return builder.ToString();
        }

        private static void OpenRelativePath(string relativePath)
        {
            string fullPath = ResolveProjectPath(relativePath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Missing Starter Kit File", fullPath, "OK");
                return;
            }

            EditorUtility.OpenWithDefaultApp(fullPath);
        }

        private static void RevealRelativePath(string relativePath)
        {
            string fullPath = ResolveProjectPath(relativePath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            EditorUtility.RevealInFinder(fullPath);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), relativePath));
        }

        private static string GetProjectRootPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("Project root could not be resolved from Application.dataPath.");

            return projectRoot;
        }

        private static string ResolvePowerShellExecutable()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ? "powershell.exe" : "pwsh";
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        [Serializable]
        private sealed class AuthoringManifest
        {
            public string Id = string.Empty;
            public string DisplayName = string.Empty;
            public string Author = string.Empty;
            public string Version = string.Empty;
            public string[] Capabilities = new string[0];
            public AuthoringBudgets Budgets = new AuthoringBudgets();
        }

        [Serializable]
        private sealed class AuthoringBudgets
        {
            public int MaxEnvelopesPerFrame = 0;
            public long MaxAssetBytes = 0L;
        }

        [Serializable]
        private sealed class GraphDocument
        {
            public string Runtime = string.Empty;
            public int MaxEnvelopesPerFrame = 0;
            public GraphNode[] Nodes = new GraphNode[0];
        }

        [Serializable]
        private sealed class GraphNode
        {
            public string Id = string.Empty;
            public string Opcode = string.Empty;
        }

        [Serializable]
        private sealed class SettingsTableDocument
        {
            public string Schema = string.Empty;
            public SettingsRow[] Rows = new SettingsRow[0];
        }

        [Serializable]
        private sealed class SettingsRow
        {
            public string Id = string.Empty;
            public string Kind = string.Empty;
        }

        [Serializable]
        private sealed class AssetManifestDocument
        {
            public string Schema = string.Empty;
            public AssetEntry[] Assets = new AssetEntry[0];
        }

        [Serializable]
        private sealed class AssetEntry
        {
            public string Id = string.Empty;
            public string Kind = string.Empty;
            public string Path = string.Empty;
            public string Crc32 = string.Empty;
            public long Bytes = 0L;
        }

        [Serializable]
        private sealed class LocaleDocument
        {
            public string Schema = string.Empty;
            public string Locale = string.Empty;
        }

        private sealed class AllowedGraphOpcodeSet
        {
            public readonly HashSet<string> Tokens = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<AllowedGraphOpcodeChoice> Choices = new List<AllowedGraphOpcodeChoice>();
            public int HexCount;
            public int AliasCount;
        }

        private sealed class AllowedGraphOpcodeChoice
        {
            public string Label = string.Empty;
            public string Value = string.Empty;
            public string Hex = string.Empty;
        }

        [Serializable]
        private sealed class ReviewManifest
        {
            public string RootId = string.Empty;
            public ReviewIdentity Identity = new ReviewIdentity();
            public int FileCount = 0;
            public long TotalBytes = 0L;
        }

        [Serializable]
        private sealed class ReviewIdentity
        {
            public string Id = string.Empty;
            public string DisplayName = string.Empty;
            public string Author = string.Empty;
            public string Version = string.Empty;
        }
    }
}
