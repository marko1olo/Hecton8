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
            Hecton8.Core.H8Debug.Log("SHINOBU_352 voice scanner found " + result.Findings.Count + " OOP voice findings; no report files written.");
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

        private static string ToProjectPath(string projectRoot, string path)
        {
            string relative = path.StartsWith(projectRoot, StringComparison.Ordinal)
                ? path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace(Path.DirectorySeparatorChar, '/');
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
