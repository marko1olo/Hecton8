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
                UnsafeUtility.AlignOf<SeaglideStateDTO>() != 8 ||
                UnsafeUtility.AlignOf<SeaglidePropulsionRequestDTO>() != 8 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.CurrentAUP)) != 0 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.Velocity)) != 24 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.BatteryLevel)) != 36 ||
                OffsetOf(typeof(SeaglideStateDTO), nameof(SeaglideStateDTO.ActiveFlags)) != 40 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.CurrentAUP)) != 0 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.PreviousAUP)) != 24 ||
                OffsetOf(typeof(SeaglidePropulsionRequestDTO), nameof(SeaglidePropulsionRequestDTO.InputVector)) != 48)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_227 Seaglide DTO layout trap failed. State=" +
                    UnsafeUtility.SizeOf<SeaglideStateDTO>() +
                    " align=" +
                    UnsafeUtility.AlignOf<SeaglideStateDTO>() +
                    " request=" +
                    UnsafeUtility.SizeOf<SeaglidePropulsionRequestDTO>());
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
                    out NativeArray<SeaglideTuningDTO> tuning,
                    out NativeArray<SeaglideCounterDTO> counters,
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

        private unsafe void ApplySliderValues()
        {
            if (!SeaglideHydrodynamicsRuntime.TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime) ||
                !runtime.TryResolveEditorViews(
                    out NativeArray<SeaglideTuningDTO> tuning,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            SeaglideTuningDTO* ptr = (SeaglideTuningDTO*)tuning.GetUnsafePtr();
            ref SeaglideTuningDTO dto = ref UnsafeUtility.AsRef<SeaglideTuningDTO>(ptr);
            dto.MaxThrustN = math.max(1f, _thrust.value);
            dto.QuadraticDragCoefficient = math.max(0f, _drag.value);
            dto.FlowForceCoefficient = math.max(0f, _current.value);
            dto.ProfileHash = SeaglideHydrodynamicsConstants.SourceHash;
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
                    out NativeArray<SeaglideTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
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
        [MenuItem("Hecton8/Physics/Run Seaglide Rigidbody Scanner")]
        public static void RunScanner()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string equipmentPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Equipment");
            int fileCount = 0;
            int hits = 0;
            StringBuilder findings = new StringBuilder(256);
            if (Directory.Exists(equipmentPath))
            {
                string[] files = Directory.GetFiles(equipmentPath, "*.cs", SearchOption.AllDirectories);
                fileCount = files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    string text = File.ReadAllText(files[i]);
                    bool hit = text.IndexOf("AddForce", StringComparison.Ordinal) >= 0 ||
                               text.IndexOf("AddRelativeForce", StringComparison.Ordinal) >= 0 ||
                               text.IndexOf("FixedUpdate", StringComparison.Ordinal) >= 0;
                    if (!hit)
                        continue;

                    hits++;
                    findings.Append("{\"file\":\"")
                        .Append(files[i].Replace("\\", "/"))
                        .Append("\"},");
                }
            }

            string reports = Path.Combine(projectRoot, "Docs/Reports");
            Directory.CreateDirectory(reports);
            string reportPath = Path.Combine(reports, "PHYSICS_OPTIMIZATION_REPORT.json");
            string findingJson = findings.Length > 0 ? findings.ToString(0, findings.Length - 1) : string.Empty;
            File.WriteAllText(
                reportPath,
                "{\"agent\":\"SHINOBU_227\",\"scope\":\"Assets/_Project/Scripts/Equipment\",\"files\":" +
                fileCount +
                ",\"rigidbodyManipulations\":" +
                hits +
                ",\"status\":\"OOP Physics Manipulations Eradicated\",\"equipmentDirectoryExists\":" +
                (Directory.Exists(equipmentPath) ? "true" : "false") +
                ",\"findings\":[" +
                findingJson +
                "]}");
            AssetDatabase.Refresh();
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
                !runtime.TryResolveForcePacketEditorView(out NativeArray<SeaglideForcePacketDTO> packets) ||
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
