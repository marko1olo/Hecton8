#if UNITY_EDITOR
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    public static class DispatcherFenceLayoutValidator
    {
        private const int FenceTelemetrySizeBytes = 64;
        private const int JobDependencySizeBytes = 32;

        static DispatcherFenceLayoutValidator()
        {
            Validate();
        }

        [MenuItem("Hecton/Diagnostics/Validate Dispatcher Fence Layouts")]
        public static void Validate()
        {
            bool valid =
                UnsafeUtility.SizeOf<DispatcherFenceTelemetryEntry>() == FenceTelemetrySizeBytes &&
                UnsafeUtility.AlignOf<DispatcherFenceTelemetryEntry>() >= 8 &&
                DispatcherFenceTelemetryLayoutGuard.ValidateLayout() &&
                UnsafeUtility.SizeOf<JobDependencyDTO>() == JobDependencySizeBytes &&
                UnsafeUtility.AlignOf<JobDependencyDTO>() >= 8;

            if (!valid)
            {
                Debug.LogError(
                    "[DispatcherFenceLayoutValidator] ARM64 fence DTO layout invalid. " +
                    "DispatcherFenceTelemetryEntry must be 64B and JobDependencyDTO must be 32B.");
            }
        }
    }
}
#endif
