#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public static class OOP_Voice_Scanner_SHINOBU_352
    {
        private const string SectionKey = "shinobu_352_vocal_warning_system_audio_queue";
        private const string SharedReportPath = "Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json";

        private static readonly string[] Roots =
        {
            "Assets/_Project/Scripts/Audio",
            "Assets/_Project/Scripts/Gameplay",
            "Assets/_Project/Scripts/Combat",
            "Assets/_Project/Scripts/Physiology"
        };

        private static readonly string[] RootPlayerFilePrefixes =
        {
            "Player",
            "HectonPlayer"
        };

        private static readonly string[] ForbiddenPatterns =
        {
            "AudioSource.PlayOneShot",
            "SubtitleManager.DisplaySubtitle",
            "PlayWarning",
            "Queue<AudioClip>",
            "Queue<Voice*>",
            "List<Voice*>",
            "Dictionary<string, AudioClip>"
        };

        [MenuItem("Hecton8/Audio/Scan OOP Voice Triggers SHINOBU_352")]
        public static void Scan()
        {
            ScanResult result = ScanProject();
            WriteReports(result);
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_352 voice scanner found " + result.Findings.Count + " OOP voice findings.");
        }

        internal static ScanResult ScanProject()
        {
            ScanResult result = new ScanResult
            {
                Findings = new List<Finding>(64)
            };

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int rootIndex = 0; rootIndex < Roots.Length; rootIndex++)
            {
                string root = Path.Combine(projectRoot, Roots[rootIndex]);
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(files[fileIndex], projectRoot, ref result);
            }

            ScanPlayerNamedFiles(projectRoot, ref result);
            return result;
        }

        private static void ScanPlayerNamedFiles(string projectRoot, ref ScanResult result)
        {
            string root = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string normalized = files[fileIndex].Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf("/Audio/", StringComparison.Ordinal) >= 0 ||
                    normalized.IndexOf("/Gameplay/", StringComparison.Ordinal) >= 0 ||
                    normalized.IndexOf("/Combat/", StringComparison.Ordinal) >= 0 ||
                    normalized.IndexOf("/Physiology/", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                string fileName = Path.GetFileName(files[fileIndex]);
                for (int prefixIndex = 0; prefixIndex < RootPlayerFilePrefixes.Length; prefixIndex++)
                {
                    if (!fileName.StartsWith(RootPlayerFilePrefixes[prefixIndex], StringComparison.Ordinal))
                        continue;

                    ScanFile(files[fileIndex], projectRoot, ref result);
                    break;
                }
            }
        }

        private static void ScanFile(string path, string projectRoot, ref ScanResult result)
        {
            string normalizedPath = ToProjectPath(projectRoot, path);
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedPath.EndsWith("OOP_Voice_Scanner_SHINOBU_352.cs", StringComparison.Ordinal))
            {
                return;
            }

            result.FilesScanned++;
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception)
            {
                result.ParserFailures++;
                ScanLexicalFallback(normalizedPath, source, ref result);
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFailures++;
                ScanLexicalFallback(normalizedPath, source, ref result);
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            result.SyntaxTreesParsed++;
            ScanSyntaxTree(normalizedPath, tree, root, ref result);
        }

        private static void ScanSyntaxTree(
            string path,
            SyntaxTree tree,
            CompilationUnitSyntax root,
            ref ScanResult result)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    result.SyntaxNodesVisited++;
                    if (TryResolveAstFinding(node, out string pattern))
                        RegisterFinding(path, LineOf(tree, node), pattern, "ROSLYN_AST", ref result);
                }
            }
        }

        private static bool TryResolveAstFinding(SyntaxNode node, out string pattern)
        {
            pattern = null;

            if (node is InvocationExpressionSyntax invocation)
                return TryResolveForbiddenInvocation(invocation, out pattern);

            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                if (TryResolveForbiddenType(objectCreation.Type, out pattern))
                {
                    pattern = "new " + pattern;
                    return true;
                }
            }

            if (node is VariableDeclarationSyntax variableDeclaration)
                return TryResolveForbiddenType(variableDeclaration.Type, out pattern);

            if (node is PropertyDeclarationSyntax propertyDeclaration)
                return TryResolveForbiddenType(propertyDeclaration.Type, out pattern);

            if (node is ParameterSyntax parameterSyntax && parameterSyntax.Type != null)
                return TryResolveForbiddenType(parameterSyntax.Type, out pattern);

            return false;
        }

        private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string pattern)
        {
            pattern = null;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                if (memberName == "PlayOneShot")
                {
                    pattern = "AudioSource.PlayOneShot";
                    return true;
                }

                if (memberName == "DisplaySubtitle")
                {
                    pattern = "SubtitleManager.DisplaySubtitle";
                    return true;
                }

                if (memberName == "PlayWarning")
                {
                    pattern = "PlayWarning";
                    return true;
                }
            }

            if (invocation.Expression is IdentifierNameSyntax identifierName)
            {
                string value = identifierName.Identifier.ValueText;
                if (value == "PlayOneShot")
                {
                    pattern = "AudioSource.PlayOneShot";
                    return true;
                }

                if (value == "DisplaySubtitle")
                {
                    pattern = "SubtitleManager.DisplaySubtitle";
                    return true;
                }

                if (value == "PlayWarning")
                {
                    pattern = "PlayWarning";
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveForbiddenType(TypeSyntax type, out string pattern)
        {
            pattern = null;
            GenericNameSyntax genericName = ExtractGenericName(type);
            if (genericName == null)
                return false;

            string identifier = genericName.Identifier.ValueText;
            if (identifier == "Queue")
            {
                if (HasAudioClipArgument(genericName))
                {
                    pattern = "Queue<AudioClip>";
                    return true;
                }

                if (HasVoiceArgument(genericName))
                {
                    pattern = "Queue<Voice*>";
                    return true;
                }
            }

            if (identifier == "List" && HasVoiceArgument(genericName))
            {
                pattern = "List<Voice*>";
                return true;
            }

            if (identifier == "Dictionary" && IsStringToAudioClipDictionary(genericName))
            {
                pattern = "Dictionary<string, AudioClip>";
                return true;
            }

            return false;
        }

        private static void ScanLexicalFallback(string path, string source, ref ScanResult result)
        {
            result.LexicalFallbackFiles++;
            string[] lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.IndexOf("PlayOneShot(", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "AudioSource.PlayOneShot:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);

                if (line.IndexOf("DisplaySubtitle(", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "SubtitleManager.DisplaySubtitle:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);

                if (line.IndexOf("PlayWarning(", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "PlayWarning:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);

                if (line.IndexOf("Queue<AudioClip", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "Queue<AudioClip>:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);

                if (line.IndexOf("Queue<Voice", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "Queue<Voice*>:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);

                if (line.IndexOf("List<Voice", StringComparison.Ordinal) >= 0)
                    RegisterFinding(path, lineIndex + 1, "List<Voice*>:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);
            }
        }

        private static GenericNameSyntax ExtractGenericName(TypeSyntax type)
        {
            if (type is GenericNameSyntax genericName)
                return genericName;

            if (type is QualifiedNameSyntax qualifiedName && qualifiedName.Right is GenericNameSyntax qualifiedGeneric)
                return qualifiedGeneric;

            if (type is AliasQualifiedNameSyntax aliasQualifiedName && aliasQualifiedName.Name is GenericNameSyntax aliasGeneric)
                return aliasGeneric;

            return null;
        }

        private static bool HasAudioClipArgument(GenericNameSyntax genericName)
        {
            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                string value = arguments[i].ToString();
                if (TypeNameEquals(value, "AudioClip"))
                    return true;
            }

            return false;
        }

        private static bool HasVoiceArgument(GenericNameSyntax genericName)
        {
            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                string value = arguments[i].ToString();
                if (value.IndexOf("Voice", StringComparison.Ordinal) >= 0 ||
                    value.IndexOf("Vocal", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStringToAudioClipDictionary(GenericNameSyntax genericName)
        {
            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            if (arguments.Count != 2)
                return false;

            return IsStringType(arguments[0]) && TypeNameEquals(arguments[1].ToString(), "AudioClip");
        }

        private static bool IsStringType(TypeSyntax type)
        {
            string value = type.ToString();
            return value == "string" ||
                   value == "String" ||
                   value == "System.String" ||
                   value.EndsWith(".String", StringComparison.Ordinal);
        }

        private static bool TypeNameEquals(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.Ordinal) ||
                   value.EndsWith("." + expected, StringComparison.Ordinal);
        }

        private static bool HasParseError(SyntaxTree tree)
        {
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static void RegisterFinding(
            string path,
            int line,
            string pattern,
            string route,
            ref ScanResult result)
        {
            result.Findings.Add(new Finding
            {
                Path = path,
                Line = line,
                Pattern = pattern,
                Route = route
            });
        }

        private static int LineOf(SyntaxTree tree, SyntaxNode node)
        {
            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static void WriteReports(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sectionJson = BuildSectionJson(result);
            string sidecarPath = Path.Combine(projectRoot, SidecarReportPath);
            string sharedPath = Path.Combine(projectRoot, SharedReportPath);
            string sidecarDirectory = Path.GetDirectoryName(sidecarPath);
            if (!string.IsNullOrEmpty(sidecarDirectory))
                Directory.CreateDirectory(sidecarDirectory);

            File.WriteAllText(sidecarPath, "{\n" + sectionJson + "\n}\n", Encoding.UTF8);

            string existing = File.Exists(sharedPath) ? File.ReadAllText(sharedPath, Encoding.UTF8) : string.Empty;
            File.WriteAllText(sharedPath, UpsertSection(existing, sectionJson), Encoding.UTF8);
        }

        private static string BuildSectionJson(ScanResult result)
        {
            StringBuilder builder = new StringBuilder(4096 + result.Findings.Count * 192);
            builder.Append("  \"").Append(SectionKey).Append("\": {\n");
            builder.Append("    \"agent\": \"SHINOBU_352\",\n");
            builder.Append("    \"summary\": \"")
                .Append(result.Findings.Count == 0 ? "OOP Voice Triggers Eradicated" : "OOP Voice Triggers Detected")
                .Append("\",\n");
            builder.Append("    \"scanner\": \"Assets/_Project/Scripts/Audio/Editor/OOP_Voice_Scanner_SHINOBU_352.cs\",\n");
            builder.Append("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree AST primary pass; lexical fallback only on parse exception\",\n");
            builder.Append("    \"scannerExecution\": \"Unity Editor MenuItem; CLI rg verification may be used only as a source-control fallback report\",\n");
            builder.Append("    \"scannerUsesRoslynAst\": true,\n");
            builder.Append("    \"filesScanned\": ").Append(result.FilesScanned).Append(",\n");
            builder.Append("    \"syntaxTreesParsed\": ").Append(result.SyntaxTreesParsed).Append(",\n");
            builder.Append("    \"syntaxNodesVisited\": ").Append(result.SyntaxNodesVisited).Append(",\n");
            builder.Append("    \"parserFailures\": ").Append(result.ParserFailures).Append(",\n");
            builder.Append("    \"lexicalFallbackFiles\": ").Append(result.LexicalFallbackFiles).Append(",\n");
            builder.Append("    \"forbiddenFindingCount\": ").Append(result.Findings.Count).Append(",\n");
            builder.Append("    \"scope\": [\n");
            for (int i = 0; i < Roots.Length; i++)
            {
                builder.Append("      ");
                AppendJsonString(builder, Roots[i]);
                builder.Append(i + 1 < Roots.Length ? ",\n" : "\n");
            }

            builder.Append("    ],\n");
            builder.Append("    \"playerFilePrefixes\": [");
            for (int i = 0; i < RootPlayerFilePrefixes.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendJsonString(builder, RootPlayerFilePrefixes[i]);
            }

            builder.Append("],\n");
            builder.Append("    \"forbiddenPatterns\": [");
            for (int i = 0; i < ForbiddenPatterns.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendJsonString(builder, ForbiddenPatterns[i]);
            }

            builder.Append("],\n");
            builder.Append("    \"replacementRoute\": \"SignalBus/Vault VocalWarningDTO -> 64-bit VwsPriorityWord -> VocalCueSignal + SubtitleSignal; no gameplay voice AudioSource or managed voice queues\",\n");
            builder.Append("    \"findings\": [");
            for (int i = 0; i < result.Findings.Count; i++)
            {
                Finding finding = result.Findings[i];
                builder.Append(i == 0 ? "\n" : ",\n");
                builder.Append("      { \"path\": ");
                AppendJsonString(builder, finding.Path);
                builder.Append(", \"line\": ").Append(finding.Line).Append(", \"pattern\": ");
                AppendJsonString(builder, finding.Pattern);
                builder.Append(", \"route\": ");
                AppendJsonString(builder, finding.Route);
                builder.Append(" }");
            }

            if (result.Findings.Count > 0)
                builder.Append("\n    ]\n");
            else
                builder.Append("]\n");

            builder.Append("  }");
            return builder.ToString();
        }

        private static string UpsertSection(string existing, string sectionJson)
        {
            string trimmed = string.IsNullOrWhiteSpace(existing) ? "{}" : existing.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
                return "{\n" + sectionJson + "\n}\n";

            string key = "\"" + SectionKey + "\"";
            int keyIndex = trimmed.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int propertyStart = FindPropertyStart(trimmed, keyIndex);
                int objectStart = trimmed.IndexOf('{', keyIndex);
                int objectEnd = FindObjectEnd(trimmed, objectStart);
                if (objectStart >= 0 && objectEnd >= objectStart)
                {
                    int propertyEnd = objectEnd + 1;
                    int next = SkipWhitespace(trimmed, propertyEnd);
                    if (next < trimmed.Length && trimmed[next] == ',')
                    {
                        propertyEnd = next + 1;
                    }
                    else
                    {
                        int previous = propertyStart - 1;
                        while (previous >= 0 && char.IsWhiteSpace(trimmed[previous]))
                            previous--;
                        if (previous >= 0 && trimmed[previous] == ',')
                            propertyStart = previous;
                    }

                    trimmed = trimmed.Remove(propertyStart, propertyEnd - propertyStart);
                }
            }

            int insertIndex = trimmed.LastIndexOf('}');
            string prefix = trimmed.Substring(0, insertIndex).TrimEnd();
            string separator = prefix.Length > 1 ? ",\n" : "\n";
            return prefix + separator + sectionJson + "\n}\n";
        }

        private static int FindPropertyStart(string value, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && value[start - 1] != '\n' && value[start - 1] != '\r')
                start--;
            return start;
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }

        private static int FindObjectEnd(string value, int objectStart)
        {
            if (objectStart < 0)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < value.Length; i++)
            {
                char c = value[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string relative = path.StartsWith(projectRoot, StringComparison.Ordinal)
                ? path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }

        internal struct ScanResult
        {
            public int FilesScanned;
            public int SyntaxTreesParsed;
            public int SyntaxNodesVisited;
            public int ParserFailures;
            public int LexicalFallbackFiles;
            public List<Finding> Findings;
        }

        internal struct Finding
        {
            public string Path;
            public int Line;
            public string Pattern;
            public string Route;
        }
    }
}
#endif
