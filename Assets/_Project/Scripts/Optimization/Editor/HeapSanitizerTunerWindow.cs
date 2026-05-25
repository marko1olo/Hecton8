#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// UI Toolkit facade for Addressables heap sanitizer runtime state.
    /// </summary>
    public sealed class HeapSanitizerTunerWindow : EditorWindow
    {
        private const int GraphSamples = 64;
        private const int MaxRows = 64;

        private Label _statusLabel;
        private Label _activeLabel;
        private Label _hitsLabel;
        private Label _missesLabel;
        private Label _releasedLabel;
        private Label _leakBanner;
        private Slider _ttlSlider;
        private Slider _vramSlider;
        private TextField _csvPathField;
        private VisualElement[] _activeBars;
        private VisualElement[] _hitBars;
        private VisualElement[] _vramBars;
        private Label[] _trackerRows;
        private IVisualElementScheduledItem _updateLoop;
        private bool _isUpdatingControls;
        private bool _statusLabelInitialized;
        private bool _governorRegisteredLabelState;
        private int _lastActiveHandles = int.MinValue;
        private int _lastCacheHits = int.MinValue;
        private int _lastCacheMisses = int.MinValue;
        private int _lastReleasedHandles = int.MinValue;
        private int _lastVisibleRows = -1;
        private bool _lastLeakVisible;
        private uint _lastLeakHash;
        private ulong _lastLeakBundle;
        private int _lastLeakRefCount;
        private uint[] _rowHashes;
        private int[] _rowRefCounts;
        private ulong[] _rowSlots;
        private int[] _rowTtlTenths;
        private byte[] _rowFlags;

        [MenuItem("HECTON-8/Optimization/Heap Sanitizer Tuner")]
        private static void Open()
        {
            GetWindow<HeapSanitizerTunerWindow>("Heap Sanitizer Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _statusLabel = new Label("AssetLifecycleGovernor: unresolved");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_statusLabel);

            CreateMetricStrip(rootVisualElement);
            CreateTuningControls(rootVisualElement);
            CreateGraphBand(rootVisualElement);
            CreateTrackerRows(rootVisualElement);
            ResetEditorCaches();

            _updateLoop = rootVisualElement.schedule.Execute(UpdateUi).Every(250);
            UpdateUi();
        }

        private void OnDisable()
        {
            _updateLoop?.Pause();
        }

        private void CreateMetricStrip(VisualElement root)
        {
            VisualElement metrics = new VisualElement();
            metrics.style.flexDirection = FlexDirection.Row;
            metrics.style.marginTop = 8f;
            metrics.style.marginBottom = 8f;
            root.Add(metrics);

            _activeLabel = CreateMetricLabel(metrics);
            _hitsLabel = CreateMetricLabel(metrics);
            _missesLabel = CreateMetricLabel(metrics);
            _releasedLabel = CreateMetricLabel(metrics);
        }

        private static Label CreateMetricLabel(VisualElement parent)
        {
            Label label = new Label("0");
            label.style.minWidth = 130f;
            label.style.marginRight = 8f;
            parent.Add(label);
            return label;
        }

        private void CreateTuningControls(VisualElement root)
        {
            _ttlSlider = new Slider("Base TTL", 10f, 300f);
            _ttlSlider.RegisterValueChangedCallback(OnTtlChanged);
            root.Add(_ttlSlider);

            _vramSlider = new Slider("VRAM Panic", 0.5f, 0.99f);
            _vramSlider.RegisterValueChangedCallback(OnVramChanged);
            root.Add(_vramSlider);

            VisualElement csv = new VisualElement();
            csv.style.flexDirection = FlexDirection.Row;
            csv.style.marginTop = 6f;
            root.Add(csv);

            _csvPathField = new TextField();
            _csvPathField.value = "Assets/_Project/Data/asset_cache_profiles.csv";
            _csvPathField.style.flexGrow = 1f;
            csv.Add(_csvPathField);

            Button loadButton = new Button(OnLoadCsvClicked) { text = "Load CSV" };
            loadButton.style.width = 96f;
            csv.Add(loadButton);
        }

        private void CreateGraphBand(VisualElement root)
        {
            _activeBars = new VisualElement[GraphSamples]; // COLD ALLOC: VisualElement[64] - editor graph bars - owner: HeapSanitizerTunerWindow
            _hitBars = new VisualElement[GraphSamples]; // COLD ALLOC: VisualElement[64] - editor graph bars - owner: HeapSanitizerTunerWindow
            _vramBars = new VisualElement[GraphSamples]; // COLD ALLOC: VisualElement[64] - editor graph bars - owner: HeapSanitizerTunerWindow

            root.Add(CreateGraph("Active Handles", _activeBars, new Color(0.1f, 0.65f, 0.9f)));
            root.Add(CreateGraph("Cache Hit Ratio", _hitBars, new Color(0.15f, 0.8f, 0.35f)));
            root.Add(CreateGraph("VRAM Pressure", _vramBars, new Color(0.95f, 0.25f, 0.15f)));
        }

        private static VisualElement CreateGraph(string title, VisualElement[] bars, Color color)
        {
            VisualElement group = new VisualElement();
            group.style.marginTop = 6f;
            group.Add(new Label(title));

            VisualElement graph = new VisualElement();
            graph.style.height = 76f;
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.alignItems = Align.FlexEnd;
            group.Add(graph);

            for (int i = 0; i < bars.Length; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 4f;
                bar.style.height = 2f;
                bar.style.marginRight = 1f;
                bar.style.backgroundColor = color;
                bars[i] = bar;
                graph.Add(bar);
            }

            return group;
        }

        private void CreateTrackerRows(VisualElement root)
        {
            _leakBanner = new Label("LEAK SUSPECT");
            _leakBanner.style.display = DisplayStyle.None;
            _leakBanner.style.unityFontStyleAndWeight = FontStyle.Bold;
            _leakBanner.style.fontSize = 22f;
            _leakBanner.style.color = Color.white;
            _leakBanner.style.backgroundColor = new Color(0.72f, 0.02f, 0.02f);
            _leakBanner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _leakBanner.style.height = 42f;
            _leakBanner.style.marginTop = 8f;
            root.Add(_leakBanner);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.minHeight = 180f;
            scroll.style.marginTop = 8f;
            root.Add(scroll);

            _trackerRows = new Label[MaxRows]; // COLD ALLOC: Label[64] - editor tracker rows - owner: HeapSanitizerTunerWindow
            _rowHashes = new uint[MaxRows]; // COLD ALLOC: uint[64] - editor row cache - owner: HeapSanitizerTunerWindow
            _rowRefCounts = new int[MaxRows]; // COLD ALLOC: int[64] - editor row cache - owner: HeapSanitizerTunerWindow
            _rowSlots = new ulong[MaxRows]; // COLD ALLOC: ulong[64] - editor row cache - owner: HeapSanitizerTunerWindow
            _rowTtlTenths = new int[MaxRows]; // COLD ALLOC: int[64] - editor row cache - owner: HeapSanitizerTunerWindow
            _rowFlags = new byte[MaxRows]; // COLD ALLOC: byte[64] - editor row cache - owner: HeapSanitizerTunerWindow
            for (int i = 0; i < MaxRows; i++)
            {
                Label row = new Label();
                row.style.height = 18f;
                _trackerRows[i] = row;
                scroll.Add(row);
            }
        }

        private void UpdateUi()
        {
            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor == null)
            {
                SetStatusRegistered(false);
                SetRowsVisible(0);
                return;
            }

            SetStatusRegistered(true);
            int active = governor.GetHeapSanitizerActiveHandleCount();
            int hits = governor.GetHeapSanitizerCacheHitCount();
            int misses = governor.GetHeapSanitizerCacheMissCount();
            int released = governor.GetHeapSanitizerOrphanedReleaseCount();
            if (active != _lastActiveHandles)
            {
                _activeLabel.text = "Active: " + active.ToString();
                _lastActiveHandles = active;
            }

            if (hits != _lastCacheHits)
            {
                _hitsLabel.text = "Hits: " + hits.ToString();
                _lastCacheHits = hits;
            }

            if (misses != _lastCacheMisses)
            {
                _missesLabel.text = "Misses: " + misses.ToString();
                _lastCacheMisses = misses;
            }

            if (released != _lastReleasedHandles)
            {
                _releasedLabel.text = "Released: " + released.ToString();
                _lastReleasedHandles = released;
            }

            _isUpdatingControls = true;
            _ttlSlider.SetValueWithoutNotify(governor.GetHeapSanitizerBaseTtlSeconds());
            _vramSlider.SetValueWithoutNotify(governor.GetHeapSanitizerVramPanicThreshold());
            _isUpdatingControls = false;

            UpdateGraphs(governor);
            UpdateLeakBanner(governor);
            UpdateTrackerRows(governor);
        }

        private void UpdateGraphs(AssetLifecycleGovernor governor)
        {
            uint maxActive = 1u;
            for (int i = 0; i < GraphSamples; i++)
            {
                if (governor.TryGetHeapSanitizerTelemetryAt(i, out uint active, out _, out _, out _, out _) && active > maxActive)
                    maxActive = active;
            }

            for (int i = 0; i < GraphSamples; i++)
            {
                int barIndex = GraphSamples - 1 - i;
                if (!governor.TryGetHeapSanitizerTelemetryAt(i, out uint active, out uint hits, out uint misses, out float vram, out _))
                {
                    SetBar(_activeBars[barIndex], 0f);
                    SetBar(_hitBars[barIndex], 0f);
                    SetBar(_vramBars[barIndex], 0f);
                    continue;
                }

                uint total = hits + misses;
                float hitRatio = total > 0u ? hits / (float)total : 0f;
                SetBar(_activeBars[barIndex], active / (float)maxActive);
                SetBar(_hitBars[barIndex], Mathf.Clamp01(hitRatio));
                SetBar(_vramBars[barIndex], Mathf.Clamp01(vram));
            }
        }

        private static void SetBar(VisualElement bar, float value)
        {
            bar.style.height = Mathf.Lerp(2f, 72f, Mathf.Clamp01(value));
        }

        private void UpdateLeakBanner(AssetLifecycleGovernor governor)
        {
            if (governor.TryGetHeapSanitizerLeakSuspectAt(0, out uint hash, out ulong bundle, out int refCount))
            {
                if (!_lastLeakVisible)
                    _leakBanner.style.display = DisplayStyle.Flex;
                if (!_lastLeakVisible || hash != _lastLeakHash || bundle != _lastLeakBundle || refCount != _lastLeakRefCount)
                {
                    _leakBanner.text = "LEAK SUSPECT  asset=0x" + hash.ToString("X8") +
                                       " bundle=0x" + bundle.ToString("X16") +
                                       " ref=" + refCount.ToString();
                    _lastLeakHash = hash;
                    _lastLeakBundle = bundle;
                    _lastLeakRefCount = refCount;
                }

                _lastLeakVisible = true;
                return;
            }

            if (_lastLeakVisible)
                _leakBanner.style.display = DisplayStyle.None;
            _lastLeakVisible = false;
        }

        private void UpdateTrackerRows(AssetLifecycleGovernor governor)
        {
            int visible = 0;
            for (int i = 0; i < MaxRows; i++)
            {
                if (!governor.TryGetHeapSanitizerTrackerAt(i, out AssetTrackerDTO tracker, out float ttl, out byte flags))
                    break;

                int ttlTenths = Mathf.RoundToInt(ttl * 10f);
                if (_rowHashes[i] != tracker.AssetHash ||
                    _rowRefCounts[i] != tracker.ReferenceCount ||
                    _rowSlots[i] != tracker.HandlePointer ||
                    _rowTtlTenths[i] != ttlTenths ||
                    _rowFlags[i] != flags)
                {
                    _trackerRows[i].text = "0x" + tracker.AssetHash.ToString("X8") +
                                           " | ref " + tracker.ReferenceCount.ToString() +
                                           " | slot " + tracker.HandlePointer.ToString() +
                                           " | ttl " + (ttlTenths * 0.1f).ToString("0.0", CultureInfo.InvariantCulture) +
                                           " | flags 0x" + flags.ToString("X2");
                    _rowHashes[i] = tracker.AssetHash;
                    _rowRefCounts[i] = tracker.ReferenceCount;
                    _rowSlots[i] = tracker.HandlePointer;
                    _rowTtlTenths[i] = ttlTenths;
                    _rowFlags[i] = flags;
                }

                visible++;
            }

            SetRowsVisible(visible);
        }

        private void SetStatusRegistered(bool registered)
        {
            if (_statusLabelInitialized && _governorRegisteredLabelState == registered)
                return;

            _statusLabel.text = registered
                ? "AssetLifecycleGovernor: registered"
                : "AssetLifecycleGovernor: not registered";
            _governorRegisteredLabelState = registered;
            _statusLabelInitialized = true;
        }

        private void ResetEditorCaches()
        {
            _statusLabelInitialized = false;
            _lastActiveHandles = int.MinValue;
            _lastCacheHits = int.MinValue;
            _lastCacheMisses = int.MinValue;
            _lastReleasedHandles = int.MinValue;
            _lastVisibleRows = -1;
            _lastLeakVisible = false;
            _lastLeakHash = 0u;
            _lastLeakBundle = 0UL;
            _lastLeakRefCount = 0;
            if (_rowHashes == null)
                return;

            for (int i = 0; i < _rowHashes.Length; i++)
            {
                _rowHashes[i] = uint.MaxValue;
                _rowRefCounts[i] = int.MinValue;
                _rowSlots[i] = ulong.MaxValue;
                _rowTtlTenths[i] = int.MinValue;
                _rowFlags[i] = byte.MaxValue;
            }
        }

        private void SetRowsVisible(int visible)
        {
            if (_trackerRows == null)
                return;
            if (_lastVisibleRows == visible)
                return;

            for (int i = 0; i < _trackerRows.Length; i++)
                _trackerRows[i].style.display = i < visible ? DisplayStyle.Flex : DisplayStyle.None;
            _lastVisibleRows = visible;
        }

        private void OnTtlChanged(ChangeEvent<float> evt)
        {
            if (_isUpdatingControls)
                return;

            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
                governor.SetHeapSanitizerBaseTtlSeconds(evt.newValue);
        }

        private void OnVramChanged(ChangeEvent<float> evt)
        {
            if (_isUpdatingControls)
                return;

            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
                governor.SetHeapSanitizerVramPanicThreshold(evt.newValue);
        }

        private void OnLoadCsvClicked()
        {
            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
                governor.TryParseAssetCacheRulesCsv(_csvPathField.value);
        }
    }
}
#endif
