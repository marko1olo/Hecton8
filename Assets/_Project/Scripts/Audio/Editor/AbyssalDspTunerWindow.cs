#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// UI Toolkit facade for structural hull-stress DSP tuning and signal-source x-ray.
    /// </summary>
    public sealed class AbyssalDspTunerWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.05d;
        private const int OscilloscopeSampleCount = 256;
        private const int HistorySampleCount = 300;

        private readonly float[] _oscilloscopeSamples = new float[OscilloscopeSampleCount];
        private readonly float[] _voiceHistory = new float[HistorySampleCount];
        private readonly float[] _stressHistory = new float[HistorySampleCount];

        private int _historyCursor;
        private double _nextRefreshTime;
        private Slider _maxPolyphonySlider;
        private Slider _baseGrainLengthSlider;
        private Slider _distanceAttenuationSlider;
        private Slider _qualityOverrideSlider;
        private Slider _basePitchSlider;
        private Slider _overlapDensitySlider;
        private Slider _fmModulationSlider;
        private Toggle _drawSourcesToggle;
        private Label _statusLabel;
        private VisualElement _oscilloscope;
        private VisualElement _historyGraph;

        [MenuItem("Hecton8/Audio/Abyssal DSP Tuner")]
        public static void Open()
        {
            GetWindow<AbyssalDspTunerWindow>("Abyssal DSP");
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _maxPolyphonySlider = BuildSlider("Max Polyphony", 8f, 64f, 64f);
            _baseGrainLengthSlider = BuildSlider("Base Grain Length Ms", 10f, 50f, 50f);
            _distanceAttenuationSlider = BuildSlider("Distance Attenuation Curve", 0f, 1f, 0.5f);
            _qualityOverrideSlider = BuildSlider("GlobalQualityWeight Override", 0f, 1f, 1f);
            _basePitchSlider = BuildSlider("Base Pitch", 0.35f, 2.4f, 1f);
            _overlapDensitySlider = BuildSlider("Overlap Density", 0f, 4f, 1f);
            _fmModulationSlider = BuildSlider("FM Modulation Index", 0f, 4f, 1f);
            _drawSourcesToggle = new Toggle("Scene Source Gizmos") { value = true };
            _statusLabel = new Label("Runtime unavailable.");
            _oscilloscope = BuildGraphElement(96f);
            _historyGraph = BuildGraphElement(96f);
            _oscilloscope.generateVisualContent += DrawOscilloscope;
            _historyGraph.generateVisualContent += DrawHistory;

            rootVisualElement.Add(_maxPolyphonySlider);
            rootVisualElement.Add(_baseGrainLengthSlider);
            rootVisualElement.Add(_distanceAttenuationSlider);
            rootVisualElement.Add(_qualityOverrideSlider);
            rootVisualElement.Add(_basePitchSlider);
            rootVisualElement.Add(_overlapDensitySlider);
            rootVisualElement.Add(_fmModulationSlider);
            rootVisualElement.Add(_drawSourcesToggle);
            rootVisualElement.Add(_oscilloscope);
            rootVisualElement.Add(_historyGraph);
            rootVisualElement.Add(_statusLabel);

            _maxPolyphonySlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _baseGrainLengthSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _distanceAttenuationSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _qualityOverrideSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _basePitchSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _overlapDensitySlider.RegisterValueChangedCallback(_ => PublishToRuntime());
            _fmModulationSlider.RegisterValueChangedCallback(_ => PublishToRuntime());
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

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshSeconds;
            if (EditorApplication.isPlaying)
                PublishToRuntime();

            PlayerCriticalProceduralAudioRenderer renderer = ResolveRenderer(false);
            bool hasRenderer = renderer != null;
            bool readyOwner = hasRenderer && renderer.IsPlayerCriticalAudioRuntimeReady;
            bool hasScope = readyOwner && renderer.TryCopyLatestGranularOscilloscope(_oscilloscopeSamples, 0, _oscilloscopeSamples.Length);
            ReadOnlySpan<BaseStructuralWarningSignal> structuralWarnings = SignalBus<BaseStructuralWarningSignal>.GetFrameSnapshot();
            float maxStress = 0f;
            for (int i = 0; i < structuralWarnings.Length; i++)
                maxStress = math.max(maxStress, math.saturate(structuralWarnings[i].HighestStress01));

            _voiceHistory[_historyCursor] = math.saturate(structuralWarnings.Length * math.rcp(64f));
            _stressHistory[_historyCursor] = maxStress;
            _historyCursor = (_historyCursor + 1) % HistorySampleCount;

            if (_statusLabel != null)
            {
                _statusLabel.text = hasRenderer
                    ? "Runtime " + (readyOwner ? "ready" : "not ready") + " | signals " + structuralWarnings.Length + " | scope " + (hasScope ? "native" : "silent")
                    : "Runtime unavailable.";
            }

            if (_oscilloscope != null)
                _oscilloscope.MarkDirtyRepaint();
            if (_historyGraph != null)
                _historyGraph.MarkDirtyRepaint();
            Repaint();
        }

        private void PublishToRuntime()
        {
            if (!EditorApplication.isPlaying)
                return;

            PlayerCriticalProceduralAudioRenderer renderer = ResolveRenderer();
            if (renderer == null)
                return;

            renderer.ApplyAbyssalDspTuning(
                _maxPolyphonySlider != null ? _maxPolyphonySlider.value : 64f,
                _baseGrainLengthSlider != null ? _baseGrainLengthSlider.value : 50f,
                _distanceAttenuationSlider != null ? _distanceAttenuationSlider.value : 0.5f,
                _qualityOverrideSlider != null ? _qualityOverrideSlider.value : 1f,
                _basePitchSlider != null ? _basePitchSlider.value : 1f,
                _overlapDensitySlider != null ? _overlapDensitySlider.value : 1f,
                _fmModulationSlider != null ? _fmModulationSlider.value : 1f);
        }

        private void DrawOscilloscope(MeshGenerationContext context)
        {
            DrawLineGraph(context, _oscilloscope, _oscilloscopeSamples, new Color(0.1f, 0.88f, 0.74f, 0.95f), centerSamples: true);
        }

        private void DrawHistory(MeshGenerationContext context)
        {
            DrawHistoryLine(context, _historyGraph, _stressHistory, _historyCursor, new Color(0.95f, 0.38f, 0.22f, 0.92f));
            DrawHistoryLine(context, _historyGraph, _voiceHistory, _historyCursor, new Color(0.24f, 0.64f, 1f, 0.84f));
        }

        private static void DrawLineGraph(
            MeshGenerationContext context,
            VisualElement element,
            float[] samples,
            Color color,
            bool centerSamples)
        {
            if (element == null || samples == null || samples.Length <= 1)
                return;

            Rect rect = element.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = 1.4f;
            painter.strokeColor = color;
            float midY = rect.y + rect.height * 0.5f;
            painter.BeginPath();
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = math.clamp(samples[i], -1f, 1f);
                float x = rect.x + rect.width * (i / math.max(1f, samples.Length - 1f));
                float y = centerSamples
                    ? midY - sample * rect.height * 0.45f
                    : rect.yMax - math.saturate(sample) * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private static void DrawHistoryLine(
            MeshGenerationContext context,
            VisualElement element,
            float[] history,
            int cursor,
            Color color)
        {
            if (element == null || history == null || history.Length <= 1)
                return;

            Rect rect = element.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = 1.3f;
            painter.strokeColor = color;
            painter.BeginPath();
            for (int i = 0; i < history.Length; i++)
            {
                int index = cursor + i;
                if (index >= history.Length)
                    index -= history.Length;
                float x = rect.x + rect.width * (i / math.max(1f, history.Length - 1f));
                float y = rect.yMax - math.saturate(history[index]) * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (_drawSourcesToggle == null || !_drawSourcesToggle.value)
                return;

            ReadOnlySpan<BaseStructuralWarningSignal> structuralWarnings = SignalBus<BaseStructuralWarningSignal>.GetFrameSnapshot();
            for (int i = 0; i < structuralWarnings.Length; i++)
            {
                BaseStructuralWarningSignal signal = structuralWarnings[i];
                if (!AcousticAup.IsFinite(in signal.EpicenterAup))
                    continue;

                double3 absolute = ToDouble3(in signal.EpicenterAup);
                Vector3 position = HectonFloatingOrigin.ToRuntimePosition(absolute);
                float stress = math.saturate(signal.HighestStress01);
                float amplitude = math.saturate(signal.AudioIntensity01);
                float radius = math.lerp(0.25f, 3.5f, math.max(stress, amplitude));
                Color color = Color.Lerp(new Color(0.18f, 0.62f, 1f, 0.55f), new Color(1f, 0.22f, 0.08f, 0.85f), stress);
                Handles.color = color;
                Handles.DrawWireDisc(position, Vector3.up, radius);
                Handles.DrawWireDisc(position, Vector3.right, radius);
                Handles.DrawWireDisc(position, Vector3.forward, radius);
            }
        }

        private static PlayerCriticalProceduralAudioRenderer ResolveRenderer(bool requireReady = true)
        {
            PlayerCriticalProceduralAudioRenderer registeredRenderer = GlobalRegistry.PlayerCriticalAudio;
            if (IsRendererUsable(registeredRenderer, requireReady))
                return registeredRenderer;

#if UNITY_2023_1_OR_NEWER
            PlayerCriticalProceduralAudioRenderer sceneRenderer = UnityEngine.Object.FindAnyObjectByType<PlayerCriticalProceduralAudioRenderer>();
#else
            PlayerCriticalProceduralAudioRenderer sceneRenderer = UnityEngine.Object.FindObjectOfType<PlayerCriticalProceduralAudioRenderer>();
#endif
            return IsRendererUsable(sceneRenderer, requireReady) ? sceneRenderer : null;
        }

        private static bool IsRendererUsable(PlayerCriticalProceduralAudioRenderer renderer, bool requireReady)
        {
            if (renderer == null)
                return false;

            return requireReady ? renderer.IsPlayerCriticalAudioRuntimeReady : renderer.isActiveAndEnabled;
        }

        private static double3 ToDouble3(in AcousticAup aup)
        {
            double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                (aup.GridX * cell) + (double)aup.Local.x,
                (aup.GridY * cell) + (double)aup.Local.y,
                (aup.GridZ * cell) + (double)aup.Local.z);
        }
    }
}
#endif
