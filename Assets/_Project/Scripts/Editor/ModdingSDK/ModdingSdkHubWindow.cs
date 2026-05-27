using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Hecton8.Editor.ModdingSDK
{
    /// <summary>
    /// Editor-side entry point for HECTON-8 mod SDK authoring, validation, and support docs.
    /// </summary>
    public sealed class ModdingSdkHubWindow : EditorWindow
    {
        private const string RuntimeBoundary =
            "Runtime API: envelope-only. Managed DLL entries are legacy/internal and disabled by the loader.";
        private const string ReadmePath = "Docs/Modding/README.md";
        private const string ApiSpecPath = "Docs/Modding/Mod_API_Specification.md";
        private const string AuthoringPlanPath = "Docs/Modding/SDK_Authoring_Interface_Plan.md";
        private const string ProductBlueprintPath = "Docs/Modding/SDK_Product_Blueprint.md";
        private const string SampleModPath = "Docs/Modding/Sample_InfiniteO2_Mod.md";
        private const string RuntimePlaybookPath = "Docs/Modding/Runtime_Verification_Playbook.md";
        private const string ExternalStarterKitContractPath = "Docs/Modding/External_Starter_Kit_File_Contract.md";
        private const string StaticValidatorPath = "Docs/Modding/Validate_Mod_API_Static.ps1";
        private const string ExternalStarterKitRoot = "ModdingSDK/ExternalStarterKit";
        private const string AllowedOpcodesReferencePath = "Docs/Modding/allowed_opcodes.csv";
        private const string KernelTuningProfilesReferencePath = "Docs/Modding/kernel_tuning_profiles.csv";
        private const int CurrentRequiredApiVersion = 2;
        private const int ValidatorSummaryLineCount = 12;

        private Vector2 _scrollPosition;
        private string _lastValidatorSummary = string.Empty;

        /// <summary>
        /// Opens the HECTON modding SDK hub window.
        /// </summary>
        [MenuItem("Hecton/Modding/SDK Hub")]
        public static void ShowWindow()
        {
            ModdingSdkHubWindow window = GetWindow<ModdingSdkHubWindow>("HECTON SDK Hub");
            window.minSize = new Vector2(640f, 440f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            using (EditorGUILayout.VerticalScope _ = new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("HECTON-8 Modding SDK", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(RuntimeBoundary, MessageType.Warning);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                DrawPrimaryActions();
                EditorGUILayout.Space(10f);
                DrawDocumentationActions();
                EditorGUILayout.Space(10f);
                DrawValidationActions();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPrimaryActions()
        {
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Mod Builder", GUILayout.Height(30f)))
                ModBuilderWindow.ShowWindow();

            if (GUILayout.Button("Create External Starter Kit", GUILayout.Height(24f)))
                CreateExternalStarterKit();

            if (GUILayout.Button("Open External Starter Kit", GUILayout.Height(24f)))
                RevealRelativePath(ExternalStarterKitRoot);

            if (GUILayout.Button("Open Local Mods Folder", GUILayout.Height(24f)))
                RevealRelativePath("Mods");
        }

        private void DrawDocumentationActions()
        {
            EditorGUILayout.LabelField("Contracts", EditorStyles.boldLabel);

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("README"))
                    OpenRelativePath(ReadmePath);

                if (GUILayout.Button("API Spec"))
                    OpenRelativePath(ApiSpecPath);

                if (GUILayout.Button("Runtime Playbook"))
                    OpenRelativePath(RuntimePlaybookPath);
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Starter Kit Files"))
                    OpenRelativePath(ExternalStarterKitContractPath);

                if (GUILayout.Button("Authoring Plan"))
                    OpenRelativePath(AuthoringPlanPath);

                if (GUILayout.Button("Product Blueprint"))
                    OpenRelativePath(ProductBlueprintPath);
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Sample Mod"))
                    OpenRelativePath(SampleModPath);
            }
        }

        private void DrawValidationActions()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Static Mod API Validator", GUILayout.Height(30f)))
                RunStaticValidator();

            if (!string.IsNullOrWhiteSpace(_lastValidatorSummary))
                EditorGUILayout.HelpBox(_lastValidatorSummary, MessageType.Info);
        }

        private void RunStaticValidator()
        {
            string scriptPath = ResolveProjectPath(StaticValidatorPath);
            if (!File.Exists(scriptPath))
            {
                _lastValidatorSummary = "Missing validator: " + scriptPath;
                return;
            }

            try
            {
                DiagnosticsProcessStartInfo startInfo = new DiagnosticsProcessStartInfo
                {
                    FileName = ResolvePowerShellExecutable(),
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(scriptPath),
                    WorkingDirectory = GetProjectRootPath(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (DiagnosticsProcess process = DiagnosticsProcess.Start(startInfo))
                {
                    if (process == null)
                    {
                        _lastValidatorSummary = "Validator process did not start.";
                        return;
                    }

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    _lastValidatorSummary = BuildValidatorSummary(process.ExitCode, stdout, stderr);
                }
            }
            catch (Exception exception)
            {
                _lastValidatorSummary = "Validator launch failed: " + exception.Message;
                Debug.LogError("[ModdingSdkHubWindow] Static validator launch failed: " + exception);
            }

            Repaint();
        }

        private void CreateExternalStarterKit()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);

            try
            {
                Directory.CreateDirectory(rootPath);
                Directory.CreateDirectory(Path.Combine(rootPath, "Content"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Graphs"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Tables"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Locales"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Generated"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Reports"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Reference"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Schemas"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Tools"));
                Directory.CreateDirectory(Path.Combine(rootPath, ".vscode"));

                int createdCount = 0;
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "README.md"), BuildStarterKitReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "mod.h8manifest.json"), BuildAuthoringManifestTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "mod.json"), BuildRuntimeManifestTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Content", "README.md"), BuildContentReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Content", "assets.h8manifest.json"), BuildAssetManifestTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Graphs", "main.h8graph.json"), BuildGraphTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tables", "settings.h8table.json"), BuildSettingsTableTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Locales", "en.h8loc.json"), BuildLocaleTemplate());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Generated", "README.md"), BuildGeneratedReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Reports", "README.md"), BuildReportsReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Reference", "README.md"), BuildReferenceReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "assets.schema.json"), BuildAssetsSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "h8graph.schema.json"), BuildGraphSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "h8mod.authoring.schema.json"), BuildAuthoringManifestSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "locale.schema.json"), BuildLocaleSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "runtime.mod.schema.json"), BuildRuntimeManifestSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "settings_table.schema.json"), BuildSettingsTableSchema());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "README.md"), BuildToolsReadme());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "build_review_manifest.ps1"), BuildStarterKitReviewManifestScript());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "list_allowed_opcodes.ps1"), BuildStarterKitAllowedOpcodesScript());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "prepare_mod.ps1"), BuildStarterKitPrepareScript());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "set_mod_identity.ps1"), BuildStarterKitIdentityScript());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "validate_structure.ps1"), BuildStarterKitValidatorScript());
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, ".vscode", "settings.json"), BuildVsCodeSettings());
                createdCount += CopyReferenceFileIfMissing(AllowedOpcodesReferencePath, Path.Combine(rootPath, "Reference", "allowed_opcodes.csv"));
                createdCount += CopyReferenceFileIfMissing(KernelTuningProfilesReferencePath, Path.Combine(rootPath, "Reference", "kernel_tuning_profiles.csv"));

                _lastValidatorSummary =
                    "External starter kit ready: " + rootPath + global::System.Environment.NewLine +
                    "Files created: " + createdCount + global::System.Environment.NewLine +
                    "Existing files were not overwritten.";
                EditorUtility.RevealInFinder(rootPath);
            }
            catch (Exception exception)
            {
                _lastValidatorSummary = "External starter kit creation failed: " + exception.Message;
                Debug.LogError("[ModdingSdkHubWindow] External starter kit creation failed: " + exception);
            }

            Repaint();
        }

        private static string BuildValidatorSummary(int exitCode, string stdout, string stderr)
        {
            string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + global::System.Environment.NewLine + stderr;
            if (string.IsNullOrWhiteSpace(output))
                return "Validator exit code: " + exitCode + global::System.Environment.NewLine + "No output.";

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int firstLine = Math.Max(0, lines.Length - ValidatorSummaryLineCount);

            StringBuilder builder = new StringBuilder(512);
            builder.Append("Validator exit code: ").Append(exitCode);
            for (int i = firstLine; i < lines.Length; i++)
                builder.AppendLine().Append(lines[i]);

            return builder.ToString();
        }

        private static int WriteTextFileIfMissing(string path, string contents)
        {
            if (File.Exists(path))
                return 0;

            File.WriteAllText(path, contents, new UTF8Encoding(false));
            return 1;
        }

        private static int CopyReferenceFileIfMissing(string sourceRelativePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                return 0;

            string sourcePath = ResolveProjectPath(sourceRelativePath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Missing SDK reference file.", sourcePath);

            File.Copy(sourcePath, destinationPath, false);
            return 1;
        }

        private static string BuildStarterKitReadme()
        {
            return
                "# HECTON-8 External Mod Starter Kit" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "This folder is for public mod authors working outside the HECTON-8 Unity project." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Fast path:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Use pwsh instead of powershell on macOS/Linux with PowerShell 7. The tools normalize child paths internally; do not rewrite the folder layout per platform." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Do you need Unity?" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- No Unity project is required for manifest, graph, table, locale, and validation authoring." + global::System.Environment.NewLine +
                "- Unity is only useful for advanced asset preview before a standalone Workbench or CLI exists." + global::System.Environment.NewLine +
                "- Do not ship Harmony, BepInEx, or gameplay DLL patches. Current runtime UGC ingress is envelope-only." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Current runtime boundary:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- managed DLL gameplay execution is disabled;" + global::System.Environment.NewLine +
                "- loose AssetBundle, PNG, and localization runtime ingestion are disabled;" + global::System.Environment.NewLine +
                "- supported gameplay ingress is validated 64-byte FutureCommandEnvelope packets after SDK bake/approval;" + global::System.Environment.NewLine +
                "- this starter kit is an authoring skeleton, not a runtime-verification stamp." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Files:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- mod.h8manifest.json: authoring manifest for Workbench/CLI style tools." + global::System.Environment.NewLine +
                "- mod.json: loader compatibility manifest; EntryAssembly and EntryType stay empty in envelope-only mode." + global::System.Environment.NewLine +
                "- Graphs/main.h8graph.json: command graph draft. Empty graph emits no packets. Non-empty nodes must use opcode hex tokens or comment aliases from Reference/allowed_opcodes.csv." + global::System.Environment.NewLine +
                "- Tables/settings.h8table.json: user-facing config table draft." + global::System.Environment.NewLine +
                "- Content/assets.h8manifest.json: CRC/asset declaration draft. Runtime use requires approval." + global::System.Environment.NewLine +
                "- Locales/en.h8loc.json: locale draft. Runtime injection is not a public right yet." + global::System.Environment.NewLine +
                "- Generated/: SDK-produced binary output goes here. Do not hand-write .h8bin files." + global::System.Environment.NewLine +
                "- Reports/: validator, review, and future package reports go here." + global::System.Environment.NewLine +
                "- Reference/: copied opcode and tuning CSV references from the project docs." + global::System.Environment.NewLine +
                "- Schemas/: JSON Schemas for editor autocomplete and schema-aware validation." + global::System.Environment.NewLine +
                "- .vscode/settings.json: optional VS Code JSON schema mapping for the starter files. The local validator checks the expected schema URL/fileMatch pairs." + global::System.Environment.NewLine +
                "- Tools/prepare_mod.ps1: one-command no-Unity setup that writes identity, validates, and builds the review manifest." + global::System.Environment.NewLine +
                "- Tools/list_allowed_opcodes.ps1: local no-Unity graph helper that prints the allowed opcode aliases and hex tokens accepted by Graphs/main.h8graph.json." + global::System.Environment.NewLine +
                "- Tools/validate_structure.ps1: local no-Unity structure validator for required files, canonical IDs, manifest parity, graph opcode allowlist checks, graph budget parity, envelope-only flags, and managed-entry disablement." + global::System.Environment.NewLine +
                "- Tools/build_review_manifest.ps1: local no-Unity review manifest builder that validates first, then writes Reports/review_manifest.json with sorted file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes for submission/review. It rejects more than 256 source files, any source file over 4194304 bytes, or more than 33554432 total source bytes before hashing." + global::System.Environment.NewLine +
                "- Tools/set_mod_identity.ps1: local no-Unity identity helper that safely writes matching mod id/name/author/version values into both manifests, then validates the folder." + global::System.Environment.NewLine;
        }

        private static string BuildAuthoringManifestTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Schema\": \"hecton8.h8mod.authoring.v1\"," + global::System.Environment.NewLine +
                "  \"Id\": \"com.example.starter\"," + global::System.Environment.NewLine +
                "  \"DisplayName\": \"Starter Mod\"," + global::System.Environment.NewLine +
                "  \"Author\": \"YourName\"," + global::System.Environment.NewLine +
                "  \"Version\": \"0.1.0\"," + global::System.Environment.NewLine +
                "  \"RequiredAPIVersion\": " + CurrentRequiredApiVersion + "," + global::System.Environment.NewLine +
                "  \"Capabilities\": []," + global::System.Environment.NewLine +
                "  \"Budgets\": {" + global::System.Environment.NewLine +
                "    \"MaxEnvelopesPerFrame\": 0," + global::System.Environment.NewLine +
                "    \"MaxAssetBytes\": 0" + global::System.Environment.NewLine +
                "  }," + global::System.Environment.NewLine +
                "  \"Compatibility\": {" + global::System.Environment.NewLine +
                "    \"Game\": \"HECTON-8\"," + global::System.Environment.NewLine +
                "    \"Runtime\": \"envelope-only\"" + global::System.Environment.NewLine +
                "  }," + global::System.Environment.NewLine +
                "  \"Entrypoints\": {" + global::System.Environment.NewLine +
                "    \"CommandGraph\": \"Graphs/main.h8graph.json\"," + global::System.Environment.NewLine +
                "    \"AssetManifest\": \"Content/assets.h8manifest.json\"," + global::System.Environment.NewLine +
                "    \"SettingsTable\": \"Tables/settings.h8table.json\"," + global::System.Environment.NewLine +
                "    \"LocaleRoot\": \"Locales\"" + global::System.Environment.NewLine +
                "  }" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildRuntimeManifestTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Id\": \"com.example.starter\"," + global::System.Environment.NewLine +
                "  \"Name\": \"Starter Mod\"," + global::System.Environment.NewLine +
                "  \"Version\": \"0.1.0\"," + global::System.Environment.NewLine +
                "  \"Author\": \"YourName\"," + global::System.Environment.NewLine +
                "  \"Description\": \"Envelope-only starter package. No managed runtime entry.\"," + global::System.Environment.NewLine +
                "  \"Dependencies\": []," + global::System.Environment.NewLine +
                "  \"EntryAssembly\": \"\"," + global::System.Environment.NewLine +
                "  \"EntryType\": \"\"," + global::System.Environment.NewLine +
                "  \"RequiredAPIVersion\": " + CurrentRequiredApiVersion + "," + global::System.Environment.NewLine +
                "  \"ModPriority\": 0" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildContentReadme()
        {
            return
                "# Content" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Declare assets in assets.h8manifest.json. Runtime loading is not granted by placing files here." + global::System.Environment.NewLine +
                "The SDK/packer must CRC-approve assets and generate envelope references before gameplay can use them." + global::System.Environment.NewLine;
        }

        private static string BuildAssetManifestTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Schema\": \"hecton8.assets.draft.v1\"," + global::System.Environment.NewLine +
                "  \"Assets\": []" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildGraphTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Schema\": \"hecton8.h8graph.draft.v1\"," + global::System.Environment.NewLine +
                "  \"GraphName\": \"main\"," + global::System.Environment.NewLine +
                "  \"Runtime\": \"envelope-only\"," + global::System.Environment.NewLine +
                "  \"MaxEnvelopesPerFrame\": 0," + global::System.Environment.NewLine +
                "  \"Nodes\": []," + global::System.Environment.NewLine +
                "  \"Notes\": \"Empty graph emits no runtime packets. Node Opcode values must be a hex token or comment alias from Reference/allowed_opcodes.csv.\"" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildSettingsTableTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Schema\": \"hecton8.settings_table.draft.v1\"," + global::System.Environment.NewLine +
                "  \"Rows\": []" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildLocaleTemplate()
        {
            return
                "{" + global::System.Environment.NewLine +
                "  \"Schema\": \"hecton8.locale.draft.v1\"," + global::System.Environment.NewLine +
                "  \"Locale\": \"en\"," + global::System.Environment.NewLine +
                "  \"Strings\": {}" + global::System.Environment.NewLine +
                "}" + global::System.Environment.NewLine;
        }

        private static string BuildVsCodeSettings()
        {
            return JoinLines(
                "{",
                "  \"json.schemas\": [",
                "    { \"fileMatch\": [\"/mod.h8manifest.json\"], \"url\": \"./Schemas/h8mod.authoring.schema.json\" },",
                "    { \"fileMatch\": [\"/mod.json\"], \"url\": \"./Schemas/runtime.mod.schema.json\" },",
                "    { \"fileMatch\": [\"/Graphs/*.h8graph.json\"], \"url\": \"./Schemas/h8graph.schema.json\" },",
                "    { \"fileMatch\": [\"/Content/*.h8manifest.json\"], \"url\": \"./Schemas/assets.schema.json\" },",
                "    { \"fileMatch\": [\"/Tables/*.h8table.json\"], \"url\": \"./Schemas/settings_table.schema.json\" },",
                "    { \"fileMatch\": [\"/Locales/*.h8loc.json\"], \"url\": \"./Schemas/locale.schema.json\" }",
                "  ]",
                "}");
        }

        private static string BuildAuthoringManifestSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/h8mod.authoring.schema.json\",",
                "  \"title\": \"HECTON-8 authoring mod manifest\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Schema\", \"Id\", \"DisplayName\", \"Author\", \"Version\", \"RequiredAPIVersion\", \"Capabilities\", \"Budgets\", \"Compatibility\", \"Entrypoints\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.h8mod.authoring.v1\" },",
                "    \"Id\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "    \"DisplayName\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Author\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Version\": { \"type\": \"string\", \"pattern\": \"^(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?(\\\\+[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$\" },",
                "    \"RequiredAPIVersion\": { \"type\": \"integer\", \"minimum\": " + CurrentRequiredApiVersion + " },",
                "    \"Capabilities\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },",
                "    \"Budgets\": {",
                "      \"type\": \"object\",",
                "      \"additionalProperties\": false,",
                "      \"required\": [\"MaxEnvelopesPerFrame\", \"MaxAssetBytes\"],",
                "      \"properties\": {",
                "        \"MaxEnvelopesPerFrame\": { \"type\": \"integer\", \"minimum\": 0 },",
                "        \"MaxAssetBytes\": { \"type\": \"integer\", \"minimum\": 0 }",
                "      }",
                "    },",
                "    \"Compatibility\": {",
                "      \"type\": \"object\",",
                "      \"additionalProperties\": false,",
                "      \"required\": [\"Game\", \"Runtime\"],",
                "      \"properties\": { \"Game\": { \"const\": \"HECTON-8\" }, \"Runtime\": { \"const\": \"envelope-only\" } }",
                "    },",
                "    \"Entrypoints\": {",
                "      \"type\": \"object\",",
                "      \"additionalProperties\": false,",
                "      \"required\": [\"CommandGraph\", \"AssetManifest\", \"SettingsTable\", \"LocaleRoot\"],",
                "      \"properties\": {",
                "        \"CommandGraph\": { \"type\": \"string\" },",
                "        \"AssetManifest\": { \"type\": \"string\" },",
                "        \"SettingsTable\": { \"type\": \"string\" },",
                "        \"LocaleRoot\": { \"type\": \"string\" }",
                "      }",
                "    }",
                "  }",
                "}");
        }

        private static string BuildRuntimeManifestSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/runtime.mod.schema.json\",",
                "  \"title\": \"HECTON-8 runtime compatibility manifest\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Id\", \"Name\", \"Version\", \"Author\", \"Description\", \"Dependencies\", \"EntryAssembly\", \"EntryType\", \"RequiredAPIVersion\", \"ModPriority\"],",
                "  \"properties\": {",
                "    \"Id\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "    \"Name\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Version\": { \"type\": \"string\", \"pattern\": \"^(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?(\\\\+[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$\" },",
                "    \"Author\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Description\": { \"type\": \"string\" },",
                "    \"Dependencies\": { \"type\": \"array\", \"items\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" } },",
                "    \"EntryAssembly\": { \"const\": \"\" },",
                "    \"EntryType\": { \"const\": \"\" },",
                "    \"RequiredAPIVersion\": { \"type\": \"integer\", \"minimum\": " + CurrentRequiredApiVersion + " },",
                "    \"ModPriority\": { \"type\": \"integer\" }",
                "  }",
                "}");
        }

        private static string BuildGraphSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/h8graph.schema.json\",",
                "  \"title\": \"HECTON-8 command graph draft\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Schema\", \"GraphName\", \"Runtime\", \"MaxEnvelopesPerFrame\", \"Nodes\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.h8graph.draft.v1\" },",
                "    \"GraphName\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Runtime\": { \"const\": \"envelope-only\" },",
                "    \"MaxEnvelopesPerFrame\": { \"type\": \"integer\", \"minimum\": 0 },",
                "    \"Nodes\": {",
                "      \"type\": \"array\",",
                "      \"items\": {",
                "        \"type\": \"object\",",
                "        \"additionalProperties\": true,",
                "        \"required\": [\"Id\", \"Opcode\"],",
                "        \"properties\": {",
                "          \"Id\": { \"type\": \"string\", \"minLength\": 1 },",
                "          \"Opcode\": { \"type\": \"string\", \"pattern\": \"^(0x[0-9A-Fa-f]{1,8}|[A-Za-z][A-Za-z0-9_]*)$\" }",
                "        }",
                "      }",
                "    },",
                "    \"Notes\": { \"type\": \"string\" }",
                "  }",
                "}");
        }

        private static string BuildAssetsSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/assets.schema.json\",",
                "  \"title\": \"HECTON-8 asset declaration draft\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Schema\", \"Assets\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.assets.draft.v1\" },",
                "    \"Assets\": { \"type\": \"array\", \"items\": { \"type\": \"object\", \"additionalProperties\": true } }",
                "  }",
                "}");
        }

        private static string BuildSettingsTableSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/settings_table.schema.json\",",
                "  \"title\": \"HECTON-8 settings table draft\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Schema\", \"Rows\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.settings_table.draft.v1\" },",
                "    \"Rows\": { \"type\": \"array\", \"items\": { \"type\": \"object\", \"additionalProperties\": true } }",
                "  }",
                "}");
        }

        private static string BuildLocaleSchema()
        {
            return JoinLines(
                "{",
                "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",",
                "  \"$id\": \"https://hecton8.local/schemas/locale.schema.json\",",
                "  \"title\": \"HECTON-8 locale draft\",",
                "  \"type\": \"object\",",
                "  \"additionalProperties\": false,",
                "  \"required\": [\"Schema\", \"Locale\", \"Strings\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.locale.draft.v1\" },",
                "    \"Locale\": { \"type\": \"string\", \"minLength\": 2 },",
                "    \"Strings\": { \"type\": \"object\", \"additionalProperties\": { \"type\": \"string\" } }",
                "  }",
                "}");
        }

        private static string BuildGeneratedReadme()
        {
            return
                "# Generated" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "SDK-generated .h8bin, validation manifests, and package outputs go here." + global::System.Environment.NewLine +
                "Do not hand-write binary envelope streams." + global::System.Environment.NewLine;
        }

        private static string BuildReportsReadme()
        {
            return
                "# Reports" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Static validation, packer, and simulator reports go here." + global::System.Environment.NewLine +
                "A report is evidence only when it names the validator version and input files." + global::System.Environment.NewLine;
        }

        private static string BuildReferenceReadme()
        {
            return
                "# Reference" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "allowed_opcodes.csv is the current envelope opcode allowlist snapshot." + global::System.Environment.NewLine +
                "Run Tools/list_allowed_opcodes.ps1 from the starter root to print the graph opcode aliases and hex tokens that Graphs/main.h8graph.json may use." + global::System.Environment.NewLine +
                "kernel_tuning_profiles.csv is editor/simulator tuning reference only; it does not make reserved opcodes public." + global::System.Environment.NewLine;
        }

        private static string BuildToolsReadme()
        {
            return
                "# Tools" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Fast path for a copied starter kit:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Use pwsh instead of powershell on macOS/Linux with PowerShell 7. The scripts normalize child paths internally; do not rewrite Tools/, Reports/, or .vscode/ paths per platform." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "prepare_mod.ps1 runs identity setup, structure validation, and review manifest generation in the correct order." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run list_allowed_opcodes.ps1 when editing Graphs/main.h8graph.json. It prints every currently allowed graph opcode alias and hex token from Reference/allowed_opcodes.csv; use either value in Nodes[].Opcode." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run validate_structure.ps1 before sending this folder to another tool or author." + global::System.Environment.NewLine +
                "This local validator checks only starter-kit structure, canonical IDs, manifest parity, graph opcode allowlist, graph budget parity, exact editor schema mappings, and envelope-only safety. It is not runtime verification." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run build_review_manifest.ps1 before submitting a starter folder for review. It runs the structure validator first, then writes Reports/review_manifest.json with sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes. Generated/ and Reports/ are excluded from the hash list so reports do not hash themselves. The source side is bounded at 256 files, 4194304 bytes per file, and 33554432 total bytes; oversized source files fail before hashing." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run set_mod_identity.ps1 once when you copy the starter kit. It writes the same canonical mod id, display name, author, and version into mod.h8manifest.json and mod.json, then runs the structure validator." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Command:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine;
        }

        private static string BuildStarterKitAllowedOpcodesScript()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [switch]$Json");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_OPCODE_LIST] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath([string]$BasePath, [string]$RelativePath) {");
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in ($RelativePath.Replace('\\','/') -split '/')) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($segment)) {");
            builder.AppendLine("            $current = Join-Path $current $segment");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Require-File([string]$RelativePath) {");
            builder.AppendLine("    $path = Join-StarterPath $Root $RelativePath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {");
            builder.AppendLine("        Fail ('Missing required file: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine("    return $path");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Read-AllowedOpcodeRows() {");
            builder.AppendLine("    $path = Require-File 'Reference/allowed_opcodes.csv'");
            builder.AppendLine("    $rows = New-Object 'System.Collections.Generic.List[object]'");
            builder.AppendLine("    $seenHex = @{}");
            builder.AppendLine("    $seenAlias = @{}");
            builder.AppendLine();
            builder.AppendLine("    foreach ($line in (Get-Content -LiteralPath $path)) {");
            builder.AppendLine("        $text = [string]$line");
            builder.AppendLine("        $comment = ''");
            builder.AppendLine("        $commentIndex = $text.IndexOf('#')");
            builder.AppendLine("        if ($commentIndex -ge 0) {");
            builder.AppendLine("            $comment = $text.Substring($commentIndex + 1).Trim()");
            builder.AppendLine("            $text = $text.Substring(0, $commentIndex).Trim()");
            builder.AppendLine("        } else {");
            builder.AppendLine("            $text = $text.Trim()");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if ([string]::IsNullOrWhiteSpace($text)) { continue }");
            builder.AppendLine("        if ($text -notmatch '^0x[0-9A-Fa-f]{1,8}$') {");
            builder.AppendLine("            Fail ('Reference/allowed_opcodes.csv contains invalid opcode token: ' + $text)");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        $hex = '0x' + $text.Substring(2).ToUpperInvariant()");
            builder.AppendLine("        if ($seenHex.ContainsKey($hex)) {");
            builder.AppendLine("            Fail ('Reference/allowed_opcodes.csv contains duplicate opcode token: ' + $hex)");
            builder.AppendLine("        }");
            builder.AppendLine("        $seenHex[$hex] = $true");
            builder.AppendLine();
            builder.AppendLine("        $alias = ''");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($comment)) {");
            builder.AppendLine("            $candidateAlias = @($comment -split '\\s+')[0]");
            builder.AppendLine("            if ($candidateAlias -match '^[A-Za-z][A-Za-z0-9_]*$') {");
            builder.AppendLine("                $alias = $candidateAlias");
            builder.AppendLine("                if ($seenAlias.ContainsKey($alias)) {");
            builder.AppendLine("                    Fail ('Reference/allowed_opcodes.csv contains duplicate opcode alias: ' + $alias)");
            builder.AppendLine("                }");
            builder.AppendLine("                $seenAlias[$alias] = $true");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        [void]$rows.Add([pscustomobject][ordered]@{");
            builder.AppendLine("            Index = $rows.Count + 1");
            builder.AppendLine("            Hex = $hex");
            builder.AppendLine("            Alias = $alias");
            builder.AppendLine("            Description = $comment");
            builder.AppendLine("        })");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if ($rows.Count -eq 0) { Fail 'Reference/allowed_opcodes.csv has no allowed graph opcodes.' }");
            builder.AppendLine("    return $rows.ToArray()");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$Root = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$rows = Read-AllowedOpcodeRows");
            builder.AppendLine();
            builder.AppendLine("if ($Json) {");
            builder.AppendLine("    $payload = [pscustomobject][ordered]@{");
            builder.AppendLine("        Schema = 'hecton8.allowed_graph_opcodes.v1'");
            builder.AppendLine("        Runtime = 'envelope-only'");
            builder.AppendLine("        Source = 'Reference/allowed_opcodes.csv'");
            builder.AppendLine("        Count = $rows.Count");
            builder.AppendLine("        Opcodes = $rows");
            builder.AppendLine("    }");
            builder.AppendLine("    Write-Output ($payload | ConvertTo-Json -Depth 6)");
            builder.AppendLine("    exit 0");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Write-Output 'HECTON-8 allowed graph opcodes (envelope-only)'");
            builder.AppendLine("Write-Output 'Use Alias or Hex in Graphs/main.h8graph.json Nodes[].Opcode.'");
            builder.AppendLine("foreach ($row in $rows) {");
            builder.AppendLine("    $alias = [string]$row.Alias");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($alias)) {");
            builder.AppendLine("        $alias = '(no-alias)'");
            builder.AppendLine("    }");
            builder.AppendLine("    Write-Output ('{0,-24} {1}' -f $alias, [string]$row.Hex)");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildStarterKitReviewManifestScript()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Output = 'Reports/review_manifest.json'");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("$MaxReviewFiles = 256");
            builder.AppendLine("$MaxReviewFileBytes = 4194304");
            builder.AppendLine("$MaxReviewTotalBytes = 33554432");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_REVIEW_MANIFEST] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath([string]$BasePath, [string]$RelativePath) {");
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in ($RelativePath.Replace('\\','/') -split '/')) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($segment)) {");
            builder.AppendLine("            $current = Join-Path $current $segment");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if ([System.IO.Path]::IsPathRooted($Output)) {");
            builder.AppendLine("    Fail 'Output path must be relative to the starter kit root.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$normalizedOutput = $Output.Replace('\\','/')");
            builder.AppendLine("if ($normalizedOutput.StartsWith('../') -or $normalizedOutput.Contains('/../') -or -not $normalizedOutput.StartsWith('Reports/')) {");
            builder.AppendLine("    Fail 'Output path must stay under Reports/.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$rootFull = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'");
            builder.AppendLine("if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Missing Tools/validate_structure.ps1.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("& $validator -Root $rootFull | Out-Host");
            builder.AppendLine();
            builder.AppendLine("$rootPrefix = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar");
            builder.AppendLine("$excludePrefixes = @('Generated/','Reports/')");
            builder.AppendLine("$files = New-Object 'System.Collections.Generic.List[object]'");
            builder.AppendLine("$totalBytes = [long]0");
            builder.AppendLine();
            builder.AppendLine("Get-ChildItem -LiteralPath $rootFull -Recurse -File | ForEach-Object {");
            builder.AppendLine("    $fullPath = [System.IO.Path]::GetFullPath($_.FullName)");
            builder.AppendLine("    $relative = $fullPath.Substring($rootPrefix.Length).Replace('\\','/')");
            builder.AppendLine("    $excluded = $false");
            builder.AppendLine("    foreach ($prefix in $excludePrefixes) {");
            builder.AppendLine("        if ($relative.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {");
            builder.AppendLine("            $excluded = $true");
            builder.AppendLine("            break");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (-not $excluded) {");
            builder.AppendLine("        if ($files.Count -ge $MaxReviewFiles) {");
            builder.AppendLine("            Fail ('Review manifest source file limit exceeded: ' + $MaxReviewFiles)");
            builder.AppendLine("        }");
            builder.AppendLine("        if ($_.Length -gt $MaxReviewFileBytes) {");
            builder.AppendLine("            Fail ('Review file exceeds max bytes: ' + $relative)");
            builder.AppendLine("        }");
            builder.AppendLine("        $totalBytes += [long]$_.Length");
            builder.AppendLine("        if ($totalBytes -gt $MaxReviewTotalBytes) {");
            builder.AppendLine("            Fail ('Review manifest total byte limit exceeded: ' + $MaxReviewTotalBytes)");
            builder.AppendLine("        }");
            builder.AppendLine("        $hash = Get-FileHash -LiteralPath $fullPath -Algorithm SHA256");
            builder.AppendLine("        [void]$files.Add([pscustomobject][ordered]@{");
            builder.AppendLine("            Path = $relative");
            builder.AppendLine("            Bytes = $_.Length");
            builder.AppendLine("            Sha256 = $hash.Hash.ToLowerInvariant()");
            builder.AppendLine("        })");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$orderedFiles = @($files | Sort-Object -Property Path)");
            builder.AppendLine("$runtime = Get-Content -Raw -LiteralPath (Join-StarterPath $rootFull 'mod.json') | ConvertFrom-Json");
            builder.AppendLine("$manifest = [pscustomobject][ordered]@{");
            builder.AppendLine("    Schema = 'hecton8.external_review_manifest.v1'");
            builder.AppendLine("    Runtime = 'envelope-only'");
            builder.AppendLine("    RootId = [string]$runtime.Id");
            builder.AppendLine("    FileCount = $orderedFiles.Count");
            builder.AppendLine("    TotalBytes = $totalBytes");
            builder.AppendLine("    Limits = [pscustomobject][ordered]@{");
            builder.AppendLine("        MaxFiles = $MaxReviewFiles");
            builder.AppendLine("        MaxFileBytes = $MaxReviewFileBytes");
            builder.AppendLine("        MaxTotalBytes = $MaxReviewTotalBytes");
            builder.AppendLine("    }");
            builder.AppendLine("    Files = $orderedFiles");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$outputPath = Join-StarterPath $rootFull $normalizedOutput");
            builder.AppendLine("$outputDirectory = Split-Path -Parent $outputPath");
            builder.AppendLine("if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {");
            builder.AppendLine("    [void](New-Item -ItemType Directory -Path $outputDirectory)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$json = $manifest | ConvertTo-Json -Depth 8");
            builder.AppendLine("$utf8NoBom = New-Object System.Text.UTF8Encoding $false");
            builder.AppendLine("[System.IO.File]::WriteAllText($outputPath, $json + [System.Environment]::NewLine, $utf8NoBom)");
            builder.AppendLine();
            builder.AppendLine("Write-Host ('PASS HECTON-8 review manifest: ' + $Output)");
            return builder.ToString();
        }

        private static string BuildStarterKitPrepareScript()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Id,");
            builder.AppendLine("    [string]$DisplayName,");
            builder.AppendLine("    [string]$Author,");
            builder.AppendLine("    [string]$Version,");
            builder.AppendLine("    [string]$ReviewOutput = 'Reports/review_manifest.json'");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_PREPARE] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath([string]$BasePath, [string]$RelativePath) {");
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in ($RelativePath.Replace('\\','/') -split '/')) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($segment)) {");
            builder.AppendLine("            $current = Join-Path $current $segment");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($Id)) {");
            builder.AppendLine("    Fail 'Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$rootFull = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$identityTool = Join-StarterPath $rootFull 'Tools/set_mod_identity.ps1'");
            builder.AppendLine("$reviewTool = Join-StarterPath $rootFull 'Tools/build_review_manifest.ps1'");
            builder.AppendLine();
            builder.AppendLine("if (-not (Test-Path -LiteralPath $identityTool -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Missing Tools/set_mod_identity.ps1.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not (Test-Path -LiteralPath $reviewTool -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Missing Tools/build_review_manifest.ps1.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("& $identityTool -Root $rootFull -Id $Id -DisplayName $DisplayName -Author $Author -Version $Version | Out-Host");
            builder.AppendLine();
            builder.AppendLine("& $reviewTool -Root $rootFull -Output $ReviewOutput | Out-Host");
            builder.AppendLine();
            builder.AppendLine("Write-Host ('PASS HECTON-8 starter prepared: ' + $Id)");
            return builder.ToString();
        }

        private static string BuildStarterKitIdentityScript()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Id,");
            builder.AppendLine("    [string]$DisplayName,");
            builder.AppendLine("    [string]$Author,");
            builder.AppendLine("    [string]$Version");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_SET_IDENTITY] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath([string]$BasePath, [string]$RelativePath) {");
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in ($RelativePath.Replace('\\','/') -split '/')) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($segment)) {");
            builder.AppendLine("            $current = Join-Path $current $segment");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Test-ReservedModIdSegment([string]$Segment) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Segment)) { return $false }");
            builder.AppendLine("    switch ($Segment) {");
            builder.AppendLine("        'con' { return $true }");
            builder.AppendLine("        'prn' { return $true }");
            builder.AppendLine("        'aux' { return $true }");
            builder.AppendLine("        'nul' { return $true }");
            builder.AppendLine("    }");
            builder.AppendLine("    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) {");
            builder.AppendLine("        return $true");
            builder.AppendLine("    }");
            builder.AppendLine("    return $false");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-ModId([string]$Value, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {");
            builder.AppendLine("        Fail ($Label + \" may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.\")");
            builder.AppendLine("    }");
            builder.AppendLine("    foreach ($segment in ($trimmed -split '[._-]')) {");
            builder.AppendLine("        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-RequiredText([string]$Value, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-Version([string]$Value, [string]$Label) {");
            builder.AppendLine("    $trimmed = Validate-RequiredText $Value $Label");
            builder.AppendLine("    if ($trimmed -notmatch '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {");
            builder.AppendLine("        Fail ($Label + ' must use semantic version form MAJOR.MINOR.PATCH with optional -prerelease or +build metadata.')");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Read-JsonFile([string]$Path) {");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {");
            builder.AppendLine("        Fail ('Missing file: ' + $Path)");
            builder.AppendLine("    }");
            builder.AppendLine("    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Write-JsonFile([string]$Path, [object]$Value) {");
            builder.AppendLine("    $json = $Value | ConvertTo-Json -Depth 16");
            builder.AppendLine("    $utf8NoBom = New-Object System.Text.UTF8Encoding $false");
            builder.AppendLine("    [System.IO.File]::WriteAllText($Path, $json + [System.Environment]::NewLine, $utf8NoBom)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($Id)) {");
            builder.AppendLine("    Fail 'Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$rootFull = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$authoringPath = Join-StarterPath $rootFull 'mod.h8manifest.json'");
            builder.AppendLine("$runtimePath = Join-StarterPath $rootFull 'mod.json'");
            builder.AppendLine("$authoring = Read-JsonFile $authoringPath");
            builder.AppendLine("$runtime = Read-JsonFile $runtimePath");
            builder.AppendLine("$canonicalId = Validate-ModId $Id 'Id'");
            builder.AppendLine();
            builder.AppendLine("$authoring.Id = $canonicalId");
            builder.AppendLine("$runtime.Id = $canonicalId");
            builder.AppendLine();
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace($DisplayName)) {");
            builder.AppendLine("    $canonicalDisplayName = Validate-RequiredText $DisplayName 'DisplayName'");
            builder.AppendLine("    $authoring.DisplayName = $canonicalDisplayName");
            builder.AppendLine("    $runtime.Name = $canonicalDisplayName");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace($Author)) {");
            builder.AppendLine("    $canonicalAuthor = Validate-RequiredText $Author 'Author'");
            builder.AppendLine("    $authoring.Author = $canonicalAuthor");
            builder.AppendLine("    $runtime.Author = $canonicalAuthor");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace($Version)) {");
            builder.AppendLine("    $canonicalVersion = Validate-Version $Version 'Version'");
            builder.AppendLine("    $authoring.Version = $canonicalVersion");
            builder.AppendLine("    $runtime.Version = $canonicalVersion");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Write-JsonFile $authoringPath $authoring");
            builder.AppendLine("Write-JsonFile $runtimePath $runtime");
            builder.AppendLine();
            builder.AppendLine("$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'");
            builder.AppendLine("if (Test-Path -LiteralPath $validator -PathType Leaf) {");
            builder.AppendLine("    & $validator -Root $rootFull | Out-Host");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Write-Host ('PASS HECTON-8 starter identity set: ' + $canonicalId)");
            return builder.ToString();
        }

        private static string BuildStarterKitValidatorScript()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot)");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_STARTER_VALIDATION] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath([string]$BasePath, [string]$RelativePath) {");
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in ($RelativePath.Replace('\\','/') -split '/')) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($segment)) {");
            builder.AppendLine("            $current = Join-Path $current $segment");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Require-File([string]$RelativePath) {");
            builder.AppendLine("    $path = Join-StarterPath $Root $RelativePath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {");
            builder.AppendLine("        Fail ('Missing required file: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine("    return $path");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Require-Directory([string]$RelativePath) {");
            builder.AppendLine("    $path = Join-StarterPath $Root $RelativePath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $path -PathType Container)) {");
            builder.AppendLine("        Fail ('Missing required directory: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Read-Json([string]$RelativePath) {");
            builder.AppendLine("    $path = Require-File $RelativePath");
            builder.AppendLine("    try {");
            builder.AppendLine("        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json");
            builder.AppendLine("    } catch {");
            builder.AppendLine("        Fail ('Invalid JSON in ' + $RelativePath + ': ' + $_.Exception.Message)");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Test-ReservedModIdSegment([string]$Segment) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Segment)) { return $false }");
            builder.AppendLine("    switch ($Segment) {");
            builder.AppendLine("        'con' { return $true }");
            builder.AppendLine("        'prn' { return $true }");
            builder.AppendLine("        'aux' { return $true }");
            builder.AppendLine("        'nul' { return $true }");
            builder.AppendLine("    }");
            builder.AppendLine("    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) {");
            builder.AppendLine("        return $true");
            builder.AppendLine("    }");
            builder.AppendLine("    return $false");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-ModId([string]$Value, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {");
            builder.AppendLine("        Fail ($Label + \" may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.\")");
            builder.AppendLine("    }");
            builder.AppendLine("    foreach ($segment in ($trimmed -split '[._-]')) {");
            builder.AppendLine("        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-RequiredText([string]$Value, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-Version([string]$Value, [string]$Label) {");
            builder.AppendLine("    $trimmed = Validate-RequiredText $Value $Label");
            builder.AppendLine("    if ($trimmed -notmatch '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {");
            builder.AppendLine("        Fail ($Label + ' must use semantic version form MAJOR.MINOR.PATCH with optional -prerelease or +build metadata.')");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Read-AllowedGraphOpcodeTokens() {");
            builder.AppendLine("    $path = Require-File 'Reference/allowed_opcodes.csv'");
            builder.AppendLine("    $tokens = @{}");
            builder.AppendLine("    foreach ($line in (Get-Content -LiteralPath $path)) {");
            builder.AppendLine("        $text = [string]$line");
            builder.AppendLine("        $comment = ''");
            builder.AppendLine("        $commentIndex = $text.IndexOf('#')");
            builder.AppendLine("        if ($commentIndex -ge 0) {");
            builder.AppendLine("            $comment = $text.Substring($commentIndex + 1).Trim()");
            builder.AppendLine("            $text = $text.Substring(0, $commentIndex).Trim()");
            builder.AppendLine("        } else {");
            builder.AppendLine("            $text = $text.Trim()");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if ([string]::IsNullOrWhiteSpace($text)) { continue }");
            builder.AppendLine("        if ($text -notmatch '^0x[0-9A-Fa-f]{1,8}$') {");
            builder.AppendLine("            Fail ('Reference/allowed_opcodes.csv contains invalid opcode token: ' + $text)");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        $tokens[$text.ToUpperInvariant()] = $true");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($comment)) {");
            builder.AppendLine("            $alias = @($comment -split '\\s+')[0]");
            builder.AppendLine("            if ($alias -match '^[A-Za-z][A-Za-z0-9_]*$') {");
            builder.AppendLine("                $tokens[$alias] = $true");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if ($tokens.Count -eq 0) { Fail 'Reference/allowed_opcodes.csv has no allowed graph opcodes.' }");
            builder.AppendLine("    return $tokens");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("@('Content','Graphs','Tables','Locales','Generated','Reports','Reference','Schemas','Tools','.vscode') | ForEach-Object { Require-Directory $_ }");
            builder.AppendLine("@(");
            builder.AppendLine("    'README.md',");
            builder.AppendLine("    'mod.h8manifest.json',");
            builder.AppendLine("    'mod.json',");
            builder.AppendLine("    'Content/README.md',");
            builder.AppendLine("    'Content/assets.h8manifest.json',");
            builder.AppendLine("    'Graphs/main.h8graph.json',");
            builder.AppendLine("    'Tables/settings.h8table.json',");
            builder.AppendLine("    'Locales/en.h8loc.json',");
            builder.AppendLine("    'Generated/README.md',");
            builder.AppendLine("    'Reports/README.md',");
            builder.AppendLine("    'Reference/README.md',");
            builder.AppendLine("    'Reference/allowed_opcodes.csv',");
            builder.AppendLine("    'Reference/kernel_tuning_profiles.csv',");
            builder.AppendLine("    'Schemas/assets.schema.json',");
            builder.AppendLine("    'Schemas/h8graph.schema.json',");
            builder.AppendLine("    'Schemas/h8mod.authoring.schema.json',");
            builder.AppendLine("    'Schemas/locale.schema.json',");
            builder.AppendLine("    'Schemas/runtime.mod.schema.json',");
            builder.AppendLine("    'Schemas/settings_table.schema.json',");
            builder.AppendLine("    'Tools/README.md',");
            builder.AppendLine("    'Tools/build_review_manifest.ps1',");
            builder.AppendLine("    'Tools/list_allowed_opcodes.ps1',");
            builder.AppendLine("    'Tools/prepare_mod.ps1',");
            builder.AppendLine("    'Tools/set_mod_identity.ps1',");
            builder.AppendLine("    'Tools/validate_structure.ps1',");
            builder.AppendLine("    '.vscode/settings.json'");
            builder.AppendLine(") | ForEach-Object { [void](Require-File $_) }");
            builder.AppendLine();
            builder.AppendLine("$authoring = Read-Json 'mod.h8manifest.json'");
            builder.AppendLine("$runtime = Read-Json 'mod.json'");
            builder.AppendLine("$graph = Read-Json 'Graphs/main.h8graph.json'");
            builder.AppendLine("$assets = Read-Json 'Content/assets.h8manifest.json'");
            builder.AppendLine("$settings = Read-Json 'Tables/settings.h8table.json'");
            builder.AppendLine("$locale = Read-Json 'Locales/en.h8loc.json'");
            builder.AppendLine("$vscodeSettings = Read-Json '.vscode/settings.json'");
            builder.AppendLine("$schemaFiles = @(");
            builder.AppendLine("    'Schemas/assets.schema.json',");
            builder.AppendLine("    'Schemas/h8graph.schema.json',");
            builder.AppendLine("    'Schemas/h8mod.authoring.schema.json',");
            builder.AppendLine("    'Schemas/locale.schema.json',");
            builder.AppendLine("    'Schemas/runtime.mod.schema.json',");
            builder.AppendLine("    'Schemas/settings_table.schema.json'");
            builder.AppendLine(")");
            builder.AppendLine("foreach ($schemaFile in $schemaFiles) {");
            builder.AppendLine("    $schema = Read-Json $schemaFile");
            builder.AppendLine("    if ($null -eq $schema.PSObject.Properties['$schema']) { Fail ($schemaFile + ' requires $schema.') }");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace([string]$schema.title)) { Fail ($schemaFile + ' requires title.') }");
            builder.AppendLine("    if ([string]$schema.type -ne 'object') { Fail ($schemaFile + ' must describe a JSON object.') }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$authoringId = Validate-ModId ([string]$authoring.Id) 'mod.h8manifest.json Id'");
            builder.AppendLine("$authoringDisplayName = Validate-RequiredText ([string]$authoring.DisplayName) 'mod.h8manifest.json DisplayName'");
            builder.AppendLine("$authoringAuthor = Validate-RequiredText ([string]$authoring.Author) 'mod.h8manifest.json Author'");
            builder.AppendLine("$authoringVersion = Validate-Version ([string]$authoring.Version) 'mod.h8manifest.json Version'");
            builder.AppendLine("if ([string]$authoring.Compatibility.Runtime -ne 'envelope-only') { Fail 'mod.h8manifest.json Compatibility.Runtime must be envelope-only.' }");
            builder.AppendLine("if ([int]$authoring.RequiredAPIVersion -lt 2) { Fail 'mod.h8manifest.json RequiredAPIVersion must be >= 2.' }");
            builder.AppendLine("$runtimeId = Validate-ModId ([string]$runtime.Id) 'mod.json Id'");
            builder.AppendLine("if ($authoringId -ne $runtimeId) { Fail 'mod.h8manifest.json Id must match mod.json Id.' }");
            builder.AppendLine("$runtimeName = Validate-RequiredText ([string]$runtime.Name) 'mod.json Name'");
            builder.AppendLine("$runtimeAuthor = Validate-RequiredText ([string]$runtime.Author) 'mod.json Author'");
            builder.AppendLine("$runtimeVersion = Validate-Version ([string]$runtime.Version) 'mod.json Version'");
            builder.AppendLine("if ($authoringDisplayName -ne $runtimeName) { Fail 'mod.h8manifest.json DisplayName must match mod.json Name.' }");
            builder.AppendLine("if ($authoringAuthor -ne $runtimeAuthor) { Fail 'mod.h8manifest.json Author must match mod.json Author.' }");
            builder.AppendLine("if ($authoringVersion -ne $runtimeVersion) { Fail 'mod.h8manifest.json Version must match mod.json Version.' }");
            builder.AppendLine("if ($null -ne $runtime.Dependencies) {");
            builder.AppendLine("    foreach ($dependencyId in @($runtime.Dependencies)) {");
            builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace([string]$dependencyId)) {");
            builder.AppendLine("            [void](Validate-ModId ([string]$dependencyId) 'mod.json Dependencies item')");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryAssembly)) { Fail 'mod.json EntryAssembly must stay empty in envelope-only starter kits.' }");
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryType)) { Fail 'mod.json EntryType must stay empty in envelope-only starter kits.' }");
            builder.AppendLine("if ([int]$runtime.RequiredAPIVersion -lt 2) { Fail 'mod.json RequiredAPIVersion must be >= 2.' }");
            builder.AppendLine("if ([string]$graph.Runtime -ne 'envelope-only') { Fail 'Graphs/main.h8graph.json Runtime must be envelope-only.' }");
            builder.AppendLine("if ([int]$graph.MaxEnvelopesPerFrame -gt [int]$authoring.Budgets.MaxEnvelopesPerFrame) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must not exceed mod.h8manifest.json Budgets.MaxEnvelopesPerFrame.' }");
            builder.AppendLine("$allowedGraphOpcodes = Read-AllowedGraphOpcodeTokens");
            builder.AppendLine("$graphNodeIds = @{}");
            builder.AppendLine("$graphOpcodeNodeCount = 0");
            builder.AppendLine("foreach ($node in @($graph.Nodes)) {");
            builder.AppendLine("    if ($null -eq $node) { Fail 'Graphs/main.h8graph.json Nodes must not contain null entries.' }");
            builder.AppendLine("    $nodeId = [string]$node.Id");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($nodeId)) { Fail 'Graphs/main.h8graph.json node Id is required.' }");
            builder.AppendLine("    if ($graphNodeIds.ContainsKey($nodeId)) { Fail ('Graphs/main.h8graph.json duplicate node Id: ' + $nodeId) }");
            builder.AppendLine("    $graphNodeIds[$nodeId] = $true");
            builder.AppendLine("    $opcode = [string]$node.Opcode");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($opcode)) { Fail ('Graphs/main.h8graph.json node Opcode is required: ' + $nodeId) }");
            builder.AppendLine("    $opcode = $opcode.Trim()");
            builder.AppendLine("    $opcodeToken = $opcode");
            builder.AppendLine("    if ($opcode -match '^0x[0-9A-Fa-f]{1,8}$') {");
            builder.AppendLine("        $opcodeToken = $opcode.ToUpperInvariant()");
            builder.AppendLine("    }");
            builder.AppendLine("    if (-not $allowedGraphOpcodes.ContainsKey($opcodeToken)) {");
            builder.AppendLine("        Fail ('Graphs/main.h8graph.json node Opcode is not in Reference/allowed_opcodes.csv: ' + $opcode)");
            builder.AppendLine("    }");
            builder.AppendLine("    $graphOpcodeNodeCount++");
            builder.AppendLine("}");
            builder.AppendLine("if ($graphOpcodeNodeCount -gt 0 -and [int]$graph.MaxEnvelopesPerFrame -lt 1) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must be >= 1 when opcode nodes exist.' }");
            builder.AppendLine("if ($null -eq $assets.Assets) { Fail 'Content/assets.h8manifest.json requires Assets array.' }");
            builder.AppendLine("if ($null -eq $settings.Rows) { Fail 'Tables/settings.h8table.json requires Rows array.' }");
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace([string]$locale.Locale)) { Fail 'Locales/en.h8loc.json requires Locale.' }");
            builder.AppendLine("if ($null -eq $vscodeSettings.PSObject.Properties['json.schemas']) { Fail '.vscode/settings.json requires json.schemas mapping.' }");
            builder.AppendLine("$schemaMappings = @($vscodeSettings.PSObject.Properties['json.schemas'].Value)");
            builder.AppendLine("$requiredSchemaMappings = @(");
            builder.AppendLine("    @{ Url = './Schemas/h8mod.authoring.schema.json'; Match = '/mod.h8manifest.json' },");
            builder.AppendLine("    @{ Url = './Schemas/runtime.mod.schema.json'; Match = '/mod.json' },");
            builder.AppendLine("    @{ Url = './Schemas/h8graph.schema.json'; Match = '/Graphs/*.h8graph.json' },");
            builder.AppendLine("    @{ Url = './Schemas/assets.schema.json'; Match = '/Content/*.h8manifest.json' },");
            builder.AppendLine("    @{ Url = './Schemas/settings_table.schema.json'; Match = '/Tables/*.h8table.json' },");
            builder.AppendLine("    @{ Url = './Schemas/locale.schema.json'; Match = '/Locales/*.h8loc.json' }");
            builder.AppendLine(")");
            builder.AppendLine("foreach ($requiredMapping in $requiredSchemaMappings) {");
            builder.AppendLine("    $matched = $false");
            builder.AppendLine("    foreach ($schemaMapping in $schemaMappings) {");
            builder.AppendLine("        $fileMatches = @($schemaMapping.fileMatch | ForEach-Object { [string]$_ })");
            builder.AppendLine("        if ([string]$schemaMapping.url -eq $requiredMapping.Url -and $fileMatches -contains $requiredMapping.Match) {");
            builder.AppendLine("            $matched = $true");
            builder.AppendLine("            break");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    if (-not $matched) {");
            builder.AppendLine("        Fail ('.vscode/settings.json missing schema mapping ' + $requiredMapping.Url + ' -> ' + $requiredMapping.Match)");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Write-Host 'PASS HECTON-8 external starter structure'");
            return builder.ToString();
        }

        private static string JoinLines(params string[] lines)
        {
            return string.Join(global::System.Environment.NewLine, lines) + global::System.Environment.NewLine;
        }

        private static void OpenRelativePath(string relativePath)
        {
            string fullPath = ResolveProjectPath(relativePath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Missing SDK File", fullPath, "OK");
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
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
