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

        [MenuItem("Hecton8/Geology Forge/Scan Runtime Mesh Generation", false, 181)]
        public static void ScanAndWriteReport()
        {
            if (!Application.isBatchMode && StartAsyncScan())
                return;

            List<Finding> findings = Scan();
            WriteReport(findings);
            Debug.Log("[SHINOBU_208] Runtime mesh generation scan wrote " + GeologyForgeConstants.ScannerReportPath + " with " + findings.Count + " findings.");
        }

        [MenuItem("Hecton8/Geology Forge/Cancel Runtime Mesh Scan", false, 182)]
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
                foreach (string childDirectoryPath in Directory.EnumerateDirectories(directory))
                {
                    string childDirectory = childDirectoryPath.Replace('\\', '/');
                    if (!IsEditorPath(childDirectory))
                        _asyncDirectoryStack.Add(childDirectory);
                }

                foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
                {
                    string path = file.Replace('\\', '/');
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

                foreach (string rootFile in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string path = rootFile.Replace('\\', '/');
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
                bool pendingContextMenuAttribute = false;
                List<PreprocessorScope> preprocessorStack = new List<PreprocessorScope>(4);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineIndex++;
                    string trimmed = line.TrimStart();
                    if (TryUpdateEditorOnlyPreprocessorScope(trimmed, preprocessorStack))
                        continue;

                    bool commentOnly = IsCommentOnly(trimmed);
                    bool editorCompileGuarded = IsEditorCompileGuarded(preprocessorStack);

                    UpdateMethodScope(line, trimmed, lineIndex, braceDepth, commentOnly, ref methodScope, ref pendingContextMenuAttribute);
                    TrackPlayModeBlockedContextMenu(trimmed, commentOnly, ref methodScope);
                    EvaluatePatterns(path, line, lineIndex, commentOnly, editorCompileGuarded, methodScope, findings);
                    UpdateBraceDepth(line, lineIndex, ref braceDepth, ref methodScope);
                }
            }
        }

        private static void UpdateMethodScope(string line, string trimmed, int lineIndex, int braceDepth, bool commentOnly, ref MethodScope methodScope, ref bool pendingContextMenuAttribute)
        {
            TrackPendingContextMenuAttribute(trimmed, commentOnly, ref pendingContextMenuAttribute);
            if (!commentOnly && TryExtractMethodName(trimmed, out string methodName))
            {
                methodScope.Active = true;
                methodScope.Opened = line.IndexOf('\x7B') >= 0;
                methodScope.MethodName = methodName;
                methodScope.StartLine = lineIndex;
                methodScope.ParentBraceDepth = braceDepth;
                methodScope.ContextMenu = pendingContextMenuAttribute;
                pendingContextMenuAttribute = false;
            }
            else if (!commentOnly && ShouldClearPendingContextMenuAttribute(trimmed))
            {
                pendingContextMenuAttribute = false;
            }
        }

        private static void EvaluatePatterns(string path, string line, int lineIndex, bool commentOnly, bool editorCompileGuarded, MethodScope methodScope, List<Finding> findings)
        {
            for (int patternIndex = 0; patternIndex < _ForbiddenPatterns.Length; patternIndex++)
            {
                string pattern = _ForbiddenPatterns[patternIndex];
                if (LineContainsPattern(line, pattern))
                {
                    string executionContext = ClassifyExecutionContext(methodScope, commentOnly, editorCompileGuarded);
                    string kind = ClassifyPattern(pattern, line);
                    string ownerDomain = ClassifyOwnerDomain(path);
                    bool approvedCoreVoxelPipeline = !commentOnly && IsApprovedCoreVoxelPipeline(path, methodScope, kind, line);
                    findings.Add(new Finding
                    {
                        Path = path,
                        Line = lineIndex,
                        Pattern = pattern,
                        Kind = kind,
                        OwnerDomain = ownerDomain,
                        ApprovedCoreVoxelPipeline = approvedCoreVoxelPipeline,
                        ExecutionContext = executionContext,
                        Method = methodScope.Active ? methodScope.MethodName : string.Empty,
                        RuntimePhaseRisk = ClassifyRisk(kind, executionContext, commentOnly, editorCompileGuarded),
                        CommentOnly = commentOnly,
                        EditorCompileGuarded = editorCompileGuarded,
                        EditorPlayModeBlocked = IsEditorPlayModeBlocked(methodScope)
                    });
                }
            }
        }

        private static void UpdateBraceDepth(string line, int lineIndex, ref int braceDepth, ref MethodScope methodScope)
        {
            int openingBraces = CountChar(line, '\x7B');
            int closingBraces = CountChar(line, '\x7D');
            if (methodScope.Active && openingBraces > 0)
                methodScope.Opened = true;

            braceDepth += openingBraces;
            braceDepth -= closingBraces;
            if (braceDepth < 0)
                braceDepth = 0;

            if (methodScope.Active && methodScope.Opened && lineIndex > methodScope.StartLine && braceDepth <= methodScope.ParentBraceDepth)
                methodScope = default;
        }

        private static void WriteReport(List<Finding> findings)
        {
            string folder = Path.GetDirectoryName(GeologyForgeConstants.ScannerReportPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int actionableCount = 0;
            int routeActionableCount = 0;
            int coreVoxelPipelineCount = 0;
            int simulationCount = 0;
            int bootstrapCount = 0;
            int materialCloneCount = 0;
            int editorGuardedCount = 0;
            int editorPlayModeBlockedCount = 0;
            int materialReferenceCount = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                if (finding.EditorCompileGuarded)
                    editorGuardedCount++;
                if (finding.EditorPlayModeBlocked)
                    editorPlayModeBlockedCount++;
                if (finding.ApprovedCoreVoxelPipeline)
                    coreVoxelPipelineCount++;
                if (IsActionableFinding(finding))
                    actionableCount++;
                if (IsRouteActionableFinding(finding))
                    routeActionableCount++;
                if (finding.ExecutionContext == "SIMULATION_RUNTIME")
                    simulationCount++;
                if (finding.ExecutionContext == "BOOTSTRAP_RUNTIME")
                    bootstrapCount++;
                if (finding.Kind == "PROCEDURAL_MATERIAL_CLONE")
                    materialCloneCount++;
                if (finding.Kind == "MATERIAL_PROPERTY_REFERENCE")
                    materialReferenceCount++;
            }

            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_208\",\n  \"schemaVersion\": 6,\n  \"status\": \"PENDING_VERIFICATION\",\n  \"scanScope\": \"Assets/_Project/Scripts excluding Editor folders\",\n  \"runtimeMeshAllocationsEradicated\": ");
            builder.Append(actionableCount == 0 ? "true" : "false");
            builder.Append(",\n  \"findingCount\": ");
            builder.Append(findings.Count);
            builder.Append(",\n  \"actionableFindingCount\": ");
            builder.Append(actionableCount);
            builder.Append(",\n  \"geologyRouteActionableFindingCount\": ");
            builder.Append(routeActionableCount);
            builder.Append(",\n  \"approvedCoreVoxelPipelineFindingCount\": ");
            builder.Append(coreVoxelPipelineCount);
            builder.Append(",\n  \"simulationPhaseFindingCount\": ");
            builder.Append(simulationCount);
            builder.Append(",\n  \"bootstrapPhaseFindingCount\": ");
            builder.Append(bootstrapCount);
            builder.Append(",\n  \"proceduralMaterialCloneFindingCount\": ");
            builder.Append(materialCloneCount);
            builder.Append(",\n  \"editorCompileGuardedFindingCount\": ");
            builder.Append(editorGuardedCount);
            builder.Append(",\n  \"editorPlayModeBlockedFindingCount\": ");
            builder.Append(editorPlayModeBlockedCount);
            builder.Append(",\n  \"materialPropertyReferenceFindingCount\": ");
            builder.Append(materialReferenceCount);
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
                builder.Append("\", \"ownerDomain\": \"");
                builder.Append(Escape(finding.OwnerDomain));
                builder.Append("\", \"approvedCoreVoxelPipeline\": ");
                builder.Append(finding.ApprovedCoreVoxelPipeline ? "true" : "false");
                builder.Append(", \"executionContext\": \"");
                builder.Append(Escape(finding.ExecutionContext));
                builder.Append("\", \"method\": \"");
                builder.Append(Escape(finding.Method));
                builder.Append("\", \"runtimePhaseRisk\": \"");
                builder.Append(Escape(finding.RuntimePhaseRisk));
                builder.Append("\", \"commentOnly\": ");
                builder.Append(finding.CommentOnly ? "true" : "false");
                builder.Append(", \"editorCompileGuarded\": ");
                builder.Append(finding.EditorCompileGuarded ? "true" : "false");
                builder.Append(", \"editorPlayModeBlocked\": ");
                builder.Append(finding.EditorPlayModeBlocked ? "true" : "false");
                builder.Append(" }");
            }

            builder.Append("\n  ],\n  \"note\": \"Editor-only Geology Forge added. Remaining runtime topology sites require owner-specific removal, not blind cross-domain deletion. Schema v6 scans the project runtime script surface, marks owner domains, reports a separate geologyRouteActionableFindingCount, separates the approved HectonVoxelEngine core voxel pipeline from debt-to-delete findings, downgrades pure UNITY_EDITOR compile-guarded authoring helpers and ContextMenu methods that explicitly return in Play Mode, and still treats DEVELOPMENT_BUILD/runtime branches as actionable.\"\n}\n");
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
            return trimmed.StartsWith("//") ||
                   trimmed.StartsWith("/*") ||
                   trimmed.StartsWith("*") ||
                   trimmed.StartsWith("#region", System.StringComparison.Ordinal) ||
                   trimmed.StartsWith("#endregion", System.StringComparison.Ordinal);
        }

        private static bool IsActionableFinding(Finding finding)
        {
            return !finding.CommentOnly &&
                   !finding.EditorCompileGuarded &&
                   !finding.EditorPlayModeBlocked &&
                   !finding.ApprovedCoreVoxelPipeline &&
                   finding.Kind != "MATERIAL_PROPERTY_REFERENCE";
        }

        private static bool IsRouteActionableFinding(Finding finding)
        {
            return IsActionableFinding(finding) &&
                   !finding.ApprovedCoreVoxelPipeline &&
                   finding.OwnerDomain == "WORLD_TERRAIN_VOXEL";
        }

        private static bool IsApprovedCoreVoxelPipeline(string path, MethodScope methodScope, string kind, string line)
        {
            string normalized = path.Replace('\\', '/');
            if (!normalized.EndsWith("/HectonVoxelEngine.cs", System.StringComparison.Ordinal))
                return false;

            if (kind == "MARCHING_CUBES_RUNTIME_TOPOLOGY")
                return true;

            if (kind != "MESH_BUFFER_UPLOAD")
                return false;

            string method = methodScope.Active ? methodScope.MethodName : string.Empty;
            return method == "UploadSurfaceMesh" ||
                   method == "UploadColliderMesh";
        }

        private static bool LineContainsPattern(string line, string pattern)
        {
            if (pattern != ".material")
                return line.Contains(pattern);

            int searchStart = 0;
            while (searchStart < line.Length)
            {
                int index = line.IndexOf(pattern, searchStart, System.StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int next = index + pattern.Length;
                if (next >= line.Length || !IsIdentifierChar(line[next]))
                    return true;

                searchStart = next;
            }

            return false;
        }

        private static bool TryUpdateEditorOnlyPreprocessorScope(string trimmed, List<PreprocessorScope> preprocessorStack)
        {
            if (trimmed.StartsWith("#if ", System.StringComparison.Ordinal))
            {
                bool parentEditorOnly = IsEditorCompileGuarded(preprocessorStack);
                preprocessorStack.Add(new PreprocessorScope
                {
                    ParentEditorOnly = parentEditorOnly,
                    CurrentEditorOnly = parentEditorOnly || IsPureEditorCondition(trimmed.Substring(4).Trim())
                });
                return true;
            }

            if (trimmed.StartsWith("#elif", System.StringComparison.Ordinal))
            {
                if (preprocessorStack.Count > 0)
                {
                    PreprocessorScope scope = preprocessorStack[preprocessorStack.Count - 1];
                    string condition = trimmed.Length > 5 ? trimmed.Substring(5).Trim() : string.Empty;
                    scope.CurrentEditorOnly = scope.ParentEditorOnly || IsPureEditorCondition(condition);
                    preprocessorStack[preprocessorStack.Count - 1] = scope;
                }

                return true;
            }

            if (trimmed.StartsWith("#else", System.StringComparison.Ordinal))
            {
                if (preprocessorStack.Count > 0)
                {
                    PreprocessorScope scope = preprocessorStack[preprocessorStack.Count - 1];
                    scope.CurrentEditorOnly = scope.ParentEditorOnly;
                    preprocessorStack[preprocessorStack.Count - 1] = scope;
                }

                return true;
            }

            if (trimmed.StartsWith("#endif", System.StringComparison.Ordinal))
            {
                if (preprocessorStack.Count > 0)
                    preprocessorStack.RemoveAt(preprocessorStack.Count - 1);
                return true;
            }

            return false;
        }

        private static bool IsPureEditorCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            condition = condition.Trim();
            if (string.Equals(condition, "UNITY_EDITOR", System.StringComparison.Ordinal) ||
                string.Equals(condition, "defined(UNITY_EDITOR)", System.StringComparison.Ordinal) ||
                string.Equals(condition, "(UNITY_EDITOR)", System.StringComparison.Ordinal) ||
                string.Equals(condition, "(defined(UNITY_EDITOR))", System.StringComparison.Ordinal))
            {
                return true;
            }

            return condition.IndexOf("||", System.StringComparison.Ordinal) < 0 &&
                   ContainsPositiveUnityEditorSymbol(condition);
        }

        private static bool ContainsPositiveUnityEditorSymbol(string condition)
        {
            const string Symbol = "UNITY_EDITOR";
            int searchStart = 0;
            while (searchStart < condition.Length)
            {
                int index = condition.IndexOf(Symbol, searchStart, System.StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int next = index + Symbol.Length;
                if ((index == 0 || !IsIdentifierChar(condition[index - 1])) &&
                    (next >= condition.Length || !IsIdentifierChar(condition[next])) &&
                    !IsNegatedUnityEditorSymbol(condition, index))
                {
                    return true;
                }

                searchStart = next;
            }

            return false;
        }

        private static bool IsNegatedUnityEditorSymbol(string condition, int symbolIndex)
        {
            int previous = PreviousNonWhitespaceIndex(condition, symbolIndex - 1);
            if (previous >= 0 && condition[previous] == '!')
                return true;

            const string DefinedToken = "defined";
            int definedStart = condition.LastIndexOf(DefinedToken, symbolIndex, System.StringComparison.Ordinal);
            if (definedStart < 0)
                return false;

            int tokenEnd = definedStart + DefinedToken.Length;
            if ((definedStart > 0 && IsIdentifierChar(condition[definedStart - 1])) ||
                (tokenEnd < condition.Length && IsIdentifierChar(condition[tokenEnd])))
            {
                return false;
            }

            previous = PreviousNonWhitespaceIndex(condition, definedStart - 1);
            return previous >= 0 && condition[previous] == '!';
        }

        private static int PreviousNonWhitespaceIndex(string value, int start)
        {
            for (int i = start; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static bool IsEditorCompileGuarded(List<PreprocessorScope> preprocessorStack)
        {
            if (preprocessorStack == null || preprocessorStack.Count == 0)
                return false;

            return preprocessorStack[preprocessorStack.Count - 1].CurrentEditorOnly;
        }

        private static void TrackPendingContextMenuAttribute(string trimmed, bool commentOnly, ref bool pendingContextMenuAttribute)
        {
            if (commentOnly || trimmed.Length == 0)
                return;

            if (trimmed.StartsWith("[ContextMenu(", System.StringComparison.Ordinal))
                pendingContextMenuAttribute = true;
        }

        private static bool ShouldClearPendingContextMenuAttribute(string trimmed)
        {
            return trimmed.Length > 0 &&
                   !trimmed.StartsWith("[", System.StringComparison.Ordinal) &&
                   !trimmed.StartsWith("#", System.StringComparison.Ordinal);
        }

        private static void TrackPlayModeBlockedContextMenu(string trimmed, bool commentOnly, ref MethodScope methodScope)
        {
            if (!methodScope.Active || commentOnly || trimmed.Length == 0)
                return;

            if (methodScope.WaitingForApplicationPlayingReturn)
            {
                methodScope.WaitingForApplicationPlayingReturn = false;
                if (trimmed == "return;" || trimmed.StartsWith("return;", System.StringComparison.Ordinal))
                    methodScope.PlayModeBlocked = true;
            }

            if (!methodScope.ContextMenu || methodScope.PlayModeBlocked)
                return;

            if (trimmed.StartsWith("if (Application.isPlaying)", System.StringComparison.Ordinal) ||
                trimmed.StartsWith("if(Application.isPlaying)", System.StringComparison.Ordinal))
            {
                if (trimmed.IndexOf("return;", System.StringComparison.Ordinal) >= 0)
                    methodScope.PlayModeBlocked = true;
                else
                    methodScope.WaitingForApplicationPlayingReturn = true;
            }
        }

        private static bool IsEditorPlayModeBlocked(MethodScope methodScope)
        {
            return methodScope.Active && methodScope.ContextMenu && methodScope.PlayModeBlocked;
        }

        private static string ClassifyOwnerDomain(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.EndsWith("/HectonWorldGenerator.cs", System.StringComparison.Ordinal) ||
                normalized.EndsWith("/HectonVoxelEngine.cs", System.StringComparison.Ordinal) ||
                normalized.Contains("/World/") ||
                normalized.Contains("/Gameplay/Geology/") ||
                normalized.Contains("/Gameplay/Flora/"))
            {
                return "WORLD_TERRAIN_VOXEL";
            }

            if (normalized.Contains("/UI/"))
                return "UI_DIEGETIC";
            if (normalized.Contains("/VFX/"))
                return "VFX";
            if (normalized.Contains("/Core/Diagnostics/"))
                return "DIAGNOSTICS";

            return "OTHER_RUNTIME";
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
            if (trimmed.StartsWith("public ")
                || trimmed.StartsWith("private ")
                || trimmed.StartsWith("protected ")
                || trimmed.StartsWith("internal ")
                || trimmed.StartsWith("static ")
                || trimmed.StartsWith("unsafe ")
                || trimmed.StartsWith("async ")
                || trimmed.StartsWith("void "))
            {
                return true;
            }

            int paren = trimmed.IndexOf('(');
            if (paren <= 0)
                return false;

            string beforeParen = trimmed.Substring(0, paren).TrimEnd();
            int firstSpace = beforeParen.IndexOf(' ');
            if (firstSpace <= 0 || beforeParen.IndexOf('=') >= 0)
                return false;

            string firstToken = beforeParen.Substring(0, firstSpace);
            return !IsControlKeyword(firstToken) &&
                   firstToken != "yield" &&
                   firstToken != "await";
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

        private static string ClassifyPattern(string pattern, string line)
        {
            if (pattern == ".material")
                return LooksLikeRendererMaterialClone(line)
                    ? "PROCEDURAL_MATERIAL_CLONE"
                    : "MATERIAL_PROPERTY_REFERENCE";
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

        private static bool LooksLikeRendererMaterialClone(string line)
        {
            return line.IndexOf("renderer.material", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("meshRenderer.material", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Renderer.material", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("MeshRenderer.material", System.StringComparison.Ordinal) >= 0;
        }

        private static string ClassifyExecutionContext(MethodScope methodScope, bool commentOnly, bool editorCompileGuarded)
        {
            if (commentOnly)
                return "COMMENT_OR_DOC";
            if (editorCompileGuarded)
                return "EDITOR_COMPILE_GUARDED";
            if (IsEditorPlayModeBlocked(methodScope))
                return "EDITOR_CONTEXT_MENU_PLAYMODE_BLOCKED";
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

        private static string ClassifyRisk(string kind, string executionContext, bool commentOnly, bool editorCompileGuarded)
        {
            if (commentOnly)
                return "LOW_COMMENT_ONLY";
            if (editorCompileGuarded)
                return "LOW_EDITOR_ONLY";
            if (executionContext == "EDITOR_CONTEXT_MENU_PLAYMODE_BLOCKED")
                return "LOW_EDITOR_PREVIEW_ONLY";
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
            public string OwnerDomain;
            public bool ApprovedCoreVoxelPipeline;
            public string ExecutionContext;
            public string Method;
            public string RuntimePhaseRisk;
            public bool CommentOnly;
            public bool EditorCompileGuarded;
            public bool EditorPlayModeBlocked;
        }

        private struct MethodScope
        {
            public string MethodName;
            public int StartLine;
            public int ParentBraceDepth;
            public bool Active;
            public bool Opened;
            public bool ContextMenu;
            public bool PlayModeBlocked;
            public bool WaitingForApplicationPlayingReturn;
        }

        private struct PreprocessorScope
        {
            public bool ParentEditorOnly;
            public bool CurrentEditorOnly;
        }
    }
}
