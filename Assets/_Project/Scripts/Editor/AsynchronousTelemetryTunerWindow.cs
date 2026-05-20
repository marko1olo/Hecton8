#if UNITY_EDITOR
using System.Text;
using Hecton8.Core.Diagnostics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class AsynchronousTelemetryEditorLayoutGuard
    {
        static AsynchronousTelemetryEditorLayoutGuard()
        {
            AnalyticsLayout.ValidateOrThrow();
        }
    }

    public sealed class AsynchronousTelemetryTunerWindow : EditorWindow
    {
        private Label _status;
        private Label _telemetry;
        private Slider _heatmapSeconds;
        private IntegerField _batchBytes;
        private IntegerField _timeoutMs;
        private Toggle _mockEvents;
        private Toggle _kccHeatmap;
        private Toggle _networkEnabled;
        private double _nextRefreshTime;
        // COLD ALLOC: StringBuilder[256] - editor telemetry facade text buffer - owner: AsynchronousTelemetryTunerWindow
        private readonly StringBuilder _telemetryBuilder = new StringBuilder(256);

        [MenuItem("Hecton8/Diagnostics/Asynchronous Telemetry")]
        private static void Open()
        {
            GetWindow<AsynchronousTelemetryTunerWindow>("H8 Analytics");
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
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _status = new Label("SHINOBU_160 analytics exporter");
            rootVisualElement.Add(_status);

            _mockEvents = AddToggle("Mock Events", AnalyticsExporterFlags.MockEvents);
            _kccHeatmap = AddToggle("KCC Heatmap", AnalyticsExporterFlags.HeatmapKcc);
            _networkEnabled = AddToggle("Network Enabled", AnalyticsExporterFlags.NetworkEnabled);

            _heatmapSeconds = new Slider("Heatmap Seconds", 0.5f, 60f);
            _heatmapSeconds.RegisterValueChangedCallback(evt => MutateFloat(evt.newValue, static (ref AnalyticsTuningDTO tuning, float value) => tuning.HeatmapSampleSeconds = value));
            rootVisualElement.Add(_heatmapSeconds);

            _batchBytes = new IntegerField("Batch Bytes");
            _batchBytes.RegisterValueChangedCallback(evt => MutateInt(evt.newValue, static (ref AnalyticsTuningDTO tuning, int value) => tuning.BatchFlushThresholdBytes = value));
            rootVisualElement.Add(_batchBytes);

            _timeoutMs = new IntegerField("Network Timeout Ms");
            _timeoutMs.RegisterValueChangedCallback(evt => MutateInt(evt.newValue, static (ref AnalyticsTuningDTO tuning, int value) => tuning.NetworkTimeoutMs = value));
            rootVisualElement.Add(_timeoutMs);

            _telemetry = new Label("inactive");
            rootVisualElement.Add(_telemetry);

            RefreshControls();
            RefreshTelemetry();
        }

        private Toggle AddToggle(string label, int flag)
        {
            Toggle toggle = new Toggle(label);
            toggle.RegisterValueChangedCallback(evt => MutateFlag(flag, evt.newValue));
            rootVisualElement.Add(toggle);
            return toggle;
        }

        private void RefreshControls()
        {
            if (_status == null)
                return;

            if (!AsynchronousTelemetryExporter.TryReadTuning(out AnalyticsTuningDTO tuning))
            {
                _status.text = "runtime inactive";
                return;
            }

            _status.text = "runtime active";
            _mockEvents.SetValueWithoutNotify((tuning.Flags & AnalyticsExporterFlags.MockEvents) != 0);
            _kccHeatmap.SetValueWithoutNotify((tuning.Flags & AnalyticsExporterFlags.HeatmapKcc) != 0);
            _networkEnabled.SetValueWithoutNotify((tuning.Flags & AnalyticsExporterFlags.NetworkEnabled) != 0);
            _heatmapSeconds.SetValueWithoutNotify(tuning.HeatmapSampleSeconds);
            _batchBytes.SetValueWithoutNotify(tuning.BatchFlushThresholdBytes);
            _timeoutMs.SetValueWithoutNotify(tuning.NetworkTimeoutMs);
        }

        private void RefreshTelemetry()
        {
            if (_status == null || _telemetry == null)
                return;

            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + 0.5d;
            RefreshControls();

            if (!AsynchronousTelemetryExporter.TryReadCounters(out AnalyticsCountersDTO counters) ||
                !AsynchronousTelemetryExporter.TryReadLatestTelemetry(out AnalyticsExporterTelemetryEntry entry))
            {
                _telemetry.text = "telemetry unavailable";
                return;
            }

            _telemetryBuilder.Length = 0;
            _telemetryBuilder.Append("enqueued=");
            _telemetryBuilder.Append(counters.EnqueuedEvents);
            _telemetryBuilder.Append(" drained=");
            _telemetryBuilder.Append(counters.DrainedEvents);
            _telemetryBuilder.Append(" dropped=");
            _telemetryBuilder.Append(counters.DroppedEvents);
            _telemetryBuilder.Append(" disk=");
            _telemetryBuilder.Append(entry.DiskFallbackEvents);
            _telemetryBuilder.Append(" sent=");
            _telemetryBuilder.Append(entry.SentEvents);
            _telemetryBuilder.Append(" backlog=");
            _telemetryBuilder.Append(entry.BacklogEvents);
            _telemetryBuilder.Append(" response=");
            _telemetryBuilder.Append(entry.LastResponseCode);
            _telemetryBuilder.Append(" ratio_milli=");
            _telemetryBuilder.Append(entry.CompressionRatioMilli);
            _telemetryBuilder.Append(" vault_bytes=");
            _telemetryBuilder.Append(entry.VaultBytes);
            _telemetry.text = _telemetryBuilder.ToString();
        }

        private static void MutateFlag(int flag, bool enabled)
        {
            if (!AsynchronousTelemetryExporter.TryReadTuning(out AnalyticsTuningDTO current))
                return;

            if (enabled)
                current.Flags |= flag;
            else
                current.Flags &= ~flag;
            AsynchronousTelemetryExporter.TryWriteTuning(in current);
        }

        private static void MutateFloat(float value, FloatMutator mutator)
        {
            if (!AsynchronousTelemetryExporter.TryReadTuning(out AnalyticsTuningDTO tuning))
                return;

            mutator(ref tuning, value);
            AsynchronousTelemetryExporter.TryWriteTuning(in tuning);
        }

        private static void MutateInt(int value, IntMutator mutator)
        {
            if (!AsynchronousTelemetryExporter.TryReadTuning(out AnalyticsTuningDTO tuning))
                return;

            mutator(ref tuning, value);
            AsynchronousTelemetryExporter.TryWriteTuning(in tuning);
        }

        private delegate void FloatMutator(ref AnalyticsTuningDTO tuning, float value);

        private delegate void IntMutator(ref AnalyticsTuningDTO tuning, int value);
    }
}
#endif
