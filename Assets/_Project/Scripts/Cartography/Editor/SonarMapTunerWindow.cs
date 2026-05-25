#if UNITY_EDITOR
using Hecton8.Cartography;
using Hecton8.PDA;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Cartography.Editor
{
    public sealed class SonarMapTunerWindow : EditorWindow
    {
        private ObjectField _runtimeField;
        private Slider _voxelSlider;
        private Slider _radiusSlider;
        private Slider _surfaceSlider;
        private Slider _glowSlider;
        private Slider _qualitySlider;
        private Label _statusLabel;
        private TelemetryGraphElement _graph;
        private PlayerExplorationTracker _runtime;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/Cartography/Sonar Map Tuner")]
        private static void Open()
        {
            GetWindow<SonarMapTunerWindow>("Sonar Map Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _runtimeField = new ObjectField("Runtime")
            {
                objectType = typeof(PlayerExplorationTracker),
                allowSceneObjects = true
            };
            _runtimeField.RegisterValueChangedCallback(evt =>
            {
                _runtime = evt.newValue as PlayerExplorationTracker;
                RefreshFromRuntime();
            });
            root.Add(_runtimeField);

            Button findButton = new Button(FindRuntime) { text = "Find Runtime" };
            root.Add(findButton);

            _voxelSlider = CreateSlider("Voxel Size Meters", CartographyGridConstants.MacroCellSizeMeters, CartographyGridConstants.MaxDesignerVoxelSizeMeters);
            _radiusSlider = CreateSlider("Sonar Ping Radius", CartographyGridConstants.MacroCellSizeMeters, CartographyGridConstants.MaxRevealRadiusMeters);
            _surfaceSlider = CreateSlider("Surface Thickness", 0.25f, 8f);
            _glowSlider = CreateSlider("Visual Overkill Glow", 0f, 8f);
            _qualitySlider = CreateSlider("Global Quality Weight", 0f, 1f);
            root.Add(_voxelSlider);
            root.Add(_radiusSlider);
            root.Add(_surfaceSlider);
            root.Add(_glowSlider);
            root.Add(_qualitySlider);

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 8f;
            buttonRow.Add(new Button(GenerateMockData) { text = "Generate Mock Sonar" });
            buttonRow.Add(new Button(ReloadCsv) { text = "Reload Scanner CSV" });
            buttonRow.Add(new Button(BuildRleRuns) { text = "Build RLE Runs" });
            root.Add(buttonRow);

            _statusLabel = new Label();
            _statusLabel.style.marginTop = 8f;
            root.Add(_statusLabel);
            _graph = new TelemetryGraphElement();
            _graph.style.height = 96f;
            _graph.style.marginTop = 8f;
            root.Add(_graph);
            RefreshFromRuntime();
        }

        private void OnFocus()
        {
            RefreshFromRuntime();
        }

        private void OnInspectorUpdate()
        {
            RefreshFromRuntime();
        }

        private Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.RegisterValueChangedCallback(_ => WriteTuning());
            return slider;
        }

        private void FindRuntime()
        {
            _runtime = FindAnyObjectByType<PlayerExplorationTracker>();
            if (_runtimeField != null)
                _runtimeField.SetValueWithoutNotify(_runtime);
            RefreshFromRuntime();
        }

        private void RefreshFromRuntime()
        {
            if (_runtime == null)
                _runtime = _runtimeField != null ? _runtimeField.value as PlayerExplorationTracker : null;

            CartographyTuningDTO tuning = default;
            bool ready = Application.isPlaying &&
                         _runtime != null &&
                         _runtime.TryGetCartographyTuning(out tuning);

            SetControlsEnabled(ready);
            if (!ready)
            {
                if (_statusLabel != null)
                    _statusLabel.text = Application.isPlaying
                        ? "Select an active PlayerExplorationTracker with resolved GlobalDataVault cartography buffers."
                        : "Enter Play Mode to edit DataVault-backed cartography memory.";
                return;
            }

            _suppressCallbacks = true;
            try
            {
                _radiusSlider.SetValueWithoutNotify(tuning.SonarPingRadiusMeters);
                _voxelSlider.SetValueWithoutNotify(tuning.CellSizeMeters);
                _surfaceSlider.SetValueWithoutNotify(tuning.SurfaceThicknessMeters);
                _glowSlider.SetValueWithoutNotify(tuning.VisualGlowIntensity);
                _qualitySlider.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            }
            finally
            {
                _suppressCallbacks = false;
            }

            if (_statusLabel != null)
            {
                if (_graph != null && _runtime.TryGetLatestCartographyTelemetry(out CartographyTelemetryEntry telemetry))
                    _graph.PushSample(telemetry.DiscoveredVoxelCount, telemetry.RleCompressionPermille);

                _statusLabel.text = "Vault buffers active. Upload cadence " +
                                    ((int)tuning.UploadCadenceFrames).ToString() +
                                    " frames. Tuning revision " +
                                    tuning.Revision.ToString() +
                                    ".";
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (_voxelSlider != null)
                _voxelSlider.SetEnabled(enabled);
            if (_radiusSlider != null)
                _radiusSlider.SetEnabled(enabled);
            if (_surfaceSlider != null)
                _surfaceSlider.SetEnabled(enabled);
            if (_glowSlider != null)
                _glowSlider.SetEnabled(enabled);
            if (_qualitySlider != null)
                _qualitySlider.SetEnabled(enabled);
        }

        private void WriteTuning()
        {
            if (_suppressCallbacks || _runtime == null || !Application.isPlaying)
                return;

            CartographyTuningDTO tuning = new CartographyTuningDTO
            {
                SonarPingRadiusMeters = _radiusSlider.value,
                SurfaceThicknessMeters = _surfaceSlider.value,
                VisualGlowIntensity = _glowSlider.value,
                GlobalQualityWeight = math.saturate(_qualitySlider.value),
                CellSizeMeters = _voxelSlider.value,
                UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(_qualitySlider.value),
                Flags = 0u
            };
            if (_runtime.TrySetCartographyTuning(in tuning))
            {
                EditorUtility.SetDirty(_runtime);
                RefreshFromRuntime();
            }
        }

        private void GenerateMockData()
        {
            if (_runtime != null && Application.isPlaying && _runtime.GenerateMockExplorationData())
                _statusLabel.text = "Mock sonar shell data written into Vault bitmasks.";
        }

        private void ReloadCsv()
        {
            if (_runtime == null || !Application.isPlaying)
                return;

            string projectRoot = Application.dataPath + "/..";
            if (_runtime.TryLoadScannerProfilesCsvForEditor(projectRoot, out int rows))
                _statusLabel.text = "Scanner CSV rows applied: " + rows.ToString();
            else
                _statusLabel.text = "Scanner CSV not applied.";
        }

        private void BuildRleRuns()
        {
            if (_runtime == null || !Application.isPlaying)
                return;

            if (_runtime.TryBuildCartographyRleRuns(out _, out int runs))
                _statusLabel.text = "RLE runs staged in Vault: " + runs.ToString();
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private const int Capacity = 128;
            private readonly float[] _exploredSamples = new float[Capacity];
            private readonly float[] _compressionSamples = new float[Capacity];
            private int _cursor;
            private int _count;
            private float _maxExplored = 1f;
            private float _maxCompression = 1000f;

            public TelemetryGraphElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void PushSample(uint exploredVoxels, uint compressionPermille)
            {
                float explored = (float)exploredVoxels;
                float compression = (float)compressionPermille;
                _exploredSamples[_cursor] = explored;
                _compressionSamples[_cursor] = compression;
                _cursor = (_cursor + 1) & (Capacity - 1);
                _count = math.min(_count + 1, Capacity);
                _maxExplored = math.max(_maxExplored, explored);
                _maxCompression = math.max(_maxCompression, compression);
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect r = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.06f, 0.45f, 0.72f, 0.55f);
                DrawPolyline(painter, r, _exploredSamples, math.max(1f, _maxExplored));
                painter.strokeColor = new Color(0.08f, 0.9f, 0.32f, 0.7f);
                DrawPolyline(painter, r, _compressionSamples, math.max(1f, _maxCompression));
            }

            private void DrawPolyline(Painter2D painter, Rect rect, float[] samples, float maxValue)
            {
                if (_count < 2 || rect.width <= 1f || rect.height <= 1f)
                    return;

                int start = (_cursor - _count + Capacity) & (Capacity - 1);
                float step = rect.width / math.max(1, _count - 1);
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    int index = (start + i) & (Capacity - 1);
                    float normalized = math.saturate(samples[index] / maxValue);
                    Vector2 point = new Vector2(
                        rect.xMin + (i * step),
                        rect.yMax - (normalized * rect.height));
                    if (i == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }

                painter.Stroke();
            }
        }
    }
}
#endif
