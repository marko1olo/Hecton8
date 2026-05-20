#if UNITY_EDITOR
using System.IO;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class SignalTrafficMonitorWindow : EditorWindow
    {
        private const int TelemetryCapacity = 256;
        private const int FrameCapacity = 300;
        private const int StressWarningCount = 1000;
        private const float HistogramWidth = 180f;

        private NativeArray<SignalLaneTelemetry> _telemetry;
        private NativeArray<SignalTelemetryFrame> _frames;
        private VisualElement _frameRows;
        private VisualElement _laneRows;
        private EnumField _injectKindField;
        private FloatField _xField;
        private FloatField _yField;
        private FloatField _zField;
        private FloatField _magnitudeField;
        private IntegerField _entityIdField;
        private TextField _surfaceField;

        private enum InjectKind
        {
            MockDamage,
            MockFootstep,
            CombatDamage,
            AcousticBurst
        }

        [MenuItem("Hecton8/Diagnostics/Signal Traffic Monitor")]
        public static void Open()
        {
            GetWindow<SignalTrafficMonitorWindow>("Signal Traffic");
        }

        private void OnEnable()
        {
            EnsureBuffers();
            EditorApplication.update -= RefreshUi;
            EditorApplication.update += RefreshUi;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshUi;
            if (_telemetry.IsCreated)
                _telemetry.Dispose();
            if (_frames.IsCreated)
                _frames.Dispose();
        }

        public void CreateGUI()
        {
            EnsureBuffers();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label title = new Label("Signal Traffic Monitor");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            BuildInjectionPanel(root);
            BuildTelemetryPanel(root);
            RefreshUi();
        }

        private void EnsureBuffers()
        {
            if (!_telemetry.IsCreated)
                _telemetry = new NativeArray<SignalLaneTelemetry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_frames.IsCreated)
                _frames = new NativeArray<SignalTelemetryFrame>(FrameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void BuildInjectionPanel(VisualElement root)
        {
            VisualElement panel = new VisualElement();
            panel.style.marginTop = 8f;
            panel.style.marginBottom = 8f;
            root.Add(panel);

            Label header = new Label("Injection");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(header);

            _injectKindField = new EnumField("Lane", InjectKind.CombatDamage);
            _xField = new FloatField("AUP X");
            _yField = new FloatField("AUP Y");
            _zField = new FloatField("AUP Z");
            _magnitudeField = new FloatField("Magnitude") { value = 1f };
            _entityIdField = new IntegerField("Entity Id") { value = 1 };
            _surfaceField = new TextField("Surface") { value = "steel" };
            panel.Add(_injectKindField);
            panel.Add(_xField);
            panel.Add(_yField);
            panel.Add(_zField);
            panel.Add(_magnitudeField);
            panel.Add(_entityIdField);
            panel.Add(_surfaceField);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 4f;
            panel.Add(buttons);

            Button push = new Button(PushSelectedSignal) { text = "Push Signal" };
            Button loadCsv = new Button(LoadSignalTuningCsv) { text = "Load Tuning CSV" };
            Button dump = new Button(() => SignalTelemetryRingBuffer.DumpToDisk()) { text = "Dump Black Box" };
            buttons.Add(push);
            buttons.Add(loadCsv);
            buttons.Add(dump);
        }

        private void BuildTelemetryPanel(VisualElement root)
        {
            Label frameHeader = new Label("Vault Ring: pushed / coalesced / dropped");
            frameHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(frameHeader);

            _frameRows = new VisualElement();
            _frameRows.style.marginBottom = 8f;
            root.Add(_frameRows);

            Label laneHeader = new Label("Lane Snapshot");
            laneHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(laneHeader);

            _laneRows = new VisualElement();
            root.Add(_laneRows);
        }

        private void RefreshUi()
        {
            if (_frameRows == null || _laneRows == null)
                return;

            EnsureBuffers();
            int frameCount = SignalTelemetryRingBuffer.CopyFrames(_frames);
            int laneCount = SignalBusRegistry.CopyTelemetry(_telemetry);
            DrawFrameRows(frameCount);
            DrawLaneRows(laneCount);
        }

        private void DrawFrameRows(int frameCount)
        {
            _frameRows.Clear();
            int drawn = 0;
            for (int i = 0; i < frameCount && drawn < 48; i++)
            {
                SignalTelemetryFrame frame = _frames[i];
                if (frame.TotalPushedSignals == 0u &&
                    frame.CoalescedSignals == 0u &&
                    frame.DroppedSignals == 0u &&
                    frame.CorruptedSignals == 0u)
                {
                    continue;
                }

                uint peak = frame.TotalPushedSignals;
                if (frame.CoalescedSignals > peak)
                    peak = frame.CoalescedSignals;
                if (frame.DroppedSignals > peak)
                    peak = frame.DroppedSignals;
                if (peak == 0u)
                    peak = 1u;
                VisualElement row = BuildRow(frame.DroppedSignals > (frame.TotalPushedSignals >> 1));
                row.Add(BuildLabel(frame.Frame.ToString(), 70f));
                row.Add(BuildBar(frame.TotalPushedSignals, peak, new Color(0.18f, 0.48f, 0.84f, 0.9f)));
                row.Add(BuildBar(frame.CoalescedSignals, peak, new Color(0.82f, 0.64f, 0.12f, 0.9f)));
                row.Add(BuildBar(frame.DroppedSignals, peak, new Color(0.82f, 0.12f, 0.08f, 0.9f)));
                row.Add(BuildLabel("Q" + frame.GlobalQualityMilli.ToString(), 54f));
                _frameRows.Add(row);
                drawn++;
            }
        }

        private void DrawLaneRows(int laneCount)
        {
            _laneRows.Clear();
            VisualElement header = BuildRow(false);
            header.Add(BuildLabel("Hash", 82f));
            header.Add(BuildLabel("Queued", 64f));
            header.Add(BuildLabel("Frame", 56f));
            header.Add(BuildLabel("Coal", 48f));
            header.Add(BuildLabel("Drop", 48f));
            header.Add(BuildLabel("Load", HistogramWidth));
            _laneRows.Add(header);

            for (int i = 0; i < laneCount; i++)
            {
                SignalLaneTelemetry telemetry = _telemetry[i];
                bool warning = telemetry.QueuedBeforeFlush > StressWarningCount ||
                               telemetry.SnapshotCount > StressWarningCount ||
                               telemetry.DroppedCount > 0;
                uint peak = (uint)math.max(1, math.max(telemetry.QueuedBeforeFlush, math.max(telemetry.SnapshotCount, telemetry.DroppedCount)));
                VisualElement row = BuildRow(warning);
                row.Add(BuildLabel(telemetry.LaneHash.ToString("X8"), 82f));
                row.Add(BuildLabel(telemetry.QueuedBeforeFlush.ToString(), 64f));
                row.Add(BuildLabel(telemetry.SnapshotCount.ToString(), 56f));
                row.Add(BuildLabel(telemetry.CoalescedCount.ToString(), 48f));
                row.Add(BuildLabel(telemetry.DroppedCount.ToString(), 48f));
                row.Add(BuildBar((uint)math.max(telemetry.SnapshotCount, telemetry.DroppedCount), peak, warning ? new Color(0.82f, 0.12f, 0.08f, 0.9f) : new Color(0.1f, 0.55f, 0.25f, 0.9f)));
                _laneRows.Add(row);
            }
        }

        private static VisualElement BuildRow(bool warning)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 20f;
            row.style.marginBottom = 2f;
            if (warning)
                row.style.backgroundColor = new Color(0.25f, 0.04f, 0.03f, 0.5f);
            return row;
        }

        private static Label BuildLabel(string text, float width)
        {
            Label label = new Label(text);
            label.style.width = width;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        private static VisualElement BuildBar(uint value, uint peak, Color color)
        {
            VisualElement container = new VisualElement();
            container.style.width = HistogramWidth;
            container.style.height = 10f;
            container.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.65f);

            VisualElement fill = new VisualElement();
            fill.style.height = 10f;
            fill.style.width = math.max(2f, HistogramWidth * math.saturate(value / (float)math.max(1u, peak)));
            fill.style.backgroundColor = color;
            container.Add(fill);
            return container;
        }

        private void LoadSignalTuningCsv()
        {
            string csvPath = Path.Combine(Application.dataPath, "StreamingAssets", "signal_tuning_profiles.csv");
            SignalTuningCsvHotSwap.TryLoad(csvPath);
            RefreshUi();
        }

        private void PushSelectedSignal()
        {
            double3 aup = new double3(_xField.value, _yField.value, _zField.value);
            float magnitude = math.max(0f, _magnitudeField.value);
            uint entityId = (uint)math.max(1, _entityIdField.value);
            switch ((InjectKind)_injectKindField.value)
            {
                case InjectKind.MockDamage:
                {
                    SignalWardenMockDamageSignal signal = default;
                    signal.Aup = aup;
                    signal.Normal = new float3(0f, 1f, 0f);
                    signal.Damage = magnitude;
                    signal.EntityId = entityId;
                    signal.Flags = 1;
                    SignalBus<SignalWardenMockDamageSignal>.TryPush(in signal);
                    break;
                }
                case InjectKind.MockFootstep:
                {
                    MockPlayerFootstepSignal signal = default;
                    signal.Aup = aup;
                    signal.Normal = new float3(0f, 1f, 0f);
                    signal.Intensity01 = math.saturate(magnitude);
                    signal.EntityId = entityId;
                    signal.Frame = unchecked((uint)Time.frameCount);
                    signal.SurfaceName.Append(_surfaceField.value);
                    signal.Flags = 1;
                    SignalBus<MockPlayerFootstepSignal>.TryPush(in signal);
                    break;
                }
                case InjectKind.CombatDamage:
                {
                    CombatDamageSignal signal = default;
                    signal.ImpactAup = aup;
                    signal.Direction = new float3(0f, 1f, 0f);
                    signal.Magnitude = magnitude;
                    signal.TargetHash = entityId;
                    signal.Frame = unchecked((uint)Time.frameCount);
                    signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
                    SignalBus<CombatDamageSignal>.TryPush(in signal);
                    break;
                }
                case InjectKind.AcousticBurst:
                {
                    float3 origin = new float3(_xField.value, _yField.value, _zField.value);
                    MockSignalGenerators.InjectAcousticBurst(
                        in origin,
                        32,
                        entityId,
                        math.max(1f, magnitude),
                        math.saturate(magnitude),
                        unchecked((uint)Time.frameCount));
                    break;
                }
            }

            RefreshUi();
        }
    }
}
#endif
