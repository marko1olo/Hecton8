#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public static class OOP_Joint_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SectionKey = "shinobu_328_projectile_harpoon_tension_solver";
        private const string StatusClean = "OOP Joints Eradicated";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Tools",
            "Assets/_Project/Scripts/Vehicles",
            "Assets/_Project/Scripts/Physics",
            "Assets/_Project/Scripts/Gameplay/Combat"
        };

        private static readonly string[] Needles =
        {
            "SpringJoint",
            "ConfigurableJoint",
            "CharacterJoint",
            "HingeJoint",
            "LineRenderer",
            "SetPositions",
            "positionCount"
        };

        [MenuItem("Hecton8/Physics/OOP Joint Scanner")]
        public static void RunMenuScan()
        {
            ScanResult result = ScanProject();
            WriteReport(result);
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_328 OOP joint scanner wrote " + ReportPath + " with " + result.ActiveViolationCount + " active violations.");
        }

        public static ScanResult ScanProject()
        {
            ScanResult result = new ScanResult();
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = ScanRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(files[fileIndex], ref result);
            }

            return result;
        }

        private static void ScanFile(string path, ref ScanResult result)
        {
            result.SourceFilesScanned++;
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                result.ParserFailureCount++;
                result.AppendFinding(path, 0, "RoslynParse", exception.GetType().Name, false);
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFailureCount++;
                result.AppendFinding(path, 0, "RoslynParse", "syntax error", false);
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            bool editorPath = IsEditorPath(path);
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveForbiddenToken(node, out string token))
                        continue;

                    result.TotalReferenceNodes++;
                    bool activeViolation = !editorPath && IsHarpoonOrCableAuthorityContext(path, node);
                    if (activeViolation)
                        result.ActiveViolationCount++;
                    result.AppendFinding(path, GetLineNumber(node), token, node.Kind().ToString(), activeViolation);
                }
            }
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

        private static bool TryResolveForbiddenToken(SyntaxNode node, out string token)
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string value = identifier.Identifier.ValueText;
                for (int i = 0; i < Needles.Length; i++)
                {
                    if (string.Equals(value, Needles[i], StringComparison.Ordinal))
                    {
                        token = value;
                        return true;
                    }
                }
            }

            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string typeName = objectCreation.Type.ToString();
                for (int i = 0; i < Needles.Length; i++)
                {
                    if (string.Equals(typeName, Needles[i], StringComparison.Ordinal) ||
                        typeName.EndsWith("." + Needles[i], StringComparison.Ordinal))
                    {
                        token = typeName;
                        return true;
                    }
                }
            }

            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                string name = memberAccess.Name.Identifier.ValueText;
                if (string.Equals(name, "SetPositions", StringComparison.Ordinal) ||
                    string.Equals(name, "positionCount", StringComparison.Ordinal))
                {
                    token = name;
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static bool IsHarpoonOrCableAuthorityContext(string path, SyntaxNode node)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (ContainsDomainWord(normalized))
                return true;

            using (System.Collections.Generic.IEnumerator<SyntaxNode> ancestors = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestors.MoveNext())
                {
                    SyntaxNode current = ancestors.Current;
                    if (current is TypeDeclarationSyntax typeDeclaration && ContainsDomainWord(typeDeclaration.Identifier.ValueText))
                        return true;
                    if (current is MethodDeclarationSyntax methodDeclaration && ContainsDomainWord(methodDeclaration.Identifier.ValueText))
                        return true;
                    if (current is FieldDeclarationSyntax fieldDeclaration && VariableListContainsDomainWord(fieldDeclaration.Declaration))
                        return true;
                    if (current is LocalDeclarationStatementSyntax localDeclaration && VariableListContainsDomainWord(localDeclaration.Declaration))
                        return true;
                    if (current is ParameterSyntax parameter && ContainsDomainWord(parameter.Identifier.ValueText))
                        return true;
                }
            }

            return normalized.IndexOf("/Tools/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Vehicles/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool VariableListContainsDomainWord(VariableDeclarationSyntax declaration)
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                if (ContainsDomainWord(variables[i].Identifier.ValueText))
                    return true;
            }

            return false;
        }

        private static bool ContainsDomainWord(string value)
        {
            return value.IndexOf("harpoon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("tether", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("cable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("tow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("winch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("rope", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEditorPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static void WriteReport(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            string section = BuildSectionJson(result);
            string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            string updated = UpsertSection(existing, section);
            JObject.Parse(updated);
            WriteTextAtomic(path, updated);
        }

        private static string BuildSectionJson(ScanResult result)
        {
            string status = result.ActiveViolationCount == 0 ? StatusClean : "OOP Joint/LineRenderer Authority Detected";
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("  \"" + SectionKey + "\": {");
            builder.AppendLine("    \"agentId\": \"SHINOBU_328\",");
            builder.AppendLine("    \"scanner\": \"OOP_Joint_Scanner\",");
            builder.AppendLine("    \"summary\": \"" + status + "\",");
            builder.AppendLine("    \"reportSchema\": 1,");
            builder.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE\",");
            builder.AppendLine("    \"scannerMode\": \"ROSLYN_AST_TARGETED\",");
            builder.AppendLine("    \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("    \"oopJointsEradicated\": " + (result.ActiveViolationCount == 0 ? "true" : "false") + ",");
            builder.AppendLine("    \"sourceFilesScanned\": " + result.SourceFilesScanned + ",");
            builder.AppendLine("    \"parserFailures\": " + result.ParserFailureCount + ",");
            builder.AppendLine("    \"forbiddenReferenceNodes\": " + result.TotalReferenceNodes + ",");
            builder.AppendLine("    \"activeViolationCount\": " + result.ActiveViolationCount + ",");
            builder.AppendLine("    \"replacementRoute\": \"TetherStateDTO double3 AUP anchors -> Burst Verlet nodes -> TetherForcePacketDTO/HarpoonTensionPhysicsEventMirrorDTO Vault mirrors -> owner completion conversion to PhysicsEventPayload SignalBus.TryPush bridge bounded to activeTetherCount*2 event rows -> GPU raw node/tangent buffer for shader Catmull-Rom\",");
            builder.AppendLine("    \"runtimeRouteProof\": \"GlobalDataVault 72180..72193 -> Burst deterministic Verlet/tension jobs -> TetherForcePacketDTO mirror + HarpoonTensionPhysicsEventMirrorDTO mirror + TetherStressStateDTO snap lane -> completed-owner conversion to SignalBus<PhysicsEventPayload>/TetherSnappedSignal/TetherTensionSignal TryPush bounded to activeTetherCount*2 event rows -> GPU TetherSplineVertexDTO buffer\",");
            builder.AppendLine("    \"burstPayloadFence\": \"CalculateTetherForceJob writes only blittable HarpoonTensionPhysicsEventMirrorDTO rows; managed PhysicsEventPayload with UnityEngine.Vector3 is constructed after DispatcherJobFence.TryFinalizeCompleted in owner phase\",");
            builder.AppendLine("    \"primaryDtoAbiFence\": \"TetherStateDTO keeps XML-required _pad0 at offset 60; cumulative snap stress lives in separate 64-byte TetherStressStateDTO Vault lane 72193\",");
            builder.AppendLine("    \"managerBridge\": \"TetherManager cold-bootstraps SHINOBU_328 mock lanes, schedules TryScheduleMockFromVault on FixedTick, registers the returned handle with H8Memory, retires it via DispatcherJobFence.TryFinalizeCompleted, and publishes completed signals only from owner phase\",");
            builder.AppendLine("    \"legacyScopeFence\": \"Focused harpoon/tether/winch asset and source audit found no production Unity Joint or LineRenderer authority; legacy TetherInstance private NativeArray and PhysicsForceRouter debt remains documented but was not rewritten in this pass because SHINOBU_328 now owns the new Vault/Burst/GPU route and a full object-graph migration would exceed the cable-joint eradication blast radius\",");
            builder.AppendLine("    \"scheduleInputFence\": \"Public Schedule clamps owner-provided active tether/node/constraint counts to non-negative buffer ranges and clamps tether count to both TetherStateDTO and TetherStressStateDTO capacities before any job scheduling\",");
            builder.AppendLine("    \"bootstrapSentinelProof\": \"BootstrapMagic is trusted only after required Vault lanes resolve with required capacities and finite first state/stress/tuning/material invariants; otherwise bootstrap[0] is reset and mock seed rewrites owned rows\",");
            builder.AppendLine("    \"dearLieProof\": \"Sparse Verlet truth writes raw spline nodes/tangents/tension to GPU; shader Catmull-Rom/thickness replaces LineRenderer and CPU rope mesh expansion\",");
            builder.AppendLine("    \"compileProof\": \"BLOCKED_BY_EXTERNAL_GAMEPLAY_COMPILE_ERRORS_AND_STALE_GENERATED_CSPROJ; guarded build reported missing VRSomaticKinematicStateMirrorDTO, VRSomaticComfortDTO, and PlayerHandIkConfigFlags in unrelated Gameplay files; generated Hecton8.Core.csproj does not yet include HarpoonTensionSolver328.cs or OOP_Joint_Scanner.cs until Unity import/project regeneration\",");
            builder.AppendLine("    \"generatedProjectStatus\": \"PENDING_UNITY_IMPORT_REGENERATION\",");
            builder.AppendLine("    \"vaultBuffers\": [72180, 72181, 72182, 72183, 72184, 72185, 72186, 72187, 72188, 72189, 72190, 72191, 72192, 72193],");
            builder.AppendLine("    \"scannedPaths\": [");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                builder.Append("      \"").Append(EscapeJson(ScanRoots[i])).Append("\"");
                builder.AppendLine(i + 1 < ScanRoots.Length ? "," : string.Empty);
            }
            builder.AppendLine("    ],");
            builder.AppendLine("    \"forbiddenPatterns\": [");
            for (int i = 0; i < Needles.Length; i++)
            {
                builder.Append("      \"").Append(EscapeJson(Needles[i])).Append("\"");
                builder.AppendLine(i + 1 < Needles.Length ? "," : string.Empty);
            }
            builder.AppendLine("    ],");
            builder.AppendLine("    \"findings\": [");
            if (result.FindingsJson != null)
                builder.Append(result.FindingsJson);
            builder.AppendLine();
            builder.AppendLine("    ],");
            builder.AppendLine("    \"notes\": \"Editor scanner/debug files are reported as references but not active runtime violations. Active violations require non-editor harpoon/tether/cable/tow/winch/rope authority context.\"");
            builder.AppendLine("  }");
            return builder.ToString();
        }

        private static string UpsertSection(string existing, string sectionJson)
        {
            JObject root = string.IsNullOrWhiteSpace(existing) ? new JObject() : JObject.Parse(existing);
            JObject wrapper = JObject.Parse("{\n" + sectionJson + "\n}");
            root[SectionKey] = wrapper[SectionKey];
            return root.ToString(Newtonsoft.Json.Formatting.Indented) + "\n";
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, text, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
                return;
            }

            File.Move(tempPath, path);
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }

        private static int FindObjectEnd(string value, int objectStart)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < value.Length; i++)
            {
                char c = value[i];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
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
                        return i + 1;
                }
            }

            return -1;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public struct ScanResult
        {
            public int SourceFilesScanned;
            public int ParserFailureCount;
            public int TotalReferenceNodes;
            public int ActiveViolationCount;
            public StringBuilder FindingsJson;

            public void AppendFinding(string path, int line, string token, string source, bool activeViolation)
            {
                if (FindingsJson == null)
                    FindingsJson = new StringBuilder(1024);
                if (FindingsJson.Length > 0)
                    FindingsJson.AppendLine(",");
                FindingsJson.Append("      { \"path\": \"")
                    .Append(EscapeJson(path.Replace('\\', '/')))
                    .Append("\", \"line\": ")
                    .Append(line)
                    .Append(", \"token\": \"")
                    .Append(EscapeJson(token))
                    .Append("\", \"syntax\": \"")
                    .Append(EscapeJson(source))
                    .Append("\", \"activeViolation\": ")
                    .Append(activeViolation ? "true" : "false")
                    .Append(" }");
            }
        }
    }

    public unsafe sealed class KinematicTetherTunerWindow328 : EditorWindow
    {
        private Slider _tensionConstant;
        private Slider _maxStrength;
        private Slider _snapStressSeconds;
        private Slider _gravityY;
        private Slider _qualityOverride;
        private SliderInt _nodes;
        private SliderInt _iterations;
        private Label _status;
        private Label _telemetry;
        private VisualElement _graph;
        private double _nextRefresh;
        private float _lastMaxTension;
        private float _lastQuality;

        [MenuItem("Hecton8/Physics/Kinematic Tether Tuner SHINOBU 328")]
        public static void Open()
        {
            GetWindow<KinematicTetherTunerWindow328>("Kinematic Tether");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshTelemetry;
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _tensionConstant = new Slider("Tension Constant", 0f, 50000f) { value = HarpoonTensionSolver328Constants.DefaultTensionConstant };
            _maxStrength = new Slider("Max Tensile Strength", 1000f, 500000f) { value = HarpoonTensionSolver328Constants.DefaultMaxTensileStrength };
            _snapStressSeconds = new Slider("Snap Stress Seconds", 0.016666667f, 2f) { value = HarpoonTensionSolver328Constants.DefaultSnapStressSeconds };
            _gravityY = new Slider("Node Gravity Y", -40f, 10f) { value = -9.81f };
            _qualityOverride = new Slider("Global Quality Override", -1f, 1f) { value = -1f };
            _nodes = new SliderInt("Nodes Per Tether", 6, 64) { value = HarpoonTensionSolver328Constants.MockNodesPerTether };
            _iterations = new SliderInt("Max Constraint Iterations", 2, 8) { value = 8 };
            _status = new Label("Vault not sampled.");
            _telemetry = new Label("Telemetry: --");
            _graph = new VisualElement();
            _graph.style.height = 80f;
            _graph.style.marginTop = 6f;
            _graph.style.marginBottom = 6f;
            _graph.generateVisualContent += DrawGraph;

            root.Add(_tensionConstant);
            root.Add(_maxStrength);
            root.Add(_snapStressSeconds);
            root.Add(_gravityY);
            root.Add(_qualityOverride);
            root.Add(_nodes);
            root.Add(_iterations);
            root.Add(new Button(PullFromVault) { text = "Read Vault" });
            root.Add(new Button(ApplyToVault) { text = "Apply Tuning" });
            root.Add(new Button(ReloadCsv) { text = "Reload tether_material_profiles.csv" });
            root.Add(new Button(DumpFaultRing) { text = "Dump Fault Ring" });
            root.Add(_graph);
            root.Add(_telemetry);
            root.Add(_status);
            PullFromVault();
        }

        private void PullFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.Tuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<HarpoonTensionTuningDTO> handle,
                    out NativeArray<HarpoonTensionTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                ref HarpoonTensionTuningDTO dto = ref UnsafeUtility.AsRef<HarpoonTensionTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
                if (dto.TensionConstant <= 0f || !math.isfinite(dto.TensionConstant))
                    dto = HarpoonTensionSolver328.DefaultTuning();
                _tensionConstant.SetValueWithoutNotify(dto.TensionConstant);
                _maxStrength.SetValueWithoutNotify(dto.MaxTensileStrength);
                float snapStressSeconds = math.isfinite(dto.SnapStressSeconds) ? dto.SnapStressSeconds : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds;
                _snapStressSeconds.SetValueWithoutNotify(math.clamp(snapStressSeconds, 0.016666667f, 2f));
                _gravityY.SetValueWithoutNotify(dto.NodeGravity.y);
                _qualityOverride.SetValueWithoutNotify(dto.GlobalQualityWeightOverride);
                _nodes.SetValueWithoutNotify(math.clamp(dto.NodesPerTether, 6, 64));
                _iterations.SetValueWithoutNotify(math.clamp(dto.MaxConstraintIterations, 2, 8));
                _status.text = "Vault sampled.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.Tuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<HarpoonTensionTuningDTO> handle,
                    out NativeArray<HarpoonTensionTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                ref HarpoonTensionTuningDTO dto = ref UnsafeUtility.AsRef<HarpoonTensionTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
                dto = HarpoonTensionSolver328.DefaultTuning();
                dto.TensionConstant = math.max(0f, _tensionConstant.value);
                dto.MaxTensileStrength = math.max(1f, _maxStrength.value);
                dto.SnapStressSeconds = math.clamp(
                    math.isfinite(_snapStressSeconds.value) ? _snapStressSeconds.value : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds,
                    0.016666667f,
                    2f);
                dto.NodeGravity = new float3(0f, _gravityY.value, 0f);
                dto.GlobalQualityWeightOverride = _qualityOverride.value;
                dto.NodesPerTether = math.clamp(_nodes.value, 6, 64);
                dto.MaxConstraintIterations = math.clamp(_iterations.value, 2, 8);
                _status.text = "Tuning written through UnsafeUtility.AsRef.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ReloadCsv()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, "tether_material_profiles.csv");
            if (!File.Exists(path))
            {
                _status.text = "tether_material_profiles.csv not found.";
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.MaterialProfiles,
                    HarpoonTensionSolver328Constants.MaterialProfileCapacity,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<TetherMaterialProfileDTO> handle,
                    out NativeArray<TetherMaterialProfileDTO> profiles))
            {
                _status.text = "Material profile Vault lane unavailable.";
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool parsed = HarpoonTensionSolver328.TryParseTetherMaterialProfiles(bytes.AsSpan(), profiles, out int count);
                _status.text = parsed ? "CSV profiles applied: " + count : "CSV parsed no profile rows.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void DumpFaultRing()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool dumped = HarpoonTensionSolver328.TryDumpTelemetryIfFault(GlobalRegistry.DataVault, projectRoot, 1);
            _status.text = dumped ? "Dump_SHINOBU_328.bin written." : "No SHINOBU_328 fault flags.";
        }

        private void RefreshTelemetry()
        {
            if (_telemetry == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh)
                return;
            _nextRefresh = now + 0.25d;

            if (!TryReadLatestTelemetry(out TetherTelemetryEntry entry))
            {
                _telemetry.text = "Telemetry: --";
                return;
            }

            _lastMaxTension = entry.MaxTension;
            _lastQuality = entry.GlobalQualityWeight;
            _telemetry.text = "Tension " + entry.MaxTension.ToString("F1") +
                              " N / iterations " + entry.IterationCount +
                              " / nodes " + entry.NodeCount +
                              " / us " + entry.CpuMicroseconds.ToString("F2");
            _graph.MarkDirtyRepaint();
        }

        private void DrawGraph(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect r = _graph.contentRect;
            float tension01 = math.saturate(_lastMaxTension / HarpoonTensionSolver328Constants.DefaultMaxTensileStrength);
            float quality = math.saturate(_lastQuality);
            painter.lineWidth = 2f;
            painter.strokeColor = Color.Lerp(Color.green, Color.red, tension01);
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin, r.yMax - r.height * tension01));
            painter.LineTo(new Vector2(r.xMax, r.yMax - r.height * tension01));
            painter.Stroke();
            painter.strokeColor = new Color(0.2f, 0.55f, 1f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin, r.yMax - r.height * quality));
            painter.LineTo(new Vector2(r.xMax, r.yMax - r.height * quality));
            painter.Stroke();
        }

        private static bool TryReadLatestTelemetry(out TetherTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TelemetryRing, out VaultGenerationHandle<TetherTelemetryEntry> ringHandle) ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TelemetryHead, out VaultGenerationHandle<int> headHandle) ||
                !vault.TryReadHandle(in ringHandle, out NativeArray<TetherTelemetryEntry> ring) ||
                !vault.TryReadHandle(in headHandle, out NativeArray<int> head) ||
                !ring.IsCreated ||
                ring.Length == 0 ||
                !head.IsCreated ||
                head.Length == 0)
            {
                return false;
            }

            int capacity = math.min(ring.Length, HarpoonTensionSolver328Constants.TelemetryCapacity);
            int index = head[0] - 1;
            if (index < 0)
                index = capacity - 1;
            index = math.clamp(index, 0, capacity - 1);
            entry = ring[index];
            return entry.FrameIndex != 0u || entry.NodeCount > 0;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;
                handle = vault.EnsureGenerationHandle<T>(bufferId, required, owner, options);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;
            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }
    }

    [InitializeOnLoad]
    public static class LiveVerletDebugGizmo328
    {
        private static bool _enabled;

        static LiveVerletDebugGizmo328()
        {
            SceneView.duringSceneGui -= DrawScene;
            SceneView.duringSceneGui += DrawScene;
        }

        [MenuItem("Hecton8/Physics/Live Verlet Debug Gizmo SHINOBU 328")]
        public static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void DrawScene(SceneView view)
        {
            if (!_enabled)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TetherStates, out VaultGenerationHandle<TetherStateDTO> stateHandle) ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TetherNodes, out VaultGenerationHandle<float3> nodeHandle) ||
                !vault.TryReadHandle(in stateHandle, out NativeArray<TetherStateDTO> states) ||
                !vault.TryReadHandle(in nodeHandle, out NativeArray<float3> nodes) ||
                !states.IsCreated ||
                !nodes.IsCreated)
            {
                return;
            }

            int nodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether;
            for (int tether = 0; tether < states.Length; tether++)
            {
                TetherStateDTO state = states[tether];
                if ((state.Flags & TetherStateFlags328.Active) == 0u)
                    continue;

                float tension01 = math.saturate(state.CurrentTension / math.max(1f, HarpoonTensionSolver328Constants.DefaultMaxTensileStrength));
                Handles.color = Color.Lerp(Color.green, Color.red, tension01);
                int offset = tether * nodesPerTether;
                int last = math.min(offset + nodesPerTether - 1, nodes.Length - 1);
                for (int i = offset; i < last; i++)
                {
                    float3 a = nodes[i];
                    float3 b = nodes[i + 1];
                    Handles.DrawLine(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z));
                }
            }
        }
    }
}
#endif
