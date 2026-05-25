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

namespace Hecton8.UI.Editor
{
    public static class OOP_Canvas_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const int CategoryCanvas = 1;
        private const int CategoryRaycaster = 2;
        private const int CategoryCollider = 3;

        [MenuItem("Hecton8/UI/OOP Canvas Scanner SHINOBU_331")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            int scannedFiles = 0;
            int habitatVehicleHits = 0;
            int canvasHits = 0;
            int graphicRaycasterHits = 0;
            int boxColliderHits = 0;
            ScanScripts(scriptsRoot, ref scannedFiles, ref habitatVehicleHits, ref canvasHits, ref graphicRaycasterHits, ref boxColliderHits);

            string reportPath = Path.Combine(root, ReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string section = BuildSection(scannedFiles, habitatVehicleHits, canvasHits, graphicRaycasterHits, boxColliderHits);
            File.WriteAllText(reportPath, UpsertSection(reportPath, section));
            AssetDatabase.Refresh();
            Debug.Log("OOP Canvas Scanner wrote " + reportPath);
        }

        private static void ScanScripts(
            string scriptsRoot,
            ref int scannedFiles,
            ref int habitatVehicleHits,
            ref int canvasHits,
            ref int graphicRaycasterHits,
            ref int boxColliderHits)
        {
            if (!Directory.Exists(scriptsRoot))
                return;

            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                scannedFiles++;
                string text = File.ReadAllText(files[i]);
                bool habitatOrVehicle =
                    path.IndexOf("/Habitat/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/Vehicles/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("namespace Hecton8.Habitat", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("namespace Hecton8.Vehicles", StringComparison.Ordinal) >= 0;
                int canvas = 0;
                int raycaster = 0;
                int collider = 0;
                bool parseFailed = false;
                try
                {
                    SyntaxNode root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
                    ScanAst(path, root, habitatOrVehicle, ref canvas, ref raycaster, ref collider);
                }
                catch (Exception)
                {
                    parseFailed = true;
                }

                if (parseFailed)
                {
                    canvas += Count(text, "UnityEngine.Canvas") + Count(text, "Canvas.renderMode") + Count(text, "RenderMode.WorldSpace");
                    raycaster += Count(text, "GraphicRaycaster") + Count(text, "EventSystem.RaycastAll");
                    collider += Count(text, "BoxCollider");
                }

                canvasHits += canvas;
                graphicRaycasterHits += raycaster;
                boxColliderHits += collider;
                if (habitatOrVehicle && (canvas + raycaster + collider) > 0)
                    habitatVehicleHits++;
            }
        }

        private static void ScanAst(
            string path,
            SyntaxNode root,
            bool habitatOrVehicle,
            ref int canvasHits,
            ref int graphicRaycasterHits,
            ref int boxColliderHits)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveOopUiNode(path, node, habitatOrVehicle, out int category))
                        continue;

