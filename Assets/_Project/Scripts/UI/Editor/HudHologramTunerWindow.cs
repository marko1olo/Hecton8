using Hecton8.UI;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public sealed class HudHologramTunerWindow : EditorWindow
    {
        private WristHologramHudRuntime _target;
        private float _distance = 0.14f;
        private float _textScale = 0.014f;
        private float _glitchMultiplier = 1.15f;
        private Color _lowColor = new Color(0.16f, 0.88f, 0.76f, 0.74f);
        private Color _midColor = new Color(0.42f, 0.96f, 0.92f, 0.88f);
        private Color _dangerColor = new Color(1.0f, 0.12f, 0.05f, 0.95f);

        [MenuItem("Hecton8/UI/HUD Hologram Tuner")]
        public static void Open()
        {
            GetWindow<HudHologramTunerWindow>("HUD Hologram Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneOverlay;
            SceneView.duringSceneGui += DrawSceneOverlay;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneOverlay;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _target = (WristHologramHudRuntime)EditorGUILayout.ObjectField("Runtime", _target, typeof(WristHologramHudRuntime), true);
            _distance = EditorGUILayout.Slider("Hologram Distance from Wrist", _distance, 0.02f, 0.40f);
            _textScale = EditorGUILayout.Slider("Text Scale", _textScale, 0.002f, 0.045f);
            _glitchMultiplier = EditorGUILayout.Slider("Glitch Multiplier", _glitchMultiplier, 0f, 6f);
            _lowColor = EditorGUILayout.ColorField("Low Color", _lowColor);
            _midColor = EditorGUILayout.ColorField("Mid Color", _midColor);
            _dangerColor = EditorGUILayout.ColorField("Danger Color", _dangerColor);

            bool changed = EditorGUI.EndChangeCheck();
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button("Apply"))
                    ApplyToRuntime();

                if (GUILayout.Button("Reload font_metrics_override.csv"))
                    _target.TryReloadFontMetricsOverride();
            }

            if (changed && Application.isPlaying)
                ApplyToRuntime();

            if (_target == null && Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent(out WristHologramHudRuntime selectedTarget))
            {
                _target = selectedTarget;
            }
        }

        private void ApplyToRuntime()
        {
            if (_target == null)
                return;

            _target.ApplyTunerSettings(_distance, _textScale, _glitchMultiplier, _lowColor, _midColor, _dangerColor);
            SceneView.RepaintAll();
        }

        private void DrawSceneOverlay(SceneView sceneView)
        {
            if (_target == null || !_target.TryGetPdaGridGizmo(out Matrix4x4 matrix, out Vector3 size))
                return;

            Handles.color = new Color(_midColor.r, _midColor.g, _midColor.b, 0.85f);
            using (new Handles.DrawingScope(matrix))
                Handles.DrawWireCube(Vector3.zero, size);
        }
    }
}
