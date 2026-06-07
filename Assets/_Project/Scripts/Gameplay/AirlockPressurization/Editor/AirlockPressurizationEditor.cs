#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - AirlockPressurizationEditor.cs
// SHINOBU_338 editor tuner, gizmo, and static OOP airlock scanner.
// ============================================================================

using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay.AirlockPressurization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay.AirlockPressurization.Editor
{
    public sealed class PressurizationCycleTunerWindow : EditorWindow
    {
        private Slider _pumpSpeed;
        private Slider _curveExponent;
        private Slider _powerDraw;
        private Slider _qualityWeight;
        private Label _readout;
        private AirlockTelemetryLineGraph _graph;

        [MenuItem("HECTON-8/Airlock/Pressurization Cycle Tuner")]
        public static void Open()
        {
            PressurizationCycleTunerWindow window = GetWindow<PressurizationCycleTunerWindow>();
            window.titleContent.text = "Airlock Pressure";
            window.minSize = new Vector2(440f, 320f);
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _pumpSpeed = AddSlider(root, "Pump L/s", 0f, 2000f);
            _curveExponent = AddSlider(root, "Curve Exp", 0.25f, 4f);
            _powerDraw = AddSlider(root, "Power W", 0f, 6000f);
            _qualityWeight = AddSlider(root, "Quality Weight", 0f, 1f);
            _readout = new Label("Vault unavailable.");
            root.Add(_readout);

            Button refresh = new Button(RefreshFromVault) { text = "Refresh" };
            root.Add(refresh);

            _graph = new AirlockTelemetryLineGraph();
            _graph.style.height = 120;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            RegisterCallbacks();
            RefreshFromVault();
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.style.marginBottom = 4;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _pumpSpeed.RegisterValueChangedCallback(evt => Mutate(TuningField.PumpSpeed, evt.newValue));
            _curveExponent.RegisterValueChangedCallback(evt => Mutate(TuningField.CurveExponent, evt.newValue));
            _powerDraw.RegisterValueChangedCallback(evt => Mutate(TuningField.PowerDraw, evt.newValue));
            _qualityWeight.RegisterValueChangedCallback(evt => Mutate(TuningField.QualityWeight, evt.newValue));
        }

        private void OnEditorUpdate()
        {
            if (_graph != null)
                _graph.MarkDirtyRepaint();
        }

        private void RefreshFromVault()
        {
            if (!AirlockPressurizationVault.TryReadTuning(GlobalRegistry.DataVault, out NativeArray<AirlockTuningDTO>.ReadOnly tuning) ||
                tuning.Length <= 0)
            {
                if (_readout != null)
                    _readout.text = "Vault buffer unavailable.";
                return;
            }

            AirlockTuningDTO dto = tuning[0];
            _pumpSpeed.SetValueWithoutNotify(dto.PumpEvacuationSpeedLps);
            _curveExponent.SetValueWithoutNotify(dto.EqualizationCurveExponent);
            _powerDraw.SetValueWithoutNotify(dto.PowerDrawWatts);
            _qualityWeight.SetValueWithoutNotify(dto.GlobalQualityWeight);
            _readout.text = $"water={dto.MaxWaterVolumeLiters:0}L pressure={dto.ExternalPressureAtm:0.00}atm tick={AirlockPressurizationMath.ResolveAuthorityTickInterval():0.000}s";
        }

        private static void Mutate(TuningField field, float value)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                vault.ActiveBurstLockMask != 0u ||
                !vault.TryGetGenerationHandle<AirlockTuningDTO>(
                    AirlockPressurizationBufferIds.Tuning,
                    out VaultGenerationHandle<AirlockTuningDTO> handle))
            {
                return;
            }

            bool acquired = vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out NativeArray<AirlockTuningDTO> tuning);
            try
            {
                if (!acquired || !tuning.IsCreated || tuning.Length <= 0)
                    return;

                AirlockTuningDTO dto = tuning[0];
                switch (field)
                {
                    case TuningField.PumpSpeed:
                        dto.PumpEvacuationSpeedLps = math.max(0f, value);
                        break;
                    case TuningField.CurveExponent:
                        dto.EqualizationCurveExponent = math.max(0.25f, value);
                        break;
                    case TuningField.PowerDraw:
                        dto.PowerDrawWatts = math.max(0f, value);
                        break;
                    case TuningField.QualityWeight:
                        dto.GlobalQualityWeight = math.saturate(value);
                        break;
                }

                tuning[0] = dto;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private enum TuningField : byte
        {
            PumpSpeed,
            CurveExponent,
            PowerDraw,
            QualityWeight
        }
    }

    internal sealed class AirlockTelemetryLineGraph : VisualElement
    {
        public AirlockTelemetryLineGraph()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect rect = contentRect;
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.1f, 0.7f, 1f, 1f);

            if (!AirlockPressurizationVault.TryReadTelemetry(GlobalRegistry.DataVault, out NativeArray<AirlockTelemetryEntry>.ReadOnly telemetry) ||
                telemetry.Length <= 1)
            {
                DrawFlatLine(painter, rect);
                return;
            }

            int count = math.min(telemetry.Length, AirlockPressurizationConstants.TelemetryFrameCount);
            float maxPressure = 0.01f;
            for (int i = 0; i < count; i++)
                maxPressure = math.max(maxPressure, telemetry[i].MaxPressureDeltaAtm);

            painter.BeginPath();
            for (int i = 0; i < count; i++)
            {
                float x = rect.xMin + rect.width * (count <= 1 ? 0f : i * math.rcp(count - 1f));
                float y = rect.yMax - rect.height * math.saturate(telemetry[i].MaxPressureDeltaAtm * math.rcp(maxPressure));
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();

            painter.strokeColor = new Color(0.1f, 0.95f, 0.55f, 1f);
            painter.BeginPath();
            float maxWater = 1f;
            for (int i = 0; i < count; i++)
                maxWater = math.max(maxWater, telemetry[i].TotalWaterDisplacedLiters);
            for (int i = 0; i < count; i++)
            {
                float x = rect.xMin + rect.width * (count <= 1 ? 0f : i * math.rcp(count - 1f));
                float y = rect.yMax - rect.height * math.saturate(telemetry[i].TotalWaterDisplacedLiters * math.rcp(maxWater));
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }

        private static void DrawFlatLine(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            float y = rect.yMin + rect.height * 0.5f;
            painter.MoveTo(new Vector2(rect.xMin, y));
            painter.LineTo(new Vector2(rect.xMax, y));
            painter.Stroke();
        }
    }

    [InitializeOnLoad]
    public static class LiveAirlockExchangeDebugGizmo
    {
        private static bool s_enabled;

        static LiveAirlockExchangeDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("HECTON-8/Airlock/Toggle Live Exchange Gizmo")]
        public static void Toggle()
        {
            s_enabled = !s_enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!s_enabled ||
                !AirlockPressurizationVault.TryReadDebugGizmos(GlobalRegistry.DataVault, out NativeArray<AirlockDebugGizmoDTO>.ReadOnly gizmos))
            {
                return;
            }

            int count = math.min(gizmos.Length, AirlockPressurizationConstants.MaxActiveAirlocks);
            for (int i = 0; i < count; i++)
            {
                AirlockDebugGizmoDTO dto = gizmos[i];
                if ((dto.Flags & AirlockDoorPoseFlags.Valid) == 0u && dto.MaxWaterVolumeLiters <= 0f)
                    continue;

                Vector3 center = ResolveScenePosition(in dto.DoorAup);
                float fill = dto.MaxWaterVolumeLiters > 0f
                    ? math.saturate(dto.CurrentWaterVolumeLiters * math.rcp(dto.MaxWaterVolumeLiters))
                    : 0f;
                Vector3 size = new Vector3(2.6f, 3.2f, 1.2f);
                Handles.color = Color.Lerp(new Color(0f, 0.15f, 0.35f, 0.35f), new Color(0f, 0.55f, 1f, 0.8f), fill);
                Handles.DrawWireCube(center, size);
                Vector3 fillCenter = center + Vector3.down * (size.y * (0.5f - fill * 0.5f));
                Handles.DrawWireCube(fillCenter, new Vector3(size.x * 0.92f, size.y * fill, size.z * 0.92f));
                Handles.Label(center + Vector3.up * 1.9f, dto.CurrentPressureAtm.ToString("0.00") + " atm");
            }
        }

        private static Vector3 ResolveScenePosition(in Hecton8.World.AbsoluteUniversePosition aup)
        {
            Hecton8.World.AbsoluteUniversePosition origin = GlobalSignals.CurrentRuntimeOriginAup();
            float3 local = Hecton8.World.AbsoluteUniversePosition.ToCameraRelativeFloat3(in aup, in origin);
            return new Vector3(local.x, local.y, local.z);
        }
    }

    public static class OOP_Airlock_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string ReportSectionKey = "\"shinobu338AirlockPressurizationScanner\"";

        [MenuItem("HECTON-8/Airlock/Run OOP Airlock Scanner")]
        public static void RunAndWriteReport()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            string reportPath = Path.Combine(projectRoot, ReportPath);
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            int coroutineHits = 0;
            int animatorHits = 0;
            int triggerHits = 0;
            int sourceFilesScanned = 0;
            int parserFailures = 0;
            StringBuilder findings = new StringBuilder(4096);

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (!IsAirlockRelevantPath(path))
                    continue;

                sourceFilesScanned++;
                string text = File.ReadAllText(path);
                SyntaxTree tree;
                try
                {
                    tree = CSharpSyntaxTree.ParseText(text);
                }
                catch (Exception exception)
                {
                    parserFailures++;
                    AppendFinding(findings, projectRoot, path, 0, "RoslynParse", exception.GetType().Name, false, false, false);
                    continue;
                }

                if (HasParseError(tree))
                {
                    parserFailures++;
                    AppendFinding(findings, projectRoot, path, 0, "RoslynParse", "syntax error", false, false, false);
                    continue;
                }

                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                bool coroutine = HasCoroutineSequencer(root, out int coroutineLine, out string coroutineToken);
                bool animator = HasAnimatorSequencer(root, out int animatorLine, out string animatorToken);
                bool trigger = HasTriggerWetting(root, out int triggerLine, out string triggerToken);

                if (!coroutine && !animator && !trigger)
                    continue;

                if (coroutine) coroutineHits++;
                if (animator) animatorHits++;
                if (trigger) triggerHits++;

                int line = coroutine ? coroutineLine : animator ? animatorLine : triggerLine;
                string token = coroutine ? coroutineToken : animator ? animatorToken : triggerToken;
                AppendFinding(findings, projectRoot, path, line, "RoslynAST", token, coroutine, animator, trigger);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            string verdict = coroutineHits == 0 && animatorHits == 0 && triggerHits == 0
                ? "OOP Sequencers Eradicated"
                : "OOP Airlock Sequencers Present";
            UpsertReportSection(
                reportPath,
                BuildJsonSection(verdict, sourceFilesScanned, parserFailures, coroutineHits, animatorHits, triggerHits, findings.ToString()));
            AssetDatabase.Refresh();
        }

        private static bool IsAirlockRelevantPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Editor/"))
                return false;

            return normalized.Contains("/Scripts/Habitat/") ||
                   normalized.Contains("/Scripts/Gameplay/BaseAirlock") ||
                   normalized.Contains("/Scripts/Gameplay/Airlock");
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

        private static bool HasCoroutineSequencer(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is YieldStatementSyntax yieldStatement && yieldStatement.ToString().IndexOf("WaitForSeconds", StringComparison.Ordinal) >= 0)
                    {
                        line = GetLineNumber(yieldStatement);
                        token = "yield return WaitForSeconds";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasAnimatorSequencer(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is InvocationExpressionSyntax invocation &&
                        invocation.Expression is MemberAccessExpressionSyntax member &&
                        (string.Equals(member.Name.Identifier.ValueText, "Play", StringComparison.Ordinal) ||
                         string.Equals(member.Name.Identifier.ValueText, "SetTrigger", StringComparison.Ordinal)) &&
                        member.Expression.ToString().IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        line = GetLineNumber(invocation);
                        token = member.Name.Identifier.ValueText;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasTriggerWetting(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is MethodDeclarationSyntax method &&
                        string.Equals(method.Identifier.ValueText, "OnTriggerEnter", StringComparison.Ordinal) &&
                        ContainsAirlockOrWetToken(method.ToString()))
                    {
                        line = GetLineNumber(method);
                        token = "OnTriggerEnter wetting";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsAirlockOrWetToken(string value)
        {
            return value.IndexOf("Airlock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("wet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("flood", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }

        private static void AppendFinding(
            StringBuilder builder,
            string projectRoot,
            string path,
            int line,
            string source,
            string token,
            bool coroutine,
            bool animator,
            bool trigger)
        {
            if (builder.Length > 0)
                builder.Append(',');

            string relative = path.Replace(projectRoot, string.Empty).TrimStart('\\', '/').Replace('\\', '/');
            builder.Append("{\"path\":\"");
            AppendEscaped(builder, relative);
            builder.Append("\",\"line\":");
            builder.Append(line);
            builder.Append(",\"source\":\"");
            AppendEscaped(builder, source);
            builder.Append("\",\"token\":\"");
            AppendEscaped(builder, token);
            builder.Append("\",\"coroutine\":");
            builder.Append(coroutine ? "true" : "false");
            builder.Append(",\"animator\":");
            builder.Append(animator ? "true" : "false");
            builder.Append(",\"triggerWetting\":");
            builder.Append(trigger ? "true" : "false");
            builder.Append('}');
        }

        private static string BuildJsonSection(
            string verdict,
            int sourceFilesScanned,
            int parserFailures,
            int coroutineHits,
            int animatorHits,
            int triggerHits,
            string findings)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("    \"shinobu338AirlockPressurizationScanner\":  {\n");
            builder.Append("                                               \"agentId\":  \"SHINOBU_338\",\n");
            builder.Append("                                               \"scanner\":  \"OOP_Airlock_Scanner\",\n");
            builder.Append("                                               \"summary\":  \"");
            AppendEscaped(builder, verdict);
            builder.Append("\",\n                                               \"evidenceClass\":  \"STATIC_SOURCE_TARGETED_EDITOR_SCAN\",\n");
            builder.Append("                                               \"sourceFilesScanned\":");
            builder.Append(sourceFilesScanned);
            builder.Append(",\n                                               \"parserFailures\":");
            builder.Append(parserFailures);
            builder.Append(",\n                                               \"coroutineHits\":");
            builder.Append(coroutineHits);
            builder.Append(",\n                                               \"animatorHits\":");
            builder.Append(animatorHits);
            builder.Append(",\n                                               \"triggerWettingHits\":");
            builder.Append(triggerHits);
            builder.Append(",\n                                               \"runtimeRoute\":  \"AirlockStateDTO -> EvaluateAirlockCyclesJob -> optional deterministic IntegrateAirlockExchangeJob -> BulkheadContainmentIntentDTO -> Construction BulkheadCollisionResultDTO -> KCC + BubbleSpawnSignal + MovementAcousticSignal + AirlockTelemetryEntry\",\n");
            builder.Append("                                               \"massConservation\":  \"Saturated FluidCompartmentDTO target writes restore unapplied water to AirlockStateDTO; gas mix uses AirlockTuningDTO.ChamberVolumeLiters\",\n");
            builder.Append("                                               \"flushGate\":  \"FlushCompletedOutputs requires dispatcherCompletionConfirmed=true and contains no Complete call\",\n");
            builder.Append("                                               \"generatedProjectBridge\":  \"Local ignored/generated Hecton8.Core.csproj includes referenced Core/Atmosphere/Physics contract source files pending Unity regeneration\",\n");
            builder.Append("                                               \"bufferIds\":  \"73380..73392\",\n");
            builder.Append("                                               \"status\":  \"STATIC_OWNER_ROUTE_ROLLBACK_KCC_ROUTED_EXTERNAL_COMPILE_WALL\",\n");
            builder.Append("                                               \"findings\":  [");
            builder.Append(findings);
            builder.Append("]\n                                           }");
            return builder.ToString();
        }

        private static void UpsertReportSection(string reportPath, string sectionJson)
        {
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{\n" + sectionJson + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(reportPath);
            int rootOpen = existing.IndexOf('{');
            int rootClose = existing.LastIndexOf('}');
            if (rootOpen < 0 || rootClose <= rootOpen)
            {
                File.WriteAllText(reportPath, "{\n" + sectionJson + "\n}\n");
                return;
            }

            int keyIndex = existing.IndexOf(ReportSectionKey, rootOpen, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int memberStart = FindMemberLineStart(existing, keyIndex);
                int memberEnd = FindMemberObjectEnd(existing, keyIndex);
                if (memberStart >= 0 && memberEnd > memberStart)
                {
                    File.WriteAllText(reportPath, existing.Substring(0, memberStart) + sectionJson + existing.Substring(memberEnd));
                    return;
                }
            }

            string separator = HasRootMembers(existing, rootOpen, rootClose) ? ",\n" : "\n";
            File.WriteAllText(reportPath, existing.Insert(rootOpen + 1, "\n" + sectionJson + separator));
        }

        private static bool HasRootMembers(string text, int rootOpen, int rootClose)
        {
            for (int i = rootOpen + 1; i < rootClose; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return true;
            }

            return false;
        }

        private static int FindMemberLineStart(string text, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && text[start - 1] != '\n' && text[start - 1] != '\r')
                start--;
            return start;
        }

        private static int FindMemberObjectEnd(string text, int keyIndex)
        {
            int colon = text.IndexOf(':', keyIndex);
            int objectStart = colon >= 0 ? text.IndexOf('{', colon) : -1;
            if (objectStart < 0)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
            }

            return -1;
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }
}
#endif
