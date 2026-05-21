using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.GeologyForge
{
    internal static class RuntimeMeshGenerationScanner
    {
        private const double AsyncScanBudgetSeconds = 0.004;
        private const string AsyncScanProgressMessage = "Scanning source files";

        private static readonly Stopwatch _AsyncScanStopwatch = new Stopwatch();
        private static readonly List<string> _asyncFiles = new List<string>(64);
        private static readonly List<string> _asyncDirectoryStack = new List<string>(16);
        private static readonly List<Finding> _asyncFindings = new List<Finding>(64);
        private static int _asyncScannedFileCount;
        private static bool _asyncScanActive;

        private static readonly string[] _ScanRoots =
        {
            "Assets/_Project/Scripts"
        };

        private static readonly string[] _ForbiddenPatterns =
        {
            "new Mesh(",
            ".SetVertices(",
            "SetVertexBufferParams(",
            "Mesh.AllocateWritableMeshData(",
            "VoxelMCExtractJob",
            "Marching Cubes",
            "mesh.vertices",
            ".RecalculateNormals(",
            ".material"
        };

        [MenuItem("HECTON-8/Geology Forge/Scan Runtime Mesh Generation", false, 181)]
        public static void ScanAndWriteReport()
        {
            if (!Application.isBatchMode && StartAsyncScan())
                return;

            List<Finding> findings = Scan();
            WriteReport(findings);
            Debug.Log("[SHINOBU_208] Runtime mesh generation scan wrote " + GeologyForgeConstants.ScannerReportPath + " with " + findings.Count + " findings.");
        }

        [MenuItem("HECTON-8/Geology Forge/Cancel Runtime Mesh Scan", false, 182)]
        public static void CancelAsyncScan()
        {
            if (!_asyncScanActive)
                return;

            EditorApplication.update -= TickAsyncScan;
            if (!Application.isBatchMode)
                EditorUtility.ClearProgressBar();
            ClearAsyncScanState();
        }

        public static List<Finding> Scan()
        {
            List<string> files = CollectScanFiles();
            var findings = new List<Finding>(64);
            for (int i = 0; i < files.Count; i++)
                ScanFile(files[i], findings);

            return findings;
        }

        private static bool StartAsyncScan()
        {
            if (_asyncScanActive)
            {
                Debug.LogWarning("[SHINOBU_208] Runtime mesh generation scan request ignored: scan already active.");
                return true;
            }

            ClearAsyncScanState();
            _asyncScanActive = true;
            SeedAsyncScanRoots();
            EditorApplication.update -= TickAsyncScan;
            EditorApplication.update += TickAsyncScan;
            return true;
        }

        private static void TickAsyncScan()
        {
            if (!_asyncScanActive)
                return;

            try
            {
                if (!Application.isBatchMode)
                {
                    float progress = EstimateAsyncProgress();
                    if (EditorUtility.DisplayCancelableProgressBar("Geology Runtime Mesh Scan", AsyncScanProgressMessage, progress))
                    {
                        CancelAsyncScan();
                        Debug.LogWarning("[SHINOBU_208] Runtime mesh generation scan canceled.");
                        return;
                    }
                }

                _AsyncScanStopwatch.Restart();
                while (true)
                {
                    if (_asyncFiles.Count > 0)
                    {
                        int lastFile = _asyncFiles.Count - 1;
                        string path = _asyncFiles[lastFile];
                        _asyncFiles.RemoveAt(lastFile);
                        ScanFile(path, _asyncFindings);
                        _asyncScannedFileCount++;
                    }
                    else if (_asyncDirectoryStack.Count > 0)
                    {
                        ExpandNextAsyncDirectory();
                    }
                    else
                    {
                        FinishAsyncScan();
                        return;
                    }

                    if (_AsyncScanStopwatch.Elapsed.TotalSeconds >= AsyncScanBudgetSeconds)
                        break;
                }
            }
            catch (System.Exception ex)
            {
                CancelAsyncScan();
                Debug.LogException(ex);
            }
        }

        private static void FinishAsyncScan()
        {
            if (!_asyncScanActive)
                return;

            int completedFindingCount = _asyncFindings.Count;
            EditorApplication.update -= TickAsyncScan;
            if (!Application.isBatchMode)
                EditorUtility.ClearProgressBar();

            try
            {
                WriteReport(_asyncFindings);
                Debug.Log("[SHINOBU_208] Runtime mesh generation scan wrote " + GeologyForgeConstants.ScannerReportPath + " with " + completedFindingCount + " findings.");
            }
            finally
            {
                ClearAsyncScanState();
            }
        }

        private static void ClearAsyncScanState()
        {
            _asyncFiles.Clear();
            _asyncDirectoryStack.Clear();
            _asyncFindings.Clear();
            _asyncScannedFileCount = 0;
            _asyncScanActive = false;
            _AsyncScanStopwatch.Reset();
        }

        private static void SeedAsyncScanRoots()
        {
            for (int rootIndex = 0; rootIndex < _ScanRoots.Length; rootIndex++)
            {
                string root = _ScanRoots[rootIndex];
                if (Directory.Exists(root))
                    _asyncDirectoryStack.Add(root.Replace('\\', '/'));
            }

        }

        private static void ExpandNextAsyncDirectory()
        {
            int lastDirectory = _asyncDirectoryStack.Count - 1;
            string directory = _asyncDirectoryStack[lastDirectory];
            _asyncDirectoryStack.RemoveAt(lastDirectory);
            if (IsEditorPath(directory))
                return;

            try
            {
                string[] childDirectories = Directory.GetDirectories(directory);
                for (int i = 0; i < childDirectories.Length; i++)
                {
                    string childDirectory = childDirectories[i].Replace('\\', '/');
                    if (!IsEditorPath(childDirectory))
                        _asyncDirectoryStack.Add(childDirectory);
                }

                string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    if (!IsEditorPath(path))
                        _asyncFiles.Add(path);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SHINOBU_208] Runtime mesh generation scan skipped " + directory + ": " + ex.GetType().Name);
            }
        }

        private static float EstimateAsyncProgress()
        {
            int queuedWork = _asyncFiles.Count + _asyncDirectoryStack.Count;
            int totalKnown = _asyncScannedFileCount + queuedWork;
            if (totalKnown <= 0)
                return 0.95f;

            return Mathf.Clamp01((float)_asyncScannedFileCount / totalKnown);
        }

        private static List<string> CollectScanFiles()
        {
            var files = new List<string>(128);
            for (int rootIndex = 0; rootIndex < _ScanRoots.Length; rootIndex++)
            {
                string root = _ScanRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] rootFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < rootFiles.Length; fileIndex++)
                {
                    string path = rootFiles[fileIndex].Replace('\\', '/');
                    if (IsEditorPath(path))
                        continue;

                    files.Add(path);
                }
            }

            return files;
        }

        private static bool IsEditorPath(string path)
        {
            return path.Contains("/Editor/") || path.EndsWith("/Editor");
        }

        private static void ScanFile(string path, List<Finding> findings)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                int lineIndex = 0;
                int braceDepth = 0;
                MethodScope methodScope = default;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineIndex++;
                    string trimmed = line.TrimStart();
                    bool commentOnly = IsCommentOnly(trimmed);
                    if (!commentOnly && TryExtractMethodName(trimmed, out string methodName))
                    {
                        methodScope.Active = true;
                        methodScope.Opened = line.IndexOf('{') >= 0;
                        methodScope.MethodName = methodName;
                        methodScope.StartLine = lineIndex;
                        methodScope.ParentBraceDepth = braceDepth;
                    }

                    for (int patternIndex = 0; patternIndex < _ForbiddenPatterns.Length; patternIndex++)
                    {
                        string pattern = _ForbiddenPatterns[patternIndex];
                        if (line.Contains(pattern))
                        {
                            string executionContext = ClassifyExecutionContext(methodScope, commentOnly);
                            string kind = ClassifyPattern(pattern);
                            findings.Add(new Finding
                            {
                                Path = path,
                                Line = lineIndex,
                                Pattern = pattern,
                                Kind = kind,
                                ExecutionContext = executionContext,
                                Method = methodScope.Active ? methodScope.MethodName : string.Empty,
                                RuntimePhaseRisk = ClassifyRisk(kind, executionContext, commentOnly),
                                CommentOnly = commentOnly
                            });
                        }
                    }

                    int openingBraces = CountChar(line, '{');
                    int closingBraces = CountChar(line, '}');
                    if (methodScope.Active && openingBraces > 0)
                        methodScope.Opened = true;

                    braceDepth += openingBraces;
                    braceDepth -= closingBraces;
                    if (braceDepth < 0)
                        braceDepth = 0;

                    if (methodScope.Active && methodScope.Opened && lineIndex > methodScope.StartLine && braceDepth <= methodScope.ParentBraceDepth)
                        methodScope = default;
                }
            }
        }

        private static void WriteReport(List<Finding> findings)
        {
            string folder = Path.GetDirectoryName(GeologyForgeConstants.ScannerReportPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int actionableCount = 0;
            int simulationCount = 0;
            int bootstrapCount = 0;
            int materialCloneCount = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                if (!finding.CommentOnly)
                    actionableCount++;
                if (finding.ExecutionContext == "SIMULATION_RUNTIME")
                    simulationCount++;
                if (finding.ExecutionContext == "BOOTSTRAP_RUNTIME")
                    bootstrapCount++;
                if (finding.Kind == "PROCEDURAL_MATERIAL_CLONE")
                    materialCloneCount++;
            }

            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_208\",\n  \"schemaVersion\": 2,\n  \"status\": \"PENDING_VERIFICATION\",\n  \"scanScope\": \"Assets/_Project/Scripts excluding Editor folders\",\n  \"runtimeMeshAllocationsEradicated\": ");
            builder.Append(actionableCount == 0 ? "true" : "false");
            builder.Append(",\n  \"findingCount\": ");
            builder.Append(findings.Count);
            builder.Append(",\n  \"actionableFindingCount\": ");
            builder.Append(actionableCount);
            builder.Append(",\n  \"simulationPhaseFindingCount\": ");
            builder.Append(simulationCount);
            builder.Append(",\n  \"bootstrapPhaseFindingCount\": ");
            builder.Append(bootstrapCount);
            builder.Append(",\n  \"proceduralMaterialCloneFindingCount\": ");
            builder.Append(materialCloneCount);
            builder.Append(",\n  \"findings\": [\n");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                if (i > 0)
                    builder.Append(",\n");
                builder.Append("    { \"path\": \"");
                builder.Append(Escape(finding.Path));
                builder.Append("\", \"line\": ");
                builder.Append(finding.Line);
                builder.Append(", \"pattern\": \"");
                builder.Append(Escape(finding.Pattern));
                builder.Append("\", \"kind\": \"");
                builder.Append(Escape(finding.Kind));
                builder.Append("\", \"executionContext\": \"");
                builder.Append(Escape(finding.ExecutionContext));
                builder.Append("\", \"method\": \"");
                builder.Append(Escape(finding.Method));
                builder.Append("\", \"runtimePhaseRisk\": \"");
                builder.Append(Escape(finding.RuntimePhaseRisk));
                builder.Append("\", \"commentOnly\": ");
                builder.Append(finding.CommentOnly ? "true" : "false");
                builder.Append(" }");
            }

            builder.Append("\n  ],\n  \"note\": \"Editor-only Geology Forge added. Remaining runtime topology sites require owner-specific removal, not blind cross-domain deletion. Schema v2 scans the project runtime script surface and classifies context/risk so integrators can route SIMULATION_RUNTIME before comment-only archaeology.\"\n}\n");
            WriteAtomicText(GeologyForgeConstants.ScannerReportPath, builder.ToString());
        }

        private static void WriteAtomicText(string path, string contents)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            try
            {
                File.WriteAllText(tempPath, contents);
                if (File.Exists(path))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(tempPath, path, backupPath);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private static bool IsCommentOnly(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*");
        }

        private static bool TryExtractMethodName(string trimmed, out string methodName)
        {
            methodName = string.Empty;
            if (trimmed.Length == 0 || trimmed.EndsWith(";") || trimmed.Contains("=>"))
                return false;
            if (trimmed[0] == '[' || !LooksLikeMethodDeclaration(trimmed))
                return false;

            int paren = trimmed.IndexOf('(');
            if (paren <= 0)
                return false;

            int nameEnd = paren - 1;
            while (nameEnd >= 0 && char.IsWhiteSpace(trimmed[nameEnd]))
                nameEnd--;

            int nameStart = nameEnd;
            while (nameStart >= 0 && IsIdentifierChar(trimmed[nameStart]))
                nameStart--;

            nameStart++;
            int length = nameEnd - nameStart + 1;
            if (length <= 0)
                return false;

            string candidate = trimmed.Substring(nameStart, length);
            if (IsControlKeyword(candidate))
                return false;

            methodName = candidate;
            return true;
        }

        private static bool LooksLikeMethodDeclaration(string trimmed)
        {
            return trimmed.StartsWith("public ")
                || trimmed.StartsWith("private ")
                || trimmed.StartsWith("protected ")
                || trimmed.StartsWith("internal ")
                || trimmed.StartsWith("static ")
                || trimmed.StartsWith("unsafe ")
                || trimmed.StartsWith("async ")
                || trimmed.StartsWith("void ");
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsControlKeyword(string value)
        {
            return value == "if"
                || value == "for"
                || value == "while"
                || value == "switch"
                || value == "catch"
                || value == "using"
                || value == "lock"
                || value == "return"
                || value == "new";
        }

        private static int CountChar(string value, char target)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == target)
                    count++;
            }

            return count;
        }

        private static string ClassifyPattern(string pattern)
        {
            if (pattern == ".material")
                return "PROCEDURAL_MATERIAL_CLONE";
            if (pattern == "new Mesh(")
                return "RUNTIME_MESH_ALLOCATION";
            if (pattern == ".RecalculateNormals(")
                return "RUNTIME_NORMAL_RECOMPUTE";
            if (pattern == "VoxelMCExtractJob" || pattern == "Marching Cubes")
                return "MARCHING_CUBES_RUNTIME_TOPOLOGY";
            if (pattern == "mesh.vertices")
                return "MANAGED_MESH_READBACK_OR_UPLOAD";

            return "MESH_BUFFER_UPLOAD";
        }

        private static string ClassifyExecutionContext(MethodScope methodScope, bool commentOnly)
        {
            if (commentOnly)
                return "COMMENT_OR_DOC";
            if (!methodScope.Active)
                return "TYPE_OR_FIELD_SCOPE";

            string method = methodScope.MethodName;
            if (method == "Awake" || method == "Start" || method == "OnEnable")
                return "BOOTSTRAP_RUNTIME";
            if (method == "Update" || method == "FixedUpdate" || method == "LateUpdate" || method == "Tick" || method == "FixedTick" || method == "SlowTick" || method == "OnUpdate")
                return "SIMULATION_RUNTIME";
            if (method.IndexOf("Simulation") >= 0 || method.IndexOf("SIMULATION") >= 0 || method.IndexOf("Runtime") >= 0)
                return "SIMULATION_RUNTIME";

            return "RUNTIME_HELPER";
        }

        private static string ClassifyRisk(string kind, string executionContext, bool commentOnly)
        {
            if (commentOnly)
                return "LOW_COMMENT_ONLY";
            if (executionContext == "SIMULATION_RUNTIME")
                return "CRITICAL_SIMULATION_HOT_PATH";
            if (kind == "PROCEDURAL_MATERIAL_CLONE")
                return "CRITICAL_BATCHER_BREAK";
            if (executionContext == "BOOTSTRAP_RUNTIME")
                return "HIGH_BOOTSTRAP_STALL";
            if (kind == "MARCHING_CUBES_RUNTIME_TOPOLOGY")
                return "HIGH_DYNAMIC_TOPOLOGY";

            return "MEDIUM_RUNTIME_HELPER";
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal struct Finding
        {
            public string Path;
            public int Line;
            public string Pattern;
            public string Kind;
            public string ExecutionContext;
            public string Method;
            public string RuntimePhaseRisk;
            public bool CommentOnly;
        }

        private struct MethodScope
        {
            public string MethodName;
            public int StartLine;
            public int ParentBraceDepth;
            public bool Active;
            public bool Opened;
        }
    }
}
