#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    /// <summary>
    /// Release-player gate that blocks static-data text/config parser residue outside Editor/Development lanes.
    /// </summary>
    internal sealed class H8DataMonolithReleaseBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -9090;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = report != null && (report.summary.options & BuildOptions.Development) != 0;
            BuildTarget target = report != null ? report.summary.platform : EditorUserBuildSettings.activeBuildTarget;
            H8DataMonolithReleaseParserScanner.Scan(writeReport: true, blockOnFindings: !development, developmentBuild: development, target: target);
        }
    }

    internal static class H8DataMonolithReleaseParserScanner
    {
        private const string AgentId = "X_002";
        private const string AgentId1313 = "1313";
        private const string RuntimeRoot = "Assets/_Project/Scripts";
        private const string ReleaseReportPath = "Docs/Reports/DATA_MONOLITH_RELEASE_BUILD_GATE_X_002.json";
        private const string DevelopmentReportPath = "Docs/Reports/DATA_MONOLITH_DEVELOPMENT_BUILD_GATE_X_002.json";
        private const string ReleaseReportPath1313 = "Docs/Reports/DATA_MONOLITH_RELEASE_BUILD_GATE_1313.json";
        private const string DevelopmentReportPath1313 = "Docs/Reports/DATA_MONOLITH_DEVELOPMENT_BUILD_GATE_1313.json";
        private const int MaxFindingsWritten = 256;

        [MenuItem("Hecton8/Data Monolith/Run Release Parser Build Gate")]
        private static void RunFromMenu()
        {
            Scan(writeReport: true, blockOnFindings: false, developmentBuild: false, target: EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("Hecton8/Data Monolith/Run Development Parser Warning Gate")]
        private static void RunDevelopmentFromMenu()
        {
            Scan(writeReport: true, blockOnFindings: false, developmentBuild: true, target: EditorUserBuildSettings.activeBuildTarget);
        }

        internal static ScanResult Scan(bool writeReport, bool blockOnFindings, bool developmentBuild)
        {
            return Scan(writeReport, blockOnFindings, developmentBuild, EditorUserBuildSettings.activeBuildTarget);
        }

        internal static ScanResult Scan(bool writeReport, bool blockOnFindings, bool developmentBuild, BuildTarget target)
        {
            string projectRoot = ResolveProjectRoot();
            ScanResult result = default;
            result.DevelopmentBuild = developmentBuild;
            result.TargetName = target.ToString();
            result.TargetHasNativeMonolithPal = IsSupportedProductionMonolithTarget(target);
            StringBuilder findingsJson = new StringBuilder(32768);

            string root = Path.Combine(projectRoot, RuntimeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                result.MissingRoots = 1;
            }
            else
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                    ScanFile(projectRoot, files[i], target, ref result, findingsJson);
            }

            if (!developmentBuild && !result.TargetHasNativeMonolithPal)
            {
                result.UnsupportedPlatformPalFindingCount++;
                AppendFinding(
                    findingsJson,
                    ref result,
                    "BUILD_TARGET:" + result.TargetName,
                    0,
                    "unsupportedStaticDataMonolithPlatformPal",
                    "Production target has no zero-GC static_data.h8bin native/PAL loader; current non-Windows runtime branch fails closed.");
            }

            result.Status = result.BlockingFindingCount == 0
                ? (developmentBuild ? "PASS_DEVELOPMENT_PARSER_GATE" : "PASS_RELEASE_PARSER_GATE")
                : developmentBuild
                    ? "WARN_DEVELOPMENT_BUILD_PARSER_RESIDUE"
                    : "FAIL_RELEASE_PARSER_GATE_BLOCKED";

            string reportPath = GetReportPath(developmentBuild);
            if (writeReport)
            {
                WriteText(Path.Combine(projectRoot, reportPath), BuildReport(in result, findingsJson, AgentId));
                WriteText(Path.Combine(projectRoot, GetReportPath1313(developmentBuild)), BuildReport(in result, findingsJson, AgentId1313));
            }

            if (blockOnFindings && result.BlockingFindingCount > 0)
            {
                throw new BuildFailedException(
                    "[H8DataMonolithReleaseBuildGate] Release build blocked: " +
                    result.BlockingFindingCount +
                    " production static-data parser/file-IO/platform-PAL findings. Report: " +
                    reportPath);
            }

            if (result.BlockingFindingCount > 0)
            {
                Debug.LogWarning(
                    "[H8DataMonolithReleaseBuildGate] Production gate findings=" +
                    result.BlockingFindingCount +
                    " report=" +
                    reportPath);
            }

            return result;
        }

        private static bool IsSupportedProductionMonolithTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows ||
                   target == BuildTarget.StandaloneWindows64;
        }

        private static void ScanFile(string projectRoot, string absolutePath, BuildTarget target, ref ScanResult result, StringBuilder findingsJson)
        {
            string relativePath = MakeRelative(projectRoot, absolutePath);
            result.FilesScanned++;

            if (IsEditorOrTestPath(relativePath))
            {
                result.EditorOrTestFilesSkipped++;
                return;
            }

            result.ProductionFilesScanned++;
            bool allowedRuntimePersistence = IsAllowedRuntimePersistencePath(relativePath);
            string[] lines;
            try
            {
                lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                AppendFinding(findingsJson, ref result, relativePath, 0, "sourceReadFailure", exception.GetType().Name);
                return;
            }

            List<PreprocessorFrame> preprocessor = new List<PreprocessorFrame>(8);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string sourceLine = StripLineComment(lines[lineIndex]);
                string line = StripStringLiterals(sourceLine);
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    ApplyPreprocessorDirective(trimmed, preprocessor, result.DevelopmentBuild, target);
                    continue;
                }

                if (!IsPlayerLineActive(preprocessor))
                {
                    result.ReleaseInactiveLinesSkipped++;
                    continue;
                }

                string kind = ClassifyBlockingLine(line);
                if (string.IsNullOrEmpty(kind))
                    continue;

                if (allowedRuntimePersistence)
                {
                    result.AllowedPersistenceFindingCount++;
                    continue;
                }

                AppendFinding(findingsJson, ref result, relativePath, lineIndex + 1, kind, sourceLine.Trim());
            }
        }

        private static string ClassifyBlockingLine(string line)
        {
            if (IsFileStreamReadByteLine(line))
                return "fileStreamReadByte";

            if (line.IndexOf("ReadAllText", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("ReadAllLines", StringComparison.Ordinal) >= 0)
            {
                return "managedTextFileRead";
            }

            if (line.IndexOf("ReadAllBytes", StringComparison.Ordinal) >= 0)
                return "managedWholeFileByteRead";

            if (line.IndexOf("StreamReader", StringComparison.Ordinal) >= 0)
                return "managedTextStreamReader";

            if (line.IndexOf(".Split(", StringComparison.Ordinal) >= 0 ||
                line.IndexOf(" string.Split(", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("= Split(", StringComparison.Ordinal) >= 0)
            {
                return "managedStringSplit";
            }

            if (HasStaticDataParserContext(line) &&
                (line.IndexOf(".Parse(", StringComparison.Ordinal) >= 0 ||
                 line.IndexOf(".TryParse(", StringComparison.Ordinal) >= 0))
            {
                return "managedScalarParse";
            }

            if (line.IndexOf("JsonUtility.FromJson", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("JsonConvert.DeserializeObject", StringComparison.Ordinal) >= 0)
            {
                return "managedJsonDeserialize";
            }

            if (IsCsvParserRouteLine(line))
            {
                return "csvParserRoute";
            }

            return string.Empty;
        }

        private static bool HasStaticDataParserContext(string line)
        {
            return line.IndexOf("Csv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf(".csv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf(".json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Balance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Tuning", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCsvParserRouteLine(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            bool hasCsv = line.IndexOf("Csv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          line.IndexOf(".csv", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasCsv)
                return false;

            bool hasCallable = line.IndexOf("(", StringComparison.Ordinal) >= 0;
            if (hasCallable &&
                (line.IndexOf("Parse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("TryApply", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("TryIngest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("TryLoad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("TryReload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("Reload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            bool declaresIngestor =
                line.IndexOf("CsvIngestor", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (line.IndexOf(" class ", StringComparison.Ordinal) >= 0 ||
                 line.IndexOf(" struct ", StringComparison.Ordinal) >= 0);

            return declaresIngestor;
        }

        private static bool IsFileStreamReadByteLine(string line)
        {
            int readIndex = line.IndexOf(".ReadByte(", StringComparison.Ordinal);
            if (readIndex < 0)
                return false;

            return line.IndexOf("stream", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("FileStream", StringComparison.Ordinal) >= 0;
        }

        private static void ApplyPreprocessorDirective(string trimmed, List<PreprocessorFrame> stack, bool developmentBuild, BuildTarget target)
        {
            if (trimmed.StartsWith("#if", StringComparison.Ordinal))
            {
                string expression = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                stack.Add(new PreprocessorFrame(EvaluateForPlayer(expression, developmentBuild, target)));
                return;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                    return;

                PreprocessorFrame frame = stack[stack.Count - 1];
                if (frame.BranchTaken)
                {
                    frame.CurrentActive = false;
                }
                else
                {
                    string expression = trimmed.Length > 5 ? trimmed.Substring(5).Trim() : string.Empty;
                    bool active = EvaluateForPlayer(expression, developmentBuild, target);
                    frame.CurrentActive = active;
                    frame.BranchTaken = active;
                }

                stack[stack.Count - 1] = frame;
                return;
            }

            if (trimmed.StartsWith("#else", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                    return;

                PreprocessorFrame frame = stack[stack.Count - 1];
                frame.CurrentActive = !frame.BranchTaken;
                frame.BranchTaken = true;
                stack[stack.Count - 1] = frame;
                return;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal) && stack.Count > 0)
                stack.RemoveAt(stack.Count - 1);
        }

        private static bool IsPlayerLineActive(List<PreprocessorFrame> stack)
        {
            for (int i = 0; i < stack.Count; i++)
                if (!stack[i].CurrentActive)
                    return false;

            return true;
        }

        private static bool EvaluateForPlayer(string expression, bool developmentBuild, BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            PreprocessorExpressionParser parser = new PreprocessorExpressionParser(expression, developmentBuild, target);
            return parser.ParseExpression();
        }

        private static void AppendFinding(
            StringBuilder builder,
            ref ScanResult result,
            string path,
            int line,
            string kind,
            string source)
        {
            result.BlockingFindingCount++;
            if (result.WrittenFindingCount >= MaxFindingsWritten)
                return;

            if (builder.Length > 0)
                builder.AppendLine(",");

            builder.Append("    { \"kind\": \"").Append(Escape(kind))
                .Append("\", \"path\": \"").Append(Escape(path))
                .Append("\", \"line\": ").Append(line)
                .Append(", \"source\": \"").Append(Escape(Trim(source))).Append("\" }");
            result.WrittenFindingCount++;
        }

        private static string BuildReport(in ScanResult result, StringBuilder findingsJson, string agentId)
        {
            StringBuilder report = new StringBuilder(65536);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_MONOLITH_RELEASE_BUILD_GATE_V1\",");
            report.AppendLine("  \"agent\": \"" + agentId + "\",");
            report.AppendLine("  \"scanner\": \"H8DataMonolithReleaseParserScanner\",");
            report.AppendLine("  \"status\": \"" + result.Status + "\",");
            report.AppendLine("  \"policy\": \"Non-editor player builds are scanned with the matching DEVELOPMENT_BUILD symbol. Non-development players are blocked on production static-data parser routes and on targets without a zero-GC native/PAL static_data.h8bin loader; development players emit warning evidence for editor-only CSV policy enforcement.\",");
            report.AppendLine("  \"developmentBuild\": " + LowerBool(result.DevelopmentBuild) + ",");
            report.AppendLine("  \"buildTarget\": \"" + Escape(result.TargetName) + "\",");
            report.AppendLine("  \"targetHasNativeMonolithPal\": " + LowerBool(result.TargetHasNativeMonolithPal) + ",");
            report.AppendLine("  \"platformPalStatus\": \"" + Escape(GetPlatformPalStatus(in result)) + "\",");
            report.AppendLine("  \"symbolModel\": \"UNITY_EDITOR=false, DEVELOPMENT_BUILD=" + LowerBool(result.DevelopmentBuild) + ", DEBUG=" + LowerBool(result.DevelopmentBuild) + ", platform_symbols=BuildTarget, unknown_symbols=true\",");
            report.AppendLine("  \"filesScanned\": " + result.FilesScanned + ",");
            report.AppendLine("  \"productionFilesScanned\": " + result.ProductionFilesScanned + ",");
            report.AppendLine("  \"editorOrTestFilesSkipped\": " + result.EditorOrTestFilesSkipped + ",");
            report.AppendLine("  \"releaseInactiveLinesSkipped\": " + result.ReleaseInactiveLinesSkipped + ",");
            report.AppendLine("  \"missingRoots\": " + result.MissingRoots + ",");
            report.AppendLine("  \"blockingFindingCount\": " + result.BlockingFindingCount + ",");
            report.AppendLine("  \"unsupportedPlatformPalFindingCount\": " + result.UnsupportedPlatformPalFindingCount + ",");
            report.AppendLine("  \"allowedPersistenceFindingCount\": " + result.AllowedPersistenceFindingCount + ",");
            report.AppendLine("  \"writtenFindingLimit\": " + MaxFindingsWritten + ",");
            report.AppendLine("  \"writtenFindingCount\": " + result.WrittenFindingCount + ",");
            report.AppendLine("  \"proofBoundary\": \"Editor-only static source gate. It proves a release build cannot pass this preprocessor while listed production parser routes remain. It is not a Unity player profiler or runtime GC capture.\",");
            report.AppendLine("  \"findings\": [");
            report.Append(findingsJson);
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            return report.ToString();
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo directory = Directory.GetParent(Application.dataPath);
            return directory == null ? Directory.GetCurrentDirectory() : directory.FullName;
        }

        private static string GetReportPath(bool developmentBuild)
        {
            return developmentBuild ? DevelopmentReportPath : ReleaseReportPath;
        }

        private static string GetReportPath1313(bool developmentBuild)
        {
            return developmentBuild ? DevelopmentReportPath1313 : ReleaseReportPath1313;
        }

        private static string GetPlatformPalStatus(in ScanResult result)
        {
            if (result.TargetHasNativeMonolithPal)
                return "NATIVE_MONOLITH_PAL_PRESENT";

            return result.DevelopmentBuild
                ? "DEVELOPMENT_TARGET_NOT_RELEASE_PROOF"
                : "FAIL_NO_NATIVE_MONOLITH_PAL";
        }

        private static bool IsEditorOrTestPath(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/EditorValidation/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedRuntimePersistencePath(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return string.Equals(normalized, "Assets/_Project/Scripts/SaveThumbnailSystem.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/Core/RebindingManager.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/Input/UserOptionsPersistence.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/Meta/GlobalProfileManager.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/ModdingAPI/ModLoader.cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string StripLineComment(string line)
        {
            bool inString = false;
            bool verbatim = false;
            for (int i = 0; i < line.Length - 1; i++)
            {
                char c = line[i];
                if (!inString && c == '@' && line[i + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    i++;
                    continue;
                }

                if (c == '"' && (i == 0 || line[i - 1] != '\\' || verbatim))
                {
                    if (inString && verbatim && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inString = !inString;
                    if (!inString)
                        verbatim = false;
                    continue;
                }

                if (!inString && c == '/' && line[i + 1] == '/')
                    return line.Substring(0, i);
            }

            return line;
        }

        private static string StripStringLiterals(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            StringBuilder builder = new StringBuilder(line.Length);
            bool inString = false;
            bool verbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inString && c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '"' && (i == 0 || line[i - 1] != '\\' || verbatim))
                {
                    if (inString && verbatim && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        i++;
                        continue;
                    }

                    inString = !inString;
                    if (!inString)
                        verbatim = false;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(inString ? ' ' : c);
            }

            return builder.ToString();
        }

        private static string MakeRelative(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(path);
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                int start = normalizedRoot.Length;
                if (start < normalizedPath.Length &&
                    (normalizedPath[start] == Path.DirectorySeparatorChar || normalizedPath[start] == Path.AltDirectorySeparatorChar))
                {
                    start++;
                }

                return normalizedPath.Substring(start).Replace('\\', '/');
            }

            return path.Replace('\\', '/');
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string Trim(string value)
        {
            string trimmed = value.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            return trimmed.Length <= 180 ? trimmed : trimmed.Substring(0, 180);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string LowerBool(bool value)
        {
            return value ? "true" : "false";
        }

        internal struct ScanResult
        {
            public string Status;
            public string TargetName;
            public bool DevelopmentBuild;
            public bool TargetHasNativeMonolithPal;
            public int FilesScanned;
            public int ProductionFilesScanned;
            public int EditorOrTestFilesSkipped;
            public int ReleaseInactiveLinesSkipped;
            public int MissingRoots;
            public int BlockingFindingCount;
            public int UnsupportedPlatformPalFindingCount;
            public int WrittenFindingCount;
            public int AllowedPersistenceFindingCount;
        }

        private struct PreprocessorFrame
        {
            public bool CurrentActive;
            public bool BranchTaken;

            public PreprocessorFrame(bool active)
            {
                CurrentActive = active;
                BranchTaken = active;
            }
        }

        private struct PreprocessorExpressionParser
        {
            private readonly string _text;
            private readonly bool _developmentBuild;
            private readonly BuildTarget _target;
            private int _index;

            public PreprocessorExpressionParser(string text, bool developmentBuild, BuildTarget target)
            {
                _text = text;
                _developmentBuild = developmentBuild;
                _target = target;
                _index = 0;
            }

            public bool ParseExpression()
            {
                bool value = ParseOr();
                return value;
            }

            private bool ParseOr()
            {
                bool value = ParseAnd();
                while (true)
                {
                    SkipWhite();
                    if (!TryConsume("||"))
                        return value;

                    bool right = ParseAnd();
                    value = value || right;
                }
            }

            private bool ParseAnd()
            {
                bool value = ParseUnary();
                while (true)
                {
                    SkipWhite();
                    if (!TryConsume("&&"))
                        return value;

                    bool right = ParseUnary();
                    value = value && right;
                }
            }

            private bool ParseUnary()
            {
                SkipWhite();
                if (TryConsume("!"))
                    return !ParseUnary();

                if (TryConsume("("))
                {
                    bool value = ParseOr();
                    SkipWhite();
                    TryConsume(")");
                    return value;
                }

                string symbol = ReadSymbol();
                return SymbolValueForPlayer(symbol);
            }

            private string ReadSymbol()
            {
                SkipWhite();
                int start = _index;
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (!char.IsLetterOrDigit(c) && c != '_')
                        break;
                    _index++;
                }

                return start == _index ? string.Empty : _text.Substring(start, _index - start);
            }

            private bool TryConsume(string token)
            {
                if (_index + token.Length > _text.Length)
                    return false;

                for (int i = 0; i < token.Length; i++)
                    if (_text[_index + i] != token[i])
                        return false;

                _index += token.Length;
                return true;
            }

            private void SkipWhite()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                    _index++;
            }

            private bool SymbolValueForPlayer(string symbol)
            {
                if (string.Equals(symbol, "UNITY_EDITOR", StringComparison.Ordinal))
                    return false;

                if (symbol.StartsWith("UNITY_EDITOR_", StringComparison.Ordinal))
                    return false;

                if (string.Equals(symbol, "DEVELOPMENT_BUILD", StringComparison.Ordinal) ||
                    string.Equals(symbol, "DEBUG", StringComparison.Ordinal))
                    return _developmentBuild;

                if (string.Equals(symbol, "UNITY_ANDROID", StringComparison.Ordinal))
                    return _target == BuildTarget.Android;

                if (string.Equals(symbol, "UNITY_IOS", StringComparison.Ordinal) ||
                    string.Equals(symbol, "UNITY_IPHONE", StringComparison.Ordinal))
                    return _target == BuildTarget.iOS;

                if (string.Equals(symbol, "UNITY_WEBGL", StringComparison.Ordinal))
                    return _target == BuildTarget.WebGL;

                if (string.Equals(symbol, "UNITY_STANDALONE_WIN", StringComparison.Ordinal))
                    return _target == BuildTarget.StandaloneWindows ||
                           _target == BuildTarget.StandaloneWindows64;

                if (string.Equals(symbol, "UNITY_STANDALONE_LINUX", StringComparison.Ordinal))
                    return _target == BuildTarget.StandaloneLinux64;

                if (string.Equals(symbol, "UNITY_STANDALONE_OSX", StringComparison.Ordinal))
                    return _target == BuildTarget.StandaloneOSX;

                if (string.Equals(symbol, "UNITY_STANDALONE", StringComparison.Ordinal))
                    return _target == BuildTarget.StandaloneWindows ||
                           _target == BuildTarget.StandaloneWindows64 ||
                           _target == BuildTarget.StandaloneLinux64 ||
                           _target == BuildTarget.StandaloneOSX;

                if (string.Equals(symbol, "true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(symbol, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(symbol))
                {
                    return false;
                }

                return true;
            }
        }
    }
}
#endif
