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
    /// Editor-only facade over the public external starter kit.
    /// </summary>
    public sealed class ExternalStarterKitWorkbenchWindow : EditorWindow
    {
        private const string ExternalStarterKitRoot = "ModdingSDK/ExternalStarterKit";
        private const string RuntimeBoundary =
            "Runtime API: envelope-only. This Workbench edits starter-kit files and runs validation tools; it does not enable managed DLL, Harmony, BepInEx, loose AssetBundle, PNG, or localization runtime ingress.";
        private const int ToolSummaryLineCount = 16;
        private const int MaxFreshnessScanFiles = 512;
        private static readonly string[] RequiredStarterFiles =
        {
            "README.md",
            "mod.h8manifest.json",
            "mod.json",
            "Graphs/main.h8graph.json",
            "Tables/settings.h8table.json",
            "Locales/en.h8loc.json",
            "Content/assets.h8manifest.json",
            "Reference/allowed_opcodes.csv",
            "Reference/kernel_tuning_profiles.csv",
            "Schemas/h8mod.authoring.schema.json",
            "Schemas/runtime.mod.schema.json",
            "Schemas/h8graph.schema.json",
            "Schemas/h8table.schema.json",
            "Schemas/h8loc.schema.json",
            ".vscode/settings.json",
            "Tools/prepare_mod.ps1",
            "Tools/set_mod_identity.ps1",
            "Tools/list_allowed_opcodes.ps1",
            "Tools/validate_structure.ps1",
            "Tools/build_review_manifest.ps1"
        };

        private Vector2 _scrollPosition;
        private string _modId = string.Empty;
        private string _displayName = string.Empty;
        private string _author = string.Empty;
        private string _version = string.Empty;
        private string _starterHealthSummary = "Starter kit health not loaded.";
        private string _starterHealthDetails = string.Empty;
        private string _reviewSummary = "Review manifest not loaded.";
        private string _reviewFreshnessSummary = "Review freshness not loaded.";
        private string _toolSummary = string.Empty;
        private bool _starterHealthHasMissingFiles;
        private bool _reviewFreshnessWarning;
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

                if (GUILayout.Button("Validate Structure Only", GUILayout.Height(28f)))
                    RunStarterTool("Tools/validate_structure.ps1", string.Empty, true);
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("List Graph Opcodes", GUILayout.Height(28f)))
                    RunStarterTool("Tools/list_allowed_opcodes.ps1", string.Empty, false);
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

                if (GUILayout.Button("Command Graph"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Graphs/main.h8graph.json");
            }

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Settings Table"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Tables/settings.h8table.json");

                if (GUILayout.Button("Locale"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Locales/en.h8loc.json");

                if (GUILayout.Button("Review Manifest"))
                    OpenRelativePath("ModdingSDK/ExternalStarterKit/Reports/review_manifest.json");
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

            if (!string.IsNullOrWhiteSpace(_toolSummary))
            {
                EditorGUILayout.LabelField("Last Tool Output", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_toolSummary, MessageType.Info);
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

        private void CreateOrRefreshStarterKit()
        {
            ModdingSdkHubWindow.CreateExternalStarterKit();
            Reload();
        }

        private void Reload()
        {
            LoadStarterHealth();
            LoadIdentity();
            LoadReviewSummary();
            Repaint();
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
            }
            catch (Exception exception)
            {
                _toolSummary = "Identity load failed: " + exception.Message;
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
                Repaint();
                return;
            }

            string rootPath = ResolveProjectPath(ExternalStarterKitRoot);
            string scriptPath = Path.Combine(rootPath, scriptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptPath))
            {
                _toolSummary = "Missing starter tool: " + scriptPath;
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
                    DisposeRunningTool();
                    Repaint();
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _toolSummary = "Tool running: " + scriptRelativePath;
                EditorApplication.update -= PollRunningTool;
                EditorApplication.update += PollRunningTool;
                Repaint();
            }
            catch (Exception exception)
            {
                _toolSummary = "Tool launch failed: " + exception.Message;
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
            string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
            if (string.IsNullOrWhiteSpace(output))
                return "Tool exit code: " + exitCode + Environment.NewLine + "No output.";

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
            public string Id;
            public string DisplayName;
            public string Author;
            public string Version;
        }

        [Serializable]
        private sealed class ReviewManifest
        {
            public string RootId;
            public ReviewIdentity Identity = new ReviewIdentity();
            public int FileCount;
            public long TotalBytes;
        }

        [Serializable]
        private sealed class ReviewIdentity
        {
            public string Id;
            public string DisplayName;
            public string Author;
            public string Version;
        }
    }
}
