using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hecton8.Tools.DataMonolithBakeCli
{
    internal static class DataMonolithPlayerParserAbsenceProbe
    {
        private const string AgentId = "X_002";
        private const string RuntimeRoot = "Assets/_Project/Scripts";
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_PLAYER_PARSER_ABSENCE_CLI_X_002.json";
        private const int MaxFindings = 256;

        public static bool Run(string projectRoot)
        {
            ScanMode release = Scan(projectRoot, developmentBuild: false);
            ScanMode development = Scan(projectRoot, developmentBuild: true);
            bool passed = release.BlockingFindingCount == 0 &&
                          development.BlockingFindingCount == 0 &&
                          release.DirectFileStreamReadByteCount == 0 &&
                          development.DirectFileStreamReadByteCount == 0;

            string reportPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, BuildReport(passed, release, development), Encoding.UTF8);
            return passed;
        }

        private static ScanMode Scan(string projectRoot, bool developmentBuild)
        {
            ScanMode mode = new ScanMode(developmentBuild);
            string root = Path.Combine(projectRoot, RuntimeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                mode.MissingRoots = 1;
                mode.Status = "FAIL_MISSING_RUNTIME_ROOT";
                return mode;
            }

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                ScanFile(projectRoot, files[i], ref mode);

            mode.Status = mode.BlockingFindingCount == 0 && mode.DirectFileStreamReadByteCount == 0
                ? developmentBuild ? "PASS_DEVELOPMENT_PLAYER_PARSER_ABSENCE" : "PASS_RELEASE_PLAYER_PARSER_ABSENCE"
                : developmentBuild ? "FAIL_DEVELOPMENT_PLAYER_PARSER_RESIDUE" : "FAIL_RELEASE_PLAYER_PARSER_RESIDUE";
            return mode;
        }

        private static void ScanFile(string projectRoot, string absolutePath, ref ScanMode mode)
        {
            string relativePath = MakeRelative(projectRoot, absolutePath);
            mode.FilesScanned++;
            if (IsEditorOrTestPath(relativePath))
            {
                mode.EditorOrTestFilesSkipped++;
                return;
            }

            mode.ProductionFilesScanned++;
            bool allowedPersistencePath = IsAllowedRuntimePersistencePath(relativePath);
            string[] lines;
            try
            {
                lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            }
            catch (IOException exception)
            {
                AddFinding(ref mode, relativePath, 0, "sourceReadFailure", exception.GetType().Name, allowed: false);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                AddFinding(ref mode, relativePath, 0, "sourceReadFailure", exception.GetType().Name, allowed: false);
                return;
            }
            catch (ArgumentException exception)
            {
                AddFinding(ref mode, relativePath, 0, "sourceReadFailure", exception.GetType().Name, allowed: false);
                return;
            }
            catch (NotSupportedException exception)
            {
                AddFinding(ref mode, relativePath, 0, "sourceReadFailure", exception.GetType().Name, allowed: false);
                return;
            }

            List<PreprocessorFrame> preprocessor = new List<PreprocessorFrame>(8);
            for (int i = 0; i < lines.Length; i++)
            {
                string sourceLine = StripLineComment(lines[i]);
                string line = StripStringLiterals(sourceLine);
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    ApplyPreprocessorDirective(trimmed, preprocessor, mode.DevelopmentBuild);
                    continue;
                }

                if (!IsPlayerLineActive(preprocessor))
                {
                    mode.InactiveLinesSkipped++;
                    continue;
                }

                string kind = ClassifyLine(line);
                if (string.IsNullOrEmpty(kind))
                    continue;

                if (string.Equals(kind, "fileStreamReadByte", StringComparison.Ordinal))
                    mode.DirectFileStreamReadByteCount++;

                if (allowedPersistencePath && !string.Equals(kind, "fileStreamReadByte", StringComparison.Ordinal))
                {
                    AddFinding(ref mode, relativePath, i + 1, kind, sourceLine.Trim(), allowed: true);
                    continue;
                }

                AddFinding(ref mode, relativePath, i + 1, kind, sourceLine.Trim(), allowed: false);
            }
        }

        private static string ClassifyLine(string line)
        {
            if (IsFileStreamReadByteLine(line))
                return "fileStreamReadByte";

            if (line.IndexOf("ReadAllText", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("ReadAllLines", StringComparison.Ordinal) >= 0)
            {
                return "managedTextFileRead";
            }

            if (line.IndexOf("StreamReader", StringComparison.Ordinal) >= 0)
                return "managedTextStreamReader";

            if (line.IndexOf("JsonUtility.FromJson", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("JsonConvert.DeserializeObject", StringComparison.Ordinal) >= 0)
            {
                return "managedJsonDeserialize";
            }

            if (IsCsvParserRouteLine(line))
                return "csvParserRoute";

            if (line.IndexOf(".Split(", StringComparison.Ordinal) >= 0 ||
                line.IndexOf(" string.Split(", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("= Split(", StringComparison.Ordinal) >= 0)
            {
                return HasStaticConfigContext(line) ? "managedStringSplitConfig" : string.Empty;
            }

            if (HasStaticConfigContext(line) &&
                (line.IndexOf(".Parse(", StringComparison.Ordinal) >= 0 ||
                 line.IndexOf(".TryParse(", StringComparison.Ordinal) >= 0))
            {
                return "managedScalarParseConfig";
            }

            return string.Empty;
        }

        private static bool IsFileStreamReadByteLine(string line)
        {
            int readIndex = line.IndexOf(".ReadByte(", StringComparison.Ordinal);
            if (readIndex < 0)
                return false;

            return line.IndexOf("stream", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("FileStream", StringComparison.Ordinal) >= 0;
        }

        private static bool IsCsvParserRouteLine(string line)
        {
            bool hasCsv = line.IndexOf("Csv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          line.IndexOf(".csv", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasCsv)
                return false;

            string trimmed = line.TrimStart();
            if (IsPassiveCsvSymbol(trimmed))
                return false;

            if (ContainsAnyOrdinalIgnoreCase(
                    trimmed,
                    "LoadProfilesFromCsv(",
                    "LoadToleranceProfilesFromBytes(",
                    "TryLoadProfilesFromDisk(",
                    "ReadCsvFileIntoScratch(",
                    "ReadSiltProfileCsvBytes(",
                    "TryStageCsvProfileFromDisk(",
                    "RunCsvOverrideLoad(",
                    "ParseCsvOverrides(",
                    "ParseSwarmSpeciesProfiles(",
                    "TryReadCsvBytesForLoad(",
                    "TryLoadDefault(",
                    "SignalTuningCsvHotSwap.TryLoadDefault(",
                    "SignalThreadContentionCsvHotSwap.TryLoadDefault(",
                    "VolumetricSiltCsvParser.TryParse(",
                    "PropwashGpuProfileCsvParser.TryParse",
                    "SpatialGridProfileCsv.Parse("))
            {
                return true;
            }

            if (trimmed.IndexOf("FileStream", StringComparison.Ordinal) >= 0 &&
                trimmed.IndexOf("Csv", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (IsMethodDeclaration(trimmed) &&
                ContainsAnyOrdinalIgnoreCase(trimmed, "CsvParser", "CsvIngestor", "ReadCsv", "SplitCsv", "NextCsv", "TrimCsv"))
            {
                return true;
            }

            return false;
        }

        private static bool IsPassiveCsvSymbol(string trimmed)
        {
            return trimmed.IndexOf("BufferID", StringComparison.Ordinal) >= 0 ||
                   trimmed.IndexOf("VaultGenerationHandle", StringComparison.Ordinal) >= 0 ||
                   trimmed.IndexOf("NativeArray<byte> CsvScratch", StringComparison.Ordinal) >= 0 ||
                   trimmed.IndexOf("CsvScratch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("CsvLoadedCount", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("CounterCsvLoaded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("_csvThreadFaultCode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("_csvGate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("CsvIoFault", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("WriteIntCsv", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMethodDeclaration(string trimmed)
        {
            return (trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("private ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("internal ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("protected ", StringComparison.Ordinal)) &&
                   trimmed.IndexOf("(", StringComparison.Ordinal) >= 0 &&
                   trimmed.IndexOf(";", StringComparison.Ordinal) < 0;
        }

        private static bool ContainsAnyOrdinalIgnoreCase(string text, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool HasStaticConfigContext(string line)
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

        private static void AddFinding(ref ScanMode mode, string path, int line, string kind, string source, bool allowed)
        {
            Dictionary<string, int> targetCounts = allowed ? mode.AllowedByKind : mode.BlockingByKind;
            targetCounts.TryGetValue(kind, out int count);
            targetCounts[kind] = count + 1;

            if (allowed)
            {
                mode.AllowedPersistenceFindingCount++;
                if (mode.AllowedFindings.Count < MaxFindings)
                    mode.AllowedFindings.Add(new Finding(kind, path, line, source));
                return;
            }

            mode.BlockingFindingCount++;
            if (mode.BlockingFindings.Count < MaxFindings)
                mode.BlockingFindings.Add(new Finding(kind, path, line, source));
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

        private static void ApplyPreprocessorDirective(string trimmed, List<PreprocessorFrame> stack, bool developmentBuild)
        {
            if (trimmed.StartsWith("#if", StringComparison.Ordinal))
            {
                string expression = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                stack.Add(new PreprocessorFrame(EvaluateForPlayer(expression, developmentBuild)));
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
                    bool active = EvaluateForPlayer(expression, developmentBuild);
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
            {
                if (!stack[i].CurrentActive)
                    return false;
            }

            return true;
        }

        private static bool EvaluateForPlayer(string expression, bool developmentBuild)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            return new PreprocessorExpressionParser(expression, developmentBuild).ParseExpression();
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

        private static string BuildReport(bool passed, ScanMode release, ScanMode development)
        {
            StringBuilder report = new StringBuilder(65536);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_MONOLITH_PLAYER_PARSER_ABSENCE_CLI_V1\",");
            report.AppendLine("  \"agent\": \"" + AgentId + "\",");
            report.Append("  \"generated\": \"").Append(DateTime.UtcNow.ToString("O")).AppendLine("\",");
            report.Append("  \"status\": \"").Append(passed ? "PASS_PLAYER_STATIC_CONFIG_PARSER_ABSENCE" : "FAIL_PLAYER_STATIC_CONFIG_PARSER_RESIDUE").AppendLine("\",");
            report.AppendLine("  \"proofBoundary\": \"Standalone CLI source/preprocessor scan. It proves no active release/development player static-config CSV/text parser routes under Assets/_Project/Scripts, except documented user save/profile/mod persistence. It is not a Unity player profiler trace.\",");
            report.AppendLine("  \"modes\": {");
            AppendMode(report, "release", release, comma: true);
            AppendMode(report, "development", development, comma: false);
            report.AppendLine("  }");
            report.AppendLine("}");
            return report.ToString();
        }

        private static void AppendMode(StringBuilder report, string name, ScanMode mode, bool comma)
        {
            report.Append("    \"").Append(name).AppendLine("\": {");
            report.Append("      \"status\": \"").Append(mode.Status).AppendLine("\",");
            report.Append("      \"developmentBuild\": ").Append(Lower(mode.DevelopmentBuild)).AppendLine(",");
            report.Append("      \"filesScanned\": ").Append(mode.FilesScanned).AppendLine(",");
            report.Append("      \"productionFilesScanned\": ").Append(mode.ProductionFilesScanned).AppendLine(",");
            report.Append("      \"editorOrTestFilesSkipped\": ").Append(mode.EditorOrTestFilesSkipped).AppendLine(",");
            report.Append("      \"inactiveLinesSkipped\": ").Append(mode.InactiveLinesSkipped).AppendLine(",");
            report.Append("      \"missingRoots\": ").Append(mode.MissingRoots).AppendLine(",");
            report.Append("      \"blockingFindingCount\": ").Append(mode.BlockingFindingCount).AppendLine(",");
            report.Append("      \"directFileStreamReadByteCount\": ").Append(mode.DirectFileStreamReadByteCount).AppendLine(",");
            report.Append("      \"allowedPersistenceFindingCount\": ").Append(mode.AllowedPersistenceFindingCount).AppendLine(",");
            AppendDictionary(report, "blockingByKind", mode.BlockingByKind, comma: true);
            AppendDictionary(report, "allowedByKind", mode.AllowedByKind, comma: true);
            AppendFindings(report, "findings", mode.BlockingFindings, comma: true);
            AppendFindings(report, "allowedPersistenceFindings", mode.AllowedFindings, comma: false);
            report.Append("    }");
            report.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendDictionary(StringBuilder report, string name, Dictionary<string, int> values, bool comma)
        {
            report.Append("      \"").Append(name).AppendLine("\": {");
            int index = 0;
            foreach (KeyValuePair<string, int> pair in values)
            {
                report.Append("        \"").Append(Escape(pair.Key)).Append("\": ").Append(pair.Value);
                report.AppendLine(++index < values.Count ? "," : string.Empty);
            }

            report.Append("      }");
            report.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendFindings(StringBuilder report, string name, List<Finding> findings, bool comma)
        {
            report.Append("      \"").Append(name).AppendLine("\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                report.Append("        { \"kind\": \"").Append(Escape(finding.Kind))
                    .Append("\", \"path\": \"").Append(Escape(finding.Path))
                    .Append("\", \"line\": ").Append(finding.Line)
                    .Append(", \"source\": \"").Append(Escape(Trim(finding.Source))).Append("\" }");
                report.AppendLine(i + 1 < findings.Count ? "," : string.Empty);
            }

            report.Append("      ]");
            report.AppendLine(comma ? "," : string.Empty);
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

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string trimmed = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
            return trimmed.Length <= 180 ? trimmed : trimmed.Substring(0, 180);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static string Lower(bool value)
        {
            return value ? "true" : "false";
        }

        private sealed class ScanMode
        {
            public readonly bool DevelopmentBuild;
            public string Status = string.Empty;
            public int FilesScanned;
            public int ProductionFilesScanned;
            public int EditorOrTestFilesSkipped;
            public int InactiveLinesSkipped;
            public int MissingRoots;
            public int BlockingFindingCount;
            public int DirectFileStreamReadByteCount;
            public int AllowedPersistenceFindingCount;
            public readonly Dictionary<string, int> BlockingByKind = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> AllowedByKind = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly List<Finding> BlockingFindings = new List<Finding>(MaxFindings);
            public readonly List<Finding> AllowedFindings = new List<Finding>(MaxFindings);

            public ScanMode(bool developmentBuild)
            {
                DevelopmentBuild = developmentBuild;
            }
        }

        private readonly struct Finding
        {
            public readonly string Kind;
            public readonly string Path;
            public readonly int Line;
            public readonly string Source;

            public Finding(string kind, string path, int line, string source)
            {
                Kind = kind;
                Path = path;
                Line = line;
                Source = source;
            }
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
            private int _index;

            public PreprocessorExpressionParser(string text, bool developmentBuild)
            {
                _text = text;
                _developmentBuild = developmentBuild;
                _index = 0;
            }

            public bool ParseExpression()
            {
                return ParseOr();
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

                return SymbolValueForPlayer(ReadSymbol());
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
                {
                    if (_text[_index + i] != token[i])
                        return false;
                }

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

                if (string.Equals(symbol, "DEVELOPMENT_BUILD", StringComparison.Ordinal) ||
                    string.Equals(symbol, "DEBUG", StringComparison.Ordinal))
                {
                    return _developmentBuild;
                }

                if (string.Equals(symbol, "true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(symbol, "false", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(symbol))
                    return false;

                return true;
            }
        }
    }
}
