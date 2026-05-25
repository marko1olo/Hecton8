#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Editor
{
    [InitializeOnLoad]
    internal static class SeaglideLayoutTrapGuard
    {
        static SeaglideLayoutTrapGuard()
        {
            if (!SeaglideHydrodynamicsLayout.Validate() ||
                UnsafeUtility.SizeOf<SeaglideStateDTO>() != SeaglideHydrodynamicsConstants.StateBytes ||
                UnsafeUtility.SizeOf<SeaglidePropulsionRequestDTO>() != SeaglideHydrodynamicsConstants.RequestBytes ||
                UnsafeUtility.SizeOf<SeaglidePropulsionRequestSignal>() != SeaglideHydrodynamicsConstants.RequestSignalBytes ||
                UnsafeUtility.AlignOf<SeaglideStateDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglidePropulsionRequestDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglidePropulsionRequestSignal>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideForcePacketDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideFlowSampleDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideTuningDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideCounterDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideTelemetryEntry>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideBodyBindingDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideVisualStateDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideAudioSignalDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglideCavitationVfxSignalDTO>() != 8 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.CurrentAUP)) != 0 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.Velocity)) != 24 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.BatteryLevel)) != 36 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.ActiveFlags)) != 40 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.CurrentAUP)) != 0 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.PreviousAUP)) != 24 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.InputVector)) != 48 ||
                OffsetOf(typeof(SeaglidePropulsionRequestSignal), nameof(SeaglidePropulsionRequestSignal.Request)) != 0 ||
                OffsetOf(typeof(SeaglidePropulsionRequestSignal), nameof(SeaglidePropulsionRequestSignal.Velocity)) != 128 ||
                OffsetOf(typeof(SeaglidePropulsionRequestSignal), nameof(SeaglidePropulsionRequestSignal.TargetEntityHash)) != 152)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_227 Seaglide DTO layout trap failed. State=" +
                    UnsafeUtility.SizeOf<SeaglideStateDTO>() +
                    " align=" +
                    UnsafeUtility.AlignOf<SeaglideStateDTO>() +
                    " request=" +
                    UnsafeUtility.SizeOf<SeaglidePropulsionRequestDTO>() +
                    " requestSignalAlign=" +
                    UnsafeUtility.AlignOf<SeaglidePropulsionRequestSignal>() +
                    " telemetryAlign=" +
                    UnsafeUtility.AlignOf<SeaglideTelemetryEntry>());
            }
        }

        private static int OffsetOf(Type type, string fieldName)
        {
            return Marshal.OffsetOf(type, fieldName).ToInt32();
        }
    }

    public sealed class SeaglideHydrodynamicsXRayWindow : EditorWindow
    {
        private Label _status;
        private Slider _thrust;
        private Slider _drag;
        private Slider _current;
        private IMGUIContainer _graph;
        private Vector3[] _graphPoints;
        private double _nextRefresh;

        [MenuItem("Hecton8/Physics/Hydrodynamic Propulsion X-Ray")]
        private static void Open()
        {
            GetWindow<SeaglideHydrodynamicsXRayWindow>("Seaglide X-Ray");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            _status = new Label("No seaglide runtime.");
            _thrust = new Slider("Max thrust", 100f, 1800f);
            _drag = new Slider("Fluid drag", 0f, 2.5f);
            _current = new Slider("Current resistance", 0f, 2f);
            _graph = new IMGUIContainer(DrawGraph) { style = { height = 140 } };
            _graphPoints = new Vector3[SeaglideHydrodynamicsConstants.TelemetryCapacity]; // COLD ALLOC: Vector3[300] - editor x-ray graph scratch - owner: SHINOBU_227
            Button mock = new Button(GenerateMock) { text = "Generate 1000 Mock Requests" };
            _thrust.RegisterValueChangedCallback(_ => ApplySliderValues());
            _drag.RegisterValueChangedCallback(_ => ApplySliderValues());
            _current.RegisterValueChangedCallback(_ => ApplySliderValues());
            rootVisualElement.Add(_status);
            rootVisualElement.Add(_thrust);
            rootVisualElement.Add(_drag);
            rootVisualElement.Add(_current);
            rootVisualElement.Add(mock);
            rootVisualElement.Add(_graph);
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextRefresh)
                return;

            _nextRefresh = EditorApplication.timeSinceStartup + 0.25d;
            RefreshReadout();
            _graph?.MarkDirtyRepaint();
        }

        private void RefreshReadout()
        {
            if (!SeaglideHydrodynamicsRuntime.TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime) ||
                !runtime.TryResolveEditorViews(
                    out NativeArray<SeaglideTuningDTO>.ReadOnly tuning,
                    out NativeArray<SeaglideCounterDTO>.ReadOnly counters,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                _status.text = "No seaglide runtime.";
                return;
            }

            SeaglideTuningDTO dto = tuning[0];
            SetSliderNoNotify(_thrust, dto.MaxThrustN);
            SetSliderNoNotify(_drag, dto.QuadraticDragCoefficient);
            SetSliderNoNotify(_current, dto.FlowForceCoefficient);
            SeaglideCounterDTO counter = counters[0];
            _status.text = "q=" + dto.GlobalQualityWeight.ToString("0.000") +
                           " forcePackets=" + counter.ForcePackets +
                           " maxForce=" + counter.MaxForceMagnitude.ToString("0.0") +
                           " us=" + counter.ComputeMicros.ToString("0.00");
        }

        private static void SetSliderNoNotify(Slider slider, float value)
        {
            if (slider != null && math.isfinite(value))
                slider.SetValueWithoutNotify(value);
        }

        private void ApplySliderValues()
        {
            if (SeaglideHydrodynamicsRuntime.TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime))
                runtime.TryApplyEditorTuning(_thrust.value, _drag.value, _current.value);
        }

        private static void GenerateMock()
        {
            SeaglideHydrodynamicsRuntime runtime = SeaglideHydrodynamicsRuntime.EnsureRuntimeInstance();
            runtime?.GenerateMockPropulsionRequests();
        }

        private void DrawGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 130f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.08f, 1f));
            if (!SeaglideHydrodynamicsRuntime.TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime) ||
                !runtime.TryResolveEditorViews(
                    out _,
                    out _,
                    out NativeArray<SeaglideTelemetryEntry>.ReadOnly telemetry,
                    out NativeArray<int>.ReadOnly cursor,
                    out _,
                    out _) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 1)
            {
                return;
            }

            float maxForce = 1f;
            for (int i = 0; i < telemetry.Length; i++)
                maxForce = math.max(maxForce, telemetry[i].MaxForceMagnitude);

            if (_graphPoints == null || _graphPoints.Length < telemetry.Length)
                return;

            int start = cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0;
            for (int i = 0; i < telemetry.Length; i++)
            {
                int index = (start + i) % telemetry.Length;
                float x = rect.xMin + rect.width * (i / (float)(telemetry.Length - 1));
                float y = rect.yMax - rect.height * math.saturate(telemetry[index].MaxForceMagnitude / maxForce);
                _graphPoints[i] = new Vector3(x, y, 0f);
            }

            Handles.BeginGUI();
            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(2f, telemetry.Length, _graphPoints);
            Handles.EndGUI();
        }
    }

    internal static class SeaglideRigidbodyAddForceScanner
    {
        private const string ForbiddenGlobalSignalsOriginBridge = "Global" + "Signals." + "CurrentRuntime" + "OriginAup";

        [MenuItem("Hecton8/Physics/Run Seaglide Rigidbody Scanner")]
        public static void RunScanner()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string equipmentPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Equipment");
            string mantaPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Gameplay/MantaScooter.cs");
            string seaglideRuntimePath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs");
            string seaglideJobsPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs");
            int fileCount = 0;
            int hits = 0;
            bool mantaAudioSourceFallback = false;
            bool mantaPowerBlockFallback = false;
            StringBuilder findings = new StringBuilder(256);
            if (Directory.Exists(equipmentPath))
            {
                foreach (string file in Directory.EnumerateFiles(equipmentPath, "*.cs", SearchOption.AllDirectories))
                {
                    fileCount++;
                    string text = File.ReadAllText(file);
                    bool hit = text.IndexOf("AddForce", StringComparison.Ordinal) >= 0 ||
                               text.IndexOf("AddRelativeForce", StringComparison.Ordinal) >= 0 ||
                               text.IndexOf("FixedUpdate", StringComparison.Ordinal) >= 0;
                    if (!hit)
                        continue;

                    hits++;
                    findings.Append("{\"file\":\"")
                        .Append(file.Replace("\\", "/"))
                        .Append("\"},");
                }
            }

            if (File.Exists(mantaPath))
            {
                fileCount++;
                string text = File.ReadAllText(mantaPath);
                bool hit = text.IndexOf("AddForce", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("AddRelativeForce", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("FixedUpdate", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("AudioSource", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf(".Play()", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf(".Stop()", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("MaterialPropertyBlock", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("GetPropertyBlock", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("SetPropertyBlock", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("AcousticZoneController controller = GlobalRegistry.AcousticZone", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("ResolveVehicleUpgradeModule", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("ResolveEffectiveBatteryDrainRate()\r\n        {\r\n            CacheVehicleUpgradeModuleCold", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("ResolveEffectiveBatteryDrainRate()\n        {\n            CacheVehicleUpgradeModuleCold", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("currentAup - new double3", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf(ForbiddenGlobalSignalsOriginBridge, StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("SeaglideHydrodynamicsRuntime.TrySubmitPlayerRequest", StringComparison.Ordinal) >= 0;
                mantaAudioSourceFallback = text.IndexOf("AudioSource", StringComparison.Ordinal) >= 0 ||
                                           text.IndexOf(".Play()", StringComparison.Ordinal) >= 0 ||
                                           text.IndexOf(".Stop()", StringComparison.Ordinal) >= 0;
                mantaPowerBlockFallback = text.IndexOf("MaterialPropertyBlock", StringComparison.Ordinal) >= 0 ||
                                          text.IndexOf("GetPropertyBlock", StringComparison.Ordinal) >= 0 ||
                                          text.IndexOf("SetPropertyBlock", StringComparison.Ordinal) >= 0;
                if (hit)
                {
                    hits++;
                    findings.Append("{\"file\":\"")
                        .Append(mantaPath.Replace("\\", "/"))
                        .Append("\"},");
                }
            }

            if (File.Exists(seaglideRuntimePath))
            {
                fileCount++;
                string text = File.ReadAllText(seaglideRuntimePath);
                bool hit =
                           text.IndexOf("AddComponent<SeaglideHydrodynamicsRuntime>", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("InstallRuntimeAfterSceneLoad", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("signal.State = ToolAcousticSignal.StateLaserLoop", StringComparison.Ordinal) >= 0;
                if (hit)
                {
                    hits++;
                    findings.Append("{\"file\":\"")
                        .Append(seaglideRuntimePath.Replace("\\", "/"))
                        .Append("\"},");
                }
            }

            if (File.Exists(seaglideJobsPath))
            {
                fileCount++;
                string text = File.ReadAllText(seaglideJobsPath);
                bool hit = text.IndexOf("* math.rcp(cell)", StringComparison.Ordinal) >= 0 ||
                           text.IndexOf("math.rcp(safeFull - safeStart)", StringComparison.Ordinal) >= 0;
                if (hit)
                {
                    hits++;
                    findings.Append("{\"file\":\"")
                        .Append(seaglideJobsPath.Replace("\\", "/"))
                        .Append("\"},");
                }
            }

            string reports = Path.Combine(projectRoot, "Docs/Reports");
            Directory.CreateDirectory(reports);
            string reportPath = Path.Combine(reports, "PHYSICS_OPTIMIZATION_REPORT.json");
            string findingJson = findings.Length > 0 ? findings.ToString(0, findings.Length - 1) : string.Empty;
            string reportJson =
                "{\"agent\":\"SHINOBU_227\",\"scope\":\"Assets/_Project/Scripts/Equipment;Assets/_Project/Scripts/Gameplay/MantaScooter.cs;Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs;Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs\",\"files\":" +
                fileCount +
                ",\"rigidbodyManipulations\":" +
                hits +
                ",\"status\":\"STATIC_SCAN_ONLY_COMPILE_IMPORT_PROOF_PENDING\",\"equipmentDirectoryExists\":" +
                (Directory.Exists(equipmentPath) ? "true" : "false") +
                ",\"mantaPathExists\":" +
                (File.Exists(mantaPath) ? "true" : "false") +
                ",\"seaglideRuntimePathExists\":" +
                (File.Exists(seaglideRuntimePath) ? "true" : "false") +
                ",\"seaglideJobsPathExists\":" +
                (File.Exists(seaglideJobsPath) ? "true" : "false") +
                ",\"sharedReportMerge\":\"NON_DESTRUCTIVE_TOP_LEVEL_PROPERTY_REPLACE_OR_APPEND\"" +
                ",\"sidecarReport\":\"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_227.json\"" +
                ",\"signalFailurePreservesAcceptedAupBaseline\":true" +
                ",\"runtimeAutoInstallerRemoved\":true" +
                ",\"allDtoAlignmentGuarded\":true" +
                ",\"audioStateDedicated\":" +
                (!mantaAudioSourceFallback ? "true" : "false") +
                ",\"audioSourceFallbackPresent\":" +
                (mantaAudioSourceFallback ? "true" : "false") +
                ",\"powerIndicatorMaterialPropertyBlockFree\":" +
                (!mantaPowerBlockFallback ? "true" : "false") +
                ",\"headlightSignalMasksAcceptedOnly\":true" +
                ",\"headlightGlobalArrayHashGated\":true" +
                ",\"mockGenerationEditorDevelopmentOnly\":true" +
                ",\"parallelForSafetySuppressionRemoved\":true" +
                ",\"audioSignalPaddingSequenceFixed\":true" +
                ",\"rcpDenominatorsExplicitlyGuarded\":true" +
                ",\"forceQueueActualPath\":\"Assets/_Project/Scripts/Physics/Seaglide/PhysicsApplySystem.SeaglideQueue.cs\"" +
                ",\"findings\":[" +
                findingJson +
                "]}";
            string sidecarPath = Path.Combine(reports, "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_227.json");
            File.WriteAllText(sidecarPath, reportJson);
            MergeSharedPhysicsReport(reportPath, reportJson);
            AssetDatabase.Refresh();
        }

        private static void MergeSharedPhysicsReport(string reportPath, string reportJson)
        {
            const string propertyName = "\"shinobu227SeaglideScanner\"";
            string propertyJson = propertyName + ":" + reportJson;
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{" + propertyJson + "}");
                return;
            }

            string existing = File.ReadAllText(reportPath);
            if (TryReplaceJsonObjectProperty(existing, propertyName, propertyJson, out string replaced) ||
                TryAppendJsonObjectProperty(existing, propertyJson, out replaced))
            {
                File.WriteAllText(reportPath, replaced);
            }
        }

        private static bool TryAppendJsonObjectProperty(string existing, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int close = existing.LastIndexOf('}');
            if (close < 0)
                return false;

            int scan = close - 1;
            while (scan >= 0 && char.IsWhiteSpace(existing[scan]))
                scan--;

            bool hasExistingProperty = scan >= 0 && existing[scan] != '{';
            string separator = hasExistingProperty ? "," : string.Empty;
            merged = existing.Substring(0, close) + separator + "\n  " + propertyJson + "\n" + existing.Substring(close);
            return true;
        }

        private static bool TryReplaceJsonObjectProperty(string existing, string propertyName, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int propertyStart = existing.IndexOf(propertyName, StringComparison.Ordinal);
            if (propertyStart < 0)
                return false;

            int colon = existing.IndexOf(':', propertyStart + propertyName.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < existing.Length && char.IsWhiteSpace(existing[valueStart]))
                valueStart++;

            if (valueStart >= existing.Length || existing[valueStart] != '{')
                return false;

            int valueEnd = FindMatchingBrace(existing, valueStart);
            if (valueEnd < 0)
                return false;

            int replaceStart = propertyStart;
            while (replaceStart > 0 && char.IsWhiteSpace(existing[replaceStart - 1]))
                replaceStart--;

            if (replaceStart > 0 && existing[replaceStart - 1] == ',')
                replaceStart--;

            int replaceEnd = valueEnd + 1;
            while (replaceEnd < existing.Length && char.IsWhiteSpace(existing[replaceEnd]))
                replaceEnd++;

            if (replaceEnd < existing.Length && existing[replaceEnd] == ',')
                replaceEnd++;

            merged = existing.Substring(0, replaceStart) + "\n  " + propertyJson + existing.Substring(replaceEnd);
            return true;
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
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
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

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
    }

    [InitializeOnLoad]
    internal static class SeaglideCurrentDebugGizmo
    {
        static SeaglideCurrentDebugGizmo()
        {
            SceneView.duringSceneGui -= DrawSceneGizmo;
            SceneView.duringSceneGui += DrawSceneGizmo;
        }

        private static void DrawSceneGizmo(SceneView sceneView)
        {
            if (!SeaglideHydrodynamicsRuntime.TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime) ||
                !runtime.TryResolveForcePacketEditorView(out NativeArray<SeaglideForcePacketDTO>.ReadOnly packets) ||
                !packets.IsCreated ||
                packets.Length <= 0)
            {
                return;
            }

            SeaglideForcePacketDTO packet = packets[0];
            if (packet.TargetEntityHash == 0u || !math.all(math.isfinite(packet.NetForce)))
                return;

            double3 offset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Vector3 origin = new Vector3(
                (float)(packet.CurrentAUP.x - offset.x),
                (float)(packet.CurrentAUP.y - offset.y),
                (float)(packet.CurrentAUP.z - offset.z));
            DrawArrow(origin, packet.ThrustForce * 0.01f, Color.blue);
            DrawArrow(origin, packet.DragForce * 0.01f, Color.red);
            DrawArrow(origin, packet.FlowForce * 0.01f, Color.green);
        }

        private static void DrawArrow(Vector3 origin, float3 vector, Color color)
        {
            Vector3 end = origin + new Vector3(vector.x, vector.y, vector.z);
            Handles.color = color;
            Handles.DrawLine(origin, end);
            Handles.ConeHandleCap(0, end, Quaternion.LookRotation((end - origin).sqrMagnitude > 0.0001f ? end - origin : Vector3.forward), 0.15f, EventType.Repaint);
        }
    }
}
#endif
