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

namespace Hecton8.Physics.Buoyancy.Editor
{
    public static class Wave_Math_Scanner
    {
        private const int MaxFindings = 128;
        private const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string AgentReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_263.json";
        private const string SharedReportSection = "shinobu263WaveMathScanner";

        [MenuItem("HECTON-8/Physics/Wave Math Scanner")]
        public static void Run()
        {
            string projectRoot = ProjectRoot();
            string assetsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            ScanResult result = Scan(assetsRoot);
            string reportJson = result.ToJson();
            WriteReportFile(Path.Combine(projectRoot, AgentReportPath), reportJson);
            UpsertSharedReportSection(Path.Combine(projectRoot, SharedReportPath), SharedReportSection, reportJson);
            AssetDatabase.Refresh();
            Debug.Log("[SHINOBU_263] Wave math scanner wrote " + AgentReportPath + " and updated shared physics report section.");
        }

        public static ScanResult Scan(string assetsRoot)
        {
            ScanResult result = new ScanResult();
            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot))
            {
                result.AddFinding("ERROR", assetsRoot ?? string.Empty, 0, "Assets root missing.");
                return result;
            }

            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ShouldSkip(path))
                    continue;

                result.FilesScanned++;
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    result.AddFinding("IO_ERROR", path, 0, "File read failed.");
                    continue;
                }

                SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                ScanText(path, text, result);
                ScanSyntax(path, tree, root, result);
            }

            result.Pass = result.FrameLoopTrigHits == 0 &&
                          result.FrameLoopArrayAllocs == 0 &&
                          result.SolverForbiddenHotPathHits == 0 &&
                          result.HiddenOriginReadHits == 0 &&
                          result.AupShiftSequenceFields >= 3;
            return result;
        }

        private static void ScanText(string path, string text, ScanResult result)
        {
            string normalized = path.Replace('\\', '/');
            bool analyticalWaveFile = normalized.IndexOf("/Physics/Buoyancy/AnalyticalGerstnerWave", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!analyticalWaveFile)
                return;

            int directOriginReads = CountOccurrences(text, "HectonFloatingOrigin.CurrentTotalOffsetDouble");
            if (directOriginReads > 0)
            {
                result.HiddenOriginReadHits += directOriginReads;
                result.AddFinding("HIDDEN_ORIGIN_REGISTRY_READ", path, LineOfText(text, "HectonFloatingOrigin.CurrentTotalOffsetDouble"), "Analytical wave authority must consume cached origin-shift snapshots, not registry-backed origin reads.");
            }

            if (normalized.EndsWith("/AnalyticalGerstnerWaveContracts.cs", StringComparison.OrdinalIgnoreCase))
            {
                result.AupShiftSequenceFields += CountOccurrences(text, "ShiftFrameID");
                result.AupShiftSequenceFields += CountOccurrences(text, "OriginShiftSequence");
                if (text.IndexOf("[FieldOffset(0)] public double3 SampleAUP", StringComparison.Ordinal) >= 0 &&
                    text.IndexOf("ShiftFrameID", StringComparison.Ordinal) < 0)
                {
                    result.RawAupWithoutShiftHits++;
                    result.AddFinding("RAW_AUP_WITHOUT_SHIFT_SEQUENCE", path, LineOfText(text, "SampleAUP"), "Raw double3 AUP payload requires a shift sequence field for rollback/rebase proof.");
                }
            }
        }

        private static void ScanSyntax(string path, SyntaxTree tree, CompilationUnitSyntax root, ScanResult result)
        {
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is ClassDeclarationSyntax classDeclaration))
                    continue;

                string name = classDeclaration.Identifier.ValueText;
                if (name.IndexOf("Wave", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    InheritsMonoBehaviour(classDeclaration) &&
                    IsWaveAuthorityCandidate(path, name))
                {
                    result.WaveMonoBehaviourCandidates++;
                    result.AddFinding("WAVE_MONOBEHAVIOUR", path, LineOf(tree, classDeclaration), "Wave-named MonoBehaviour candidate; keep math out of object Update loops.");
                }
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is IdentifierNameSyntax identifier))
                    continue;

                string token = identifier.Identifier.ValueText;
                if (token == "AsyncGPUReadback" || token == "AsyncGPUReadbackRequest")
                    result.AsyncGpuReadbackTypeHits++;
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!(node is MethodDeclarationSyntax method))
                    continue;

                string methodName = method.Identifier.ValueText;
                bool frameLoop = methodName == "Update" || methodName == "FixedUpdate" || methodName == "LateUpdate";
                bool solverHotPath = IsSolverHotPath(path, methodName);
                bool hotPath = frameLoop || solverHotPath;
                if (!hotPath)
                    continue;

                foreach (SyntaxNode child in method.DescendantNodes())
                {
                    if (!(child is InvocationExpressionSyntax invocation))
                        continue;

                    if (IsMathfTrig(invocation))
                    {
                        result.FrameLoopTrigHits++;
                        result.AddFinding("MATHF_TRIG_FRAME_LOOP", path, LineOf(tree, invocation), "Mathf." + "Sin/Cos in frame loop; Burst wave kernel must own hot trig.");
                    }

                    if (IsGpuWaitInvocation(invocation))
                    {
                        result.GpuWaitSymbolHits++;
                        result.AddFinding("GPU_WAIT_HOT_PATH", path, LineOf(tree, invocation), "GPU wait/readback invocation in hot path.");
                    }

                    if (solverHotPath && IsSolverForbiddenInvocation(invocation))
                    {
                        result.SolverForbiddenHotPathHits++;
                        result.AddFinding("SOLVER_FORBIDDEN_INVOCATION", path, LineOf(tree, invocation), "Forbidden call in SHINOBU_263 solver hot path.");
                    }
                }

                foreach (SyntaxNode child in method.DescendantNodes())
                {
                    if (!(child is ArrayCreationExpressionSyntax allocation))
                        continue;

                    if (IsFloatArrayCreation(allocation))
                    {
                        result.FrameLoopArrayAllocs++;
                        result.AddFinding("FRAME_LOOP_FLOAT_ARRAY_ALLOC", path, LineOf(tree, allocation), "new float[] inside frame loop; wave spectra must be Vault/Burst-owned.");
                    }
                }

                if (solverHotPath)
                    ScanSolverForbiddenSyntax(path, tree, method, result);
            }
        }

        private static bool IsSolverHotPath(string path, string methodName)
        {
            string normalized = path.Replace('\\', '/');
            bool analyticalFile = normalized.IndexOf("/Physics/Buoyancy/AnalyticalGerstnerWave", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!analyticalFile)
                return false;

            return methodName == "FixedTick" ||
                   methodName == "PostFixedTick" ||
                   methodName == "Tick" ||
                   methodName == "LateTick" ||
                   methodName == "Execute" ||
                   methodName == "LocalizeAupXZ" ||
                   methodName == "StoreResult" ||
                   methodName == "StoreStaleResult" ||
                   methodName == "ResolveOriginProjectionModulo" ||
                   methodName == "ResolvePhaseTimeSeconds" ||
                   methodName == "ResolveTimePhaseModulo" ||
                   methodName == "ResolveDeepWaterPhaseVelocity" ||
                   methodName == "ResolveOctaveBudget" ||
                   methodName == "ResolveOctaveWeight" ||
                   methodName == "EvaluateScalar" ||
                   methodName == "BuildWave" ||
                   methodName == "SampleMacroHeight" ||
                   methodName == "SampleMacroHeight4" ||
                   methodName == "ResolveCoarseMask" ||
                   methodName == "ResolveAmplitude" ||
                   methodName == "ResolveNormal" ||
                   methodName == "TryPrepareRuntimeVault" ||
                   methodName == "TryResolveRuntimeBuffers" ||
                   methodName == "ResolveTelemetryBuffers" ||
                   methodName == "TryLockJobBuffers" ||
                   methodName == "TryLockTelemetryBuffers" ||
                   methodName == "TryLock" ||
                   methodName == "ClearCounterLanes" ||
                   methodName == "UnlockJobBuffers" ||
                   methodName == "Unlock" ||
                   methodName == "PrepareTuning" ||
                   methodName == "ConsumeMockRequestSeedGate";
        }

        private static void ScanSolverForbiddenSyntax(string path, SyntaxTree tree, MethodDeclarationSyntax method, ScanResult result)
        {
            foreach (SyntaxNode node in method.DescendantNodes())
            {
                if (!(node is MemberAccessExpressionSyntax member))
                    continue;

                string text = member.ToString();
                if (text.IndexOf("GlobalRegistry.", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Application.", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Time.", StringComparison.Ordinal) >= 0)
                {
                    result.SolverForbiddenHotPathHits++;
                    result.AddFinding("SOLVER_FORBIDDEN_MEMBER", path, LineOf(tree, member), "GlobalRegistry/Application/Time member access in SHINOBU_263 solver hot path.");
                }
            }

            foreach (SyntaxNode node in method.DescendantNodes())
            {
                if (!(node is ObjectCreationExpressionSyntax allocation))
                    continue;

                if (IsAllowedHotValueConstruction(allocation))
                    continue;

                result.SolverForbiddenHotPathHits++;
                result.AddFinding("SOLVER_MANAGED_ALLOC", path, LineOf(tree, allocation), "Managed object allocation in SHINOBU_263 solver hot path.");
            }

            foreach (SyntaxNode node in method.DescendantNodes())
            {
                if (!(node is ArrayCreationExpressionSyntax allocation))
                    continue;

                result.SolverForbiddenHotPathHits++;
                result.AddFinding("SOLVER_ARRAY_ALLOC", path, LineOf(tree, allocation), "Array allocation in SHINOBU_263 solver hot path.");
            }

            foreach (SyntaxNode node in method.DescendantNodes())
            {
                if (!(node is ForEachStatementSyntax statement))
                    continue;

                result.SolverForbiddenHotPathHits++;
                result.AddFinding("SOLVER_FOREACH", path, LineOf(tree, statement), "foreach in SHINOBU_263 solver hot path.");
            }
        }

        private static bool IsMathfTrig(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax member))
                return false;

            string memberName = member.Name.Identifier.ValueText;
            if (memberName != "Sin" && memberName != "Cos")
                return false;

            string receiver = member.Expression.ToString();
            return receiver == "Mathf" || receiver == "UnityEngine.Mathf";
        }

        private static bool IsGpuWaitInvocation(InvocationExpressionSyntax invocation)
        {
            string name = invocation.Expression is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.ValueText
                : invocation.Expression.ToString();
            return name == "ReadPixels" ||
                   name == "WaitForCompletion" ||
                   name == "GetData";
        }

        private static bool IsSolverForbiddenInvocation(InvocationExpressionSyntax invocation)
        {
            string text = invocation.Expression.ToString();
            string name = invocation.Expression is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.ValueText
                : text;

            return name == "Complete" ||
                   name == "ToArray" ||
                   text.IndexOf(".OfType", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf(".Select", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf(".Where", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("GlobalRegistry.", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("Application.", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("Time.", StringComparison.Ordinal) >= 0;
        }

        private static bool IsAllowedHotValueConstruction(ObjectCreationExpressionSyntax allocation)
        {
            string type = allocation.Type.ToString();
            return type == "bool4" ||
                   type == "float2" ||
                   type == "float3" ||
                   type == "float4" ||
                   type == "double3" ||
                   type.EndsWith("DTO", StringComparison.Ordinal) ||
                   type.EndsWith("Job", StringComparison.Ordinal);
        }

        private static bool IsFloatArrayCreation(ArrayCreationExpressionSyntax allocation)
        {
            string element = allocation.Type.ElementType.ToString();
            return element == "float" ||
                   element == "Single" ||
                   element == "float2" ||
                   element == "float3" ||
                   element == "float4";
        }

        private static bool InheritsMonoBehaviour(ClassDeclarationSyntax classDeclaration)
        {
            BaseListSyntax baseList = classDeclaration.BaseList;
            if (baseList == null)
                return false;

            foreach (BaseTypeSyntax baseType in baseList.Types)
            {
                string text = baseType.Type.ToString();
                if (text == "MonoBehaviour" || text == "UnityEngine.MonoBehaviour")
                    return true;
            }

            return false;
        }

        private static bool IsWaveAuthorityCandidate(string path, string className)
        {
            if (className == "AnalyticalGerstnerWaveRuntime")
                return false;

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Physics/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Environment/Fluids/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int LineOf(SyntaxTree tree, SyntaxNode node)
        {
            return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
        }

        private static bool ShouldSkip(string path)
        {
            return path.IndexOf("\\Library\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }

        private static void WriteReportFile(string path, string json)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static void UpsertSharedReportSection(string sharedPath, string sectionKey, string sectionJson)
        {
            string existing = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : "{\n}\n";
            string withoutSection = RemoveTopLevelSection(existing, sectionKey);
            int objectEnd = withoutSection.LastIndexOf('}');
            if (objectEnd < 0)
                withoutSection = "{\n}\n";

            objectEnd = withoutSection.LastIndexOf('}');
            string prefix = withoutSection.Substring(0, objectEnd).TrimEnd();
            bool hasExistingFields = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            StringBuilder builder = new StringBuilder(prefix.Length + sectionJson.Length + sectionKey.Length + 32);
            builder.Append(prefix);
            builder.Append(hasExistingFields ? ",\n" : "\n");
            builder.Append("  \"").Append(sectionKey).Append("\": ");
            AppendIndentedJson(builder, sectionJson, 2);
            builder.Append("\n}\n");
            WriteReportFile(sharedPath, builder.ToString());
        }

        private static string RemoveTopLevelSection(string json, string sectionKey)
        {
            if (string.IsNullOrEmpty(json))
                return "{\n}\n";

            int objectStart = json.IndexOf('{');
            int objectEnd = json.LastIndexOf('}');
            if (objectStart < 0 || objectEnd <= objectStart)
                return "{\n}\n";

            int keyStart = FindTopLevelKey(json, sectionKey, objectStart, objectEnd);
            if (keyStart < 0)
                return json;

            int propertyStart = keyStart;
            while (propertyStart > objectStart + 1 && char.IsWhiteSpace(json[propertyStart - 1]))
                propertyStart--;
            if (propertyStart > objectStart + 1 && json[propertyStart - 1] == ',')
            {
                propertyStart--;
                while (propertyStart > objectStart + 1 && char.IsWhiteSpace(json[propertyStart - 1]))
                    propertyStart--;
            }

            int colon = json.IndexOf(':', keyStart);
            if (colon < 0 || colon > objectEnd)
                return json;

            int propertyEnd = FindTopLevelValueEnd(json, colon + 1, objectEnd);
            int after = propertyEnd;
            while (after < objectEnd && char.IsWhiteSpace(json[after]))
                after++;
            if (after < objectEnd && json[after] == ',')
            {
                after++;
                while (after < objectEnd && char.IsWhiteSpace(json[after]))
                    after++;
            }

            return json.Remove(propertyStart, Math.Max(0, after - propertyStart));
        }

        private static int FindTopLevelKey(string json, string sectionKey, int objectStart, int objectEnd)
        {
            int depth = 1;
            for (int i = objectStart + 1; i < objectEnd; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int end = FindStringEnd(json, i + 1, objectEnd);
                    if (depth == 1 &&
                        end > i &&
                        end - i - 1 == sectionKey.Length &&
                        string.CompareOrdinal(json, i + 1, sectionKey, 0, sectionKey.Length) == 0)
                    {
                        int cursor = end + 1;
                        while (cursor < objectEnd && char.IsWhiteSpace(json[cursor]))
                            cursor++;
                        if (cursor < objectEnd && json[cursor] == ':')
                            return i;
                    }

                    i = end;
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
            }

            return -1;
        }

        private static int FindTopLevelValueEnd(string json, int valueStart, int objectEnd)
        {
            int depth = 0;
            for (int i = valueStart; i < objectEnd; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    i = FindStringEnd(json, i + 1, objectEnd);
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    if (depth == 0)
                        return i;
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return i;
                }
            }

            return objectEnd;
        }

        private static int FindStringEnd(string json, int start, int limit)
        {
            bool escaped = false;
            for (int i = start; i < limit; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                    return i;
            }

            return limit;
        }

        private static void AppendIndentedJson(StringBuilder builder, string json, int spaces)
        {
            string trimmed = string.IsNullOrEmpty(json) ? "{}" : json.Trim();
            string pad = new string(' ', spaces);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                builder.Append(c);
                if (c == '\n' && i + 1 < trimmed.Length)
                    builder.Append(pad);
            }
        }

        private static int CountOccurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                cursor = index + token.Length;
            }

            return count;
        }

        private static int LineOfText(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int index = text.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return 0;

            int line = 1;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        public sealed class ScanResult
        {
            private readonly List<Finding> _findings = new List<Finding>(MaxFindings);

            public int FilesScanned;
            public int WaveMonoBehaviourCandidates;
            public int FrameLoopTrigHits;
            public int FrameLoopArrayAllocs;
            public int AsyncGpuReadbackTypeHits;
            public int GpuWaitSymbolHits;
            public int SolverForbiddenHotPathHits;
            public int HiddenOriginReadHits;
            public int RawAupWithoutShiftHits;
            public int AupShiftSequenceFields;
            public bool Pass;

            public void AddFinding(string code, string path, int line, string message)
            {
                if (_findings.Count >= MaxFindings)
                    return;

                _findings.Add(new Finding
                {
                    Code = code,
                    Path = NormalizePath(path),
                    Line = line,
                    Message = message
                });
            }

            public string ToJson()
            {
                StringBuilder builder = new StringBuilder(4096);
                builder.Append("{\n");
                AppendJson(builder, "agent", "SHINOBU_263", true);
                AppendJson(builder, "scanner", nameof(Wave_Math_Scanner), true);
                AppendJson(builder, "verdict", Pass ? "PASS" : "REVIEW", true);
                AppendJson(builder, "summary", Pass ? "OOP Wave Math Eradicated" : "OOP Wave Math Requires Review", true);
                builder.Append("  \"filesScanned\": ").Append(FilesScanned).Append(",\n");
                builder.Append("  \"waveMonoBehaviourCandidates\": ").Append(WaveMonoBehaviourCandidates).Append(",\n");
                builder.Append("  \"frameLoopTrigHits\": ").Append(FrameLoopTrigHits).Append(",\n");
                builder.Append("  \"frameLoopArrayAllocs\": ").Append(FrameLoopArrayAllocs).Append(",\n");
                builder.Append("  \"asyncGpuReadbackTypeHits\": ").Append(AsyncGpuReadbackTypeHits).Append(",\n");
                builder.Append("  \"gpuWaitSymbolHits\": ").Append(GpuWaitSymbolHits).Append(",\n");
                builder.Append("  \"solverForbiddenHotPathHits\": ").Append(SolverForbiddenHotPathHits).Append(",\n");
                builder.Append("  \"hiddenOriginReadHits\": ").Append(HiddenOriginReadHits).Append(",\n");
                builder.Append("  \"rawAupWithoutShiftHits\": ").Append(RawAupWithoutShiftHits).Append(",\n");
                builder.Append("  \"aupShiftSequenceFields\": ").Append(AupShiftSequenceFields).Append(",\n");
                builder.Append("  \"findings\": [\n");
                for (int i = 0; i < _findings.Count; i++)
                {
                    Finding finding = _findings[i];
                    builder.Append("    {");
                    AppendJsonInline(builder, "code", finding.Code, true);
                    AppendJsonInline(builder, "path", finding.Path, true);
                    builder.Append("\"line\": ").Append(finding.Line).Append(", ");
                    AppendJsonInline(builder, "message", finding.Message, false);
                    builder.Append("}");
                    if (i + 1 < _findings.Count)
                        builder.Append(',');
                    builder.Append('\n');
                }

                builder.Append("  ]\n");
                builder.Append("}\n");
                return builder.ToString();
            }

            private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
            {
                builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
                builder.Append(comma ? ",\n" : "\n");
            }

            private static void AppendJsonInline(StringBuilder builder, string key, string value, bool comma)
            {
                builder.Append('"').Append(key).Append("\": \"").Append(Escape(value)).Append('"');
                builder.Append(comma ? ", " : string.Empty);
            }

            private static string NormalizePath(string path)
            {
                return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
            }

            private static string Escape(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;

                return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }

            private struct Finding
            {
                public string Code;
                public string Path;
                public int Line;
                public string Message;
            }
        }
    }
}
#endif
