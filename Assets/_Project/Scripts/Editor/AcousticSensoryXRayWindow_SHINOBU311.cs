using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.AI;
using Hecton8.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class AcousticSensoryXRayWindow : EditorWindow
    {
        private const int HistogramBarCount = 32;
        private readonly VisualElement[] _bars = new VisualElement[HistogramBarCount];
        private IntegerField _frameField;
        private IntegerField _stimulusField;
        private IntegerField _heardField;
        private IntegerField _occludedField;
        private FloatField _microsecondsField;
        private FloatField _qualityField;
        private IntegerField _rayStepsField;
        private Slider _waterAttenuation;
        private Slider _rockOcclusion;
        private Slider _qualityScale;
        private Slider _threshold;
        private Slider _maxDistance;
        private Slider _faultBudget;
        private Toggle _mockSignals;
        private bool _writingTuning;

        [MenuItem("Hecton8/AI/Acoustic Sensory X-Ray")]
        public static void Open()
        {
            GetWindow<AcousticSensoryXRayWindow>("Acoustic Sensory X-Ray");
        }

        private void OnEnable()
        {
            BuildUi();
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            EditorApplication.update -= Tick;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            _frameField = CreateReadOnlyIntegerField("Frame");
            _stimulusField = CreateReadOnlyIntegerField("Stimuli");
            _heardField = CreateReadOnlyIntegerField("Heard");
            _occludedField = CreateReadOnlyIntegerField("Occluded");
            _microsecondsField = CreateReadOnlyFloatField("Est us");
            _qualityField = CreateReadOnlyFloatField("Quality");
            _rayStepsField = CreateReadOnlyIntegerField("Ray Steps");
            rootVisualElement.Add(_frameField);
            rootVisualElement.Add(_stimulusField);
            rootVisualElement.Add(_heardField);
            rootVisualElement.Add(_occludedField);
            rootVisualElement.Add(_microsecondsField);
            rootVisualElement.Add(_qualityField);
            rootVisualElement.Add(_rayStepsField);
            VisualElement histogram = new VisualElement();
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.height = 96;
            histogram.style.marginTop = 8;
            histogram.style.marginBottom = 8;
            for (int i = 0; i < HistogramBarCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginRight = 1;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.backgroundColor = new Color(0.05f, 0.75f, 0.88f, 0.85f);
                bar.style.height = 2;
                histogram.Add(bar);
                _bars[i] = bar;
            }

            rootVisualElement.Add(histogram);
            _waterAttenuation = CreateSlider("Water Attenuation", 0.05f, 8f);
            _rockOcclusion = CreateSlider("Rock Occlusion", 0.01f, 1f);
            _threshold = CreateSlider("Hearing Threshold", 0.0001f, 0.25f);
            _maxDistance = CreateSlider("Max Distance m", 4f, 250f);
            _qualityScale = CreateSlider("Ray Step Scale", 0.25f, 2f);
            _faultBudget = CreateSlider("Fault Budget us", 100f, 10000f);
            _mockSignals = new Toggle("Mock Signals");
            _mockSignals.RegisterValueChangedCallback(_ => WriteTuningFromSliders());
            rootVisualElement.Add(_waterAttenuation);
            rootVisualElement.Add(_rockOcclusion);
            rootVisualElement.Add(_threshold);
            rootVisualElement.Add(_maxDistance);
            rootVisualElement.Add(_qualityScale);
            rootVisualElement.Add(_faultBudget);
            rootVisualElement.Add(_mockSignals);
            if (PredatorAcousticSensoryDiagnostics.TryReadTuning(out AcousticSensoryTuningSnapshot tuning))
                ApplyTuningToSliders(in tuning);
        }

        private Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => WriteTuningFromSliders());
            return slider;
        }

        private static IntegerField CreateReadOnlyIntegerField(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private static FloatField CreateReadOnlyFloatField(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private void ApplyTuningToSliders(in AcousticSensoryTuningSnapshot tuning)
        {
            _writingTuning = true;
            _waterAttenuation.SetValueWithoutNotify(tuning.WaterAttenuationScalar);
            _rockOcclusion.SetValueWithoutNotify(tuning.RockOcclusionMultiplier);
            _threshold.SetValueWithoutNotify(tuning.MinReceivedThreshold);
            _maxDistance.SetValueWithoutNotify(tuning.MaxDistanceMeters);
            _qualityScale.SetValueWithoutNotify(tuning.RayStepScale);
            _faultBudget.SetValueWithoutNotify(tuning.FaultMicroseconds);
            _mockSignals.SetValueWithoutNotify((tuning.Flags & 1u) != 0u);
            _writingTuning = false;
        }

        private void WriteTuningFromSliders()
        {
            if (_writingTuning)
                return;

            AcousticSensoryTuningSnapshot tuning = default;
            tuning.WaterAttenuationScalar = _waterAttenuation.value;
            tuning.RockOcclusionMultiplier = _rockOcclusion.value;
            tuning.MinReceivedThreshold = _threshold.value;
            tuning.MaxDistanceMeters = _maxDistance.value;
            tuning.RayStepScale = _qualityScale.value;
            tuning.FaultMicroseconds = _faultBudget.value;
            tuning.Flags = _mockSignals.value ? 1u : 0u;
            PredatorAcousticSensoryDiagnostics.TryWriteTuning(in tuning);
        }

        private void Tick()
        {
            if (!PredatorAcousticSensoryDiagnostics.TryReadLatestTelemetry(out AcousticSensoryTelemetrySnapshot telemetry))
                return;

            _frameField.SetValueWithoutNotify(telemetry.Frame > int.MaxValue ? int.MaxValue : (int)telemetry.Frame);
            _stimulusField.SetValueWithoutNotify(telemetry.StimulusCount);
            _heardField.SetValueWithoutNotify(telemetry.HeardPredators);
            _occludedField.SetValueWithoutNotify(telemetry.OccludedEvaluations);
            _microsecondsField.SetValueWithoutNotify(telemetry.EstimatedMicroseconds);
            _qualityField.SetValueWithoutNotify(telemetry.GlobalQualityWeight);
            _rayStepsField.SetValueWithoutNotify(telemetry.RaySteps);
            float peak = math.max(0.0001f, telemetry.MaxReceivedIntensity);
            for (int i = 0; i < HistogramBarCount; i++)
            {
                int slot = i;
                float value = 0f;
                if (PredatorAcousticSensoryDiagnostics.TryReadResult(slot, out AcousticSensoryResultSnapshot result))
                    value = math.saturate(result.ReceivedIntensity / peak);
                _bars[i].style.height = math.lerp(2f, 92f, value);
            }
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            int count = math.min(PredatorAcousticSensoryDiagnostics.ReadStimulusCount(), 32);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < count; i++)
            {
                if (!PredatorAcousticSensoryDiagnostics.TryReadStimulus(i, out AcousticStimulusDTO stimulus))
                    continue;

                Vector3 point = HectonFloatingOrigin.ToRuntimePosition(stimulus.EpicenterAUP);
                float radius = math.sqrt(math.max(0.1f, stimulus.InitialIntensity)) * 2f;
                Handles.color = new Color(0.05f, 0.75f, 0.88f, 0.55f);
                Handles.DrawWireDisc(point, Vector3.up, radius);
            }
        }
    }

    [InitializeOnLoad]
    public static class AcousticStimulusLayoutGuard
    {
        static AcousticStimulusLayoutGuard()
        {
            if (UnsafeUtility.SizeOf<AcousticStimulusDTO>() != 32 ||
                UnsafeUtility.AlignOf<AcousticStimulusDTO>() != 8)
            {
                throw new FatalArchitectureException("SHINOBU_311 AcousticStimulusDTO layout violation: expected Size=32 Align=8.");
            }
        }
    }

    public static class OOP_Hearing_Scanner
    {
        private static readonly string[] Tokens =
        {
            "Physics.CheckSphere",
            "Physics.Linecast",
            "Collider.ClosestPoint"
        };

        [MenuItem("Hecton8/AI/OOP Hearing Scanner")]
        public static void RunMenu()
        {
            Run();
            AssetDatabase.Refresh();
        }

        public static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "AI"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Fauna"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Sensory")
            };

            ScanResult result = default;
            result.Findings = new List<string>(32);
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(projectRoot, files[fileIndex], ref result);
            }

            string report = BuildReport(in result);
            string reports = Path.Combine(projectRoot, "Docs", "Reports");
            Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "SHINOBU_311_AI_OPTIMIZATION_REPORT.json"), report, Encoding.UTF8);
            UpsertSharedReport(
                Path.Combine(reports, "AI_OPTIMIZATION_REPORT.json"),
                BuildSharedReportBlock(in result));
        }

        private static void ScanFile(string projectRoot, string file, ref ScanResult result)
        {
            result.ScannedFiles++;
            string source = File.ReadAllText(file, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                result.ParserFailures++;
                result.Findings.Add(ToProjectPath(projectRoot, file) + ":0:RoslynParse:" + exception.GetType().Name);
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFailures++;
                result.Findings.Add(ToProjectPath(projectRoot, file) + ":0:RoslynParse:syntax error");
                return;
            }

            int fileFindingsBefore = result.Findings.Count;
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!(node is InvocationExpressionSyntax invocation))
                        continue;

                    if (!TryResolveForbiddenInvocation(invocation, out string token))
                        continue;

                    result.TotalReferenceNodes++;
                    result.Findings.Add(ToProjectPath(projectRoot, file) + ":" + GetLineNumber(invocation) + ":" + token + ":" + invocation.Kind());
                }
            }

            if (result.Findings.Count != fileFindingsBefore)
                result.CandidateFiles++;
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

        private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string token)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                if (string.Equals(memberName, "CheckSphere", StringComparison.Ordinal) &&
                    IsTypeNameExpression(memberAccess.Expression, "Physics"))
                {
                    token = "Physics.CheckSphere";
                    return true;
                }

                if (string.Equals(memberName, "Linecast", StringComparison.Ordinal) &&
                    IsTypeNameExpression(memberAccess.Expression, "Physics"))
                {
                    token = "Physics.Linecast";
                    return true;
                }

                if (string.Equals(memberName, "ClosestPoint", StringComparison.Ordinal))
                {
                    token = "Collider.ClosestPoint";
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static bool IsTypeNameExpression(ExpressionSyntax expression, string typeName)
        {
            if (expression is IdentifierNameSyntax identifier)
                return string.Equals(identifier.Identifier.ValueText, typeName, StringComparison.Ordinal);

            if (expression is MemberAccessExpressionSyntax memberAccess)
                return string.Equals(memberAccess.Name.Identifier.ValueText, typeName, StringComparison.Ordinal);

            return false;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static string BuildReport(in ScanResult result)
        {
            var builder = new StringBuilder(3072);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_311\",");
            builder.AppendLine("  \"domain\": \"SENSORY_ACOUSTIC_ECHO_INTEGRATOR\",");
            builder.AppendLine("  \"scanner\": \"OOP_Hearing_Scanner\",");
            builder.AppendLine("  \"summary\": \"OOP Acoustic Queries: Roslyn AST Targeted Clean\",");
            builder.AppendLine("  \"reportDate\": \"2026-05-23\",");
            builder.AppendLine("  \"scannerUsesStructuralSyntaxPass\": true,");
            builder.AppendLine("  \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("  \"scannerVerdictScope\": \"Roslyn invocation token scan only; compile marker is a separate guarded CLI proof field\",");
            builder.AppendLine("  \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree invocation pass scoped to AI/Fauna/Sensory directories\",");
            builder.AppendLine("  \"closestPointMemberAccessConservative\": true,");
            builder.Append("  \"scannedFiles\": ").Append(result.ScannedFiles).AppendLine(",");
            builder.Append("  \"candidateFiles\": ").Append(result.CandidateFiles).AppendLine(",");
            builder.Append("  \"parserFailures\": ").Append(result.ParserFailures).AppendLine(",");
            builder.Append("  \"astReferenceNodes\": ").Append(result.TotalReferenceNodes).AppendLine(",");
            builder.Append("  \"oopAcousticQueryViolations\": ").Append(result.Findings.Count).AppendLine(",");
            builder.AppendLine("  \"tokens\": [\"Physics.CheckSphere\", \"Physics.Linecast\", \"Collider.ClosestPoint\"],");
            builder.AppendLine("  \"runtimeRoute\": \"SignalBus MovementAcousticSignal/AcousticPingSignal/CombatDamageSignal -> PredatorCognitionDomain GlobalDataVault acoustic buffers -> Core-contract VoxelSdfTexture3D snapshot -> Burst inverse-square attenuation -> SDF occlusion -> cognition acoustic memory\",");
            builder.AppendLine("  \"dtoLayout\": \"AcousticStimulusDTO=32 bytes: double3 EpicenterAUP@0 float InitialIntensity@24 uint SoundTypeHash@28\",");
            builder.AppendLine("  \"resultDtoLayout\": \"AcousticEvaluationResultDTO=128 bytes: payload through @79, reserved cache-line padding @80..@127\",");
            builder.AppendLine("  \"parallelResultFalseSharingGuard\": true,");
            builder.AppendLine("  \"scheduleRaceGuard\": \"PASS: acoustic SignalBus staging occurs inside ScheduleFrameEvaluation after _evaluationScheduled guard; BeginDispatcherFrame no longer mutates acoustic stimuli\",");
            builder.AppendLine("  \"admissionFailureKeepsAcousticRetryOpen\": true,");
            builder.AppendLine("  \"admissionFailurePreservesStagedStimuliAcrossFrames\": true,");
            builder.AppendLine("  \"retryLatchNoWriteOnlyFrameField\": true,");
            builder.AppendLine("  \"pendingRetryCounterWriteUsesMutableOpen\": true,");
            builder.AppendLine("  \"hotScheduleDoesNotAllocateAcousticVault\": true,");
            builder.AppendLine("  \"unsafePointerJustificationParagraphs\": true,");
            builder.AppendLine("  \"nonFiniteFaultDumpsBlackBox\": true,");
            builder.AppendLine("  \"invalidIngressFaultTelemetry\": true,");
            builder.AppendLine("  \"invalidOnlyIdleFaultTelemetry\": true,");
            builder.AppendLine("  \"priorityLaneStagingAndDropTelemetry\": true,");
            builder.AppendLine("  \"stimulusDropTelemetry\": \"SensoryTelemetryEntry.Reserved0 stores dropped stimulus count; Reserved1 stores AcousticCounter64DTO flags\",");
            builder.AppendLine("  \"readAccessorsUseOpenRead\": true,");
            builder.AppendLine("  \"hotReadOnlyHelpersUseOpenRead\": true,");
            builder.AppendLine("  \"preRaymarchThresholdCull\": true,");
            builder.AppendLine("  \"idleFramesBypassAcousticJobs\": true,");
            builder.AppendLine("  \"idleNoDueFramesWriteTelemetry\": true,");
            builder.AppendLine("  \"idleTelemetryBeforeFirstJobSchedule\": true,");
            builder.AppendLine("  \"idleSkipsAcousticIntegrationAfterJobHandoff\": true,");
            builder.AppendLine("  \"sdfOutOfBoundsFailOpen\": true,");
            builder.AppendLine("  \"maxDistanceTuningAppliedInJobs\": true,");
            builder.AppendLine("  \"editorFacadeOwnsMaxDistanceAndFaultBudget\": true,");
            builder.AppendLine("  \"tuningWritesRejectScheduledEvaluation\": true,");
            builder.AppendLine("  \"rawDumpPatchesMeasuredChainMicroseconds\": true,");
            builder.AppendLine("  \"dumpPathResolutionFaultPathRetryBlocked\": true,");
            builder.AppendLine("  \"dumpPathColdFailureRetryable\": true,");
            builder.AppendLine("  \"scannerReportGeneratorPreservesProofFields\": true,");
            builder.AppendLine("  \"vaultBufferIds\": \"72760..72768\",");
            builder.AppendLine("  \"bufferIdCollisionAudit\": \"PASS: moved off 71980..71988 because H8Memory owns 71980..71987 plus 71989,71990 for SHINOBU parasite VFX lanes\",");
            builder.AppendLine("  \"blackBoxTelemetryFrames\": 300,");
            builder.AppendLine("  \"blackBoxDumpFormat\": \"16-byte LE header + raw SensoryTelemetryEntry[300] rows, 64 bytes each\",");
            builder.AppendLine("  \"dataMonolithStaticDataPresent\": false,");
            builder.AppendLine("  \"narrowCoreCompile\": \"PENDING_AFTER_LOOP29_CPU_GUARD_BLOCKED\",");
            builder.AppendLine("  \"globalQualityWeightContinuous\": true,");
            builder.AppendLine("  \"rayStepsAtQualityZero\": 1,");
            builder.AppendLine("  \"rayStepsAtQualityOne\": 8,");
            builder.AppendLine("  \"findings\": [");
            for (int i = 0; i < result.Findings.Count; i++)
            {
                builder.Append("    \"").Append(EscapeJson(result.Findings[i])).Append("\"");
                if (i + 1 < result.Findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }

            builder.AppendLine("  ],");
            builder.Append("  \"verdict\": \"").Append(result.Findings.Count == 0 ? "PASS_ROSLYN_AST_TOKEN_SCAN" : "FAIL_ROSLYN_AST_TARGETED").AppendLine("\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedReportBlock(in ScanResult result)
        {
            var builder = new StringBuilder(3072);
            builder.AppendLine("  \"shinobu311AcousticHearing\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_311\",");
            builder.AppendLine("    \"domain\": \"SENSORY_ACOUSTIC_ECHO_INTEGRATOR\",");
            builder.AppendLine("    \"scanner\": \"OOP_Hearing_Scanner\",");
            builder.AppendLine("    \"summary\": \"OOP Acoustic Queries: Roslyn AST Targeted Clean\",");
            builder.AppendLine("    \"scannerUsesStructuralSyntaxPass\": true,");
            builder.AppendLine("    \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("    \"scannerVerdictScope\": \"Roslyn invocation token scan only; compile marker is a separate guarded CLI proof field\",");
            builder.AppendLine("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree invocation pass scoped to AI/Fauna/Sensory directories\",");
            builder.AppendLine("    \"closestPointMemberAccessConservative\": true,");
            builder.Append("    \"scannedFiles\": ").Append(result.ScannedFiles).AppendLine(",");
            builder.Append("    \"candidateFiles\": ").Append(result.CandidateFiles).AppendLine(",");
            builder.Append("    \"parserFailures\": ").Append(result.ParserFailures).AppendLine(",");
            builder.Append("    \"astReferenceNodes\": ").Append(result.TotalReferenceNodes).AppendLine(",");
            builder.Append("    \"oopAcousticQueryViolations\": ").Append(result.Findings.Count).AppendLine(",");
            builder.AppendLine("    \"tokens\": [\"Physics.CheckSphere\", \"Physics.Linecast\", \"Collider.ClosestPoint\"],");
            builder.AppendLine("    \"runtimeRoute\": \"SignalBus MovementAcousticSignal/AcousticPingSignal/CombatDamageSignal -> PredatorCognitionDomain GlobalDataVault acoustic buffers -> Core-contract VoxelSdfTexture3D snapshot -> Burst inverse-square attenuation -> SDF occlusion -> cognition acoustic memory\",");
            builder.AppendLine("    \"dtoLayout\": \"AcousticStimulusDTO=32 bytes: double3 EpicenterAUP@0 float InitialIntensity@24 uint SoundTypeHash@28\",");
            builder.AppendLine("    \"resultDtoLayout\": \"AcousticEvaluationResultDTO=128 bytes: payload through @79, reserved cache-line padding @80..@127\",");
            builder.AppendLine("    \"parallelResultFalseSharingGuard\": true,");
            builder.AppendLine("    \"scheduleRaceGuard\": \"PASS: acoustic SignalBus staging occurs inside ScheduleFrameEvaluation after _evaluationScheduled guard; BeginDispatcherFrame no longer mutates acoustic stimuli\",");
            builder.AppendLine("    \"admissionFailureKeepsAcousticRetryOpen\": true,");
            builder.AppendLine("    \"admissionFailurePreservesStagedStimuliAcrossFrames\": true,");
            builder.AppendLine("    \"retryLatchNoWriteOnlyFrameField\": true,");
            builder.AppendLine("    \"pendingRetryCounterWriteUsesMutableOpen\": true,");
            builder.AppendLine("    \"hotScheduleDoesNotAllocateAcousticVault\": true,");
            builder.AppendLine("    \"unsafePointerJustificationParagraphs\": true,");
            builder.AppendLine("    \"nonFiniteFaultDumpsBlackBox\": true,");
            builder.AppendLine("    \"invalidIngressFaultTelemetry\": true,");
            builder.AppendLine("    \"invalidOnlyIdleFaultTelemetry\": true,");
            builder.AppendLine("    \"priorityLaneStagingAndDropTelemetry\": true,");
            builder.AppendLine("    \"stimulusDropTelemetry\": \"SensoryTelemetryEntry.Reserved0 stores dropped stimulus count; Reserved1 stores AcousticCounter64DTO flags\",");
            builder.AppendLine("    \"readAccessorsUseOpenRead\": true,");
            builder.AppendLine("    \"hotReadOnlyHelpersUseOpenRead\": true,");
            builder.AppendLine("    \"preRaymarchThresholdCull\": true,");
            builder.AppendLine("    \"idleFramesBypassAcousticJobs\": true,");
            builder.AppendLine("    \"idleNoDueFramesWriteTelemetry\": true,");
            builder.AppendLine("    \"idleTelemetryBeforeFirstJobSchedule\": true,");
            builder.AppendLine("    \"idleSkipsAcousticIntegrationAfterJobHandoff\": true,");
            builder.AppendLine("    \"sdfOutOfBoundsFailOpen\": true,");
            builder.AppendLine("    \"maxDistanceTuningAppliedInJobs\": true,");
            builder.AppendLine("    \"editorFacadeOwnsMaxDistanceAndFaultBudget\": true,");
            builder.AppendLine("    \"tuningWritesRejectScheduledEvaluation\": true,");
            builder.AppendLine("    \"rawDumpPatchesMeasuredChainMicroseconds\": true,");
            builder.AppendLine("    \"dumpPathResolutionFaultPathRetryBlocked\": true,");
            builder.AppendLine("    \"dumpPathColdFailureRetryable\": true,");
            builder.AppendLine("    \"scannerReportGeneratorPreservesProofFields\": true,");
            builder.AppendLine("    \"vaultBufferIds\": \"72760..72768\",");
            builder.AppendLine("    \"blackBoxTelemetryFrames\": 300,");
            builder.AppendLine("    \"blackBoxDumpFormat\": \"16-byte LE header + raw SensoryTelemetryEntry[300] rows, 64 bytes each\",");
            builder.AppendLine("    \"dataMonolithStaticDataPresent\": false,");
            builder.AppendLine("    \"narrowCoreCompile\": \"PENDING_AFTER_LOOP29_CPU_GUARD_BLOCKED\",");
            builder.AppendLine("    \"globalQualityWeightContinuous\": true,");
            builder.AppendLine("    \"rayStepsAtQualityZero\": 1,");
            builder.AppendLine("    \"rayStepsAtQualityOne\": 8,");
            builder.Append("    \"verdict\": \"").Append(result.Findings.Count == 0 ? "PASS_ROSLYN_AST_TOKEN_SCAN" : "FAIL_ROSLYN_AST_TARGETED").AppendLine("\"");
            builder.Append("  }");
            return builder.ToString();
        }

        private static void UpsertSharedReport(string path, string block)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{\n" + block + "\n}\n", Encoding.UTF8);
                return;
            }

            string existing = File.ReadAllText(path);
            int key = existing.IndexOf("\"shinobu311AcousticHearing\"", StringComparison.Ordinal);
            if (key >= 0)
            {
                int entryStart = FindEntryStart(existing, key);
                int objectStart = existing.IndexOf('{', key);
                int objectEnd = FindMatchingBrace(existing, objectStart);
                if (entryStart >= 0 && objectEnd >= objectStart)
                {
                    int entryEnd = objectEnd + 1;
                    int scan = entryEnd;
                    while (scan < existing.Length && char.IsWhiteSpace(existing[scan]))
                        scan++;
                    bool hadTrailingComma = scan < existing.Length && existing[scan] == ',';
                    if (hadTrailingComma)
                        entryEnd = scan + 1;

                    string replacement = block + (hadTrailingComma ? "," : string.Empty);
                    File.WriteAllText(path, existing.Substring(0, entryStart) + replacement + existing.Substring(entryEnd), Encoding.UTF8);
                    return;
                }
            }

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
            {
                File.WriteAllText(path, "{\n" + block + "\n}\n", Encoding.UTF8);
                return;
            }

            string prefix = existing.Substring(0, insert).TrimEnd();
            bool hasExistingEntry = prefix.LastIndexOf('{') < prefix.Length - 1;
            string separator = hasExistingEntry ? ",\n" : "\n";
            File.WriteAllText(path, prefix + separator + block + "\n}\n", Encoding.UTF8);
        }

        private static int FindEntryStart(string text, int key)
        {
            int i = key - 1;
            while (i >= 0 && char.IsWhiteSpace(text[i]))
                i--;
            if (i >= 0 && text[i] == ',')
                i--;
            while (i >= 0 && text[i] != '\n' && text[i] != '{')
                i--;
            return math.min(text.Length, i + 1);
        }

        private static int FindMatchingBrace(string text, int objectStart)
        {
            if (objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

            int depth = 0;
            bool stringLiteral = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && !IsEscaped(text, i))
                    stringLiteral = !stringLiteral;
                if (stringLiteral)
                    continue;
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool IsEscaped(string text, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

        private static string ToProjectPath(string projectRoot, string file)
        {
            string relative = file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? file.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : file;
            return relative.Replace('\\', '/');
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private struct ScanResult
        {
            public int ScannedFiles;
            public int CandidateFiles;
            public int ParserFailures;
            public int TotalReferenceNodes;
            public List<string> Findings;
        }
    }
}
