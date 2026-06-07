using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class UberNoirGlobalTunerWindow : EditorWindow
    {
        private const int MaxWakeGizmos = 16;
        private const float FlowArrowMeters = 7.5f;
        private UberNoirGlobalTuning _cachedTuning;
        private bool _hasState;
        private bool _showGizmos = true;

        [MenuItem("Hecton8/Rendering/UberNoir Global Tuner")]
        public static void Open()
        {
            GetWindow<UberNoirGlobalTunerWindow>("UberNoir Global Tuner");
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
            _hasState = GlobalShaderDispatcher.TryReadEditorTuning(out _cachedTuning);
            using (new EditorGUI.DisabledScope(!_hasState))
            {
                EditorGUI.BeginChangeCheck();
                Color fog = new Color(
                    _cachedTuning.FogColor.x,
                    _cachedTuning.FogColor.y,
                    _cachedTuning.FogColor.z,
                    1f);
                fog = EditorGUILayout.ColorField("Base Fog Color", fog);
                float fogDensity = EditorGUILayout.Slider("Fog Density", _cachedTuning.FogDensity, 0f, 0.12f);
                float causticSpeed = EditorGUILayout.Slider("Caustic Speed", _cachedTuning.CausticSpeed, 0f, 2f);
                float flowMagnitude = EditorGUILayout.Slider("Global Flow Magnitude", _cachedTuning.FlowMagnitude, 0f, 2.5f);
                Vector3 flowVector = EditorGUILayout.Vector3Field("Global Flow Vector", _cachedTuning.FlowVector);
                _showGizmos = EditorGUILayout.Toggle("Flow Gizmos", _showGizmos);
                if (EditorGUI.EndChangeCheck())
                {
                    _cachedTuning.FogColor = new Vector4(fog.r, fog.g, fog.b, fogDensity);
                    _cachedTuning.FogDensity = fogDensity;
                    _cachedTuning.CausticSpeed = causticSpeed;
                    _cachedTuning.FlowMagnitude = flowMagnitude;
                    _cachedTuning.FlowVector = NormalizeOrDefault(flowVector, Vector3.right);
                    GlobalShaderDispatcher.TryWriteEditorTuning(in _cachedTuning);
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Override"))
                {
                    GlobalShaderDispatcher.ClearEditorOverrides();
                    Repaint();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Refresh"))
                {
                    Repaint();
                    SceneView.RepaintAll();
                }
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_showGizmos || sceneView == null || Event.current.type != EventType.Repaint)
                return;

            DrawFlowGizmo(sceneView);
            DrawWakeGizmos();
        }

        private static void DrawFlowGizmo(SceneView sceneView)
        {
            if (!GlobalShaderDispatcher.TryGetEditorGlobalFlow(out Vector4 flow))
                return;

            Vector3 direction = new Vector3(flow.x, flow.y, flow.z);
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector3 origin = sceneView.pivot;
            float magnitude = Mathf.Max(0.25f, flow.w);
            Quaternion rotation = Quaternion.LookRotation(NormalizeOrDefault(direction, Vector3.right), ResolveUp(direction));
            Handles.color = new Color(0.15f, 0.85f, 1f, 0.9f);
            Handles.ArrowHandleCap(
                0,
                origin,
                rotation,
                FlowArrowMeters * magnitude,
                EventType.Repaint);
        }

        private static void DrawWakeGizmos()
        {
            Handles.color = new Color(1f, 0.55f, 0.12f, 0.85f);
            for (int i = 0; i < MaxWakeGizmos; i++)
            {
                if (!GlobalShaderDispatcher.TryGetGizmoWake(i, out Vector4 wake, out Vector4 vector))
                    continue;

                Vector3 origin = new Vector3(wake.x, wake.y, wake.z);
                Vector3 direction = new Vector3(vector.x, vector.y, vector.z);
                if (direction.sqrMagnitude <= 0.0001f)
                    continue;

                Handles.ArrowHandleCap(
                    i + 1,
                    origin,
                    Quaternion.LookRotation(NormalizeOrDefault(direction, Vector3.right), ResolveUp(direction)),
                    Mathf.Clamp(direction.magnitude, 0.5f, 5f),
                    EventType.Repaint);
            }
        }

        private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (float.IsNaN(lengthSq) || float.IsInfinity(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * (1f / Mathf.Sqrt(Mathf.Max(lengthSq, 0.0001f)));
        }

        private static Vector3 ResolveUp(Vector3 direction)
        {
            Vector3 normalized = NormalizeOrDefault(direction, Vector3.right);
            return Mathf.Abs(Vector3.Dot(normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        }
    }
}
