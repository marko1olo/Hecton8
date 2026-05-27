#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.World;
using UnityEditor;

namespace Hecton8.World.Editor
{
    [InitializeOnLoad]
    internal static class FloraInteractionMemorySovereigntyValidator1327
    {
        static FloraInteractionMemorySovereigntyValidator1327()
        {
            Validate();
        }

        [MenuItem("Hecton8/Validation/Flora Interaction Memory Sovereignty 1327")]
        private static void ValidateMenu()
        {
            Validate();
        }

        private static void Validate()
        {
            int failureMask = 0;
            if (!FloraInteractionManager.ValidateFloraDisplacementDtoLayout(
                    out int displacementSize,
                    out int forceOffset,
                    out int decayOffset) ||
                displacementSize != 16 ||
                forceOffset != 0 ||
                decayOffset != 12)
            {
                failureMask |= 1 << 0;
            }

            if (!FloraInteractionManager.ValidateFloraSwayTelemetryLayout(
                    out int swayTelemetrySize,
                    out int swayFieldCenterOffset,
                    out int swayCpuMicrosecondsOffset,
                    out int swayResolutionOffset) ||
                swayTelemetrySize != 64 ||
                swayFieldCenterOffset != 12 ||
                swayCpuMicrosecondsOffset != 56 ||
                swayResolutionOffset != 60)
            {
                failureMask |= 1 << 1;
            }

            if (!FloraInteractionManager.ValidateFloraStiffnessRuleDtoLayout(
                    out int stiffnessSize,
                    out int plantHashOffset,
                    out int stiffnessFlagsOffset) ||
                stiffnessSize != 16 ||
                plantHashOffset != 0 ||
                stiffnessFlagsOffset != 12)
            {
                failureMask |= 1 << 6;
            }

            if (!FloraInteractionManager.ValidateFloraMemoryTelemetryLayout(
                    out int memoryTelemetrySize,
                    out int bufferIdOffset,
                    out int flagsOffset,
                    out int cpuMicrosecondsOffset) ||
                memoryTelemetrySize != 64 ||
                bufferIdOffset != 8 ||
                flagsOffset != 28 ||
                cpuMicrosecondsOffset != 56)
            {
                failureMask |= 1 << 2;
            }

            if (!FloraInteractionManager.ValidateParasiteNodeLayout(
                    out int parasiteSize,
                    out int birthOffset,
                    out int stateOffset) ||
                parasiteSize != 64 ||
                birthOffset != 0 ||
                stateOffset != 60)
            {
                failureMask |= 1 << 3;
            }

            if (!FloraInteractionManager.ValidateFloraCascadeEventPayloadLayout(
                    out int cascadeSize,
                    out int centerOffset,
                    out int radiusOffset) ||
                cascadeSize != 32 ||
                centerOffset != 0 ||
                radiusOffset != 16)
            {
                failureMask |= 1 << 4;
            }

            if (!FloraInteractionManager.ValidateConsumedWakeTelemetryLayout(
                    out int wakeTelemetrySize,
                    out int budgetPressureOffset) ||
                wakeTelemetrySize != 64 ||
                budgetPressureOffset != 60)
            {
                failureMask |= 1 << 5;
            }

            if (failureMask != 0)
                throw new FatalArchitectureException("1327 flora interaction DTO layout violation mask=" + failureMask);
        }
    }
}
#endif
