using System.Globalization;
using Hecton8.Graphics.Materials;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Graphics.Materials.Editor
{
    public sealed class UberNoirMaterialLabWindow : EditorWindow
    {
        private static readonly string[] DebugModes = { "Off", "Wear / Bio / Salt" };

        private float _globalRustRate = 1.0f;
        private float _causticIntensity = 0.65f;
        private float _sssTranslucency = 0.55f;
        private float _saltLineDepth;
        private int _debugMode;
        private int _visibleCount;
        private float _lastUploadMs;
        private bool _bound;

        [MenuItem("Hecton8/Rendering/UberNoir Material Lab")]
        private static void Open()
        {
            UberNoirMaterialLabWindow window = GetWindow<UberNoirMaterialLabWindow>("UberNoir Material Lab");
            window.minSize = new Vector2(380f, 280f);
            window.RefreshFromRuntime();
        }

        private void OnEnable()
        {
            RefreshFromRuntime();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UberNoir Material Lab", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime", _bound ? "GlobalDataVault bound" : "Play Mode bridge pending");
                EditorGUILayout.LabelField("Visible DTOs", _visibleCount.ToString());
                EditorGUILayout.LabelField("Last Upload ms", _lastUploadMs.ToString("0.000", CultureInfo.InvariantCulture));
            }

            EditorGUI.BeginChangeCheck();
            _globalRustRate = EditorGUILayout.Slider("Global Rust Rate", _globalRustRate, 0.0f, 4.0f);
            _causticIntensity = EditorGUILayout.Slider("Caustic Intensity", _causticIntensity, 0.0f, 2.0f);
            _sssTranslucency = EditorGUILayout.Slider("SSS Translucency", _sssTranslucency, 0.0f, 1.0f);
            _saltLineDepth = EditorGUILayout.Slider("Salt Line Depth", _saltLineDepth, -200.0f, 50.0f);
            _debugMode = EditorGUILayout.Popup("Debug Heatmap", _debugMode, DebugModes);
            if (EditorGUI.EndChangeCheck())
                PushToRuntime();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Refresh Runtime Values"))
                RefreshFromRuntime();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("InstanceMaterialDTO", "16 bytes: wear/salt/bio/hash");
                EditorGUILayout.LabelField("GlobalShaderConstantsDTO", "48 bytes: SSS/Caustic/Wear/pads");
                EditorGUILayout.LabelField("Mutation Path", "GraphicsBuffer + DataVault only");
            }
        }

        private void PushToRuntime()
        {
            _bound = ShinobuMaterialResponseRuntime.TryWriteEditorTuning(
                _globalRustRate,
                _causticIntensity,
                _sssTranslucency,
                _saltLineDepth,
                (uint)_debugMode);
        }

        private void RefreshFromRuntime()
        {
            uint debugMode;
            _bound = ShinobuMaterialResponseRuntime.TryReadEditorTuning(
                out _globalRustRate,
                out _causticIntensity,
                out _sssTranslucency,
                out _saltLineDepth,
                out debugMode,
                out _visibleCount,
                out _lastUploadMs);
            _debugMode = (int)debugMode;
            Repaint();
        }
    }
}
