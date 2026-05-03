#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Editor-only leak overlay bridge driven by dispatcher heartbeats.
    /// </summary>
    public static class NativeAllocationTrackerRuntimeBridge
    {
        private const float OverlayWidth = 960f;
        private const float OverlayHeight = 88f;
        private const float OverlayMargin = 24f;

        private static bool _initialized;
        private static bool _hasActiveLeak;
        private static string _activeMessage;
        private static GUIStyle _labelStyle;

        public static void NotifyDispatcherHeartbeat()
        {
            if (Application.isBatchMode)
                return;

            EnsureInitialized();
            if (_hasActiveLeak)
                SceneView.RepaintAll();
        }

        public static void ReportLeak(string message)
        {
            if (Application.isBatchMode)
                return;

            EnsureInitialized();
            _hasActiveLeak = true;
            _activeMessage = message;
            SceneView.RepaintAll();
        }

        public static void ClearLeak()
        {
            if (Application.isBatchMode)
                return;

            _hasActiveLeak = false;
            _activeMessage = null;
            SceneView.RepaintAll();
        }

        private static void EnsureInitialized()
        {
            if (Application.isBatchMode)
                return;

            if (_initialized)
                return;

            _initialized = true;
            SceneView.duringSceneGui -= HandleSceneGui;
            SceneView.duringSceneGui += HandleSceneGui;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                ClearLeak();
        }

        private static void HandleSceneGui(SceneView sceneView)
        {
            if (!_hasActiveLeak || !EditorApplication.isPlaying || string.IsNullOrEmpty(_activeMessage))
                return;

            Handles.BeginGUI();
            GUI.color = new Color(1f, 0.18f, 0.12f, 1f);
            GUI.Label(
                new Rect(OverlayMargin, OverlayMargin, OverlayWidth, OverlayHeight),
                _activeMessage,
                ResolveLabelStyle());
            GUI.color = Color.white;
            Handles.EndGUI();
        }

        private static GUIStyle ResolveLabelStyle()
        {
            if (_labelStyle != null)
                return _labelStyle;

            // COLD ALLOC: GUIStyle[1] — editor-only native leak overlay label style — owner: NativeAllocationTrackerRuntimeBridge
            _labelStyle = new GUIStyle(EditorStyles.whiteLargeLabel)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _labelStyle.normal.textColor = new Color(1f, 0.18f, 0.12f, 1f);
            return _labelStyle;
        }
    }
}
#else
namespace Hecton8.Core
{
    internal static class NativeAllocationTrackerRuntimeBridge
    {
        internal static void NotifyDispatcherHeartbeat() { }
        internal static void ReportLeak(string message) { }
        internal static void ClearLeak() { }
    }
}
#endif
