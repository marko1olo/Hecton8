using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    /// <summary>
    /// Allocation-free VFX compute-particle budget catalog mirrored by
    /// Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json.
    /// </summary>
    public static class VfxComputeParticleBudgetCatalog
    {
        /// <summary>MX350 maximum threads per dispatch from the compute mandate.</summary>
        public const int Mx350MaxThreadsPerDispatch = 1048576;

        /// <summary>Default portable compute group size for particle kernels.</summary>
        public const int DefaultThreadsPerGroup = 64;

        /// <summary>MX350 soft group cap. Larger tier totals must be split by pool.</summary>
        public const int Mx350SoftGroupsPerDispatch = 512;

        /// <summary>Low-tier total particle ceiling.</summary>
        public const int LowParticleCount = 8512;

        /// <summary>Mid-tier total particle ceiling.</summary>
        public const int MidParticleCount = 16384;

        /// <summary>High-tier total particle ceiling.</summary>
        public const int HighParticleCount = 104096;

        /// <summary>Ultra-tier total particle ceiling.</summary>
        public const int UltraParticleCount = 105120;

        /// <summary>Low-tier marine-snow pool ceiling.</summary>
        public const int LowMarineSnowCount = 8000;

        /// <summary>Mid-tier marine-snow pool ceiling.</summary>
        public const int MidMarineSnowCount = 14336;

        /// <summary>High-tier marine-snow pool ceiling.</summary>
        public const int HighMarineSnowCount = 100000;

        /// <summary>Ultra-tier marine-snow pool ceiling.</summary>
        public const int UltraMarineSnowCount = 100000;

        /// <summary>Low-tier bubble pool ceiling.</summary>
        public const int LowBubbleCount = 384;

        /// <summary>Mid-tier bubble pool ceiling.</summary>
        public const int MidBubbleCount = 1536;

        /// <summary>High-tier bubble pool ceiling.</summary>
        public const int HighBubbleCount = 3072;

        /// <summary>Ultra-tier bubble pool ceiling.</summary>
        public const int UltraBubbleCount = 4096;

        /// <summary>Low-tier debris pool ceiling.</summary>
        public const int LowDebrisCount = 128;

        /// <summary>Mid-tier debris pool ceiling.</summary>
        public const int MidDebrisCount = 512;

        /// <summary>High-tier debris pool ceiling.</summary>
        public const int HighDebrisCount = 1024;

        /// <summary>Ultra-tier debris pool ceiling.</summary>
        public const int UltraDebrisCount = 1024;

        /// <summary>Low-tier collision/integration step distance in meters.</summary>
        public const float LowStepDistanceMeters = 0.40f;

        /// <summary>Mid-tier collision/integration step distance in meters.</summary>
        public const float MidStepDistanceMeters = 0.25f;

        /// <summary>High-tier collision/integration step distance in meters.</summary>
        public const float HighStepDistanceMeters = 0.16f;

        /// <summary>Ultra-tier collision/integration step distance in meters.</summary>
        public const float UltraStepDistanceMeters = 0.10f;

        /// <summary>Low-tier fake depth/fog occlusion tap count.</summary>
        public const int LowShadowTaps = 0;

        /// <summary>Mid-tier fake depth/fog occlusion tap count.</summary>
        public const int MidShadowTaps = 1;

        /// <summary>High-tier fake depth/fog occlusion tap count.</summary>
        public const int HighShadowTaps = 2;

        /// <summary>Ultra-tier fake depth/fog occlusion tap count.</summary>
        public const int UltraShadowTaps = 4;

        /// <summary>Low-tier flow resample cadence in frames. Zero means disabled.</summary>
        public const int LowFlowResampleFrames = 0;

        /// <summary>Mid-tier flow resample cadence in frames.</summary>
        public const int MidFlowResampleFrames = 8;

        /// <summary>High-tier flow resample cadence in frames.</summary>
        public const int HighFlowResampleFrames = 4;

        /// <summary>Ultra-tier flow resample cadence in frames.</summary>
        public const int UltraFlowResampleFrames = 2;

        /// <summary>Emergency MarineSnow multiplier encoded as permille to avoid float policy drift.</summary>
        public const int EmergencyMarineSnowMultiplierPermille = 500;

        /// <summary>Homeostasis bit that disables particle flow advection.</summary>
        public const ulong ParticleAdvectionMask = (ulong)SystemBit.ParticleAdvection;

        /// <summary>Homeostasis bit that disables high-resolution volumetric/fake occlusion work.</summary>
        public const ulong VolumetricFogHighResMask = (ulong)SystemBit.VolumetricFogHighRes;

        /// <summary>Homeostasis bit that disables non-critical VFX pools.</summary>
        public const ulong NonCriticalVfxMask = (ulong)SystemBit.NonCriticalVfx;

        /// <summary>Prompt policy mask for sacrifice level 1.</summary>
        public const ulong PressureLevel1DisableMask = ParticleAdvectionMask;

        /// <summary>Prompt policy mask for sacrifice level 2.</summary>
        public const ulong PressureLevel2DisableMask =
            ParticleAdvectionMask |
            VolumetricFogHighResMask |
            NonCriticalVfxMask;

        /// <summary>Prompt policy mask for emergency level 3.</summary>
        public const ulong PressureLevel3DisableMask = PressureLevel2DisableMask;

        /// <summary>4x4 deterministic blue-noise fallback thresholds as half-ready float constants.</summary>
        public const float BlueNoise4x4_00 = 0.90625f;
        public const float BlueNoise4x4_01 = 0.53125f;
        public const float BlueNoise4x4_02 = 0.71875f;
        public const float BlueNoise4x4_03 = 0.84375f;
        public const float BlueNoise4x4_10 = 0.03125f;
        public const float BlueNoise4x4_11 = 0.78125f;
        public const float BlueNoise4x4_12 = 0.15625f;
        public const float BlueNoise4x4_13 = 0.34375f;
        public const float BlueNoise4x4_20 = 0.40625f;
        public const float BlueNoise4x4_21 = 0.65625f;
        public const float BlueNoise4x4_22 = 0.59375f;
        public const float BlueNoise4x4_23 = 0.96875f;
        public const float BlueNoise4x4_30 = 0.09375f;
        public const float BlueNoise4x4_31 = 0.46875f;
        public const float BlueNoise4x4_32 = 0.28125f;
        public const float BlueNoise4x4_33 = 0.21875f;

        /// <summary>
        /// Resolves the static budget for a project quality tier before homeostasis pressure is applied.
        /// </summary>
        /// <param name="qualityTier">Current hardware quality tier.</param>
        /// <returns>Compute-particle budget row.</returns>
        public static VfxComputeParticleBudget ResolveBudget(HectonQualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case HectonQualityTier.Ultra:
                    return VfxComputeParticleBudget.Ultra;
                case HectonQualityTier.High:
                    return VfxComputeParticleBudget.High;
                case HectonQualityTier.Mid:
                    return VfxComputeParticleBudget.Mid;
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                case HectonQualityTier.Unknown:
                default:
                    return VfxComputeParticleBudget.Low;
            }
        }

        /// <summary>
        /// Resolves the budget row after homeostasis pressure has clamped the visual tier.
        /// </summary>
        /// <param name="qualityTier">Selected quality tier.</param>
        /// <param name="pressureLevel">Homeostasis pressure level.</param>
        /// <returns>Pressure-gated compute-particle budget row.</returns>
        public static VfxComputeParticleBudget ResolveBudgetForPressure(HectonQualityTier qualityTier, byte pressureLevel)
        {
            if (pressureLevel >= 2)
                return VfxComputeParticleBudget.Low;
            if (pressureLevel == 1)
            {
                VfxComputeParticleBudget selected = ResolveBudget(qualityTier);
                return selected.ParticleCount > MidParticleCount ? VfxComputeParticleBudget.Mid : selected;
            }

            return ResolveBudget(qualityTier);
        }

        /// <summary>
        /// Combines the observed homeostasis mask with the VFX prompt policy mask for the active pressure level.
        /// </summary>
        /// <param name="pressureLevel">Homeostasis pressure level.</param>
        /// <param name="killSwitchMask">Observed homeostasis kill-switch mask.</param>
        /// <returns>Effective VFX kill-switch mask.</returns>
        public static ulong ResolvePolicyKillSwitchMask(byte pressureLevel, ulong killSwitchMask)
        {
            if (pressureLevel >= 3)
                return killSwitchMask | PressureLevel3DisableMask;
            if (pressureLevel >= 2)
                return killSwitchMask | PressureLevel2DisableMask;
            if (pressureLevel == 1)
                return killSwitchMask | PressureLevel1DisableMask;

            return killSwitchMask;
        }

        /// <summary>
        /// Resolves the pool capacity for the requested fluid class.
        /// </summary>
        /// <param name="qualityTier">Selected quality tier.</param>
        /// <param name="pressureLevel">Homeostasis pressure level.</param>
        /// <param name="fluidType">Fluid class emitted by the GPU particle owner.</param>
        /// <returns>Pool capacity for that class and pressure state.</returns>
        public static int ResolvePoolCapacity(
            HectonQualityTier qualityTier,
            byte pressureLevel,
            VFXEmissionProfile.FluidType fluidType)
        {
            VfxComputeParticleBudget budget = ResolveBudgetForPressure(qualityTier, pressureLevel);
            return budget.ResolvePoolCapacity(fluidType);
        }

        /// <summary>
        /// Applies emergency VFX kill switches to a resolved active particle count.
        /// </summary>
        /// <param name="activeParticleCount">Count after local density, render-scale, and VRAM pressure.</param>
        /// <param name="fluidType">Fluid class emitted by the GPU particle owner.</param>
        /// <param name="killSwitchMask">Homeostasis kill-switch mask.</param>
        /// <returns>Final active count after kill switches.</returns>
        public static int ApplyKillSwitchCount(
            int activeParticleCount,
            VFXEmissionProfile.FluidType fluidType,
            ulong killSwitchMask)
        {
            return ApplyKillSwitchCount(
                activeParticleCount,
                fluidType,
                killSwitchMask,
                byte.MaxValue);
        }

        /// <summary>
        /// Applies pressure-aware emergency VFX kill switches to a resolved active particle count.
        /// </summary>
        /// <param name="activeParticleCount">Count after local density, render-scale, and VRAM pressure.</param>
        /// <param name="fluidType">Fluid class emitted by the GPU particle owner.</param>
        /// <param name="killSwitchMask">Effective VFX kill-switch mask.</param>
        /// <param name="pressureLevel">Homeostasis pressure level.</param>
        /// <returns>Final active count after pressure-aware kill switches.</returns>
        public static int ApplyKillSwitchCount(
            int activeParticleCount,
            VFXEmissionProfile.FluidType fluidType,
            ulong killSwitchMask,
            byte pressureLevel)
        {
            if (activeParticleCount <= 0)
                return 0;

            if ((killSwitchMask & NonCriticalVfxMask) == 0UL)
                return activeParticleCount;

            if (fluidType == VFXEmissionProfile.FluidType.Bubble ||
                fluidType == VFXEmissionProfile.FluidType.Debris)
            {
                return 0;
            }

            if (pressureLevel >= 3)
                return math.max(64, activeParticleCount * EmergencyMarineSnowMultiplierPermille / 1000);

            return activeParticleCount;
        }

        /// <summary>
        /// Resolves the number of 64-thread groups needed for a particle count.
        /// </summary>
        /// <param name="particleCount">Particle count.</param>
        /// <returns>Dispatch group count.</returns>
        public static int ResolveDispatchGroups(int particleCount)
        {
            return math.max(1, (math.max(1, particleCount) + DefaultThreadsPerGroup - 1) / DefaultThreadsPerGroup);
        }
    }

    /// <summary>
    /// Immutable compute-particle budget row.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VfxComputeParticleBudget
    {
        /// <summary>Low-tier budget row.</summary>
        public static readonly VfxComputeParticleBudget Low = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.LowParticleCount,
            VfxComputeParticleBudgetCatalog.LowMarineSnowCount,
            VfxComputeParticleBudgetCatalog.LowBubbleCount,
            VfxComputeParticleBudgetCatalog.LowDebrisCount,
            VfxComputeParticleBudgetCatalog.LowStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.LowShadowTaps,
            VfxComputeParticleBudgetCatalog.LowFlowResampleFrames);

        /// <summary>Mid-tier budget row.</summary>
        public static readonly VfxComputeParticleBudget Mid = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.MidParticleCount,
            VfxComputeParticleBudgetCatalog.MidMarineSnowCount,
            VfxComputeParticleBudgetCatalog.MidBubbleCount,
            VfxComputeParticleBudgetCatalog.MidDebrisCount,
            VfxComputeParticleBudgetCatalog.MidStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.MidShadowTaps,
            VfxComputeParticleBudgetCatalog.MidFlowResampleFrames);

        /// <summary>High-tier budget row.</summary>
        public static readonly VfxComputeParticleBudget High = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.HighParticleCount,
            VfxComputeParticleBudgetCatalog.HighMarineSnowCount,
            VfxComputeParticleBudgetCatalog.HighBubbleCount,
            VfxComputeParticleBudgetCatalog.HighDebrisCount,
            VfxComputeParticleBudgetCatalog.HighStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.HighShadowTaps,
            VfxComputeParticleBudgetCatalog.HighFlowResampleFrames);

        /// <summary>Ultra-tier budget row.</summary>
        public static readonly VfxComputeParticleBudget Ultra = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.UltraParticleCount,
            VfxComputeParticleBudgetCatalog.UltraMarineSnowCount,
            VfxComputeParticleBudgetCatalog.UltraBubbleCount,
            VfxComputeParticleBudgetCatalog.UltraDebrisCount,
            VfxComputeParticleBudgetCatalog.UltraStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.UltraShadowTaps,
            VfxComputeParticleBudgetCatalog.UltraFlowResampleFrames);

        /// <summary>Total particle count budget.</summary>
        public readonly int ParticleCount;

        /// <summary>Marine-snow pool count.</summary>
        public readonly int MarineSnowCount;

        /// <summary>Bubble pool count.</summary>
        public readonly int BubbleCount;

        /// <summary>Debris pool count.</summary>
        public readonly int DebrisCount;

        /// <summary>Collision/integration step distance in meters.</summary>
        public readonly float StepDistanceMeters;

        /// <summary>Fake depth/fog occlusion tap count. Particle shadow casting remains forbidden.</summary>
        public readonly int ShadowTaps;

        /// <summary>Flow resample cadence in frames. Zero disables flow resampling.</summary>
        public readonly int FlowResampleFrames;

        /// <summary>
        /// Creates an immutable VFX compute-particle budget row.
        /// </summary>
        /// <param name="particleCount">Total particle count budget.</param>
        /// <param name="marineSnowCount">Marine-snow pool count.</param>
        /// <param name="bubbleCount">Bubble pool count.</param>
        /// <param name="debrisCount">Debris pool count.</param>
        /// <param name="stepDistanceMeters">Collision/integration step distance in meters.</param>
        /// <param name="shadowTaps">Fake depth/fog occlusion tap count.</param>
        /// <param name="flowResampleFrames">Flow resample cadence in frames.</param>
        public VfxComputeParticleBudget(
            int particleCount,
            int marineSnowCount,
            int bubbleCount,
            int debrisCount,
            float stepDistanceMeters,
            int shadowTaps,
            int flowResampleFrames)
        {
            ParticleCount = particleCount;
            MarineSnowCount = marineSnowCount;
            BubbleCount = bubbleCount;
            DebrisCount = debrisCount;
            StepDistanceMeters = stepDistanceMeters;
            ShadowTaps = shadowTaps;
            FlowResampleFrames = flowResampleFrames;
        }

        /// <summary>
        /// Resolves the pool capacity for a fluid class.
        /// </summary>
        /// <param name="fluidType">Fluid class emitted by the GPU particle owner.</param>
        /// <returns>Pool capacity for the fluid class.</returns>
        public int ResolvePoolCapacity(VFXEmissionProfile.FluidType fluidType)
        {
            switch (fluidType)
            {
                case VFXEmissionProfile.FluidType.Bubble:
                    return BubbleCount;
                case VFXEmissionProfile.FluidType.Debris:
                    return DebrisCount;
                case VFXEmissionProfile.FluidType.Plankton:
                    return MarineSnowCount;
                case VFXEmissionProfile.FluidType.Snow:
                default:
                    return MarineSnowCount;
            }
        }
    }
}
