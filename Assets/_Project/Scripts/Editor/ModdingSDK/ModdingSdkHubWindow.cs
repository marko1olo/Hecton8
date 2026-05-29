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
            "Runtime API: envelope-only. Public authors start from External Starter Kit; Managed DLL entries are legacy/internal and disabled by the loader.";
        private const string LegacyBuilderWarning =
            "Internal legacy package builder. Public authors should use External Starter Kit; managed DLL and loose AssetBundle paths are disabled by envelope-only runtime policy.";
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
        private bool _lastValidatorFailed;
        private readonly object _validatorOutputLock = new object();
        private DiagnosticsProcess _runningValidatorProcess;
        private StringBuilder _runningValidatorStdout;
        private StringBuilder _runningValidatorStderr;
        private bool _runningValidatorCompleted;
        private int _runningValidatorExitCode = -1;

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

        private void OnDisable()
        {
            EditorApplication.update -= PollRunningValidator;
            if (_runningValidatorProcess == null)
                return;

            try
            {
                if (!_runningValidatorProcess.HasExited)
                    _runningValidatorProcess.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ModdingSdkHubWindow] Validator cleanup failed: " + exception.Message);
            }
            finally
            {
                DisposeRunningValidator();
            }
        }

        private void DrawPrimaryActions()
        {
            EditorGUILayout.LabelField("Public Authoring", EditorStyles.boldLabel);

            if (GUILayout.Button("Create External Starter Kit", GUILayout.Height(30f)))
            {
                _lastValidatorSummary = CreateExternalStarterKit();
                _lastValidatorFailed = _lastValidatorSummary.StartsWith("External starter kit creation failed:", StringComparison.Ordinal);
                Repaint();
            }

            if (GUILayout.Button("Open Starter Kit Workbench", GUILayout.Height(28f)))
                ExternalStarterKitWorkbenchWindow.ShowWindow();

            if (GUILayout.Button("Open External Starter Kit", GUILayout.Height(24f)))
                RevealRelativePath(ExternalStarterKitRoot);

            if (GUILayout.Button("Open Local Mods Folder", GUILayout.Height(24f)))
                RevealRelativePath("Mods");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Internal Legacy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(LegacyBuilderWarning, MessageType.Warning);

            if (GUILayout.Button("Open Internal Legacy Mod Builder", GUILayout.Height(24f)))
                OpenLegacyModBuilder();
        }

        private static void OpenLegacyModBuilder()
        {
            if (!EditorUtility.DisplayDialog(
                    "Internal Legacy Mod Builder",
                    LegacyBuilderWarning,
                    "Open Legacy Builder",
                    "Cancel"))
            {
                return;
            }

            ModBuilderWindow.ShowWindow();
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

            EditorGUI.BeginDisabledGroup(IsValidatorRunning);
            if (GUILayout.Button("Run Static Mod API Validator", GUILayout.Height(30f)))
                RunStaticValidator();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Run APEX Source Guard", GUILayout.Height(30f)))
                RunApexSourceGuard();

            if (!string.IsNullOrWhiteSpace(_lastValidatorSummary))
                EditorGUILayout.HelpBox(_lastValidatorSummary, _lastValidatorFailed ? MessageType.Error : MessageType.Info);

            if (IsValidatorRunning)
                EditorGUILayout.HelpBox("Static validator running.", MessageType.Info);
        }

        private void RunApexSourceGuard()
        {
            ApexIntegratorSourceGuardResult result = ApexIntegratorSourceGuard.RunDefaultScope();
            _lastValidatorSummary = result.Summary;
            _lastValidatorFailed = result.Failed;

            if (result.Failed)
                Debug.LogError(result.Summary);
            else
                Debug.Log(result.Summary);

            Repaint();
        }

        private void RunStaticValidator()
        {
            if (IsValidatorRunning)
            {
                _lastValidatorSummary = "Static validator already running.";
                _lastValidatorFailed = false;
                Repaint();
                return;
            }

            string scriptPath = ResolveProjectPath(StaticValidatorPath);
            if (!File.Exists(scriptPath))
            {
                _lastValidatorSummary = "Missing validator: " + scriptPath;
                _lastValidatorFailed = true;
                Repaint();
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

                DiagnosticsProcess process = new DiagnosticsProcess
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                _runningValidatorStdout = new StringBuilder(2048);
                _runningValidatorStderr = new StringBuilder(2048);
                _runningValidatorCompleted = false;
                _runningValidatorExitCode = -1;
                _runningValidatorProcess = process;

                process.OutputDataReceived += (sender, args) => AppendValidatorOutput(_runningValidatorStdout, args.Data);
                process.ErrorDataReceived += (sender, args) => AppendValidatorOutput(_runningValidatorStderr, args.Data);
                process.Exited += (sender, args) => MarkValidatorCompleted();

                if (!process.Start())
                {
                    _lastValidatorSummary = "Validator process did not start.";
                    _lastValidatorFailed = true;
                    DisposeRunningValidator();
                    Repaint();
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _lastValidatorSummary = "Static validator running.";
                _lastValidatorFailed = false;
                EditorApplication.update -= PollRunningValidator;
                EditorApplication.update += PollRunningValidator;
            }
            catch (Exception exception)
            {
                _lastValidatorSummary = "Validator launch failed: " + exception.Message;
                _lastValidatorFailed = true;
                DisposeRunningValidator();
                Debug.LogError("[ModdingSdkHubWindow] Static validator launch failed: " + exception);
            }

            Repaint();
        }

        private bool IsValidatorRunning
        {
            get { return _runningValidatorProcess != null; }
        }

        private void AppendValidatorOutput(StringBuilder builder, string line)
        {
            if (builder == null || line == null)
                return;

            lock (_validatorOutputLock)
            {
                builder.AppendLine(line);
            }
        }

        private void MarkValidatorCompleted()
        {
            DiagnosticsProcess process = _runningValidatorProcess;
            if (process == null)
                return;

            try
            {
                _runningValidatorExitCode = process.ExitCode;
            }
            catch
            {
                _runningValidatorExitCode = -1;
            }

            _runningValidatorCompleted = true;
        }

        private void PollRunningValidator()
        {
            if (!_runningValidatorCompleted)
                return;

            EditorApplication.update -= PollRunningValidator;

            string stdout;
            string stderr;
            lock (_validatorOutputLock)
            {
                stdout = _runningValidatorStdout != null ? _runningValidatorStdout.ToString() : string.Empty;
                stderr = _runningValidatorStderr != null ? _runningValidatorStderr.ToString() : string.Empty;
            }

            int exitCode = _runningValidatorExitCode;
            _lastValidatorSummary = BuildValidatorSummary(exitCode, stdout, stderr);
            _lastValidatorFailed = exitCode != 0;
            DisposeRunningValidator();
            Repaint();
        }

        private void DisposeRunningValidator()
        {
            EditorApplication.update -= PollRunningValidator;
            if (_runningValidatorProcess != null)
            {
                _runningValidatorProcess.Dispose();
                _runningValidatorProcess = null;
            }

            _runningValidatorStdout = null;
            _runningValidatorStderr = null;
            _runningValidatorCompleted = false;
            _runningValidatorExitCode = -1;
        }

        internal static string CreateExternalStarterKit()
        {
            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);

            try
            {
                Directory.CreateDirectory(rootPath);
                Directory.CreateDirectory(Path.Combine(rootPath, "Content"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Content", "Assets"));
                Directory.CreateDirectory(Path.Combine(rootPath, "Docs"));
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
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "README.md"), BuildStarterKitTemplateFile("README.md", BuildStarterKitReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Docs", "capabilities.md"), BuildStarterKitTemplateFile("Docs/capabilities.md", BuildCapabilitiesGuide));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "h8mod.ps1"), BuildStarterKitTemplateFile("h8mod.ps1", BuildStarterKitLauncherScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "mod.h8manifest.json"), BuildStarterKitTemplateFile("mod.h8manifest.json", BuildAuthoringManifestTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "mod.json"), BuildStarterKitTemplateFile("mod.json", BuildRuntimeManifestTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Content", "README.md"), BuildStarterKitTemplateFile("Content/README.md", BuildContentReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Content", "Assets", "README.md"), BuildStarterKitTemplateFile("Content/Assets/README.md", BuildContentAssetsReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Content", "assets.h8manifest.json"), BuildStarterKitTemplateFile("Content/assets.h8manifest.json", BuildAssetManifestTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Graphs", "main.h8graph.json"), BuildStarterKitTemplateFile("Graphs/main.h8graph.json", BuildGraphTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tables", "settings.h8table.json"), BuildStarterKitTemplateFile("Tables/settings.h8table.json", BuildSettingsTableTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Locales", "en.h8loc.json"), BuildStarterKitTemplateFile("Locales/en.h8loc.json", BuildLocaleTemplate));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Generated", "README.md"), BuildStarterKitTemplateFile("Generated/README.md", BuildGeneratedReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Reports", "README.md"), BuildStarterKitTemplateFile("Reports/README.md", BuildReportsReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Reference", "README.md"), BuildStarterKitTemplateFile("Reference/README.md", BuildReferenceReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "assets.schema.json"), BuildStarterKitTemplateFile("Schemas/assets.schema.json", BuildAssetsSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "h8graph.schema.json"), BuildStarterKitTemplateFile("Schemas/h8graph.schema.json", BuildGraphSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "h8mod.authoring.schema.json"), BuildStarterKitTemplateFile("Schemas/h8mod.authoring.schema.json", BuildAuthoringManifestSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "locale.schema.json"), BuildStarterKitTemplateFile("Schemas/locale.schema.json", BuildLocaleSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "runtime.mod.schema.json"), BuildStarterKitTemplateFile("Schemas/runtime.mod.schema.json", BuildRuntimeManifestSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Schemas", "settings_table.schema.json"), BuildStarterKitTemplateFile("Schemas/settings_table.schema.json", BuildSettingsTableSchema));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "README.md"), BuildStarterKitTemplateFile("Tools/README.md", BuildToolsReadme));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_asset_entry_snippet.ps1"), BuildStarterKitTemplateFile("Tools/apply_asset_entry_snippet.ps1", BuildStarterKitAssetEntryApplyScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "build_review_manifest.ps1"), BuildStarterKitTemplateFile("Tools/build_review_manifest.ps1", BuildStarterKitReviewManifestScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "build_submission_package.ps1"), BuildStarterKitTemplateFile("Tools/build_submission_package.ps1", BuildStarterKitSubmissionPackageScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "configure_dependencies.ps1"), BuildStarterKitTemplateFile("Tools/configure_dependencies.ps1", BuildStarterKitDependenciesScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "configure_manifest_contract.ps1"), BuildStarterKitTemplateFile("Tools/configure_manifest_contract.ps1", BuildStarterKitManifestContractScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_first_mod.ps1"), BuildStarterKitTemplateFile("Tools/create_first_mod.ps1", BuildStarterKitFirstModScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "install_local_mod.ps1"), BuildStarterKitTemplateFile("Tools/install_local_mod.ps1", BuildStarterKitInstallLocalModScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "diagnose_local_mods.ps1"), BuildStarterKitTemplateFile("Tools/diagnose_local_mods.ps1", BuildStarterKitDiagnoseLocalModsScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_graph_node_snippet.ps1"), BuildStarterKitTemplateFile("Tools/apply_graph_node_snippet.ps1", BuildStarterKitGraphNodeApplyScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_locale_entry_snippet.ps1"), BuildStarterKitTemplateFile("Tools/apply_locale_entry_snippet.ps1", BuildStarterKitLocaleEntryApplyScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_settings_row_snippet.ps1"), BuildStarterKitTemplateFile("Tools/apply_settings_row_snippet.ps1", BuildStarterKitSettingsRowApplyScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_asset_entry_snippet.ps1"), BuildStarterKitTemplateFile("Tools/create_asset_entry_snippet.ps1", BuildStarterKitAssetEntrySnippetScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_locale_entry_snippet.ps1"), BuildStarterKitTemplateFile("Tools/create_locale_entry_snippet.ps1", BuildStarterKitLocaleEntrySnippetScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_graph_node_snippet.ps1"), BuildStarterKitTemplateFile("Tools/create_graph_node_snippet.ps1", BuildStarterKitGraphNodeSnippetScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_settings_row_snippet.ps1"), BuildStarterKitTemplateFile("Tools/create_settings_row_snippet.ps1", BuildStarterKitSettingsRowSnippetScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "list_allowed_opcodes.ps1"), BuildStarterKitTemplateFile("Tools/list_allowed_opcodes.ps1", BuildStarterKitAllowedOpcodesScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "prepare_mod.ps1"), BuildStarterKitTemplateFile("Tools/prepare_mod.ps1", BuildStarterKitPrepareScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "set_mod_identity.ps1"), BuildStarterKitTemplateFile("Tools/set_mod_identity.ps1", BuildStarterKitIdentityScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "validate_structure.ps1"), BuildStarterKitTemplateFile("Tools/validate_structure.ps1", BuildStarterKitValidatorScript));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, ".vscode", "settings.json"), BuildStarterKitTemplateFile(".vscode/settings.json", BuildVsCodeSettings));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, ".vscode", "tasks.json"), BuildStarterKitTemplateFile(".vscode/tasks.json", BuildVsCodeTasks));
                createdCount += CopyReferenceFileIfMissing(AllowedOpcodesReferencePath, Path.Combine(rootPath, "Reference", "allowed_opcodes.csv"));
                createdCount += CopyReferenceFileIfMissing(KernelTuningProfilesReferencePath, Path.Combine(rootPath, "Reference", "kernel_tuning_profiles.csv"));

                string summary =
                    "External starter kit ready: " + rootPath + global::System.Environment.NewLine +
                    "Files created: " + createdCount + global::System.Environment.NewLine +
                    "Existing files were not overwritten.";
                EditorUtility.RevealInFinder(rootPath);
                return summary;
            }
            catch (Exception exception)
            {
                string summary = "External starter kit creation failed: " + exception.Message;
                Debug.LogError("[ModdingSdkHubWindow] External starter kit creation failed: " + exception);
                return summary;
            }
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

        private static string BuildStarterKitToolFromTemplate(string toolRelativePath)
        {
            string sourcePath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/" + toolRelativePath);
            if (!File.Exists(sourcePath))
            {
                return
                    "param()" + global::System.Environment.NewLine +
                    "Write-Error 'Missing checked-in starter tool template: " + toolRelativePath + "'" + global::System.Environment.NewLine +
                    "exit 1" + global::System.Environment.NewLine;
            }

            return File.ReadAllText(sourcePath);
        }

        private static string BuildStarterKitTemplateFile(string templateRelativePath, Func<string> fallbackFactory)
        {
            string sourcePath = ResolveProjectPath("ModdingSDK/ExternalStarterKit/" + templateRelativePath);
            if (File.Exists(sourcePath))
                return File.ReadAllText(sourcePath);

            return fallbackFactory();
        }

        private static string BuildStarterKitReadme()
        {
            return
                "# HECTON-8 External Mod Starter Kit" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "This folder is for public mod authors working outside the HECTON-8 Unity project." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "First setup:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "After edits:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Optional menu:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Use pwsh instead of powershell on macOS/Linux with PowerShell 7. The tools normalize child paths internally; do not rewrite the folder layout per platform." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Do you need Unity?" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- No Unity project is required for manifest, graph, table, locale, content asset declaration, and validation authoring." + global::System.Environment.NewLine +
                "- Read Docs/capabilities.md first. It is the current source of truth for what modders can and cannot do with this starter kit." + global::System.Environment.NewLine +
                "- If you do use the HECTON-8 Unity project, open Hecton/Modding/External Starter Kit Workbench; it can create/refresh missing starter files, shows required starter-file health and Capability Matrix, configures manifest capabilities/budgets, runs these same tools asynchronously, generates graph/settings/locale/content asset snippets, applies graph/settings/locale/content asset snippets with validation, opens the core contracts, and shows review summary plus review manifest freshness without changing the file contract." + global::System.Environment.NewLine +
                "- Unity is also useful for advanced asset preview." + global::System.Environment.NewLine +
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
                "- h8mod.ps1: root no-Unity launcher for first playable mod creation, local discovery install, local Mods diagnosis, dependency editing, setup, validate, review, prepare, submission package build, opcode discovery, manifest capability/budget configuration, graph/settings/locale/content asset snippets, graph/settings/locale/content asset snippet apply, and capability-matrix display. first-mod sets identity, enables graph authoring, creates/applies one graph node, one setting, one locale entry, validates, and builds Reports/review_manifest.json. install-local copies the reviewed source set plus review manifest into Mods/<mod-id> for loader discovery only after byte/SHA-256 verification. diagnose-local inspects a local Mods folder recursively and reports manifest/review/dependency/runtime-boundary status without mutating files. dependencies edits dependency IDs in both manifests and validates. node-snippet accepts -NodeParametersJson and -NodeDisabled; parameters accept strict JSON or a flat CLI fallback like {Quantity:3,Item:demo}. asset-snippet accepts -AssetCrc32 auto and -AssetBytes -1 when the file exists. It delegates to Tools/*.ps1 and is not a runtime activation contract." + global::System.Environment.NewLine +
                "- Docs/capabilities.md: current capability matrix for public authors: supported authoring surfaces, forbidden runtime rights, and expansion route." + global::System.Environment.NewLine +
                "- mod.h8manifest.json: authoring manifest for Workbench/CLI style tools." + global::System.Environment.NewLine +
                "- mod.json: loader compatibility manifest; EntryAssembly and EntryType stay empty in envelope-only mode." + global::System.Environment.NewLine +
                "- Graphs/main.h8graph.json: command graph draft. Empty graph emits no packets. Non-empty nodes must use opcode hex tokens or comment aliases from Reference/allowed_opcodes.csv." + global::System.Environment.NewLine +
                "- Tables/settings.h8table.json: user-facing config table draft. Rows use canonical Id, lower-case Kind (bool, int, float, string, enum), and a matching Default value." + global::System.Environment.NewLine +
                "- Content/assets.h8manifest.json and Content/Assets/: CRC/asset declaration draft. Use asset-snippet and apply-asset-snippet to avoid hand-editing entries. Runtime use requires approval." + global::System.Environment.NewLine +
                "- Locales/en.h8loc.json: locale draft. Locale uses xx or xx-YY; string keys use the same canonical id form as other starter data. Runtime injection is not a public right yet." + global::System.Environment.NewLine +
                "- Generated/: SDK-produced binary output goes here. Do not hand-write .h8bin files." + global::System.Environment.NewLine +
                "- Reports/: validator, review, and future package reports go here." + global::System.Environment.NewLine +
                "- Reference/: copied opcode and tuning CSV references from the project docs." + global::System.Environment.NewLine +
                "- Schemas/: JSON Schemas for editor autocomplete and schema-aware validation." + global::System.Environment.NewLine +
                "- .vscode/settings.json: optional VS Code JSON schema mapping plus hecton8.powerShellExecutable override for the task runner. The local validator checks the expected schema URL/fileMatch pairs and rejects invalid settings/locale data before review packaging." + global::System.Environment.NewLine +
                "- .vscode/tasks.json: VS Code Tasks surface for first playable mod creation, local discovery install, local Mods diagnosis, setup, validate, prepare, submission, capability/opcode discovery, snippet creation/apply, and manifest contract edits. Tasks route through h8mod.ps1 only; they do not bypass validation or create runtime rights." + global::System.Environment.NewLine +
                "- Tools/prepare_mod.ps1: one-command no-Unity setup/review loop. With -Id it writes identity, validates, and builds the review manifest; without -Id it validates existing manifests and rebuilds the review manifest." + global::System.Environment.NewLine +
                "- Tools/validate_structure.ps1: local no-Unity structure validator for required files, canonical IDs, manifest parity, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, graph opcode allowlist checks, graph budget parity, envelope-only flags, and managed-entry disablement." + global::System.Environment.NewLine +
                "- Tools/build_review_manifest.ps1: local no-Unity review manifest builder that validates first, then writes Reports/review_manifest.json with package identity, sorted file paths, byte counts, total bytes, explicit source limits, and SHA-256 hashes for submission/review. It rejects more than 256 source files, any source file over 4194304 bytes, or more than 33554432 total source bytes before hashing." + global::System.Environment.NewLine +
                "- Tools/build_submission_package.ps1: local no-Unity submission packer. It runs prepare, then writes Generated/<mod-id>_submission.zip containing the reviewed starter sources plus Reports/review_manifest.json. It writes to a temp zip first and restores the previous submission zip if final replacement fails. This is a review handoff artifact, not a runtime install stamp." + global::System.Environment.NewLine +
                "- Tools/install_local_mod.ps1: local no-Unity discovery installer. It runs prepare, verifies reviewed files against Reports/review_manifest.json byte counts and SHA-256 hashes, then atomically copies the reviewed source set plus review manifest into Mods/<mod-id>. This is loader discovery only; managed entry and loose content ingestion stay disabled." + global::System.Environment.NewLine +
                "- Tools/diagnose_local_mods.ps1: local no-Unity read-only Mods inspector. It checks recursive loader discovery, loader caps, mod.json health, Reports/review_manifest.json hashes, duplicate IDs, missing dependencies, dependency cycles, load order, managed DLL/bundle/lang counts, and the envelope-only disable reason for each package under ProjectRoot/Mods or -ModsRoot." + global::System.Environment.NewLine +
                "- Tools/configure_dependencies.ps1: local no-Unity dependency helper that edits mod.h8manifest.json and mod.json together, rejects invalid IDs, duplicates, and self-dependencies, validates after write, and restores both manifests on failure." + global::System.Environment.NewLine +
                "- Tools/configure_manifest_contract.ps1: local no-Unity manifest helper that enables/disables public authoring capabilities and sets capped budgets with validation and rollback. Capabilities are review metadata, not runtime rights." + global::System.Environment.NewLine +
                "- Tools/create_first_mod.ps1: local no-Unity onboarding helper that runs bounded identity, manifest contract, graph, settings, locale, validation, and review-manifest tools in sequence. -Replace makes starter onboarding rerunnable for the same sample IDs." + global::System.Environment.NewLine +
                "- Tools/list_allowed_opcodes.ps1: local no-Unity graph helper that prints the allowed opcode aliases and hex tokens accepted by Graphs/main.h8graph.json." + global::System.Environment.NewLine +
                "- Tools/create_graph_node_snippet.ps1: local no-Unity graph helper that writes Generated/graph_node_snippet.json from a validated node id, allowed opcode, ParametersJson object or flat CLI fallback, and optional disabled state; it does not mutate Graphs/main.h8graph.json." + global::System.Environment.NewLine +
                "- Tools/apply_graph_node_snippet.ps1: local no-Unity graph helper that inserts Generated/graph_node_snippet.json into Graphs/main.h8graph.json, rejects duplicate node ids unless -Replace is explicit, raises the graph/manifest envelope budget to one when the first node is applied, validates after the atomic temp-write, and restores previous files on failure." + global::System.Environment.NewLine +
                "- Tools/create_settings_row_snippet.ps1: local no-Unity settings helper that writes Generated/settings_row_snippet.json from a canonical setting id, supported kind, and typed default; it does not mutate Tables/settings.h8table.json." + global::System.Environment.NewLine +
                "- Tools/create_locale_entry_snippet.ps1: local no-Unity locale helper that writes Generated/locale_entry_snippet.json from a canonical locale key and text value; it does not mutate Locales/en.h8loc.json." + global::System.Environment.NewLine +
                "- Tools/apply_settings_row_snippet.ps1: local no-Unity settings helper that inserts Generated/settings_row_snippet.json into Tables/settings.h8table.json, rejects duplicates unless -Replace is explicit, validates after the atomic temp-write, and restores the previous table on failure." + global::System.Environment.NewLine +
                "- Tools/apply_locale_entry_snippet.ps1: local no-Unity locale helper that inserts Generated/locale_entry_snippet.json into Locales/en.h8loc.json, rejects duplicates unless -Replace is explicit, validates after the atomic temp-write, and restores the previous locale file on failure." + global::System.Environment.NewLine +
                "- Tools/create_asset_entry_snippet.ps1: local no-Unity content helper that writes Generated/asset_entry_snippet.json from a canonical asset id, kind, Content/Assets path, CRC32, and byte length. Use -Crc32 auto and -Bytes -1 to compute them from an existing file." + global::System.Environment.NewLine +
                "- Tools/apply_asset_entry_snippet.ps1: local no-Unity content helper that inserts Generated/asset_entry_snippet.json into Content/assets.h8manifest.json, verifies the file CRC/bytes, rejects duplicate asset ids unless -Replace is explicit, raises MaxAssetBytes, validates after the atomic temp-write, and restores previous files on failure." + global::System.Environment.NewLine +
                "- Tools/set_mod_identity.ps1: local no-Unity identity helper that safely writes matching mod id/name/author/version values into both manifests, then validates the folder." + global::System.Environment.NewLine;
        }

        private static string BuildCapabilitiesGuide()
        {
            return
                "# HECTON-8 Mod Capability Matrix" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "This file is the public starter-kit answer to: what can a modder create today, what is blocked, and where new capabilities must be added." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Runtime rule: public gameplay ingress is envelope-only. Mods author data and review packages; the engine owns execution, validation, save authority, hot SignalBus lanes, GlobalRegistry routes, and asset loading." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## Supported now" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "| Surface | Files | What the modder can do | Runtime status |" + global::System.Environment.NewLine +
                "| --- | --- | --- | --- |" + global::System.Environment.NewLine +
                "| Identity | mod.h8manifest.json, mod.json | Set id, display name, author, version, dependencies, API version. | Validated before review. |" + global::System.Environment.NewLine +
                "| Command graph draft | Graphs/main.h8graph.json, Reference/allowed_opcodes.csv | Describe allowed FutureCommandEnvelope requests using approved opcode aliases/hex tokens. | Review/bake required before packets can reach runtime. |" + global::System.Environment.NewLine +
                "| Settings | Tables/settings.h8table.json | Define user-facing bool/int/float/string/enum options with typed defaults. | Authoring/review contract now; runtime UI binding is engine-owned. |" + global::System.Environment.NewLine +
                "| Locale | Locales/en.h8loc.json | Provide keyed localized text in canonical key form. | Authoring/review contract now; runtime injection is not a public right. |" + global::System.Environment.NewLine +
                "| Content manifest | Content/assets.h8manifest.json, Content/Assets/ | Declare content ids, paths, CRCs, and byte budgets through bounded snippet/apply tools. | Approval required; no loose runtime ingestion from this folder. |" + global::System.Environment.NewLine +
                "| Review package | Reports/review_manifest.json, Generated/*_submission.zip | Produce one hashed handoff artifact for review. | Not a runtime install stamp. |" + global::System.Environment.NewLine +
                "| Local Mods diagnosis | Mods/<mod-id>, Reports/review_manifest.json | Inspect a local Mods folder through diagnose_local_mods.ps1 and report recursive discovery, loader caps, manifest health, dependency blockers, duplicate IDs, cycles, load order, review hash drift, file counts, and envelope-only disable reasons. | Read-only diagnostic; no runtime rights. |" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## Not public rights" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- No Harmony, BepInEx, managed gameplay DLL execution, arbitrary Unity scripts, frame callbacks, or direct C# patching." + global::System.Environment.NewLine +
                "- No direct GameObject, ScriptableObject, material, save, inventory, world, physics, AI, renderer, or GlobalRegistry mutation." + global::System.Environment.NewLine +
                "- No runtime loading of loose AssetBundles, PNGs, audio, localization files, or arbitrary paths from this starter folder." + global::System.Environment.NewLine +
                "- No new hot SignalBus lane or GlobalSignals queue from a mod. New lanes require engine owner, capacity, schema, and runtime proof." + global::System.Environment.NewLine +
                "- No gameplay truth changes through settings, locale, content manifests, or review zips without an engine-owned validated command or resource route." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## How to create a mod without Unity" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "1. Run h8mod.ps1 -Action setup with id/name/author/version." + global::System.Environment.NewLine +
                "2. Inspect h8mod.ps1 -Action capabilities and h8mod.ps1 -Action opcodes." + global::System.Environment.NewLine +
                "3. Use h8mod.ps1 -Action manifest-contract to enable reviewed authoring capabilities and set capped budgets without hand-editing JSON." + global::System.Environment.NewLine +
                "4. Put content files under Content/Assets/ when declaring data blobs, raw textures, or audio clips for review." + global::System.Environment.NewLine +
                "5. Use h8mod.ps1 -Action node-snippet -NodeParametersJson '{}' and optional -NodeDisabled to generate safe graph node JSON under Generated/. For non-empty CLI parameters, strict JSON and flat fallback forms such as {Quantity:3,Item:demo} are accepted." + global::System.Environment.NewLine +
                "6. Use h8mod.ps1 -Action apply-node-snippet to insert the generated graph node into Graphs/main.h8graph.json with duplicate checks, budget repair, validation, and rollback." + global::System.Environment.NewLine +
                "7. Use h8mod.ps1 -Action setting-snippet and h8mod.ps1 -Action locale-snippet to generate safe settings/locale JSON under Generated/." + global::System.Environment.NewLine +
                "8. Use h8mod.ps1 -Action apply-setting-snippet and h8mod.ps1 -Action apply-locale-snippet to insert the generated settings/locale snippets into Tables/settings.h8table.json and Locales/en.h8loc.json with duplicate checks and validation." + global::System.Environment.NewLine +
                "9. Use h8mod.ps1 -Action asset-snippet -AssetCrc32 auto -AssetBytes -1 to generate a safe content asset entry, then h8mod.ps1 -Action apply-asset-snippet to insert it into Content/assets.h8manifest.json with CRC/byte proof, budget repair, validation, and rollback." + global::System.Environment.NewLine +
                "10. Run h8mod.ps1 -Action validate." + global::System.Environment.NewLine +
                "11. Run h8mod.ps1 -Action submission and hand off Generated/<mod-id>_submission.zip." + global::System.Environment.NewLine +
                "12. Run h8mod.ps1 -Action diagnose-local -ProjectRoot <HECTON-8 project root> after install-local to inspect the local Mods folder, dependency graph, and load order without mutating runtime files." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## How to create a mod inside the HECTON-8 Unity project" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Open Hecton/Modding/External Starter Kit Workbench. Use Starter Health, Capability Matrix, Graph Contract Preview, Authoring Data Preview, Manifest Contract, Dependency Contract, Authoring Snippets, Content Asset Snippet, Graph Node Snippet, Validation And Review, and Submission Package panels. The Workbench can configure manifest capabilities/budgets/dependencies and generate/apply graph/settings/locale/content asset snippets through the same bounded starter tools, including Graph Opcode Picker, Parameters JSON, disabled-node, asset kind picker, CRC/byte fields, and replace-on-apply controls; it does not grant extra runtime rights." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## Expansion route" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "New mod powers must be added as engine-owned capability contracts: schema entry, static validator rule, Workbench visibility, starter docs, review-package proof, runtime owner, bounded budget, and runtime telemetry. Mods request; the engine validates and executes." + global::System.Environment.NewLine;
        }

        private static string BuildStarterKitLauncherScript()
        {
            StringBuilder builder = new StringBuilder(7168);
            builder.AppendLine("param(");
            builder.AppendLine("    [ValidateSet('menu','first-mod','install-local','diagnose-local','dependencies','setup','validate','review','prepare','submission','opcodes','opcodes-json','node-snippet','apply-node-snippet','setting-snippet','locale-snippet','apply-setting-snippet','apply-locale-snippet','asset-snippet','apply-asset-snippet','manifest-contract','capabilities')]");
            builder.AppendLine("    [string]$Action = 'menu',");
            builder.AppendLine("    [string]$Id = '',");
            builder.AppendLine("    [string]$DisplayName = '',");
            builder.AppendLine("    [string]$Author = '',");
            builder.AppendLine("    [string]$Version = '',");
            builder.AppendLine("    [string]$NodeId = 'node.spawn_item',");
            builder.AppendLine("    [string]$Opcode = 'SpawnItem',");
            builder.AppendLine("    [string]$NodeParametersJson = '{}',");
            builder.AppendLine("    [string]$Output = 'Generated/graph_node_snippet.json',");
            builder.AppendLine("    [string]$NodeSnippet = 'Generated/graph_node_snippet.json',");
            builder.AppendLine("    [switch]$NodeDisabled,");
            builder.AppendLine("    [string]$SettingId = 'setting.example_toggle',");
            builder.AppendLine("    [string]$SettingKind = 'bool',");
            builder.AppendLine("    [string]$SettingDefault = 'false',");
            builder.AppendLine("    [string]$SettingOutput = 'Generated/settings_row_snippet.json',");
            builder.AppendLine("    [string]$SettingSnippet = 'Generated/settings_row_snippet.json',");
            builder.AppendLine("    [string]$LocaleKey = 'text.example_line',");
            builder.AppendLine("    [string]$LocaleValue = 'Your localized text',");
            builder.AppendLine("    [string]$LocaleOutput = 'Generated/locale_entry_snippet.json',");
            builder.AppendLine("    [string]$LocaleSnippet = 'Generated/locale_entry_snippet.json',");
            builder.AppendLine("    [string]$AssetId = 'asset.example_blob',");
            builder.AppendLine("    [string]$AssetKind = 'data_blob',");
            builder.AppendLine("    [string]$AssetPath = 'Content/Assets/example.bytes',");
            builder.AppendLine("    [string]$AssetCrc32 = '00000000',");
            builder.AppendLine("    [long]$AssetBytes = 0,");
            builder.AppendLine("    [string]$AssetOutput = 'Generated/asset_entry_snippet.json',");
            builder.AppendLine("    [string]$AssetSnippet = 'Generated/asset_entry_snippet.json',");
            builder.AppendLine("    [string]$Capability = 'cap.graph.command_draft',");
            builder.AppendLine("    [ValidateSet('list','add','remove','clear')]");
            builder.AppendLine("    [string]$DependencyAction = 'list',");
            builder.AppendLine("    [string]$DependencyId = '',");
            builder.AppendLine("    [ValidateSet('unchanged','enable','disable')]");
            builder.AppendLine("    [string]$CapabilityState = 'enable',");
            builder.AppendLine("    [int]$MaxEnvelopesPerFrame = -1,");
            builder.AppendLine("    [long]$MaxAssetBytes = -1,");
            builder.AppendLine("    [switch]$Replace,");
            builder.AppendLine("    [switch]$BuildSubmission,");
            builder.AppendLine("    [string]$SubmissionOutput = '',");
            builder.AppendLine("    [string]$ProjectRoot = '',");
            builder.AppendLine("    [string]$ModsRoot = '',");
            builder.AppendLine("    [switch]$Json");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath {");
            builder.AppendLine("    param(");
            builder.AppendLine("        [string]$BasePath,");
            builder.AppendLine("        [Parameter(ValueFromRemainingArguments = $true)]");
            builder.AppendLine("        [string[]]$Segments");
            builder.AppendLine("    )");
            builder.AppendLine();
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in $Segments) {");
            builder.AppendLine("        foreach ($part in ($segment.Replace('\\','/') -split '/')) {");
            builder.AppendLine("            if (-not [string]::IsNullOrWhiteSpace($part)) {");
            builder.AppendLine("                $current = Join-Path $current $part");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $current");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Resolve-StarterTool([string]$RelativePath) {");
            builder.AppendLine("    $tool = Join-StarterPath $Root $RelativePath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {");
            builder.AppendLine("        Fail ('Missing starter tool: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine("    return $tool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Complete-StarterTool {");
            builder.AppendLine("    if (-not $?) {");
            builder.AppendLine("        exit 1");
            builder.AppendLine("    }");
            builder.AppendLine("    if ($global:LASTEXITCODE -ne 0) {");
            builder.AppendLine("        exit $global:LASTEXITCODE");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Validate {");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/validate_structure.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Review {");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/build_review_manifest.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-PrepareExisting {");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/prepare_mod.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-SubmissionPackage {");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/build_submission_package.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($SubmissionOutput)) {");
            builder.AppendLine("        & $tool -Root $Root");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Output $SubmissionOutput");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-InstallLocal([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $installProjectRoot = $ProjectRoot");
            builder.AppendLine("    $installModsRoot = $ModsRoot");
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $installProjectRoot = Read-SetupValue $installProjectRoot 'HECTON-8 project root, blank to auto-detect from starter location'");
            builder.AppendLine("        $installModsRoot = Read-SetupValue $installModsRoot 'Mods root override, blank to use ProjectRoot/Mods'");
            builder.AppendLine("    }");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/install_local_mod.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -ProjectRoot $installProjectRoot -ModsRoot $installModsRoot -Replace");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -ProjectRoot $installProjectRoot -ModsRoot $installModsRoot");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-DiagnoseLocal([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $diagnoseProjectRoot = $ProjectRoot");
            builder.AppendLine("    $diagnoseModsRoot = $ModsRoot");
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $diagnoseProjectRoot = Read-SetupValue $diagnoseProjectRoot 'HECTON-8 project root, blank to auto-detect from starter location'");
            builder.AppendLine("        $diagnoseModsRoot = Read-SetupValue $diagnoseModsRoot 'Mods root override, blank to use ProjectRoot/Mods'");
            builder.AppendLine("    }");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/diagnose_local_mods.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Json) {");
            builder.AppendLine("        & $tool -Root $Root -ProjectRoot $diagnoseProjectRoot -ModsRoot $diagnoseModsRoot -Json");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -ProjectRoot $diagnoseProjectRoot -ModsRoot $diagnoseModsRoot");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Opcodes([bool]$Json) {");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/list_allowed_opcodes.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Json) {");
            builder.AppendLine("        & $tool -Root $Root -Json");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Capabilities {");
            builder.AppendLine("    $guide = Join-StarterPath $Root 'Docs/capabilities.md'");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $guide -PathType Leaf)) {");
            builder.AppendLine("        Fail 'Missing Docs/capabilities.md'");
            builder.AppendLine("    }");
            builder.AppendLine("    Get-Content -LiteralPath $guide");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Dependencies([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $dependencyAction = $DependencyAction");
            builder.AppendLine("    $dependencyId = $DependencyId");
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $dependencyAction = Read-SetupValue $dependencyAction 'Dependency action: list, add, remove, or clear'");
            builder.AppendLine("        if (@('list','add','remove','clear') -notcontains $dependencyAction) {");
            builder.AppendLine("            Fail 'Dependency action must be one of: list, add, remove, clear.'");
            builder.AppendLine("        }");
            builder.AppendLine("        if ($dependencyAction -eq 'add' -or $dependencyAction -eq 'remove') {");
            builder.AppendLine("            $dependencyId = Read-SetupValue $dependencyId 'Dependency mod id, example com.example.library'");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/configure_dependencies.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Json) {");
            builder.AppendLine("        & $tool -Root $Root -Action $dependencyAction -DependencyId $dependencyId -Json");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Action $dependencyAction -DependencyId $dependencyId");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-GraphNodeSnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetNodeId = $NodeId");
            builder.AppendLine("    $snippetOpcode = $Opcode");
            builder.AppendLine("    $snippetParametersJson = $NodeParametersJson");
            builder.AppendLine("    $snippetOutput = $Output");
            builder.AppendLine("    $snippetDisabled = $NodeDisabled");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetNodeId = Read-SetupValue $snippetNodeId 'Graph node id, example node.spawn_item'");
            builder.AppendLine("        $snippetOpcode = Read-SetupValue $snippetOpcode 'Opcode alias or hex, example SpawnItem'");
            builder.AppendLine("        $snippetParametersJson = Read-SetupValue $snippetParametersJson 'Parameters JSON object, example {}'");
            builder.AppendLine("        $snippetOutput = Read-SetupValue $snippetOutput 'Output path under Generated/, example Generated/graph_node_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/create_graph_node_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($snippetDisabled) {");
            builder.AppendLine("        & $tool -Root $Root -Id $snippetNodeId -Opcode $snippetOpcode -ParametersJson $snippetParametersJson -Output $snippetOutput -Disabled");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Id $snippetNodeId -Opcode $snippetOpcode -ParametersJson $snippetParametersJson -Output $snippetOutput");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-ApplyGraphNodeSnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetPath = $NodeSnippet");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetPath = Read-SetupValue $snippetPath 'Graph node snippet path under Generated/, example Generated/graph_node_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/apply_graph_node_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath -Replace");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-SettingsRowSnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetSettingId = $SettingId");
            builder.AppendLine("    $snippetSettingKind = $SettingKind");
            builder.AppendLine("    $snippetSettingDefault = $SettingDefault");
            builder.AppendLine("    $snippetSettingOutput = $SettingOutput");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetSettingId = Read-SetupValue $snippetSettingId 'Setting id, example setting.example_toggle'");
            builder.AppendLine("        $snippetSettingKind = Read-SetupValue $snippetSettingKind 'Setting kind: bool, int, float, string, or enum'");
            builder.AppendLine("        $snippetSettingDefault = Read-SetupValue $snippetSettingDefault 'Setting default value'");
            builder.AppendLine("        $snippetSettingOutput = Read-SetupValue $snippetSettingOutput 'Output path under Generated/, example Generated/settings_row_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/create_settings_row_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root -Id $snippetSettingId -Kind $snippetSettingKind -Default $snippetSettingDefault -Output $snippetSettingOutput");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-ApplySettingsRowSnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetPath = $SettingSnippet");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetPath = Read-SetupValue $snippetPath 'Settings snippet path under Generated/, example Generated/settings_row_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/apply_settings_row_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath -Replace");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-LocaleEntrySnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetLocaleKey = $LocaleKey");
            builder.AppendLine("    $snippetLocaleValue = $LocaleValue");
            builder.AppendLine("    $snippetLocaleOutput = $LocaleOutput");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetLocaleKey = Read-SetupValue $snippetLocaleKey 'Locale key, example text.example_line'");
            builder.AppendLine("        $snippetLocaleValue = Read-SetupValue $snippetLocaleValue 'Localized text value'");
            builder.AppendLine("        $snippetLocaleOutput = Read-SetupValue $snippetLocaleOutput 'Output path under Generated/, example Generated/locale_entry_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/create_locale_entry_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root -Key $snippetLocaleKey -Value $snippetLocaleValue -Output $snippetLocaleOutput");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-ApplyLocaleEntrySnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetPath = $LocaleSnippet");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetPath = Read-SetupValue $snippetPath 'Locale snippet path under Generated/, example Generated/locale_entry_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/apply_locale_entry_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath -Replace");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-AssetEntrySnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetAssetId = $AssetId");
            builder.AppendLine("    $snippetAssetKind = $AssetKind");
            builder.AppendLine("    $snippetAssetPath = $AssetPath");
            builder.AppendLine("    $snippetAssetCrc32 = $AssetCrc32");
            builder.AppendLine("    $snippetAssetBytes = $AssetBytes");
            builder.AppendLine("    $snippetAssetOutput = $AssetOutput");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetAssetId = Read-SetupValue $snippetAssetId 'Asset id, example asset.example_blob'");
            builder.AppendLine("        $snippetAssetKind = Read-SetupValue $snippetAssetKind 'Asset kind: data_blob, raw_texture, or audio_clip'");
            builder.AppendLine("        $snippetAssetPath = Read-SetupValue $snippetAssetPath 'Asset path under Content/Assets/, example Content/Assets/example.bytes'");
            builder.AppendLine("        $snippetAssetCrc32 = Read-SetupValue $snippetAssetCrc32 'CRC32 hex or auto'");
            builder.AppendLine("        $snippetAssetBytesText = Read-SetupValue ([string]$snippetAssetBytes) 'Byte length, or -1 to read the file length'");
            builder.AppendLine("        [long]$parsedBytes = 0");
            builder.AppendLine("        if (-not [long]::TryParse($snippetAssetBytesText, [ref]$parsedBytes)) {");
            builder.AppendLine("            Fail 'Asset bytes must be an integer.'");
            builder.AppendLine("        }");
            builder.AppendLine("        $snippetAssetBytes = $parsedBytes");
            builder.AppendLine("        $snippetAssetOutput = Read-SetupValue $snippetAssetOutput 'Output path under Generated/, example Generated/asset_entry_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/create_asset_entry_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root -Id $snippetAssetId -Kind $snippetAssetKind -Path $snippetAssetPath -Crc32 $snippetAssetCrc32 -Bytes $snippetAssetBytes -Output $snippetAssetOutput");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-ApplyAssetEntrySnippet([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $snippetPath = $AssetSnippet");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $snippetPath = Read-SetupValue $snippetPath 'Asset snippet path under Generated/, example Generated/asset_entry_snippet.json'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/apply_asset_entry_snippet.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath -Replace");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Snippet $snippetPath");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-ManifestContractConfig([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $contractCapability = $Capability");
            builder.AppendLine("    $contractCapabilityState = $CapabilityState");
            builder.AppendLine("    $contractMaxEnvelopesPerFrame = $MaxEnvelopesPerFrame");
            builder.AppendLine("    $contractMaxAssetBytes = $MaxAssetBytes");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $contractCapability = Read-SetupValue $contractCapability 'Capability id, example cap.graph.command_draft'");
            builder.AppendLine("        $contractCapabilityState = Read-SetupValue $contractCapabilityState 'Capability state: enable, disable, or unchanged'");
            builder.AppendLine("        if (@('unchanged','enable','disable') -notcontains $contractCapabilityState) {");
            builder.AppendLine("            Fail 'Capability state must be one of: unchanged, enable, disable.'");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        $envelopeText = Read-SetupValue ([string]$contractMaxEnvelopesPerFrame) 'MaxEnvelopesPerFrame, or -1 unchanged'");
            builder.AppendLine("        [int]$parsedEnvelopeBudget = 0");
            builder.AppendLine("        if (-not [int]::TryParse($envelopeText, [ref]$parsedEnvelopeBudget)) {");
            builder.AppendLine("            Fail 'MaxEnvelopesPerFrame must be an integer.'");
            builder.AppendLine("        }");
            builder.AppendLine("        $contractMaxEnvelopesPerFrame = $parsedEnvelopeBudget");
            builder.AppendLine();
            builder.AppendLine("        $assetText = Read-SetupValue ([string]$contractMaxAssetBytes) 'MaxAssetBytes, or -1 unchanged'");
            builder.AppendLine("        [long]$parsedAssetBudget = 0");
            builder.AppendLine("        if (-not [long]::TryParse($assetText, [ref]$parsedAssetBudget)) {");
            builder.AppendLine("            Fail 'MaxAssetBytes must be an integer.'");
            builder.AppendLine("        }");
            builder.AppendLine("        $contractMaxAssetBytes = $parsedAssetBudget");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/configure_manifest_contract.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root -Capability $contractCapability -CapabilityState $contractCapabilityState -MaxEnvelopesPerFrame $contractMaxEnvelopesPerFrame -MaxAssetBytes $contractMaxAssetBytes");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-FirstMod([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $firstId = if ([string]::IsNullOrWhiteSpace($Id)) { 'com.yourname.firstmod' } else { $Id }");
            builder.AppendLine("    $firstDisplayName = if ([string]::IsNullOrWhiteSpace($DisplayName)) { 'First HECTON Mod' } else { $DisplayName }");
            builder.AppendLine("    $firstAuthor = if ([string]::IsNullOrWhiteSpace($Author)) { 'YourName' } else { $Author }");
            builder.AppendLine("    $firstVersion = if ([string]::IsNullOrWhiteSpace($Version)) { '0.1.0' } else { $Version }");
            builder.AppendLine("    $firstNodeId = if ($NodeId -eq 'node.spawn_item') { 'node.first_spawn_item' } else { $NodeId }");
            builder.AppendLine("    $firstOpcode = if ([string]::IsNullOrWhiteSpace($Opcode)) { 'SpawnItem' } else { $Opcode }");
            builder.AppendLine("    $firstNodeParametersJson = if ($NodeParametersJson -eq '{}') { '{\"Item\":\"demo\",\"Quantity\":1}' } else { $NodeParametersJson }");
            builder.AppendLine("    $firstSettingId = if ($SettingId -eq 'setting.example_toggle') { 'setting.first_enabled' } else { $SettingId }");
            builder.AppendLine("    $firstSettingKind = if ([string]::IsNullOrWhiteSpace($SettingKind)) { 'bool' } else { $SettingKind }");
            builder.AppendLine("    $firstSettingDefault = if ($SettingDefault -eq 'false') { 'true' } else { $SettingDefault }");
            builder.AppendLine("    $firstLocaleKey = if ($LocaleKey -eq 'text.example_line') { 'text.first_mod_ready' } else { $LocaleKey }");
            builder.AppendLine("    $firstLocaleValue = if ($LocaleValue -eq 'Your localized text') { 'First HECTON mod ready.' } else { $LocaleValue }");
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/create_first_mod.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    if ($Replace -and $BuildSubmission) {");
            builder.AppendLine("        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -Replace -BuildSubmission");
            builder.AppendLine("    } elseif ($Replace) {");
            builder.AppendLine("        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -Replace");
            builder.AppendLine("    } elseif ($BuildSubmission) {");
            builder.AppendLine("        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -BuildSubmission");
            builder.AppendLine("    } else {");
            builder.AppendLine("        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue");
            builder.AppendLine("    }");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Require-SetupValue([string]$Value, [string]$Name) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) {");
            builder.AppendLine("        Fail ($Name + ' is required for setup. Provide -' + $Name + ' or run menu mode.')");
            builder.AppendLine("    }");
            builder.AppendLine("    return $Value");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Read-SetupValue([string]$Value, [string]$Prompt) {");
            builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($Value)) {");
            builder.AppendLine("        return $Value");
            builder.AppendLine("    }");
            builder.AppendLine("    return Read-Host $Prompt");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Invoke-Setup([bool]$PromptForMissingValues) {");
            builder.AppendLine("    $setupId = $Id");
            builder.AppendLine("    $setupDisplayName = $DisplayName");
            builder.AppendLine("    $setupAuthor = $Author");
            builder.AppendLine("    $setupVersion = $Version");
            builder.AppendLine();
            builder.AppendLine("    if ($PromptForMissingValues) {");
            builder.AppendLine("        $setupId = Read-SetupValue $setupId 'Mod id, example com.yourname.mod'");
            builder.AppendLine("        $setupDisplayName = Read-SetupValue $setupDisplayName 'Display name'");
            builder.AppendLine("        $setupAuthor = Read-SetupValue $setupAuthor 'Author'");
            builder.AppendLine("        $setupVersion = Read-SetupValue $setupVersion 'Version, example 0.1.0'");
            builder.AppendLine("    } else {");
            builder.AppendLine("        $setupId = Require-SetupValue $setupId 'Id'");
            builder.AppendLine("        $setupDisplayName = Require-SetupValue $setupDisplayName 'DisplayName'");
            builder.AppendLine("        $setupAuthor = Require-SetupValue $setupAuthor 'Author'");
            builder.AppendLine("        $setupVersion = Require-SetupValue $setupVersion 'Version'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $tool = Resolve-StarterTool 'Tools/prepare_mod.ps1'");
            builder.AppendLine("    $global:LASTEXITCODE = 0");
            builder.AppendLine("    & $tool -Root $Root -Id $setupId -DisplayName $setupDisplayName -Author $setupAuthor -Version $setupVersion");
            builder.AppendLine("    Complete-StarterTool");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Show-Menu {");
            builder.AppendLine("    Write-Host ''");
            builder.AppendLine("    Write-Host 'HECTON-8 External Starter Kit'");
            builder.AppendLine("    Write-Host '1 setup identity + build review'");
            builder.AppendLine("    Write-Host '2 validate structure'");
            builder.AppendLine("    Write-Host '3 build review manifest'");
            builder.AppendLine("    Write-Host '4 prepare existing manifest'");
            builder.AppendLine("    Write-Host '5 build submission package'");
            builder.AppendLine("    Write-Host '6 list graph opcodes'");
            builder.AppendLine("    Write-Host '7 list graph opcodes JSON'");
            builder.AppendLine("    Write-Host '8 create graph node snippet'");
            builder.AppendLine("    Write-Host '9 apply graph node snippet'");
            builder.AppendLine("    Write-Host '10 create setting row snippet'");
            builder.AppendLine("    Write-Host '11 create locale entry snippet'");
            builder.AppendLine("    Write-Host '12 apply setting row snippet'");
            builder.AppendLine("    Write-Host '13 apply locale entry snippet'");
            builder.AppendLine("    Write-Host '14 create asset entry snippet'");
            builder.AppendLine("    Write-Host '15 apply asset entry snippet'");
            builder.AppendLine("    Write-Host '16 configure manifest capability/budgets'");
            builder.AppendLine("    Write-Host '17 show capability matrix'");
            builder.AppendLine("    Write-Host '18 create first playable mod'");
            builder.AppendLine("    Write-Host '19 install local discovery copy'");
            builder.AppendLine("    Write-Host '20 diagnose local Mods folder'");
            builder.AppendLine("    Write-Host '21 configure dependencies'");
            builder.AppendLine("    Write-Host 'q quit'");
            builder.AppendLine("    Write-Host ''");
            builder.AppendLine("    $choice = Read-Host 'Select action'");
            builder.AppendLine();
            builder.AppendLine("    switch ($choice) {");
            builder.AppendLine("        '1' { Invoke-Setup $true }");
            builder.AppendLine("        '2' { Invoke-Validate }");
            builder.AppendLine("        '3' { Invoke-Review }");
            builder.AppendLine("        '4' { Invoke-PrepareExisting }");
            builder.AppendLine("        '5' { Invoke-SubmissionPackage }");
            builder.AppendLine("        '6' { Invoke-Opcodes $false }");
            builder.AppendLine("        '7' { Invoke-Opcodes $true }");
            builder.AppendLine("        '8' { Invoke-GraphNodeSnippet $true }");
            builder.AppendLine("        '9' { Invoke-ApplyGraphNodeSnippet $true }");
            builder.AppendLine("        '10' { Invoke-SettingsRowSnippet $true }");
            builder.AppendLine("        '11' { Invoke-LocaleEntrySnippet $true }");
            builder.AppendLine("        '12' { Invoke-ApplySettingsRowSnippet $true }");
            builder.AppendLine("        '13' { Invoke-ApplyLocaleEntrySnippet $true }");
            builder.AppendLine("        '14' { Invoke-AssetEntrySnippet $true }");
            builder.AppendLine("        '15' { Invoke-ApplyAssetEntrySnippet $true }");
            builder.AppendLine("        '16' { Invoke-ManifestContractConfig $true }");
            builder.AppendLine("        '17' { Invoke-Capabilities }");
            builder.AppendLine("        '18' { Invoke-FirstMod $true }");
            builder.AppendLine("        '19' { Invoke-InstallLocal $true }");
            builder.AppendLine("        '20' { Invoke-DiagnoseLocal $true }");
            builder.AppendLine("        '21' { Invoke-Dependencies $true }");
            builder.AppendLine("        'q' { return }");
            builder.AppendLine("        'Q' { return }");
            builder.AppendLine("        default { Fail ('Unknown menu action: ' + $choice) }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$Root = Split-Path -Parent $MyInvocation.MyCommand.Path");
            builder.AppendLine();
            builder.AppendLine("switch ($Action) {");
            builder.AppendLine("    'menu' { Show-Menu }");
            builder.AppendLine("    'first-mod' { Invoke-FirstMod $false }");
            builder.AppendLine("    'install-local' { Invoke-InstallLocal $false }");
            builder.AppendLine("    'diagnose-local' { Invoke-DiagnoseLocal $false }");
            builder.AppendLine("    'dependencies' { Invoke-Dependencies $false }");
            builder.AppendLine("    'setup' { Invoke-Setup $false }");
            builder.AppendLine("    'validate' { Invoke-Validate }");
            builder.AppendLine("    'review' { Invoke-Review }");
            builder.AppendLine("    'prepare' { Invoke-PrepareExisting }");
            builder.AppendLine("    'submission' { Invoke-SubmissionPackage }");
            builder.AppendLine("    'opcodes' { Invoke-Opcodes $false }");
            builder.AppendLine("    'opcodes-json' { Invoke-Opcodes $true }");
            builder.AppendLine("    'node-snippet' { Invoke-GraphNodeSnippet $false }");
            builder.AppendLine("    'apply-node-snippet' { Invoke-ApplyGraphNodeSnippet $false }");
            builder.AppendLine("    'setting-snippet' { Invoke-SettingsRowSnippet $false }");
            builder.AppendLine("    'locale-snippet' { Invoke-LocaleEntrySnippet $false }");
            builder.AppendLine("    'apply-setting-snippet' { Invoke-ApplySettingsRowSnippet $false }");
            builder.AppendLine("    'apply-locale-snippet' { Invoke-ApplyLocaleEntrySnippet $false }");
            builder.AppendLine("    'asset-snippet' { Invoke-AssetEntrySnippet $false }");
            builder.AppendLine("    'apply-asset-snippet' { Invoke-ApplyAssetEntrySnippet $false }");
            builder.AppendLine("    'manifest-contract' { Invoke-ManifestContractConfig $false }");
            builder.AppendLine("    'capabilities' { Invoke-Capabilities }");
            builder.AppendLine("    default { Fail ('Unsupported action: ' + $Action) }");
            builder.AppendLine("}");
            return builder.ToString();
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
                "  \"Dependencies\": []," + global::System.Environment.NewLine +
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
                "Declare assets in assets.h8manifest.json. Put referenced files under Content/Assets/ and use h8mod.ps1 -Action asset-snippet plus h8mod.ps1 -Action apply-asset-snippet instead of hand-editing JSON when possible." + global::System.Environment.NewLine +
                "Runtime loading is not granted by placing files here. The SDK/packer must CRC-approve assets and generate envelope references before gameplay can use them." + global::System.Environment.NewLine;
        }

        private static string BuildContentAssetsReadme()
        {
            return
                "# Content Assets" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Put files referenced by Content/assets.h8manifest.json here." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Supported starter declarations are bounded to:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "- data_blob: .json, .bytes, .bin" + global::System.Environment.NewLine +
                "- raw_texture: .png, .jpg, .jpeg, .webp" + global::System.Environment.NewLine +
                "- audio_clip: .wav, .ogg" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Declaring a file here does not grant runtime loading. The manifest is an authoring/review contract; runtime use still requires engine-owned approval and bake." + global::System.Environment.NewLine;
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
                "  \"hecton8.powerShellExecutable\": \"powershell\",",
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

        private static string BuildVsCodeTasks()
        {
            return BuildStarterKitToolFromTemplate(".vscode/tasks.json");
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
                "  \"required\": [\"Schema\", \"Id\", \"DisplayName\", \"Author\", \"Version\", \"RequiredAPIVersion\", \"Dependencies\", \"Capabilities\", \"Budgets\", \"Compatibility\", \"Entrypoints\"],",
                "  \"properties\": {",
                "    \"Schema\": { \"const\": \"hecton8.h8mod.authoring.v1\" },",
                "    \"Id\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "    \"DisplayName\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Author\": { \"type\": \"string\", \"minLength\": 1 },",
                "    \"Version\": { \"type\": \"string\", \"pattern\": \"^(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)\\\\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?(\\\\+[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$\" },",
                "    \"RequiredAPIVersion\": { \"type\": \"integer\", \"minimum\": " + CurrentRequiredApiVersion + " },",
                "    \"Dependencies\": { \"type\": \"array\", \"uniqueItems\": true, \"maxItems\": 32, \"items\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" } },",
                "    \"Capabilities\": {",
                "      \"type\": \"array\",",
                "      \"uniqueItems\": true,",
                "      \"maxItems\": 16,",
                "      \"items\": {",
                "        \"type\": \"string\",",
                "        \"enum\": [",
                "          \"cap.graph.command_draft\",",
                "          \"cap.settings.table\",",
                "          \"cap.locale.en\",",
                "          \"cap.content.asset_manifest\",",
                "          \"cap.review.submission_package\"",
                "        ]",
                "      }",
                "    },",
                "    \"Budgets\": {",
                "      \"type\": \"object\",",
                "      \"additionalProperties\": false,",
                "      \"required\": [\"MaxEnvelopesPerFrame\", \"MaxAssetBytes\"],",
                "      \"properties\": {",
                "        \"MaxEnvelopesPerFrame\": { \"type\": \"integer\", \"minimum\": 0, \"maximum\": 256 },",
                "        \"MaxAssetBytes\": { \"type\": \"integer\", \"minimum\": 0, \"maximum\": 33554432 }",
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
                "    \"Dependencies\": { \"type\": \"array\", \"uniqueItems\": true, \"maxItems\": 32, \"items\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" } },",
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
                "      \"maxItems\": 256,",
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
                "    \"Assets\": {",
                "      \"type\": \"array\",",
                "      \"maxItems\": 512,",
                "      \"items\": {",
                "        \"type\": \"object\",",
                "        \"additionalProperties\": false,",
                "        \"required\": [\"Id\", \"Kind\", \"Path\", \"Crc32\", \"Bytes\"],",
                "        \"properties\": {",
                "          \"Id\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "          \"Kind\": { \"type\": \"string\", \"enum\": [\"raw_texture\", \"audio_clip\", \"data_blob\"] },",
                "          \"Path\": { \"type\": \"string\", \"pattern\": \"^Content/Assets/[^.].*$\" },",
                "          \"Crc32\": { \"type\": \"string\", \"pattern\": \"^[0-9A-Fa-f]{8}$\" },",
                "          \"Bytes\": { \"type\": \"integer\", \"minimum\": 0, \"maximum\": 4194304 }",
                "        }",
                "      }",
                "    }",
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
                "    \"Rows\": {",
                "      \"type\": \"array\",",
                "      \"maxItems\": 128,",
                "      \"items\": {",
                "        \"type\": \"object\",",
                "        \"additionalProperties\": false,",
                "        \"required\": [\"Id\", \"Kind\", \"Default\"],",
                "        \"properties\": {",
                "          \"Id\": { \"type\": \"string\", \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "          \"Kind\": { \"type\": \"string\", \"enum\": [\"bool\", \"int\", \"float\", \"string\", \"enum\"] },",
                "          \"Default\": {},",
                "          \"Label\": { \"type\": \"string\", \"minLength\": 1 },",
                "          \"Description\": { \"type\": \"string\" },",
                "          \"Min\": { \"type\": \"number\" },",
                "          \"Max\": { \"type\": \"number\" },",
                "          \"Options\": {",
                "            \"type\": \"array\",",
                "            \"minItems\": 1,",
                "            \"maxItems\": 64,",
                "            \"uniqueItems\": true,",
                "            \"items\": { \"type\": \"string\", \"minLength\": 1 }",
                "          }",
                "        }",
                "      }",
                "    }",
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
                "    \"Locale\": { \"type\": \"string\", \"pattern\": \"^[a-z]{2}(-[A-Z]{2})?$\" },",
                "    \"Strings\": {",
                "      \"type\": \"object\",",
                "      \"maxProperties\": 512,",
                "      \"propertyNames\": { \"pattern\": \"^[a-z0-9]+([._-][a-z0-9]+)*$\" },",
                "      \"additionalProperties\": { \"type\": \"string\", \"minLength\": 1 }",
                "    }",
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
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action first-mod -Id com.yourname.firstmod -DisplayName \"First HECTON Mod\" -Author \"YourName\" -Version 0.1.0 -Replace" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Manual identity-only setup:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setup -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Normal edit-review loop:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Capability matrix:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action capabilities" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Manifest capability/budget setup:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action manifest-contract -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Submission handoff:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Use pwsh instead of powershell on macOS/Linux with PowerShell 7. The scripts normalize child paths internally; do not rewrite Tools/, Reports/, or .vscode/ paths per platform. In VS Code, change hecton8.powerShellExecutable in .vscode/settings.json to pwsh and run Tasks: Run Task for the same h8mod.ps1 actions." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "The root h8mod.ps1 launcher is the preferred no-Unity entry point for humans. VS Code tasks call that launcher directly, including first playable mod creation, disabled graph node creation, and explicit graph/settings/locale/asset replace applies. It delegates to these Tools/*.ps1 scripts, prints Docs/capabilities.md for capability discovery, and does not add a second validation contract." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run create_first_mod.ps1 only through h8mod.ps1 -Action first-mod unless automation needs the inner tool. It sets identity, enables cap.graph.command_draft, creates and applies one SpawnItem graph node, one boolean setting, and one locale entry, then validates and builds Reports/review_manifest.json. Use -Replace for a rerunnable onboarding pass over the same sample IDs. Use -BuildSubmission when the first pass should also write the submission zip." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "prepare_mod.ps1 runs identity setup only when -Id is provided. Without -Id it validates the existing manifests and rebuilds Reports/review_manifest.json for the normal edit-review loop." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run list_allowed_opcodes.ps1 when editing Graphs/main.h8graph.json. It prints every currently allowed graph opcode alias and hex token from Reference/allowed_opcodes.csv; use either value in Nodes[].Opcode." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run configure_manifest_contract.ps1 when you need to declare public authoring capabilities or set explicit starter budgets without hand-editing mod.h8manifest.json. It accepts only the public capability allowlist, caps MaxEnvelopesPerFrame at 256, caps MaxAssetBytes at 33554432, refuses to lower budgets below current graph or asset manifest requirements, writes through a temp file, restores the previous manifest if validation fails, and then runs validate_structure.ps1. Capabilities are review metadata, not runtime rights." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run create_graph_node_snippet.ps1 when you want a safe starter node object. It writes Generated/graph_node_snippet.json after validating the node id, opcode, optional ParametersJson object, and optional disabled state against Reference/allowed_opcodes.csv; it also accepts a flat CLI fallback like {Quantity:3,Item:demo} when a shell strips JSON quotes. It never rewrites Graphs/main.h8graph.json." + global::System.Environment.NewLine +
                "Run apply_graph_node_snippet.ps1 to insert Generated/graph_node_snippet.json into Graphs/main.h8graph.json with duplicate rejection, graph/manifest budget repair, validation, and rollback." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run create_settings_row_snippet.ps1 when you want a safe settings row object. It writes Generated/settings_row_snippet.json after validating the setting id, kind, and typed default value; it never rewrites Tables/settings.h8table.json." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run create_locale_entry_snippet.ps1 when you want a safe locale key/value object. It writes Generated/locale_entry_snippet.json after validating the key and localized value; it never rewrites Locales/en.h8loc.json." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run apply_settings_row_snippet.ps1 after generating a settings row snippet. It inserts the clean row into Tables/settings.h8table.json, strips snippet-only notes, rejects duplicate setting ids unless -Replace is explicit, writes through a temp file, restores the previous table if validation fails, and then runs validate_structure.ps1." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run apply_locale_entry_snippet.ps1 after generating a locale entry snippet. It inserts the key/value into Locales/en.h8loc.json, rejects duplicate locale keys unless -Replace is explicit, writes through a temp file, restores the previous locale file if validation fails, and then runs validate_structure.ps1." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run create_asset_entry_snippet.ps1 when you want a safe content asset manifest entry. Put the file under Content/Assets/, choose data_blob/raw_texture/audio_clip, and use -Crc32 auto -Bytes -1 to compute CRC32 and byte length from the file. It never rewrites Content/assets.h8manifest.json." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run apply_asset_entry_snippet.ps1 after generating an asset entry snippet. It verifies the referenced Content/Assets file, inserts the clean entry into Content/assets.h8manifest.json, rejects duplicate asset ids unless -Replace is explicit, raises mod.h8manifest.json Budgets.MaxAssetBytes when needed, restores previous files if validation fails, and then runs validate_structure.ps1." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run validate_structure.ps1 before sending this folder to another tool or author." + global::System.Environment.NewLine +
                "This local validator checks only starter-kit structure, canonical IDs, manifest parity, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, content asset manifest id/kind/path/byte/CRC/budget constraints, graph opcode allowlist, graph budget parity, exact editor schema mappings, and envelope-only safety. It is not runtime verification." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run build_review_manifest.ps1 before submitting a starter folder for review. It runs the structure validator first, then writes Reports/review_manifest.json with package identity, sorted file paths, byte counts, total bytes, explicit limits, and SHA-256 hashes. Generated/ and Reports/ are excluded from the hash list so reports do not hash themselves. The source side is bounded at 256 files, 4194304 bytes per file, and 33554432 total bytes; oversized source files fail before hashing." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run build_submission_package.ps1 when you need one artifact to hand off. It runs prepare, then writes Generated/<mod-id>_submission.zip with the reviewed starter sources plus Reports/review_manifest.json. It writes the replacement to a temp zip first and restores the previous submission zip if final replacement fails. This is a review/submission package only; it does not claim runtime loading." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Run set_mod_identity.ps1 once when you copy the starter kit. It writes the same canonical mod id, display name, author, and version into mod.h8manifest.json and mod.json, then runs the structure validator." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "Command:" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action first-mod -Id com.yourname.firstmod -DisplayName \"First HECTON Mod\" -Author \"YourName\" -Version 0.1.0 -Replace" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action validate" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action review" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action prepare" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action capabilities" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action manifest-contract -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action submission" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action opcodes-json" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action node-snippet -NodeId node.spawn_item -Opcode SpawnItem -NodeParametersJson '{}'" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action setting-snippet -SettingId setting.example_toggle -SettingKind bool -SettingDefault false" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action locale-snippet -LocaleKey text.example_line -LocaleValue \"Your localized text\"" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-setting-snippet" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-locale-snippet" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action asset-snippet -AssetId asset.example_blob -AssetKind data_blob -AssetPath Content/Assets/example.bytes -AssetCrc32 auto -AssetBytes -1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File h8mod.ps1 -Action apply-asset-snippet" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_structure.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/list_allowed_opcodes.ps1 -Json" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/configure_manifest_contract.ps1 -Capability cap.graph.command_draft -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_first_mod.ps1 -Id com.yourname.firstmod -DisplayName \"First HECTON Mod\" -Author \"YourName\" -Version 0.1.0 -Replace" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_graph_node_snippet.ps1 -Id node.spawn_item -Opcode SpawnItem -ParametersJson '{}'" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_settings_row_snippet.ps1 -Id setting.example_toggle -Kind bool -Default false" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_locale_entry_snippet.ps1 -Key text.example_line -Value \"Your localized text\"" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/create_asset_entry_snippet.ps1 -Id asset.example_blob -Kind data_blob -Path Content/Assets/example.bytes -Crc32 auto -Bytes -1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_settings_row_snippet.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_locale_entry_snippet.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply_asset_entry_snippet.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_review_manifest.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/build_submission_package.ps1" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName \"Your Mod\" -Author \"YourName\" -Version 0.1.0" + global::System.Environment.NewLine +
                "powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1" + global::System.Environment.NewLine;
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

        private static string BuildStarterKitGraphNodeSnippetScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/create_graph_node_snippet.ps1");
        }

        private static string BuildStarterKitGraphNodeApplyScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/apply_graph_node_snippet.ps1");
        }

        private static string BuildStarterKitAssetEntrySnippetScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/create_asset_entry_snippet.ps1");
        }

        private static string BuildStarterKitAssetEntryApplyScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/apply_asset_entry_snippet.ps1");
        }

        private static string BuildStarterKitSettingsRowSnippetScript()
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Id = 'setting.example_toggle',");
            builder.AppendLine("    [string]$Kind = 'bool',");
            builder.AppendLine("    [string]$Default = 'false',");
            builder.AppendLine("    [string]$Output = 'Generated/settings_row_snippet.json',");
            builder.AppendLine("    [switch]$Json");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_SETTINGS_SNIPPET] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath {");
            builder.AppendLine("    param(");
            builder.AppendLine("        [string]$BasePath,");
            builder.AppendLine("        [Parameter(ValueFromRemainingArguments = $true)]");
            builder.AppendLine("        [string[]]$Segments");
            builder.AppendLine("    )");
            builder.AppendLine();
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in $Segments) {");
            builder.AppendLine("        foreach ($part in ($segment.Replace('\\','/') -split '/')) {");
            builder.AppendLine("            if (-not [string]::IsNullOrWhiteSpace($part)) {");
            builder.AppendLine("                $current = Join-Path $current $part");
            builder.AppendLine("            }");
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
            builder.AppendLine("    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) { return $true }");
            builder.AppendLine("    return $false");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-CanonicalId([string]$Value, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    if ($trimmed.Length -gt 96) { Fail ($Label + ' must be 96 characters or shorter.') }");
            builder.AppendLine("    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {");
            builder.AppendLine("        Fail ($Label + \" may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.\")");
            builder.AppendLine("    }");
            builder.AppendLine("    foreach ($segment in ($trimmed -split '[._-]')) {");
            builder.AppendLine("        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-Kind([string]$Value) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Kind is required.' }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    if ($trimmed -ne $Value) { Fail 'Kind must not contain leading or trailing whitespace.' }");
            builder.AppendLine("    if (@('bool','int','float','string','enum') -notcontains $trimmed) { Fail 'Kind must be one of: bool, int, float, string, enum.' }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Convert-DefaultValue([string]$Value, [string]$KindValue) {");
            builder.AppendLine("    if ($null -eq $Value) { Fail 'Default is required.' }");
            builder.AppendLine("    $trimmed = $Value.Trim()");
            builder.AppendLine("    switch ($KindValue) {");
            builder.AppendLine("        'bool' {");
            builder.AppendLine("            if ($trimmed -ieq 'true') { return $true }");
            builder.AppendLine("            if ($trimmed -ieq 'false') { return $false }");
            builder.AppendLine("            Fail 'Default for bool settings must be true or false.'");
            builder.AppendLine("        }");
            builder.AppendLine("        'int' {");
            builder.AppendLine("            $parsed = [long]0");
            builder.AppendLine("            if (-not [long]::TryParse($trimmed, [ref]$parsed)) { Fail 'Default for int settings must be a JSON integer.' }");
            builder.AppendLine("            return $parsed");
            builder.AppendLine("        }");
            builder.AppendLine("        'float' {");
            builder.AppendLine("            $parsed = [double]0");
            builder.AppendLine("            if (-not [double]::TryParse($trimmed, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) { Fail 'Default for float settings must be a JSON number.' }");
            builder.AppendLine("            if ([double]::IsNaN($parsed) -or [double]::IsInfinity($parsed)) { Fail 'Default for float settings must be finite.' }");
            builder.AppendLine("            return $parsed");
            builder.AppendLine("        }");
            builder.AppendLine("        'string' {");
            builder.AppendLine("            if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Default for string settings must not be empty.' }");
            builder.AppendLine("            if ($trimmed -ne $Value) { Fail 'Default for string settings must not contain leading or trailing whitespace.' }");
            builder.AppendLine("            return $Value");
            builder.AppendLine("        }");
            builder.AppendLine("        'enum' {");
            builder.AppendLine("            if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Default for enum settings must not be empty.' }");
            builder.AppendLine("            if ($trimmed -ne $Value) { Fail 'Default for enum settings must not contain leading or trailing whitespace.' }");
            builder.AppendLine("            return $Value");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Resolve-GeneratedOutputPath([string]$RelativePath) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($RelativePath)) { Fail 'Output is required.' }");
            builder.AppendLine("    $normalized = $RelativePath.Replace('\\','/').Trim()");
            builder.AppendLine("    if ([System.IO.Path]::IsPathRooted($normalized)) { Fail 'Output must be a starter-relative path under Generated/.' }");
            builder.AppendLine("    if ($normalized.Contains('..') -or -not $normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) { Fail 'Output must stay under Generated/ and must not contain .. segments.' }");
            builder.AppendLine("    $directory = Join-StarterPath $Root 'Generated'");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { [void](New-Item -ItemType Directory -Path $directory -Force) }");
            builder.AppendLine("    $outputPath = Join-StarterPath $Root $normalized");
            builder.AppendLine("    $outputDirectory = Split-Path -Parent $outputPath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) { [void](New-Item -ItemType Directory -Path $outputDirectory -Force) }");
            builder.AppendLine("    return [pscustomobject][ordered]@{ Relative = $normalized; Full = $outputPath }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$Root = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$settingId = Validate-CanonicalId $Id 'Setting Id'");
            builder.AppendLine("$settingKind = Validate-Kind $Kind");
            builder.AppendLine("$defaultValue = Convert-DefaultValue $Default $settingKind");
            builder.AppendLine("$outputPath = Resolve-GeneratedOutputPath $Output");
            builder.AppendLine("$row = [pscustomobject][ordered]@{");
            builder.AppendLine("    Id = $settingId");
            builder.AppendLine("    Kind = $settingKind");
            builder.AppendLine("    Default = $defaultValue");
            builder.AppendLine("    Notes = 'Apply with h8mod.ps1 -Action apply-setting-snippet, or copy this object into Tables/settings.h8table.json Rows[] and run h8mod.ps1 -Action validate.'");
            builder.AppendLine("}");
            builder.AppendLine("$utf8NoBom = New-Object System.Text.UTF8Encoding $false");
            builder.AppendLine("$rowJson = ($row | ConvertTo-Json -Depth 8)");
            builder.AppendLine("[System.IO.File]::WriteAllText($outputPath.Full, ($rowJson + [System.Environment]::NewLine), $utf8NoBom)");
            builder.AppendLine("if ($Json) {");
            builder.AppendLine("    $payload = [pscustomobject][ordered]@{");
            builder.AppendLine("        Schema = 'hecton8.settings_row_snippet.v1'");
            builder.AppendLine("        Runtime = 'envelope-only'");
            builder.AppendLine("        Output = $outputPath.Relative");
            builder.AppendLine("        Row = $row");
            builder.AppendLine("    }");
            builder.AppendLine("    Write-Output ($payload | ConvertTo-Json -Depth 8)");
            builder.AppendLine("    exit 0");
            builder.AppendLine("}");
            builder.AppendLine("Write-Output 'PASS HECTON-8 settings row snippet written'");
            builder.AppendLine("Write-Output ('Output: ' + $outputPath.Relative)");
            builder.AppendLine("Write-Output ('Setting Id: ' + $settingId)");
            builder.AppendLine("Write-Output ('Kind: ' + $settingKind)");
            builder.AppendLine("Write-Output 'Next: h8mod.ps1 -Action apply-setting-snippet. Manual fallback: copy the JSON object into Tables/settings.h8table.json Rows[], then run h8mod.ps1 -Action validate.'");
            return builder.ToString();
        }

        private static string BuildStarterKitLocaleEntrySnippetScript()
        {
            StringBuilder builder = new StringBuilder(7168);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Key = 'text.example_line',");
            builder.AppendLine("    [string]$Value = 'Your localized text',");
            builder.AppendLine("    [string]$Output = 'Generated/locale_entry_snippet.json',");
            builder.AppendLine("    [switch]$Json");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_LOCALE_SNIPPET] ' + $Message)");
            builder.AppendLine("    exit 1");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Join-StarterPath {");
            builder.AppendLine("    param(");
            builder.AppendLine("        [string]$BasePath,");
            builder.AppendLine("        [Parameter(ValueFromRemainingArguments = $true)]");
            builder.AppendLine("        [string[]]$Segments");
            builder.AppendLine("    )");
            builder.AppendLine();
            builder.AppendLine("    $current = $BasePath");
            builder.AppendLine("    foreach ($segment in $Segments) {");
            builder.AppendLine("        foreach ($part in ($segment.Replace('\\','/') -split '/')) {");
            builder.AppendLine("            if (-not [string]::IsNullOrWhiteSpace($part)) {");
            builder.AppendLine("                $current = Join-Path $current $part");
            builder.AppendLine("            }");
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
            builder.AppendLine("    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) { return $true }");
            builder.AppendLine("    return $false");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-CanonicalId([string]$InputValue, [string]$Label) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($InputValue)) { Fail ($Label + ' is required.') }");
            builder.AppendLine("    $trimmed = $InputValue.Trim()");
            builder.AppendLine("    if ($trimmed -ne $InputValue) { Fail ($Label + ' must not contain leading or trailing whitespace.') }");
            builder.AppendLine("    if ($trimmed.Length -gt 96) { Fail ($Label + ' must be 96 characters or shorter.') }");
            builder.AppendLine("    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {");
            builder.AppendLine("        Fail ($Label + \" may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.\")");
            builder.AppendLine("    }");
            builder.AppendLine("    foreach ($segment in ($trimmed -split '[._-]')) {");
            builder.AppendLine("        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }");
            builder.AppendLine("    }");
            builder.AppendLine("    return $trimmed");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-LocaleValue([string]$InputValue) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($InputValue)) { Fail 'Locale value is required.' }");
            builder.AppendLine("    $trimmed = $InputValue.Trim()");
            builder.AppendLine("    if ($trimmed -ne $InputValue) { Fail 'Locale value must not contain leading or trailing whitespace.' }");
            builder.AppendLine("    if ($trimmed.Length -gt 2048) { Fail 'Locale value must be 2048 characters or shorter.' }");
            builder.AppendLine("    return $InputValue");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Resolve-GeneratedOutputPath([string]$RelativePath) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($RelativePath)) { Fail 'Output is required.' }");
            builder.AppendLine("    $normalized = $RelativePath.Replace('\\','/').Trim()");
            builder.AppendLine("    if ([System.IO.Path]::IsPathRooted($normalized)) { Fail 'Output must be a starter-relative path under Generated/.' }");
            builder.AppendLine("    if ($normalized.Contains('..') -or -not $normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) { Fail 'Output must stay under Generated/ and must not contain .. segments.' }");
            builder.AppendLine("    $directory = Join-StarterPath $Root 'Generated'");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { [void](New-Item -ItemType Directory -Path $directory -Force) }");
            builder.AppendLine("    $outputPath = Join-StarterPath $Root $normalized");
            builder.AppendLine("    $outputDirectory = Split-Path -Parent $outputPath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) { [void](New-Item -ItemType Directory -Path $outputDirectory -Force) }");
            builder.AppendLine("    return [pscustomobject][ordered]@{ Relative = $normalized; Full = $outputPath }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$Root = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$localeKey = Validate-CanonicalId $Key 'Locale key'");
            builder.AppendLine("$localeValue = Validate-LocaleValue $Value");
            builder.AppendLine("$outputPath = Resolve-GeneratedOutputPath $Output");
            builder.AppendLine("$entry = [pscustomobject][ordered]@{");
            builder.AppendLine("    Key = $localeKey");
            builder.AppendLine("    Value = $localeValue");
            builder.AppendLine("    Notes = 'Apply with h8mod.ps1 -Action apply-locale-snippet, or copy Key and Value into Locales/en.h8loc.json Strings and run h8mod.ps1 -Action validate.'");
            builder.AppendLine("}");
            builder.AppendLine("$utf8NoBom = New-Object System.Text.UTF8Encoding $false");
            builder.AppendLine("$entryJson = ($entry | ConvertTo-Json -Depth 8)");
            builder.AppendLine("[System.IO.File]::WriteAllText($outputPath.Full, ($entryJson + [System.Environment]::NewLine), $utf8NoBom)");
            builder.AppendLine("if ($Json) {");
            builder.AppendLine("    $payload = [pscustomobject][ordered]@{");
            builder.AppendLine("        Schema = 'hecton8.locale_entry_snippet.v1'");
            builder.AppendLine("        Runtime = 'envelope-only'");
            builder.AppendLine("        Output = $outputPath.Relative");
            builder.AppendLine("        Entry = $entry");
            builder.AppendLine("    }");
            builder.AppendLine("    Write-Output ($payload | ConvertTo-Json -Depth 8)");
            builder.AppendLine("    exit 0");
            builder.AppendLine("}");
            builder.AppendLine("Write-Output 'PASS HECTON-8 locale entry snippet written'");
            builder.AppendLine("Write-Output ('Output: ' + $outputPath.Relative)");
            builder.AppendLine("Write-Output ('Locale key: ' + $localeKey)");
            builder.AppendLine("Write-Output 'Next: h8mod.ps1 -Action apply-locale-snippet. Manual fallback: copy Key and Value into Locales/en.h8loc.json Strings, then run h8mod.ps1 -Action validate.'");
            return builder.ToString();
        }

        private static string BuildStarterKitSettingsRowApplyScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/apply_settings_row_snippet.ps1");
        }

        private static string BuildStarterKitLocaleEntryApplyScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/apply_locale_entry_snippet.ps1");
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
            builder.AppendLine("$authoring = Get-Content -Raw -LiteralPath (Join-StarterPath $rootFull 'mod.h8manifest.json') | ConvertFrom-Json");
            builder.AppendLine("$runtime = Get-Content -Raw -LiteralPath (Join-StarterPath $rootFull 'mod.json') | ConvertFrom-Json");
            builder.AppendLine("$manifest = [pscustomobject][ordered]@{");
            builder.AppendLine("    Schema = 'hecton8.external_review_manifest.v1'");
            builder.AppendLine("    Runtime = 'envelope-only'");
            builder.AppendLine("    RootId = [string]$runtime.Id");
            builder.AppendLine("    Identity = [pscustomobject][ordered]@{");
            builder.AppendLine("        Id = [string]$runtime.Id");
            builder.AppendLine("        DisplayName = [string]$authoring.DisplayName");
            builder.AppendLine("        Author = [string]$runtime.Author");
            builder.AppendLine("        Version = [string]$runtime.Version");
            builder.AppendLine("        RequiredAPIVersion = [int]$runtime.RequiredAPIVersion");
            builder.AppendLine("        ModPriority = [int]$runtime.ModPriority");
            builder.AppendLine("    }");
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

        private static string BuildStarterKitSubmissionPackageScript()
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [string]$Output = '',");
            builder.AppendLine("    [string]$ReviewOutput = 'Reports/review_manifest.json'");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    Write-Error ('[H8MOD_SUBMISSION_PACKAGE] ' + $Message)");
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
            builder.AppendLine("function Test-SafeRelativePath([string]$RelativePath, [string]$RequiredPrefix) {");
            builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }");
            builder.AppendLine("    if ([System.IO.Path]::IsPathRooted($RelativePath)) { return $false }");
            builder.AppendLine("    $normalized = $RelativePath.Replace('\\','/')");
            builder.AppendLine("    if ($normalized.StartsWith('../') -or $normalized.Contains('/../')) { return $false }");
            builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($RequiredPrefix) -and");
            builder.AppendLine("        -not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {");
            builder.AppendLine("        return $false");
            builder.AppendLine("    }");
            builder.AppendLine("    return $true");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Require-File([string]$RelativePath) {");
            builder.AppendLine("    if (-not (Test-SafeRelativePath $RelativePath '')) {");
            builder.AppendLine("        Fail ('Unsafe package source path: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $path = Join-StarterPath $rootFull $RelativePath");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {");
            builder.AppendLine("        Fail ('Missing package source file: ' + $RelativePath)");
            builder.AppendLine("    }");
            builder.AppendLine("    return $path");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Normalize-EntryName([string]$RelativePath) {");
            builder.AppendLine("    $normalized = $RelativePath.Replace('\\','/')");
            builder.AppendLine("    if ($normalized.StartsWith('/')) {");
            builder.AppendLine("        $normalized = $normalized.Substring(1)");
            builder.AppendLine("    }");
            builder.AppendLine("    return $normalized");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function New-TempArtifactPath([string]$LeafName) {");
            builder.AppendLine("    $safeLeaf = $LeafName -replace '[^a-zA-Z0-9._-]', '_'");
            builder.AppendLine("    return (Join-Path ([System.IO.Path]::GetTempPath()) ('hecton8-' + $safeLeaf + '-' + [System.Guid]::NewGuid().ToString('N')))");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Remove-TempFile([string]$Path) {");
            builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) {");
            builder.AppendLine("        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$rootFull = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$prepareTool = Join-StarterPath $rootFull 'Tools/prepare_mod.ps1'");
            builder.AppendLine("if (-not (Test-Path -LiteralPath $prepareTool -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Missing Tools/prepare_mod.ps1.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not (Test-SafeRelativePath $ReviewOutput 'Reports/')) {");
            builder.AppendLine("    Fail 'ReviewOutput path must stay under Reports/.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("& $prepareTool -Root $rootFull -ReviewOutput $ReviewOutput | Out-Host");
            builder.AppendLine();
            builder.AppendLine("$reviewPath = Require-File $ReviewOutput");
            builder.AppendLine("$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json");
            builder.AppendLine("if ([string]$review.Schema -ne 'hecton8.external_review_manifest.v1') {");
            builder.AppendLine("    Fail 'Review manifest schema mismatch.'");
            builder.AppendLine("}");
            builder.AppendLine("if ([string]$review.Runtime -ne 'envelope-only') {");
            builder.AppendLine("    Fail 'Review manifest runtime must be envelope-only.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$packageId = [string]$review.Identity.Id");
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($packageId)) {");
            builder.AppendLine("    $packageId = [string]$review.RootId");
            builder.AppendLine("}");
            builder.AppendLine("if ($packageId -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {");
            builder.AppendLine("    Fail 'Review manifest package id is missing or non-canonical.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($Output)) {");
            builder.AppendLine("    $Output = 'Generated/' + $packageId + '_submission.zip'");
            builder.AppendLine("}");
            builder.AppendLine("if (-not (Test-SafeRelativePath $Output 'Generated/')) {");
            builder.AppendLine("    Fail 'Output path must stay under Generated/.'");
            builder.AppendLine("}");
            builder.AppendLine("if (-not $Output.Replace('\\','/').EndsWith('.zip', [System.StringComparison]::OrdinalIgnoreCase)) {");
            builder.AppendLine("    Fail 'Output path must end with .zip.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$outputPath = Join-StarterPath $rootFull $Output");
            builder.AppendLine("$outputDirectory = Split-Path -Parent $outputPath");
            builder.AppendLine("if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {");
            builder.AppendLine("    [void](New-Item -ItemType Directory -Path $outputDirectory)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$sourceEntries = New-Object 'System.Collections.Generic.List[string]'");
            builder.AppendLine("$seenEntries = @{}");
            builder.AppendLine("foreach ($file in @($review.Files)) {");
            builder.AppendLine("    $relative = [string]$file.Path");
            builder.AppendLine("    if (-not (Test-SafeRelativePath $relative '')) {");
            builder.AppendLine("        Fail ('Unsafe review file path: ' + $relative)");
            builder.AppendLine("    }");
            builder.AppendLine("    if ($relative.StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)) {");
            builder.AppendLine("        Fail ('Review manifest must not package Generated output: ' + $relative)");
            builder.AppendLine("    }");
            builder.AppendLine("    if ($relative.StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase)) {");
            builder.AppendLine("        Fail ('Review manifest must not package Reports output: ' + $relative)");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $entry = Normalize-EntryName $relative");
            builder.AppendLine("    if (-not $seenEntries.ContainsKey($entry)) {");
            builder.AppendLine("        $seenEntries[$entry] = $true");
            builder.AppendLine("        [void]$sourceEntries.Add($relative)");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$reviewEntry = Normalize-EntryName $ReviewOutput");
            builder.AppendLine("if (-not $seenEntries.ContainsKey($reviewEntry)) {");
            builder.AppendLine("    $seenEntries[$reviewEntry] = $true");
            builder.AppendLine("    [void]$sourceEntries.Add($ReviewOutput)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("try {");
            builder.AppendLine("    Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop");
            builder.AppendLine("    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop");
            builder.AppendLine("} catch {");
            builder.AppendLine("    Fail ('System.IO.Compression assemblies unavailable: ' + $_.Exception.Message)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$outputLeafName = [System.IO.Path]::GetFileName($outputPath)");
            builder.AppendLine("$tempOutputPath = (New-TempArtifactPath ($outputLeafName + '.tmp.zip'))");
            builder.AppendLine("$backupOutputPath = (New-TempArtifactPath ($outputLeafName + '.previous.zip'))");
            builder.AppendLine();
            builder.AppendLine("$zip = $null");
            builder.AppendLine("try {");
            builder.AppendLine("    $zip = [System.IO.Compression.ZipFile]::Open($tempOutputPath, [System.IO.Compression.ZipArchiveMode]::Create)");
            builder.AppendLine("    foreach ($relative in $sourceEntries) {");
            builder.AppendLine("        $sourcePath = Require-File $relative");
            builder.AppendLine("        $entryName = Normalize-EntryName $relative");
            builder.AppendLine("        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(");
            builder.AppendLine("            $zip,");
            builder.AppendLine("            $sourcePath,");
            builder.AppendLine("            $entryName,");
            builder.AppendLine("            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null");
            builder.AppendLine("    }");
            builder.AppendLine("} catch {");
            builder.AppendLine("    if ($null -ne $zip) {");
            builder.AppendLine("        $zip.Dispose()");
            builder.AppendLine("        $zip = $null");
            builder.AppendLine("    }");
            builder.AppendLine("    Remove-TempFile $tempOutputPath");
            builder.AppendLine("    Fail ('Submission package zip write failed: ' + $_.Exception.Message)");
            builder.AppendLine("} finally {");
            builder.AppendLine("    if ($null -ne $zip) {");
            builder.AppendLine("        $zip.Dispose()");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$hadPreviousOutput = Test-Path -LiteralPath $outputPath -PathType Leaf");
            builder.AppendLine("$previousCopiedToBackup = $false");
            builder.AppendLine("try {");
            builder.AppendLine("    if ($hadPreviousOutput) {");
            builder.AppendLine("        Copy-Item -LiteralPath $outputPath -Destination $backupOutputPath -Force");
            builder.AppendLine("        $previousCopiedToBackup = $true");
            builder.AppendLine("    }");
            builder.AppendLine("    Copy-Item -LiteralPath $tempOutputPath -Destination $outputPath -Force");
            builder.AppendLine("    Remove-TempFile $backupOutputPath");
            builder.AppendLine("    Remove-TempFile $tempOutputPath");
            builder.AppendLine("} catch {");
            builder.AppendLine("    if ($previousCopiedToBackup -and (Test-Path -LiteralPath $backupOutputPath -PathType Leaf)) {");
            builder.AppendLine("        Copy-Item -LiteralPath $backupOutputPath -Destination $outputPath -Force -ErrorAction SilentlyContinue");
            builder.AppendLine("    }");
            builder.AppendLine("    if ((-not $hadPreviousOutput) -and (Test-Path -LiteralPath $outputPath -PathType Leaf)) {");
            builder.AppendLine("        Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue");
            builder.AppendLine("    }");
            builder.AppendLine("    Remove-TempFile $backupOutputPath");
            builder.AppendLine("    Remove-TempFile $tempOutputPath");
            builder.AppendLine("    Fail ('Submission package zip replace failed: ' + $_.Exception.Message)");
            builder.AppendLine("}");
            builder.AppendLine("Write-Host ('PASS HECTON-8 submission package: ' + $Output)");
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
            builder.AppendLine("$rootFull = (Resolve-Path -LiteralPath $Root).Path");
            builder.AppendLine("$identityTool = Join-StarterPath $rootFull 'Tools/set_mod_identity.ps1'");
            builder.AppendLine("$reviewTool = Join-StarterPath $rootFull 'Tools/build_review_manifest.ps1'");
            builder.AppendLine("$hasIdentityEdits = -not [string]::IsNullOrWhiteSpace($Id)");
            builder.AppendLine();
            builder.AppendLine("if ((-not $hasIdentityEdits) -and");
            builder.AppendLine("    ((-not [string]::IsNullOrWhiteSpace($DisplayName)) -or");
            builder.AppendLine("     (-not [string]::IsNullOrWhiteSpace($Author)) -or");
            builder.AppendLine("     (-not [string]::IsNullOrWhiteSpace($Version)))) {");
            builder.AppendLine("    Fail 'Id is required when changing identity fields. Omit all identity arguments to validate the existing manifests.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not (Test-Path -LiteralPath $reviewTool -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Missing Tools/build_review_manifest.ps1.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if ($hasIdentityEdits) {");
            builder.AppendLine("    if (-not (Test-Path -LiteralPath $identityTool -PathType Leaf)) {");
            builder.AppendLine("        Fail 'Missing Tools/set_mod_identity.ps1.'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    & $identityTool -Root $rootFull -Id $Id -DisplayName $DisplayName -Author $Author -Version $Version | Out-Host");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("& $reviewTool -Root $rootFull -Output $ReviewOutput | Out-Host");
            builder.AppendLine();
            builder.AppendLine("$reviewOutputPath = if ([System.IO.Path]::IsPathRooted($ReviewOutput)) {");
            builder.AppendLine("    $ReviewOutput");
            builder.AppendLine("} else {");
            builder.AppendLine("    Join-StarterPath $rootFull $ReviewOutput");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("if (-not (Test-Path -LiteralPath $reviewOutputPath -PathType Leaf)) {");
            builder.AppendLine("    Fail 'Review manifest was not written.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("$review = Get-Content -Raw -LiteralPath $reviewOutputPath | ConvertFrom-Json");
            builder.AppendLine("$preparedId = [string]$review.Identity.Id");
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($preparedId)) {");
            builder.AppendLine("    $preparedId = [string]$review.RootId");
            builder.AppendLine("}");
            builder.AppendLine("if ([string]::IsNullOrWhiteSpace($preparedId)) {");
            builder.AppendLine("    Fail 'Review manifest did not report package identity.'");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Write-Host ('PASS HECTON-8 starter prepared: ' + $preparedId)");
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

        private static string BuildStarterKitManifestContractScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/configure_manifest_contract.ps1");
        }

        private static string BuildStarterKitDependenciesScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/configure_dependencies.ps1");
        }

        private static string BuildStarterKitFirstModScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/create_first_mod.ps1");
        }

        private static string BuildStarterKitInstallLocalModScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/install_local_mod.ps1");
        }

        private static string BuildStarterKitDiagnoseLocalModsScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/diagnose_local_mods.ps1");
        }

        private static string BuildStarterKitValidatorScript()
        {
            string validatorTemplate = BuildStarterKitToolFromTemplate("Tools/validate_structure.ps1");
            if (!validatorTemplate.Contains("Missing checked-in starter tool template"))
                return validatorTemplate;

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("param(");
            builder.AppendLine("    [string]$Root = (Split-Path -Parent $PSScriptRoot),");
            builder.AppendLine("    [switch]$ThrowInsteadOfExit");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine();
            builder.AppendLine("function Fail([string]$Message) {");
            builder.AppendLine("    if ($ThrowInsteadOfExit) {");
            builder.AppendLine("        throw ('[H8MOD_STARTER_VALIDATION] ' + $Message)");
            builder.AppendLine("    }");
            builder.AppendLine();
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
            builder.AppendLine("function Get-AllowedManifestCapabilities() {");
            builder.AppendLine("    return @('cap.graph.command_draft','cap.settings.table','cap.locale.en','cap.content.asset_manifest','cap.review.submission_package')");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-ManifestCapabilities([object]$Value) {");
            builder.AppendLine("    [void](Validate-JsonArray $Value 'mod.h8manifest.json Capabilities')");
            builder.AppendLine("    $capabilities = @($Value)");
            builder.AppendLine("    if ($capabilities.Count -gt 16) { Fail 'mod.h8manifest.json Capabilities exceeds 16 entries.' }");
            builder.AppendLine("    $allowed = Get-AllowedManifestCapabilities");
            builder.AppendLine("    $seen = @{}");
            builder.AppendLine("    for ($i = 0; $i -lt $capabilities.Count; $i++) {");
            builder.AppendLine("        $capabilityId = Validate-ModId ([string]$capabilities[$i]) ('mod.h8manifest.json Capabilities[' + $i + ']')");
            builder.AppendLine("        if ($seen.ContainsKey($capabilityId)) { Fail ('mod.h8manifest.json duplicate Capability: ' + $capabilityId) }");
            builder.AppendLine("        if ($allowed -notcontains $capabilityId) { Fail ('mod.h8manifest.json Capability is not public: ' + $capabilityId) }");
            builder.AppendLine("        $seen[$capabilityId] = $true");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-DependencyList([object]$Value, [string]$OwnerId, [string]$Label) {");
            builder.AppendLine("    [void](Validate-JsonArray $Value $Label)");
            builder.AppendLine("    $dependencies = @($Value)");
            builder.AppendLine("    if ($dependencies.Count -gt 32) { Fail ($Label + ' exceeds 32 entries.') }");
            builder.AppendLine("    $seen = @{}");
            builder.AppendLine("    $result = @()");
            builder.AppendLine("    for ($i = 0; $i -lt $dependencies.Count; $i++) {");
            builder.AppendLine("        $dependencyId = Validate-ModId ([string]$dependencies[$i]) ($Label + '[' + $i + ']')");
            builder.AppendLine("        if ($dependencyId -eq $OwnerId) { Fail ($Label + ' must not contain self dependency: ' + $dependencyId) }");
            builder.AppendLine("        if ($seen.ContainsKey($dependencyId)) { Fail ($Label + ' contains duplicate dependency: ' + $dependencyId) }");
            builder.AppendLine("        $seen[$dependencyId] = $true");
            builder.AppendLine("        $result += $dependencyId");
            builder.AppendLine("    }");
            builder.AppendLine("    return @($result)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-JsonArray([object]$Value, [string]$Label) {");
            builder.AppendLine("    if ($null -eq $Value -or -not $Value.GetType().IsArray) {");
            builder.AppendLine("        Fail ($Label + ' must be a JSON array.')");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return @($Value)");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-SettingDefault([object]$Value, [string]$Kind, [string]$Label) {");
            builder.AppendLine("    switch ($Kind) {");
            builder.AppendLine("        'bool' {");
            builder.AppendLine("            if ($Value -isnot [bool]) { Fail ($Label + ' Default must be a JSON boolean.') }");
            builder.AppendLine("            return");
            builder.AppendLine("        }");
            builder.AppendLine("        'int' {");
            builder.AppendLine("            if (-not (($Value -is [int]) -or ($Value -is [long]))) { Fail ($Label + ' Default must be a JSON integer.') }");
            builder.AppendLine("            return");
            builder.AppendLine("        }");
            builder.AppendLine("        'float' {");
            builder.AppendLine("            if (-not (($Value -is [double]) -or ($Value -is [decimal]) -or ($Value -is [single]) -or ($Value -is [int]) -or ($Value -is [long]))) {");
            builder.AppendLine("                Fail ($Label + ' Default must be a JSON number.')");
            builder.AppendLine("            }");
            builder.AppendLine("            return");
            builder.AppendLine("        }");
            builder.AppendLine("        'string' {");
            builder.AppendLine("            [void](Validate-RequiredText ([string]$Value) ($Label + ' Default'))");
            builder.AppendLine("            return");
            builder.AppendLine("        }");
            builder.AppendLine("        'enum' {");
            builder.AppendLine("            [void](Validate-RequiredText ([string]$Value) ($Label + ' Default'))");
            builder.AppendLine("            return");
            builder.AppendLine("        }");
            builder.AppendLine("        default {");
            builder.AppendLine("            Fail ($Label + ' Kind must be one of: bool, int, float, string, enum.')");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-SettingsTable([object]$Settings) {");
            builder.AppendLine("    if ([string]$Settings.Schema -ne 'hecton8.settings_table.draft.v1') {");
            builder.AppendLine("        Fail 'Tables/settings.h8table.json Schema must be hecton8.settings_table.draft.v1.'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    [void](Validate-JsonArray $Settings.Rows 'Tables/settings.h8table.json Rows')");
            builder.AppendLine("    $rows = @($Settings.Rows)");
            builder.AppendLine("    if ($rows.Count -gt 128) { Fail 'Tables/settings.h8table.json Rows exceeds 128 entries.' }");
            builder.AppendLine("    $rowIds = @{}");
            builder.AppendLine("    for ($i = 0; $i -lt $rows.Count; $i++) {");
            builder.AppendLine("        $row = $rows[$i]");
            builder.AppendLine("        if ($null -eq $row) { Fail ('Tables/settings.h8table.json Rows[' + $i + '] must not be null.') }");
            builder.AppendLine("        $label = 'Tables/settings.h8table.json Rows[' + $i + ']'");
            builder.AppendLine("        $rowId = Validate-ModId ([string]$row.Id) ($label + ' Id')");
            builder.AppendLine("        if ($rowIds.ContainsKey($rowId)) { Fail ('Tables/settings.h8table.json duplicate row Id: ' + $rowId) }");
            builder.AppendLine("        $rowIds[$rowId] = $true");
            builder.AppendLine();
            builder.AppendLine("        $kind = Validate-RequiredText ([string]$row.Kind) ($label + ' Kind')");
            builder.AppendLine("        if (@('bool','int','float','string','enum') -notcontains $kind) {");
            builder.AppendLine("            Fail ($label + ' Kind must be one of: bool, int, float, string, enum.')");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        $defaultProperty = $row.PSObject.Properties['Default']");
            builder.AppendLine("        if ($null -eq $defaultProperty) { Fail ($label + ' Default is required.') }");
            builder.AppendLine("        Validate-SettingDefault $defaultProperty.Value $kind $label");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("function Validate-LocaleTable([object]$LocaleDocument) {");
            builder.AppendLine("    if ([string]$LocaleDocument.Schema -ne 'hecton8.locale.draft.v1') {");
            builder.AppendLine("        Fail 'Locales/en.h8loc.json Schema must be hecton8.locale.draft.v1.'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $localeId = Validate-RequiredText ([string]$LocaleDocument.Locale) 'Locales/en.h8loc.json Locale'");
            builder.AppendLine("    if ($localeId -notmatch '^[a-z]{2}(-[A-Z]{2})?$') {");
            builder.AppendLine("        Fail 'Locales/en.h8loc.json Locale must use xx or xx-YY form.'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $stringsProperty = $LocaleDocument.PSObject.Properties['Strings']");
            builder.AppendLine("    if ($null -eq $stringsProperty -or $null -eq $stringsProperty.Value -or $stringsProperty.Value.GetType().IsArray) {");
            builder.AppendLine("        Fail 'Locales/en.h8loc.json Strings must be a JSON object.'");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    $stringEntries = @($stringsProperty.Value.PSObject.Properties)");
            builder.AppendLine("    if ($stringEntries.Count -gt 512) { Fail 'Locales/en.h8loc.json Strings exceeds 512 entries.' }");
            builder.AppendLine("    foreach ($entry in $stringEntries) {");
            builder.AppendLine("        [void](Validate-ModId ([string]$entry.Name) 'Locales/en.h8loc.json Strings key')");
            builder.AppendLine("        [void](Validate-RequiredText ([string]$entry.Value) ('Locales/en.h8loc.json Strings.' + [string]$entry.Name))");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("@('Content','Docs','Graphs','Tables','Locales','Generated','Reports','Reference','Schemas','Tools','.vscode') | ForEach-Object { Require-Directory $_ }");
            builder.AppendLine("@(");
            builder.AppendLine("    'README.md',");
            builder.AppendLine("    'Docs/capabilities.md',");
            builder.AppendLine("    'h8mod.ps1',");
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
            builder.AppendLine("    'Tools/build_submission_package.ps1',");
            builder.AppendLine("    'Tools/configure_dependencies.ps1',");
            builder.AppendLine("    'Tools/configure_manifest_contract.ps1',");
            builder.AppendLine("    'Tools/create_first_mod.ps1',");
            builder.AppendLine("    'Tools/install_local_mod.ps1',");
            builder.AppendLine("    'Tools/diagnose_local_mods.ps1',");
            builder.AppendLine("    'Tools/apply_graph_node_snippet.ps1',");
            builder.AppendLine("    'Tools/apply_locale_entry_snippet.ps1',");
            builder.AppendLine("    'Tools/apply_settings_row_snippet.ps1',");
            builder.AppendLine("    'Tools/create_locale_entry_snippet.ps1',");
            builder.AppendLine("    'Tools/create_graph_node_snippet.ps1',");
            builder.AppendLine("    'Tools/create_settings_row_snippet.ps1',");
            builder.AppendLine("    'Tools/list_allowed_opcodes.ps1',");
            builder.AppendLine("    'Tools/prepare_mod.ps1',");
            builder.AppendLine("    'Tools/set_mod_identity.ps1',");
            builder.AppendLine("    'Tools/validate_structure.ps1',");
            builder.AppendLine("    '.vscode/settings.json'");
            builder.AppendLine(") | ForEach-Object { [void](Require-File $_) }");
            builder.AppendLine();
            builder.AppendLine("$capabilitiesText = Get-Content -Raw -LiteralPath (Require-File 'Docs/capabilities.md')");
            builder.AppendLine("foreach ($requiredCapabilityText in @('Supported now','Not public rights','envelope-only','FutureCommandEnvelope','Harmony','BepInEx','h8mod.ps1 -Action capabilities','h8mod.ps1 -Action manifest-contract','configure_manifest_contract.ps1','h8mod.ps1 -Action dependencies','configure_dependencies.ps1','h8mod.ps1 -Action install-local','install_local_mod.ps1','h8mod.ps1 -Action diagnose-local','diagnose_local_mods.ps1','recursive manifest discovery','dependency cycles','load order','h8mod.ps1 -Action node-snippet','h8mod.ps1 -Action apply-node-snippet','h8mod.ps1 -Action setting-snippet','h8mod.ps1 -Action locale-snippet','h8mod.ps1 -Action apply-setting-snippet','h8mod.ps1 -Action apply-locale-snippet','h8mod.ps1 -Action asset-snippet','h8mod.ps1 -Action apply-asset-snippet')) {");
            builder.AppendLine("    if (-not $capabilitiesText.Contains($requiredCapabilityText)) {");
            builder.AppendLine("        Fail ('Docs/capabilities.md missing required capability text: ' + $requiredCapabilityText)");
            builder.AppendLine("    }");
            builder.AppendLine("}");
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
            builder.AppendLine("$authoringDependencies = @(Validate-DependencyList $authoring.Dependencies $authoringId 'mod.h8manifest.json Dependencies')");
            builder.AppendLine("Validate-ManifestCapabilities $authoring.Capabilities");
            builder.AppendLine("if ([int]$authoring.Budgets.MaxEnvelopesPerFrame -lt 0) { Fail 'mod.h8manifest.json Budgets.MaxEnvelopesPerFrame must be >= 0.' }");
            builder.AppendLine("if ([int]$authoring.Budgets.MaxEnvelopesPerFrame -gt 256) { Fail 'mod.h8manifest.json Budgets.MaxEnvelopesPerFrame exceeds 256.' }");
            builder.AppendLine("if ([long]$authoring.Budgets.MaxAssetBytes -lt 0) { Fail 'mod.h8manifest.json Budgets.MaxAssetBytes must be >= 0.' }");
            builder.AppendLine("if ([long]$authoring.Budgets.MaxAssetBytes -gt 33554432) { Fail 'mod.h8manifest.json Budgets.MaxAssetBytes exceeds 33554432.' }");
            builder.AppendLine("$runtimeId = Validate-ModId ([string]$runtime.Id) 'mod.json Id'");
            builder.AppendLine("if ($authoringId -ne $runtimeId) { Fail 'mod.h8manifest.json Id must match mod.json Id.' }");
            builder.AppendLine("$runtimeName = Validate-RequiredText ([string]$runtime.Name) 'mod.json Name'");
            builder.AppendLine("$runtimeAuthor = Validate-RequiredText ([string]$runtime.Author) 'mod.json Author'");
            builder.AppendLine("$runtimeVersion = Validate-Version ([string]$runtime.Version) 'mod.json Version'");
            builder.AppendLine("if ($authoringDisplayName -ne $runtimeName) { Fail 'mod.h8manifest.json DisplayName must match mod.json Name.' }");
            builder.AppendLine("if ($authoringAuthor -ne $runtimeAuthor) { Fail 'mod.h8manifest.json Author must match mod.json Author.' }");
            builder.AppendLine("if ($authoringVersion -ne $runtimeVersion) { Fail 'mod.h8manifest.json Version must match mod.json Version.' }");
            builder.AppendLine("if ($null -eq $runtime.PSObject.Properties['Dependencies']) { Fail 'mod.json Dependencies is required.' }");
            builder.AppendLine("$runtimeDependencies = @(Validate-DependencyList $runtime.Dependencies $runtimeId 'mod.json Dependencies')");
            builder.AppendLine("if (($authoringDependencies -join \"`n\") -ne ($runtimeDependencies -join \"`n\")) {");
            builder.AppendLine("    Fail 'mod.h8manifest.json Dependencies must match mod.json Dependencies in the same order.'");
            builder.AppendLine("}");
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryAssembly)) { Fail 'mod.json EntryAssembly must stay empty in envelope-only starter kits.' }");
            builder.AppendLine("if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryType)) { Fail 'mod.json EntryType must stay empty in envelope-only starter kits.' }");
            builder.AppendLine("if ([int]$runtime.RequiredAPIVersion -lt 2) { Fail 'mod.json RequiredAPIVersion must be >= 2.' }");
            builder.AppendLine("if ([string]$graph.Runtime -ne 'envelope-only') { Fail 'Graphs/main.h8graph.json Runtime must be envelope-only.' }");
            builder.AppendLine("if ([int]$graph.MaxEnvelopesPerFrame -gt [int]$authoring.Budgets.MaxEnvelopesPerFrame) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must not exceed mod.h8manifest.json Budgets.MaxEnvelopesPerFrame.' }");
            builder.AppendLine("$allowedGraphOpcodes = Read-AllowedGraphOpcodeTokens");
            builder.AppendLine("$graphNodeIds = @{}");
            builder.AppendLine("$graphOpcodeNodeCount = 0");
            builder.AppendLine("$graphNodes = @($graph.Nodes)");
            builder.AppendLine("if ($graphNodes.Count -gt 256) { Fail 'Graphs/main.h8graph.json Nodes exceeds 256 entries.' }");
            builder.AppendLine("foreach ($node in $graphNodes) {");
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
            builder.AppendLine("Validate-SettingsTable $settings");
            builder.AppendLine("Validate-LocaleTable $locale");
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
