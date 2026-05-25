#if UNITY_EDITOR
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    /// <summary>
    /// Editor facade for live unmanaged thermodynamics constants and macro-grid visualization.
    /// </summary>
    public sealed unsafe class ThermodynamicsTunerWindow : EditorWindow
    {
        private const float MinHalfLifeSeconds = 10f;
        private const float MaxHalfLifeSeconds = 900f;
        private const int MaxDrawnCells = 4096;

        private bool _drawGrid = true;
        private float _heatThreshold = 40f;
        private float _radiationThreshold = 0.05f;

        [MenuItem("Hecton8/Thermodynamics/Thermodynamics Tuner")]
        public static void Open()
        {
            ThermodynamicsTunerWindow window = GetWindow<ThermodynamicsTunerWindow>("Thermodynamics Tuner");
            window.minSize = new Vector2(360f, 230f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            ThermodynamicsHazardGridRuntime runtime = ThermodynamicsHazardGridRuntime.ActiveRuntimeInstance;
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("No active ThermodynamicsHazardGridRuntime in Play Mode.", MessageType.Info);
                return;
            }

            if (!runtime.TryReadConstants(out ThermodynamicsHazardConstants value))
            {
                EditorGUILayout.HelpBox("GlobalDataVault thermodynamics constants are unavailable in Play Mode.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            value.BaseWaterTempCelsius = EditorGUILayout.Slider("Base Water Temp", value.BaseWaterTempCelsius, -8f, 40f);
            value.HeatDiffusionRate = EditorGUILayout.Slider("Heat Diffusion Rate", value.HeatDiffusionRate, 0f, 1f);
            float halfLife = DecayToHalfLife(value.RadiationDecayCoefficient);
            halfLife = EditorGUILayout.Slider("Radiation Half-Life", halfLife, MinHalfLifeSeconds, MaxHalfLifeSeconds);
            value.RadiationDecayCoefficient = MathLodApproximation.ApproxExpNegPade33Reduced(new float4(0.69314718056f / Mathf.Max(1f, halfLife))).x;
            value.RockShieldingFactor = EditorGUILayout.Slider("Rock Shielding Factor", value.RockShieldingFactor, 0f, 1f);
            _drawGrid = EditorGUILayout.Toggle("Draw Grid", _drawGrid);
            _heatThreshold = EditorGUILayout.Slider("Heat Gizmo Threshold", _heatThreshold, -8f, 300f);
            _radiationThreshold = EditorGUILayout.Slider("Radiation Gizmo Threshold", _radiationThreshold, 0f, 2f);

            if (EditorGUI.EndChangeCheck())
                runtime.TryWriteConstants(in value);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            OnDrawGizmos();
        }

        private void OnDrawGizmos()
        {
            if (!_drawGrid)
                return;

            ThermodynamicsHazardGridRuntime runtime = ThermodynamicsHazardGridRuntime.ActiveRuntimeInstance;
            if (runtime == null ||
                !runtime.PrepareVaultGridReadback() ||
                !runtime.TryGetVaultGridReadback(
                    out NativeArray<float>.ReadOnly temperature,
                    out NativeArray<float>.ReadOnly radiation,
                    out int resolution,
                    out _,
                    out float cellSize,
                    out _))
            {
                return;
            }

            int cellCount = resolution * resolution * resolution;
            int stride = math.max(1, (int)math.ceil(cellCount / (float)MaxDrawnCells));
            Vector3 origin = Vector3.zero;
            Vector3 size = Vector3.one * Mathf.Max(0.1f, cellSize * 0.9f);
            for (int index = 0; index < cellCount; index += stride)
            {
                float temp = temperature[index];
                float rad = radiation[index];
                float cold01 = Mathf.InverseLerp(_heatThreshold, -8f, temp);
                float heat01 = Mathf.InverseLerp(_heatThreshold, _heatThreshold + 200f, temp);
                float rad01 = Mathf.Clamp01(rad);
                if (cold01 <= 0.02f && heat01 <= 0.02f && rad < _radiationThreshold)
                    continue;

                int plane = resolution * resolution;
                int z = index / plane;
                int rem = index - z * plane;
                int y = rem / resolution;
                int x = rem - y * resolution;
                Vector3 position = origin + (new Vector3(x, y, z) - Vector3.one * (resolution * 0.5f)) * cellSize;
                Handles.color = rad >= _radiationThreshold && rad01 >= heat01
                    ? new Color(0.05f, 1f, 0.1f, Mathf.Lerp(0.18f, 0.8f, rad01))
                    : heat01 > cold01
                        ? new Color(1f, 0.12f, 0.05f, Mathf.Lerp(0.16f, 0.72f, heat01))
                        : new Color(0.08f, 0.28f, 1f, Mathf.Lerp(0.05f, 0.2f, cold01));
                Handles.DrawWireCube(position, size);
            }
        }

        private static float DecayToHalfLife(float decay)
        {
            float safeDecay = Mathf.Clamp(decay, 0.9001f, 0.9999f);
            float low = MinHalfLifeSeconds;
            float high = MaxHalfLifeSeconds;
            for (int i = 0; i < 18; i++)
            {
                float mid = (low + high) * 0.5f;
                float candidate = MathLodApproximation.ApproxExpNegPade33Reduced(new float4(0.69314718056f / Mathf.Max(1f, mid))).x;
                bool raiseLow = candidate < safeDecay;
                low = math.select(low, mid, raiseLow);
                high = math.select(mid, high, raiseLow);
            }

            return Mathf.Clamp((low + high) * 0.5f, MinHalfLifeSeconds, MaxHalfLifeSeconds);
        }
    }
}
#endif
