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
    public sealed class VoxelSaveTunerWindow : EditorWindow
    {
        private Slider _pruneThreshold;
        private Slider _lz4MinEffort;
        private Slider _lz4MaxEffort;
        private Slider _lowWriteHz;
        private Slider _highWriteHz;
        private Slider _chunkUnloadDistance;
        private Slider _ioPressureBias;
        private Slider _maxWalWriteMs;
        private IntegerField _maxBytesPerFrame;
        private Label _summary;
        private HistogramElement _histogram;
        private bool _suppressCallbacks;

        [MenuItem("HECTON-8/Save/Voxel Save Tuner")]
        public static void Open()
        {
            GetWindow<VoxelSaveTunerWindow>("Voxel Save Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _summary = new Label("Voxel delta WAL telemetry unavailable.");
            _summary.style.marginBottom = 8f;
            root.Add(_summary);

            _pruneThreshold = AddSlider(root, "Prune Threshold", 0f, 0.001f);
            _lz4MinEffort = AddSlider(root, "LZ4 Min Effort", 0f, 1f);
            _lz4MaxEffort = AddSlider(root, "LZ4 Max Effort", 0f, 1f);
            _lowWriteHz = AddSlider(root, "Low Quality Write Hz", 1f, 20f);
            _highWriteHz = AddSlider(root, "High Quality Write Hz", 1f, 60f);
            _chunkUnloadDistance = AddSlider(root, "Chunk Unload Meters", 64f, 4000f);
            _ioPressureBias = AddSlider(root, "I/O Pressure Bias", 0f, 1f);
            _maxWalWriteMs = AddSlider(root, "Max WAL Write Ms", 0.05f, 2f);
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
            SceneView.duringSceneGui += DrawVoxelHeatmap;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawVoxelHeatmap;
        }

        private void Update()
        {
            ReadFromVault();
            if (_histogram != null)
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
            _pruneThreshold.RegisterValueChangedCallback(_ => WriteToVault());
            _lz4MinEffort.RegisterValueChangedCallback(_ => WriteToVault());
            _lz4MaxEffort.RegisterValueChangedCallback(_ => WriteToVault());
            _lowWriteHz.RegisterValueChangedCallback(_ => WriteToVault());
            _highWriteHz.RegisterValueChangedCallback(_ => WriteToVault());
            _chunkUnloadDistance.RegisterValueChangedCallback(_ => WriteToVault());
            _ioPressureBias.RegisterValueChangedCallback(_ => WriteToVault());
            _maxWalWriteMs.RegisterValueChangedCallback(_ => WriteToVault());
            _maxBytesPerFrame.RegisterValueChangedCallback(_ => WriteToVault());
        }

        private void ReadFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                if (_summary != null)
                    _summary.text = "GlobalDataVault is not registered.";
                return;
            }

            if (!TryOpenOrAcquireTuningBuffer(vault, out NativeArray<VoxelDeltaCompressionTuningDTO> tuningBuffer))
                return;

            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            VoxelDeltaCompressionTuningDTO tuning = tuningBuffer[0];
            if (tuning.SchemaHash == 0u)
            {
                tuning = VoxelDeltaCompressionArchitecture.BuildDefaultTuning();
                tuningBuffer[0] = tuning;
            }

            _suppressCallbacks = true;
            SetSliderWithoutNotify(_pruneThreshold, tuning.PruneThreshold01);
            SetSliderWithoutNotify(_lz4MinEffort, tuning.Lz4MinEffort01);
            SetSliderWithoutNotify(_lz4MaxEffort, tuning.Lz4MaxEffort01);
            SetSliderWithoutNotify(_lowWriteHz, tuning.LowQualityWriteHz);
            SetSliderWithoutNotify(_highWriteHz, tuning.HighQualityWriteHz);
            SetSliderWithoutNotify(_chunkUnloadDistance, tuning.ChunkUnloadDistanceMeters);
            SetSliderWithoutNotify(_ioPressureBias, tuning.IoPressureBias01);
            SetSliderWithoutNotify(_maxWalWriteMs, tuning.MaxWalWriteMillis);
            if (_maxBytesPerFrame != null)
                _maxBytesPerFrame.SetValueWithoutNotify(tuning.MaxBytesPerFrame > int.MaxValue ? int.MaxValue : (int)tuning.MaxBytesPerFrame);
            _suppressCallbacks = false;

            RefreshSummary(vault);
        }

        private void WriteDefaultTuning()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryOpenOrAcquireTuningBuffer(vault, out NativeArray<VoxelDeltaCompressionTuningDTO> tuningBuffer))
                return;

            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            tuningBuffer[0] = VoxelDeltaCompressionArchitecture.BuildDefaultTuning();
            ReadFromVault();
        }

        private void WriteToVault()
        {
            if (_suppressCallbacks)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryOpenOrAcquireTuningBuffer(vault, out NativeArray<VoxelDeltaCompressionTuningDTO> tuningBuffer))
                return;

            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            VoxelDeltaCompressionTuningDTO tuning = tuningBuffer[0];
            if (tuning.SchemaHash == 0u)
                tuning = VoxelDeltaCompressionArchitecture.BuildDefaultTuning();

            tuning.PruneThreshold01 = math.clamp(_pruneThreshold.value, 0f, 0.001f);
            tuning.Lz4MinEffort01 = math.saturate(_lz4MinEffort.value);
            tuning.Lz4MaxEffort01 = math.max(tuning.Lz4MinEffort01, math.saturate(_lz4MaxEffort.value));
            tuning.LowQualityWriteHz = math.max(1f, _lowWriteHz.value);
            tuning.HighQualityWriteHz = math.max(tuning.LowQualityWriteHz, _highWriteHz.value);
            tuning.ChunkUnloadDistanceMeters = math.max(64f, _chunkUnloadDistance.value);
            tuning.IoPressureBias01 = math.saturate(_ioPressureBias.value);
            tuning.MaxWalWriteMillis = math.max(0.05f, _maxWalWriteMs.value);
            tuning.MaxBytesPerFrame = (uint)math.max(1024, _maxBytesPerFrame != null ? _maxBytesPerFrame.value : (int)tuning.MaxBytesPerFrame);
            tuning.SchemaHash = 0x56584431u;
            tuning.Flags = 1u;
            tuningBuffer[0] = tuning;
        }

        private void RefreshSummary(IDataVault vault)
        {
            if (_summary == null)
                return;

            if (!TryReadExistingBuffer(vault, BufferID.SaveVoxelDeltaTelemetryRing, out NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetry) ||
                telemetry.Length == 0)
            {
                _summary.text = "Voxel delta WAL telemetry ring is empty.";
                return;
            }

            int last = 0;
            if (TryReadExistingBuffer(vault, BufferID.SaveVoxelDeltaTelemetryCursor, out NativeArray<int> cursor) && cursor.Length > 0)
                last = math.max(0, cursor[0] - 1);

            VoxelDeltaCompressionTelemetryEntry entry = telemetry[last % telemetry.Length];
            float ratio = entry.RawBytes > 0u ? math.saturate((float)entry.CompressedBytes / entry.RawBytes) : 0f;
            _summary.text = "Last sector: " + entry.SectorHash.ToString("X16") +
                            " | raw " + entry.RawBytes +
                            " | stored " + entry.CompressedBytes +
                            " | ratio " + ratio.ToString("0.000") +
                            " | flags 0x" + entry.Flags.ToString("X8");
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private static void DrawVoxelHeatmap(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryReadExistingBuffer(vault, BufferID.SaveVoxelDeltaSectorStats, out NativeArray<VoxelDeltaSectorStatsDTO> stats))
            {
                return;
            }

            for (int i = 0; i < stats.Length; i++)
            {
                VoxelDeltaSectorStatsDTO stat = stats[i];
                if (stat.ModifiedCells == 0u)
                    continue;

                float heat = math.saturate(stat.CompressedBytes / 65536f);
                Handles.color = Color.Lerp(
                    new Color(0.1f, 0.9f, 0.25f, 0.22f),
                    new Color(1f, 0.1f, 0.02f, 0.78f),
                    heat);
                Vector3 center = new Vector3(
                    stat.SectorX * VoxelDeltaCompressionArchitecture.ChunkResolution,
                    stat.SectorY * VoxelDeltaCompressionArchitecture.ChunkResolution,
                    stat.SectorZ * VoxelDeltaCompressionArchitecture.ChunkResolution);
                Handles.DrawWireCube(center, Vector3.one * VoxelDeltaCompressionArchitecture.ChunkResolution);
            }
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
                    !TryReadExistingBuffer(vault, BufferID.SaveVoxelDeltaTelemetryRing, out NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetry) ||
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
                    VoxelDeltaCompressionTelemetryEntry entry = telemetry[i];
                    float saved01 = entry.RawBytes > 0u
                        ? 1f - math.saturate((float)entry.CompressedBytes / entry.RawBytes)
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
                    VoxelDeltaCompressionTelemetryEntry entry = telemetry[i];
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

        private static bool TryOpenOrAcquireTuningBuffer(
            IDataVault vault,
            out NativeArray<VoxelDeltaCompressionTuningDTO> buffer)
        {
            buffer = default;
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(
                    BufferID.SaveVoxelDeltaTuning,
                    out VaultGenerationHandle<VoxelDeltaCompressionTuningDTO> existing) &&
                vault.TryResolveHandle(in existing, out buffer) &&
                buffer.IsCreated &&
                buffer.Length > 0)
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                buffer = default;
                return false;
            }

            VaultGenerationHandle<VoxelDeltaCompressionTuningDTO> handle = vault.GetGenerationHandle<VoxelDeltaCompressionTuningDTO>(
                BufferID.SaveVoxelDeltaTuning,
                1,
                SystemID.SavePersistence,
                NativeArrayOptions.ClearMemory);
            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryReadExistingBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }
    }
}
#endif
