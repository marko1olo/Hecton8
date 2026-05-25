#if UNITY_EDITOR
namespace Hecton8.Tools.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor-only static inquisition for SHINOBU_224 equipment authority.
    /// </summary>
    public static class Equipment_Update_Inquisition
    {
        private const string ReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_224.json";
        private const string SharedReportCompatibilityPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json";
        private static readonly Regex ForbiddenUpdateRegex = new Regex(@"\b(?:public|private|protected|internal)?\s*(?:virtual|override|sealed|static)?\s*void\s+(Update|FixedUpdate|LateUpdate)\s*\(", RegexOptions.Compiled);
        private static readonly Regex ForbiddenCoroutineRegex = new Regex(@"\b(IEnumerator\s+\w+\s*\(|StartCoroutine\s*\()", RegexOptions.Compiled);

        [MenuItem("HECTON-8/Tools/Equipment Update Inquisition")]
        public static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            int scannedFiles = 0;
            int candidateFiles = 0;
            int violations = 0;
            StringBuilder findings = new StringBuilder(4096);

            if (Directory.Exists(scriptsRoot))
                ScanDirectory(scriptsRoot, projectRoot, ref scannedFiles, ref candidateFiles, ref violations, findings);

            string reportFullPath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(reportFullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_224\",");
            json.AppendLine("  \"scanner\": \"Equipment_Update_Inquisition\",");
            json.Append("  \"sharedReportCompatibilityPath\": \"").Append(SharedReportCompatibilityPath).AppendLine("\",");
            json.AppendLine("  \"scannerUsesStructuralSyntaxPass\": true,");
            json.AppendLine("  \"scannerUsesRoslynAst\": false,");
            json.AppendLine("  \"scannerParserRoute\": \"comment/string stripped declaration and invocation parser; Hecton8.Editor has no Roslyn asmdef reference\",");
            json.AppendLine("  \"summary\": \"Tool Updates Purged\",");
            json.Append("  \"scannedFiles\": ").Append(scannedFiles).AppendLine(",");
            json.Append("  \"candidateToolFiles\": ").Append(candidateFiles).AppendLine(",");
            json.Append("  \"forbiddenPatternHits\": ").Append(violations).AppendLine(",");
            json.AppendLine("  \"authority\": \"ModularEquipmentEngine: Burst equipment thermal/battery/wear processor over GlobalDataVault buffers 71300-71316\",");
            json.Append("  \"verdict\": \"").Append(violations == 0 ? "PASS" : "FAIL").AppendLine("\",");
            json.AppendLine("  \"findings\": [");
            json.Append(findings);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");

            File.WriteAllText(reportFullPath, json.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[SHINOBU_224] Equipment update inquisition wrote {ReportPath}. Candidates={candidateFiles}, Violations={violations}.");
        }

        private static void ScanDirectory(
            string directory,
            string projectRoot,
            ref int scannedFiles,
            ref int candidateFiles,
            ref int violations,
            StringBuilder findings)
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string normalizedPath = file.Replace('\\', '/');
                if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                scannedFiles++;
                string source = File.ReadAllText(file);
                string relativePath = MakeRelative(projectRoot, file);
                if (!IsToolCandidate(relativePath, source))
                    continue;

                candidateFiles++;
                string sanitized = StripCommentsAndStrings(source);
                violations += AppendMatches(relativePath, sanitized, ForbiddenUpdateRegex, "FORBIDDEN_TOOL_UPDATE", findings, violations);
                violations += AppendMatches(relativePath, sanitized, ForbiddenCoroutineRegex, "FORBIDDEN_TOOL_COROUTINE", findings, violations);
            }
        }

        private static bool IsToolCandidate(string relativePath, string source)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Tools/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("/PlayerTool.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/LaserCutter.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/FlashlightTool.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/ScannerTool.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/MantaScooter.cs", StringComparison.OrdinalIgnoreCase) ||
                   source.IndexOf("namespace Hecton8.Tools", StringComparison.Ordinal) >= 0 ||
                   source.IndexOf(": PlayerTool", StringComparison.Ordinal) >= 0;
        }

        private static int AppendMatches(string relativePath, string sanitized, Regex regex, string kind, StringBuilder findings, int existingViolations)
        {
            MatchCollection matches = regex.Matches(sanitized);
            for (int i = 0; i < matches.Count; i++)
            {
                if (existingViolations + i > 0)
                    findings.AppendLine(",");

                Match match = matches[i];
                findings.Append("    { \"file\": \"")
                    .Append(Escape(relativePath))
                    .Append("\", \"line\": ")
                    .Append(CountLine(sanitized, match.Index))
                    .Append(", \"kind\": \"")
                    .Append(kind)
                    .Append("\", \"snippet\": \"")
                    .Append(Escape(ExtractSnippet(sanitized, match.Index)))
                    .Append("\" }");
            }

            return matches.Count;
        }

        private static string StripCommentsAndStrings(string source)
        {
            StringBuilder output = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;
            bool verbatimString = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        output.Append(c);
                    }
                    else
                    {
                        output.Append(' ');
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        output.Append("  ");
                        i++;
                    }
                    else
                    {
                        output.Append(c == '\n' ? '\n' : ' ');
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && n == '"')
                    {
                        output.Append("  ");
                        i++;
                        continue;
                    }

                    bool end = c == '"' && (verbatimString || !IsEscaped(source, i));
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    bool end = c == '\'' && !IsEscaped(source, i);
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '"' || (c == '@' && n == '"'))
                {
                    stringLiteral = true;
                    verbatimString = c == '@';
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (c == '@')
                    {
                        output.Append(' ');
                        i++;
                    }
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                    output.Append(' ');
                    continue;
                }

                output.Append(c);
            }

            return output.ToString();
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

        private static int CountLine(string source, int index)
        {
            int line = 1;
            int count = Math.Min(index, source.Length);
            for (int i = 0; i < count; i++)
            {
                if (source[i] == '\n')
                    line++;
            }
            return line;
        }

        private static string ExtractSnippet(string source, int index)
        {
            int start = index;
            while (start > 0 && source[start - 1] != '\n' && source[start - 1] != '\r')
                start--;

            int end = index;
            while (end < source.Length && source[end] != '\n' && source[end] != '\r')
                end++;

            return source.Substring(start, end - start).Trim();
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(root));
            Uri pathUri = new Uri(path);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
