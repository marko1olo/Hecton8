using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.ModdingSDK
{
    /// <summary>
    /// Editor-only source guard for APEX integration checks. It builds a static method AST in memory and writes no reports.
    /// </summary>
    internal static class ApexIntegratorSourceGuard
    {
        private static readonly string[] DefaultScope =
        {
            "Assets/_Project/Scripts/Editor/ModdingSDK/ApexIntegratorSourceGuard.cs",
            "Assets/_Project/Scripts/Editor/ModdingSDK/ExternalStarterKitWorkbenchWindow.cs",
            "Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs",
            "Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs",
            "Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs"
        };

        private static readonly HashSet<string> HotMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Execute",
            "FixedTick",
            "FixedUpdate",
            "LateFrameTick",
            "LateUpdate",
            "PostSimulationTick",
            "PreSimulationTick",
            "SlowTick",
            "Tick",
            "ToolTick",
            "Update",
            "VisualSyncTick"
        };

        private static readonly HashSet<string> VisualPhaseMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "LateFrameTick",
            "VisualSyncTick"
        };

        private static readonly string[] HotLookupTokens =
        {
            "GlobalRegistry." + "Get<",
            "GlobalRegistry." + "Get(",
            ".Get" + "Component<",
            ".Get" + "Component(",
            ".Get" + "Components<",
            ".Get" + "Components(",
            ".Get" + "ComponentInChildren<",
            ".Get" + "ComponentInChildren(",
            ".Get" + "ComponentsInChildren<",
            ".Get" + "ComponentsInChildren(",
            ".Get" + "ComponentInParent<",
            ".Get" + "ComponentInParent(",
            ".Get" + "ComponentsInParent<",
            ".Get" + "ComponentsInParent(",
            " Get" + "Component<",
            " Get" + "Component(",
            "\tGet" + "Component<",
            "\tGet" + "Component("
        };

        private static readonly string[] PresentationTokens =
        {
            ".SetGlobal",
            ".SetBuffer(",
            ".SetColor(",
            ".SetData(",
            ".SetFloat(",
            ".SetInt(",
            ".SetMatrix(",
            ".SetPropertyBlock(",
            ".SetText(",
            ".SetTexture(",
            ".SetVector("
        };

        [MenuItem("Hecton/Modding/Run APEX Source Guard")]
        private static void RunFromMenu()
        {
            ApexIntegratorSourceGuardResult result = RunDefaultScope();
            if (result.Failed)
                Debug.LogError(result.Summary);
            else
                Debug.Log(result.Summary);
        }

        internal static ApexIntegratorSourceGuardResult RunDefaultScope()
        {
            return Run(DefaultScope);
        }

        internal static ApexIntegratorSourceGuardResult Run(IReadOnlyList<string> relativePaths)
        {
            string projectRoot = GetProjectRootPath();
            List<string> failures = new List<string>(32);
            int parsedFiles = 0;
            int parsedMethods = 0;
            int hotMethods = 0;
            int hotLookupViolations = 0;
            int phaseViolations = 0;
            int vaultLockViolations = 0;
            int buildProcessTokens = 0;

            for (int i = 0; i < relativePaths.Count; i++)
            {
                string relativePath = NormalizeRelativePath(relativePaths[i]);
                string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                {
                    AppendFailure(failures, relativePath, 0, "missing source file");
                    continue;
                }

                string source = File.ReadAllText(absolutePath);
                if (!TryBuildSyntaxTree(relativePath, source, failures, out SourceFileAst syntaxTree))
                    continue;

                parsedFiles++;
                bool editorOnlyFile = IsEditorOnlyPath(relativePath);
                buildProcessTokens += CountLiteralBuildTokens(source);
                parsedMethods += syntaxTree.Methods.Count;

                for (int methodIndex = 0; methodIndex < syntaxTree.Methods.Count; methodIndex++)
                {
                    SourceMethod method = syntaxTree.Methods[methodIndex];

                    bool hotMethod = HotMethodNames.Contains(method.Name);
                    if (hotMethod)
                    {
                        hotMethods++;
                        ScanHotLookups(relativePath, syntaxTree.MaskedSource, method, failures, ref hotLookupViolations);
                        if (!editorOnlyFile && !VisualPhaseMethodNames.Contains(method.Name))
                            ScanPresentationWrites(relativePath, syntaxTree.MaskedSource, method, failures, ref phaseViolations);
                    }

                    ScanVaultWriteLocks(relativePath, syntaxTree.MaskedSource, method, failures, ref vaultLockViolations);
                }
            }

            if (buildProcessTokens != 0)
                AppendFailure(failures, "APEX_SOURCE_SCOPE", 0, "dotnet/build process token present in guarded C# scope");

            StringBuilder summary = new StringBuilder(1024);
            summary.AppendLine(failures.Count == 0 ? "APEX Source Guard PASS" : "APEX Source Guard FAIL");
            summary.Append("Parser: in-memory static method AST, no external process, no disk reports. FilesParsed=")
                .Append(parsedFiles)
                .Append(", MethodsParsed=")
                .Append(parsedMethods)
                .Append(", HotMethods=")
                .Append(hotMethods)
                .AppendLine(".");
            summary.Append("HotLookupViolations=")
                .Append(hotLookupViolations)
                .Append(", PhaseViolations=")
                .Append(phaseViolations)
                .Append(", VaultLockViolations=")
                .Append(vaultLockViolations)
                .Append(", BuildProcessTokens=")
                .Append(buildProcessTokens)
                .AppendLine(".");
            summary.AppendLine("Timing proof: guarded modding runtime/editor scope has no deferred presentation mutation outside LateFrameTick or VisualSyncTick.");
            summary.AppendLine("Lock proof: guarded scope has no method that can hold more than one GlobalDataVault write lock, and lock methods must use finally-release.");
            summary.AppendLine("Compile throttle proof: this guard launches no external compiler process.");

            for (int i = 0; i < failures.Count; i++)
                summary.AppendLine(failures[i]);

            return new ApexIntegratorSourceGuardResult(failures.Count != 0, summary.ToString());
        }

        private static bool TryBuildSyntaxTree(
            string relativePath,
            string source,
            List<string> failures,
            out SourceFileAst syntaxTree)
        {
            string masked = MaskCommentsAndStrings(source);
            if (!HasBalancedBraces(masked))
            {
                AppendFailure(failures, relativePath, 0, "unbalanced braces in guarded source");
                syntaxTree = default(SourceFileAst);
                return false;
            }

            List<SourceMethod> methods = new List<SourceMethod>(128);
            int methodSearchIndex = 0;
            while (TryReadNextMethod(masked, methodSearchIndex, out SourceMethod method))
            {
                methodSearchIndex = method.BodyEndExclusive;
                methods.Add(method);
            }

            syntaxTree = new SourceFileAst(masked, methods);
            return true;
        }

        private static void ScanHotLookups(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int hotLookupViolations)
        {
            for (int i = 0; i < HotLookupTokens.Length; i++)
            {
                int hit = IndexOf(masked, HotLookupTokens[i], method.BodyStartInclusive, method.BodyEndExclusive);
                if (hit < 0)
                    continue;

                hotLookupViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, hit), "hot method " + method.Name + " contains " + HotLookupTokens[i].Trim());
            }
        }

        private static void ScanPresentationWrites(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int phaseViolations)
        {
            for (int i = 0; i < PresentationTokens.Length; i++)
            {
                int hit = IndexOf(masked, PresentationTokens[i], method.BodyStartInclusive, method.BodyEndExclusive);
                if (hit < 0)
                    continue;

                phaseViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, hit), "presentation mutation outside visual phase in " + method.Name + ": " + PresentationTokens[i]);
            }
        }

        private static void ScanVaultWriteLocks(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int vaultLockViolations)
        {
            int acquireCount = CountOccurrences(masked, "TryAcquireWriteLock", method.BodyStartInclusive, method.BodyEndExclusive);
            if (acquireCount == 0)
                return;

            if (acquireCount > 1)
            {
                vaultLockViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, method.DeclarationStart), "method " + method.Name + " has " + acquireCount + " DataVault write-lock acquisitions");
            }

            int finallyIndex = IndexOf(masked, "finally", method.BodyStartInclusive, method.BodyEndExclusive);
            int releaseIndex = IndexOf(masked, "ReleaseWriteLock", Math.Max(finallyIndex, method.BodyStartInclusive), method.BodyEndExclusive);
            if (finallyIndex >= 0 && releaseIndex >= 0)
                return;

            vaultLockViolations++;
            AppendFailure(failures, relativePath, GetLine(masked, method.DeclarationStart), "method " + method.Name + " acquires DataVault write lock without finally ReleaseWriteLock");
        }

        private static bool TryReadNextMethod(string masked, int start, out SourceMethod method)
        {
            int search = start;
            while (search >= 0 && search < masked.Length)
            {
                int openParen = masked.IndexOf('(', search);
                if (openParen < 0)
                    break;

                string name = ReadIdentifierBefore(masked, openParen);
                if (string.IsNullOrEmpty(name) || IsNonMethodIdentifier(name))
                {
                    search = openParen + 1;
                    continue;
                }

                int closeParen = FindMatching(masked, openParen, '(', ')');
                if (closeParen < 0)
                    break;

                int bodyStart = FindNextMethodBodyBrace(masked, closeParen + 1);
                if (bodyStart < 0)
                {
                    search = closeParen + 1;
                    continue;
                }

                int bodyEnd = FindMatching(masked, bodyStart, '{', '}');
                if (bodyEnd < 0)
                    break;

                method = new SourceMethod(name, openParen, bodyStart + 1, bodyEnd);
                return true;
            }

            method = default(SourceMethod);
            return false;
        }

        private static int FindNextMethodBodyBrace(string masked, int start)
        {
            for (int i = start; i < masked.Length; i++)
            {
                char c = masked[i];
                if (c == '{')
                    return i;

                if (c == ';' || c == '=')
                    return -1;
            }

            return -1;
        }

        private static int FindMatching(string text, int openIndex, char open, char close)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                if (text[i] == open)
                    depth++;
                else if (text[i] == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string ReadIdentifierBefore(string text, int index)
        {
            int end = index - 1;
            while (end >= 0 && char.IsWhiteSpace(text[end]))
                end--;

            if (end < 0 || (!char.IsLetterOrDigit(text[end]) && text[end] != '_'))
                return string.Empty;

            int start = end;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
                start--;

            return text.Substring(start + 1, end - start);
        }

        private static bool IsNonMethodIdentifier(string name)
        {
            return string.Equals(name, "catch", StringComparison.Ordinal) ||
                   string.Equals(name, "for", StringComparison.Ordinal) ||
                   string.Equals(name, "foreach", StringComparison.Ordinal) ||
                   string.Equals(name, "if", StringComparison.Ordinal) ||
                   string.Equals(name, "lock", StringComparison.Ordinal) ||
                   string.Equals(name, "return", StringComparison.Ordinal) ||
                   string.Equals(name, "switch", StringComparison.Ordinal) ||
                   string.Equals(name, "using", StringComparison.Ordinal) ||
                   string.Equals(name, "while", StringComparison.Ordinal);
        }

        private static string MaskCommentsAndStrings(string source)
        {
            char[] buffer = source.ToCharArray();
            int i = 0;
            while (i < buffer.Length)
            {
                char c = buffer[i];
                if (c == '/' && i + 1 < buffer.Length && buffer[i + 1] == '/')
                {
                    buffer[i++] = ' ';
                    buffer[i++] = ' ';
                    while (i < buffer.Length && buffer[i] != '\n')
                        buffer[i++] = ' ';
                    continue;
                }

                if (c == '/' && i + 1 < buffer.Length && buffer[i + 1] == '*')
                {
                    buffer[i++] = ' ';
                    buffer[i++] = ' ';
                    while (i + 1 < buffer.Length && !(buffer[i] == '*' && buffer[i + 1] == '/'))
                    {
                        if (buffer[i] != '\n' && buffer[i] != '\r')
                            buffer[i] = ' ';
                        i++;
                    }

                    if (i + 1 < buffer.Length)
                    {
                        buffer[i++] = ' ';
                        buffer[i++] = ' ';
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    buffer[i++] = ' ';
                    while (i < buffer.Length)
                    {
                        bool escaped = buffer[i] == '\\';
                        if (buffer[i] != '\n' && buffer[i] != '\r')
                            buffer[i] = ' ';

                        if (escaped && i + 1 < buffer.Length)
                        {
                            i++;
                            if (buffer[i] != '\n' && buffer[i] != '\r')
                                buffer[i] = ' ';
                            i++;
                            continue;
                        }

                        if (source[i] == quote)
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                i++;
            }

            return new string(buffer);
        }

        private static bool HasBalancedBraces(string text)
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                    depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth < 0)
                        return false;
                }
            }

            return depth == 0;
        }

        private static int CountLiteralBuildTokens(string source)
        {
            return source.IndexOf("dotnet" + " build", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
        }

        private static int CountOccurrences(string text, string token, int startInclusive, int endExclusive)
        {
            int count = 0;
            int index = startInclusive;
            while (index >= 0 && index < endExclusive)
            {
                index = IndexOf(text, token, index, endExclusive);
                if (index < 0)
                    break;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static int IndexOf(string text, string token, int startInclusive, int endExclusive)
        {
            int index = text.IndexOf(token, startInclusive, StringComparison.Ordinal);
            return index >= 0 && index < endExclusive ? index : -1;
        }

        private static int GetLine(string text, int index)
        {
            int line = 1;
            int end = Math.Min(index, text.Length);
            for (int i = 0; i < end; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static void AppendFailure(List<string> failures, string relativePath, int line, string message)
        {
            failures.Add(line > 0
                ? relativePath + ":" + line + " " + message
                : relativePath + " " + message);
        }

        private static bool IsEditorOnlyPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private readonly struct SourceMethod
        {
            public SourceMethod(string name, int declarationStart, int bodyStartInclusive, int bodyEndExclusive)
            {
                Name = name;
                DeclarationStart = declarationStart;
                BodyStartInclusive = bodyStartInclusive;
                BodyEndExclusive = bodyEndExclusive;
            }

            public string Name { get; }

            public int DeclarationStart { get; }

            public int BodyStartInclusive { get; }

            public int BodyEndExclusive { get; }
        }

        private readonly struct SourceFileAst
        {
            public SourceFileAst(string maskedSource, List<SourceMethod> methods)
            {
                MaskedSource = maskedSource;
                Methods = methods;
            }

            public string MaskedSource { get; }

            public List<SourceMethod> Methods { get; }
        }
    }

    internal readonly struct ApexIntegratorSourceGuardResult
    {
        public ApexIntegratorSourceGuardResult(bool failed, string summary)
        {
            Failed = failed;
            Summary = summary;
        }

        public bool Failed { get; }

        public string Summary { get; }
    }
}
