#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only master telemetry facade over existing Core-owned rings.
    /// </summary>
    public sealed class ChroniclerDiagnosticHeatmapWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.25d;
        private const int SignalLaneCapacity = 256;
        private const int SignalFrameCapacity = 300;
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxEventCapacity = 64;
        private const int StripSampleCapacity = 96;
        private const int TopLaneCount = 8;
        private const int EventRowCount = 8;
        private const float FrameBudgetMs = 16.6667f;
        private const float ContentionCommittedSignalScale = 4096f;
        private const string NativeMemoryOwner = nameof(ChroniclerDiagnosticHeatmapWindow);
        private const string SignalLanesLabel = "signalLanes";
        private const string SignalFramesLabel = "signalFrames";

        private readonly float[] _phaseMs = new float[4];
        private readonly uint[] _bucketLoads = new uint[64];
        private readonly float[] _memoryPressure = new float[StripSampleCapacity];
        private readonly float[] _signalPressure = new float[StripSampleCapacity];
        private readonly float[] _fencePressure = new float[StripSampleCapacity];
        private readonly float[] _contentionPressure = new float[StripSampleCapacity];
        private readonly uint[] _memoryFaults = new uint[StripSampleCapacity];
        private readonly uint[] _topLaneHashes = new uint[TopLaneCount];
        private readonly int[] _topLaneScores = new int[TopLaneCount];
        private readonly GlobalTelemetryBus.BlackboxEditorFrame[] _blackboxFrames =
            new GlobalTelemetryBus.BlackboxEditorFrame[BlackboxFrameCapacity];
        private readonly TelemetryEventDTO[] _blackboxEvents = new TelemetryEventDTO[BlackboxEventCapacity];
        private readonly Label[] _topLaneLabels = new Label[TopLaneCount];
        private readonly Label[] _eventLabels = new Label[EventRowCount];

        private NativeArray<SignalLaneTelemetry> _signalLanes;
        private NativeArray<SignalTelemetryFrame> _signalFrames;
        private int _signalLanesSentinelId;
        private int _signalFramesSentinelId;
        private HeatmapStripElement _memoryStrip;
        private HeatmapStripElement _signalStrip;
        private HeatmapStripElement _fenceStrip;
        private HeatmapStripElement _contentionStrip;
        private Label _summaryLabel;
        private Label _memoryLabel;
        private Label _signalLabel;
        private Label _dispatcherLabel;
        private Label _contentionLabel;
        private Label _blackboxLabel;
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Diagnostics/Chronicler Diagnostic Heatmap")]
        public static void Open()
        {
            ChroniclerDiagnosticHeatmapWindow window = GetWindow<ChroniclerDiagnosticHeatmapWindow>();
            window.titleContent = new GUIContent("Chronicler Heatmap");
            window.minSize = new Vector2(760f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureBuffers();
            EditorApplication.update -= TickRefresh;
            EditorApplication.update += TickRefresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickRefresh;
            DisposeTrackedBuffer(ref _signalLanes, ref _signalLanesSentinelId);
            DisposeTrackedBuffer(ref _signalFrames, ref _signalFramesSentinelId);
        }

        private static void DisposeTrackedBuffer<T>(ref NativeArray<T> buffer, ref int sentinelId)
            where T : struct
        {
            System.Exception cleanupException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (System.Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (buffer.IsCreated)
            {
                try
                {
                    buffer.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    buffer = default;
                }
            }
            else
            {
                buffer = default;
            }

            if (cleanupException != null)
                throw cleanupException;
        }

        public void CreateGUI()
        {
            EnsureBuffers();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;
            root.style.backgroundColor = new Color(0.055f, 0.062f, 0.068f, 1f);

            _summaryLabel = CreateHeader("CHRONICLER CORE TELEMETRY");
            root.Add(_summaryLabel);

            _memoryLabel = CreateSmall("Vault unavailable");
            root.Add(BuildSection("MEMORY / VAULT", _memoryLabel, out _memoryStrip));

            _signalLabel = CreateSmall("Signal telemetry unavailable");
            root.Add(BuildSection("SIGNAL BUS", _signalLabel, out _signalStrip));

            _dispatcherLabel = CreateSmall("Dispatcher inactive");
            root.Add(BuildSection("DISPATCHER / PHYSICS / AUDIO / NETCODE FENCES", _dispatcherLabel, out _fenceStrip));

            _contentionLabel = CreateSmall("Signal contention telemetry unavailable");
            root.Add(BuildSection("MPSC CONTENTION", _contentionLabel, out _contentionStrip));

            root.Add(BuildTopLanePanel());

            _blackboxLabel = CreateSmall("Blackbox unavailable");
            root.Add(BuildBlackboxPanel());

            RefreshSnapshot();
            ApplySnapshotToUi();
        }

        private void EnsureBuffers()
        {
            if (!_signalLanes.IsCreated)
            {
                _signalLanes = new NativeArray<SignalLaneTelemetry>(SignalLaneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                try
                {
                    _signalLanesSentinelId = NativeMemorySentinel.RegisterNativeArray(
                        _signalLanes,
                        NativeMemoryOwner,
                        SignalLanesLabel,
                        NativeAllocationLifetime.Session);
                    if (_signalLanesSentinelId <= 0)
                        throw new System.InvalidOperationException($"Native memory sentinel registration failed for {SignalLanesLabel}.");
                }
                catch
                {
                    _signalLanes.Dispose();
                    _signalLanes = default;
                    _signalLanesSentinelId = 0;
                    throw;
                }
            }
            if (!_signalFrames.IsCreated)
            {
                _signalFrames = new NativeArray<SignalTelemetryFrame>(SignalFrameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                try
                {
                    _signalFramesSentinelId = NativeMemorySentinel.RegisterNativeArray(
                        _signalFrames,
                        NativeMemoryOwner,
                        SignalFramesLabel,
                        NativeAllocationLifetime.Session);
                    if (_signalFramesSentinelId <= 0)
                        throw new System.InvalidOperationException($"Native memory sentinel registration failed for {SignalFramesLabel}.");
                }
                catch
                {
                    _signalFrames.Dispose();
                    _signalFrames = default;
                    _signalFramesSentinelId = 0;
                    throw;
                }
            }
        }

        private void TickRefresh()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;
            RefreshSnapshot();
            ApplySnapshotToUi();
        }

        private VisualElement BuildSection(string title, Label label, out HeatmapStripElement strip)
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 10f;
            section.style.paddingLeft = 8f;
            section.style.paddingRight = 8f;
            section.style.paddingTop = 8f;
            section.style.paddingBottom = 8f;
            section.style.backgroundColor = new Color(0.09f, 0.10f, 0.105f, 1f);

            Label header = CreateHeader(title);
            section.Add(header);
            section.Add(label);

            strip = new HeatmapStripElement();
            strip.style.marginTop = 6f;
            strip.style.height = 56f;
            section.Add(strip);
            return section;
        }

        private VisualElement BuildTopLanePanel()
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 10f;
            section.style.paddingLeft = 8f;
            section.style.paddingRight = 8f;
            section.style.paddingTop = 8f;
            section.style.paddingBottom = 8f;
            section.style.backgroundColor = new Color(0.09f, 0.10f, 0.105f, 1f);

            section.Add(CreateHeader("HOTTEST SIGNAL LANES"));
            for (int i = 0; i < _topLaneLabels.Length; i++)
            {
                Label label = CreateSmall(string.Empty);
                label.style.height = 18f;
                label.style.display = DisplayStyle.None;
                _topLaneLabels[i] = label;
                section.Add(label);
            }

            return section;
        }

        private VisualElement BuildBlackboxPanel()
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 10f;
            section.style.paddingLeft = 8f;
            section.style.paddingRight = 8f;
            section.style.paddingTop = 8f;
            section.style.paddingBottom = 8f;
            section.style.backgroundColor = new Color(0.09f, 0.10f, 0.105f, 1f);

            section.Add(CreateHeader("BLACKBOX EVENTS"));
            section.Add(_blackboxLabel);
            for (int i = 0; i < _eventLabels.Length; i++)
            {
                Label label = CreateSmall(string.Empty);
                label.style.height = 18f;
                label.style.display = DisplayStyle.None;
                _eventLabels[i] = label;
                section.Add(label);
            }

            return section;
        }

        private void RefreshSnapshot()
        {
            EnsureBuffers();
            RefreshVaultStrip();
            RefreshSignalStrip();
            RefreshDispatcherStrip();
            RefreshContentionStrip();
        }

        private void RefreshVaultStrip()
        {
            ClearStrip(_memoryPressure);
            ClearFaultStrip(_memoryFaults);

            GlobalDataVault vault = GlobalRegistry.DataVault as GlobalDataVault;
            if (vault == null)
                return;

            for (int i = 0; i < StripSampleCapacity; i++)
            {
                int age = StripSampleCapacity - 1 - i;
                if (!vault.TryGetVaultTelemetrySnapshot(age, out VaultTelemetrySnapshot snapshot))
                    continue;

                float pressure = snapshot.ArenaBytes > 0L
                    ? math.saturate((float)((double)snapshot.AllocatedBytes / snapshot.ArenaBytes))
                    : 0f;
                _memoryPressure[i] = pressure;
                _memoryFaults[i] = snapshot.GenerationMismatchCount;
            }
        }

        private void RefreshSignalStrip()
        {
            ClearStrip(_signalPressure);
            int frameCount = SignalTelemetryRingBuffer.CopyFrames(_signalFrames);
            int start = math.max(0, frameCount - StripSampleCapacity);
            for (int i = start; i < frameCount; i++)
            {
                int sample = i - start;
                SignalTelemetryFrame frame = _signalFrames[i];
                uint peak = math.max(1u, math.max(frame.PeakSignalsPerFrame, math.max(frame.TotalPushedSignals, frame.DroppedSignals)));
                float pushed01 = math.saturate(frame.TotalPushedSignals / (float)peak);
                float dropped01 = math.saturate(frame.DroppedSignals / (float)peak);
                float corrupt01 = math.saturate(frame.CorruptedSignals / (float)peak);
                _signalPressure[sample] = math.saturate(pushed01 * 0.72f + dropped01 * 0.45f + corrupt01);
            }

            int laneCount = SignalBusRegistry.CopyTelemetry(_signalLanes);
            ResolveTopSignalLanes(laneCount);
        }

        private void RefreshDispatcherStrip()
        {
            if (SystemDispatcher.TryGetExecutionPipelineXRaySnapshot(_phaseMs, _bucketLoads, out DispatcherStateDTO state) &&
                SystemDispatcher.TryGetLatestFenceTelemetry(out DispatcherFenceTelemetryEntry fence))
            {
                ShiftLeft(_fencePressure);
                float phaseTotal = _phaseMs[0] + _phaseMs[1] + _phaseMs[2] + _phaseMs[3];
                float waitTotal = fence.SimulationWaitMs + fence.FixedWaitMs + fence.AupHardFenceMs;
                _fencePressure[StripSampleCapacity - 1] = math.saturate((phaseTotal + waitTotal) / FrameBudgetMs);
                return;
            }

            ClearStrip(_fencePressure);
        }

        private void RefreshContentionStrip()
        {
            ClearStrip(_contentionPressure);
            if (!SignalThreadLocalScratchpad.TryGetTelemetryReadOnly(
                    out NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly telemetry,
                    out int cursor) ||
                telemetry.Length <= 0)
            {
                return;
            }

            int columns = math.min(StripSampleCapacity, telemetry.Length);
            int start = cursor - columns;
            for (int i = 0; i < columns; i++)
            {
                int index = start + i;
                while (index < 0)
                    index += telemetry.Length;
                index %= telemetry.Length;

                SignalThreadContentionTelemetryEntry entry = telemetry[index];
                float written01 = math.saturate(entry.WrittenSignals / ContentionCommittedSignalScale);
                float drop01 = math.saturate((entry.DroppedSignals + entry.OverflowSignals + entry.NonFiniteSignals) / 128f);
                float commit01 = math.saturate(entry.CommitMicroseconds / 2000f);
                _contentionPressure[i] = math.saturate(math.max(written01, drop01) + commit01 * 0.25f);
            }
        }

        private void ResolveTopSignalLanes(int laneCount)
        {
            for (int i = 0; i < TopLaneCount; i++)
            {
                _topLaneHashes[i] = 0u;
                _topLaneScores[i] = 0;
            }

            int safeLaneCount = math.min(math.max(0, laneCount), _signalLanes.Length);
            for (int i = 0; i < safeLaneCount; i++)
            {
                SignalLaneTelemetry telemetry = _signalLanes[i];
                int score = math.max(
                    0,
                    telemetry.QueuedBeforeFlush +
                    telemetry.SnapshotCount +
                    telemetry.DroppedCount * 4 +
                    telemetry.CoalescedCount * 2);
                if (score <= 0)
                    continue;

                InsertTopLane(telemetry.LaneHash, score);
            }
        }

        private void InsertTopLane(uint laneHash, int score)
        {
            for (int i = 0; i < TopLaneCount; i++)
            {
                if (score <= _topLaneScores[i])
                    continue;

                for (int j = TopLaneCount - 1; j > i; j--)
                {
                    _topLaneScores[j] = _topLaneScores[j - 1];
                    _topLaneHashes[j] = _topLaneHashes[j - 1];
                }

                _topLaneScores[i] = score;
                _topLaneHashes[i] = laneHash;
                return;
            }
        }

        private void ApplySnapshotToUi()
        {
            if (_summaryLabel == null)
                return;

            float quality = ResolveGlobalQualityWeight01();
            _summaryLabel.text = "CHRONICLER CORE TELEMETRY | Q " + quality.ToString("0.000", CultureInfo.InvariantCulture);
            ApplyMemoryUi();
            ApplySignalUi();
            ApplyDispatcherUi();
            ApplyContentionUi();
            ApplyTopLaneUi();
            ApplyBlackboxUi();
        }

        private void ApplyMemoryUi()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _memoryLabel.text = "Vault unavailable";
                _memoryStrip.SetSamples(null, null, 0);
                return;
            }

            _memoryLabel.text =
                "alloc " + FormatBytes(vault.AllocatedBytes) +
                " / " + FormatBytes(vault.ArenaBytes) +
                " | fragmentation " + (math.saturate(vault.HeapFragmentationRatio) * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%" +
                " | mutation 0x" + vault.ActiveMutationGuardMask.ToString("X16");
            _memoryStrip.SetSamples(_memoryPressure, _memoryFaults, StripSampleCapacity);
        }

        private void ApplySignalUi()
        {
            int laneCount = SignalBusRegistry.CopyTelemetry(_signalLanes);
            int frameCount = SignalTelemetryRingBuffer.CopyFrames(_signalFrames);
            uint pushed = 0u;
            uint dropped = 0u;
            uint corrupt = 0u;
            if (frameCount > 0)
            {
                SignalTelemetryFrame frame = _signalFrames[frameCount - 1];
                pushed = frame.TotalPushedSignals;
                dropped = frame.DroppedSignals;
                corrupt = frame.CorruptedSignals;
            }

            _signalLabel.text =
                "lanes " + laneCount +
                " | pushed " + pushed +
                " | dropped " + dropped +
                " | corrupt " + corrupt;
            _signalStrip.SetSamples(_signalPressure, null, StripSampleCapacity);
        }

        private void ApplyDispatcherUi()
        {
            if (!SystemDispatcher.TryGetExecutionPipelineXRaySnapshot(_phaseMs, _bucketLoads, out DispatcherStateDTO state))
            {
                _dispatcherLabel.text = "Dispatcher inactive";
                _fenceStrip.SetSamples(null, null, 0);
                return;
            }

            if (SystemDispatcher.TryGetLatestFenceTelemetry(out DispatcherFenceTelemetryEntry fence))
            {
                _dispatcherLabel.text =
                    "frame " + state.CurrentFrame +
                    " | systems " + state.SortedSystemCount +
                    " | pending jobs " + state.PendingSimulationJobCount +
                    " | sim/fixed/AUP " + fence.SimulationWaitMs.ToString("0.00", CultureInfo.InvariantCulture) +
                    "/" + fence.FixedWaitMs.ToString("0.00", CultureInfo.InvariantCulture) +
                    "/" + fence.AupHardFenceMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms" +
                    " | P/A/N handles 0x" + fence.PhysicsHandleBits.ToString("X4") +
                    "/0x" + fence.AudioHandleBits.ToString("X4") +
                    "/0x" + fence.NetcodeHandleBits.ToString("X4");
            }
            else
            {
                _dispatcherLabel.text =
                    "frame " + state.CurrentFrame +
                    " | systems " + state.SortedSystemCount +
                    " | pending jobs " + state.PendingSimulationJobCount +
                    " | fence telemetry unavailable";
            }

            _fenceStrip.SetSamples(_fencePressure, null, StripSampleCapacity);
        }

        private void ApplyContentionUi()
        {
            if (!SignalThreadLocalScratchpad.TryGetLatestTelemetry(out SignalThreadContentionTelemetryEntry latest))
            {
                _contentionLabel.text = "Signal contention telemetry unavailable";
                _contentionStrip.SetSamples(null, null, 0);
                return;
            }

            _contentionLabel.text =
                "frame " + latest.Frame +
                " | written " + latest.WrittenSignals +
                " | coalesced " + latest.CoalescedSignals +
                " | dropped " + latest.DroppedSignals +
                " | overflow " + latest.OverflowSignals +
                " | commit " + latest.CommitMicroseconds + " us";
            _contentionStrip.SetSamples(_contentionPressure, null, StripSampleCapacity);
        }

        private void ApplyTopLaneUi()
        {
            for (int i = 0; i < _topLaneLabels.Length; i++)
            {
                Label label = _topLaneLabels[i];
                if (label == null)
                    continue;

                if (_topLaneScores[i] <= 0)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                label.style.display = DisplayStyle.Flex;
                label.text = "0x" + _topLaneHashes[i].ToString("X8") + " pressure " + _topLaneScores[i];
            }
        }

        private void ApplyBlackboxUi()
        {
            int frameCount = GlobalTelemetryBus.CopyBlackboxEditorFrames(_blackboxFrames);
            int eventCount = GlobalTelemetryBus.CopyBlackboxEditorEvents(_blackboxEvents);
            uint lastFrame = frameCount > 0 ? _blackboxFrames[frameCount - 1].FrameNumber : 0u;
            uint lastFatal = frameCount > 0 ? _blackboxFrames[frameCount - 1].FatalHash : 0u;
            _blackboxLabel.text =
                "frames " + frameCount +
                " | events " + eventCount +
                " | last frame " + lastFrame +
                " | fatal 0x" + lastFatal.ToString("X8");

            int rowCount = math.min(EventRowCount, eventCount);
            int start = eventCount - rowCount;
            for (int i = 0; i < _eventLabels.Length; i++)
            {
                Label label = _eventLabels[i];
                if (label == null)
                    continue;

                if (i >= rowCount)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                TelemetryEventDTO entry = _blackboxEvents[start + i];
                label.style.display = DisplayStyle.Flex;
                label.text =
                    "event 0x" + entry.EventHash.ToString("X8") +
                    " | scalar " + entry.ScalarValue.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | entity " + entry.EntityId;
            }
        }

        private static void ClearStrip(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0f;
        }

        private static void ClearFaultStrip(uint[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0u;
        }

        private static void ShiftLeft(float[] samples)
        {
            for (int i = 1; i < samples.Length; i++)
                samples[i - 1] = samples[i];
            samples[samples.Length - 1] = 0f;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float signalWeight = SignalBusRegistry.GlobalQualityWeight01;
            if (math.isfinite(signalWeight) && signalWeight > 0f)
                return math.saturate(signalWeight);

            float brainWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(brainWeight) ? brainWeight : 1f);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return (bytes / (1024f * 1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture) + " GiB";
            if (bytes >= 1024L * 1024L)
                return (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
            if (bytes >= 1024L)
                return (bytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KiB";
            return bytes + " B";
        }

        private static Label CreateHeader(string text)
        {
            Label label = new Label(text);
            label.style.color = new Color(0.77f, 0.88f, 0.90f, 1f);
            label.style.fontSize = 12f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label CreateSmall(string text)
        {
            Label label = new Label(text);
            label.style.color = new Color(0.68f, 0.74f, 0.76f, 1f);
            label.style.fontSize = 11f;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private sealed class HeatmapStripElement : VisualElement
        {
            private readonly VisualElement[] _columns = new VisualElement[StripSampleCapacity];

            public HeatmapStripElement()
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.FlexEnd;
                style.backgroundColor = new Color(0.035f, 0.040f, 0.045f, 1f);
                for (int i = 0; i < _columns.Length; i++)
                {
                    VisualElement column = new VisualElement();
                    column.style.flexGrow = 1f;
                    column.style.marginLeft = 1f;
                    column.style.marginRight = 1f;
                    column.style.alignSelf = Align.FlexEnd;
                    column.style.backgroundColor = new Color(0.08f, 0.36f, 0.56f, 0.95f);
                    Add(column);
                    _columns[i] = column;
                }
            }

            public void SetSamples(float[] samples, uint[] faultSamples, int sampleCount)
            {
                int count = samples != null ? math.min(math.max(0, sampleCount), math.min(samples.Length, _columns.Length)) : 0;
                uint previousFaultCount = 0u;
                for (int i = 0; i < _columns.Length; i++)
                {
                    VisualElement column = _columns[i];
                    if (i >= count)
                    {
                        column.style.display = DisplayStyle.None;
                        continue;
                    }

                    float pressure = math.saturate(samples[i]);
                    bool faultPulse = faultSamples != null && i < faultSamples.Length &&
                                      (i == 0 ? faultSamples[i] != 0u : faultSamples[i] != previousFaultCount);
                    if (faultSamples != null && i < faultSamples.Length)
                        previousFaultCount = faultSamples[i];

                    column.style.display = DisplayStyle.Flex;
                    column.style.height = Length.Percent(math.max(4f, pressure * 100f));
                    column.style.backgroundColor = faultPulse
                        ? new Color(0.94f, 0.08f, 0.04f, 1f)
                        : Color.Lerp(new Color(0.07f, 0.34f, 0.56f, 0.95f), new Color(0.94f, 0.43f, 0.07f, 1f), pressure);
                }
            }
        }
    }
}
#endif
