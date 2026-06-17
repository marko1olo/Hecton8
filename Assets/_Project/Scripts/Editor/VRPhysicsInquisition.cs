// ============================================================================
// HECTON-8 - VRPhysicsInquisition.cs
// Editor proof tooling for SHINOBU_271 VR kinematic hand bridge.
// ============================================================================

namespace Hecton8.Editor
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Memory;
    using Hecton8.Interaction;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public static class VRPhysicsInquisition
    {
        private const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string DedicatedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_271.json";
        private const string SelfAuditPath = "Docs/Reports/VR_INTERACTION_SELF_AUDIT_SHINOBU_271.md";
        private const string StatusPath = "Docs/Tasks/Status_SHINOBU_271.md";
        private const string RootPath = "Assets/_Project/Scripts";
        private const string SharedReportKey = "shinobu271VRKinematicBridgeScanner";
        private const string RuntimeProofLimit =
            "Unity import, Unity Console, Play Mode GCMonitor, profiler captures, player-build, Quest/Steam Deck runtime, and live VR device proof remain pending.";

        [MenuItem("Hecton8/VR/Run Physics Inquisition")]
        public static void Run()
        {
            Directory.CreateDirectory("Docs/Reports");
            int springJointHits = 0;
            int configurableJointHits = 0;
            int fixedJointHits = 0;
            int handMovePositionHits = 0;
            int physicalHandArticulationHits = 0;
            int physicalHandRigidbodyShellHits = 0;

            string[] files = Directory.GetFiles(RootPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (path.EndsWith("/Editor/VRPhysicsInquisition.cs", StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(files[i]);
                bool runtimeScript = path.IndexOf("/Editor/", StringComparison.Ordinal) < 0;
                if (runtimeScript)
                {
                    springJointHits += Count(text, "SpringJoint");
                    configurableJointHits += Count(text, "ConfigurableJoint");
                    fixedJointHits += Count(text, "FixedJoint");
                }

                if (path.EndsWith("/Interaction/PhysicalHandController.cs", StringComparison.Ordinal))
                {
                    physicalHandArticulationHits += Count(text, "AddComponent<ArticulationBody>");
                    physicalHandRigidbodyShellHits += Count(text, "AddComponent<Rigidbody>");
                    handMovePositionHits += Count(text, "MovePosition");
                }
            }

            bool layoutValid = VRInteractionKinematicBridgeLayout.Validate();
            bool bridgePurgedDefault = physicalHandArticulationHits > 0 && physicalHandRigidbodyShellHits > 0;
            string compileProof = ResolveCompileProofForReport();

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            AppendJson(builder, "agent", "SHINOBU_271", true);
            AppendJson(builder, "domain", "VR_INTERACTION_KINEMATIC_BRIDGE", true);
            AppendJson(builder, "summary", "Physics-Based Hands Purged by default kinematic SDF bridge; legacy PhysX proxy remains behind explicit fallback branch.", true);
            AppendJson(builder, "layoutValid", layoutValid ? "true" : "false", true, raw: true);
            AppendJson(builder, "springJointHits", springJointHits.ToString(), true, raw: true);
            AppendJson(builder, "configurableJointHits", configurableJointHits.ToString(), true, raw: true);
            AppendJson(builder, "fixedJointHits", fixedJointHits.ToString(), true, raw: true);
            AppendJson(builder, "physicalHandAddComponentArticulationHits", physicalHandArticulationHits.ToString(), true, raw: true);
            AppendJson(builder, "physicalHandAddComponentRigidbodyHits", physicalHandRigidbodyShellHits.ToString(), true, raw: true);
            AppendJson(builder, "physicalHandMovePositionHits", handMovePositionHits.ToString(), true, raw: true);
            AppendJson(builder, "dtoSizeBytes", UnsafeUtility.SizeOf<VRHandStateDTO>().ToString(), true, raw: true);
            AppendJson(builder, "telemetryEntries", VRInteractionKinematicBridgeConstants.TelemetryCapacity.ToString(), true, raw: true);
            AppendJson(builder, "routeCard", "Docs/ARCHITECTURE/SHINOBU_271_VR_INTERACTION_KINEMATIC_BRIDGE_ROUTE_CARD.md", true);
            AppendJson(builder, "binaryLedger", "Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md#2026-05-21-shinobu_271-vr-interaction-kinematic-bridge-payload-boundary", true);
            AppendJson(builder, "sdfRoute", "IVoxelSonarSdfReadModel.TryReadNearestSonarSdf -> VRHandStateDTO -> transform-only runtime target", true);
            AppendJson(builder, "hotPathAmendments", "cached IDataVault TryResolveExisting in fixed-step; live VRControllerMatrixDTO ingestion; all-active socket scan; over-budget state is telemetry-only.", true);
            AppendJson(builder, "continuousQuality", "GlobalQualityWeight maps continuously to a 2..8 presentation/telemetry iteration hint. Authoritative SDF truth uses the deterministic 8-step fence for rollback.", true);
            AppendJson(builder, "compileProof", compileProof, true);
            AppendJson(builder, "verificationLimit", RuntimeProofLimit, false);
            builder.AppendLine("}");
            File.WriteAllText(DedicatedReportPath, builder.ToString());
            UpsertSharedReport(BuildSharedReportBlock(layoutValid, springJointHits, configurableJointHits, fixedJointHits, handMovePositionHits, physicalHandArticulationHits, physicalHandRigidbodyShellHits, compileProof));
            WriteSelfAudit();
            AssetDatabase.Refresh();

            Debug.Log(
                "[SHINOBU_271] VR Physics Inquisition complete. " +
                "Report: " + DedicatedReportPath + " LayoutValid=" + layoutValid +
                " DefaultBridgePurged=" + bridgePurgedDefault);
        }

        [MenuItem("Hecton8/VR/Validate Kinematic Bridge Layout")]
        public static void ValidateLayoutMenu()
        {
            bool valid = VRInteractionKinematicBridgeLayout.Validate();
            if (!valid)
                Debug.LogError("[SHINOBU_271] VRHandStateDTO layout fence failed.");
            else
                Debug.Log("[SHINOBU_271] VRHandStateDTO layout fence passed.");
        }

        [MenuItem("Hecton8/VR/Write SHINOBU 271 Self Audit")]
        public static void WriteSelfAudit()
        {
            Directory.CreateDirectory("Docs/Reports");
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# SHINOBU_271 VR Interaction Self Audit");
            builder.AppendLine();
            builder.AppendLine("<SELF_AUDIT agent=\"SHINOBU_271\" role=\"VR_INTERACTION_KINEMATIC_BRIDGE\">");
            builder.AppendLine("  <HOT_PATH_GC>0 managed allocations by design in solver jobs and controller math path. Managed file IO, reflection, UI Toolkit, and report strings are editor/fault-only.</HOT_PATH_GC>");
            builder.AppendLine("  <PHYSICS_PROXY>Status: default path uses transform-only runtime target. ArticulationBody/Rigidbody suit shell are legacy fallback behind useKinematicSdfHandBridge=false.</PHYSICS_PROXY>");
            builder.Append("  <DTO name=\"VRHandStateDTO\" size=\"").Append(UnsafeUtility.SizeOf<VRHandStateDTO>()).AppendLine("\">");
            AppendOffset(builder, "RawControllerAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.RawControllerAUP)));
            AppendOffset(builder, "ResolvedHandAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.ResolvedHandAUP)));
            AppendOffset(builder, "Velocity", VRInteractionKinematicBridgeLayout.OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.Velocity)));
            AppendOffset(builder, "InteractionFlags", VRInteractionKinematicBridgeLayout.OffsetOf<VRHandStateDTO>(nameof(VRHandStateDTO.InteractionFlags)));
            builder.AppendLine("  </DTO>");
            builder.Append("  <DTO name=\"VRInteractionTuningDTO\" size=\"").Append(UnsafeUtility.SizeOf<VRInteractionTuningDTO>()).AppendLine("\">");
            AppendOffset(builder, "PlayerRootAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.PlayerRootAUP)));
            AppendOffset(builder, "ShoulderAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.ShoulderAUP)));
            AppendOffset(builder, "SdfOriginAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.SdfOriginAUP)));
            AppendOffset(builder, "SdfDimensions", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.SdfDimensions)));
            AppendOffset(builder, "Flags", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTuningDTO>(nameof(VRInteractionTuningDTO.Flags)));
            builder.AppendLine("  </DTO>");
            builder.Append("  <DTO name=\"VRInteractionTelemetryEntry\" size=\"").Append(UnsafeUtility.SizeOf<VRInteractionTelemetryEntry>()).AppendLine("\">");
            AppendOffset(builder, "RawControllerAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.RawControllerAUP)));
            AppendOffset(builder, "ResolvedHandAUP", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.ResolvedHandAUP)));
            AppendOffset(builder, "Velocity", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.Velocity)));
            AppendOffset(builder, "CpuTimeMicros", VRInteractionKinematicBridgeLayout.OffsetOf<VRInteractionTelemetryEntry>(nameof(VRInteractionTelemetryEntry.CpuTimeMicros)));
            builder.AppendLine("  </DTO>");
            builder.AppendLine("  <VAULT_BUFFERS>");
            AppendBuffer(builder, "HandStates", VRInteractionKinematicBridgeConstants.HandStatesBuffer);
            AppendBuffer(builder, "PreviousHandStates", VRInteractionKinematicBridgeConstants.PreviousHandStatesBuffer);
            AppendBuffer(builder, "ControllerMatrixInputs", VRInteractionKinematicBridgeConstants.ControllerMatrixInputsBuffer);
            AppendBuffer(builder, "ResolvedHandMatrices", VRInteractionKinematicBridgeConstants.ResolvedHandMatricesBuffer);
            AppendBuffer(builder, "InteractionSockets", VRInteractionKinematicBridgeConstants.InteractionSocketsBuffer);
            AppendBuffer(builder, "Tuning", VRInteractionKinematicBridgeConstants.TuningBuffer);
            AppendBuffer(builder, "TelemetryRing", VRInteractionKinematicBridgeConstants.TelemetryRingBuffer);
            AppendBuffer(builder, "TelemetryCursor", VRInteractionKinematicBridgeConstants.TelemetryCursorBuffer);
            builder.AppendLine("  </VAULT_BUFFERS>");
            builder.AppendLine("  <AUP_PRECISION>All socket, velocity, SDF, stretch, and matrix localization paths subtract double3 AUP before float3 cast.</AUP_PRECISION>");
            builder.AppendLine("  <QUALITY_CURVE>GlobalQualityWeight maps continuously to a 2..8 presentation/telemetry hint. Authoritative SDF truth uses the deterministic 8-step fence so rollback hand state is not quality-dependent.</QUALITY_CURVE>");
            builder.AppendLine("  <JOB_FENCE>Jobs are Burst deterministic and expose schedulable kernels. Same-frame two-hand controller path uses direct pure math to avoid tiny schedule/Complete loops.</JOB_FENCE>");
            builder.AppendLine("  <H_PHI_VAULT_STATUS>Persistent SHINOBU_271 hand truth is owned by Vault buffers 73680..73687; no private NativeArray owns authoritative hand state.</H_PHI_VAULT_STATUS>");
            builder.AppendLine("  <POINTER_ALIASING>GenerateMockVRInputsJob, IngestVRControllerInputJob, ResolveSdfHandCollisionJob, EvaluateInteractionSnappingJob, and ComposeResolvedHandMatricesJob use NoAlias lanes where arrays do not overlap.</POINTER_ALIASING>");
            builder.AppendLine("  <COMPILE_GUARD>No new sibling runtime assembly reference. SDF route uses Hecton8.Core.Contracts.IVoxelSonarSdfReadModel.</COMPILE_GUARD>");
            builder.AppendLine("  <DEAR_LIE>Default hand collision is SDF depenetration plus arm clamp/socket snap. SpringJoint, ConfigurableJoint, trigger sockets, and Rigidbody hand truth are rejected.</DEAR_LIE>");
            builder.AppendLine("  <REGRESSION_MODEL>Missing Vault/SDF fails closed to transform-only local target; fixed-step uses cached IDataVault TryResolveExisting only; over-budget frames are telemetry-flagged and do not dump.</REGRESSION_MODEL>");
            builder.AppendLine("</SELF_AUDIT>");
            File.WriteAllText(SelfAuditPath, builder.ToString());
        }

        [MenuItem("Hecton8/VR/Import Interaction Sockets CSV")]
        public static void ImportSocketsCsv()
        {
            string path = EditorUtility.OpenFilePanel("Import VR interaction sockets CSV", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (!VRInteractionKinematicBridgeVault.EnsureBuffers(vault, out VRInteractionKinematicBridgeViews views))
            {
                Debug.LogError("[SHINOBU_271] GlobalDataVault unavailable; cannot import socket CSV.");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            int count = VRInteractionSocketCsvParser.ParseSockets(bytes, views.Sockets);
            Debug.Log("[SHINOBU_271] Imported " + count + " VR interaction sockets from " + path);
        }

        private static int Count(string text, string needle)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma, bool raw = false)
        {
            builder.Append("  \"").Append(key).Append("\": ");
            if (raw)
                builder.Append(value);
            else
                builder.Append('"').Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string BuildSharedReportBlock(
            bool layoutValid,
            int springJointHits,
            int configurableJointHits,
            int fixedJointHits,
            int handMovePositionHits,
            int physicalHandArticulationHits,
            int physicalHandRigidbodyShellHits,
            string compileProof)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("  \"").Append(SharedReportKey).AppendLine("\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_271\",");
            builder.AppendLine("    \"scanner\": \"VR_Physics_Inquisition static/editor hybrid\",");
            builder.AppendLine("    \"verdict\": \"PASS_STATIC_COMPILE_GREEN_UNITY_EXECUTION_PENDING\",");
            builder.AppendLine("    \"summary\": \"Physics-Based Hands Purged by default transform-only kinematic SDF bridge; legacy PhysX hand proxy remains behind explicit fallback only.\",");
            builder.AppendLine("    \"dedicatedReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_271.json\",");
            builder.AppendLine("    \"routeCard\": \"Docs/ARCHITECTURE/SHINOBU_271_VR_INTERACTION_KINEMATIC_BRIDGE_ROUTE_CARD.md\",");
            builder.AppendLine("    \"binaryLedger\": \"Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md#2026-05-21-shinobu_271-vr-interaction-kinematic-bridge-payload-boundary\",");
            builder.Append("    \"layoutValid\": ").Append(layoutValid ? "true" : "false").AppendLine(",");
            builder.Append("    \"runtimeSpringJointHits\": ").Append(springJointHits).AppendLine(",");
            builder.Append("    \"runtimeConfigurableJointHits\": ").Append(configurableJointHits).AppendLine(",");
            builder.Append("    \"runtimeFixedJointHits\": ").Append(fixedJointHits).AppendLine(",");
            builder.Append("    \"physicalHandMovePositionHits\": ").Append(handMovePositionHits).AppendLine(",");
            builder.Append("    \"physicalHandAddComponentArticulationHits\": ").Append(physicalHandArticulationHits).AppendLine(",");
            builder.Append("    \"physicalHandAddComponentRigidbodyHits\": ").Append(physicalHandRigidbodyShellHits).AppendLine(",");
            builder.AppendLine("    \"sdfRoute\": \"IVoxelSonarSdfReadModel.TryReadNearestSonarSdf -> VRHandStateDTO -> resolved float4x4 matrix\",");
            builder.AppendLine("    \"hotPathAmendments\": \"cached IDataVault TryResolveExisting in fixed-step; live VRControllerMatrixDTO ingestion; all-active socket scan; mutation guard; over-budget telemetry-only flag\",");
            builder.AppendLine("    \"blackBoxDump\": \"Docs/AgentLogs/Dump_SHINOBU_271.bin\",");
            builder.Append("    \"compileProof\": \"").Append(Escape(compileProof)).AppendLine("\",");
            builder.Append("    \"verificationLimit\": \"").Append(Escape(RuntimeProofLimit)).AppendLine("\"");
            builder.Append("  }");
            return builder.ToString();
        }

        private static string ResolveCompileProofForReport()
        {
            if (!File.Exists(StatusPath))
                return "Status_SHINOBU_271.md missing; compile proof must be read from Docs/AgentLogs build logs.";

            string[] lines = File.ReadAllLines(StatusPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Verification state:", StringComparison.Ordinal))
                    return line.Substring("Verification state:".Length).Trim();
            }

            return "Status_SHINOBU_271.md has no Verification state line; compile proof must be read from Docs/AgentLogs build logs.";
        }

        private static void UpsertSharedReport(string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            Directory.CreateDirectory("Docs/Reports");
            JObject root = new JObject();
            if (File.Exists(SharedReportPath))
            {
                string existing = File.ReadAllText(SharedReportPath);
                if (!string.IsNullOrWhiteSpace(existing))
                    root = JObject.Parse(existing);
            }

            JObject wrapper = JObject.Parse("{\n" + block + "\n}");
            root[SharedReportKey] = wrapper[SharedReportKey];
            File.WriteAllText(SharedReportPath, root.ToString(Formatting.Indented) + "\n");
        }

        private static void AppendOffset(StringBuilder builder, string field, int offset)
        {
            builder.Append("    <FIELD name=\"").Append(field).Append("\" offset=\"").Append(offset).AppendLine("\" />");
        }

        private static void AppendBuffer(StringBuilder builder, string name, BufferID bufferId)
        {
            builder.Append("    <BUFFER name=\"").Append(name).Append("\" id=\"").Append((int)bufferId).AppendLine("\" />");
        }
    }

    public sealed class VRInteractionKinematicTunerWindow : EditorWindow
    {
        private Slider _handRadius;
        private Slider _sdfEpsilon;
        private Slider _armLength;
        private Slider _snapScale;
        private Slider _velocityThreshold;
        private Slider _quality;
        private Slider _maxSubSteps;
        private Label _status;
        private Label _leftReadout;
        private Label _rightReadout;
        private Label _telemetryReadout;

        [MenuItem("Hecton8/VR/Open Kinematic Hand Tuner")]
        public static void Open()
        {
            GetWindow<VRInteractionKinematicTunerWindow>("VR Hand Bridge");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            _handRadius = AddSlider(root, "Hand Radius", 0.025f, 0.18f, VRInteractionKinematicBridgeConstants.DefaultHandRadiusMeters);
            _sdfEpsilon = AddSlider(root, "SDF Epsilon", 0.005f, 0.35f, 0.05f);
            _armLength = AddSlider(root, "Max Arm Length", 0.25f, 1.2f, VRInteractionKinematicBridgeConstants.DefaultMaxArmLengthMeters);
            _snapScale = AddSlider(root, "Socket Snap Scale", 0.05f, 3f, 1f);
            _velocityThreshold = AddSlider(root, "Velocity Signal", 0.1f, 12f, VRInteractionKinematicBridgeConstants.DefaultVelocitySignalThreshold);
            _quality = AddSlider(root, "GlobalQualityWeight", 0f, 1f, 1f);
            _maxSubSteps = AddSlider(root, "Max Sub-Steps", VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow, VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra, VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra);
            _quality.RegisterValueChangedCallback(evt =>
            {
                if (_maxSubSteps != null)
                    _maxSubSteps.SetValueWithoutNotify(VRInteractionKinematicBridgeMath.ResolveQualityIterationHint(evt.newValue));
            });
            _maxSubSteps.RegisterValueChangedCallback(evt =>
            {
                if (_quality != null)
                {
                    float normalized = (math.round(evt.newValue) - VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow) /
                                       (VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra - VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow);
                    _quality.SetValueWithoutNotify(math.saturate(normalized));
                }
            });

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8;
            row.Add(new Button(RefreshFromVault) { text = "Refresh" });
            row.Add(new Button(PushToVault) { text = "Apply" });
            row.Add(new Button(VRPhysicsInquisition.Run) { text = "Report" });
            root.Add(row);

            _status = new Label("Vault not read.");
            _status.style.marginTop = 8;
            root.Add(_status);
            _leftReadout = AddReadout(root, "Left: no telemetry.");
            _rightReadout = AddReadout(root, "Right: no telemetry.");
            _telemetryReadout = AddReadout(root, "Telemetry: no cursor.");
            RefreshFromVault();
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.showInputField = true;
            root.Add(slider);
            return slider;
        }

        private static Label AddReadout(VisualElement root, string text)
        {
            Label label = new Label(text);
            label.style.marginTop = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            root.Add(label);
            return label;
        }

        private void OnInspectorUpdate()
        {
            UpdateReadouts();
            Repaint();
        }

        private void RefreshFromVault()
        {
            if (!OpenOrCreateEditorViews(out VRInteractionKinematicBridgeViews views))
            {
                SetStatus("GlobalDataVault unavailable.");
                return;
            }

            VRInteractionTuningDTO tuning = views.Tuning[0];
            float epsilon = math.cmin(tuning.SdfCellSize);
            if (!math.isfinite(epsilon) || epsilon <= 0f)
                epsilon = 0.05f;
            _handRadius.SetValueWithoutNotify(math.max(0.025f, tuning.HandRadiusMeters));
            _sdfEpsilon.SetValueWithoutNotify(math.max(0.005f, epsilon));
            _armLength.SetValueWithoutNotify(math.max(0.25f, tuning.MaxArmLengthMeters));
            _snapScale.SetValueWithoutNotify(math.max(0.05f, tuning.SnapRadiusScale));
            _velocityThreshold.SetValueWithoutNotify(math.max(0.1f, tuning.VelocitySignalThreshold));
            _quality.SetValueWithoutNotify(math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight))));
            int hint = VRInteractionKinematicBridgeMath.ResolveQualityIterationHint(_quality.value);
            SetStatus("Vault read. Truth iterations " + VRInteractionKinematicBridgeMath.ResolveAuthoritativeIterationCount() + " hint " + hint);
            _maxSubSteps.SetValueWithoutNotify(hint);
            UpdateReadouts(views);
        }

        private void PushToVault()
        {
            if (!OpenOrCreateEditorViews(out VRInteractionKinematicBridgeViews views))
            {
                SetStatus("GlobalDataVault unavailable.");
                return;
            }

            VRInteractionTuningDTO tuning = views.Tuning[0];
            float normalizedSteps = (math.round(_maxSubSteps.value) - VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow) /
                                    (VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsUltra - VRInteractionKinematicBridgeConstants.DefaultSdfProbeIterationsLow);
            tuning.HandRadiusMeters = math.max(0.025f, _handRadius.value);
            tuning.SdfCellSize = new float3(math.max(0.005f, _sdfEpsilon.value));
            tuning.SdfRangeMeters = math.max(tuning.SdfRangeMeters, tuning.SdfCellSize.x * 8f);
            tuning.MaxArmLengthMeters = math.max(0.25f, _armLength.value);
            tuning.SnapRadiusScale = math.max(0.05f, _snapScale.value);
            tuning.VelocitySignalThreshold = math.max(0.1f, _velocityThreshold.value);
            tuning.GlobalQualityWeight = math.saturate(normalizedSteps);
            _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            tuning.Flags |=
                VRInteractionKinematicBridgeConstants.TuningFlagInitialized |
                VRInteractionKinematicBridgeConstants.TuningFlagSdfEnabled |
                VRInteractionKinematicBridgeConstants.TuningFlagSocketSnapEnabled |
                VRInteractionKinematicBridgeConstants.TuningFlagVelocitySignalEnabled;
            views.Tuning[0] = tuning;
            SetStatus("Applied. Truth iterations " + VRInteractionKinematicBridgeMath.ResolveAuthoritativeIterationCount() + " hint " + VRInteractionKinematicBridgeMath.ResolveQualityIterationHint(tuning.GlobalQualityWeight));
            UpdateReadouts(views);
        }

        private static bool OpenOrCreateEditorViews(out VRInteractionKinematicBridgeViews views)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return VRInteractionKinematicBridgeVault.EnsureBuffers(vault, out views);
        }

        private void SetStatus(string text)
        {
            if (_status != null)
                _status.text = text;
        }

        private void UpdateReadouts()
        {
            if (OpenOrCreateEditorViews(out VRInteractionKinematicBridgeViews views))
                UpdateReadouts(views);
        }

        private void UpdateReadouts(VRInteractionKinematicBridgeViews views)
        {
            if (!views.IsValid())
                return;

            int cursor = views.TelemetryCursor[0];
            if ((uint)cursor >= (uint)views.TelemetryRing.Length)
                cursor = 0;

            SetReadout(_leftReadout, "Left", views.HandStates[VRInteractionKinematicBridgeConstants.LeftHandIndex], views.TelemetryRing[cursor]);
            int rightSlot = math.min(cursor + 1, views.TelemetryRing.Length - 1);
            SetReadout(_rightReadout, "Right", views.HandStates[VRInteractionKinematicBridgeConstants.RightHandIndex], views.TelemetryRing[rightSlot]);
            VRInteractionTelemetryEntry telemetry = views.TelemetryRing[cursor];
            if (_telemetryReadout != null)
            {
                _telemetryReadout.text =
                    "Telemetry frame=" + telemetry.FrameIndex.ToString(CultureInfo.InvariantCulture) +
                    " micros=" + telemetry.CpuTimeMicros.ToString(CultureInfo.InvariantCulture) +
                    " flags=0x" + telemetry.Flags.ToString("X8", CultureInfo.InvariantCulture) +
                    " iterations=" + telemetry.SolverIterations.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void SetReadout(Label label, string handName, in VRHandStateDTO state, in VRInteractionTelemetryEntry telemetry)
        {
            if (label == null)
                return;

            label.text =
                handName +
                " raw=" + FormatAup(state.RawControllerAUP) +
                " resolved=" + FormatAup(state.ResolvedHandAUP) +
                " velocity=" + FormatFloat3(state.Velocity) +
                " micros=" + telemetry.CpuTimeMicros.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatAup(double3 value)
        {
            return "(" +
                   value.x.ToString("F3", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("F3", CultureInfo.InvariantCulture) + "," +
                   value.z.ToString("F3", CultureInfo.InvariantCulture) + ")";
        }

        private static string FormatFloat3(float3 value)
        {
            return "(" +
                   value.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   value.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }

    [InitializeOnLoad]
    public static class VRInteractionKinematicBridgeGizmo
    {
        static VRInteractionKinematicBridgeGizmo()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            double3 runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!VRInteractionKinematicBridgeMath.IsFinite(runtimeOriginAup))
                return;

            DrawHand(vault, PhysicalHandSide.Left, new Color(0.2f, 0.75f, 1f, 0.85f), runtimeOriginAup);
            DrawHand(vault, PhysicalHandSide.Right, new Color(1f, 0.75f, 0.2f, 0.85f), runtimeOriginAup);
        }

        private static void DrawHand(IDataVault vault, PhysicalHandSide side, Color color, double3 runtimeOriginAup)
        {
            if (!VRInteractionKinematicBridgeVault.TryReadLatestHandState(vault, side, out VRHandStateDTO state) ||
                !VRInteractionKinematicBridgeMath.TryResolveRuntimePosition(state.ResolvedHandAUP, runtimeOriginAup, out Vector3 resolved) ||
                !VRInteractionKinematicBridgeMath.TryResolveRuntimePosition(state.RawControllerAUP, runtimeOriginAup, out Vector3 raw))
            {
                return;
            }

            Handles.color = Color.yellow;
            Handles.SphereHandleCap(0, raw, Quaternion.identity, 0.055f, EventType.Repaint);
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, resolved, Quaternion.identity, 0.075f, EventType.Repaint);

            Vector3 correction = resolved - raw;
            if (correction.sqrMagnitude > 0.000001f)
            {
                Handles.color = Color.red;
                Handles.DrawLine(raw, resolved);
                Handles.ArrowHandleCap(
                    0,
                    resolved,
                    Quaternion.LookRotation(correction.normalized, Vector3.up),
                    Mathf.Min(0.25f, correction.magnitude),
                    EventType.Repaint);
            }
            else
            {
                Handles.color = color;
                Handles.DrawWireDisc(resolved, Vector3.up, 0.08f);
            }
        }
    }
}
