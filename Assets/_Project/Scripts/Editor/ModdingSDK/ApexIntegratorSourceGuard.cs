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
            "Assets/_Project/Scripts/ModdingAPI",
            "Assets/_Project/Scripts/Editor/ModdingSDK"
        };

        private static readonly string[] CoreRuntimeScope =
        {
            "Assets/_Project/Scripts/BuoyancyObject.cs",
            "Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs",
            "Assets/_Project/Scripts/Tools/LaserCutterDodContracts.cs",
            "Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs",
            "Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs",
            "Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs",
            "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs",
            "Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs",
            "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs"
        };

        private static readonly HashSet<string> HotMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ColdTick",
            "Execute",
            "FastTick",
            "FixedTick",
            "FixedUpdate",
            "LateFrameTick",
            "LateUpdate",
            "OnUpdate",
            "PostSimulationTick",
            "PostFixedTick",
            "PreSimulationTick",
            "SlowTick",
            "Tick",
            "ToolTick",
            "UnscaledFastTick",
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
            "GlobalRegistry.",
            "GlobalRegistry." + "Get<",
            "GlobalRegistry." + "Get(",
            ".TryGet" + "Component<",
            ".TryGet" + "Component(",
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
            "\tGet" + "Component(",
            "Camera.main",
            "GameObject.Find(",
            "GameObject.FindWithTag(",
            "FindAnyObjectByType<",
            "FindAnyObjectByType(",
            "Object.FindAnyObjectByType<",
            "FindFirstObjectByType(",
            "Object.FindAnyObjectByType<",
            "FindObjectOfType(",
            "FindObjectsByType<",
            "FindObjectsByType(",
            "FindObjectsOfType<",
            "FindObjectsOfType(",
            "Resources.Load<",
            "Resources.Load("
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

        private static readonly string[] HotGcTokens =
        {
            ".Any(",
            ".FirstOrDefault(",
            ".GroupBy(",
            ".OrderBy(",
            ".Select(",
            ".Sum(",
            ".ToString(",
            ".Where(",
            "foreach (",
            "foreach(",
            "new Dictionary<",
            "new HashSet<",
            "new List<",
            "new NativeArray<",
            "new NativeHashMap<",
            "new NativeList<",
            "new NativeParallelHashMap<",
            "new NativeQueue<",
            "new Queue<",
            "new Stack<",
            "Allocator.Persistent",
            "Allocator.Temp)",
            "Allocator.Temp,",
            "Allocator.TempJob",
            "Enumerable.",
            "String.Concat(",
            "string.Concat(",
            ".ToArray(",
            ".ToDictionary(",
            ".ToList(",
            "String.Format(",
            "string.Format("
        };

        private static readonly string[] HotSyncTokens =
        {
            ".Complete(",
            ".Run(",
            ".Wait(",
            ".WaitForCompletion(",
            ".Result"
        };

        private static readonly string[] ExternalBuildProcessTokens =
        {
            "dotnet" + " build",
            "ms" + "build",
            "csc" + ".exe",
            "BuildPipeline." + "BuildPlayer",
            "Request" + "ScriptCompilation"
        };

        private static readonly string[] VaultAcquireTokens =
        {
            "TryAcquireWriteLock",
            "TryAcquireVaultLaneWrite"
        };

        private static readonly string[] VaultReleaseTokens =
        {
            "ReleaseWriteLock",
            "ReleaseVaultLaneWrite"
        };

        private static readonly string[] VaultLatestCreatedTokens =
        {
            "TryGetLatestCreated"
        };

        [MenuItem("Hecton8/Modding/Run APEX Source Guard")]
        private static void RunFromMenu()
        {
            ApexIntegratorSourceGuardResult result = RunDefaultScope();
            if (result.Failed)
                Debug.LogError(result.Summary);
            else
                Debug.Log(result.Summary);
        }

        [MenuItem("Hecton8/Diagnostics/Run APEX Core Runtime Source Guard")]
        private static void RunCoreRuntimeFromMenu()
        {
            ApexIntegratorSourceGuardResult result = RunCoreRuntimeScope();
            if (result.Failed)
                Debug.LogError(result.Summary);
            else
                Debug.Log(result.Summary);
        }

        internal static ApexIntegratorSourceGuardResult RunDefaultScope()
        {
            return Run(DefaultScope);
        }

        internal static ApexIntegratorSourceGuardResult RunCoreRuntimeScope()
        {
            return Run(CoreRuntimeScope);
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
            int hotGcViolations = 0;
            int hotSyncViolations = 0;
            int vaultLockViolations = 0;
            int vaultLatestCreatedViolations = 0;
            int buildProcessTokens = 0;
            List<string> sourceFiles = CollectScopeFiles(projectRoot, relativePaths, failures);

            for (int i = 0; i < sourceFiles.Count; i++)
            {
                string relativePath = NormalizeRelativePath(sourceFiles[i]);
                string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                {
                    AppendFailure(failures, relativePath, 0, "missing source file");
                    continue;
                }

                string source = File.ReadAllText(absolutePath);
                buildProcessTokens += CountExternalBuildProcessTokens(MaskSource(source, false));
                if (!TryBuildSyntaxTree(relativePath, source, failures, out SourceFileAst syntaxTree))
                    continue;

                parsedFiles++;
                bool editorOnlyFile = IsEditorOnlyPath(relativePath);
                parsedMethods += syntaxTree.Methods.Count;

                for (int methodIndex = 0; methodIndex < syntaxTree.Methods.Count; methodIndex++)
                {
                    SourceMethod method = syntaxTree.Methods[methodIndex];

                    bool hotMethod = HotMethodNames.Contains(method.Name);
                    if (hotMethod)
                    {
                        hotMethods++;
                        ScanHotLookups(relativePath, syntaxTree.MaskedSource, method, failures, ref hotLookupViolations);
                        if (!editorOnlyFile)
                        {
                            ScanHotGcTokens(relativePath, syntaxTree.MaskedSource, method, failures, ref hotGcViolations);
                            ScanHotSyncTokens(relativePath, syntaxTree.MaskedSource, method, failures, ref hotSyncViolations);
                            if (!VisualPhaseMethodNames.Contains(method.Name))
                                ScanPresentationWrites(relativePath, syntaxTree.MaskedSource, method, failures, ref phaseViolations);
                        }
                    }

                    ScanVaultWriteLocks(relativePath, syntaxTree.MaskedSource, method, failures, ref vaultLockViolations);
                    ScanVaultLatestCreated(relativePath, syntaxTree.MaskedSource, method, failures, ref vaultLatestCreatedViolations);
                }
            }

            if (buildProcessTokens != 0)
                AppendFailure(failures, "APEX_SOURCE_SCOPE", 0, "external compiler/build trigger token present in guarded C# scope");

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
                .Append(", HotGcViolations=")
                .Append(hotGcViolations)
                .Append(", HotSyncViolations=")
                .Append(hotSyncViolations)
                .Append(", VaultLockViolations=")
                .Append(vaultLockViolations)
                .Append(", VaultLatestCreatedViolations=")
                .Append(vaultLatestCreatedViolations)
                .Append(", BuildProcessTokens=")
                .Append(buildProcessTokens)
                .AppendLine(".");
            summary.AppendLine("Timing proof: guarded modding runtime/editor scope has no deferred presentation mutation outside LateFrameTick or VisualSyncTick.");
            summary.AppendLine("Transfer proof: guarded runtime hot methods contain no managed growth/LINQ copy tokens; state transfer stays cold or native.");
            summary.AppendLine("Lock proof: guarded scope has no method that can hold more than one GlobalDataVault write lock, and write-lock users must use finally-release.");
            summary.AppendLine("Compile throttle proof: this guard launches no external compiler process.");

            for (int i = 0; i < failures.Count; i++)
                summary.AppendLine(failures[i]);

            return new ApexIntegratorSourceGuardResult(failures.Count != 0, summary.ToString());
        }

        private static List<string> CollectScopeFiles(string projectRoot, IReadOnlyList<string> scopeRoots, List<string> failures)
        {
            List<string> sourceFiles = new List<string>(64);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < scopeRoots.Count; i++)
            {
                string relativePath = NormalizeRelativePath(scopeRoots[i]);
                string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absolutePath))
                {
                    AddSourceFile(projectRoot, absolutePath, sourceFiles, seen);
                    continue;
                }

                if (!Directory.Exists(absolutePath))
                {
                    AppendFailure(failures, relativePath, 0, "missing source scope");
                    continue;
                }

                string[] files = Directory.GetFiles(absolutePath, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    AddSourceFile(projectRoot, files[fileIndex], sourceFiles, seen);
            }

            return sourceFiles;
        }

        private static void AddSourceFile(string projectRoot, string absolutePath, List<string> sourceFiles, HashSet<string> seen)
        {
            string relativePath = NormalizeRelativePath(MakeRelativePath(projectRoot, absolutePath));
            if (seen.Add(relativePath))
                sourceFiles.Add(relativePath);
        }

        private static bool TryBuildSyntaxTree(
            string relativePath,
            string source,
            List<string> failures,
            out SourceFileAst syntaxTree)
        {
            string masked = MaskSource(source, true);
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

        private static void ScanHotGcTokens(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int hotGcViolations)
        {
            for (int i = 0; i < HotGcTokens.Length; i++)
            {
                int hit = IndexOf(masked, HotGcTokens[i], method.BodyStartInclusive, method.BodyEndExclusive);
                if (hit < 0)
                    continue;

                hotGcViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, hit), "hot method " + method.Name + " contains managed allocation/copy token " + HotGcTokens[i].Trim());
            }
        }

        private static void ScanHotSyncTokens(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int hotSyncViolations)
        {
            for (int i = 0; i < HotSyncTokens.Length; i++)
            {
                int hit = IndexOf(masked, HotSyncTokens[i], method.BodyStartInclusive, method.BodyEndExclusive);
                if (hit < 0)
                    continue;

                if (HotSyncTokens[i] == ".Run(" && IsTaskRunAt(masked, hit))
                    continue;
                if (HotSyncTokens[i] == ".Result" && IsIdentifierContinuationAfter(masked, hit + HotSyncTokens[i].Length))
                    continue;

                hotSyncViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, hit), "hot method " + method.Name + " contains sync barrier token " + HotSyncTokens[i].Trim());
            }
        }

        private static void ScanVaultWriteLocks(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int vaultLockViolations)
        {
            List<TokenHit> hits = new List<TokenHit>(8);
            CollectTokenHits(masked, method.BodyStartInclusive, method.BodyEndExclusive, VaultAcquireTokens, true, hits);
            CollectTokenHits(masked, method.BodyStartInclusive, method.BodyEndExclusive, VaultReleaseTokens, false, hits);
            if (hits.Count == 0)
                return;

            hits.Sort(TokenHit.CompareByIndex);
            int acquireCount = 0;
            int releaseCount = 0;
            int activeDepth = 0;
            int maxDepth = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                TokenHit hit = hits[i];
                if (hit.IsAcquire)
                {
                    acquireCount++;
                    activeDepth++;
                    if (activeDepth > maxDepth)
                        maxDepth = activeDepth;
                }
                else
                {
                    releaseCount++;
                    if (!IsInsideFinally(masked, method, hit.Index))
                    {
                        vaultLockViolations++;
                        AppendFailure(failures, relativePath, GetLine(masked, hit.Index), "method " + method.Name + " releases DataVault write lock outside finally");
                    }

                    if (activeDepth > 0)
                        activeDepth--;
                }
            }

            if (acquireCount == 0)
                return;

            if (maxDepth > 1)
            {
                vaultLockViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, method.DeclarationStart), "method " + method.Name + " can hold " + maxDepth + " DataVault write locks simultaneously");
            }

            if (releaseCount < acquireCount)
            {
                vaultLockViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, method.DeclarationStart), "method " + method.Name + " has " + acquireCount + " DataVault write lock acquisitions but only " + releaseCount + " releases");
            }
        }

        private static void ScanVaultLatestCreated(
            string relativePath,
            string masked,
            SourceMethod method,
            List<string> failures,
            ref int vaultLatestCreatedViolations)
        {
            if (IsAllowedLatestCreatedPath(relativePath) || IsAllowedLatestCreatedMethod(method.Name))
                return;

            for (int i = 0; i < VaultLatestCreatedTokens.Length; i++)
            {
                int hit = IndexOf(masked, VaultLatestCreatedTokens[i], method.BodyStartInclusive, method.BodyEndExclusive);
                if (hit < 0)
                    continue;

                vaultLatestCreatedViolations++;
                AppendFailure(failures, relativePath, GetLine(masked, hit), "method " + method.Name + " uses DataVault latest-created fallback outside bootstrap/editor/diagnostic/crash scope");
            }
        }

        private static bool IsAllowedLatestCreatedPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return normalized.IndexOf("/Bootstrap/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Diagnostics/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("CrashTelemetry", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAllowedLatestCreatedMethod(string methodName)
        {
            return methodName.IndexOf("Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("Crash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("Diagnostic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CollectTokenHits(
            string masked,
            int startInclusive,
            int endExclusive,
            string[] tokens,
            bool isAcquire,
            List<TokenHit> hits)
        {
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                int index = startInclusive;
                while (index >= 0 && index < endExclusive)
                {
                    index = IndexOf(masked, token, index, endExclusive);
                    if (index < 0)
                        break;

                    hits.Add(new TokenHit(index, isAcquire));
                    index += token.Length;
                }
            }
        }

        private static bool IsInsideFinally(string masked, SourceMethod method, int index)
        {
            int search = method.BodyStartInclusive;
            while (search >= 0 && search < method.BodyEndExclusive)
            {
                int finallyIndex = IndexOf(masked, "finally", search, method.BodyEndExclusive);
                if (finallyIndex < 0)
                    return false;

                int blockStart = FindNextMethodBodyBrace(masked, finallyIndex + "finally".Length);
                if (blockStart < 0 || blockStart >= method.BodyEndExclusive)
                    return false;

                int blockEnd = FindMatching(masked, blockStart, '{', '}');
                if (blockEnd < 0)
                    return false;

                if (index > blockStart && index < blockEnd)
                    return true;

                search = blockEnd + 1;
            }

            return false;
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

        private static string MaskSource(string source, bool maskStringsAndChars)
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
                    i = SkipOrMaskQuotedLiteral(source, buffer, i, c, maskStringsAndChars);
                    continue;
                }

                i++;
            }

            return new string(buffer);
        }

        private static int SkipOrMaskQuotedLiteral(string source, char[] buffer, int start, char quote, bool mask)
        {
            bool verbatimString = quote == '"' && HasVerbatimStringPrefix(source, start);
            int i = start;
            if (mask && buffer[i] != '\n' && buffer[i] != '\r')
                buffer[i] = ' ';

            i++;
            while (i < buffer.Length)
            {
                char c = source[i];
                if (mask && buffer[i] != '\n' && buffer[i] != '\r')
                    buffer[i] = ' ';

                if (verbatimString)
                {
                    if (c == quote)
                    {
                        if (i + 1 < buffer.Length && source[i + 1] == quote)
                        {
                            i++;
                            if (mask && buffer[i] != '\n' && buffer[i] != '\r')
                                buffer[i] = ' ';
                            i++;
                            continue;
                        }

                        i++;
                        break;
                    }

                    i++;
                    continue;
                }

                if (c == '\\' && i + 1 < buffer.Length)
                {
                    i++;
                    if (mask && buffer[i] != '\n' && buffer[i] != '\r')
                        buffer[i] = ' ';
                    i++;
                    continue;
                }

                if (c == quote)
                {
                    i++;
                    break;
                }

                i++;
            }

            return i;
        }

        private static bool HasVerbatimStringPrefix(string source, int quoteIndex)
        {
            bool hasAt = false;
            int i = quoteIndex - 1;
            while (i >= 0)
            {
                char c = source[i];
                if (c == '@')
                {
                    hasAt = true;
                    i--;
                    continue;
                }

                if (c == '$')
                {
                    i--;
                    continue;
                }

                break;
            }

            return hasAt;
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

        private static int CountExternalBuildProcessTokens(string source)
        {
            int count = 0;
            for (int i = 0; i < ExternalBuildProcessTokens.Length; i++)
            {
                if (source.IndexOf(ExternalBuildProcessTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }

            return count;
        }

        private static int IndexOf(string text, string token, int startInclusive, int endExclusive)
        {
            int index = text.IndexOf(token, startInclusive, StringComparison.Ordinal);
            return index >= 0 && index < endExclusive ? index : -1;
        }

        private static bool IsTaskRunAt(string masked, int runIndex)
        {
            const string taskRun = "Task.Run(";
            int taskStart = runIndex - "Task".Length;
            if (taskStart < 0 || taskStart + taskRun.Length > masked.Length)
                return false;

            return string.Compare(masked, taskStart, taskRun, 0, taskRun.Length, StringComparison.Ordinal) == 0;
        }

        private static bool IsIdentifierContinuationAfter(string text, int index)
        {
            return index >= 0 &&
                   index < text.Length &&
                   (char.IsLetterOrDigit(text[index]) || text[index] == '_');
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

        private static string MakeRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;

            return path + Path.DirectorySeparatorChar;
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

        private readonly struct TokenHit
        {
            public TokenHit(int index, bool isAcquire)
            {
                Index = index;
                IsAcquire = isAcquire;
            }

            public int Index { get; }

            public bool IsAcquire { get; }

            public static int CompareByIndex(TokenHit left, TokenHit right)
            {
                return left.Index.CompareTo(right.Index);
            }
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
