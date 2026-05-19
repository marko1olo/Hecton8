#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Audio.Virtualization;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// UI Toolkit facade for the vault-backed acoustic DSP tuning lane.
    /// </summary>
    public sealed class AbyssalAcousticsTunerWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;
        private const string MaterialCsvAssetPath = "Assets/_Project/Data/Audio/acoustic_materials.csv";

        private VirtualVoiceTuningSnapshot _tuning = VirtualVoiceTuningSnapshot.CreateDefault();
        private VirtualVoiceStatistics _stats;
        private Slider _qualitySlider;
        private Slider _soundSpeedSlider;
        private Slider _occlusionSlider;
        private Slider _lowPassSlider;
        private Slider _sabineSlider;
        private IntegerField _maxVoicesField;
        private Toggle _disableSdfToggle;
        private Label _statsLabel;
        private readonly VisualElement[] _histogramBars = new VisualElement[16];
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Audio/Abyssal Acoustics Tuner")]
        public static void Open()
        {
            GetWindow<AbyssalAcousticsTunerWindow>("Abyssal Acoustics");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _qualitySlider = BuildSlider("Global Quality", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _soundSpeedSlider = BuildSlider("Sound Speed", 250f, 2000f, _tuning.SoundSpeedMetersPerSecond);
            _occlusionSlider = BuildSlider("Occlusion Gain", 0.03162278f, 1f, _tuning.GlobalOcclusionPenalty);
            _lowPassSlider = BuildSlider("Water Density LPF", 80f, VirtualVoiceUtility.OpenLowPassHertz, _tuning.OccludedLowPassHertz);
            _sabineSlider = BuildSlider("Sabine Scale", 0.1f, 4f, _tuning.SabineDecayScale);
            _maxVoicesField = new IntegerField("Max Voices") { value = _tuning.MaxHydratedVoices };
            _disableSdfToggle = new Toggle("Disable SDF") { value = _tuning.DisableSdfOcclusion != 0 };
            _statsLabel = new Label();

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_soundSpeedSlider);
            rootVisualElement.Add(_occlusionSlider);
            rootVisualElement.Add(_lowPassSlider);
            rootVisualElement.Add(_sabineSlider);
            rootVisualElement.Add(_maxVoicesField);
            rootVisualElement.Add(_disableSdfToggle);
            rootVisualElement.Add(new Button(ReloadMaterialCsv) { text = "Reload Material CSV" });
            rootVisualElement.Add(BuildHistogram());
            rootVisualElement.Add(_statsLabel);

            _qualitySlider.RegisterValueChangedCallback(evt => HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(evt.newValue, true));
            _soundSpeedSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _occlusionSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _lowPassSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _sabineSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _maxVoicesField.RegisterValueChangedCallback(_ => PublishToRuntime());
            _disableSdfToggle.RegisterValueChangedCallback(_ => PublishToRuntime());
            PullFromRuntime();
            RefreshStatsLabel();
            RefreshHistogram();
        }

        private static Slider BuildSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = math.clamp(value, min, max),
                showInputField = true
            };
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshSeconds;
            PullFromRuntime();
            RefreshStatsLabel();
            RefreshHistogram();
        }

        private VisualElement BuildHistogram()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.height = 48f;
            root.style.marginTop = 8f;
            root.style.marginBottom = 8f;

            for (int i = 0; i < _histogramBars.Length; i++)
            {
                var bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginLeft = 1f;
                bar.style.marginRight = 1f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.height = 2f;
                bar.style.backgroundColor = new StyleColor(new Color(0f, 0.75f, 0.55f, 0.9f));
                _histogramBars[i] = bar;
                root.Add(bar);
            }

            return root;
        }

        private void PullFromRuntime()
        {
            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
                return;

            if (manager.TryGetVirtualVoiceRuntimeTuning(out VirtualVoiceTuningSnapshot tuning))
                _tuning = tuning;
            manager.TryGetVirtualizationStats(out _stats);

            if (_soundSpeedSlider != null)
                _soundSpeedSlider.SetValueWithoutNotify(_tuning.SoundSpeedMetersPerSecond);
            if (_occlusionSlider != null)
                _occlusionSlider.SetValueWithoutNotify(_tuning.GlobalOcclusionPenalty);
            if (_lowPassSlider != null)
                _lowPassSlider.SetValueWithoutNotify(_tuning.OccludedLowPassHertz);
            if (_sabineSlider != null)
                _sabineSlider.SetValueWithoutNotify(_tuning.SabineDecayScale);
            if (_maxVoicesField != null)
                _maxVoicesField.SetValueWithoutNotify(_tuning.MaxHydratedVoices);
            if (_disableSdfToggle != null)
                _disableSdfToggle.SetValueWithoutNotify(_tuning.DisableSdfOcclusion != 0);
        }

        private void PublishToRuntime()
        {
            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
                return;

            _tuning.SoundSpeedMetersPerSecond = _soundSpeedSlider != null ? _soundSpeedSlider.value : _tuning.SoundSpeedMetersPerSecond;
            _tuning.GlobalOcclusionPenalty = _occlusionSlider != null ? _occlusionSlider.value : _tuning.GlobalOcclusionPenalty;
            _tuning.OccludedLowPassHertz = _lowPassSlider != null ? _lowPassSlider.value : _tuning.OccludedLowPassHertz;
            _tuning.SabineDecayScale = _sabineSlider != null ? _sabineSlider.value : _tuning.SabineDecayScale;
            _tuning.MaxHydratedVoices = _maxVoicesField != null
                ? math.clamp(_maxVoicesField.value, 1, VirtualVoiceUtility.MaxPhysicalVoiceCount)
                : _tuning.MaxHydratedVoices;
            _tuning.DisableSdfOcclusion = _disableSdfToggle != null && _disableSdfToggle.value ? (byte)1 : (byte)0;
            _tuning = VirtualVoiceTuningSnapshot.Sanitize(in _tuning);
            manager.ApplyVirtualVoiceRuntimeTuning(in _tuning);
        }

        private void ReloadMaterialCsv()
        {
            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string absolutePath = Path.Combine(projectRoot, MaterialCsvAssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                if (_statsLabel != null)
                    _statsLabel.text = "Material CSV missing: " + MaterialCsvAssetPath;
                return;
            }

            byte[] bytes = File.ReadAllBytes(absolutePath);
            int rows = manager.ReloadAcousticMaterialRowsFromCsvCold(bytes.AsSpan());
            if (_statsLabel != null)
                _statsLabel.text = "Material rows loaded: " + rows;
        }

        private void RefreshStatsLabel()
        {
            if (_statsLabel == null)
                return;

            _statsLabel.text =
                "Voices " + _stats.ActivePhysicalVoices + "/" + _stats.PhysicalVoiceLimit +
                " | Virtual " + _stats.AudibleVoices + "/" + _stats.TotalVoices +
                " | RT60 " + _stats.AverageRt60Seconds.ToString("0.00") +
                " | LPF " + _stats.AverageLowPassHertz.ToString("0");
        }

        private void RefreshHistogram()
        {
            int physicalLimit = math.max(1, _stats.PhysicalVoiceLimit);
            float active01 = math.saturate(_stats.ActivePhysicalVoices * math.rcp((float)physicalLimit));
            float culled01 = _stats.TotalVoices > 0
                ? math.saturate(_stats.CulledVoices * math.rcp((float)_stats.TotalVoices))
                : 0f;

            for (int i = 0; i < _histogramBars.Length; i++)
            {
                VisualElement bar = _histogramBars[i];
                if (bar == null)
                    continue;

                float bucket = (i + 1f) * math.rcp(_histogramBars.Length);
                float height01 = math.saturate(active01 - bucket + 1f / _histogramBars.Length) * _histogramBars.Length;
                bar.style.height = math.lerp(2f, 44f, height01);
                bar.style.backgroundColor = new StyleColor(Color.Lerp(
                    new Color(0f, 0.75f, 0.55f, 0.9f),
                    new Color(1f, 0.16f, 0.08f, 0.9f),
                    culled01));
            }
        }

        private static SpatialAudioManager ResolveSpatialAudioManager()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<SpatialAudioManager>();
#else
            return Object.FindObjectOfType<SpatialAudioManager>();
#endif
        }
    }
}
#endif
