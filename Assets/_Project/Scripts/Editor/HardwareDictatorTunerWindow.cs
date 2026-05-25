#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only facade for the Hardware Scalability Dictator vault state.
    /// </summary>
    public sealed class HardwareDictatorTunerWindow : EditorWindow
    {
        private const int ScopeSampleCount = 300;
        private const float ScopeHeight = 150f;
        private const float ScopePadding = 6f;

        // COLD ALLOC: float[300] - editor-only quality-weight oscilloscope samples - owner: HardwareDictatorTunerWindow
        private readonly float[] _qualitySamples = new float[ScopeSampleCount];
        // COLD ALLOC: float[300] - editor-only frame-time oscilloscope samples - owner: HardwareDictatorTunerWindow
        private readonly float[] _frameMsSamples = new float[ScopeSampleCount];
        // COLD ALLOC: Vector3[300] - editor-only quality polyline cache - owner: HardwareDictatorTunerWindow
        private readonly Vector3[] _qualityPoints = new Vector3[ScopeSampleCount];
        // COLD ALLOC: Vector3[300] - editor-only frame-time polyline cache - owner: HardwareDictatorTunerWindow
        private readonly Vector3[] _framePoints = new Vector3[ScopeSampleCount];

        private float _targetFrameMs = 16.667f;
        private float _emergencyThreshold = 0.9f;
        private int _hysteresisFrames = 300;
        private float _mockSpikeMs = 20f;
        private float _mockVramPressure;
        private bool _mockEnabled;
        private bool _gcSafeMenu;
        private float _forcedQualityWeight = 1f;
        private bool _forceQualityWeight;

        [MenuItem("Hecton8/Core/Continuous Scalability Tuner")]
        public static void Open()
        {
            GetWindow<HardwareDictatorTunerWindow>("Continuous Scalability Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            PullRuntimeState();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (EditorApplication.isPlaying)
            {
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(0f, false);
                HomeostasisBrain.SetMockHeavyLoadForTuner(0f, 0f, false);
                HomeostasisBrain.SetHardwareDictatorGcSafeBaseMenu(false);
            }
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            PullRuntimeState();

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
            EditorGUI.BeginChangeCheck();
            _targetFrameMs = EditorGUILayout.Slider("Target Frame Time", _targetFrameMs, 4f, 50f);
            _emergencyThreshold = EditorGUILayout.Slider("Thermal Danger Threshold", _emergencyThreshold, 0.5f, 1f);
            _hysteresisFrames = EditorGUILayout.IntSlider("Hysteresis Recovery Frames", _hysteresisFrames, 60, 900);
            if (EditorGUI.EndChangeCheck())
                HomeostasisBrain.ApplyHardwareDictatorTuner(_targetFrameMs, _emergencyThreshold, _hysteresisFrames);

            EditorGUI.BeginChangeCheck();
            _forceQualityWeight = EditorGUILayout.Toggle("Force GlobalQualityWeight", _forceQualityWeight);
            _forcedQualityWeight = EditorGUILayout.Slider("Forced Weight", _forcedQualityWeight, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(_forcedQualityWeight, _forceQualityWeight);

            EditorGUILayout.Space(8f);
            EditorGUI.BeginChangeCheck();
            _mockEnabled = EditorGUILayout.Toggle("Mock Heavy Load", _mockEnabled);
            _mockSpikeMs = EditorGUILayout.Slider("Mock Frame Spike", _mockSpikeMs, 0f, 60f);
            _mockVramPressure = EditorGUILayout.Slider("Mock VRAM Pressure", _mockVramPressure, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                HomeostasisBrain.SetMockHeavyLoadForTuner(_mockSpikeMs, _mockVramPressure, _mockEnabled);

            EditorGUI.BeginChangeCheck();
            _gcSafeMenu = EditorGUILayout.Toggle("GC Safe Base Menu", _gcSafeMenu);
            if (EditorGUI.EndChangeCheck())
                HomeostasisBrain.SetHardwareDictatorGcSafeBaseMenu(_gcSafeMenu);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8f);
            DrawSnapshot();
            DrawOscilloscope();
        }

        private void PullRuntimeState()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (!HomeostasisBrain.TryGetHardwareDictatorSnapshot(out _, out ScalabilityStateDTO state))
                return;

            _forcedQualityWeight = Mathf.Clamp01(state.GlobalQualityWeight);
            if (HomeostasisBrain.TryGetHardwareDictatorTuning(out ScalabilityTuningDTO tuning))
            {
                _targetFrameMs = Mathf.Clamp(tuning.TargetFrameMs, 4f, 50f);
                _emergencyThreshold = Mathf.Clamp(tuning.EmergencyThreshold, 0.5f, 1f);
                _hysteresisFrames = Mathf.Clamp(tuning.HysteresisReleaseFrames, 60, 900);
            }
            else
            {
                _emergencyThreshold = Mathf.Clamp(_emergencyThreshold, 0.5f, 1f);
            }
        }

        private void DrawSnapshot()
        {
            if (!EditorApplication.isPlaying ||
                !HomeostasisBrain.TryGetHardwareDictatorSnapshot(out SystemHealthDTO health, out ScalabilityStateDTO state))
            {
                EditorGUILayout.LabelField("Runtime", "Not playing");
                return;
            }

            EditorGUILayout.LabelField("GlobalQualityWeight", state.GlobalQualityWeight.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("FractionalTimeSlice", state.FractionalTimeSlice.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Frame ms", health.FrameTimeMs.ToString("0.00", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("VRAM pressure", state.VramPressure.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Thermal index", state.ThermalIndex.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Target render scale", HomeostasisBrain.TargetRenderScale01.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Culling multiplier", HomeostasisBrain.CullingMultiplier.ToString("0.000", CultureInfo.InvariantCulture));
            if (HomeostasisBrain.TryGetMockTerrainSamplerStatus(out MockTerrainSamplerStatus terrain))
                EditorGUILayout.LabelField("Trilinear skip", terrain.SkippedTrilinearPercent01.ToString("0.000", CultureInfo.InvariantCulture));
        }

        private void DrawOscilloscope()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, ScopeHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.03f, 0.035f, 0.04f, 1f));

            int count = EditorApplication.isPlaying
                ? HomeostasisBrain.CopyHardwareDictatorOscilloscope(_qualitySamples, _frameMsSamples, ScopeSampleCount)
                : 0;
            BuildScopePoints(rect, count);

            Handles.BeginGUI();
            DrawThreshold(rect, 0.6f, new Color(0.92f, 0.12f, 0.10f, 0.35f));
            DrawThreshold(rect, 0.1f, new Color(0.92f, 0.12f, 0.10f, 0.85f));
            Handles.color = new Color(0.20f, 0.92f, 0.72f, 1f);
            Handles.DrawPolyLine(_qualityPoints);
            Handles.color = new Color(0.38f, 0.62f, 1f, 0.85f);
            Handles.DrawPolyLine(_framePoints);
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
                float quality = i < safeCount ? Mathf.Clamp01(_qualitySamples[i]) : 0f;
                float frame01 = i < safeCount ? Mathf.Clamp01(_frameMsSamples[i] / 50f) : 0f;
                _qualityPoints[i] = new Vector3(x, rect.yMax - ScopePadding - quality * height, 0f);
                _framePoints[i] = new Vector3(x, rect.yMax - ScopePadding - frame01 * height, 0f);
            }
        }

        private static void DrawThreshold(Rect rect, float value01, Color color)
        {
            float y = rect.yMax - ScopePadding - Mathf.Clamp01(value01) * Mathf.Max(1f, rect.height - ScopePadding * 2f);
            Handles.color = color;
            Handles.DrawLine(
                new Vector3(rect.x + ScopePadding, y, 0f),
                new Vector3(rect.xMax - ScopePadding, y, 0f));
        }
    }
}
#endif