                    if (category == CategoryCanvas)
                        canvasHits++;
                    else if (category == CategoryRaycaster)
                        graphicRaycasterHits++;
                    else if (category == CategoryCollider)
                        boxColliderHits++;
                }
            }
        }

        private static bool TryResolveOopUiNode(string path, SyntaxNode node, bool habitatOrVehicle, out int category)
        {
            category = 0;
            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string type = objectCreation.Type.ToString();
                if (IsCanvasType(type))
                {
                    category = CategoryCanvas;
                    return true;
                }

                if (IsGraphicRaycasterType(type))
                {
                    category = CategoryRaycaster;
                    return true;
                }

                if (IsBoxColliderType(type) && IsTerminalUiContext(path, node, habitatOrVehicle))
                {
                    category = CategoryCollider;
                    return true;
                }
            }

            if (node is AssignmentExpressionSyntax assignment)
            {
                string left = assignment.Left.ToString();
                string right = assignment.Right.ToString();
                if ((left.IndexOf("renderMode", StringComparison.Ordinal) >= 0 ||
                     left.IndexOf("RenderMode", StringComparison.Ordinal) >= 0) &&
                    right.IndexOf("WorldSpace", StringComparison.Ordinal) >= 0)
                {
                    category = CategoryCanvas;
                    return true;
                }
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                string invocationText = invocation.ToString();
                if (invocationText.IndexOf("AddComponent<Canvas>", StringComparison.Ordinal) >= 0 ||
                    invocationText.IndexOf("AddComponent<UnityEngine.Canvas>", StringComparison.Ordinal) >= 0 ||
                    invocationText.IndexOf("AddComponent(typeof(Canvas))", StringComparison.Ordinal) >= 0 ||
                    invocationText.IndexOf("AddComponent(typeof(UnityEngine.Canvas))", StringComparison.Ordinal) >= 0)
                {
                    category = CategoryCanvas;
                    return true;
                }

                if (invocationText.IndexOf("AddComponent<GraphicRaycaster>", StringComparison.Ordinal) >= 0 ||
                    invocationText.IndexOf("AddComponent<UnityEngine.UI.GraphicRaycaster>", StringComparison.Ordinal) >= 0 ||
                    invocationText.IndexOf("EventSystem.RaycastAll", StringComparison.Ordinal) >= 0)
                {
                    category = CategoryRaycaster;
                    return true;
                }

                if ((invocationText.IndexOf("AddComponent<BoxCollider>", StringComparison.Ordinal) >= 0 ||
                     invocationText.IndexOf("AddComponent<UnityEngine.BoxCollider>", StringComparison.Ordinal) >= 0 ||
                     invocationText.IndexOf("Physics.Raycast", StringComparison.Ordinal) >= 0) &&
                    IsTerminalUiContext(path, node, habitatOrVehicle))
                {
                    category = CategoryCollider;
                    return true;
                }
            }

            return false;
        }

        private static bool IsCanvasType(string type)
        {
            return type == "Canvas" || type == "UnityEngine.Canvas";
        }

        private static bool IsGraphicRaycasterType(string type)
        {
            return type == "GraphicRaycaster" || type == "UnityEngine.UI.GraphicRaycaster";
        }

        private static bool IsBoxColliderType(string type)
        {
            return type == "BoxCollider" || type == "UnityEngine.BoxCollider";
        }

        private static bool IsTerminalUiContext(string path, SyntaxNode node, bool habitatOrVehicle)
        {
            if (!habitatOrVehicle)
                return false;

            if (ContainsUiWord(path))
                return true;

            SyntaxNode current = node;
            while (current != null)
            {
                if (current is ClassDeclarationSyntax classDeclaration && ContainsUiWord(classDeclaration.Identifier.ValueText))
                    return true;
                if (current is MethodDeclarationSyntax methodDeclaration && ContainsUiWord(methodDeclaration.Identifier.ValueText))
                    return true;
                current = current.Parent;
            }

            return false;
        }

        private static bool ContainsUiWord(string value)
        {
            return value.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Console", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildSection(int scannedFiles, int habitatVehicleHits, int canvasHits, int graphicRaycasterHits, int boxColliderHits)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("  \"shinobu_331_terminal_projection\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_331\",");
            builder.AppendLine("    \"summary\": \"STATIC_SCRIPT_SCAN_ONLY_terminal_takeover_route_recorded\",");
            builder.Append("    \"timestampLocal\": \"").Append(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")).AppendLine("\",");
            builder.AppendLine("    \"claimScope\": \"Static script scan plus runtime takeover route; prefab YAML deletion intentionally not claimed.\",");
            builder.Append("    \"scannedFiles\": ").Append(scannedFiles).AppendLine(",");
            builder.Append("    \"habitatVehicleOopHitFiles\": ").Append(habitatVehicleHits).AppendLine(",");
            builder.Append("    \"canvasTokenHits\": ").Append(canvasHits).AppendLine(",");
            builder.Append("    \"graphicRaycasterTokenHits\": ").Append(graphicRaycasterHits).AppendLine(",");
            builder.Append("    \"boxColliderTokenHits\": ").Append(boxColliderHits).AppendLine(",");
            builder.AppendLine("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree object-creation/invocation/assignment pass; token fallback only for parse failures\",");
            builder.AppendLine("    \"takeoverPath\": \"TerminalOsRuntime_TerminalProjection + EvaluateTerminalGazeJob + _TerminalInputStates StructuredBuffer\",");
            builder.AppendLine("    \"vaultBuffers\": [71380, 71381, 71382, 71383],");
            builder.AppendLine("    \"rollbackFence\": \"TerminalInputStateDTO is presentation-only and excluded from StateRingBuffer/Merkle truth routes.\",");
            builder.AppendLine("    \"gpuUpload\": \"GraphicsBuffer.LockBufferForWrite with double-buffered _TerminalInputStates; CPU 64-byte TerminalInputStateDTO is compacted into 32-byte TerminalInputGpuStateDTO rows. Vault 71383 hashes gate contiguous dirty-row uploads after the forced first upload; runtime Frame Debugger proof pending.\",");
            builder.AppendLine("    \"reviewDisposition\": \"YELLOW: static source route exists; compile, Unity import, profiler/GCMonitor, Frame Debugger, and device proof pending.\",");
            builder.AppendLine("    \"hotPathAllocBytes\": null,");
            builder.AppendLine("    \"hotPathAllocProof\": \"PENDING_PROFILER_GCMONITOR\",");
            builder.AppendLine("    \"status\": \"STATIC_SCAN_RECORDED_RUNTIME_ROUTE_ACTIVE_RUNTIME_PROOF_PENDING\"");
            builder.Append("  }");
            return builder.ToString();
        }

        private static string UpsertSection(string reportPath, string section)
        {
            if (!File.Exists(reportPath))
                return "{\n" + section + "\n}\n";

            string existing = File.ReadAllText(reportPath);
            const string sectionKey = "  \"shinobu_331_terminal_projection\": {";
            int start = existing.IndexOf(sectionKey, StringComparison.Ordinal);
            if (start >= 0)
            {
                int end = FindSectionEnd(existing, start);
                if (end > start)
                    return existing.Substring(0, start) + section + existing.Substring(end);
            }

            int objectEnd = FindLastObjectEnd(existing);
            if (objectEnd < 0)
                return "{\n" + section + "\n}\n";

            int previous = objectEnd - 1;
            while (previous >= 0 && char.IsWhiteSpace(existing[previous]))
                previous--;

            string comma = previous >= 0 && existing[previous] == '{' ? string.Empty : ",";
            return existing.Substring(0, objectEnd).TrimEnd() + comma + "\n" + section + "\n}\n";
        }

        private static int FindSectionEnd(string text, int sectionStart)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            bool foundObject = false;
            for (int i = sectionStart; i < text.Length; i++)
            {
                char c = text[i];
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
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    foundObject = true;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (foundObject && depth == 0)
                    {
                        int end = i + 1;
                        while (end < text.Length && (text[end] == '\r' || text[end] == '\n'))
                            end++;
                        return end;
                    }
                }
            }

            return -1;
        }

        private static int FindLastObjectEnd(string text)
        {
            bool inString = false;
            bool escaped = false;
            int lastObjectEnd = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
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
                {
                    inString = true;
                    continue;
                }

                if (c == '}')
                    lastObjectEnd = i;
            }

            return lastObjectEnd;
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
#endif
