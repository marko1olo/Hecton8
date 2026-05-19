#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Graphics.Scalability;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only DRS control surface for render-scale smoothing and reconstruction sharpening.
    /// </summary>
    public sealed class DynamicResolutionTunerWindow : EditorWindow
    {
        private const int ScopeSampleCount = 300;
        private const float ScopeHeight = 150f;
        private const float ScopePadding = 6f;

        // COLD ALLOC: float[300] - editor-only DRS current-scale oscilloscope samples - owner: DynamicResolutionTunerWindow
        private readonly float[] _currentScaleSamples = new float[ScopeSampleCount];
        // COLD ALLOC: float[300] - editor-only DRS target-scale oscilloscope samples - owner: DynamicResolutionTunerWindow
        private readonly float[] _targetScaleSamples = new float[ScopeSampleCount];
        // COLD ALLOC: float[300] - editor-only DRS stress oscilloscope samples - owner: DynamicResolutionTunerWindow
        private readonly float[] _stressSamples = new float[ScopeSampleCount];
        // COLD ALLOC: Vector3[300] - editor-only current-scale polyline cache - owner: DynamicResolutionTunerWindow
        private readonly Vector3[] _currentPoints = new Vector3[ScopeSampleCount];
        // COLD ALLOC: Vector3[300] - editor-only target-scale polyline cache - owner: DynamicResolutionTunerWindow
        private readonly Vector3[] _targetPoints = new Vector3[ScopeSampleCount];
        // COLD ALLOC: Vector3[300] - editor-only stress polyline cache - owner: DynamicResolutionTunerWindow
        private readonly Vector3[] _stressPoints = new Vector3[ScopeSampleCount];

        private float _minScaleLimit = 0.6f;
        private float _smoothingFactor = 8f;
        private float _sharpeningMultiplier = 0.8f;
        private bool _mockQualityWeightEnabled;
        private float _mockQualityWeight = 0.2f;

        [MenuItem("Hecton8/Rendering/Dynamic Resolution Tuner")]
        public static void Open()
        {
            GetWindow<DynamicResolutionTunerWindow>("Dynamic Resolution Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            PullRuntimeSettings();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            ThermalDynamicResolutionAdapter adapter = ResolveAdapter();
            EditorGUI.BeginDisabledGroup(adapter == null);
            if (adapter != null)
                PullRuntimeSettings(adapter);

            EditorGUI.BeginChangeCheck();
            _minScaleLimit = EditorGUILayout.Slider("Min Scale Limit", _minScaleLimit, 0.6f, 1f);
            _smoothingFactor = EditorGUILayout.Slider("Smoothing Factor", _smoothingFactor, 0.1f, 32f);
            _sharpeningMultiplier = EditorGUILayout.Slider("Sharpening Multiplier", _sharpeningMultiplier, 0f, 2f);
            if (EditorGUI.EndChangeCheck() && adapter != null)
                adapter.ApplyTunerSettings(_minScaleLimit, _smoothingFactor, _sharpeningMultiplier);

            EditorGUILayout.Space(8f);
            EditorGUI.BeginChangeCheck();
            _mockQualityWeightEnabled = EditorGUILayout.Toggle("Mock Quality Weight", _mockQualityWeightEnabled);
            _mockQualityWeight = EditorGUILayout.Slider("Mock Weight", _mockQualityWeight, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && adapter != null)
                adapter.SetMockQualityWeightForTuner(_mockQualityWeight, _mockQualityWeightEnabled);

            if (GUILayout.Button("Drop Mock Weight To 0.2") && adapter != null)
                adapter.ForceMockQualityWeightDrop();

            if (GUILayout.Button("Load drs_profiles.csv") && adapter != null)
                LoadCsv(adapter);

            EditorGUI.EndDisabledGroup();

            DrawSnapshot(adapter);
            DrawOscilloscope(adapter);
        }

        private static ThermalDynamicResolutionAdapter ResolveAdapter()
        {
            return GlobalRegistry.ResolutionScaler as ThermalDynamicResolutionAdapter;
        }

        private void PullRuntimeSettings()
        {
            ThermalDynamicResolutionAdapter adapter = ResolveAdapter();
            if (adapter != null)
                PullRuntimeSettings(adapter);
        }

        private void PullRuntimeSettings(ThermalDynamicResolutionAdapter adapter)
        {
            adapter.GetTunerSettings(
                out _minScaleLimit,
                out _smoothingFactor,
                out _sharpeningMultiplier);
        }

        private void DrawSnapshot(ThermalDynamicResolutionAdapter adapter)
        {
            if (adapter == null || !adapter.TryGetScaleState(out ResolutionScaleState state))
            {
                EditorGUILayout.LabelField("Runtime", "Not playing");
                return;
            }

            EditorGUILayout.LabelField("Current Scale", state.CurrentRenderScale01.ToString("0.000"));
            EditorGUILayout.LabelField("Target Scale", state.TargetRenderScale01.ToString("0.000"));
            EditorGUILayout.LabelField("Global Quality Weight", state.GlobalQualityWeight01.ToString("0.000"));
            EditorGUILayout.LabelField("Sharpen", state.SharpenIntensity01.ToString("0.000"));
            EditorGUILayout.LabelField("Stress EWMA", state.SystemStressEwma01.ToString("0.000"));
        }

        private void DrawOscilloscope(ThermalDynamicResolutionAdapter adapter)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, ScopeHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.03f, 0.035f, 0.04f, 1f));

            int count = adapter != null
                ? adapter.CopyTelemetryForEditor(_currentScaleSamples, _targetScaleSamples, _stressSamples, ScopeSampleCount)
                : 0;
            BuildScopePoints(rect, count);

            Handles.BeginGUI();
            Handles.color = new Color(0.20f, 0.92f, 0.72f, 1f);
            Handles.DrawPolyLine(_currentPoints);
            Handles.color = new Color(0.38f, 0.62f, 1f, 0.85f);
            Handles.DrawPolyLine(_targetPoints);
            Handles.color = new Color(0.92f, 0.35f, 0.18f, 0.75f);
            Handles.DrawPolyLine(_stressPoints);
            Handles.EndGUI();
        }

        private void BuildScopePoints(Rect rect, int count)
        {
            float width = Mathf.Max(1f, rect.width - ScopePadding * 2f);
            float height = Mathf.Max(1f, rect.height - ScopePadding * 2f);
            int safeCount = Mathf.Clamp(count, 0, ScopeSampleCount);
            for (int i = 0; i < ScopeSampleCount; i++)
            {
                float x = rect.x + ScopePadding + width * (i / (float)(ScopeSampleCount - 1));
                float current = i < safeCount ? Mathf.Clamp01(_currentScaleSamples[i]) : 0f;
                float target = i < safeCount ? Mathf.Clamp01(_targetScaleSamples[i]) : 0f;
                float stress = i < safeCount ? Mathf.Clamp01(_stressSamples[i]) : 0f;
                _currentPoints[i] = new Vector3(x, rect.yMax - ScopePadding - current * height, 0f);
                _targetPoints[i] = new Vector3(x, rect.yMax - ScopePadding - target * height, 0f);
                _stressPoints[i] = new Vector3(x, rect.yMax - ScopePadding - stress * height, 0f);
            }
        }

        private static void LoadCsv(ThermalDynamicResolutionAdapter adapter)
        {
            string path = EditorUtility.OpenFilePanel("Load DRS Profile", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            string csv = File.ReadAllText(path);
            adapter.TryApplyCsvProfile(csv.AsSpan());
        }
    }
}
#endif
