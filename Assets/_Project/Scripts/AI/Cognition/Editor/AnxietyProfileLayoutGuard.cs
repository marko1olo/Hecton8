#if UNITY_EDITOR
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.AI.Cognition.Editor
{
    [InitializeOnLoad]
    internal static class AnxietyProfileLayoutGuard
    {
        static AnxietyProfileLayoutGuard()
        {
            bool valid = UtilityAICognitionVault.TryRunAnxietySelfAudit(out uint failureMask) &&
                         failureMask == 0u &&
                         UtilityAICognitionVault.ValidateAnxietyLayouts() &&
                         UnsafeUtility.SizeOf<AnxietyProfileDTO>() == 16 &&
                         UnsafeUtility.AlignOf<AnxietyProfileDTO>() == 4 &&
                         UnsafeUtility.SizeOf<AnxietyDecayScratchDTO>() == 64 &&
                         UnsafeUtility.SizeOf<AnxietyTelemetryEntry>() == 64;
            if (!valid)
            {
                throw new global::Hecton8.Core.FatalArchitectureException(
                    "SHINOBU_312 anxiety layout drift. FailureMask=" + failureMask + ". Required Profile=16/Align4, Scratch=64, Telemetry=64 for ARM64 deterministic Vault rows.");
            }
        }
    }
}
#endif
