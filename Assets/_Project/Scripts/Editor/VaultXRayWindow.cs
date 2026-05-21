#if UNITY_EDITOR
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Core.Memory.Editor
{
    /// <summary>
    /// Editor-only UI Toolkit block-map view for GlobalDataVault fragmentation.
    /// </summary>
    public sealed class VaultXRayWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.5d;
        private const int MaxVisibleBlocks = 256;
        private const int MaxWaterfallSamples = 64;
        private const float Padding = 6f;
        private static readonly Color _freeColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        private static readonly Color _activeColor = new Color(0.05f, 0.45f, 0.16f, 1f);
        private static readonly Color _lockedColor = new Color(0.76f, 0.57f, 0.06f, 1f);
        private static readonly Color _faultColor = new Color(0.95f, 0.02f, 0.02f, 1f);
        private static readonly Color _backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color _panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        private readonly VaultMemoryBlockSnapshot[] _blocks = new VaultMemoryBlockSnapshot[MaxVisibleBlocks];
        private readonly float[] _waterfallPressure = new float[MaxWaterfallSamples];
        private readonly uint[] _waterfallFaults = new uint[MaxWaterfallSamples];
        private readonly byte[] _waterfallFlags = new byte[MaxWaterfallSamples];
        private VaultHeatmapElement _heatmap;
        private VaultTelemetryWaterfallElement _waterfall;
        private Label _summaryLabel;
        private Label _qualityLabel;
        private Label _statusLabel;
        private Label _faultLabel;
        private double _nextRefreshTime;
        private int _blockCount;
        private long _totalBytes;
        private long _allocatedBytes;
        private long _arenaBytes;
        private uint _generationMismatchCount;
        private int _lastFaultBufferId;
        private uint _lastFaultHandleGeneration;
        private uint _lastFaultMetaGeneration;
        private uint _generation;
        private float _fragmentation01;
        private int _waterfallCount;
        private string _overrideStatus = "CSV idle.";

        [MenuItem("Hecton8/Core/Vault X-Ray")]
        public static void Open()
        {
            VaultXRayWindow window = GetWindow<VaultXRayWindow>("Vault X-Ray");
            window.minSize = new Vector2(520f, 280f);
            window.RefreshSnapshot();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = Padding;
            rootVisualElement.style.paddingRight = Padding;
            rootVisualElement.style.paddingTop = Padding;
            rootVisualElement.style.paddingBottom = Padding;
            rootVisualElement.style.backgroundColor = _backgroundColor;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 4f;
            rootVisualElement.Add(toolbar);

            _summaryLabel = new Label();
            _summaryLabel.style.flexGrow = 1f;
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(_summaryLabel);

            Button reloadButton = new Button(ReloadCsvOverride) { text = "Reload CSV" };
            reloadButton.style.width = 110f;
            toolbar.Add(reloadButton);

            Button forceButton = new Button(ForceDefragNextPreSimulation) { text = "Force Defrag" };
            forceButton.style.width = 120f;
            toolbar.Add(forceButton);

            _qualityLabel = new Label();
            _qualityLabel.style.marginBottom = 3f;
            rootVisualElement.Add(_qualityLabel);

            _statusLabel = new Label();
            _statusLabel.style.marginBottom = 6f;
            rootVisualElement.Add(_statusLabel);

            _faultLabel = new Label();
            _faultLabel.style.marginBottom = 6f;
            rootVisualElement.Add(_faultLabel);

            _waterfall = new VaultTelemetryWaterfallElement();
            _waterfall.style.height = 42f;
            _waterfall.style.marginBottom = 6f;
            _waterfall.style.backgroundColor = _panelColor;
            rootVisualElement.Add(_waterfall);

            _heatmap = new VaultHeatmapElement();
            _heatmap.style.flexGrow = 1f;
            _heatmap.style.backgroundColor = _panelColor;
            _heatmap.style.paddingLeft = 4f;
            _heatmap.style.paddingRight = 4f;
            _heatmap.style.paddingTop = 4f;
            _heatmap.style.paddingBottom = 4f;
            rootVisualElement.Add(_heatmap);

            RefreshSnapshot();
            ApplySnapshotToUi();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            RefreshSnapshot();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;
            RefreshSnapshot();
            ApplySnapshotToUi();
        }

        private void RefreshSnapshot()
        {
            _blockCount = 0;
            _totalBytes = 0L;
            _allocatedBytes = 0L;
            _arenaBytes = 0L;
            _generation = 0u;
            _fragmentation01 = 0f;
            _generationMismatchCount = 0u;
            _lastFaultBufferId = 0;
            _lastFaultHandleGeneration = 0u;
            _lastFaultMetaGeneration = 0u;
            _waterfallCount = 0;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            _allocatedBytes = vault.AllocatedBytes;
            _arenaBytes = vault.ArenaBytes;
            _generation = vault.VaultGenerationID;
            _fragmentation01 = math.saturate(vault.HeapFragmentationRatio);
            if (vault.TryGetVaultTelemetrySnapshot(0, out VaultTelemetrySnapshot telemetry))
            {
                _generationMismatchCount = telemetry.GenerationMismatchCount;
                _lastFaultBufferId = telemetry.LastFaultBufferID;
                _lastFaultHandleGeneration = telemetry.LastFaultHandleGeneration;
                _lastFaultMetaGeneration = telemetry.LastFaultMetaGeneration;
            }

            for (int age = MaxWaterfallSamples - 1; age >= 0; age--)
            {
                if (!vault.TryGetVaultTelemetrySnapshot(age, out VaultTelemetrySnapshot telemetry))
                    continue;

                int sample = _waterfallCount++;
                _waterfallPressure[sample] = telemetry.ArenaBytes > 0L
                    ? math.saturate((float)((double)telemetry.AllocatedBytes / telemetry.ArenaBytes))
                    : 0f;
                _waterfallFaults[sample] = telemetry.GenerationMismatchCount;
                _waterfallFlags[sample] = telemetry.LastDefragFlags;
            }

            int count = math.min(vault.MemoryBlockSnapshotCount, MaxVisibleBlocks);
            for (int i = 0; i < count; i++)
            {
                if (!vault.TryGetMemoryBlockSnapshot(i, out VaultMemoryBlockSnapshot block))
                    continue;

                _blocks[_blockCount++] = block;
                if (block.Bytes > 0L)
                    _totalBytes += block.Bytes;
            }
        }

        private void ApplySnapshotToUi()
        {
            if (_summaryLabel == null ||
                _qualityLabel == null ||
                _statusLabel == null ||
                _faultLabel == null ||
                _heatmap == null ||
                _waterfall == null)
            {
                return;
            }

            _summaryLabel.text = "Blocks " + _blockCount +
                                 " | Alloc " + FormatBytes(_allocatedBytes) +
                                 " / " + FormatBytes(_arenaBytes) +
                                 " | Gen " + _generation;

            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            _qualityLabel.text = "GlobalQualityWeight " + quality.ToString("0.000") +
                                 " | stride x" + ResolveQualityStride(quality) +
                                 " | fragmentation " + (_fragmentation01 * 100f).ToString("0.0") + "%";
            _statusLabel.text = _overrideStatus;
            _faultLabel.text = "Generation faults " + _generationMismatchCount +
                               " | last buffer " + _lastFaultBufferId +
                               " | handle/meta " + _lastFaultHandleGeneration + "/" + _lastFaultMetaGeneration;
            _faultLabel.style.color = _generationMismatchCount == 0u ? Color.gray : _faultColor;
            _waterfall.SetSamples(_waterfallPressure, _waterfallFaults, _waterfallFlags, _waterfallCount);
            _heatmap.SetBlocks(_blocks, _blockCount, _totalBytes);
        }

        private void ForceDefragNextPreSimulation()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _overrideStatus = "No active vault.";
                ApplySnapshotToUi();
                return;
            }

            vault.RequestEditorForceDefragmentation();
            _overrideStatus = "Force defrag armed for next PRE_SIMULATION fence.";
            ApplySnapshotToUi();
        }

        private void ReloadCsvOverride()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _overrideStatus = "No active vault.";
                ApplySnapshotToUi();
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                _overrideStatus = "Project root unavailable.";
                ApplySnapshotToUi();
                return;
            }

            string path = Path.Combine(projectRoot, "memory_overrides.csv");
            _overrideStatus = VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv(vault, path)
                ? "CSV applied: memory_overrides.csv"
                : "CSV missing or rejected.";
            RefreshSnapshot();
            ApplySnapshotToUi();
        }

        private static int ResolveQualityStride(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.clamp(1 + (int)math.floor((1f - quality) * 3.333334f), 1, 4);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return (bytes / (1024f * 1024f * 1024f)).ToString("0.00") + " GiB";
            if (bytes >= 1024L * 1024L)
                return (bytes / (1024f * 1024f)).ToString("0.0") + " MiB";
            if (bytes >= 1024L)
                return (bytes / 1024f).ToString("0.0") + " KiB";
            return bytes + " B";
        }

        private sealed class VaultHeatmapElement : VisualElement
        {
            private readonly VisualElement[] _bars = new VisualElement[MaxVisibleBlocks];

            public VaultHeatmapElement()
            {
                style.flexDirection = FlexDirection.Column;
                for (int i = 0; i < _bars.Length; i++)
                {
                    VisualElement bar = new VisualElement();
                    bar.style.height = 12f;
                    bar.style.marginBottom = 2f;
                    bar.style.display = DisplayStyle.None;
                    Add(bar);
                    _bars[i] = bar;
                }
            }

            public void SetBlocks(VaultMemoryBlockSnapshot[] blocks, int blockCount, long totalBytes)
            {
                int count = blocks != null ? math.min(math.max(0, blockCount), _bars.Length) : 0;
                for (int i = 0; i < _bars.Length; i++)
                {
                    VisualElement bar = _bars[i];
                    if (i >= count || totalBytes <= 0L)
                    {
                        bar.style.display = DisplayStyle.None;
                        continue;
                    }

                    VaultMemoryBlockSnapshot block = blocks[i];
                    float fraction = math.saturate((float)((double)block.Bytes / totalBytes));
                    bar.style.display = DisplayStyle.Flex;
                    bar.style.width = new Length(math.max(0.5f, fraction * 100f), LengthUnit.Percent);
                    bar.style.backgroundColor = block.LockCount != 0
                        ? _lockedColor
                        : block.State == GlobalDataVault.BlockStateOccupied
                            ? _activeColor
                            : _freeColor;
                }
            }
        }

        private sealed class VaultTelemetryWaterfallElement : VisualElement
        {
            private readonly VisualElement[] _columns = new VisualElement[MaxWaterfallSamples];

            public VaultTelemetryWaterfallElement()
            {
                style.flexDirection = FlexDirection.Row;
                for (int i = 0; i < _columns.Length; i++)
                {
                    VisualElement column = new VisualElement();
                    column.style.flexGrow = 1f;
                    column.style.marginLeft = 1f;
                    column.style.marginRight = 1f;
                    column.style.alignSelf = Align.FlexEnd;
                    Add(column);
                    _columns[i] = column;
                }
            }

            public void SetSamples(float[] pressure, uint[] faults, byte[] flags, int sampleCount)
            {
                int count = pressure != null && faults != null && flags != null
                    ? math.min(math.max(0, sampleCount), _columns.Length)
                    : 0;

                uint previousFaultCount = 0u;
                for (int i = 0; i < _columns.Length; i++)
                {
                    VisualElement column = _columns[i];
                    if (i >= count)
                    {
                        column.style.display = DisplayStyle.None;
                        continue;
                    }

                    float p = math.saturate(pressure[i]);
                    bool faultPulse = i == 0
                        ? faults[i] != 0u
                        : faults[i] != previousFaultCount;
                    bool maintenancePulse = (flags[i] & (1 << 5)) != 0;
                    previousFaultCount = faults[i];

                    column.style.display = DisplayStyle.Flex;
                    column.style.height = math.max(2f, 38f * p);
                    column.style.backgroundColor = faultPulse
                        ? _faultColor
                        : maintenancePulse
                            ? _lockedColor
                            : Color.Lerp(_freeColor, _activeColor, p);
                }
            }
        }
    }
}
#endif
