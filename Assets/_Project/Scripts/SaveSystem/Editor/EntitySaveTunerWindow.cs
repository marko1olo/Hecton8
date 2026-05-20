#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.SaveSystem.Editor
{
    public sealed class EntitySaveTunerWindow : EditorWindow
    {
        private const double TelemetryRefreshSeconds = 0.25d;
        private const int SummaryBufferCapacity = 192;
        private const string HexDigits = "0123456789ABCDEF";

        private Slider _tombstoneDays;
        private Slider _lz4MinEffort;
        private Slider _lz4MaxEffort;
        private Slider _lowWriteHz;
        private Slider _highWriteHz;
        private Slider _ioPressureBias;
        private Slider _maxWalWriteMs;
        private Slider _mockMutationRate;
        private Slider _rleMinSaving;
        private IntegerField _maxBytesPerFrame;
        private Label _summary;
        private HistogramElement _histogram;
        private readonly char[] _summaryBuffer = new char[SummaryBufferCapacity];
        private bool _suppressCallbacks;
        private double _nextTelemetryRefreshTime;
        private int _lastTelemetryCursor = int.MinValue;
        private ulong _lastSummarySectorHash;
        private ulong _lastSummaryPayloadHash;
        private uint _lastSummaryFrame;
        private uint _lastSummaryFullBytes;
        private uint _lastSummaryDeltaBytes;
        private uint _lastSummaryStoredBytes;
        private uint _lastSummaryFlags;
        private EventCallback<ChangeEvent<float>> _sliderChangedCallback;
        private EventCallback<ChangeEvent<int>> _integerChangedCallback;

        [MenuItem("HECTON-8/Save/Entity Save Tuner")]
        public static void Open()
        {
            GetWindow<EntitySaveTunerWindow>("Entity Save Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _summary = new Label("Entity delta WAL telemetry unavailable.");
            _summary.style.marginBottom = 8f;
            root.Add(_summary);

            _tombstoneDays = AddSlider(root, "Tombstone Days", 0.25f, 30f);
            _lz4MinEffort = AddSlider(root, "LZ4 Min Effort", 0f, 1f);
            _lz4MaxEffort = AddSlider(root, "LZ4 Max Effort", 0f, 1f);
            _lowWriteHz = AddSlider(root, "Low Quality Write Hz", 1f, 20f);
            _highWriteHz = AddSlider(root, "High Quality Write Hz", 1f, 60f);
            _ioPressureBias = AddSlider(root, "I/O Pressure Bias", 0f, 1f);
            _maxWalWriteMs = AddSlider(root, "Max WAL Write Ms", 0.05f, 2f);
            _mockMutationRate = AddSlider(root, "Mock Mutation Rate", 0f, 0.5f);
            _rleMinSaving = AddSlider(root, "RLE Min Saving", 0f, 0.2f);
            _maxBytesPerFrame = new IntegerField("Max Bytes Per Frame");
            _maxBytesPerFrame.style.marginBottom = 4f;
            root.Add(_maxBytesPerFrame);

            Button reset = new Button(WriteDefaultTuning) { text = "Reset Tuning DTO" };
            reset.style.marginTop = 4f;
            root.Add(reset);

            _histogram = new HistogramElement();
            _histogram.style.height = 120f;
            _histogram.style.marginTop = 8f;
            root.Add(_histogram);

            RegisterCallbacks();
            ReadFromVault();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawEntityHeatmap;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawEntityHeatmap;
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + TelemetryRefreshSeconds;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                SetSummaryText("GlobalDataVault is not registered.");
                return;
            }

            if (RefreshSummary(vault) && _histogram != null)
                _histogram.MarkDirtyRepaint();
        }

        private static Slider AddSlider(VisualElement root, string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high) { showInputField = true };
            slider.style.marginBottom = 4f;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _sliderChangedCallback = OnSliderValueChanged;
            _integerChangedCallback = OnIntegerValueChanged;
            _tombstoneDays.RegisterValueChangedCallback(_sliderChangedCallback);
            _lz4MinEffort.RegisterValueChangedCallback(_sliderChangedCallback);
            _lz4MaxEffort.RegisterValueChangedCallback(_sliderChangedCallback);
            _lowWriteHz.RegisterValueChangedCallback(_sliderChangedCallback);
            _highWriteHz.RegisterValueChangedCallback(_sliderChangedCallback);
            _ioPressureBias.RegisterValueChangedCallback(_sliderChangedCallback);
            _maxWalWriteMs.RegisterValueChangedCallback(_sliderChangedCallback);
            _mockMutationRate.RegisterValueChangedCallback(_sliderChangedCallback);
            _rleMinSaving.RegisterValueChangedCallback(_sliderChangedCallback);
            _maxBytesPerFrame.RegisterValueChangedCallback(_integerChangedCallback);
        }

        private void OnSliderValueChanged(ChangeEvent<float> changeEvent)
        {
            WriteToVault();
        }

        private void OnIntegerValueChanged(ChangeEvent<int> changeEvent)
        {
            WriteToVault();
        }

        private void ReadFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                SetSummaryText("GlobalDataVault is not registered.");
                return;
            }

            VaultBufferHandle<EntityDeltaCompressionTuningDTO> handle = vault.GetBufferHandle<EntityDeltaCompressionTuningDTO>(
                BufferID.SaveEntityDeltaTuning,
                1,
                SystemID.SavePersistence,
                NativeArrayOptions.ClearMemory);
            NativeArray<EntityDeltaCompressionTuningDTO> tuningBuffer = handle.Resolve(vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            EntityDeltaCompressionTuningDTO tuning = tuningBuffer[0];
            if (tuning.SchemaHash == 0u)
            {
                tuning = EntityDeltaCompressionArchitecture.BuildDefaultTuning();
                tuningBuffer[0] = tuning;
            }

            _suppressCallbacks = true;
            SetSliderWithoutNotify(_tombstoneDays, tuning.TombstoneMaxDays);
            SetSliderWithoutNotify(_lz4MinEffort, tuning.Lz4MinEffort01);
            SetSliderWithoutNotify(_lz4MaxEffort, tuning.Lz4MaxEffort01);
            SetSliderWithoutNotify(_lowWriteHz, tuning.LowQualityWriteHz);
            SetSliderWithoutNotify(_highWriteHz, tuning.HighQualityWriteHz);
            SetSliderWithoutNotify(_ioPressureBias, tuning.IoPressureBias01);
            SetSliderWithoutNotify(_maxWalWriteMs, tuning.MaxWalWriteMillis);
            SetSliderWithoutNotify(_mockMutationRate, tuning.MockMutationRate01);
            SetSliderWithoutNotify(_rleMinSaving, tuning.RleMinSaving01);
            if (_maxBytesPerFrame != null)
                _maxBytesPerFrame.SetValueWithoutNotify(tuning.MaxBytesPerFrame > int.MaxValue ? int.MaxValue : (int)tuning.MaxBytesPerFrame);
            _suppressCallbacks = false;

            if (RefreshSummary(vault) && _histogram != null)
                _histogram.MarkDirtyRepaint();
        }

        private void WriteDefaultTuning()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            VaultBufferHandle<EntityDeltaCompressionTuningDTO> handle = vault.GetBufferHandle<EntityDeltaCompressionTuningDTO>(
                BufferID.SaveEntityDeltaTuning,
                1,
                SystemID.SavePersistence,
                NativeArrayOptions.ClearMemory);
            NativeArray<EntityDeltaCompressionTuningDTO> tuningBuffer = handle.Resolve(vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            tuningBuffer[0] = EntityDeltaCompressionArchitecture.BuildDefaultTuning();
            ReadFromVault();
        }

        private void WriteToVault()
        {
            if (_suppressCallbacks)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            VaultBufferHandle<EntityDeltaCompressionTuningDTO> handle = vault.GetBufferHandle<EntityDeltaCompressionTuningDTO>(
                BufferID.SaveEntityDeltaTuning,
                1,
                SystemID.SavePersistence,
                NativeArrayOptions.ClearMemory);
            NativeArray<EntityDeltaCompressionTuningDTO> tuningBuffer = handle.Resolve(vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            EntityDeltaCompressionTuningDTO tuning = tuningBuffer[0];
            if (tuning.SchemaHash == 0u)
                tuning = EntityDeltaCompressionArchitecture.BuildDefaultTuning();

            tuning.TombstoneMaxDays = math.clamp(_tombstoneDays.value, 0.25f, 30f);
            tuning.Lz4MinEffort01 = math.saturate(_lz4MinEffort.value);
            tuning.Lz4MaxEffort01 = math.max(tuning.Lz4MinEffort01, math.saturate(_lz4MaxEffort.value));
            tuning.LowQualityWriteHz = math.max(1f, _lowWriteHz.value);
            tuning.HighQualityWriteHz = math.max(tuning.LowQualityWriteHz, _highWriteHz.value);
            tuning.IoPressureBias01 = math.saturate(_ioPressureBias.value);
            tuning.MaxWalWriteMillis = math.max(0.05f, _maxWalWriteMs.value);
            tuning.MockMutationRate01 = math.saturate(_mockMutationRate.value);
            tuning.RleMinSaving01 = math.saturate(_rleMinSaving.value);
            tuning.MaxBytesPerFrame = (uint)math.max(1024, _maxBytesPerFrame != null ? _maxBytesPerFrame.value : (int)tuning.MaxBytesPerFrame);
            tuning.SchemaHash = EntityDeltaCompressionArchitecture.SchemaHash;
            tuning.Flags = 1u;
            tuningBuffer[0] = tuning;
        }

        private bool RefreshSummary(IDataVault vault)
        {
            if (_summary == null)
                return false;

            if (!TryResolveExistingBuffer(vault, BufferID.SaveEntityDeltaTelemetryRing, out NativeArray<EntityCompressionTelemetryEntry> telemetry) ||
                telemetry.Length == 0)
            {
                SetSummaryText("Entity delta WAL telemetry ring is empty.");
                return false;
            }

            int cursorValue = 0;
            if (TryResolveExistingBuffer(vault, BufferID.SaveEntityDeltaTelemetryCursor, out NativeArray<int> cursor) && cursor.Length > 0)
                cursorValue = math.max(0, cursor[0]);

            int last = math.max(0, cursorValue - 1);
            EntityCompressionTelemetryEntry entry = telemetry[last % telemetry.Length];
            if (cursorValue == _lastTelemetryCursor &&
                entry.SectorHash == _lastSummarySectorHash &&
                entry.PayloadHash == _lastSummaryPayloadHash &&
                entry.Frame == _lastSummaryFrame &&
                entry.FullSnapshotBytes == _lastSummaryFullBytes &&
                entry.DenseDeltaBytes == _lastSummaryDeltaBytes &&
                entry.CompressedBytes == _lastSummaryStoredBytes &&
                entry.Flags == _lastSummaryFlags)
            {
                return false;
            }

            _lastTelemetryCursor = cursorValue;
            _lastSummarySectorHash = entry.SectorHash;
            _lastSummaryPayloadHash = entry.PayloadHash;
            _lastSummaryFrame = entry.Frame;
            _lastSummaryFullBytes = entry.FullSnapshotBytes;
            _lastSummaryDeltaBytes = entry.DenseDeltaBytes;
            _lastSummaryStoredBytes = entry.CompressedBytes;
            _lastSummaryFlags = entry.Flags;
            float ratio = entry.FullSnapshotBytes > 0u ? math.saturate((float)entry.CompressedBytes / entry.FullSnapshotBytes) : 0f;
            SetSummaryFromTelemetry(in entry, ratio);
            return true;
        }

        private void SetSummaryFromTelemetry(in EntityCompressionTelemetryEntry entry, float ratio)
        {
            int cursor = 0;
            cursor = AppendLiteral(_summaryBuffer, cursor, "Last sector: ");
            cursor = AppendHex64(_summaryBuffer, cursor, entry.SectorHash);
            cursor = AppendLiteral(_summaryBuffer, cursor, " | full ");
            cursor = AppendUInt32(_summaryBuffer, cursor, entry.FullSnapshotBytes);
            cursor = AppendLiteral(_summaryBuffer, cursor, " | delta ");
            cursor = AppendUInt32(_summaryBuffer, cursor, entry.DenseDeltaBytes);
            cursor = AppendLiteral(_summaryBuffer, cursor, " | stored ");
            cursor = AppendUInt32(_summaryBuffer, cursor, entry.CompressedBytes);
            cursor = AppendLiteral(_summaryBuffer, cursor, " | ratio ");
            cursor = AppendRatio3(_summaryBuffer, cursor, ratio);
            cursor = AppendLiteral(_summaryBuffer, cursor, " | flags 0x");
            cursor = AppendHex32(_summaryBuffer, cursor, entry.Flags);

            // UI Toolkit Label stores text as a managed string. The real-time histogram stays Vault-driven and allocation-free;
            // this editor-only summary crosses the string boundary only after the telemetry cursor or payload fields change.
            SetSummaryText(new string(_summaryBuffer, 0, cursor));
        }

        private void SetSummaryText(string text)
        {
            if (_summary != null && _summary.text != text)
                _summary.text = text;
        }

        private static int AppendLiteral(char[] buffer, int cursor, string text)
        {
            int writable = math.min(text.Length, buffer.Length - cursor);
            for (int i = 0; i < writable; i++)
                buffer[cursor + i] = text[i];

            return cursor + writable;
        }

        private static int AppendUInt32(char[] buffer, int cursor, uint value)
        {
            if (cursor >= buffer.Length)
                return cursor;

            if (value == 0u)
            {
                buffer[cursor++] = '0';
                return cursor;
            }

            int start = cursor;
            uint remaining = value;
            while (remaining > 0u && cursor < buffer.Length)
            {
                buffer[cursor++] = (char)('0' + (remaining % 10u));
                remaining /= 10u;
            }

            Reverse(buffer, start, cursor - 1);
            return cursor;
        }

        private static int AppendRatio3(char[] buffer, int cursor, float value)
        {
            uint scaled = (uint)math.round(math.saturate(value) * 1000f);
            cursor = AppendUInt32(buffer, cursor, scaled / 1000u);
            if (cursor < buffer.Length)
                buffer[cursor++] = '.';

            uint fraction = scaled % 1000u;
            if (cursor < buffer.Length)
                buffer[cursor++] = (char)('0' + (fraction / 100u));
            if (cursor < buffer.Length)
                buffer[cursor++] = (char)('0' + ((fraction / 10u) % 10u));
            if (cursor < buffer.Length)
                buffer[cursor++] = (char)('0' + (fraction % 10u));

            return cursor;
        }

        private static int AppendHex64(char[] buffer, int cursor, ulong value)
        {
            for (int nibble = 15; nibble >= 0 && cursor < buffer.Length; nibble--)
            {
                int digit = (int)((value >> (nibble * 4)) & 0xFUL);
                buffer[cursor++] = HexDigits[digit];
            }

            return cursor;
        }

        private static int AppendHex32(char[] buffer, int cursor, uint value)
        {
            for (int nibble = 7; nibble >= 0 && cursor < buffer.Length; nibble--)
            {
                int digit = (int)((value >> (nibble * 4)) & 0xFU);
                buffer[cursor++] = HexDigits[digit];
            }

            return cursor;
        }

        private static void Reverse(char[] buffer, int left, int right)
        {
            while (left < right)
            {
                char value = buffer[left];
                buffer[left] = buffer[right];
                buffer[right] = value;
                left++;
                right--;
            }
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private static void DrawEntityHeatmap(SceneView sceneView)
        {
            Hecton8.SaveSystem.EntityDeltaGizmoProbe.DrawSectorHeatmap(131072u, 1f);
        }

        private sealed class HistogramElement : VisualElement
        {
            public HistogramElement()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.25f;

                IDataVault vault = GlobalRegistry.DataVault;
                if (vault == null ||
                    !TryResolveExistingBuffer(vault, BufferID.SaveEntityDeltaTelemetryRing, out NativeArray<EntityCompressionTelemetryEntry> telemetry) ||
                    telemetry.Length == 0)
                {
                    painter.strokeColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.Stroke();
                    return;
                }

                painter.strokeColor = new Color(0.2f, 0.85f, 0.65f, 1f);
                painter.BeginPath();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    EntityCompressionTelemetryEntry entry = telemetry[i];
                    float saved01 = entry.FullSnapshotBytes > 0u
                        ? 1f - math.saturate((float)entry.CompressedBytes / entry.FullSnapshotBytes)
                        : 0f;
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetry.Length - 1f));
                    float y = rect.yMax - rect.height * saved01;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();

                painter.strokeColor = new Color(1f, 0.45f, 0.12f, 0.95f);
                painter.BeginPath();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    EntityCompressionTelemetryEntry entry = telemetry[i];
                    float latency01 = math.saturate(entry.DiskWriteLatencyMs / 50f);
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetry.Length - 1f));
                    float y = rect.yMax - rect.height * latency01;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }

        private static bool TryResolveExistingBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                !vault.TryGetBufferHandle(bufferId, out VaultBufferHandle<T> handle))
            {
                return false;
            }

            buffer = handle.Resolve(vault);
            return buffer.IsCreated;
        }
    }
}
#endif
