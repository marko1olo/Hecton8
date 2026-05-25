#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Physics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    public static class SynchronousGpuReadbackScanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string AgentReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_264.json";

        [MenuItem("HECTON-8/Physics/Run Sync GPU Readback Scanner")]
        public static void RunFromMenu()
        {
            bool clean = Run();
            if (clean)
                Debug.Log("SHINOBU_264 Roslyn sync GPU scanner passed.");
            else
                Debug.LogWarning("SHINOBU_264 Roslyn sync GPU scanner found blocking readback risks. See Docs/Reports.");
        }

        public static bool Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string physicsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Physics");
            string vehiclesRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Vehicles");
            StringBuilder findings = new StringBuilder(2048);
            int findingCount = 0;
            int allowedAsyncReadbackCount = 0;
            int scannedFiles = 0;

            ScanRoot(projectRoot, physicsRoot, findings, ref findingCount, ref allowedAsyncReadbackCount, ref scannedFiles);
            ScanRoot(projectRoot, vehiclesRoot, findings, ref findingCount, ref allowedAsyncReadbackCount, ref scannedFiles);

            bool layoutValid = AsyncBuoyancyReadbackLayoutValidator.Validate();
            if (!layoutValid)
                AppendFinding(findings, ref findingCount, "LAYOUT", "Readback DTO layout failed explicit size/offset validation.");

            string json = BuildReport(scannedFiles, findingCount, allowedAsyncReadbackCount, layoutValid, findings.ToString());
            WriteReport(projectRoot, SharedReportRelativePath, json);
            WriteReport(projectRoot, AgentReportRelativePath, json);
            return findingCount == 0;
        }

        private static void ScanRoot(
            string projectRoot,
            string root,
            StringBuilder findings,
            ref int findingCount,
            ref int allowedAsyncReadbackCount,
            ref int scannedFiles)
        {
            if (!Directory.Exists(root))
                return;

            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.EndsWith(nameof(SynchronousGpuReadbackScanner) + ".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                scannedFiles++;
                ScanFile(projectRoot, file, text, findings, ref findingCount, ref allowedAsyncReadbackCount);
            }
        }

        private static void ScanFile(
            string projectRoot,
            string file,
            string text,
            StringBuilder findings,
            ref int findingCount,
            ref int allowedAsyncReadbackCount)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            string relative = MakeRelative(projectRoot, file);

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is InvocationExpressionSyntax invocation))
                    continue;

                string name = ResolveInvocationName(invocation.Expression);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (name == "ReadPixels" || name == "WaitForCompletion" || name.StartsWith("GetPixel", StringComparison.Ordinal))
                {
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, invocation), invocation.ToString());
                    continue;
                }

                if (name == "SetData")
                {
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, invocation), "Forbidden GPU buffer SetData upload in readback domain: " + invocation);
                    continue;
                }

                if (name == "GetData")
                {
                    if (IsAllowedAsyncReadbackGetData(invocation))
                    {
                        allowedAsyncReadbackCount++;
                    }
                    else
                    {
                        AppendFinding(findings, ref findingCount, LocationOf(tree, relative, invocation), invocation.ToString());
                    }
                }
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is ObjectCreationExpressionSyntax creation))
                    continue;

                string typeName = ResolveTypeName(creation.Type);
                if (typeName == "Texture2D" || typeName == "RenderTexture")
                {
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, creation), "Forbidden texture allocation in GPU readback domain: " + creation);
                    continue;
                }

                if (typeName == "NativeArray" && IsInsideHotMethod(creation))
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, creation), "Forbidden hot NativeArray allocation: " + creation);
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is ArrayCreationExpressionSyntax creation))
                    continue;

                if (IsInsideHotMethod(creation))
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, creation), "Forbidden managed array allocation in hot method: " + creation);
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is ImplicitArrayCreationExpressionSyntax creation))
                    continue;

                if (IsInsideHotMethod(creation))
                    AppendFinding(findings, ref findingCount, LocationOf(tree, relative, creation), "Forbidden implicit managed array allocation in hot method: " + creation);
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is AttributeSyntax attribute))
                    continue;

                if (!HasForbiddenPackOne(attribute))
                    continue;

                AppendFinding(findings, ref findingCount, LocationOf(tree, relative, attribute), "Forbidden Pack=1 layout in physics readback domain.");
            }

            if (!relative.EndsWith("AsyncBuoyancyReadbackContracts.cs", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is PropertyDeclarationSyntax property))
                    continue;

                AppendFinding(findings, ref findingCount, LocationOf(tree, relative, property), "Readback DTO contract must stay raw public fields only.");
            }
        }

        private static bool IsAllowedAsyncReadbackGetData(InvocationExpressionSyntax invocation)
        {
            MemberAccessExpressionSyntax member = invocation.Expression as MemberAccessExpressionSyntax;
            if (member == null)
                return false;

            string receiver = member.Expression.ToString();
            if (!receiver.Contains("request", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetAncestor(invocation, out MethodDeclarationSyntax method))
                return false;

            foreach (SyntaxNode node in method.DescendantNodes())
            {
                if (!(node is InvocationExpressionSyntax earlier))
                    continue;

                if (earlier.SpanStart >= invocation.SpanStart)
                    continue;

                string earlierName = ResolveInvocationName(earlier.Expression);
                if (earlierName == "IsAsyncReadbackReadyNoWait" && earlier.ToString().Contains(receiver, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsInsideHotMethod(SyntaxNode node)
        {
            if (!TryGetAncestor(node, out MethodDeclarationSyntax method))
                return false;

            string name = method.Identifier.ValueText;
            return name == "Update" ||
                   name == "FixedUpdate" ||
                   name == "LateUpdate" ||
                   name == "PreSimulationTick" ||
                   name == "ScheduleSimulation" ||
                   name == "PostSimulationTick" ||
                   name == "VisualSyncTick" ||
                   name == "Execute";
        }

        private static bool TryGetAncestor<T>(SyntaxNode node, out T ancestor) where T : SyntaxNode
        {
            SyntaxNode current = node.Parent;
            while (current != null)
            {
                if (current is T typed)
                {
                    ancestor = typed;
                    return true;
                }

                current = current.Parent;
            }

            ancestor = null;
            return false;
        }

        private static string ResolveInvocationName(ExpressionSyntax expression)
        {
            if (expression is MemberAccessExpressionSyntax member)
                return ResolveSimpleName(member.Name);
            if (expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (expression is GenericNameSyntax generic)
                return generic.Identifier.ValueText;
            return string.Empty;
        }

        private static string ResolveSimpleName(SimpleNameSyntax simpleName)
        {
            if (simpleName is GenericNameSyntax generic)
                return generic.Identifier.ValueText;
            return simpleName.Identifier.ValueText;
        }

        private static string ResolveTypeName(TypeSyntax type)
        {
            if (type is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (type is GenericNameSyntax generic)
                return generic.Identifier.ValueText;
            if (type is QualifiedNameSyntax qualified)
                return qualified.Right.Identifier.ValueText;
            if (type is AliasQualifiedNameSyntax aliasQualified)
                return aliasQualified.Name.Identifier.ValueText;
            return type.ToString();
        }

        private static bool HasForbiddenPackOne(AttributeSyntax attribute)
        {
            if (attribute.ArgumentList == null)
                return false;

            foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
            {
                if (argument.NameEquals == null || argument.NameEquals.Name.Identifier.ValueText != "Pack")
                    continue;

                if (argument.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.Value is int intValue &&
                    intValue == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static string LocationOf(SyntaxTree tree, string relative, SyntaxNode node)
        {
            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            return relative + ":" + (span.StartLinePosition.Line + 1);
        }

        private static void AppendFinding(StringBuilder findings, ref int count, string location, string message)
        {
            if (count > 0)
                findings.Append(',');
            findings.Append("{\"location\":\"")
                .Append(Escape(location))
                .Append("\",\"message\":\"")
                .Append(Escape(message))
                .Append("\"}");
            count++;
        }

        private static string BuildReport(int scannedFiles, int findingCount, int allowedAsyncReadbacks, bool layoutValid, string findingsJson)
        {
            StringBuilder builder = new StringBuilder(2048 + findingsJson.Length);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_264\",\n");
            builder.Append("  \"scanner\": \"SynchronousGpuReadbackScanner.RoslynAST\",\n");
            builder.Append("  \"scannedFiles\": ").Append(scannedFiles).Append(",\n");
            builder.Append("  \"syncFindingCount\": ").Append(findingCount).Append(",\n");
            builder.Append("  \"allowedAsyncGetDataCount\": ").Append(allowedAsyncReadbacks).Append(",\n");
            builder.Append("  \"readbackRequestLayoutValid\": ").Append(layoutValid ? "true" : "false").Append(",\n");
            builder.Append("  \"dtoSizeBytes\": ").Append(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<ReadbackRequestDTO>()).Append(",\n");
            builder.Append("  \"forbiddenPatterns\": [\"ReadPixels\", \"GetPixel\", \"ComputeBuffer.GetData\", \"GraphicsBuffer.GetData\", \"AsyncGPUReadbackRequest.WaitForCompletion\", \"GraphicsBuffer.SetData\", \"ComputeBuffer.SetData\", \"new Texture2D\", \"new RenderTexture\", \"hot new[]\", \"hot new NativeArray\", \"Pack=1\"],\n");
            builder.Append("  \"findings\": [").Append(findingsJson).Append("]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void WriteReport(string projectRoot, string relativePath, string json)
        {
            string path = Path.Combine(projectRoot, relativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static string MakeRelative(string projectRoot, string file)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullFile = Path.GetFullPath(file);
            return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullFile.Substring(fullRoot.Length).Replace('\\', '/')
                : fullFile.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    public static class Synchronous_GPU_Scanner
    {
        public static void RunFromMenu()
        {
            SynchronousGpuReadbackScanner.RunFromMenu();
        }

        public static bool Run()
        {
            return SynchronousGpuReadbackScanner.Run();
        }
    }
}
#endif
