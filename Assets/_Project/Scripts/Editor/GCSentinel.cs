#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Play-mode editor sentinel that escalates frequent gen0 collections as hard console errors.
    /// </summary>
    [InitializeOnLoad]
    internal static class GCSentinel
    {
        private const int FrameWindow = 60;

        private static int s_framesRemaining = FrameWindow;
        private static int s_lastGen0CollectionCount;
        private static long s_lastManagedHeapBytes;
        private static bool s_installed;

        static GCSentinel()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying)
                return;

            ResetCounters();
            if (s_installed)
                return;

            EditorApplication.update -= TickEditor;
            EditorApplication.update += TickEditor;
            s_installed = true;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Uninstall();
            }
        }

        private static void Uninstall()
        {
            if (!s_installed)
                return;

            EditorApplication.update -= TickEditor;
            s_installed = false;
        }

        private static void ResetCounters()
        {
            s_lastGen0CollectionCount = GC.CollectionCount(0);
            s_lastManagedHeapBytes = GC.GetTotalMemory(false);
            s_framesRemaining = FrameWindow;
        }

        private static void TickEditor()
        {
            if (!EditorApplication.isPlaying)
            {
                Uninstall();
                return;
            }

            if (--s_framesRemaining > 0)
                return;

            int currentGen0Collections = GC.CollectionCount(0);
            int gen0Delta = currentGen0Collections - s_lastGen0CollectionCount;
            long currentManagedHeapBytes = GC.GetTotalMemory(false);

            if (gen0Delta > 1)
            {
                Debug.LogError("[GCSentinel] GEN0 GC spike detected.");
            }

            s_lastGen0CollectionCount = currentGen0Collections;
            s_lastManagedHeapBytes = currentManagedHeapBytes;
            s_framesRemaining = FrameWindow;
        }
    }
}
#endif
