#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.Editor
{
    public sealed class TerrainChunkPagerTunerWindow : EditorWindow
    {
        private const int GraphSampleCount = 96;

        private Label _statusLabel;
        private Label _graphLabel;
        private VisualElement[] _bars;
        private float[] _latencySamples;
        private int[] _activeSamples;
        private Slider _quality;
        private Slider _minRing;
        private Slider _maxRing;
        private Slider _evictionHysteresis;
        private Slider _safeLatency;
        private Slider _criticalLatency;
        private SliderInt _maxQueuedLoads;
        private SliderInt _maxCommits;
        private SliderInt _chunkKiB;
        private SliderInt _mockDelayMin;
        private SliderInt _mockDelayMax;

        [MenuItem("Tools/Hecton8/World/Terrain Chunk Pager Tuner")]
        public static void Open()
        {
            TerrainChunkPagerTunerWindow window = GetWindow<TerrainChunkPagerTunerWindow>();
            window.titleContent = new UnityEngine.GUIContent("Terrain Pager");
            window.minSize = new UnityEngine.Vector2(360f, 420f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _statusLabel = new Label("No active TerrainChunkPagerRuntime.");
            root.Add(_statusLabel);

            _graphLabel = new Label("Latency waterfall");
            _graphLabel.style.marginTop = 8f;
            root.Add(_graphLabel);
            CreateWaterfall(root);

            _quality = AddSlider(root, "GlobalQualityWeight", 0f, 1f);
            _minRing = AddSlider(root, "Min Ring Radius", 1f, TerrainChunkPagerConstants.MaxEvaluatedRingRadius);
            _maxRing = AddSlider(root, "Max Ring Radius", 1f, TerrainChunkPagerConstants.MaxEvaluatedRingRadius);
            _evictionHysteresis = AddSlider(root, "Eviction Hysteresis", 0.5f, 4f);
            _safeLatency = AddSlider(root, "Safe Latency ms", 1f, 500f);
            _criticalLatency = AddSlider(root, "Critical Latency ms", 2f, 2000f);
            _maxQueuedLoads = AddSliderInt(root, "Max Queued Loads", 1, 64);
            _maxCommits = AddSliderInt(root, "Max Commits", 1, 8);
            _chunkKiB = AddSliderInt(root, "Chunk KiB", 4, 4096);
            _chunkKiB.SetEnabled(false);
            _mockDelayMin = AddSliderInt(root, "Mock Delay Min ms", 0, 1000);
            _mockDelayMax = AddSliderInt(root, "Mock Delay Max ms", 0, 3000);

            Button refresh = new Button(RefreshFromRuntime) { text = "Refresh" };
            root.Add(refresh);

            RegisterCallbacks();
            RefreshFromRuntime();
            root.schedule.Execute(UpdateWaterfall).Every(250);
        }

        private void CreateWaterfall(VisualElement root)
        {
            _bars = new VisualElement[GraphSampleCount];
            _latencySamples = new float[GraphSampleCount];
            _activeSamples = new int[GraphSampleCount];

            VisualElement graph = new VisualElement();
            graph.style.height = 84f;
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.alignItems = Align.FlexEnd;
            graph.style.marginBottom = 8f;
            graph.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            root.Add(graph);

            for (int i = 0; i < GraphSampleCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginLeft = 1f;
                bar.style.height = 1f;
                bar.style.backgroundColor = new Color(0.18f, 0.45f, 0.72f, 1f);
                _bars[i] = bar;
                graph.Add(bar);
            }
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.style.marginTop = 6f;
            root.Add(slider);
            return slider;
        }

        private static SliderInt AddSliderInt(VisualElement root, string label, int min, int max)
        {
            SliderInt slider = new SliderInt(label, min, max) { showInputField = true };
            slider.style.marginTop = 6f;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _quality.RegisterValueChangedCallback(_ => WriteToRuntime());
            _minRing.RegisterValueChangedCallback(_ => WriteToRuntime());
            _maxRing.RegisterValueChangedCallback(_ => WriteToRuntime());
            _evictionHysteresis.RegisterValueChangedCallback(_ => WriteToRuntime());
            _safeLatency.RegisterValueChangedCallback(_ => WriteToRuntime());
            _criticalLatency.RegisterValueChangedCallback(_ => WriteToRuntime());
            _maxQueuedLoads.RegisterValueChangedCallback(_ => WriteToRuntime());
            _maxCommits.RegisterValueChangedCallback(_ => WriteToRuntime());
            _mockDelayMin.RegisterValueChangedCallback(_ => WriteToRuntime());
            _mockDelayMax.RegisterValueChangedCallback(_ => WriteToRuntime());
        }

        private void RefreshFromRuntime()
        {
            if (!TerrainChunkPagerRuntime.TryReadTuning(out TerrainChunkPagerTuningDTO tuning))
            {
                _statusLabel.text = "No active TerrainChunkPagerRuntime.";
                SetEnabled(false);
                return;
            }

            SetEnabled(true);
            _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _minRing.SetValueWithoutNotify(tuning.MinRingRadius);
            _maxRing.SetValueWithoutNotify(tuning.MaxRingRadius);
            _evictionHysteresis.SetValueWithoutNotify(tuning.EvictionHysteresisSectors);
            _safeLatency.SetValueWithoutNotify(tuning.SafeLatencyMs);
            _criticalLatency.SetValueWithoutNotify(tuning.CriticalLatencyMs);
            _maxQueuedLoads.SetValueWithoutNotify(tuning.MaxQueuedLoads);
            _maxCommits.SetValueWithoutNotify(tuning.MaxCommitsPerVisualSync);
            _chunkKiB.SetValueWithoutNotify(UnityEngine.Mathf.Max(4, tuning.ChunkByteCapacity / 1024));
            _mockDelayMin.SetValueWithoutNotify(tuning.WorkerMockDelayMinMs);
            _mockDelayMax.SetValueWithoutNotify(tuning.WorkerMockDelayMaxMs);
            _statusLabel.text = "Active. Effective radius " + tuning.EffectiveRingRadius.ToString("0.00") +
                                ", latency EWMA " + tuning.LatencyEwmaMs.ToString("0.0") + " ms.";
        }

        private void UpdateWaterfall()
        {
            if (_bars == null || !TerrainChunkPagerRuntime.TryReadCounters(out TerrainChunkPagerCountersDTO counters))
                return;

            for (int i = 1; i < GraphSampleCount; i++)
            {
                _latencySamples[i - 1] = _latencySamples[i];
                _activeSamples[i - 1] = _activeSamples[i];
            }

            _latencySamples[GraphSampleCount - 1] = UnityEngine.Mathf.Max(0f, counters.LatencyEwmaMs);
            _activeSamples[GraphSampleCount - 1] = counters.ActiveChunks;
            float maxLatency = 1f;
            int maxActive = 1;
            for (int i = 0; i < GraphSampleCount; i++)
            {
                maxLatency = UnityEngine.Mathf.Max(maxLatency, _latencySamples[i]);
                maxActive = UnityEngine.Mathf.Max(maxActive, _activeSamples[i]);
            }

            for (int i = 0; i < GraphSampleCount; i++)
            {
                float latency01 = UnityEngine.Mathf.Clamp01(_latencySamples[i] / maxLatency);
                float active01 = UnityEngine.Mathf.Clamp01((float)_activeSamples[i] / maxActive);
                _bars[i].style.height = 1f + latency01 * 78f;
                _bars[i].style.backgroundColor = Color.Lerp(
                    new Color(0.16f, 0.48f, 0.72f, 1f),
                    new Color(0.95f, 0.74f, 0.20f, 1f),
                    active01);
            }

            _graphLabel.text = "Latency " + counters.LatencyEwmaMs.ToString("0.0") +
                               " ms | active " + counters.ActiveChunks +
                               " | pending " + counters.PendingRequests;
        }

        private void WriteToRuntime()
        {
            if (!TerrainChunkPagerRuntime.TryReadTuning(out TerrainChunkPagerTuningDTO tuning))
            {
                _statusLabel.text = "No active TerrainChunkPagerRuntime.";
                return;
            }

            tuning.GlobalQualityWeight = _quality.value;
            tuning.MinRingRadius = _minRing.value;
            tuning.MaxRingRadius = _maxRing.value;
            tuning.EvictionHysteresisSectors = _evictionHysteresis.value;
            tuning.SafeLatencyMs = _safeLatency.value;
            tuning.CriticalLatencyMs = _criticalLatency.value;
            tuning.MaxQueuedLoads = _maxQueuedLoads.value;
            tuning.MaxCommitsPerVisualSync = _maxCommits.value;
            tuning.WorkerMockDelayMinMs = _mockDelayMin.value;
            tuning.WorkerMockDelayMaxMs = _mockDelayMax.value;

            if (TerrainChunkPagerRuntime.TryWriteTuning(in tuning))
                RefreshFromRuntime();
        }

        private void SetEnabled(bool enabled)
        {
            _quality?.SetEnabled(enabled);
            _minRing?.SetEnabled(enabled);
            _maxRing?.SetEnabled(enabled);
            _evictionHysteresis?.SetEnabled(enabled);
            _safeLatency?.SetEnabled(enabled);
            _criticalLatency?.SetEnabled(enabled);
            _maxQueuedLoads?.SetEnabled(enabled);
            _maxCommits?.SetEnabled(enabled);
            _chunkKiB?.SetEnabled(false);
            _mockDelayMin?.SetEnabled(enabled);
            _mockDelayMax?.SetEnabled(enabled);
        }
    }
}
#endif
