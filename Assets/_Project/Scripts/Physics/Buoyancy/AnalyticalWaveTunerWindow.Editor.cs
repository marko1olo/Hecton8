#if UNITY_EDITOR
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics
{
    public sealed class AnalyticalWaveTunerWindow : EditorWindow
    {
        private Slider _quality;
        private Slider _amplitude;
        private Slider _storm;
        private Slider _windDirection;
        private Slider _windSpeed;
        private Slider _coarseThreshold;
        private IntegerField _requestCount;
        private IntegerField _maxOctaves;
        private IntegerField _macroGridResolution;
        private FloatField _macroCellSize;
        private FloatField _dumpThreshold;
        private Label _statusLabel;
        private WaveTelemetryGraph _graph;
        private readonly StringBuilder _builder = new StringBuilder(256);
        private IDataVault _cachedVault;
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Physics/Analytical Gerstner Wave Tuner")]
        public static void Open()
        {
            GetWindow<AnalyticalWaveTunerWindow>("Analytical Waves");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            rootVisualElement.Add(row);
            row.Add(new Button(ReadFromVault) { text = "Resolve Vault" });
            row.Add(new Button(WriteToVault) { text = "Apply" });

            _quality = AddSlider("Global Quality Weight", 0f, 1f);
            _amplitude = AddSlider("Amplitude Multiplier", 0f, 0.16f);
            _storm = AddSlider("Storm Weight", 0f, 1f);
            _windDirection = AddSlider("Wind Direction Rad", -math.PI, math.PI);
            _windSpeed = AddSlider("Wind Speed MPS", 0.1f, 36f);
            _coarseThreshold = AddSlider("Coarse Priority Threshold", 0f, 255f);

            _requestCount = AddInteger("Active Requests");
            _maxOctaves = AddInteger("Max Octaves");
            _macroGridResolution = AddInteger("Macro Grid Resolution");
            _macroCellSize = AddFloat("Macro Cell Size M");
            _dumpThreshold = AddFloat("Dump Threshold Micros");

            _graph = new WaveTelemetryGraph();
            _graph.style.height = 128;
            _graph.style.marginTop = 8;
            rootVisualElement.Add(_graph);

            _statusLabel = new Label("Vault not resolved.");
            _statusLabel.style.marginTop = 8;
            rootVisualElement.Add(_statusLabel);

            RegisterCallbacks();
            ReadFromVault();
        }

        private Slider AddSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.style.marginBottom = 4;
            rootVisualElement.Add(slider);
            return slider;
        }

        private IntegerField AddInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.style.marginBottom = 4;
            rootVisualElement.Add(field);
            return field;
        }

        private FloatField AddFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.style.marginBottom = 4;
            rootVisualElement.Add(field);
            return field;
        }

        private void RegisterCallbacks()
        {
            _quality.RegisterValueChangedCallback(_ => WriteToVault());
            _amplitude.RegisterValueChangedCallback(_ => WriteToVault());
            _storm.RegisterValueChangedCallback(_ => WriteToVault());
            _windDirection.RegisterValueChangedCallback(_ => WriteToVault());
            _windSpeed.RegisterValueChangedCallback(_ => WriteToVault());
            _coarseThreshold.RegisterValueChangedCallback(_ => WriteToVault());
            _requestCount.RegisterValueChangedCallback(_ => WriteToVault());
            _maxOctaves.RegisterValueChangedCallback(_ => WriteToVault());
            _macroGridResolution.RegisterValueChangedCallback(_ => WriteToVault());
            _macroCellSize.RegisterValueChangedCallback(_ => WriteToVault());
            _dumpThreshold.RegisterValueChangedCallback(_ => WriteToVault());
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + 0.1d;
            UpdateTelemetryLabel();
            _graph?.MarkDirtyRepaint();
        }

        private void ReadFromVault()
        {
            IDataVault vault = ResolveVaultCold();
            GerstnerWaveTuningDTO tuning = TryReadBuffer(
                    vault,
                    AnalyticalGerstnerWaveBufferIds.Tuning,
                    out NativeArray<GerstnerWaveTuningDTO> tuningBuffer)
                ? tuningBuffer[0]
                : GerstnerWaveTuningDTO.Default();

            if (tuning.Flags == 0u)
                tuning = GerstnerWaveTuningDTO.Default();

            SetSlider(_quality, math.saturate(tuning.GlobalQualityWeight));
            SetSlider(_amplitude, math.max(0f, tuning.WaveAmplitudeMultiplier));
            SetSlider(_storm, math.saturate(tuning.StormWeight01));
            SetSlider(_windDirection, math.clamp(tuning.WindDirectionRadians, -math.PI, math.PI));
            SetSlider(_windSpeed, math.max(0.1f, tuning.WindSpeedMetersPerSecond));
            SetSlider(_coarseThreshold, math.clamp(tuning.CoarsePriorityThreshold, 0f, 255f));
            _requestCount?.SetValueWithoutNotify(math.clamp(tuning.ActiveRequestCount <= 0 ? AnalyticalGerstnerWaveConstants.SampleCapacity : tuning.ActiveRequestCount, 1, AnalyticalGerstnerWaveConstants.SampleCapacity));
            _maxOctaves?.SetValueWithoutNotify(math.clamp(tuning.MaxOctaveLimit <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.MaxOctaveLimit, 1, AnalyticalGerstnerWaveConstants.MaxOctaves));
            _macroGridResolution?.SetValueWithoutNotify(math.clamp(tuning.MacroGridResolution <= 0 ? 32 : tuning.MacroGridResolution, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution));
            _macroCellSize?.SetValueWithoutNotify(math.max(0.25f, tuning.MacroGridCellSizeMeters <= 0f ? AnalyticalGerstnerWaveConstants.DefaultMacroGridCellSizeMeters : tuning.MacroGridCellSizeMeters));
            _dumpThreshold?.SetValueWithoutNotify(math.max(1f, tuning.MaxSolverMicrosBeforeDump <= 0f ? AnalyticalGerstnerWaveConstants.DefaultDumpThresholdMicros : tuning.MaxSolverMicrosBeforeDump));
            UpdateTelemetryLabel();
        }

        private void WriteToVault()
        {
            IDataVault vault = ResolveVaultCold();
            if (!TryAcquireEditorWriteView(
                    vault,
                    AnalyticalGerstnerWaveBufferIds.Tuning,
                    1,
                    out VaultGenerationHandle<GerstnerWaveTuningDTO> handle,
                    out NativeArray<GerstnerWaveTuningDTO> tuningBuffer))
            {
                return;
            }

            try
            {
                GerstnerWaveTuningDTO tuning = tuningBuffer[0].Flags == 0u ? GerstnerWaveTuningDTO.Default() : tuningBuffer[0];
                tuning.GlobalQualityWeight = math.saturate(_quality.value);
                tuning.WaveAmplitudeMultiplier = math.max(0f, _amplitude.value);
                tuning.StormWeight01 = math.saturate(_storm.value);
                tuning.WindDirectionRadians = math.clamp(_windDirection.value, -math.PI, math.PI);
                tuning.WindSpeedMetersPerSecond = math.max(0.1f, _windSpeed.value);
                tuning.CoarsePriorityThreshold = math.clamp(_coarseThreshold.value, 0f, 255f);
                tuning.ActiveRequestCount = math.clamp(_requestCount.value, 1, AnalyticalGerstnerWaveConstants.SampleCapacity);
                tuning.MaxOctaveLimit = math.clamp(_maxOctaves.value, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
                tuning.TotalOctaves = math.clamp(tuning.TotalOctaves <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.TotalOctaves, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
                tuning.ActiveOctaves = AnalyticalGerstnerWaveMath.ResolveActiveOctaves(in tuning);
                tuning.MacroGridResolution = math.clamp(_macroGridResolution.value, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
                tuning.MacroGridCellSizeMeters = math.max(0.25f, _macroCellSize.value);
                tuning.MaxSolverMicrosBeforeDump = math.max(1f, _dumpThreshold.value);
                tuning.Flags |= AnalyticalGerstnerWaveConstants.FlagActive | AnalyticalGerstnerWaveConstants.FlagDearLie;
                tuningBuffer[0] = tuning;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void UpdateTelemetryLabel()
        {
            if (_statusLabel == null)
                return;

            IDataVault vault = _cachedVault;
            if (!TryReadBuffer(vault, AnalyticalGerstnerWaveBufferIds.TelemetryRing, out NativeArray<WaveMathTelemetryEntry> telemetry) ||
                !TryReadBuffer(vault, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, out NativeArray<int> cursor) ||
                telemetry.Length <= 0)
            {
                _statusLabel.text = "Analytical wave telemetry is not allocated.";
                return;
            }

            int slot = math.clamp((cursor[0] - 1 + telemetry.Length) % telemetry.Length, 0, telemetry.Length - 1);
            WaveMathTelemetryEntry entry = telemetry[slot];
            _builder.Length = 0;
            _builder.Append("Frame ");
            _builder.Append(entry.FrameIndex);
            _builder.Append(" | requests ");
            _builder.Append(entry.RequestCount);
            _builder.Append(" | evaluated ");
            _builder.Append(entry.EvaluatedCoordinates);
            _builder.Append(" | octaves ");
            _builder.Append(entry.ActiveOctaves);
            _builder.Append(" | coarse ");
            _builder.Append(entry.CoarseGridSamples);
            _builder.Append(" | us ");
            AppendFixed2(_builder, entry.BurstMicros);
            _builder.Append(" | nonfinite ");
            _builder.Append(entry.NonFiniteCount);
            _statusLabel.text = _builder.ToString();
        }

        private IDataVault ResolveVaultCold()
        {
            _cachedVault = GlobalRegistry.DataVault;
            if (_graph != null)
                _graph.Vault = _cachedVault;

            return _cachedVault;
        }

        private static bool TryReadBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.ActiveBurstLockMask != 0u)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Physics, NativeArrayOptions.ClearMemory);
            }

            if (vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
            {
                bool releaseOnFailure = true;
                try
                {
                    if (buffer.IsCreated && buffer.Length >= requiredLength)
                    {
                        releaseOnFailure = false;
                        return true;
                    }
                }
                finally
                {
                    if (releaseOnFailure)
                        vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
                }
            }

            buffer = default;
            return false;
        }

        private static void SetSlider(Slider slider, float value)
        {
            slider?.SetValueWithoutNotify(math.select(0f, value, math.isfinite(value)));
        }

        private static void AppendFixed2(StringBuilder builder, float value)
        {
            if (!math.isfinite(value))
            {
                builder.Append("NaN");
                return;
            }

            int scaled = (int)math.round(math.max(0f, value) * 100f);
            int whole = scaled / 100;
            int fraction = scaled - whole * 100;
            builder.Append(whole);
            builder.Append('.');
            if (fraction < 10)
                builder.Append('0');
            builder.Append(fraction);
        }

        private sealed class WaveTelemetryGraph : VisualElement
        {
            public IDataVault Vault;

            public WaveTelemetryGraph()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                IDataVault vault = Vault;
                if (!TryReadBuffer(vault, AnalyticalGerstnerWaveBufferIds.TelemetryRing, out NativeArray<WaveMathTelemetryEntry> telemetry) ||
                    !TryReadBuffer(vault, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, out NativeArray<int> cursor) ||
                    telemetry.Length <= 0)
                {
                    painter.strokeColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.Stroke();
                    return;
                }

                int start = math.clamp(cursor[0], 0, telemetry.Length - 1);
                float maxMicros = 1f;
                int maxEvaluated = 1;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    WaveMathTelemetryEntry entry = telemetry[(start + i) % telemetry.Length];
                    maxMicros = math.max(maxMicros, entry.BurstMicros);
                    maxEvaluated = math.max(maxEvaluated, entry.EvaluatedCoordinates);
                }

                DrawSeries(painter, rect, telemetry, start, maxMicros, 0, new Color(0.05f, 0.9f, 0.9f, 1f));
                DrawSeries(painter, rect, telemetry, start, maxEvaluated, 1, new Color(1f, 0.7f, 0.1f, 1f));
            }

            private static void DrawSeries(
                Painter2D painter,
                Rect rect,
                NativeArray<WaveMathTelemetryEntry> telemetry,
                int start,
                float maxValue,
                int series,
                Color color)
            {
                painter.lineWidth = 1.5f;
                painter.strokeColor = color;
                painter.BeginPath();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    WaveMathTelemetryEntry entry = telemetry[(start + i) % telemetry.Length];
                    float value = series == 0 ? entry.BurstMicros : entry.EvaluatedCoordinates;
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetry.Length - 1f));
                    float y = rect.yMax - rect.height * math.saturate(value / math.max(0.001f, maxValue));
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
