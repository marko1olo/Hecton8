#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Roslyn AST proof that hull/base stress audio does not route through Unity AudioSource playback.
    /// </summary>
    public static class OOP_AudioSource_Scanner
    {
        private const string SharedReportPath = "Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_351.json";
        private const string SectionKey = "shinobu351AudioSourceScanner";
        private const string CleanSummary = "OOP Audio Sources Eradicated";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Habitat",
            "Assets/_Project/Scripts/Physics",
            "Assets/_Project/Scripts/Audio"
        };

        [MenuItem("Hecton8/Audio/OOP AudioSource Scanner")]
        public static void RunMenuScan()
        {
            ScanResult result = ScanProject();
            WriteReports(result);
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log("SHINOBU_351 OOP_AudioSource_Scanner found " + result.ActiveViolationCount + " active violations.");
        }

        public static ScanResult ScanProject()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScanResult result = new ScanResult();
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = Path.Combine(projectRoot, ScanRoots[rootIndex]);
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(projectRoot, files[fileIndex], result);
            }

            return result;
        }

        private static void ScanFile(string projectRoot, string path, ScanResult result)
        {
            string normalizedPath = ToProjectPath(projectRoot, path);
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            result.FilesScanned++;
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                result.ParserFailures++;
                result.AppendFinding(normalizedPath, 0, "RoslynParseFailure", exception.GetType().Name, false);
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFailures++;
                result.AppendFinding(normalizedPath, 0, "RoslynParseError", "syntax error", false);
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveForbiddenAudioSourceNode(node, out string token))
                        continue;

                    bool activeViolation = IsHabitatOrPhysicsRuntimePath(normalizedPath) || IsHabitatOrPhysicsNamespace(node);
                    result.TotalForbiddenNodes++;
                    if (activeViolation)
                        result.ActiveViolationCount++;

                    result.AppendFinding(
                        normalizedPath,
                        GetLineNumber(node),
                        token,
                        node.Kind().ToString(),
                        activeViolation);
                }
            }
        }

        private static bool TryResolveForbiddenAudioSourceNode(SyntaxNode node, out string token)
        {
            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string typeName = objectCreation.Type.ToString();
                if (TypeNameEquals(typeName, "AudioSource"))
                {
                    token = "new AudioSource";
                    return true;
                }

                if (IsDictionaryStringAudioClipType(objectCreation.Type))
                {
                    token = "Dictionary<string, AudioClip>";
                    return true;
                }
            }

            if (node is ArrayCreationExpressionSyntax arrayCreation &&
                TypeNameEquals(arrayCreation.Type.ElementType.ToString(), "AudioSource"))
            {
                token = "AudioSource[]";
                return true;
            }

            if (node is VariableDeclarationSyntax variableDeclaration)
            {
                if (TypeNameEquals(variableDeclaration.Type.ToString(), "AudioSource"))
                {
                    token = "AudioSource variable";
                    return true;
                }

                if (TypeNameEquals(variableDeclaration.Type.ToString(), "AudioClip"))
                {
                    token = "AudioClip variable";
                    return true;
                }

                if (IsDictionaryStringAudioClipType(variableDeclaration.Type))
                {
                    token = "Dictionary<string, AudioClip>";
                    return true;
                }
            }

            if (node is InvocationExpressionSyntax invocation)
                return TryResolveForbiddenInvocation(invocation, out token);

            token = string.Empty;
            return false;
        }

        private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string token)
        {
            token = string.Empty;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                string receiver = memberAccess.Expression.ToString();
                if (memberName == "PlayClipAtPoint" && ReceiverEndsWith(receiver, "AudioSource"))
                {
                    token = "AudioSource.PlayClipAtPoint";
                    return true;
                }

                if ((memberName == "PlayOneShot" || memberName == "Play") && LooksLikeAudioReceiver(receiver))
                {
                    token = "AudioSource." + memberName;
                    return true;
                }

                if (memberName == "PlayAtPoint")
                {
                    token = "PlayAtPoint";
                    return true;
                }

                if (memberAccess.Name is GenericNameSyntax genericName)
                {
                    if (memberName == "Load" &&
                        ReceiverEndsWith(receiver, "Resources") &&
                        HasGenericArgument(genericName, "AudioClip"))
                    {
                        token = "Resources.Load<AudioClip>";
                        return true;
                    }

                    if (memberName == "AddComponent" &&
                        HasGenericArgument(genericName, "AudioSource"))
                    {
                        token = "AddComponent<AudioSource>";
                        return true;
                    }
                }
            }

            if (invocation.Expression is GenericNameSyntax directGeneric)
            {
                string memberName = directGeneric.Identifier.ValueText;
                if (memberName == "AddComponent" && HasGenericArgument(directGeneric, "AudioSource"))
                {
                    token = "AddComponent<AudioSource>";
                    return true;
                }
            }

            return false;
        }

        private static bool IsDictionaryStringAudioClipType(TypeSyntax type)
        {
            GenericNameSyntax genericName = ExtractGenericName(type);
            if (genericName == null || !TypeNameEquals(genericName.Identifier.ValueText, "Dictionary"))
                return false;

            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            if (arguments.Count != 2)
                return false;

            return IsStringType(arguments[0]) && TypeNameEquals(arguments[1].ToString(), "AudioClip");
        }

        private static GenericNameSyntax ExtractGenericName(TypeSyntax type)
        {
            if (type is GenericNameSyntax genericName)
                return genericName;

            if (type is QualifiedNameSyntax qualifiedName && qualifiedName.Right is GenericNameSyntax qualifiedGeneric)
                return qualifiedGeneric;

            if (type is AliasQualifiedNameSyntax aliasQualified && aliasQualified.Name is GenericNameSyntax aliasGeneric)
                return aliasGeneric;

            return null;
        }

        private static bool IsStringType(TypeSyntax type)
        {
            string value = type.ToString();
            return string.Equals(value, "string", StringComparison.Ordinal) ||
                   string.Equals(value, "String", StringComparison.Ordinal) ||
                   string.Equals(value, "System.String", StringComparison.Ordinal) ||
                   value.EndsWith(".String", StringComparison.Ordinal);
        }

        private static bool HasParseError(SyntaxTree tree)
        {
            using (System.Collections.Generic.IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static bool HasGenericArgument(GenericNameSyntax genericName, string expectedTypeName)
        {
            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                if (TypeNameEquals(arguments[i].ToString(), expectedTypeName))
                    return true;
            }

            return false;
        }

        private static bool TypeNameEquals(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.Ordinal) ||
                   value.EndsWith("." + expected, StringComparison.Ordinal);
        }

        private static bool ReceiverEndsWith(string receiver, string expected)
        {
            return string.Equals(receiver, expected, StringComparison.Ordinal) ||
                   receiver.EndsWith("." + expected, StringComparison.Ordinal);
        }

        private static bool LooksLikeAudioReceiver(string receiver)
        {
            return receiver.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   receiver.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsHabitatOrPhysicsRuntimePath(string normalizedPath)
        {
            return normalizedPath.StartsWith("Assets/_Project/Scripts/Habitat/", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith("Assets/_Project/Scripts/Physics/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHabitatOrPhysicsNamespace(SyntaxNode node)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> ancestors = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestors.MoveNext())
                {
                    if (ancestors.Current is NamespaceDeclarationSyntax namespaceDeclaration)
                    {
                        string value = namespaceDeclaration.Name.ToString();
                        return value.StartsWith("Hecton8.Habitat", StringComparison.Ordinal) ||
                               value.StartsWith("Hecton8.Physics", StringComparison.Ordinal);
                    }

                    if (ancestors.Current is FileScopedNamespaceDeclarationSyntax fileScoped)
                    {
                        string value = fileScoped.Name.ToString();
                        return value.StartsWith("Hecton8.Habitat", StringComparison.Ordinal) ||
                               value.StartsWith("Hecton8.Physics", StringComparison.Ordinal);
                    }
                }
            }

            return false;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static void WriteReports(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            JObject section = BuildSection(result);
            WriteJsonAtomic(Path.Combine(projectRoot, SidecarReportPath), section);

            string sharedPath = Path.Combine(projectRoot, SharedReportPath);
            JObject shared = ReadJsonObject(sharedPath);
            shared[SectionKey] = section;
            WriteJsonAtomic(sharedPath, shared);
        }

        private static JObject BuildSection(ScanResult result)
        {
            JObject section = new JObject
            {
                ["agent"] = "SHINOBU_351",
                ["scanner"] = "OOP_AudioSource_Scanner",
                ["summary"] = CleanSummary,
                ["scannerMode"] = "ROSLYN_AST_TARGETED",
                ["scannerUsesRoslynAst"] = true,
                ["passed"] = result.ActiveViolationCount == 0,
                ["filesScanned"] = result.FilesScanned,
                ["parserFailures"] = result.ParserFailures,
                ["totalForbiddenNodes"] = result.TotalForbiddenNodes,
                ["activeHabitatPhysicsViolationCount"] = result.ActiveViolationCount,
                ["policy"] = "Hull/base stress audio must enter player-critical DSP through SignalBus/BaseStructuralWarningSignal and flat PCM/Vault buffers, not Unity AudioSource playback."
            };

            JArray roots = new JArray();
            for (int i = 0; i < ScanRoots.Length; i++)
                roots.Add(ScanRoots[i]);
            section["scanRoots"] = roots;

            JArray findings = new JArray();
            for (int i = 0; i < result.FindingCount; i++)
            {
                Finding finding = result.Findings[i];
                findings.Add(new JObject
                {
                    ["path"] = finding.Path,
                    ["line"] = finding.Line,
                    ["token"] = finding.Token,
                    ["syntaxKind"] = finding.SyntaxKind,
                    ["activeViolation"] = finding.ActiveViolation
                });
            }

            section["findings"] = findings;
            return section;
        }

        private static JObject ReadJsonObject(string path)
        {
            if (!File.Exists(path))
                return new JObject();

            try
            {
                return JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                return new JObject();
            }
        }

        private static void WriteJsonAtomic(string path, JObject root)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, root.ToString(Formatting.Indented) + "\n", Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
                return;
            }

            File.Move(tempPath, path);
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string relative = path.StartsWith(projectRoot, StringComparison.Ordinal)
                ? path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public sealed class ScanResult
        {
            public int FilesScanned;
            public int ParserFailures;
            public int TotalForbiddenNodes;
            public int ActiveViolationCount;
            public int FindingCount;
            public readonly Finding[] Findings = new Finding[256];

            public void AppendFinding(string path, int line, string token, string syntaxKind, bool activeViolation)
            {
                if (FindingCount >= Findings.Length)
                    return;

                Findings[FindingCount++] = new Finding
                {
                    Path = path,
                    Line = line,
                    Token = token,
                    SyntaxKind = syntaxKind,
                    ActiveViolation = activeViolation
                };
            }
        }

        public struct Finding
        {
            public string Path;
            public int Line;
            public string Token;
            public string SyntaxKind;
            public bool ActiveViolation;
        }
    }
}
#endif
