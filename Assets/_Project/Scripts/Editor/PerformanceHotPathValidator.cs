// ============================================================================
// HECTON-8 - PerformanceHotPathValidator.cs
// Static audit for first-party runtime scripts.
// Flags hot-path patterns that violate project performance policy.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class PerformanceHotPathValidator
    {
        private const int MaxConsoleIssues = 80;
        private const string ScriptsRoot = "Assets/_Project/Scripts";

        private static readonly Regex _HotMethodSignatureRegex = new Regex(
            @"^\s*(?:public|private|protected|internal|static|virtual|override|sealed|unsafe|partial|\s)+[\w<>\[\],\s]+\s+(Update|LateUpdate|FixedUpdate|Tick|FixedTick|SlowTick)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex _SceneSearchRegex = new Regex(
            @"\b(?:FindFirstObjectByType|FindAnyObjectByType|FindObjectOfType|FindObjectsOfType|FindObjectsByType|GameObject\.Find|FindWithTag)\b",
            RegexOptions.Compiled);

        private static readonly Regex _GetComponentRegex = new Regex(
            @"\bGetComponent\s*<",
            RegexOptions.Compiled);

        private static readonly Regex _CoroutineRegex = new Regex(
            @"\bStartCoroutine\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex _WaitInstructionRegex = new Regex(
            @"\b(?:WaitForSeconds|WaitForSecondsRealtime)\b",
            RegexOptions.Compiled);

        private static readonly Regex _ManagedAllocationRegex = new Regex(
            @"\bnew\s+(?:List\s*<|Dictionary\s*<|HashSet\s*<|StringBuilder\b|CancellationTokenSource\b)",
            RegexOptions.Compiled);

        private static readonly Regex _ArrayAllocationRegex = new Regex(
            @"\bnew\s+[A-Za-z_][A-Za-z0-9_<>\.\[\]]*\s*\[",
            RegexOptions.Compiled);

        private static readonly Regex _CapacityGrowthContextRegex = new Regex(
            @"(?:\.Length|\.Capacity|currentCapacity)\s*<|Ensure buffer is large enough|newSize\s*=",
            RegexOptions.Compiled);

        private static readonly Regex _StringInterpolationRegex = new Regex(
            "\\$\"",
            RegexOptions.Compiled);

        private static readonly Regex _ToStringRegex = new Regex(
            @"\.ToString\s*\(",
            RegexOptions.Compiled);

        private static readonly string[] _AllowedNativeUpdatePaths =
        {
            "Assets/_Project/Scripts/GameTickManager.cs",
            "Assets/_Project/Scripts/HectonAtmosphereManager.cs",
            "Assets/_Project/Scripts/HectonCelestialEngine.cs",
            "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs",
            "Assets/_Project/Scripts/HUDNotification.cs",
            "Assets/_Project/Scripts/HUDQuickBar.cs",
            "Assets/_Project/Scripts/HectonSuitHUD_v4.cs",
            "Assets/_Project/Scripts/HectonSuitHUDExtensions.cs",
            "Assets/_Project/Scripts/RuntimePerformanceProfiler.cs",
            "Assets/_Project/Scripts/SkySystemFollowCamera.cs",
            "Assets/_Project/Scripts/PlayerThrusterAudio.cs",
            "Assets/_Project/Scripts/BuoyancyObject.cs",
            "Assets/_Project/Scripts/Visor/VisorHUDController.cs",
            "Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs",
            "Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs",
            "Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs",
            "Assets/_Project/Scripts/UI/PDABarterTab.cs",
            "Assets/_Project/Scripts/UI/PDAConstructionTab.cs",
            "Assets/_Project/Scripts/UI/PDAShellChrome.cs",
            "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs"
        };

        private static readonly string[] _ExcludedNameFragments =
        {
            "SmokeTester",
            "RuntimeSmoke",
            " - Old - deprecated - ",
            "DemoFirstPersonController",
            "FabricationRuntimeSmokeTester",
            "ToolLoadoutProvisioner",
            "ToolStagingSpawner"
        };

        private static readonly HashSet<string> _AllowedNativeUpdates =
            new HashSet<string>(_AllowedNativeUpdatePaths, StringComparer.OrdinalIgnoreCase);

        [MenuItem("Hecton/Validation/Validate Performance Hot Paths")]
        private static void Validate()
        {
            if (!Directory.Exists(ScriptsRoot))
            {
                Debug.LogWarning($"[PerformanceValidation] Scripts root not found: {ScriptsRoot}");
                return;
            }

            string[] filePaths = Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
            List<PerformanceIssue> issues = new List<PerformanceIssue>(256);

            for (int i = 0; i < filePaths.Length; i++)
            {
                string assetPath = NormalizeAssetPath(filePaths[i]);
                if (ShouldSkipFile(assetPath))
                    continue;

                ScanFile(assetPath, issues);
            }

            EmitReport(issues);
        }

        private static void ScanFile(string assetPath, List<PerformanceIssue> issues)
        {
            string[] lines = File.ReadAllLines(assetPath);
            string activeMethod = null;
            int activeMethodLine = -1;
            int activeBraceDepth = 0;
            string pendingMethod = null;
            int pendingMethodLine = -1;
            int editorConditionalDepth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal))
                {
                    editorConditionalDepth++;
                    continue;
                }

                if (trimmed.StartsWith("#endif", StringComparison.Ordinal) && editorConditionalDepth > 0)
                {
                    editorConditionalDepth--;
                    continue;
                }

                if (editorConditionalDepth > 0)
                    continue;

                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (activeMethod == null && pendingMethod == null)
                {
                    Match match = _HotMethodSignatureRegex.Match(line);
                    if (match.Success)
                    {
                        pendingMethod = match.Groups[1].Value;
                        pendingMethodLine = i + 1;
                    }
                }

                if (pendingMethod != null && activeMethod == null)
                {
                    int openCount = CountChar(line, '{');
                    int closeCount = CountChar(line, '}');
                    if (openCount > 0)
                    {
                        activeMethod = pendingMethod;
                        activeMethodLine = pendingMethodLine;
                        activeBraceDepth = 0;
                        pendingMethod = null;
                        pendingMethodLine = -1;

                        if (IsNativeUpdateMethod(activeMethod) && !_AllowedNativeUpdates.Contains(assetPath))
                        {
                            issues.Add(new PerformanceIssue(
                                assetPath,
                                activeMethodLine,
                                "NATIVE_UPDATE",
                                $"{activeMethod}() should be reviewed for ITickable migration or explicit exception."));
                        }
                    }
                }

                if (activeMethod == null)
                    continue;

                ScanHotPathLine(assetPath, lines, i, activeMethod, issues);

                activeBraceDepth += CountChar(line, '{');
                activeBraceDepth -= CountChar(line, '}');
                if (activeBraceDepth <= 0)
                {
                    activeMethod = null;
                    activeMethodLine = -1;
                    activeBraceDepth = 0;
                }
            }
        }

        private static void ScanHotPathLine(
            string assetPath,
            string[] lines,
            int lineIndex,
            string methodName,
            List<PerformanceIssue> issues)
        {
            int lineNumber = lineIndex + 1;
            string line = lines[lineIndex];

            if (_SceneSearchRegex.IsMatch(line))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "SCENE_SEARCH",
                    $"{methodName}() performs a scene-wide lookup."));
            }

            if (_GetComponentRegex.IsMatch(line))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "GET_COMPONENT",
                    $"{methodName}() performs GetComponent instead of cached access."));
            }

            if (_CoroutineRegex.IsMatch(line))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "COROUTINE",
                    $"{methodName}() starts a coroutine from a hot path."));
            }

            if (_WaitInstructionRegex.IsMatch(line))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "WAIT_INSTRUCTION",
                    $"{methodName}() references wait instructions that allocate."));
            }

            if ((_ManagedAllocationRegex.IsMatch(line) || _ArrayAllocationRegex.IsMatch(line)) &&
                !IsCapacityGrowthAllocation(lines, lineIndex))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "MANAGED_ALLOCATION",
                    $"{methodName}() allocates managed containers or arrays."));
            }

            if (_StringInterpolationRegex.IsMatch(line) || _ToStringRegex.IsMatch(line))
            {
                issues.Add(new PerformanceIssue(
                    assetPath,
                    lineNumber,
                    "STRING_ALLOCATION",
                    $"{methodName}() formats strings in a hot path."));
            }
        }

        private static bool IsCapacityGrowthAllocation(string[] lines, int lineIndex)
        {
            int start = Mathf.Max(0, lineIndex - 4);
            int end = Mathf.Min(lines.Length - 1, lineIndex + 1);
            for (int i = start; i <= end; i++)
            {
                if (_CapacityGrowthContextRegex.IsMatch(lines[i]))
                    return true;
            }

            return false;
        }

        private static void EmitReport(List<PerformanceIssue> issues)
        {
            if (issues.Count == 0)
            {
                Debug.Log("[PerformanceValidation] PASS no hot-path violations found.");
                return;
            }

            issues.Sort(static (a, b) =>
            {
                int pathCompare = string.CompareOrdinal(a.AssetPath, b.AssetPath);
                if (pathCompare != 0)
                    return pathCompare;

                int lineCompare = a.LineNumber.CompareTo(b.LineNumber);
                if (lineCompare != 0)
                    return lineCompare;

                return string.CompareOrdinal(a.RuleId, b.RuleId);
            });

            Dictionary<string, int> countsByRule = new Dictionary<string, int>(StringComparer.Ordinal);
            int consoleCount = Mathf.Min(MaxConsoleIssues, issues.Count);
            for (int i = 0; i < issues.Count; i++)
            {
                PerformanceIssue issue = issues[i];
                if (countsByRule.TryGetValue(issue.RuleId, out int existing))
                    countsByRule[issue.RuleId] = existing + 1;
                else
                    countsByRule.Add(issue.RuleId, 1);

                if (i >= consoleCount)
                    continue;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(issue.AssetPath);
                Debug.LogWarning(
                    $"[PerformanceValidation] {issue.RuleId} {issue.AssetPath}:{issue.LineNumber} - {issue.Message}",
                    script);
            }

            StringBuilder summaryBuilder = new StringBuilder(512);
            summaryBuilder.Append("[PerformanceValidation] COMPLETE issues=")
                .Append(issues.Count)
                .Append(" files=")
                .Append(CountUniqueFiles(issues))
                .Append(" rules=");

            bool firstRule = true;
            foreach (KeyValuePair<string, int> pair in countsByRule)
            {
                if (!firstRule)
                    summaryBuilder.Append(", ");

                summaryBuilder.Append(pair.Key).Append('=').Append(pair.Value);
                firstRule = false;
            }

            if (issues.Count > consoleCount)
            {
                summaryBuilder.Append(" (console capped at ")
                    .Append(consoleCount)
                    .Append(')');
            }

            Debug.LogWarning(summaryBuilder.ToString());
        }

        private static bool ShouldSkipFile(string assetPath)
        {
            if (assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            for (int i = 0; i < _ExcludedNameFragments.Length; i++)
            {
                if (assetPath.IndexOf(_ExcludedNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool IsNativeUpdateMethod(string methodName)
        {
            return string.Equals(methodName, "Update", StringComparison.Ordinal) ||
                   string.Equals(methodName, "LateUpdate", StringComparison.Ordinal) ||
                   string.Equals(methodName, "FixedUpdate", StringComparison.Ordinal);
        }

        private static int CountUniqueFiles(List<PerformanceIssue> issues)
        {
            HashSet<string> uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < issues.Count; i++)
                uniqueFiles.Add(issues[i].AssetPath);

            return uniqueFiles.Count;
        }

        private static int CountChar(string line, char value)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == value)
                    count++;
            }

            return count;
        }

        private static string NormalizeAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int assetsIndex = normalized.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex) : normalized;
        }

        private readonly struct PerformanceIssue
        {
            public PerformanceIssue(string assetPath, int lineNumber, string ruleId, string message)
            {
                AssetPath = assetPath;
                LineNumber = lineNumber;
                RuleId = ruleId;
                Message = message;
            }

            public string AssetPath { get; }
            public int LineNumber { get; }
            public string RuleId { get; }
            public string Message { get; }
        }
    }
}
