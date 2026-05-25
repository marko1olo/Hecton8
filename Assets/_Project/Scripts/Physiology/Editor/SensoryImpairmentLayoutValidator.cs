#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Physiology;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    [InitializeOnLoad]
    internal static class SensoryImpairmentLayoutValidator
    {
        static SensoryImpairmentLayoutValidator()
        {
            Validate();
        }

        [MenuItem("Hecton8/Physiology/Validate Sensory Impairment Layout")]
        private static void ValidateMenu()
        {
            Validate();
        }

        internal static bool Validate()
        {
            bool valid = ShinobuSensoryImpairmentLayoutGuards.ValidateSensoryLayouts() &&
                         UnsafeUtility.SizeOf<SensoryImpairmentDTO>() == 32 &&
                         UnsafeUtility.SizeOf<SensoryImpairmentTuningDTO>() == 64 &&
                         UnsafeUtility.SizeOf<SensoryInputDriftDebugDTO>() == 64 &&
                         UnsafeUtility.SizeOf<SensoryImpairmentTelemetryEntry>() == 64;
            if (!valid)
            {
                Debug.LogError("[SHINOBU_322] Sensory impairment DTO layout violation. Required SensoryImpairmentDTO Size=32 and DriftDebug/Telemetry Size=64 with explicit ARM64-safe offsets.");
                throw new FatalArchitectureException("SHINOBU_322 sensory layout violation.");
            }

            return true;
        }
    }
}
#endif
