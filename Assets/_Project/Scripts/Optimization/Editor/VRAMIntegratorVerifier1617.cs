#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// In-memory AST verifier for agent 1617 graphics streaming integration rules.
    /// Writes no report files; failures are compiler-style exceptions in the Unity editor.
    /// </summary>
    internal static class VRAMIntegratorVerifier1617
    {
        private static readonly string[] RuntimeFiles =
        {
            "Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs",
            "Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs",
            "Assets/_Project/Scripts/Optimization/RenderTexturePool.cs",
            "Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs",
            "Assets/_Project/Scripts/Optimization/VRAMMonitor.cs",
            "Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs",
            "Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs",
            "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
            "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs",
            "Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedUpdate",
            "LateFrameTick",
            "Execute"
        };

        [MenuItem("Hecton8/Optimization/Agent 1617 APEX Integrator Verify")]
        private static void RunMenu()
        {
            Verify();
            Hecton8.Core.H8Debug.Log("Agent 1617 APEX integrator verification passed.");
        }

        internal static void Verify()
        {
            for (int i = 0; i < RuntimeFiles.Length; i++)
            {
                string absolutePath = ToAbsolutePath(RuntimeFiles[i]);
                string source = File.ReadAllText(absolutePath);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: RuntimeFiles[i]);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                AssertNoSyntaxErrors(root, RuntimeFiles[i]);
                AssertNoHotDependencyLookups(root, RuntimeFiles[i]);
                AssertAssetProgressSignalPhase(root, RuntimeFiles[i]);
                AssertWriteLocksFlattened(root, RuntimeFiles[i]);
            }
        }

        private static void AssertNoSyntaxErrors(CompilationUnitSyntax root, string path)
        {
            using (System.Collections.Generic.IEnumerator<Diagnostic> diagnostics = root.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    Diagnostic diagnostic = diagnostics.Current;
                    if (diagnostic != null && diagnostic.Severity == DiagnosticSeverity.Error)
                        throw new InvalidOperationException(path + ": syntax error: " + diagnostic);
                }
            }
        }

        private static void AssertNoHotDependencyLookups(CompilationUnitSyntax root, string path)
        {
            using (System.Collections.Generic.IEnumerator<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (method == null || !IsHotMethod(method.Identifier.ValueText))
                        continue;

                    string body = method.Body != null ? method.Body.ToString() : string.Empty;
                    if (body.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal) >= 0)
                        throw new InvalidOperationException(path + ": hot method " + method.Identifier.ValueText + " calls GlobalRegistry.Get<T>().");

                    if (body.IndexOf("GetComponent", StringComparison.Ordinal) >= 0)
                        throw new InvalidOperationException(path + ": hot method " + method.Identifier.ValueText + " performs component lookup.");
                }
            }
        }

        private static void AssertAssetProgressSignalPhase(CompilationUnitSyntax root, string path)
        {
            using (System.Collections.Generic.IEnumerator<InvocationExpressionSyntax> invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
            {
                while (invocations.MoveNext())
                {
                    InvocationExpressionSyntax invocation = invocations.Current;
                    if (invocation == null)
                        continue;

                    string expression = invocation.Expression.ToString();
                    if (expression.IndexOf("SignalBus<AssetLoadProgressSignal>.TryPushTracked", StringComparison.Ordinal) < 0)
                        continue;

                    string methodName = ResolveOwnerMethodName(invocation);
                    if (methodName == "LateFrameTick" || methodName == "FlushProgressSignalsLateFrame")
                        continue;

                    throw new InvalidOperationException(path + ": AssetLoadProgressSignal push outside late-frame route in " + methodName + ".");
                }
            }
        }

        private static void AssertWriteLocksFlattened(CompilationUnitSyntax root, string path)
        {
            using (System.Collections.Generic.IEnumerator<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().GetEnumerator())
            {
                while (methods.MoveNext())
                {
                    MethodDeclarationSyntax method = methods.Current;
                    if (method == null)
                        continue;

                    int acquireCount = 0;
                    int releaseCount = 0;
                    bool releaseInFinally = false;

                    using (System.Collections.Generic.IEnumerator<InvocationExpressionSyntax> invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().GetEnumerator())
                    {
                        while (invocations.MoveNext())
                        {
                            InvocationExpressionSyntax invocation = invocations.Current;
                            if (invocation == null)
                                continue;

                            string expression = invocation.Expression.ToString();
                            if (expression.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal) >= 0)
                                acquireCount++;
                            if (expression.IndexOf("ReleaseWriteLock", StringComparison.Ordinal) >= 0)
                            {
                                releaseCount++;
                                if (HasAncestor<FinallyClauseSyntax>(invocation))
                                    releaseInFinally = true;
                            }
                        }
                    }

                    if (acquireCount > 1)
                        throw new InvalidOperationException(path + ": method " + method.Identifier.ValueText + " acquires multiple DataVault write locks.");
                    if (acquireCount == 1 && (releaseCount != 1 || !releaseInFinally))
                        throw new InvalidOperationException(path + ": method " + method.Identifier.ValueText + " write lock is not released exactly once inside finally.");
                }
            }
        }

        private static string ResolveOwnerMethodName(SyntaxNode node)
        {
            SyntaxNode current = node;
            while (current != null)
            {
                MethodDeclarationSyntax method = current as MethodDeclarationSyntax;
                if (method != null)
                    return method.Identifier.ValueText;

                current = current.Parent;
            }

            return "<unknown>";
        }

        private static bool HasAncestor<T>(SyntaxNode node)
            where T : SyntaxNode
        {
            SyntaxNode current = node.Parent;
            while (current != null)
            {
                if (current is T)
                    return true;

                current = current.Parent;
            }

            return false;
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

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath);
        }
    }
}
#endif
