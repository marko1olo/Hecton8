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
        private const float Padding = 6f;
        private static readonly Color _freeColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        private static readonly Color _activeColor = new Color(0.05f, 0.45f, 0.16f, 1f);
        private static readonly Color _lockedColor = new Color(0.76f, 0.57f, 0.06f, 1f);
        private static readonly Color _backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color _panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        private readonly VaultMemoryBlockSnapshot[] _blocks = new VaultMemoryBlockSnapshot[MaxVisibleBlocks];
        private VaultHeatmapElement _heatmap;
        private Label _summaryLabel;
        private Label _qualityLabel;
        private Label _statusLabel;
        private double _nextRefreshTime;
        private int _blockCount;
        private long _totalBytes;
        private long _allocatedBytes;
        private long _arenaBytes;
        private uint _generation;
        private float _fragmentation01;
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

            _qualityLabel = new Label();
            _qualityLabel.style.marginBottom = 3f;
            rootVisualElement.Add(_qualityLabel);

            _statusLabel = new Label();
            _statusLabel.style.marginBottom = 6f;
            rootVisualElement.Add(_statusLabel);

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

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            _allocatedBytes = vault.AllocatedBytes;
            _arenaBytes = vault.ArenaBytes;
            _generation = vault.VaultGenerationID;
            _fragmentation01 = math.saturate(vault.HeapFragmentationRatio);

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
            if (_summaryLabel == null || _qualityLabel == null || _statusLabel == null || _heatmap == null)
                return;

            _summaryLabel.text = "Blocks " + _blockCount +
                                 " | Alloc " + FormatBytes(_allocatedBytes) +
                                 " / " + FormatBytes(_arenaBytes) +
                                 " | Gen " + _generation;

            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            _qualityLabel.text = "GlobalQualityWeight " + quality.ToString("0.000") +
                                 " | stride x" + ResolveQualityStride(quality) +
                                 " | fragmentation " + (_fragmentation01 * 100f).ToString("0.0") + "%";
            _statusLabel.text = _overrideStatus;
            _heatmap.SetBlocks(_blocks, _blockCount, _totalBytes);
        }

        private void ReloadCsvOverride()
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
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
    }
}
#endif
