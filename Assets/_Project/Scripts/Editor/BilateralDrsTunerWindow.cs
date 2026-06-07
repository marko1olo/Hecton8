#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class BilateralDrsTunerWindow : EditorWindow
    {
        private const int GraphCapacity = 128;
        private const double ReadoutIntervalSeconds = 0.125d;
        private readonly float[] _scaleHistory = new float[GraphCapacity];
        private readonly float[] _frameMsHistory = new float[GraphCapacity];
        private int _historyCursor;
        private double _nextReadoutTime;
        private Slider _depthWeight;
        private Slider _colorWeight;
        private Slider _minRadius;
        private Slider _maxRadius;
        private Slider _forcedScale;
        private Slider _forcedQuality;
        private Slider _qualityBias;
        private Toggle _edgeMaskDebug;
        private Label _layoutLabel;
        private Label _runtimeLabel;
        private DrsGraphElement _graph;

        [MenuItem("Hecton8/Rendering/Bilateral DRS Tuner")]
        public static void Open()
        {
            GetWindow<BilateralDrsTunerWindow>("Bilateral DRS");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _depthWeight = CreateSlider("Depth Edge Sensitivity", 1f, 384f, BilateralDrsUpscalerConstants.DefaultDepthWeight);
            _colorWeight = CreateSlider("Color Edge Sensitivity", 0.001f, 48f, BilateralDrsUpscalerConstants.DefaultColorWeight);
            _minRadius = CreateSlider("Minimum Radius", 0.25f, 2f, BilateralDrsUpscalerConstants.DefaultMinRadiusPixels);
            _maxRadius = CreateSlider("Maximum Radius", 0.5f, 4f, BilateralDrsUpscalerConstants.DefaultMaxRadiusPixels);
            _forcedScale = CreateSlider("Forced Scale", 0f, 1f, 0f);
            _forcedQuality = CreateSlider("Forced Quality", -1f, 1f, -1f);
            _qualityBias = CreateSlider("Quality Bias", -1f, 1f, 0f);
            _edgeMaskDebug = new Toggle("Edge Mask Debug");
            _edgeMaskDebug.RegisterValueChangedCallback(_ => ApplyTuning());

            root.Add(_depthWeight);
            root.Add(_colorWeight);
            root.Add(_minRadius);
            root.Add(_maxRadius);
            root.Add(_forcedScale);
            root.Add(_forcedQuality);
            root.Add(_qualityBias);
            root.Add(_edgeMaskDebug);

            Button loadProfiles = new Button(LoadDefaultProfiles) { text = "Load upscaler_quality_profiles.csv" };
            Button validateLayout = new Button(RefreshLayoutAudit) { text = "Validate 32B Layout" };
            root.Add(loadProfiles);
            root.Add(validateLayout);

            _runtimeLabel = new Label();
            _layoutLabel = new Label();
            _graph = new DrsGraphElement(_scaleHistory, _frameMsHistory);
            _graph.style.height = 96;
            _graph.style.marginTop = 8;
            root.Add(_runtimeLabel);
            root.Add(_layoutLabel);
            root.Add(_graph);

            RefreshLayoutAudit();
            ApplyTuning();
            RefreshReadout();
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

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextReadoutTime)
                return;

            _nextReadoutTime = now + ReadoutIntervalSeconds;
            RefreshReadout();
        }

        private Slider CreateSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max);
            slider.value = value;
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => ApplyTuning());
            return slider;
        }

        private void ApplyTuning()
        {
            if (_depthWeight == null)
                return;

            HectonBilateralDrsUpscalerRuntime.TrySetEditorTuning(
                _depthWeight.value,
                _colorWeight.value,
                _minRadius.value,
                _maxRadius.value,
                _forcedScale.value,
                _forcedQuality.value,
                _qualityBias.value,
                _edgeMaskDebug != null && _edgeMaskDebug.value);
        }

        private void LoadDefaultProfiles()
        {
            HectonBilateralDrsUpscalerRuntime.TryLoadQualityProfilesCsv("Assets/_Project/Data/upscaler_quality_profiles.csv");
        }

        private void RefreshReadout()
        {
            if (_runtimeLabel == null)
                return;

            float scale = 1f;
            float frameMs = 0f;
            if (GlobalRegistry.ResolutionScaler != null &&
                GlobalRegistry.ResolutionScaler.TryGetScaleState(out ResolutionScaleState state))
            {
                scale = math.saturate(state.CurrentRenderScale01);
                frameMs = math.max(0f, state.FrameTimeEwmaMs);
                SetRuntimeLabel(
                    "DRS scale " + scale.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | target " + state.TargetRenderScale01.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | quality " + state.GlobalQualityWeight01.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | frame " + frameMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
            }
            else if (HectonBilateralDrsUpscalerRuntime.TryReadActiveParameters(out UpscalerParamsDTO parameters))
            {
                float lowX = math.max(1f, parameters.ResolutionParams.x);
                float highX = math.max(lowX, parameters.ResolutionParams.z);
                scale = math.saturate(lowX / highX);
                frameMs = 0f;
                SetRuntimeLabel(
                    "DRS scale " + scale.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | quality " + parameters.FilterParams.w.ToString("0.000", CultureInfo.InvariantCulture) +
                    " | cbuffer active");
            }
            else
            {
                SetRuntimeLabel("Runtime unavailable.");
            }

            _scaleHistory[_historyCursor] = scale;
            _frameMsHistory[_historyCursor] = frameMs;
            _historyCursor = (_historyCursor + 1) & (GraphCapacity - 1);
            _graph?.MarkDirtyRepaint();
        }

        private void SetRuntimeLabel(string value)
        {
            if (_runtimeLabel == null || string.Equals(_runtimeLabel.text, value, StringComparison.Ordinal))
                return;

            _runtimeLabel.text = value;
        }

        private void RefreshLayoutAudit()
        {
            if (_layoutLabel == null)
                return;

            int size = UnsafeUtility.SizeOf<UpscalerParamsDTO>();
            int resolutionOffset = OffsetOf<UpscalerParamsDTO>(nameof(UpscalerParamsDTO.ResolutionParams));
            int filterOffset = OffsetOf<UpscalerParamsDTO>(nameof(UpscalerParamsDTO.FilterParams));
            bool valid = size == BilateralDrsUpscalerConstants.CBufferBytes &&
                         resolutionOffset == UpscalerParamsLayoutValidator.ResolutionParamsOffset &&
                         filterOffset == UpscalerParamsLayoutValidator.FilterParamsOffset &&
                         UpscalerParamsLayoutValidator.Validate();
            _layoutLabel.text = $"Layout {(valid ? "VALID" : "INVALID")} | size {size} | ResolutionParams {resolutionOffset} | FilterParams {filterOffset}";
        }

        private static int OffsetOf<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }

        private sealed class DrsGraphElement : VisualElement
        {
            private readonly float[] _scale;
            private readonly float[] _frameMs;

            public DrsGraphElement(float[] scale, float[] frameMs)
            {
                _scale = scale;
                _frameMs = frameMs;
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.0f, 0.95f, 0.42f, 1f);
                DrawSeries(painter, rect, _scale, 1f);
                painter.strokeColor = new Color(0.2f, 0.68f, 1f, 1f);
                DrawSeries(painter, rect, _frameMs, 33.3f);
            }

            private static void DrawSeries(Painter2D painter, Rect rect, float[] values, float maxValue)
            {
                if (values == null || values.Length < 2 || maxValue <= 0f)
                    return;

                painter.BeginPath();
                for (int i = 0; i < values.Length; i++)
                {
                    float x = rect.xMin + rect.width * (i / (float)(values.Length - 1));
                    float y = rect.yMax - rect.height * Mathf.Clamp01(values[i] / maxValue);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
