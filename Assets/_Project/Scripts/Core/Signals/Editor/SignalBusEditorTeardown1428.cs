#if UNITY_EDITOR
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.EditorSignals
{
    [InitializeOnLoad]
    internal static class SignalBusEditorTeardown1428
    {
        static SignalBusEditorTeardown1428()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.quitting -= DisposeBeforeEditorQuit;
            EditorApplication.quitting += DisposeBeforeEditorQuit;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                DisposeSignalLanes();
            }
        }

        private static void DisposeBeforeAssemblyReload()
        {
            DisposeSignalLanes();
        }

        private static void DisposeBeforeEditorQuit()
        {
            DisposeSignalLanes();
        }

        private static void DisposeSignalLanes()
        {
            try
            {
                GlobalSignals.DisposeAllQueues();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[SignalBusEditorTeardown1428] Signal lane teardown failed: " + exception.Message);
            }
        }
    }
}
#endif
