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
        [MenuItem("Hecton8/Modding/SDK Hub")]
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
            DiagnosticsProcess process = _runningValidatorProcess;
            if (process == null)
                return;

            KillValidatorProcessNoThrow(process);
            DisposeRunningValidator();
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
                    WorkingDirectory = GetProjectRootPath(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);

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
                KillValidatorProcessNoThrow(_runningValidatorProcess);
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
            DiagnosticsProcess process = _runningValidatorProcess;
            _runningValidatorProcess = null;
            if (process != null)
            {
                DisposeValidatorProcessNoThrow(process);
            }

            _runningValidatorStdout = null;
            _runningValidatorStderr = null;
            _runningValidatorCompleted = false;
            _runningValidatorExitCode = -1;
        }

        private static void KillValidatorProcessNoThrow(DiagnosticsProcess process)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ModdingSdkHubWindow] Validator cleanup failed: " + exception.Message);
            }
        }

        private static void DisposeValidatorProcessNoThrow(DiagnosticsProcess process)
        {
            try
            {
                process.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ModdingSdkHubWindow] Validator dispose failed: " + exception.Message);
            }
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
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "h8mod.ps1"), BuildStarterKitToolFromTemplate("h8mod.ps1"));
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
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "strict_json_io.ps1"), BuildStarterKitToolFromTemplate("Tools/strict_json_io.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_asset_entry_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/apply_asset_entry_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "build_review_manifest.ps1"), BuildStarterKitToolFromTemplate("Tools/build_review_manifest.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "build_submission_package.ps1"), BuildStarterKitToolFromTemplate("Tools/build_submission_package.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "configure_dependencies.ps1"), BuildStarterKitToolFromTemplate("Tools/configure_dependencies.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "configure_manifest_contract.ps1"), BuildStarterKitToolFromTemplate("Tools/configure_manifest_contract.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_first_mod.ps1"), BuildStarterKitToolFromTemplate("Tools/create_first_mod.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "install_local_mod.ps1"), BuildStarterKitToolFromTemplate("Tools/install_local_mod.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "diagnose_local_mods.ps1"), BuildStarterKitToolFromTemplate("Tools/diagnose_local_mods.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "run_doctor.ps1"), BuildStarterKitToolFromTemplate("Tools/run_doctor.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_graph_node_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/apply_graph_node_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_locale_entry_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/apply_locale_entry_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "apply_settings_row_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/apply_settings_row_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_asset_entry_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/create_asset_entry_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_locale_entry_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/create_locale_entry_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_graph_node_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/create_graph_node_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "create_settings_row_snippet.ps1"), BuildStarterKitToolFromTemplate("Tools/create_settings_row_snippet.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "list_allowed_opcodes.ps1"), BuildStarterKitToolFromTemplate("Tools/list_allowed_opcodes.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "prepare_mod.ps1"), BuildStarterKitToolFromTemplate("Tools/prepare_mod.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "set_mod_identity.ps1"), BuildStarterKitToolFromTemplate("Tools/set_mod_identity.ps1"));
                createdCount += WriteTextFileIfMissing(Path.Combine(rootPath, "Tools", "validate_structure.ps1"), BuildStarterKitToolFromTemplate("Tools/validate_structure.ps1"));
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

        private static string BuildStarterKitStrictJsonIoScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/strict_json_io.ps1");
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
                "- Tools/run_doctor.ps1: local no-Unity read-only package readiness doctor. It validates the starter structure, compares current source files against Reports/review_manifest.json hashes, checks submission zip freshness, verifies zip entry hashes against review data, prints counts and next actions, and never mutates the package." + global::System.Environment.NewLine +
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
                "Open Hecton/Modding/External Starter Kit Workbench. Use Starter Health, Capability Matrix, Graph Contract Preview, Authoring Data Preview, Manifest Contract, Dependency Contract, Authoring Snippets, Content Asset Snippet, Graph Node Snippet, Validation And Review, and Submission Package panels. The Workbench can configure manifest capabilities/budgets/dependencies, show submission zip integrity against Reports/review_manifest.json, and generate/apply graph/settings/locale/content asset snippets through the same bounded starter tools, including Graph Opcode Picker, Parameters JSON, disabled-node, asset kind picker, CRC/byte fields, and replace-on-apply controls; it does not grant extra runtime rights." + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "## Expansion route" + global::System.Environment.NewLine +
                global::System.Environment.NewLine +
                "New mod powers must be added as engine-owned capability contracts: schema entry, static validator rule, Workbench visibility, starter docs, review-package proof, runtime owner, bounded budget, and runtime telemetry. Mods request; the engine validates and executes." + global::System.Environment.NewLine;
        }

        private static string BuildStarterKitLauncherScript()
        {
            return BuildStarterKitToolFromTemplate("h8mod.ps1");
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
                "Run build_submission_package.ps1 when you need one artifact to hand off. It runs prepare, then writes Generated/<mod-id>_submission.zip with the reviewed starter sources plus Reports/review_manifest.json. It writes the replacement to a temp zip first and restores the previous submission zip if final replacement fails. Run doctor or open the Workbench Submission Package panel to verify zip entry hashes against the review manifest. This is a review/submission package only; it does not claim runtime loading." + global::System.Environment.NewLine +
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
            return BuildStarterKitToolFromTemplate("Tools/list_allowed_opcodes.ps1");
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
            return BuildStarterKitToolFromTemplate("Tools/create_settings_row_snippet.ps1");
        }

        private static string BuildStarterKitLocaleEntrySnippetScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/create_locale_entry_snippet.ps1");
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
            return BuildStarterKitToolFromTemplate("Tools/build_review_manifest.ps1");
        }

        private static string BuildStarterKitSubmissionPackageScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/build_submission_package.ps1");
        }

        private static string BuildStarterKitPrepareScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/prepare_mod.ps1");
        }

        private static string BuildStarterKitIdentityScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/set_mod_identity.ps1");
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

        private static string BuildStarterKitDoctorScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/run_doctor.ps1");
        }

        private static string BuildStarterKitValidatorScript()
        {
            return BuildStarterKitToolFromTemplate("Tools/validate_structure.ps1");
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
    }
}
