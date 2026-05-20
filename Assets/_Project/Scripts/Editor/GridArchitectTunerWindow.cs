// ============================================================================
// HECTON-8 - GridArchitectTunerWindow.cs
// UI Toolkit facade for SHINOBU_114 CSR/Jacobi base logistics.
// ============================================================================

#if UNITY_EDITOR

using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Power;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class GridArchitectTunerWindow : EditorWindow
    {
        private const int MaxDrawnEdges = 3000;
        private const int GraphBarCount = 64;

        private readonly VisualElement[] _efficiencyBars = new VisualElement[GraphBarCount];
        private readonly float[] _efficiencySamples = new float[GraphBarCount];
        private Label _runtimeLabel;
        private Label _countsLabel;
        private Label _solverLabel;
        private Label _powerTelemetryLabel;
        private Slider _reactorOutput;
        private Slider _lifeSupportDrain;
        private Slider _oxygenDiffusion;
        private Slider _crushDepth;
        private Slider _baseWireConductance;
        private Slider _sumpPumpDraw;
        private Slider _jacobiSmoothing;
        private VaultGenerationHandle<PowerTelemetryEntry> _powerTelemetryRingHandle;
        private VaultGenerationHandle<PowerGridCounter64> _powerTelemetryCursorHandle;
        private IDataVault _powerTelemetryVault;
        private bool _suppressSliderEvents;

        [MenuItem("Hecton-8/Base Logistics Tuner")]
        private static void Open()
        {
            GetWindow<GridArchitectTunerWindow>("Base Logistics Tuner");
        }

        [MenuItem("Hecton-8/Base Power Tuner")]
        private static void OpenPowerAlias()
        {
            GetWindow<GridArchitectTunerWindow>("Base Power Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _runtimeLabel = new Label();
            _countsLabel = new Label();
            _solverLabel = new Label();
            _powerTelemetryLabel = new Label();
            root.Add(_runtimeLabel);
            root.Add(_countsLabel);
            root.Add(_solverLabel);
            root.Add(_powerTelemetryLabel);

            VisualElement graph = new VisualElement();
            graph.style.height = 72;
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.alignItems = Align.FlexEnd;
            graph.style.marginTop = 8;
            graph.style.marginBottom = 8;
            for (int i = 0; i < GraphBarCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginLeft = 1;
                bar.style.marginRight = 1;
                bar.style.height = 2;
                bar.style.backgroundColor = new Color(0.1f, 0.85f, 0.55f, 1f);
                _efficiencyBars[i] = bar;
                graph.Add(bar);
            }

            root.Add(graph);

            _reactorOutput = AddSlider(root, "Generator Output", 0f, 100000f, OnTuningChanged);
            _lifeSupportDrain = AddSlider(root, "Life Support Drain", 0f, 1000f, OnTuningChanged);
            _oxygenDiffusion = AddSlider(root, "Oxygen Diffusion", 0.01f, 2f, OnTuningChanged);
            _crushDepth = AddSlider(root, "Crush Depth", 0.1f, 10f, OnTuningChanged);
            _baseWireConductance = AddSlider(root, "Base Wire Conductance", 0.125f, 1000f, OnTuningChanged);
            _sumpPumpDraw = AddSlider(root, "Sump Pump Draw", 0f, 750f, OnDrainageTuningChanged);
            _jacobiSmoothing = AddSlider(root, "Jacobi Smoothing", 0.05f, 1f, OnTuningChanged);

            Button mockGraph = new Button(() => ShinobuLogisticsRouter.Active?.ForceRebuildMockGraph()) { text = "Mock Graph" };
            Button dumpBlackBox = new Button(() => ShinobuLogisticsRouter.Active?.ForceDumpBlackBox()) { text = "Dump Black Box" };
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;
            row.Add(mockGraph);
            row.Add(dumpBlackBox);
            root.Add(row);

            RefreshUi();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += RefreshUi;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= RefreshUi;
        }

        private static Slider AddSlider(VisualElement root, string label, float low, float high, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(callback);
            root.Add(slider);
            return slider;
        }

        private void OnTuningChanged(ChangeEvent<float> evt)
        {
            if (_suppressSliderEvents)
                return;

            ShinobuLogisticsRouter.TryGetTuning(out LogisticsTuningDTO tuning);
            tuning.ReactorOutputWatts = _reactorOutput.value;
            tuning.LifeSupportDrainWatts = _lifeSupportDrain.value;
            tuning.OxygenDiffusionRate = _oxygenDiffusion.value;
            tuning.CrushDepthMultiplier = _crushDepth.value;
            tuning.BasePipeResistance = ConductanceToResistance(_baseWireConductance.value);
            tuning.JacobiSmoothingFactor = _jacobiSmoothing.value;
            ShinobuLogisticsRouter.SetTuning(in tuning);
        }

        private void OnDrainageTuningChanged(ChangeEvent<float> evt)
        {
            if (_suppressSliderEvents)
                return;

            if (!SumpPumpPipeGridRuntime.TryGetTuning(out DrainageTuningDTO tuning))
                return;

            tuning.PumpPowerDraw = Mathf.Max(0f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in tuning);
        }

        private void RefreshUi()
        {
            if (_runtimeLabel == null)
                return;

            bool active = ShinobuLogisticsRouter.HasActiveRuntime();
            _runtimeLabel.text = active ? "Runtime: Active" : "Runtime: Offline";
            _countsLabel.text = "Nodes: " + ShinobuLogisticsRouter.DebugNodeCount() + "  Edges: " + ShinobuLogisticsRouter.DebugEdgeCount();

            if (ShinobuLogisticsRouter.TryGetLatestTelemetry(out LogisticsGraphTelemetryEntry entry))
            {
                _solverLabel.text = "Jacobi: " + entry.JacobiIterations + "  Components: " + entry.ComponentCount + "  Solver us: " + entry.SolverMicros;
                PushEfficiencySample(entry.SupplyRatio);
            }

            RefreshPowerTelemetryReadout();

            if (ShinobuLogisticsRouter.TryGetTuning(out LogisticsTuningDTO tuning))
            {
                _suppressSliderEvents = true;
                _reactorOutput.SetValueWithoutNotify(tuning.ReactorOutputWatts);
                _lifeSupportDrain.SetValueWithoutNotify(tuning.LifeSupportDrainWatts);
                _oxygenDiffusion.SetValueWithoutNotify(tuning.OxygenDiffusionRate);
                _crushDepth.SetValueWithoutNotify(tuning.CrushDepthMultiplier);
                _baseWireConductance.SetValueWithoutNotify(ResistanceToConductance(tuning.BasePipeResistance));
                _jacobiSmoothing.SetValueWithoutNotify(tuning.JacobiSmoothingFactor);
                _suppressSliderEvents = false;
            }

            if (SumpPumpPipeGridRuntime.TryGetTuning(out DrainageTuningDTO drainageTuning))
            {
                _suppressSliderEvents = true;
                _sumpPumpDraw.SetValueWithoutNotify(drainageTuning.PumpPowerDraw);
                _suppressSliderEvents = false;
            }
        }

        private void RefreshPowerTelemetryReadout()
        {
            if (_powerTelemetryLabel == null)
                return;

            if (!TryGetLatestPowerTelemetry(out PowerTelemetryEntry entry))
            {
                _powerTelemetryLabel.text = "Power Telemetry: Offline";
                return;
            }

            _powerTelemetryLabel.text =
                "Power Telemetry: Gen " + entry.TotalGeneration.ToString("0.0") +
                " W  Load " + entry.TotalLoad.ToString("0.0") +
                " W  AvgV " + entry.AveragePotential.ToString("0.000") +
                "  Brownout " + entry.BrownoutCount +
                "  Solver us " + entry.SolverMicroseconds;
        }

        private bool TryGetLatestPowerTelemetry(out PowerTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            if (!ReferenceEquals(_powerTelemetryVault, vault))
            {
                _powerTelemetryVault = vault;
                _powerTelemetryRingHandle = default;
                _powerTelemetryCursorHandle = default;
            }

            if (!TryResolvePowerTelemetry(vault, out NativeArray<PowerTelemetryEntry> ring, out NativeArray<PowerGridCounter64> cursor))
                return false;

            PowerGridCounter64 cursorState = cursor[0];
            int writeCursor = cursorState.Value;
            if ((uint)writeCursor >= (uint)ring.Length)
                writeCursor = 0;

            int readIndex = writeCursor - 1;
            if (readIndex < 0)
                readIndex = ring.Length - 1;

            entry = ring[readIndex];
            return entry.FrameIndex != 0u || entry.NodeCount != 0 || entry.EdgeCount != 0 || entry.RuntimeEdgeCount != 0;
        }

        private bool TryResolvePowerTelemetry(
            IDataVault vault,
            out NativeArray<PowerTelemetryEntry> ring,
            out NativeArray<PowerGridCounter64> cursor)
        {
            ring = default;
            cursor = default;
            if (!TryResolveTelemetryRing(vault, out ring))
                return false;

            return TryResolveTelemetryCursor(vault, out cursor);
        }

        private bool TryResolveTelemetryRing(IDataVault vault, out NativeArray<PowerTelemetryEntry> ring)
        {
            if (_powerTelemetryRingHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _powerTelemetryRingHandle, out ring) ||
                !ring.IsCreated ||
                ring.Length < PowerGridJacobiConstants.TelemetryFrameCount)
            {
                if (!vault.TryGetGenerationHandle(PowerGridBufferIds.TelemetryRing, out _powerTelemetryRingHandle) ||
                    _powerTelemetryRingHandle.BufferID == 0u ||
                    !vault.TryResolveHandle(in _powerTelemetryRingHandle, out ring) ||
                    !ring.IsCreated ||
                    ring.Length < PowerGridJacobiConstants.TelemetryFrameCount)
                {
                    ring = default;
                    return false;
                }
            }

            return true;
        }

        private bool TryResolveTelemetryCursor(IDataVault vault, out NativeArray<PowerGridCounter64> cursor)
        {
            if (_powerTelemetryCursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _powerTelemetryCursorHandle, out cursor) ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                if (!vault.TryGetGenerationHandle(PowerGridBufferIds.TelemetryCursor, out _powerTelemetryCursorHandle) ||
                    _powerTelemetryCursorHandle.BufferID == 0u ||
                    !vault.TryResolveHandle(in _powerTelemetryCursorHandle, out cursor) ||
                    !cursor.IsCreated ||
                    cursor.Length <= 0)
                {
                    cursor = default;
                    return false;
                }
            }

            return true;
        }

        private static float ResistanceToConductance(float resistance)
        {
            return 1f / math.max(0.001f, math.isfinite(resistance) ? resistance : 0.35f);
        }

        private static float ConductanceToResistance(float conductance)
        {
            return 1f / math.max(0.001f, math.isfinite(conductance) ? conductance : 1f);
        }

        private void PushEfficiencySample(float supplyRatio)
        {
            for (int i = 1; i < GraphBarCount; i++)
                _efficiencySamples[i - 1] = _efficiencySamples[i];

            _efficiencySamples[GraphBarCount - 1] = math.saturate(supplyRatio);
            for (int i = 0; i < GraphBarCount; i++)
            {
                VisualElement bar = _efficiencyBars[i];
                if (bar == null)
                    continue;

                float sample = _efficiencySamples[i];
                bar.style.height = math.max(2f, sample * 68f);
                bar.style.backgroundColor = Color.Lerp(new Color(0.95f, 0.22f, 0.12f, 1f), new Color(0.1f, 0.85f, 0.55f, 1f), sample);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawTopologyGizmo();
        }

        private static void DrawTopologyGizmo()
        {
            int edgeCount = math.min(ShinobuLogisticsRouter.DebugEdgeCount(), MaxDrawnEdges);
            if (edgeCount <= 0)
                return;

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            for (int i = 0; i < edgeCount; i++)
            {
                if (!ShinobuLogisticsRouter.TryGetDebugEdge(i, out float3 nodeA, out float3 nodeB, out ulong flagsA, out ulong flagsB, out int componentA, out int componentB, out float flow01))
                    continue;

                bool isolated = componentA < 0 || componentA != componentB;
                bool flooded = ((flagsA | flagsB) & LogisticsStateFlags.Flooded) != 0;
                Color baseColor = isolated ? Color.magenta : (flooded ? Color.cyan : ComponentColor(componentA));
                Handles.color = Color.Lerp(baseColor * 0.65f, Color.white, math.saturate(flow01));
                Handles.DrawLine(ToVector3(nodeA), ToVector3(nodeB), 2f);
            }

            Handles.zTest = previousZTest;
        }

        private static Color ComponentColor(int componentId)
        {
            uint hash = (uint)(componentId * 1103515245 + 12345);
            float r = 0.25f + ((hash & 0xFFu) / 255f) * 0.7f;
            float g = 0.25f + (((hash >> 8) & 0xFFu) / 255f) * 0.7f;
            float b = 0.25f + (((hash >> 16) & 0xFFu) / 255f) * 0.7f;
            return new Color(r, g, b, 1f);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}

#endif
