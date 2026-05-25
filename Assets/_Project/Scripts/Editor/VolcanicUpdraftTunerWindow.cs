#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class VolcanicUpdraftTunerWindow : EditorWindow
    {
        private VolcanicUpdraftDirector _runtime;
        private bool _drawSceneGizmos = true;
        private int _maxDrawnVents = 16;

        [MenuItem("Hecton8/Thermodynamics/Volcanic Updraft Tuner")]
        private static void Open()
        {
            VolcanicUpdraftTunerWindow window = GetWindow<VolcanicUpdraftTunerWindow>();
            window.titleContent = new GUIContent("Volcanic Updraft");
            window.Show();
        }

        private void OnEnable()
        {
            RefreshRuntime();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Volcanic Updraft Tuner", EditorStyles.boldLabel);
            _runtime = (VolcanicUpdraftDirector)EditorGUILayout.ObjectField("Runtime", _runtime, typeof(VolcanicUpdraftDirector), true);
            if (_runtime == null)
            {
                if (GUILayout.Button("Find Runtime"))
                    RefreshRuntime();
                return;
            }

            if (!_runtime.TryGetVentReadback(0, out _, out VolcanicUpdraftSettingsDTO settings))
            {
                EditorGUILayout.HelpBox("Runtime vault buffers are not ready.", MessageType.Warning);
                if (GUILayout.Button("Refresh Runtime"))
                    RefreshRuntime();
                return;
            }

            EditorGUILayout.LabelField("Vent Count", settings.VentCount.ToString());
            EditorGUILayout.LabelField("Source Hash", settings.SourceHash.ToString("X8"));
            EditorGUILayout.LabelField("Global Quality Weight", settings.GlobalQualityWeight.ToString("0.000"));

            EditorGUI.BeginChangeCheck();
            float maxThrust = EditorGUILayout.Slider("Max Thrust", settings.MaxThrust, 0.01f, 160f);
            float eruptionFrequency = EditorGUILayout.Slider("Eruption Frequency", settings.EruptionFrequency, 0.001f, 4f);
            float cylinderRadius = EditorGUILayout.Slider("Cylinder Radius", settings.CylinderRadius, 0.25f, 220f);
            float heatOutput = EditorGUILayout.Slider("Heat Output", settings.HeatOutput, 0f, 1f);
            _drawSceneGizmos = EditorGUILayout.Toggle("Draw Scene Gizmos", _drawSceneGizmos);
            _maxDrawnVents = EditorGUILayout.IntSlider("Max Drawn Vents", _maxDrawnVents, 1, VolcanicUpdraftVault.MaxVents);
            if (EditorGUI.EndChangeCheck())
            {
                _runtime.TryWriteSettingsFromEditor(maxThrust, eruptionFrequency, cylinderRadius, heatOutput);
                SceneView.RepaintAll();
            }
        }

        private void RefreshRuntime()
        {
            _runtime = VolcanicUpdraftDirector.ActiveRuntimeInstance;
            if (_runtime == null)
                _runtime = FindAnyObjectByType<VolcanicUpdraftDirector>(FindObjectsInactive.Include);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_drawSceneGizmos)
                return;

            if (_runtime == null)
            {
                RefreshRuntime();
                if (_runtime == null)
                    return;
            }

            if (!_runtime.TryGetVentReadback(0, out _, out VolcanicUpdraftSettingsDTO settings))
                return;

            int count = math.min((int)settings.VentCount, _maxDrawnVents);
            for (int i = 0; i < count; i++)
            {
                if (!_runtime.TryGetVentReadback(i, out VentStateDTO vent, out settings))
                    continue;

                Vector3 basePos = HectonFloatingOrigin.ToRuntimePosition(vent.AUP, HectonFloatingOrigin.CurrentTotalOffsetDouble);
                float radius = math.max(0.25f, vent.Radius);
                float height = math.max(1f, settings.MaxHeight);
                float active = math.saturate(vent.ThrustPower / math.max(0.0001f, settings.MaxThrust));
                Handles.color = Color.Lerp(new Color(0.1f, 0.45f, 1f, 0.24f), new Color(1f, 0.23f, 0.04f, 0.72f), active);
                Handles.DrawWireDisc(basePos, Vector3.up, radius);
                Handles.DrawWireDisc(basePos + Vector3.up * height, Vector3.up, radius * 0.35f);
                DrawColumnLine(basePos, height, radius, Vector3.right);
                DrawColumnLine(basePos, height, radius, -Vector3.right);
                DrawColumnLine(basePos, height, radius, Vector3.forward);
                DrawColumnLine(basePos, height, radius, -Vector3.forward);
            }
        }

        private static void DrawColumnLine(Vector3 basePos, float height, float radius, Vector3 dir)
        {
            Vector3 bottom = basePos + dir * radius;
            Vector3 top = basePos + Vector3.up * height + dir * (radius * 0.35f);
            Handles.DrawLine(bottom, top);
        }
    }
}
#endif
