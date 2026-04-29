#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Play-mode editor sentinel that escalates frequent gen0 collections as hard console errors.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class GCSentinel : MonoBehaviour
    {
        private const int FrameWindow = 60;
        private const string SentinelObjectName = "__GC_SENTINEL";

        private int _framesRemaining = FrameWindow;
        private int _lastGen0CollectionCount;
        private long _lastManagedHeapBytes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying)
                return;

            GCSentinel existing = FindAnyObjectByType<GCSentinel>();
            if (existing != null)
                return;

            GameObject sentinelObject = new GameObject(SentinelObjectName);
            sentinelObject.hideFlags = HideFlags.HideAndDontSave;
            sentinelObject.AddComponent<GCSentinel>();
        }

        private void Awake()
        {
            hideFlags = HideFlags.HideAndDontSave;
            _lastGen0CollectionCount = GC.CollectionCount(0);
            _lastManagedHeapBytes = GC.GetTotalMemory(false);
            _framesRemaining = FrameWindow;
        }

        private void LateUpdate()
        {
            if (--_framesRemaining > 0)
                return;

            int currentGen0Collections = GC.CollectionCount(0);
            int gen0Delta = currentGen0Collections - _lastGen0CollectionCount;
            long currentManagedHeapBytes = GC.GetTotalMemory(false);
            long managedHeapDeltaBytes = currentManagedHeapBytes - _lastManagedHeapBytes;

            if (gen0Delta > 1)
            {
                Debug.LogError(
                    $"[GCSentinel] GEN0 GC SPIKE DETECTED | Collections/60f={gen0Delta} | ManagedHeapMB={currentManagedHeapBytes / 1048576f:0.00} | HeapDeltaKB={managedHeapDeltaBytes / 1024f:0.0}");
            }

            _lastGen0CollectionCount = currentGen0Collections;
            _lastManagedHeapBytes = currentManagedHeapBytes;
            _framesRemaining = FrameWindow;
        }
    }
}
#endif
