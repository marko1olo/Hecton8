#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Physics;
using Unity.Collections;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Editor
{
    public sealed class AsyncGpuReadbackXRayWindow : EditorWindow
    {
        private const int GraphBins = 32;
        private const double RefreshIntervalSeconds = 0.1d;

        private Label _phaseLabel;
        private Label _frameLabel;
        private Label _qualityLabel;
        private Label _requestsLabel;
        private Label _completedLabel;
        private Label _activeSlotsLabel;
        private Label _latencyLabel;
        private Label _droppedLabel;
        private Label _failedLabel;
        private Label _maxStaleLabel;
        private Label _dispatchMicrosLabel;
        private Label _applyMicrosLabel;
        private Label _smoothingLabel;
        private Label _entityLabel;
        private Label _heightLabel;
        private Label _statusLabel;
        private SliderInt _sampleCapSlider;
        private Slider _decaySlider;
        private Toggle _qualityOverrideToggle;
        private Slider _qualitySlider;
        private readonly VisualElement[] _graphBars = new VisualElement[GraphBins];
        private bool _updatingControls;
        private double _nextRefreshTime;

        [MenuItem("HECTON-8/Physics/Async Buoyancy GPU Readback XRay")]
        public static void Open()
        {
            GetWindow<AsyncGpuReadbackXRayWindow>("Async Buoyancy XRay");
        }

        private void OnEnable()
        {
            EditorApplication.update += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Refresh;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            root.Add(scroll);

            _statusLabel = new Label("No active AsyncBuoyancyReadbackRuntime.");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            scroll.Add(_statusLabel);

            scroll.Add(Section("Readback State"));
            scroll.Add(Row("Phase", out _phaseLabel));
            scroll.Add(Row("Frame", out _frameLabel));
            scroll.Add(Row("Quality", out _qualityLabel));
            scroll.Add(Row("Requests", out _requestsLabel));
            scroll.Add(Row("Completed", out _completedLabel));
            scroll.Add(Row("Active Ring Slots", out _activeSlotsLabel));
            scroll.Add(Row("Latency Frames", out _latencyLabel));
            scroll.Add(Row("Dropped", out _droppedLabel));
            scroll.Add(Row("Failed", out _failedLabel));
            scroll.Add(Row("Max Stale Frames", out _maxStaleLabel));
            scroll.Add(Row("Dispatch us", out _dispatchMicrosLabel));
            scroll.Add(Row("Apply Schedule us", out _applyMicrosLabel));
            scroll.Add(Row("Smoothing Alpha", out _smoothingLabel));
            scroll.Add(Row("Last Entity", out _entityLabel));
            scroll.Add(Row("Last Local Height", out _heightLabel));

            scroll.Add(Section("Tuning Bridge"));
            _sampleCapSlider = new SliderInt("Max Sample Points", 4, AsyncBuoyancyReadbackConstants.RequestCapacity);
            _sampleCapSlider.RegisterValueChangedCallback(_ => PushTuning());
            scroll.Add(_sampleCapSlider);
            _decaySlider = new Slider("Dead Reckoning Decay", 0.65f, 0.99f);
            _decaySlider.RegisterValueChangedCallback(_ => PushTuning());
            scroll.Add(_decaySlider);
            _qualityOverrideToggle = new Toggle("Override GlobalQualityWeight");
            _qualityOverrideToggle.RegisterValueChangedCallback(_ => PushTuning());
            scroll.Add(_qualityOverrideToggle);
            _qualitySlider = new Slider("Quality Weight", 0f, 1f);
            _qualitySlider.RegisterValueChangedCallback(_ => PushTuning());
            scroll.Add(_qualitySlider);

            scroll.Add(Section("Latency Waterfall"));
            for (int i = 0; i < _graphBars.Length; i++)
            {
                VisualElement row = new VisualElement();
                row.style.height = 6;
                row.style.marginBottom = 2;
                VisualElement bar = new VisualElement();
                bar.style.height = 5;
                bar.style.width = 4;
                bar.style.backgroundColor = new Color(0.15f, 0.75f, 0.95f, 0.8f);
                _graphBars[i] = bar;
                row.Add(bar);
                scroll.Add(row);
            }

            scroll.Add(Section("Proof Tools"));
            Button validateButton = new Button(AsyncBuoyancyReadbackLayoutValidator.ValidateFromMenu) { text = "Validate Layout" };
            scroll.Add(validateButton);
            Button scannerButton = new Button(SynchronousGpuReadbackScanner.RunFromMenu) { text = "Run Roslyn Sync GPU Scanner" };
            scroll.Add(scannerButton);

            Refresh();
        }

        private void Refresh()
        {
            if (_statusLabel == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;

            if (!AsyncBuoyancyReadbackRuntime.TryGetActiveRuntimeInstance(out AsyncBuoyancyReadbackRuntime runtime) ||
                !runtime.TryOpenEditorViews(
                    out NativeArray<ReadbackTuningDTO>.ReadOnly tuning,
                    out NativeArray<ReadbackTelemetryEntry>.ReadOnly telemetry,
                    out NativeArray<int>.ReadOnly cursor,
                    out NativeArray<AsyncReadbackCounterDTO>.ReadOnly counters))
            {
                SetRuntimeMissing();
                return;
            }

            ReadbackTuningDTO t = tuning.Length > 0 ? tuning[0] : default;
            AsyncReadbackCounterDTO c = counters.Length > 0 ? counters[0] : default;
            int cursorValue = cursor.Length > 0 ? cursor[0] : 0;

            SetLabel(_statusLabel, "Active runtime: Vault buffers resolved.");
            SetLabel(_phaseLabel, (c.Flags & AsyncBuoyancyReadbackConstants.FlagMockPath) != 0 ? "MOCK_DELAYED" : "GPU_ASYNC");
            SetLabel(_frameLabel, t.FrameIndex.ToString());
            SetLabel(_qualityLabel, t.GlobalQualityWeight.ToString("0.000", CultureInfo.InvariantCulture));
            SetLabel(_requestsLabel, c.DispatchCount.ToString());
            SetLabel(_completedLabel, c.CompletedCount.ToString());
            SetLabel(_activeSlotsLabel, c.ActiveRingSlots.ToString());
            SetLabel(_latencyLabel, c.LastLatencyFrames.ToString());
            SetLabel(_droppedLabel, c.DroppedRequests.ToString());
            SetLabel(_failedLabel, c.FailedRequests.ToString());
            SetLabel(_maxStaleLabel, c.MaxStaleFrames.ToString());
            SetLabel(_dispatchMicrosLabel, c.DispatchMicros.ToString("0.00", CultureInfo.InvariantCulture));
            SetLabel(_applyMicrosLabel, c.ApplyMicros.ToString("0.00", CultureInfo.InvariantCulture));
            SetLabel(_smoothingLabel, t.SmoothingAlpha.ToString("0.000", CultureInfo.InvariantCulture));
            SetLabel(_entityLabel, "0x" + c.LastEntityHash.ToString("X8"));
            SetLabel(_heightLabel, c.LastLocalHeight.ToString("0.000", CultureInfo.InvariantCulture));

            _updatingControls = true;
            _sampleCapSlider.SetValueWithoutNotify(math.clamp(t.MaxSampleCount, 4, AsyncBuoyancyReadbackConstants.RequestCapacity));
            _decaySlider.SetValueWithoutNotify(math.saturate(t.DeadReckoningDecayRate));
            _qualitySlider.SetValueWithoutNotify(math.saturate(t.GlobalQualityWeight));
            _updatingControls = false;

            UpdateWaterfall(telemetry, cursorValue);
        }

        private void SetRuntimeMissing()
        {
            SetLabel(_statusLabel, "No active AsyncBuoyancyReadbackRuntime with open Vault buffers.");
            ClearLabel(_phaseLabel);
            ClearLabel(_frameLabel);
            ClearLabel(_qualityLabel);
            ClearLabel(_requestsLabel);
            ClearLabel(_completedLabel);
            ClearLabel(_activeSlotsLabel);
            ClearLabel(_latencyLabel);
            ClearLabel(_droppedLabel);
            ClearLabel(_failedLabel);
            ClearLabel(_maxStaleLabel);
            ClearLabel(_dispatchMicrosLabel);
            ClearLabel(_applyMicrosLabel);
            ClearLabel(_smoothingLabel);
            ClearLabel(_entityLabel);
            ClearLabel(_heightLabel);
            for (int i = 0; i < _graphBars.Length; i++)
            {
                if (_graphBars[i] == null)
                    continue;

                _graphBars[i].style.width = 4;
                _graphBars[i].style.opacity = 0.12f;
            }
        }

        private void PushTuning()
        {
            if (_updatingControls)
                return;
            if (!AsyncBuoyancyReadbackRuntime.TryGetActiveRuntimeInstance(out AsyncBuoyancyReadbackRuntime runtime))
                return;

            runtime.ApplyEditorTuning(
                _sampleCapSlider.value,
                _decaySlider.value,
                _qualityOverrideToggle.value,
                _qualitySlider.value);
        }

        private void UpdateWaterfall(NativeArray<ReadbackTelemetryEntry>.ReadOnly telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int count = math.min(GraphBins, telemetry.Length);
            for (int i = 0; i < count; i++)
            {
                int index = (cursor - count + i + telemetry.Length) % telemetry.Length;
                ReadbackTelemetryEntry entry = telemetry[index];
                int latency = math.clamp(entry.ReadbackLatencyFrames, 0, 12);
                float width = math.lerp(4f, 220f, latency / 12f);
                _graphBars[i].style.width = width;
                _graphBars[i].style.opacity = entry.FrameIndex == 0u ? 0.12f : 1f;
                _graphBars[i].style.backgroundColor = latency > 4
                    ? new Color(0.9f, 0.18f, 0.12f, 0.9f)
                    : new Color(0.15f, 0.75f, 0.95f, 0.8f);
            }
        }

        private static VisualElement Section(string title)
        {
            Label label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 3;
            return label;
        }

        private static VisualElement Row(string name, out Label value)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            Label key = new Label(name);
            key.style.width = 170;
            value = new Label("-");
            value.style.flexGrow = 1;
            row.Add(key);
            row.Add(value);
            return row;
        }

        private static void ClearLabel(Label label)
        {
            SetLabel(label, "-");
        }

        private static void SetLabel(Label label, string value)
        {
            if (label != null && label.text != value)
                label.text = value;
        }

    }
}
#endif
