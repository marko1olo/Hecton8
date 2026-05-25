using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Steam Deck and POSIX preflight scanner.
    /// This is editor/prebuild-only and does not run in player hot paths.
    /// </summary>
    public sealed class SteamDeckPosixPreflightScanner : IPreprocessBuildWithReport
    {
        private const string StrictDefine = "HECTON_STRICT_STEAM_DECK_POSIX";
        private const string ReportFilePrefix = "STEAM_DECK_POSIX_PREFLIGHT";
        private const int MaxFindings = 512;
        private const string SeverityBlocker = "BLOCKER";
        private const string SeverityWarn = "WARN";
        private const string SeverityInfo = "INFO";
        private const string LegacyResourceLoadToken = "Resources." + "Load";

        private static readonly string[] _scanRoots =
        {
            "Assets/_Project",
            "Assets/Plugins",
        };

        private static readonly string[] _textExtensions =
        {
            ".cs",
            ".shader",
            ".hlsl",
            ".compute",
        };

        private static readonly string[] _assetReferenceExtensions =
        {
            ".asset",
            ".controller",
            ".mat",
            ".prefab",
            ".png",
            ".jpg",
            ".jpeg",
            ".tga",
            ".exr",
            ".fbx",
            ".wav",
            ".ogg",
            ".mp3",
            ".shader",
            ".compute",
        };

        private static readonly string[] _windowsOnlyDlls =
        {
            "kernel32.dll",
            "user32.dll",
            "gdi32.dll",
            "winmm.dll",
            "shell32.dll",
            "advapi32.dll",
            "ole32.dll",
        };

        public int callbackOrder => -8800;

        [MenuItem("Hecton8/Audit/Steam Deck POSIX Preflight")]
        public static void RunMenuAudit()
        {
            AuditResult result = RunAudit(strict: false, writeReport: true);
            Debug.Log("[SteamDeckPosixPreflightScanner] Report written: " + result.ReportPath);
        }

        [MenuItem("Hecton8/Audit/Steam Deck POSIX Preflight Strict")]
        public static void RunMenuStrictAudit()
        {
            AuditResult result = RunAudit(strict: true, writeReport: true);
            Debug.Log("[SteamDeckPosixPreflightScanner] Strict report written: " + result.ReportPath);
        }

        public static void RunBatchAudit()
        {
            int exitCode = 0;
            try
            {
                AuditResult result = RunAudit(strict: true, writeReport: true);
                Debug.Log("[SteamDeckPosixPreflightScanner] Batch report written: " + result.ReportPath);
                if (result.BlockerCount > 0)
                    exitCode = 1;
            }
            catch (Exception exception)
            {
                exitCode = 1;
                Debug.LogError("[SteamDeckPosixPreflightScanner] Batch audit failed: " + exception);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            bool linuxPlayer = report != null && report.summary.platform == BuildTarget.StandaloneLinux64;
            bool strict = linuxPlayer || HasActiveStrictDefine();
            AuditResult result = RunAudit(strict, writeReport: true);
            if (strict && result.BlockerCount > 0)
                throw new BuildFailedException("Steam Deck/POSIX preflight blocked build. Report: " + result.ReportPath);
        }

        private static AuditResult RunAudit(bool strict, bool writeReport)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string reportPath = Path.Combine(
                projectRoot,
                "Docs",
                "Reports",
                DateTime.Now.ToString("yyyy-MM-dd") + "_" + ReportFilePrefix + ".md");

            List<Finding> findings = new List<Finding>(MaxFindings);
            Dictionary<string, string> assetPathMap = BuildAssetPathMap(projectRoot);
            Dictionary<string, string> resourcesPathMap = BuildResourcesPathMap(assetPathMap);
            List<string> textFiles = CollectTextFiles(projectRoot);

            for (int i = 0; i < textFiles.Count; i++)
            {
                string path = textFiles[i];
                string extension = Path.GetExtension(path);
                if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
                    ScanCSharpFile(projectRoot, path, strict, assetPathMap, resourcesPathMap, findings);
                else
                    ScanShaderFile(projectRoot, path, findings);
            }

            ScanNativePluginMatrix(projectRoot, findings);
            ScanNonAsciiPaths(projectRoot, findings);

            int blockers = CountSeverity(findings, SeverityBlocker);
            int warnings = CountSeverity(findings, SeverityWarn);
            string reportText = BuildReport(projectRoot, strict, blockers, warnings, findings);

            if (writeReport)
            {
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(reportPath, reportText, Encoding.UTF8);
            }

            return new AuditResult(reportPath, blockers, warnings);
        }

        private static void ScanCSharpFile(
            string projectRoot,
            string path,
            bool strict,
            Dictionary<string, string> assetPathMap,
            Dictionary<string, string> resourcesPathMap,
            List<Finding> findings)
        {
            string relativePath = ToRelativePath(projectRoot, path);
            if (IsAuditInfrastructure(relativePath) || IsEditorSource(relativePath))
                return;

            string[] lines = File.ReadAllLines(path);
            List<string> stringLiterals = new List<string>(8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;
                stringLiterals.Clear();
                ExtractStringLiterals(line, stringLiterals);

                if (line.IndexOf("using Microsoft.Win32", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("using System.Drawing", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("using System.Windows.Forms", StringComparison.Ordinal) >= 0)
                {
                    AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "Windows-only namespace is forbidden in Steam Deck/POSIX builds.");
                }

                if (line.IndexOf("[DllImport", StringComparison.Ordinal) >= 0)
                    ScanDllImportLine(relativePath, lineNumber, line, stringLiterals, findings);

                if (line.IndexOf("Environment.SpecialFolder.LocalApplicationData", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("AppData", StringComparison.Ordinal) >= 0)
                {
                    AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "Save/config path references Windows AppData instead of Application.persistentDataPath or platform PAL.");
                }

                if (line.IndexOf("System.IO.MemoryMappedFiles", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("MemoryMappedFile", StringComparison.Ordinal) >= 0)
                {
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "MMF usage requires Linux mmap/player soak proof and per-process map-count budget.");
                }

                if (line.IndexOf("SafeMemoryMappedViewHandle", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("AcquirePointer", StringComparison.Ordinal) >= 0)
                {
                    AddFinding(findings, strict ? SeverityBlocker : SeverityWarn, relativePath, lineNumber, "Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification.");
                }

                if (line.IndexOf("ThreadPriority.", StringComparison.Ordinal) >= 0)
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits.");

                if (line.IndexOf("Path.Combine(Application.persistentDataPath", StringComparison.Ordinal) >= 0)
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Persisted runtime path should route through HectonPersistentPathPolicy.");

                if (line.IndexOf(LegacyResourceLoadToken, StringComparison.Ordinal) >= 0)
                {
                    string severity = IsFirstPartySource(relativePath) ? SeverityBlocker : SeverityWarn;
                    AddFinding(findings, severity, relativePath, lineNumber, "Legacy Resources asset lookup requires Addressables migration proof.");
                }

                for (int literalIndex = 0; literalIndex < stringLiterals.Count; literalIndex++)
                {
                    string literal = stringLiterals[literalIndex];
                    if (IsHardcodedBackslashPathLiteral(literal))
                        AddFinding(findings, strict ? SeverityBlocker : SeverityWarn, relativePath, lineNumber, "Hardcoded backslash path literal found: `" + Truncate(literal, 96) + "`.");

                    ScanCaseSensitiveLiteral(relativePath, lineNumber, literal, assetPathMap, findings);
                }

                if (line.IndexOf(LegacyResourceLoadToken, StringComparison.Ordinal) >= 0)
                    ScanResourcesLiteral(relativePath, lineNumber, stringLiterals, resourcesPathMap, findings);
            }
        }

        private static void ScanDllImportLine(
            string relativePath,
            int lineNumber,
            string line,
            List<string> stringLiterals,
            List<Finding> findings)
        {
            if (line.IndexOf("kernel32.dll", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "WINDOWS-ONLY DLL BLOCKER: kernel32.dll P/Invoke is forbidden.");
                return;
            }

            for (int i = 0; i < stringLiterals.Count; i++)
            {
                string libraryName = stringLiterals[i];
                if (IsKnownWindowsDll(libraryName))
                {
                    AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "WINDOWS-ONLY DLL BLOCKER: `" + libraryName + "`.");
                    return;
                }

                if (libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "Native import names a Windows `.dll`; add platform plugin resolver/fallback.");
            }
        }

        private static void ScanShaderFile(string projectRoot, string path, List<Finding> findings)
        {
            string relativePath = ToRelativePath(projectRoot, path);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;
                if (line.IndexOf("AllMemoryBarrierWithGroupSync", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("GroupMemoryBarrierWithGroupSync", StringComparison.Ordinal) >= 0)
                {
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups.");
                }

                if (line.IndexOf("frac(sin", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Sine-based noise in shader should be replaced with LUT/hash/poly path for Deck/MX350.");

                if (LooksLikeBitwiseNoise(line))
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers.");
            }
        }

        private static void ScanNativePluginMatrix(string projectRoot, List<Finding> findings)
        {
            bool lz4Dll = HasFile(projectRoot, "liblz4.dll");
            bool lz4So = HasFile(projectRoot, "liblz4.so") || HasFile(projectRoot, "liblz4.so.1");
            bool lz4Dylib = HasFile(projectRoot, "liblz4.dylib");
            bool audioDll = HasFile(projectRoot, "HectonAudioKernel.dll");
            bool audioSo = HasFile(projectRoot, "HectonAudioKernel.so") || HasFile(projectRoot, "libHectonAudioKernel.so");
            bool audioDylib = HasFile(projectRoot, "HectonAudioKernel.dylib") || HasFile(projectRoot, "libHectonAudioKernel.dylib");
            bool steamManager = File.Exists(Path.Combine(projectRoot, "Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs".Replace('/', Path.DirectorySeparatorChar)));
            bool steamLinux = HasFile(projectRoot, "libsteam_api.so") || HasFile(projectRoot, "steam_api.so");
            bool steamworksEnabled = ProjectHasText(projectRoot, "HECTON8_STEAMWORKS");

            if (lz4Dll && !lz4So)
                AddFinding(findings, SeverityBlocker, "Assets/_Project/Plugins", 0, "WINDOWS-ONLY DLL BLOCKER: liblz4.dll exists but no liblz4.so was found for Linux/Steam Deck.");
            if (lz4Dll && !lz4Dylib)
                AddFinding(findings, SeverityWarn, "Assets/_Project/Plugins", 0, "liblz4.dylib missing; macOS save compression path is not proven.");

            if (audioDll && !audioSo)
                AddFinding(findings, SeverityBlocker, "Assets/Plugins", 0, "WINDOWS-ONLY DLL BLOCKER: HectonAudioKernel.dll exists but no HectonAudioKernel.so/libHectonAudioKernel.so was found.");
            if (audioDll && !audioDylib)
                AddFinding(findings, SeverityWarn, "Assets/Plugins", 0, "HectonAudioKernel.dylib missing; macOS native audio path is not proven.");

            if (steamManager && !steamLinux)
            {
                string severity = steamworksEnabled ? SeverityBlocker : SeverityWarn;
                AddFinding(findings, severity, "Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs", 0, "SteamManager present but libsteam_api.so evidence is missing. Steam Deck overlay/cloud/callbacks are not proven.");
            }
        }

        private static void ScanNonAsciiPaths(string projectRoot, List<Finding> findings)
        {
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
                return;

            string[] entries = Directory.GetFileSystemEntries(assetsRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < entries.Length; i++)
            {
                string relative = ToRelativePath(projectRoot, entries[i]);
                if (ContainsNonAscii(relative))
                    AddFinding(findings, SeverityWarn, relative, 0, "Non-ASCII asset path needs Unix/package/console encoding validation.");
            }
        }

        private static Dictionary<string, string> BuildAssetPathMap(string projectRoot)
        {
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
                return new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

            string[] files = Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories);
            Dictionary<string, string> map = new Dictionary<string, string>(files.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = ToRelativePath(projectRoot, files[i]);
                if (!map.ContainsKey(relative))
                    map.Add(relative, relative);
            }

            return map;
        }

        private static Dictionary<string, string> BuildResourcesPathMap(Dictionary<string, string> assetPathMap)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(assetPathMap.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in assetPathMap)
            {
                string path = pair.Value;
                int index = path.IndexOf("/Resources/", StringComparison.Ordinal);
                if (index < 0)
                    continue;

                string key = path.Substring(index + "/Resources/".Length);
                string extension = Path.GetExtension(key);
                if (!string.IsNullOrEmpty(extension))
                    key = key.Substring(0, key.Length - extension.Length);

                if (!map.ContainsKey(key))
                    map.Add(key, key);
            }

            return map;
        }

        private static List<string> CollectTextFiles(string projectRoot)
        {
            List<string> files = new List<string>(2048);
            for (int i = 0; i < _scanRoots.Length; i++)
            {
                string root = Path.Combine(projectRoot, _scanRoots[i].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                    continue;

                string[] rootFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < rootFiles.Length; fileIndex++)
                {
                    string extension = Path.GetExtension(rootFiles[fileIndex]);
                    if (IsTextExtension(extension))
                        files.Add(rootFiles[fileIndex]);
                }
            }

            return files;
        }

        private static void ScanCaseSensitiveLiteral(
            string relativePath,
            int lineNumber,
            string literal,
            Dictionary<string, string> assetPathMap,
            List<Finding> findings)
        {
            if (!LooksLikeAssetReference(literal))
                return;

            string normalized = literal.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return;

            if (assetPathMap.TryGetValue(normalized, out string actual))
            {
                if (!string.Equals(actual, normalized, StringComparison.Ordinal))
                    AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "Case-sensitive path mismatch: requested `" + normalized + "` actual `" + actual + "`.");
            }
            else
            {
                AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Asset path literal was not found exactly: `" + normalized + "`.");
            }
        }

        private static void ScanResourcesLiteral(
            string relativePath,
            int lineNumber,
            List<string> literals,
            Dictionary<string, string> resourcesPathMap,
            List<Finding> findings)
        {
            for (int i = 0; i < literals.Count; i++)
            {
                string literal = literals[i];
                if (string.IsNullOrEmpty(literal) || HasSafeExtension(literal))
                    continue;

                string normalized = literal.Replace('\\', '/');
                if (resourcesPathMap.TryGetValue(normalized, out string actual))
                {
                    if (!string.Equals(actual, normalized, StringComparison.Ordinal))
                        AddFinding(findings, SeverityBlocker, relativePath, lineNumber, "Legacy Resources key case mismatch: requested `" + normalized + "` actual `" + actual + "`.");
                }
                else
                {
                    AddFinding(findings, SeverityWarn, relativePath, lineNumber, "Legacy Resources key not found by static path map: `" + normalized + "`.");
                }
            }
        }

        private static string BuildReport(string projectRoot, bool strict, int blockers, int warnings, List<Finding> findings)
        {
            StringBuilder report = new StringBuilder(32768);
            report.AppendLine("# Steam Deck POSIX Preflight");
            report.AppendLine();
            report.AppendLine("- Status: PENDING VERIFICATION");
            report.AppendLine("- Strict mode: " + strict);
            report.AppendLine("- Project root: " + projectRoot.Replace('\\', '/'));
            report.AppendLine("- Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("- Proof boundary: static/editor scan only. No Linux player launch, Steam Deck device run, Vulkan RenderDoc capture, profiler, GCMonitor, thermals, or battery API proof.");
            report.AppendLine("- Blockers: " + blockers);
            report.AppendLine("- Warnings: " + warnings);
            report.AppendLine();

            report.AppendLine("## Mandates Applied");
            report.AppendLine();
            report.AppendLine("- `PROJECT_LTS_Compatibility_Layer.txt`");
            report.AppendLine("- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`");
            report.AppendLine("- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`");
            report.AppendLine("- `CTRL_Device_Abstraction_Haptics.txt`");
            report.AppendLine("- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`");
            report.AppendLine("- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`");
            report.AppendLine("- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`");
            report.AppendLine("- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`");
            report.AppendLine("- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`");
            report.AppendLine();

            report.AppendLine("## POSIX Storage Path Resolution Code");
            report.AppendLine();
            report.AppendLine("Current first-party save/storage policy is FileStream plus NativeArray scratch buffers, rooted through Unity persistent paths or the project path PAL. This is not player proof; Linux/Deck save-load still needs a real player run.");
            report.AppendLine();
            report.AppendLine("```csharp");
            report.AppendLine("string root = Application.persistentDataPath;");
            report.AppendLine("string safeName = Path.GetFileName(relativeName);");
            report.AppendLine("string path = Path.Combine(root, safeName);");
            report.AppendLine("using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))");
            report.AppendLine("{");
            report.AppendLine("    // Copy into pre-owned NativeArray scratch before decode.");
            report.AppendLine("}");
            report.AppendLine("```");
            report.AppendLine();

            report.AppendLine("## Case-Sensitive Path Audit Logic");
            report.AppendLine();
            report.AppendLine("```csharp");
            report.AppendLine("Dictionary<string,string> map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);");
            report.AppendLine("// Store exact Assets/... path as value; probe string literals with OrdinalIgnoreCase lookup.");
            report.AppendLine("// If lookup succeeds but requested path != actual path by StringComparison.Ordinal, fail the audit.");
            report.AppendLine("```");
            report.AppendLine();

            report.AppendLine("## Unity Hub Cannot Fix");
            report.AppendLine();
            report.AppendLine("- Missing Linux/macOS native plugin binaries (`liblz4.so`, `HectonAudioKernel.so`, `libsteam_api.so`).");
            report.AppendLine("- Save/load correctness and IO latency under real Linux/Steam Deck storage.");
            report.AppendLine("- Shader barrier/noise compatibility on older Vulkan drivers.");
            report.AppendLine("- Steam Deck gyro/trackpad/haptic integration without a SteamInput/PAL owner.");
            report.AppendLine("- Case-sensitive asset path defects.");
            report.AppendLine();

            AppendFindings(report, "Blockers", findings, SeverityBlocker);
            AppendFindings(report, "Warnings", findings, SeverityWarn);
            AppendFindings(report, "Info", findings, SeverityInfo);

            report.AppendLine("## Regression Model");
            report.AppendLine();
            report.AppendLine("- CPU: scanner/editor tooling adds no player hot-path work. Removing Win32 sparse hint is cold-path only and may affect disk allocation, not frame time.");
            report.AppendLine("- GC: scanner allocations are editor-only. Runtime GC proof remains absent until Play Mode/player profiling.");
            report.AppendLine("- Memory: no project settings, scenes, prefabs, URP assets, or Addressables groups are mutated.");
            report.AppendLine("- Correctness: Linux support remains pending while native dependency parity and real save-load player proof are absent.");
            report.AppendLine("- Failure modes: missing native plugins, shader compile/runtime Vulkan defect, file path case mismatch, save-load IO latency, Steam Deck input provider absence.");
            report.AppendLine();
            return report.ToString();
        }

        private static void AppendFindings(StringBuilder report, string title, List<Finding> findings, string severity)
        {
            report.AppendLine("## " + title);
            report.AppendLine();
            report.AppendLine("| Severity | Location | Finding |");
            report.AppendLine("|---|---|---|");
            bool any = false;
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                if (!string.Equals(finding.Severity, severity, StringComparison.Ordinal))
                    continue;

                any = true;
                report.Append("| ");
                report.Append(finding.Severity);
                report.Append(" | ");
                report.Append(finding.Path);
                if (finding.Line > 0)
                {
                    report.Append(":");
                    report.Append(finding.Line);
                }

                report.Append(" | ");
                report.Append(EscapeTable(finding.Message));
                report.AppendLine(" |");
            }

            if (!any)
                report.AppendLine("| " + severity + " | - | none |");

            report.AppendLine();
        }

        private static void ExtractStringLiterals(string line, List<string> output)
        {
            for (int i = 0; i < line.Length; i++)
            {
                bool verbatim = line[i] == '@' && i + 1 < line.Length && line[i + 1] == '"';
                if (verbatim)
                {
                    i += 2;
                    StringBuilder literal = new StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                literal.Append('"');
                                i += 2;
                                continue;
                            }

                            break;
                        }

                        literal.Append(line[i]);
                        i++;
                    }

                    output.Add(literal.ToString());
                    continue;
                }

                if (line[i] != '"')
                    continue;

                i++;
                StringBuilder normalLiteral = new StringBuilder();
                while (i < line.Length)
                {
                    char c = line[i];
                    if (c == '"')
                        break;

                    if (c == '\\' && i + 1 < line.Length)
                    {
                        char next = line[i + 1];
                        if (next == '\\' || next == '"')
                            normalLiteral.Append(next);
                        i += 2;
                        continue;
                    }

                    normalLiteral.Append(c);
                    i++;
                }

                output.Add(normalLiteral.ToString());
            }
        }

        private static bool LooksLikeAssetReference(string literal)
        {
            if (string.IsNullOrEmpty(literal))
                return false;

            if (literal.IndexOf('/') < 0 && literal.IndexOf('\\') < 0)
                return false;

            if (!TryGetSafeExtension(literal, out string extension) || string.IsNullOrEmpty(extension))
                return false;

            for (int i = 0; i < _assetReferenceExtensions.Length; i++)
            {
                if (string.Equals(extension, _assetReferenceExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsAuditInfrastructure(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.EndsWith("/SteamDeckPosixPreflightScanner.cs", StringComparison.Ordinal) ||
                   normalized.EndsWith("/PlatformCompatibilityAudit.cs", StringComparison.Ordinal);
        }

        private static bool IsEditorSource(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFirstPartySource(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.StartsWith("Assets/_Project/", StringComparison.Ordinal);
        }

        private static bool IsHardcodedBackslashPathLiteral(string literal)
        {
            if (string.IsNullOrEmpty(literal))
                return false;

            if (literal.Length >= 3 &&
                IsAsciiLetter(literal[0]) &&
                literal[1] == ':' &&
                literal[2] == '\\')
            {
                return true;
            }

            if (literal.Length >= 4 &&
                literal[0] == '\\' &&
                literal[1] == '\\' &&
                IsPathSegmentChar(literal[2]))
            {
                return true;
            }

            if (LooksLikeRegexLiteral(literal))
                return false;

            for (int i = 1; i < literal.Length - 1; i++)
            {
                if (literal[i] != '\\')
                    continue;

                if (IsPathSegmentChar(literal[i - 1]) && IsPathSegmentStartChar(literal[i + 1]))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeRegexLiteral(string literal)
        {
            return literal.IndexOf("(?<", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\d", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\D", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\s", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\S", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\w", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\W", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\b", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\B", StringComparison.Ordinal) >= 0 ||
                   literal.IndexOf("\\.", StringComparison.Ordinal) >= 0 ||
                   (literal.IndexOf('[', StringComparison.Ordinal) >= 0 &&
                    literal.IndexOf(']', StringComparison.Ordinal) >= 0 &&
                    literal.IndexOf('\\') >= 0);
        }

        private static bool IsAsciiLetter(char value)
        {
            return ((uint)(value - 'A') <= 25u) || ((uint)(value - 'a') <= 25u);
        }

        private static bool IsPathSegmentChar(char value)
        {
            return char.IsLetterOrDigit(value) ||
                   value == '_' ||
                   value == '-' ||
                   value == '.' ||
                   value == ' ';
        }

        private static bool IsPathSegmentStartChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool HasSafeExtension(string literal)
        {
            return TryGetSafeExtension(literal, out string extension) && !string.IsNullOrEmpty(extension);
        }

        private static bool TryGetSafeExtension(string literal, out string extension)
        {
            extension = string.Empty;
            if (string.IsNullOrEmpty(literal))
                return false;

            if (literal.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            try
            {
                extension = Path.GetExtension(literal);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static bool LooksLikeBitwiseNoise(string line)
        {
            string lower = line.ToLowerInvariant();
            bool noise = lower.IndexOf("noise", StringComparison.Ordinal) >= 0 ||
                         lower.IndexOf("hash", StringComparison.Ordinal) >= 0 ||
                         lower.IndexOf("random", StringComparison.Ordinal) >= 0 ||
                         lower.IndexOf("dither", StringComparison.Ordinal) >= 0;
            if (!noise)
                return false;

            return line.IndexOf("asuint", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf(">>", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("<<", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("&", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("|", StringComparison.Ordinal) >= 0;
        }

        private static bool IsTextExtension(string extension)
        {
            for (int i = 0; i < _textExtensions.Length; i++)
            {
                if (string.Equals(extension, _textExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsKnownWindowsDll(string value)
        {
            for (int i = 0; i < _windowsOnlyDlls.Length; i++)
            {
                if (string.Equals(value, _windowsOnlyDlls[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasFile(string projectRoot, string fileName)
        {
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
                return false;

            foreach (string path in Directory.EnumerateFiles(assetsRoot, fileName, SearchOption.AllDirectories))
            {
                if (!string.IsNullOrEmpty(path))
                    return true;
            }

            return false;
        }

        private static bool ProjectHasText(string projectRoot, string needle)
        {
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets"),
                Path.Combine(projectRoot, "Packages"),
                Path.Combine(projectRoot, "ProjectSettings"),
            };

            for (int i = 0; i < roots.Length; i++)
            {
                if (!Directory.Exists(roots[i]))
                    continue;

                string[] files = Directory.GetFiles(roots[i], "*", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string extension = Path.GetExtension(files[fileIndex]);
                    if (files[fileIndex].EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                        (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string text = File.ReadAllText(files[fileIndex]);
                    if (text.IndexOf(needle, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsNonAscii(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 127)
                    return true;
            }

            return false;
        }

        private static int CountSeverity(List<Finding> findings, string severity)
        {
            int count = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (string.Equals(findings[i].Severity, severity, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static void AddFinding(List<Finding> findings, string severity, string path, int line, string message)
        {
            if (findings.Count >= MaxFindings)
                return;

            findings.Add(new Finding(severity, path, line, message));
        }

        private static string ToRelativePath(string projectRoot, string path)
        {
            string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            if (fullPath.StartsWith(fullRoot + "/", StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length + 1);

            return fullPath;
        }

        private static string EscapeTable(string value)
        {
            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;

            return value.Substring(0, max) + "...";
        }

        private static bool HasActiveStrictDefine()
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(group);
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            return defines.IndexOf(StrictDefine, StringComparison.Ordinal) >= 0;
        }

        private readonly struct AuditResult
        {
            public readonly string ReportPath;
            public readonly int BlockerCount;
            public readonly int WarningCount;

            public AuditResult(string reportPath, int blockerCount, int warningCount)
            {
                ReportPath = reportPath;
                BlockerCount = blockerCount;
                WarningCount = warningCount;
            }
        }

        private readonly struct Finding
        {
            public readonly string Severity;
            public readonly string Path;
            public readonly int Line;
            public readonly string Message;

            public Finding(string severity, string path, int line, string message)
            {
                Severity = severity;
                Path = path;
                Line = line;
                Message = message;
            }
        }
    }
}
