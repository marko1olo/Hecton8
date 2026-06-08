using System.Text;
using Hecton8.Audio;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Audio.Editor
{
    public sealed class VocalWarningQueueTunerWindow : EditorWindow
    {
        private const int GraphSampleCount = 64;
        private const double RefreshSeconds = 0.25;

        private readonly float[] _prioritySamples = new float[GraphSampleCount];
        private readonly float[] _alarmMaskSamples = new float[GraphSampleCount];
        private readonly StringBuilder _statusBuilder = new StringBuilder(256);

        private Label _status;
        private Slider _hullPriority;
        private Slider _oxygenPriority;
        private Slider _powerPriority;
        private Slider _interruptThreshold;
        private SliderInt _mockCount;
        private VisualElement _waterfall;
        private double _nextRefreshTime;
        private bool _refreshing;

        [MenuItem("Hecton8/Audio/Vocal Warning Alarm Mask Tuner")]
        public static void Open()
        {
            VocalWarningQueueTunerWindow window = GetWindow<VocalWarningQueueTunerWindow>();
            window.titleContent = new GUIContent("Vocal Alarm Mask");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorHeartbeat;
            EditorApplication.update += OnEditorHeartbeat;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorHeartbeat;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _hullPriority = BuildSlider("BasePriority_Hull", 100f, 1600f, 1000f);
            _oxygenPriority = BuildSlider("BasePriority_Oxygen", 100f, 1400f, 820f);
            _powerPriority = BuildSlider("BasePriority_Power", 10f, 400f, 120f);
            _interruptThreshold = BuildSlider("InterruptionThreshold", 0f, 600f, 180f);
            _mockCount = new SliderInt("Mock threats", 1, 50) { value = 50 };
            _waterfall = BuildGraphElement(112f);
            _waterfall.generateVisualContent += DrawWaterfall;
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;

            root.Add(_hullPriority);
            root.Add(_oxygenPriority);
            root.Add(_powerPriority);
            root.Add(_interruptThreshold);
            root.Add(_mockCount);
            root.Add(_waterfall);

            Button powerButton = new Button(() => InjectWarning(VocalWarningId.PowerLow, 0.35f))
            {
                text = "Inject Power Low"
            };
            root.Add(powerButton);

            Button toxicityButton = new Button(() => InjectWarning(VocalWarningId.Toxicity, 0.55f))
            {
                text = "Inject Toxicity"
            };
            root.Add(toxicityButton);

            Button hullButton = new Button(() => InjectWarning(VocalWarningId.HullBreach, 1f))
            {
                text = "Inject Water Breach"
            };
            root.Add(hullButton);

            Button mockButton = new Button(InjectMockThreats)
            {
                text = "Generate Mock Threats"
            };
            root.Add(mockButton);
            root.Add(_status);

            _hullPriority.RegisterValueChangedCallback(_ => PublishTuning());
            _oxygenPriority.RegisterValueChangedCallback(_ => PublishTuning());
            _powerPriority.RegisterValueChangedCallback(_ => PublishTuning());
            _interruptThreshold.RegisterValueChangedCallback(_ => PublishTuning());
            RefreshStatus();
        }

        private static Slider BuildSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = math.clamp(value, min, max),
                showInputField = true
            };
        }

        private static VisualElement BuildGraphElement(float height)
        {
            VisualElement element = new VisualElement();
            element.style.height = height;
            element.style.marginTop = 8f;
            element.style.marginBottom = 8f;
            element.style.backgroundColor = new StyleColor(new Color(0.025f, 0.032f, 0.035f, 1f));
            return element;
        }

        private static VocalWarningSystem ResolveRuntime(bool requireReady = true)
        {
            VocalWarningSystem runtime = GlobalRegistry.VocalWarnings as VocalWarningSystem;
            if (!requireReady)
                return runtime;

            return runtime != null && runtime.IsVocalWarningRuntimeReady ? runtime : null;
        }

        private void OnEditorHeartbeat()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshSeconds;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            VocalWarningSystem runtime = ResolveRuntime(false);
            if (runtime == null)
            {
                if (_status != null)
                    _status.text = "No VocalWarningSystem in loaded scene.";
                return;
            }

            bool ready = runtime.IsVocalWarningRuntimeReady;
            _refreshing = true;
            if (ready && runtime.EditorTryReadTuning(out VocalWarningSystem.VocalWarningTuningDTO tuning))
            {
                _hullPriority?.SetValueWithoutNotify(tuning.BasePriorityHull);
                _oxygenPriority?.SetValueWithoutNotify(tuning.BasePriorityOxygen);
                _powerPriority?.SetValueWithoutNotify(tuning.BasePriorityPower);
                _interruptThreshold?.SetValueWithoutNotify(tuning.InterruptionThreshold);
            }

            for (int i = 0; i < GraphSampleCount; i++)
            {
                if (ready && runtime.EditorTryGetTelemetrySample(GraphSampleCount - 1 - i, out VocalWarningSystem.VocalWarningTelemetrySnapshot sample))
                {
                    _prioritySamples[i] = math.saturate(sample.CurrentPriorityScore * (1f / 1400f));
                    _alarmMaskSamples[i] = math.saturate(sample.ActivePriorityCount * (1f / math.max(1f, runtime.EditorQueueCapacity)));
                }
                else
                {
                    _prioritySamples[i] = 0f;
                    _alarmMaskSamples[i] = 0f;
                }
            }

            if (_status != null)
            {
                _statusBuilder.Clear();
                _statusBuilder.Append("Initialized: ").Append(runtime.IsInitialized).Append('\n');
                _statusBuilder.Append("Ready owner: ").Append(ready).Append('\n');
                _statusBuilder.Append("Active alarm slots: ").Append(runtime.PendingCount).Append('/').Append(runtime.EditorQueueCapacity).Append('\n');
                _statusBuilder.Append("Current ID: ").Append(runtime.CurrentWarningId).Append('\n');
                _statusBuilder.Append("Current priority: ").Append(runtime.EditorCurrentPriorityScore.ToString("0.0")).Append('\n');
                _statusBuilder.Append("Burst micros: ").Append(runtime.EditorLastBurstExecutionMicros.ToString("0.0")).Append('\n');
                _statusBuilder.Append("DTO size: ").Append(VocalWarningSystem.EditorVocalWarningDtoSizeBytes).Append(" bytes").Append('\n');
                _statusBuilder.Append("Tuning DTO size: ").Append(VocalWarningSystem.EditorVocalWarningTuningDtoSizeBytes).Append(" bytes").Append('\n');
                _statusBuilder.Append("Direction hash: 0x").Append(runtime.EditorLastDirectionHash.ToString("X4"));
                _status.text = _statusBuilder.ToString();
            }

            _refreshing = false;
            _waterfall?.MarkDirtyRepaint();
        }

        private void PublishTuning()
        {
            if (_refreshing || !EditorApplication.isPlaying)
                return;

            VocalWarningSystem runtime = ResolveRuntime();
            if (runtime == null || !runtime.EditorTryReadTuning(out VocalWarningSystem.VocalWarningTuningDTO tuning))
                return;

            tuning.BasePriorityHull = _hullPriority != null ? _hullPriority.value : tuning.BasePriorityHull;
            tuning.BasePriorityOxygen = _oxygenPriority != null ? _oxygenPriority.value : tuning.BasePriorityOxygen;
            tuning.BasePriorityPower = _powerPriority != null ? _powerPriority.value : tuning.BasePriorityPower;
            tuning.InterruptionThreshold = _interruptThreshold != null ? _interruptThreshold.value : tuning.InterruptionThreshold;
            tuning.Revision++;
            runtime.EditorTryWriteTuning(in tuning);
        }

        private void InjectWarning(VocalWarningId warningId, float severity)
        {
            VocalWarningSystem runtime = ResolveRuntime();
            if (runtime == null)
                return;

            byte flags = warningId == VocalWarningId.HullBreach
                ? VocalWarningSignalFlags.HabitatIntegrityCompromised
                : (byte)0;
            runtime.TryQueueWarning((byte)warningId, severity, 0f, flags, 0x53483352u);
            RefreshStatus();
        }

        private void InjectMockThreats()
        {
            VocalWarningSystem runtime = ResolveRuntime();
            if (runtime == null)
                return;

            runtime.EditorInjectMockThreats(_mockCount != null ? _mockCount.value : 50);
            RefreshStatus();
        }

        private void DrawWaterfall(MeshGenerationContext context)
        {
            if (_waterfall == null)
                return;

            Rect rect = _waterfall.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawSeries(painter, rect, _prioritySamples, new Color(0.95f, 0.24f, 0.12f, 0.95f), 0f);
            DrawSeries(painter, rect, _alarmMaskSamples, new Color(0.18f, 0.7f, 1f, 0.85f), 8f);
        }

        private static void DrawSeries(Painter2D painter, Rect rect, float[] samples, Color color, float yOffset)
        {
            if (samples == null || samples.Length <= 1)
                return;

            painter.lineWidth = 1.35f;
            painter.strokeColor = color;
            painter.BeginPath();
            for (int i = 0; i < samples.Length; i++)
            {
                float x = rect.x + rect.width * (i / math.max(1f, samples.Length - 1f));
                float y = rect.yMax - yOffset - math.saturate(samples[i]) * math.max(1f, rect.height - 12f);
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }
    }
}
