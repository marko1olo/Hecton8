#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Hecton8.Audio.Synthesis;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Audio.Synthesis.Editor
{
    public sealed class AbyssalSynthTunerWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.1;
        private const int GraphSamples = 3600;
        private const int OscilloscopeSamples = 256;

        private readonly float[] _tensionHistory = new float[GraphSamples];
        private readonly float[] _voiceHistory = new float[GraphSamples];
        private int _historyCursor;
        private double _nextRefreshTime;
        private DynamicMusicSynthTuningDTO _tuning;
        private AudioDSPTelemetryEntry _telemetry;
        private DynamicMusicSharedStateDTO _sharedState;
        private Slider _baseDensitySlider;
        private Slider _tensionMultiplierSlider;
        private Slider _lfoFrequencySlider;
        private Slider _qualityMinSlider;
        private Slider _qualityMaxSlider;
        private Slider _basePitchSlider;
        private Slider _grainSizeSlider;
        private Slider _baseVolumeSlider;
        private Slider _stereoWidthSlider;
        private Label _statusLabel;
        private VisualElement _oscilloscope;
        private VisualElement _historyGraph;

        [MenuItem("Hecton8/Audio/Abyssal Synth Tuner")]
        public static void Open()
        {
            GetWindow<AbyssalSynthTunerWindow>("Abyssal Synth");
        }

        [InitializeOnLoadMethod]
        private static void ValidateLayoutOnLoad()
        {
            ValidateSynthVoiceLayout(true);
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            ValidateSynthVoiceLayout(false);
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _baseDensitySlider = BuildSlider("Base Grain Density", 1f, 128f, DefaultTuning().BaseGrainDensity);
            _tensionMultiplierSlider = BuildSlider("Tension Multiplier", 0f, 4f, DefaultTuning().TensionMultiplier);
            _lfoFrequencySlider = BuildSlider("LFO Frequency", 0.01f, 12f, DefaultTuning().LfoFrequency);
            _qualityMinSlider = BuildSlider("Quality Min", 0f, 1f, 0f);
            _qualityMaxSlider = BuildSlider("Quality Max", 0f, 1f, 1f);
            _basePitchSlider = BuildSlider("Base Pitch", 8f, 220f, DefaultTuning().BasePitchHz);
            _grainSizeSlider = BuildSlider("Grain Size", 0.01f, 0.5f, DefaultTuning().GrainSizeSeconds);
            _baseVolumeSlider = BuildSlider("Base Volume", 0f, 1f, DefaultTuning().BaseVolume);
            _stereoWidthSlider = BuildSlider("Stereo Width", 0f, 1f, DefaultTuning().StereoWidth);
            _statusLabel = new Label("Runtime unavailable.");

            _oscilloscope = BuildGraphElement(96f);
            _oscilloscope.generateVisualContent += DrawOscilloscope;
            _historyGraph = BuildGraphElement(96f);
            _historyGraph.generateVisualContent += DrawHistoryGraph;

            rootVisualElement.Add(_baseDensitySlider);
            rootVisualElement.Add(_tensionMultiplierSlider);
            rootVisualElement.Add(_lfoFrequencySlider);
            rootVisualElement.Add(_qualityMinSlider);
            rootVisualElement.Add(_qualityMaxSlider);
            rootVisualElement.Add(_basePitchSlider);
            rootVisualElement.Add(_grainSizeSlider);
            rootVisualElement.Add(_baseVolumeSlider);
            rootVisualElement.Add(_stereoWidthSlider);
            rootVisualElement.Add(new Button(ReloadCsv) { text = "Reload synth_presets.csv" });
            rootVisualElement.Add(_oscilloscope);
            rootVisualElement.Add(_historyGraph);
            rootVisualElement.Add(_statusLabel);

            _baseDensitySlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _tensionMultiplierSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _lfoFrequencySlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _qualityMinSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _qualityMaxSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _basePitchSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _grainSizeSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _baseVolumeSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _stereoWidthSlider.RegisterValueChangedCallback(_ => PublishToRuntime());

            PullFromRuntime();
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
            element.style.backgroundColor = new StyleColor(new Color(0.025f, 0.035f, 0.04f, 1f));
            return element;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshSeconds;
            PullFromRuntime();
            RecordTelemetryPoint();
            RefreshStatus();
            if (_oscilloscope != null)
                _oscilloscope.MarkDirtyRepaint();
            if (_historyGraph != null)
                _historyGraph.MarkDirtyRepaint();
        }

        private void PullFromRuntime()
        {
            DynamicMusicGranularSynthesizer synth = ResolveSynth();
            if (synth == null)
                return;

            if (synth.TryGetEditorTuning(out DynamicMusicSynthTuningDTO tuning))
            {
                _tuning = tuning;
                SetSliderWithoutNotify(_baseDensitySlider, tuning.BaseGrainDensity);
                SetSliderWithoutNotify(_tensionMultiplierSlider, tuning.TensionMultiplier);
                SetSliderWithoutNotify(_lfoFrequencySlider, tuning.LfoFrequency);
                SetSliderWithoutNotify(_qualityMinSlider, tuning.QualityMin);
                SetSliderWithoutNotify(_qualityMaxSlider, tuning.QualityMax);
                SetSliderWithoutNotify(_basePitchSlider, tuning.BasePitchHz);
                SetSliderWithoutNotify(_grainSizeSlider, tuning.GrainSizeSeconds);
                SetSliderWithoutNotify(_baseVolumeSlider, tuning.BaseVolume);
                SetSliderWithoutNotify(_stereoWidthSlider, tuning.StereoWidth);
            }

            synth.TryGetEditorTelemetry(0, out _telemetry);
            synth.TryGetEditorSharedState(out _sharedState);
        }

        private void PublishToRuntime()
        {
            DynamicMusicGranularSynthesizer synth = ResolveSynth();
            if (synth == null)
                return;

            DynamicMusicSynthTuningDTO tuning = _tuning;
            tuning.BaseGrainDensity = _baseDensitySlider != null ? _baseDensitySlider.value : tuning.BaseGrainDensity;
            tuning.TensionMultiplier = _tensionMultiplierSlider != null ? _tensionMultiplierSlider.value : tuning.TensionMultiplier;
            tuning.LfoFrequency = _lfoFrequencySlider != null ? _lfoFrequencySlider.value : tuning.LfoFrequency;
            tuning.QualityMin = _qualityMinSlider != null ? _qualityMinSlider.value : tuning.QualityMin;
            tuning.QualityMax = _qualityMaxSlider != null ? math.max(_qualityMinSlider.value + 0.0001f, _qualityMaxSlider.value) : tuning.QualityMax;
            tuning.BasePitchHz = _basePitchSlider != null ? _basePitchSlider.value : tuning.BasePitchHz;
            tuning.GrainSizeSeconds = _grainSizeSlider != null ? _grainSizeSlider.value : tuning.GrainSizeSeconds;
            tuning.BaseVolume = _baseVolumeSlider != null ? _baseVolumeSlider.value : tuning.BaseVolume;
            tuning.StereoWidth = _stereoWidthSlider != null ? _stereoWidthSlider.value : tuning.StereoWidth;
            synth.TryWriteEditorTuning(in tuning);
            _tuning = tuning;
        }

        private void ReloadCsv()
        {
            DynamicMusicGranularSynthesizer synth = ResolveSynth();
            if (synth == null)
                return;

            bool loaded = synth.ReloadSynthPresetCsvCold();
            if (_statusLabel != null)
                _statusLabel.text = loaded ? "synth_presets.csv applied." : "synth_presets.csv unavailable.";
            PullFromRuntime();
        }

        private void RecordTelemetryPoint()
        {
            _tensionHistory[_historyCursor] = math.saturate(_telemetry.TensionIndex);
            _voiceHistory[_historyCursor] = math.saturate(_telemetry.ActiveVoices / (float)DynamicMusicGranularSynthesizer.VoiceCapacity);
            _historyCursor = (_historyCursor + 1) % GraphSamples;
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text =
                "Voices " + _telemetry.ActiveVoices + "/" + DynamicMusicGranularSynthesizer.VoiceCapacity +
                " | Tension " + _telemetry.TensionIndex.ToString("0.000") +
                " | Activity " + _sharedState.MusicActivity01.ToString("0.00") +
                " | Depth " + _telemetry.DepthMeters.ToString("0") + "m" +
                " | LPF " + _telemetry.LpfCutoffHz.ToString("0") + "Hz" +
                " | DSP " + _telemetry.DspJobMicroseconds.ToString("0.0") + "us";
        }

        private void DrawOscilloscope(MeshGenerationContext context)
        {
            DynamicMusicGranularSynthesizer synth = ResolveSynth();
            if (synth == null)
                return;

            Rect rect = _oscilloscope.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.1f, 0.85f, 0.72f, 0.95f);
            float midY = rect.y + rect.height * 0.5f;
            painter.BeginPath();
            for (int i = 0; i < OscilloscopeSamples; i++)
            {
                synth.TryGetEditorOutputSample(i * 2, out float sample);
                float x = rect.x + rect.width * (i / math.max(1f, OscilloscopeSamples - 1f));
                float y = midY - math.clamp(sample, -1f, 1f) * rect.height * 0.45f;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private void DrawHistoryGraph(MeshGenerationContext context)
        {
            Rect rect = _historyGraph.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawGraphLine(painter, rect, _tensionHistory, _historyCursor, new Color(1f, 0.24f, 0.18f, 0.95f));
            DrawGraphLine(painter, rect, _voiceHistory, _historyCursor, new Color(0.18f, 0.62f, 1f, 0.85f));
        }

        private static void DrawGraphLine(Painter2D painter, Rect rect, float[] samples, int cursor, Color color)
        {
            painter.lineWidth = 1.2f;
            painter.strokeColor = color;
            painter.BeginPath();
            for (int i = 0; i < samples.Length; i++)
            {
                int index = (cursor + i) % samples.Length;
                float x = rect.x + rect.width * (i / math.max(1f, samples.Length - 1f));
                float y = rect.yMax - math.saturate(samples[index]) * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(math.clamp(value, slider.lowValue, slider.highValue));
        }

        private static DynamicMusicSynthTuningDTO DefaultTuning()
        {
            DynamicMusicSynthTuningDTO tuning = default;
            tuning.BasePitchHz = 73.416f;
            tuning.BaseGrainDensity = 32f;
            tuning.TensionMultiplier = 1.35f;
            tuning.LfoFrequency = 1.35f;
            tuning.BaseVolume = 0.22f;
            tuning.GrainSizeSeconds = 0.075f;
            tuning.QualityMax = 1f;
            tuning.DepthMaxMeters = 1800f;
            tuning.LpfMinHz = 400f;
            tuning.LpfDepthHzPerMeter = 10f;
            tuning.StereoWidth = 0.82f;
            return tuning;
        }

        private static DynamicMusicGranularSynthesizer ResolveSynth()
        {
            if (DynamicMusicGranularSynthesizer.TryGetActive(out DynamicMusicGranularSynthesizer synth))
                return synth;

#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<DynamicMusicGranularSynthesizer>(FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectOfType<DynamicMusicGranularSynthesizer>();
#endif
        }

        private static void ValidateSynthVoiceLayout(bool silent)
        {
            bool valid =
                UnsafeUtility.SizeOf<SynthVoiceDTO>() == 64 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.CurrentPhase)) == 0 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.PhaseIncrement)) == 4 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.EnvelopeState)) == 8 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.SoundHash)) == 12 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.TargetPitch)) == 16 &&
                OffsetOf<SynthVoiceDTO>(nameof(SynthVoiceDTO.TargetVolume)) == 20 &&
                OffsetOf<SynthVoiceDTO>("_pad0") == 24 &&
                OffsetOf<SynthVoiceDTO>("_pad9") == 60;

            if (!valid)
                Hecton8.Core.H8Debug.LogError("[1308] SynthVoiceDTO layout violation. Expected explicit 64 bytes with hot fields at offsets 0,4,8,12,16,20 and padding 24-63.");
            else if (!silent)
                Hecton8.Core.H8Debug.Log("[1308] SynthVoiceDTO layout verified: 64 bytes.");
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }
}
#endif
