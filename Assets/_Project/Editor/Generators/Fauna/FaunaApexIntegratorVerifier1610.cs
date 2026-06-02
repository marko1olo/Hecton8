#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools.Generators.Fauna
{
    internal static class FaunaApexIntegratorVerifier1610
    {
        private const int MaxHotStackAllocBytes = 256;

        private static readonly string[] RuntimeRoots =
        {
            "Assets/_Project/Scripts/Fauna",
            "Assets/_Project/Scripts/Animation/FaunaProcedural"
        };

        private static readonly string[] EditorRoots =
        {
            "Assets/_Project/Editor/Generators/Fauna"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "VisualSync",
            "VISUAL_SYNC",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute"
        };

        private static readonly string[] PresentationTokens =
        {
            "Shader.SetGlobal",
            "SetGlobalFloat",
            "SetGlobalVector",
            "SetGlobalVectorArray",
            "SetGlobalMatrix",
            "SetGlobalTexture",
            "SetGlobalBuffer",
            "SetGlobalConstantBuffer",
            ".SetBuffer",
            ".SetMatrix",
            ".SetVector",
            ".SetFloat",
            ".SetColor",
            ".SetTexture",
            ".SetInt",
            ".EnableKeyword",
            ".DisableKeyword",
            ".SetData",
            "Graphics.Draw",
            "Graphics.Render",
            "RenderMeshIndirect",
            "LockBufferForWrite",
            "UnlockBufferAfterWrite"
        };

        private static readonly string[] PresentationAssignmentTokens =
        {
            ".intensity",
            ".enabled",
            ".material"
        };

        [MenuItem("HECTON-8/Fauna/1610 APEX Integrator Verify Source")]
        public static void RunMenu()
        {
            SourceAuditResult result = RunInMemorySourceAudit();
            if (result.Violations.Count > 0)
            {
                Debug.LogError("[FaunaApex1610] APEX AST verification failed. violations=" +
                               result.Violations.Count.ToString() + "\n" +
                               string.Join("\n", result.Violations));
                return;
            }

            Debug.Log("[FaunaApex1610] APEX AST verification passed. runtimeFiles=" +
                      result.RuntimeFiles.ToString() +
                      " editorFiles=" + result.EditorFiles.ToString() +
                      " syntaxTrees=" + result.SyntaxTrees.ToString() +
                      " hotMethods=" + result.HotMethods.ToString() +
                      " writeLockMethods=" + result.WriteLockMethods.ToString() +
                      " hotRuntimeTrapMethods=" + result.HotRuntimeTrapMethods.ToString() +
                      " hotSynchronizationMethods=" + result.HotSynchronizationMethods.ToString() +
                      " presentationMethods=" + result.PresentationMethods.ToString());
        }

        public static SourceAuditResult RunInMemorySourceAudit()
        {
            SourceAuditResult result = new SourceAuditResult();
            AuditRoots(RuntimeRoots, true, result);
            AuditRuntimeTransitiveSource(result);
            AuditRoots(EditorRoots, false, result);
            return result;
        }

        private static void AuditRoots(string[] roots, bool runtime, SourceAuditResult result)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int j = 0; j < files.Length; j++)
                {
                    if (runtime)
                        result.RuntimeFiles++;
                    else
                        result.EditorFiles++;

                    AuditFile(files[j].Replace('\\', '/'), runtime, result);
                }
            }
        }

        private static void AuditFile(string path, bool runtime, SourceAuditResult result)
        {
            string source = File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            result.SyntaxTrees++;
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    Diagnostic diagnostic = diagnostics.Current;
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                        continue;

                    result.Violations.Add(path + "::RoslynParse " + diagnostic.ToString());
                    return;
                }
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            List<LineRange> editorOnlyRanges = runtime ? BuildEditorOnlyLineRanges(source) : null;
            using (IEnumerator<MethodDeclarationSyntax> methodEnumerator = root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methodEnumerator.MoveNext())
                {
                    MethodDeclarationSyntax method = methodEnumerator.Current;
                    if (runtime && IsInsideEditorOnlyRegion(method, tree, editorOnlyRanges))
                        continue;

                    SourceMethod sourceMethod = new SourceMethod(path, method);
                    if (runtime)
                        result.RuntimeMethods.Add(sourceMethod);

                    if (IsHotMethod(sourceMethod.Name))
                    {
                        result.HotMethods++;
                        AuditDirectHotLookup(path, sourceMethod, result);
                    }

                    AuditDataVaultLocks(path, sourceMethod, result);
                }
            }
        }

        private static void AuditRuntimeTransitiveSource(SourceAuditResult result)
        {
            AuditTransitiveHotLookups(result.RuntimeMethods, result);
            AuditTransitiveHotAllocations(result.RuntimeMethods, result);
            AuditTransitiveHotRuntimeTraps(result.RuntimeMethods, result);
            AuditTransitiveHotSynchronizationStalls(result.RuntimeMethods, result);
            AuditPresentationPhase(result.RuntimeMethods, result);
        }

        private static void AuditDirectHotLookup(string path, SourceMethod method, SourceAuditResult result)
        {
            using (IEnumerator<InvocationExpressionSyntax> invocations = method.Method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    string expression = invocations.Current.Expression.ToString();
                    if (IsGlobalRegistryLookup(expression))
                    {
                        result.Violations.Add(path + "::" + method.Name +
                                              " reads GlobalRegistry inside a high-frequency method.");
                    }

                    if (IsComponentLookup(expression))
                    {
                        result.Violations.Add(path + "::" + method.Name +
                                              " performs component lookup inside a high-frequency method.");
                    }
                }
            }

            using (IEnumerator<MemberAccessExpressionSyntax> memberAccesses = method.Method.DescendantNodes().OfType<MemberAccessExpressionSyntax>().GetEnumerator())
            {
                while (memberAccesses.MoveNext())
                {
                    if (IsGlobalRegistryMemberAccess(memberAccesses.Current))
                    {
                        result.Violations.Add(path + "::" + method.Name +
                                              " reads GlobalRegistry member inside a high-frequency method.");
                    }
                }
            }
        }

        private static List<LineRange> BuildEditorOnlyLineRanges(string source)
        {
            List<LineRange> ranges = new List<LineRange>(8);
            Stack<ConditionalFrame> frames = new Stack<ConditionalFrame>(4);
            string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                string line = lines[i].Trim();
                if (line.StartsWith("#if ", StringComparison.Ordinal))
                {
                    string condition = line.Substring(4).Trim();
                    frames.Push(new ConditionalFrame(IsEditorOnlyCondition(condition), lineNumber + 1));
                    continue;
                }

                if (line.StartsWith("#elif ", StringComparison.Ordinal))
                {
                    if (frames.Count <= 0)
                        continue;

                    ConditionalFrame frame = frames.Pop();
                    if (frame.EditorOnly)
                        ranges.Add(new LineRange(frame.StartLine, lineNumber - 1));

                    string condition = line.Substring(6).Trim();
                    frames.Push(new ConditionalFrame(IsEditorOnlyCondition(condition), lineNumber + 1));
                    continue;
                }

                if (line.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (frames.Count <= 0)
                        continue;

                    ConditionalFrame frame = frames.Pop();
                    if (frame.EditorOnly)
                        ranges.Add(new LineRange(frame.StartLine, lineNumber - 1));

                    frames.Push(new ConditionalFrame(false, lineNumber + 1));
                    continue;
                }

                if (!line.StartsWith("#endif", StringComparison.Ordinal) || frames.Count <= 0)
                    continue;

                ConditionalFrame closingFrame = frames.Pop();
                if (closingFrame.EditorOnly)
                    ranges.Add(new LineRange(closingFrame.StartLine, lineNumber - 1));
            }

            while (frames.Count > 0)
            {
                ConditionalFrame frame = frames.Pop();
                if (frame.EditorOnly)
                    ranges.Add(new LineRange(frame.StartLine, lines.Length));
            }

            return ranges;
        }

        private static bool IsEditorOnlyCondition(string condition)
        {
            return condition.IndexOf("UNITY_EDITOR", StringComparison.Ordinal) >= 0 &&
                   condition.IndexOf("||", StringComparison.Ordinal) < 0;
        }

        private static bool IsInsideEditorOnlyRegion(
            MethodDeclarationSyntax method,
            SyntaxTree tree,
            List<LineRange> ranges)
        {
            if (ranges == null || ranges.Count <= 0)
                return false;

            int line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].Contains(line))
                    return true;
            }

            return false;
        }

        private static void AuditDataVaultLocks(string path, SourceMethod method, SourceAuditResult result)
        {
            List<InvocationExpressionSyntax> acquireInvocations = null;
            using (IEnumerator<InvocationExpressionSyntax> invocations = method.Method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    string expression = invocation.Expression.ToString();
                    if (expression.EndsWith("TryAcquireWriteLock", StringComparison.Ordinal))
                    {
                        if (acquireInvocations == null)
                            acquireInvocations = new List<InvocationExpressionSyntax>(1);
                        acquireInvocations.Add(invocation);
                    }
                    else if (expression.EndsWith("TryLockBuffer", StringComparison.Ordinal) ||
                             expression.EndsWith("TryUnlockBuffer", StringComparison.Ordinal))
                    {
                        result.Violations.Add(path + "::" + method.Name +
                                              " uses legacy buffer locks instead of a flattened mutation guard.");
                    }
                }
            }

            if (acquireInvocations == null || acquireInvocations.Count <= 0)
                return;

            result.WriteLockMethods++;
            if (acquireInvocations.Count > 1)
            {
                result.Violations.Add(path + "::" + method.Name +
                                      " acquires more than one DataVault write lock.");
            }

            for (int i = 0; i < acquireInvocations.Count; i++)
            {
                if (!HasStrictFinallyReleaseForAcquire(acquireInvocations[i]))
                {
                    result.Violations.Add(path + "::" + method.Name +
                                          " acquires a DataVault write lock without matching adjacent local finally release.");
                }
            }
        }

        private static bool HasStrictFinallyReleaseForAcquire(InvocationExpressionSyntax acquireInvocation)
        {
            using (IEnumerator<TryStatementSyntax> tryAncestors = acquireInvocation.Ancestors().OfType<TryStatementSyntax>().GetEnumerator())
            {
                while (tryAncestors.MoveNext())
                {
                    TryStatementSyntax tryStatement = tryAncestors.Current;
                    if (tryStatement.Block != null &&
                        tryStatement.Block.Span.Contains(acquireInvocation.SpanStart) &&
                        FinallyContainsMatchingWriteLockRelease(tryStatement.Finally, acquireInvocation))
                    {
                        return true;
                    }
                }
            }

            StatementSyntax acquireStatement = acquireInvocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
            BlockSyntax parentBlock = acquireStatement != null ? acquireStatement.Parent as BlockSyntax : null;
            if (parentBlock == null)
                return false;

            SyntaxList<StatementSyntax> statements = parentBlock.Statements;
            for (int i = 0; i < statements.Count - 1; i++)
            {
                if (!ReferenceEquals(statements[i], acquireStatement))
                    continue;

                for (int j = i + 1; j < statements.Count; j++)
                {
                    if (statements[j] is EmptyStatementSyntax)
                        continue;

                    TryStatementSyntax nextTry = statements[j] as TryStatementSyntax;
                    return nextTry != null && FinallyContainsMatchingWriteLockRelease(nextTry.Finally, acquireInvocation);
                }
            }

            return false;
        }

        private static bool FinallyContainsMatchingWriteLockRelease(FinallyClauseSyntax finallyClause, InvocationExpressionSyntax acquireInvocation)
        {
            if (finallyClause == null || !TryBuildWriteLockKey(acquireInvocation, out string acquireKey))
                return false;

            using (IEnumerator<InvocationExpressionSyntax> invocations = finallyClause.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    string expression = invocation.Expression.ToString();
                    if (expression.EndsWith("ReleaseWriteLock", StringComparison.Ordinal) &&
                        TryBuildWriteLockKey(invocation, out string releaseKey) &&
                        string.Equals(acquireKey, releaseKey, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryBuildWriteLockKey(InvocationExpressionSyntax invocation, out string key)
        {
            key = string.Empty;
            if (invocation.ArgumentList == null || invocation.ArgumentList.Arguments.Count < 2)
                return false;

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
            key = NormalizeLockArgument(arguments[0]) + "|" + NormalizeLockArgument(arguments[1]);
            return key.Length > 1;
        }

        private static string NormalizeLockArgument(ArgumentSyntax argument)
        {
            if (argument == null || argument.Expression == null)
                return string.Empty;

            return argument.Expression.ToString().Replace(" ", string.Empty);
        }

        private static void AuditPresentationPhase(List<SourceMethod> methods, SourceAuditResult result)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod method = methods[i];
                method.HasPresentationCall = HasPresentationCall(method.Method);
                if (method.HasPresentationCall)
                    result.PresentationMethods++;
            }

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod caller = methods[i];
                if (!IsHotMethod(caller.Name) || IsPresentationPhase(caller.Name))
                    continue;

                if (MethodReachesPresentationCall(caller, methods, new HashSet<string>(StringComparer.Ordinal)))
                {
                    result.Violations.Add(caller.Path + "::" + caller.Name +
                                          " reaches presentation-side GPU or material write outside LateFrameTick/VISUAL_SYNC.");
                }
            }
        }

        private static void AuditTransitiveHotLookups(List<SourceMethod> methods, SourceAuditResult result)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod caller = methods[i];
                if (!IsHotMethod(caller.Name))
                    continue;

                SourceMethod dependency = MethodReachesHotDependencyLookup(caller, methods, new HashSet<string>(StringComparer.Ordinal));
                if (dependency != null && !ReferenceEquals(caller, dependency))
                {
                    result.Violations.Add(caller.Path + "::" + caller.Name +
                                          " reaches " + dependency.Name +
                                          " in " + dependency.DeclaringTypePath +
                                          ", which performs hot-forbidden dependency lookup.");
                }
            }
        }

        private static void AuditTransitiveHotAllocations(List<SourceMethod> methods, SourceAuditResult result)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod caller = methods[i];
                if (!IsHotMethod(caller.Name))
                    continue;

                SourceMethod allocation = MethodReachesManagedAllocation(caller, methods, new HashSet<string>(StringComparer.Ordinal));
                if (allocation != null)
                {
                    result.Violations.Add(caller.Path + "::" + caller.Name +
                                          " reaches " + allocation.Name +
                                          " in " + allocation.DeclaringTypePath +
                                          ", which performs managed allocation in a hot phase route.");
                }
            }
        }

        private static void AuditTransitiveHotRuntimeTraps(List<SourceMethod> methods, SourceAuditResult result)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod caller = methods[i];
                if (!IsHotMethod(caller.Name))
                    continue;

                SourceMethod trap = MethodReachesHotRuntimeTrap(caller, methods, new HashSet<string>(StringComparer.Ordinal));
                if (trap != null)
                {
                    result.HotRuntimeTrapMethods++;
                    result.Violations.Add(caller.Path + "::" + caller.Name +
                                          " reaches " + trap.Name +
                                          " in " + trap.DeclaringTypePath +
                                          ", which calls a Unity scene/search/load/coroutine/property-copy API in a hot phase route.");
                }
            }
        }

        private static void AuditTransitiveHotSynchronizationStalls(List<SourceMethod> methods, SourceAuditResult result)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod caller = methods[i];
                if (!IsHotMethod(caller.Name))
                    continue;

                SourceMethod stall = MethodReachesHotSynchronizationStall(caller, methods, new HashSet<string>(StringComparer.Ordinal));
                if (stall != null)
                {
                    result.HotSynchronizationMethods++;
                    result.Violations.Add(caller.Path + "::" + caller.Name +
                                          " reaches " + stall.Name +
                                          " in " + stall.DeclaringTypePath +
                                          ", which calls a blocking job/completion fence in a hot phase route.");
                }
            }
        }

        private static bool HasPresentationCall(MethodDeclarationSyntax method)
        {
            using (IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    string expression = invocations.Current.Expression.ToString();
                    for (int i = 0; i < PresentationTokens.Length; i++)
                    {
                        if (expression.IndexOf(PresentationTokens[i], StringComparison.Ordinal) >= 0)
                            return true;
                    }
                }
            }

            using (IEnumerator<AssignmentExpressionSyntax> assignments = method.DescendantNodes().OfType<AssignmentExpressionSyntax>().GetEnumerator())
            {
                while (assignments.MoveNext())
                {
                    string left = assignments.Current.Left.ToString();
                    for (int i = 0; i < PresentationAssignmentTokens.Length; i++)
                    {
                        if (left.IndexOf(PresentationAssignmentTokens[i], StringComparison.Ordinal) >= 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool CallsMethod(SourceMethod caller, SourceMethod callee)
        {
            if (!CanResolveCallWithoutSemantic(caller, callee))
                return false;

            using (IEnumerator<InvocationExpressionSyntax> invocations = caller.Method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    string expression = invocation.Expression.ToString();
                    if (string.Equals(expression, callee.Name, StringComparison.Ordinal) ||
                        expression.EndsWith("." + callee.Name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CanResolveCallWithoutSemantic(SourceMethod caller, SourceMethod callee)
        {
            if (string.Equals(caller.DeclaringTypePath, callee.DeclaringTypePath, StringComparison.Ordinal))
                return true;

            if (string.IsNullOrEmpty(caller.DeclaringTypePath) || string.IsNullOrEmpty(callee.DeclaringTypePath))
                return false;

            return caller.DeclaringTypePath.StartsWith(callee.DeclaringTypePath + ".", StringComparison.Ordinal) ||
                   callee.DeclaringTypePath.StartsWith(caller.DeclaringTypePath + ".", StringComparison.Ordinal);
        }

        private static bool MethodReachesPresentationCall(
            SourceMethod method,
            List<SourceMethod> methods,
            HashSet<string> visited)
        {
            if (method.HasPresentationCall)
                return true;

            if (!visited.Add(method.DeclaringTypePath + "." + method.Name))
                return false;

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod callee = methods[i];
                if (ReferenceEquals(method, callee) || !CallsMethod(method, callee))
                    continue;

                if (MethodReachesPresentationCall(callee, methods, visited))
                    return true;
            }

            return false;
        }

        private static SourceMethod MethodReachesHotDependencyLookup(
            SourceMethod method,
            List<SourceMethod> methods,
            HashSet<string> visited)
        {
            if (method.HasHotForbiddenDependencyLookup)
                return method;

            if (!visited.Add(method.DeclaringTypePath + "." + method.Name))
                return null;

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod callee = methods[i];
                if (ReferenceEquals(method, callee) || !CallsMethod(method, callee))
                    continue;

                SourceMethod dependency = MethodReachesHotDependencyLookup(callee, methods, visited);
                if (dependency != null)
                    return dependency;
            }

            return null;
        }

        private static SourceMethod MethodReachesManagedAllocation(
            SourceMethod method,
            List<SourceMethod> methods,
            HashSet<string> visited)
        {
            if (method.HasManagedAllocation)
                return method;

            if (!visited.Add(method.DeclaringTypePath + "." + method.Name))
                return null;

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod callee = methods[i];
                if (ReferenceEquals(method, callee) || !CallsMethod(method, callee))
                    continue;

                SourceMethod allocation = MethodReachesManagedAllocation(callee, methods, visited);
                if (allocation != null)
                    return allocation;
            }

            return null;
        }

        private static SourceMethod MethodReachesHotRuntimeTrap(
            SourceMethod method,
            List<SourceMethod> methods,
            HashSet<string> visited)
        {
            if (method.HasHotRuntimeTrap)
                return method;

            if (!visited.Add(method.DeclaringTypePath + "." + method.Name))
                return null;

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod callee = methods[i];
                if (ReferenceEquals(method, callee) || !CallsMethod(method, callee))
                    continue;

                SourceMethod trap = MethodReachesHotRuntimeTrap(callee, methods, visited);
                if (trap != null)
                    return trap;
            }

            return null;
        }

        private static SourceMethod MethodReachesHotSynchronizationStall(
            SourceMethod method,
            List<SourceMethod> methods,
            HashSet<string> visited)
        {
            if (method.HasHotSynchronizationStall)
                return method;

            if (!visited.Add(method.DeclaringTypePath + "." + method.Name))
                return null;

            for (int i = 0; i < methods.Count; i++)
            {
                SourceMethod callee = methods[i];
                if (ReferenceEquals(method, callee) || !CallsMethod(method, callee))
                    continue;

                SourceMethod stall = MethodReachesHotSynchronizationStall(callee, methods, visited);
                if (stall != null)
                    return stall;
            }

            return null;
        }

        private static bool IsGlobalRegistryLookup(string expression)
        {
            expression = NormalizeGlobalRegistryExpression(expression);
            if (expression.Equals("GlobalRegistry.Get", StringComparison.Ordinal) ||
                expression.StartsWith("GlobalRegistry.Get<", StringComparison.Ordinal))
            {
                return true;
            }

            if (!expression.StartsWith("GlobalRegistry.", StringComparison.Ordinal))
                return false;

            return !IsGlobalRegistryLifecycleRoute(expression);
        }

        private static bool IsGlobalRegistryMemberAccess(MemberAccessExpressionSyntax memberAccess)
        {
            string expression = NormalizeGlobalRegistryExpression(memberAccess.Expression.ToString());
            if (!expression.Equals("GlobalRegistry", StringComparison.Ordinal))
                return false;

            return !IsGlobalRegistryLifecycleRoute(NormalizeGlobalRegistryExpression(memberAccess.ToString()));
        }

        private static bool IsGlobalRegistryLifecycleRoute(string expression)
        {
            expression = NormalizeGlobalRegistryExpression(expression);
            return expression.StartsWith("GlobalRegistry.Register", StringComparison.Ordinal) ||
                   expression.StartsWith("GlobalRegistry.TryRegister", StringComparison.Ordinal) ||
                   expression.StartsWith("GlobalRegistry.Unregister", StringComparison.Ordinal) ||
                   expression.StartsWith("GlobalRegistry.TryUnregister", StringComparison.Ordinal);
        }

        private static string NormalizeGlobalRegistryExpression(string expression)
        {
            string normalized = expression.Trim();
            if (normalized.StartsWith("global::", StringComparison.Ordinal))
                normalized = normalized.Substring("global::".Length);

            int memberIndex = normalized.IndexOf("GlobalRegistry.", StringComparison.Ordinal);
            if (memberIndex >= 0)
                return normalized.Substring(memberIndex);

            int bareIndex = normalized.LastIndexOf("GlobalRegistry", StringComparison.Ordinal);
            if (bareIndex < 0)
                return normalized;

            bool hasValidPrefix = bareIndex == 0 || normalized[bareIndex - 1] == '.';
            bool hasValidTail = bareIndex + "GlobalRegistry".Length == normalized.Length;
            return hasValidPrefix && hasValidTail ? "GlobalRegistry" : normalized;
        }

        private static bool IsComponentLookup(string expression)
        {
            return expression.Equals("GetComponent", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponent<", StringComparison.Ordinal) ||
                   expression.Equals("GetComponentInChildren", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponentInChildren<", StringComparison.Ordinal) ||
                   expression.Equals("GetComponentInParent", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponentInParent<", StringComparison.Ordinal) ||
                   expression.Equals("TryGetComponent", StringComparison.Ordinal) ||
                   expression.StartsWith("TryGetComponent<", StringComparison.Ordinal) ||
                   expression.Equals("GetComponents", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponents<", StringComparison.Ordinal) ||
                   expression.Equals("GetComponentsInChildren", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponentsInChildren<", StringComparison.Ordinal) ||
                   expression.Equals("GetComponentsInParent", StringComparison.Ordinal) ||
                   expression.StartsWith("GetComponentsInParent<", StringComparison.Ordinal) ||
                   expression.EndsWith(".GetComponent", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponent<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".GetComponentInChildren", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponentInChildren<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".GetComponentInParent", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponentInParent<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".TryGetComponent", StringComparison.Ordinal) ||
                   expression.IndexOf(".TryGetComponent<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".GetComponents", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponents<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".GetComponentsInChildren", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponentsInChildren<", StringComparison.Ordinal) >= 0 ||
                   expression.EndsWith(".GetComponentsInParent", StringComparison.Ordinal) ||
                   expression.IndexOf(".GetComponentsInParent<", StringComparison.Ordinal) >= 0;
        }

        private static bool IsHotMethod(string methodName)
        {
            for (int i = 0; i < HotMethodNames.Length; i++)
            {
                if (string.Equals(methodName, HotMethodNames[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsPresentationPhase(string methodName)
        {
            return methodName.IndexOf("LateFrameTick", StringComparison.Ordinal) >= 0 ||
                   methodName.IndexOf("VisualSync", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("VISUAL_SYNC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("Visual_Sync", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct LineRange
        {
            public readonly int StartLine;
            public readonly int EndLine;

            public LineRange(int startLine, int endLine)
            {
                StartLine = startLine;
                EndLine = endLine;
            }

            public bool Contains(int line)
            {
                return line >= StartLine && line <= EndLine;
            }
        }

        private readonly struct ConditionalFrame
        {
            public readonly bool EditorOnly;
            public readonly int StartLine;

            public ConditionalFrame(bool editorOnly, int startLine)
            {
                EditorOnly = editorOnly;
                StartLine = startLine;
            }
        }

        public sealed class SourceAuditResult
        {
            public readonly List<string> Violations = new List<string>(16);
            internal readonly List<SourceMethod> RuntimeMethods = new List<SourceMethod>(256);
            public int RuntimeFiles;
            public int EditorFiles;
            public int SyntaxTrees;
            public int HotMethods;
            public int WriteLockMethods;
            public int PresentationMethods;
            public int HotDependencyLookupMethods;
            public int HotRuntimeTrapMethods;
            public int HotSynchronizationMethods;
        }

        internal sealed class SourceMethod
        {
            public readonly string Path;
            public readonly MethodDeclarationSyntax Method;
            public readonly string Name;
            public readonly string DeclaringTypeName;
            public readonly string DeclaringTypePath;
            public readonly bool HasHotForbiddenDependencyLookup;
            public readonly bool HasManagedAllocation;
            public readonly bool HasHotRuntimeTrap;
            public readonly bool HasHotSynchronizationStall;
            public bool HasPresentationCall;

            public SourceMethod(string path, MethodDeclarationSyntax method)
            {
                Path = path;
                Method = method;
                Name = method.Identifier.ValueText;
                DeclaringTypeName = ResolveDeclaringTypeName(method);
                DeclaringTypePath = ResolveDeclaringTypePath(method);
                HasHotForbiddenDependencyLookup = ContainsForbiddenDependencyLookup(method);
                HasManagedAllocation = ContainsManagedAllocation(method);
                HasHotRuntimeTrap = ContainsHotRuntimeTrap(method);
                HasHotSynchronizationStall = ContainsHotSynchronizationStall(method);
            }

            private static string ResolveDeclaringTypeName(SyntaxNode node)
            {
                TypeDeclarationSyntax type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                return type != null ? type.Identifier.ValueText : string.Empty;
            }

            private static string ResolveDeclaringTypePath(SyntaxNode node)
            {
                TypeDeclarationSyntax[] types = node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().ToArray();
                if (types.Length <= 0)
                    return string.Empty;

                StringBuilder builder = new StringBuilder(64);
                for (int i = 0; i < types.Length; i++)
                {
                    if (i > 0)
                        builder.Append('.');
                    builder.Append(types[i].Identifier.ValueText);
                }

                return builder.ToString();
            }

            private static bool ContainsForbiddenDependencyLookup(MethodDeclarationSyntax method)
            {
                using (IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        string expression = invocations.Current.Expression.ToString();
                        if (IsGlobalRegistryLookup(expression) || IsComponentLookup(expression))
                            return true;
                    }
                }

                using (IEnumerator<MemberAccessExpressionSyntax> memberAccesses = method.DescendantNodes().OfType<MemberAccessExpressionSyntax>().GetEnumerator())
                {
                    while (memberAccesses.MoveNext())
                    {
                        if (IsGlobalRegistryMemberAccess(memberAccesses.Current))
                            return true;
                    }
                }

                return false;
            }

            private static bool ContainsHotRuntimeTrap(MethodDeclarationSyntax method)
            {
                using (IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        if (IsUnityRuntimeTrapInvocation(invocations.Current.Expression.ToString()))
                            return true;
                    }
                }

                using (IEnumerator<MemberAccessExpressionSyntax> memberAccesses = method.DescendantNodes().OfType<MemberAccessExpressionSyntax>().GetEnumerator())
                {
                    while (memberAccesses.MoveNext())
                    {
                        if (IsUnityRuntimeTrapMemberAccess(memberAccesses.Current))
                            return true;
                    }
                }

                return false;
            }

            private static bool ContainsHotSynchronizationStall(MethodDeclarationSyntax method)
            {
                using (IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        if (IsBlockingSynchronizationInvocation(invocations.Current.Expression.ToString()))
                            return true;
                    }
                }

                return false;
            }

            private static bool ContainsManagedAllocation(MethodDeclarationSyntax method)
            {
                using (IEnumerator<ArrayCreationExpressionSyntax> arrays = method.DescendantNodes().OfType<ArrayCreationExpressionSyntax>().GetEnumerator())
                {
                    if (arrays.MoveNext())
                        return true;
                }

                using (IEnumerator<ImplicitArrayCreationExpressionSyntax> arrays = method.DescendantNodes().OfType<ImplicitArrayCreationExpressionSyntax>().GetEnumerator())
                {
                    if (arrays.MoveNext())
                        return true;
                }

                using (IEnumerator<ObjectCreationExpressionSyntax> creations = method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().GetEnumerator())
                {
                    while (creations.MoveNext())
                    {
                        string typeName = creations.Current.Type.ToString();
                        if (IsManagedAllocationType(typeName))
                            return true;
                    }
                }

                using (IEnumerator<AnonymousObjectCreationExpressionSyntax> anonymousObjects = method.DescendantNodes().OfType<AnonymousObjectCreationExpressionSyntax>().GetEnumerator())
                {
                    if (anonymousObjects.MoveNext())
                        return true;
                }

                using (IEnumerator<QueryExpressionSyntax> queries = method.DescendantNodes().OfType<QueryExpressionSyntax>().GetEnumerator())
                {
                    if (queries.MoveNext())
                        return true;
                }

                using (IEnumerator<AnonymousFunctionExpressionSyntax> anonymousFunctions = method.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>().GetEnumerator())
                {
                    if (anonymousFunctions.MoveNext())
                        return true;
                }

                using (IEnumerator<ForEachStatementSyntax> foreachStatements = method.DescendantNodes().OfType<ForEachStatementSyntax>().GetEnumerator())
                {
                    if (foreachStatements.MoveNext())
                        return true;
                }

                using (IEnumerator<ForEachVariableStatementSyntax> foreachVariableStatements = method.DescendantNodes().OfType<ForEachVariableStatementSyntax>().GetEnumerator())
                {
                    if (foreachVariableStatements.MoveNext())
                        return true;
                }

                using (IEnumerator<YieldStatementSyntax> yields = method.DescendantNodes().OfType<YieldStatementSyntax>().GetEnumerator())
                {
                    if (yields.MoveNext())
                        return true;
                }

                using (IEnumerator<AwaitExpressionSyntax> awaits = method.DescendantNodes().OfType<AwaitExpressionSyntax>().GetEnumerator())
                {
                    if (awaits.MoveNext())
                        return true;
                }

                using (IEnumerator<StackAllocArrayCreationExpressionSyntax> stackAllocs = method.DescendantNodes().OfType<StackAllocArrayCreationExpressionSyntax>().GetEnumerator())
                {
                    while (stackAllocs.MoveNext())
                    {
                        if (IsUnsafeHotStackAlloc(stackAllocs.Current))
                            return true;
                    }
                }

                using (IEnumerator<ImplicitStackAllocArrayCreationExpressionSyntax> implicitStackAllocs = method.DescendantNodes().OfType<ImplicitStackAllocArrayCreationExpressionSyntax>().GetEnumerator())
                {
                    if (implicitStackAllocs.MoveNext())
                        return true;
                }

                using (IEnumerator<InterpolatedStringExpressionSyntax> strings = method.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().GetEnumerator())
                {
                    if (strings.MoveNext())
                        return true;
                }

                using (IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                {
                    while (invocations.MoveNext())
                    {
                        if (IsStringAllocationInvocation(invocations.Current) ||
                            IsLinqOrDeferredAllocationInvocation(invocations.Current))
                        {
                            return true;
                        }
                    }
                }

                using (IEnumerator<BinaryExpressionSyntax> binaries = method.DescendantNodes().OfType<BinaryExpressionSyntax>().GetEnumerator())
                {
                    while (binaries.MoveNext())
                    {
                        if (IsStringConcatCandidate(binaries.Current))
                            return true;
                    }
                }

                return false;
            }

            private static bool IsStringAllocationInvocation(InvocationExpressionSyntax invocation)
            {
                string expression = invocation.Expression.ToString();
                return string.Equals(expression, "ToString", StringComparison.Ordinal) ||
                       expression.EndsWith(".ToString", StringComparison.Ordinal) ||
                       IsStaticStringFactory(expression, "Format") ||
                       IsStaticStringFactory(expression, "Concat") ||
                       IsStaticStringFactory(expression, "Create");
            }

            private static bool IsStaticStringFactory(string expression, string memberName)
            {
                return string.Equals(expression, "string." + memberName, StringComparison.Ordinal) ||
                       string.Equals(expression, "String." + memberName, StringComparison.Ordinal) ||
                       string.Equals(expression, "System.String." + memberName, StringComparison.Ordinal);
            }

            private static bool IsLinqOrDeferredAllocationInvocation(InvocationExpressionSyntax invocation)
            {
                string expression = invocation.Expression.ToString();
                return EndsWithMemberName(expression, "Where") ||
                       EndsWithMemberName(expression, "Select") ||
                       EndsWithMemberName(expression, "SelectMany") ||
                       EndsWithMemberName(expression, "OrderBy") ||
                       EndsWithMemberName(expression, "OrderByDescending") ||
                       EndsWithMemberName(expression, "ThenBy") ||
                       EndsWithMemberName(expression, "ThenByDescending") ||
                       EndsWithMemberName(expression, "GroupBy") ||
                       EndsWithMemberName(expression, "Join") ||
                       EndsWithMemberName(expression, "GroupJoin") ||
                       EndsWithMemberName(expression, "ToList") ||
                       EndsWithMemberName(expression, "ToArray") ||
                       EndsWithMemberName(expression, "ToDictionary") ||
                       EndsWithMemberName(expression, "ToLookup") ||
                       EndsWithMemberName(expression, "Any") ||
                       EndsWithMemberName(expression, "All") ||
                       EndsWithMemberName(expression, "First") ||
                       EndsWithMemberName(expression, "FirstOrDefault") ||
                       EndsWithMemberName(expression, "Single") ||
                       EndsWithMemberName(expression, "SingleOrDefault") ||
                       EndsWithMemberName(expression, "Last") ||
                       EndsWithMemberName(expression, "LastOrDefault") ||
                       EndsWithMemberName(expression, "Count") ||
                       EndsWithMemberName(expression, "LongCount") ||
                       EndsWithMemberName(expression, "Sum") ||
                       EndsWithMemberName(expression, "Average") ||
                       EndsWithMemberName(expression, "Aggregate") ||
                       EndsWithMemberName(expression, "Distinct") ||
                       EndsWithMemberName(expression, "Union") ||
                       EndsWithMemberName(expression, "Intersect") ||
                       EndsWithMemberName(expression, "Except") ||
                       EndsWithMemberName(expression, "Reverse") ||
                       EndsWithMemberName(expression, "Skip") ||
                       EndsWithMemberName(expression, "SkipWhile") ||
                       EndsWithMemberName(expression, "Take") ||
                       EndsWithMemberName(expression, "TakeWhile") ||
                       EndsWithMemberName(expression, "Cast") ||
                       EndsWithMemberName(expression, "OfType");
            }

            private static bool EndsWithMemberName(string expression, string memberName)
            {
                return string.Equals(expression, memberName, StringComparison.Ordinal) ||
                       expression.EndsWith("." + memberName, StringComparison.Ordinal);
            }

            private static bool IsUnityRuntimeTrapInvocation(string expression)
            {
                return IsUnitySceneSearchInvocation(expression) ||
                       IsUnityBlockingLoadInvocation(expression) ||
                       IsCoroutineSchedulingInvocation(expression);
            }

            private static bool IsUnitySceneSearchInvocation(string expression)
            {
                return IsStaticTypeMember(expression, "GameObject", "Find") ||
                       IsStaticTypeMember(expression, "Object", "FindObjectOfType") ||
                       IsStaticTypeMember(expression, "Object", "FindObjectsOfType") ||
                       IsStaticTypeMember(expression, "Object", "FindFirstObjectByType") ||
                       IsStaticTypeMember(expression, "Object", "FindAnyObjectByType") ||
                       IsStaticTypeMember(expression, "Object", "FindObjectsByType") ||
                       EndsWithMemberName(expression, "FindObjectOfType") ||
                       EndsWithMemberName(expression, "FindObjectsOfType") ||
                       EndsWithMemberName(expression, "FindFirstObjectByType") ||
                       EndsWithMemberName(expression, "FindAnyObjectByType") ||
                       EndsWithMemberName(expression, "FindObjectsByType");
            }

            private static bool IsUnityBlockingLoadInvocation(string expression)
            {
                return IsStaticTypeMember(expression, "Resources", "Load") ||
                       IsStaticTypeMember(expression, "Resources", "LoadAll") ||
                       IsStaticTypeMember(expression, "Resources", "FindObjectsOfTypeAll") ||
                       IsStaticTypeMember(expression, "Resources", "UnloadUnusedAssets") ||
                       IsStaticTypeMember(expression, "AssetBundle", "LoadAsset") ||
                       IsStaticTypeMember(expression, "AssetBundle", "LoadAllAssets") ||
                       IsStaticTypeMember(expression, "SceneManager", "LoadScene") ||
                       IsStaticTypeMember(expression, "SceneManager", "LoadSceneAsync") ||
                       IsStaticTypeMember(expression, "Addressables", "LoadAssetAsync") ||
                       IsStaticTypeMember(expression, "Addressables", "LoadAssetsAsync") ||
                       IsStaticTypeMember(expression, "Addressables", "InstantiateAsync") ||
                       IsStaticTypeMember(expression, "GC", "Collect");
            }

            private static bool IsCoroutineSchedulingInvocation(string expression)
            {
                return EndsWithMemberName(expression, "StartCoroutine") ||
                       EndsWithMemberName(expression, "StopCoroutine") ||
                       EndsWithMemberName(expression, "StopAllCoroutines");
            }

            private static bool IsBlockingSynchronizationInvocation(string expression)
            {
                return EndsWithMemberName(expression, "Complete") ||
                       EndsWithMemberName(expression, "CompleteAll") ||
                       EndsWithMemberName(expression, "CompleteDependency") ||
                       EndsWithMemberName(expression, "CompleteReadAndWriteDependency") ||
                       EndsWithMemberName(expression, "WaitForCompletion");
            }

            private static bool IsUnityRuntimeTrapMemberAccess(MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                string owner = NormalizeMemberExpression(memberAccess.Expression.ToString());
                if (string.Equals(memberName, "main", StringComparison.Ordinal) &&
                    (string.Equals(owner, "Camera", StringComparison.Ordinal) ||
                     owner.EndsWith(".Camera", StringComparison.Ordinal)))
                {
                    return true;
                }

                return string.Equals(memberName, "material", StringComparison.Ordinal) ||
                       string.Equals(memberName, "materials", StringComparison.Ordinal) ||
                       string.Equals(memberName, "mesh", StringComparison.Ordinal) ||
                       string.Equals(memberName, "vertices", StringComparison.Ordinal) ||
                       string.Equals(memberName, "normals", StringComparison.Ordinal) ||
                       string.Equals(memberName, "tangents", StringComparison.Ordinal) ||
                       string.Equals(memberName, "triangles", StringComparison.Ordinal) ||
                       string.Equals(memberName, "uv", StringComparison.Ordinal) ||
                       string.Equals(memberName, "uv2", StringComparison.Ordinal) ||
                       string.Equals(memberName, "colors", StringComparison.Ordinal) ||
                       string.Equals(memberName, "colors32", StringComparison.Ordinal) ||
                       string.Equals(memberName, "boneWeights", StringComparison.Ordinal) ||
                       string.Equals(memberName, "bindposes", StringComparison.Ordinal);
            }

            private static bool IsStaticTypeMember(string expression, string typeName, string memberName)
            {
                string normalized = NormalizeMemberExpression(expression);
                string target = typeName + "." + memberName;
                return string.Equals(normalized, target, StringComparison.Ordinal) ||
                       normalized.EndsWith("." + target, StringComparison.Ordinal);
            }

            private static string NormalizeMemberExpression(string expression)
            {
                string normalized = expression.Trim();
                if (normalized.StartsWith("global::", StringComparison.Ordinal))
                    normalized = normalized.Substring("global::".Length);

                int genericIndex = normalized.IndexOf('<');
                if (genericIndex >= 0)
                    normalized = normalized.Substring(0, genericIndex);

                return normalized;
            }

            private static bool IsUnsafeHotStackAlloc(StackAllocArrayCreationExpressionSyntax stackAlloc)
            {
                string stackAllocType = stackAlloc.Type != null ? stackAlloc.Type.ToString() : string.Empty;
                int rankStart = stackAllocType.IndexOf('[');
                string elementType = rankStart > 0 ? stackAllocType.Substring(0, rankStart) : stackAllocType;
                int elementSize = EstimateElementSizeBytes(elementType);
                if (elementSize <= 0)
                    return true;

                int elementCount = TryReadStackAllocElementCount(stackAlloc.ToString());
                if (elementCount < 0)
                    return true;

                long byteCount = (long)elementCount * elementSize;
                return byteCount > MaxHotStackAllocBytes;
            }

            private static int TryReadStackAllocElementCount(string expression)
            {
                if (string.IsNullOrEmpty(expression))
                    return -1;

                int open = expression.IndexOf('[');
                int close = open >= 0 ? expression.IndexOf(']', open + 1) : -1;
                if (open < 0 || close <= open + 1)
                    return -1;

                string size = expression.Substring(open + 1, close - open - 1).Trim();
                int value;
                return int.TryParse(size, out value) && value >= 0 ? value : -1;
            }

            private static int EstimateElementSizeBytes(string typeName)
            {
                string normalized = NormalizeAllocatedTypeName(typeName);
                if (string.Equals(normalized, "byte", StringComparison.Ordinal) ||
                    string.Equals(normalized, "sbyte", StringComparison.Ordinal) ||
                    string.Equals(normalized, "bool", StringComparison.Ordinal))
                {
                    return 1;
                }

                if (string.Equals(normalized, "short", StringComparison.Ordinal) ||
                    string.Equals(normalized, "ushort", StringComparison.Ordinal) ||
                    string.Equals(normalized, "char", StringComparison.Ordinal))
                {
                    return 2;
                }

                if (string.Equals(normalized, "int", StringComparison.Ordinal) ||
                    string.Equals(normalized, "uint", StringComparison.Ordinal) ||
                    string.Equals(normalized, "float", StringComparison.Ordinal))
                {
                    return 4;
                }

                if (string.Equals(normalized, "long", StringComparison.Ordinal) ||
                    string.Equals(normalized, "ulong", StringComparison.Ordinal) ||
                    string.Equals(normalized, "double", StringComparison.Ordinal) ||
                    string.Equals(normalized, "Vector2", StringComparison.Ordinal) ||
                    string.Equals(normalized, "float2", StringComparison.Ordinal))
                {
                    return 8;
                }

                if (string.Equals(normalized, "Vector3", StringComparison.Ordinal) ||
                    string.Equals(normalized, "float3", StringComparison.Ordinal))
                {
                    return 12;
                }

                if (string.Equals(normalized, "Vector4", StringComparison.Ordinal) ||
                    string.Equals(normalized, "Quaternion", StringComparison.Ordinal) ||
                    string.Equals(normalized, "float4", StringComparison.Ordinal))
                {
                    return 16;
                }

                return -1;
            }

            private static bool IsStringConcatCandidate(BinaryExpressionSyntax binary)
            {
                return binary.IsKind(SyntaxKind.AddExpression) &&
                       (IsStringLikeExpression(binary.Left) || IsStringLikeExpression(binary.Right));
            }

            private static bool IsStringLikeExpression(ExpressionSyntax expression)
            {
                while (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    expression = parenthesized.Expression;
                }

                if (expression.IsKind(SyntaxKind.StringLiteralExpression) ||
                    expression is InterpolatedStringExpressionSyntax)
                {
                    return true;
                }

                if (expression is InvocationExpressionSyntax invocation &&
                    IsStringAllocationInvocation(invocation))
                {
                    return true;
                }

                return expression is BinaryExpressionSyntax binary && IsStringConcatCandidate(binary);
            }

            private static bool IsManagedAllocationType(string typeName)
            {
                string normalized = NormalizeAllocatedTypeName(typeName);
                return string.Equals(normalized, "List", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Dictionary", StringComparison.Ordinal) ||
                       string.Equals(normalized, "HashSet", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Queue", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Stack", StringComparison.Ordinal) ||
                       string.Equals(normalized, "StringBuilder", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Action", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Func", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Predicate", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Comparison", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Converter", StringComparison.Ordinal) ||
                       string.Equals(normalized, "EventHandler", StringComparison.Ordinal) ||
                       string.Equals(normalized, "UnityAction", StringComparison.Ordinal) ||
                       string.Equals(normalized, "object", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Object", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Exception", StringComparison.Ordinal) ||
                       string.Equals(normalized, "InvalidOperationException", StringComparison.Ordinal) ||
                       string.Equals(normalized, "ArgumentException", StringComparison.Ordinal) ||
                       string.Equals(normalized, "WaitForSeconds", StringComparison.Ordinal) ||
                       string.Equals(normalized, "WaitUntil", StringComparison.Ordinal) ||
                       string.Equals(normalized, "WaitWhile", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Task", StringComparison.Ordinal) ||
                       string.Equals(normalized, "TaskCompletionSource", StringComparison.Ordinal) ||
                       string.Equals(normalized, "CancellationTokenSource", StringComparison.Ordinal) ||
                       string.Equals(normalized, "Regex", StringComparison.Ordinal) ||
                       string.Equals(normalized, "NativeArray", StringComparison.Ordinal) ||
                       string.Equals(normalized, "NativeList", StringComparison.Ordinal) ||
                       string.Equals(normalized, "NativeHashMap", StringComparison.Ordinal) ||
                       string.Equals(normalized, "NativeParallelHashMap", StringComparison.Ordinal) ||
                       string.Equals(normalized, "NativeQueue", StringComparison.Ordinal) ||
                       string.Equals(normalized, "UnsafeList", StringComparison.Ordinal);
            }

            private static string NormalizeAllocatedTypeName(string typeName)
            {
                string normalized = typeName.Trim();
                if (normalized.StartsWith("global::", StringComparison.Ordinal))
                    normalized = normalized.Substring("global::".Length);

                int genericIndex = normalized.IndexOf('<');
                if (genericIndex >= 0)
                    normalized = normalized.Substring(0, genericIndex);

                int dotIndex = normalized.LastIndexOf('.');
                if (dotIndex >= 0 && dotIndex + 1 < normalized.Length)
                    normalized = normalized.Substring(dotIndex + 1);

                return normalized;
            }
        }
    }
}
#endif
