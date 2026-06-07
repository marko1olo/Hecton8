using Hecton8.Lighting;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Lighting.Editor
{
    public sealed class InteriorGITunerWindow : EditorWindow
    {
        private InteriorGIProbeVolumeRuntime _target;
        private bool _drawSceneProbes = true;
        private int _probeBudget = 768;
        private float _forcedQuality = -1f;
        private float _emergencyOverride;
        private float _propagationSpeed = 0.9f;
        private float _wallAbsorption = 1f;
        private float _emergencyIntensity = 2.4f;
        private float _waterAbsorption = 0.8f;

        [MenuItem("HECTON-8/Lighting/Interior GI Tuner")]
        private static void Open()
        {
            GetWindow<InteriorGITunerWindow>("Interior GI Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            ResolveTarget();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            ResolveTarget();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            _target = (InteriorGIProbeVolumeRuntime)EditorGUILayout.ObjectField(_target, typeof(InteriorGIProbeVolumeRuntime), true);
            if (_target == null)
            {
                EditorGUILayout.HelpBox("No InteriorGIProbeVolumeRuntime is active in the loaded scene.", MessageType.Warning);
                if (GUILayout.Button("Find Runtime"))
                    ResolveTarget(forceSceneSearch: true);
                return;
            }

            if (_target.TryGetTuningCopy(out InteriorGITuningDTO tuning))
            {
                EditorGUILayout.LabelField("Resolution", tuning.Resolution.ToString());
                EditorGUILayout.LabelField("Active Probes", tuning.ActiveProbeCount.ToString());
                EditorGUILayout.LabelField("Source Count", tuning.SourceCount.ToString());
                EditorGUILayout.LabelField("GlobalQualityWeight", tuning.GlobalQualityWeight.ToString("0.000"));
                EditorGUILayout.LabelField("Directional Weight", tuning.DirectionalWeight.ToString("0.000"));
                EditorGUILayout.LabelField("L2 Weight", tuning.L2Weight.ToString("0.000"));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            float newQuality = EditorGUILayout.Slider("Force Quality (-1 auto)", _forcedQuality, -1f, 1f);
            float newEmergency = EditorGUILayout.Slider("Emergency Red", _emergencyOverride, 0f, 1f);
            float newPropagation = EditorGUILayout.Slider("Propagation", _propagationSpeed, 0.05f, 4f);
            float newWall = EditorGUILayout.Slider("Wall Absorption", _wallAbsorption, 0f, 1f);
            float newEmergencyIntensity = EditorGUILayout.Slider("Emergency Intensity", _emergencyIntensity, 0f, 8f);
            float newWater = EditorGUILayout.Slider("Water Absorption", _waterAbsorption, 0f, 1f);
            if (!Mathf.Approximately(newQuality, _forcedQuality))
            {
                _forcedQuality = newQuality;
                _target.SetEditorForceQuality(_forcedQuality);
            }

            if (!Mathf.Approximately(newEmergency, _emergencyOverride))
            {
                _emergencyOverride = newEmergency;
                _target.SetEditorEmergencyOverride(_emergencyOverride);
            }

            if (!Mathf.Approximately(newPropagation, _propagationSpeed))
            {
                _propagationSpeed = newPropagation;
                _target.SetEditorPropagationSpeed(_propagationSpeed);
            }

            if (!Mathf.Approximately(newWall, _wallAbsorption))
            {
                _wallAbsorption = newWall;
                _target.SetEditorWallAbsorption(_wallAbsorption);
            }

            if (!Mathf.Approximately(newEmergencyIntensity, _emergencyIntensity))
            {
                _emergencyIntensity = newEmergencyIntensity;
                _target.SetEditorEmergencyLightIntensity(_emergencyIntensity);
            }

            if (!Mathf.Approximately(newWater, _waterAbsorption))
            {
                _waterAbsorption = newWater;
                _target.SetEditorWaterAbsorption(_waterAbsorption);
            }

            EditorGUILayout.Space(8f);
            _drawSceneProbes = EditorGUILayout.Toggle("Scene Probe Gizmos", _drawSceneProbes);
            _probeBudget = EditorGUILayout.IntSlider("Probe Draw Budget", _probeBudget, 32, 4096);

            if (GUILayout.Button("Reload lighting_fixtures.csv"))
                _target.RequestCsvReload();

            if (GUILayout.Button("Reload ambient_lighting_profiles.csv"))
                _target.RequestAmbientProfileCsvReload();

            if (GUILayout.Button("Dump 300-frame GI Black Box"))
                _target.DumpBlackBoxNow();

            if (GUILayout.Button("Disable Unity Realtime GI / Light Probes On Selection"))
                DisableUnityRealtimeGIOnSelection();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_drawSceneProbes || _target == null)
                return;

            if (!_target.TryGetProbeGridReadback(out NativeArray<CustomLightProbeDTO>.ReadOnly probes, out int resolution, out double3 rootAup, out float cellSize, out int version))
                return;

            int count = resolution * resolution * resolution;
            int stride = math.max(1, count / math.max(1, _probeBudget));
            Vector3 origin = _target.transform.position;
            for (int i = 0; i < count; i += stride)
            {
                CustomLightProbeDTO probe = probes[i];
                float3 forward = _target != null ? (float3)_target.transform.forward : new float3(0f, 0f, 1f);
                float3 forwardColor = InteriorGIProbeMath.EvaluateDirection(in probe, forward);
                float luma = math.saturate(math.dot(forwardColor, new float3(0.2126f, 0.7152f, 0.0722f)) * 0.25f);
                if (luma <= 0.015f)
                    continue;

                int3 coord = InteriorGIProbeMath.IndexToCoord(i, resolution);
                Vector3 local = new Vector3((coord.x + 0.5f) * cellSize, (coord.y + 0.5f) * cellSize, (coord.z + 0.5f) * cellSize);
                Handles.color = new Color(math.saturate(forwardColor.x), math.saturate(forwardColor.y), math.saturate(forwardColor.z), math.saturate(luma));
                Handles.SphereHandleCap(0, origin + local, Quaternion.identity, math.max(0.08f, cellSize * 0.12f), EventType.Repaint);
            }
        }

        private void ResolveTarget(bool forceSceneSearch = false)
        {
            if (_target != null && !forceSceneSearch)
                return;

            _target = Object.FindAnyObjectByType<InteriorGIProbeVolumeRuntime>(FindObjectsInactive.Include);
        }

        private static void DisableUnityRealtimeGIOnSelection()
        {
            Lightmapping.realtimeGI = false;
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                Renderer[] renderers = selected[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    renderers[r].lightProbeUsage = LightProbeUsage.Off;
            }
        }
    }
}
