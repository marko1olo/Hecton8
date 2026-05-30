using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    internal static class VfxComputeParticleBudgetCatalogLayout
    {
        public const int VfxComputeParticleBudgetStrideBytes = 32;
    }

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

        /// <summary>Portable maximum dispatch groups per axis.</summary>
        public const int MaxDispatchGroupsPerDimension = 65535;

        /// <summary>MX350 soft group cap. Larger quality totals must be split by pool.</summary>
        public const int Mx350SoftGroupsPerDispatch = 512;

        /// <summary>Minimum-quality total particle ceiling.</summary>
        public const int MinimumQualityParticleCount = 8512;

        /// <summary>Middle-quality total particle ceiling.</summary>
        public const int MiddleQualityParticleCount = 16384;

        /// <summary>Maximum-quality total particle ceiling.</summary>
        public const int MaximumQualityParticleCount = 104096;

        /// <summary>Visual-overkill total particle ceiling.</summary>
        public const int OverkillQualityParticleCount = 105120;

        /// <summary>Minimum-quality marine-snow pool ceiling.</summary>
        public const int MinimumQualityMarineSnowCount = 8000;

        /// <summary>Middle-quality marine-snow pool ceiling.</summary>
        public const int MiddleQualityMarineSnowCount = 14336;

        /// <summary>Maximum-quality marine-snow pool ceiling.</summary>
        public const int MaximumQualityMarineSnowCount = 100000;

        /// <summary>Visual-overkill marine-snow pool ceiling.</summary>
        public const int OverkillQualityMarineSnowCount = 100000;

        /// <summary>Minimum-quality bubble pool ceiling.</summary>
        public const int MinimumQualityBubbleCount = 384;

        /// <summary>Middle-quality bubble pool ceiling.</summary>
        public const int MiddleQualityBubbleCount = 1536;

        /// <summary>Maximum-quality bubble pool ceiling.</summary>
        public const int MaximumQualityBubbleCount = 3072;

        /// <summary>Visual-overkill bubble pool ceiling.</summary>
        public const int OverkillQualityBubbleCount = 4096;

        /// <summary>Minimum-quality debris pool ceiling.</summary>
        public const int MinimumQualityDebrisCount = 128;

        /// <summary>Middle-quality debris pool ceiling.</summary>
        public const int MiddleQualityDebrisCount = 512;

        /// <summary>Maximum-quality debris pool ceiling.</summary>
        public const int MaximumQualityDebrisCount = 1024;

        /// <summary>Visual-overkill debris pool ceiling.</summary>
        public const int OverkillQualityDebrisCount = 1024;

        /// <summary>Minimum-quality collision/integration step distance in meters.</summary>
        public const float MinimumQualityStepDistanceMeters = 0.40f;

        /// <summary>Middle-quality collision/integration step distance in meters.</summary>
        public const float MiddleQualityStepDistanceMeters = 0.25f;

        /// <summary>Maximum-quality collision/integration step distance in meters.</summary>
        public const float MaximumQualityStepDistanceMeters = 0.16f;

        /// <summary>Visual-overkill collision/integration step distance in meters.</summary>
        public const float OverkillQualityStepDistanceMeters = 0.10f;

        /// <summary>Minimum-quality fake depth/fog occlusion tap count.</summary>
        public const int MinimumQualityShadowTaps = 0;

        /// <summary>Middle-quality fake depth/fog occlusion tap count.</summary>
        public const int MiddleQualityShadowTaps = 1;

        /// <summary>Maximum-quality fake depth/fog occlusion tap count.</summary>
        public const int MaximumQualityShadowTaps = 2;

        /// <summary>Visual-overkill fake depth/fog occlusion tap count.</summary>
        public const int OverkillQualityShadowTaps = 4;

        /// <summary>Minimum-quality flow resample cadence in frames. Zero means disabled.</summary>
        public const int MinimumQualityFlowResampleFrames = 0;

        /// <summary>Middle-quality flow resample cadence in frames.</summary>
        public const int MiddleQualityFlowResampleFrames = 8;

        /// <summary>Maximum-quality flow resample cadence in frames.</summary>
        public const int MaximumQualityFlowResampleFrames = 4;

        /// <summary>Visual-overkill flow resample cadence in frames.</summary>
        public const int OverkillQualityFlowResampleFrames = 2;

        /// <summary>Emergency flow resample cadence in frames. Non-zero keeps sparse visual drift alive.</summary>
        public const int EmergencyFlowResampleFrames = 16;

        /// <summary>Minimum policy-compressed particle advection weight.</summary>
        public const float MaskedParticleAdvectionWeightFloor = 0.12f;

        /// <summary>Minimum policy-compressed fake occlusion/depth weight.</summary>
        public const float MaskedVolumetricQualityWeightFloor = 0.18f;

        /// <summary>Minimum pressure scalar applied when a shadow policy mask is active without pressure.</summary>
        public const float MaskedShadowPolicyPressureFloor = 0.33333334f;

        /// <summary>Emergency MarineSnow multiplier encoded as permille to avoid float policy drift.</summary>
        public const int EmergencyMarineSnowMultiplierPermille = 500;

        /// <summary>Emergency non-critical VFX survival multiplier encoded as permille.</summary>
        public const int EmergencyNonCriticalVfxMultiplierPermille = 125;

        /// <summary>Bubble floor preserved under non-critical VFX pressure.</summary>
        public const int EmergencyBubbleSurvivalCount = 32;

        /// <summary>Debris floor preserved under non-critical VFX pressure.</summary>
        public const int EmergencyDebrisSurvivalCount = 8;

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
        /// Resolves the budget row from a continuous global quality weight before homeostasis pressure is applied.
        /// </summary>
        /// <param name="globalQualityWeight">Continuous visual quality weight, 0..1.</param>
        /// <returns>Compute-particle budget row.</returns>
        public static VfxComputeParticleBudget ResolveBudget(float globalQualityWeight)
        {
            return ResolveContinuousBudget(globalQualityWeight, 0f);
        }

        /// <summary>
        /// Resolves the budget row after homeostasis pressure has continuously compressed the visual budget.
        /// </summary>
        /// <param name="globalQualityWeight">Continuous visual quality weight, 0..1.</param>
        /// <param name="pressureLevel">Homeostasis pressure level encoded as 0..3.</param>
        /// <returns>Pressure-gated compute-particle budget row.</returns>
        public static VfxComputeParticleBudget ResolveBudgetForPressure(float globalQualityWeight, byte pressureLevel)
        {
            return ResolveContinuousBudget(globalQualityWeight, math.saturate(pressureLevel * 0.33333334f));
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
        /// Converts a policy mask into a continuous quality multiplier. A masked feature is compressed, not killed.
        /// </summary>
        /// <param name="killSwitchMask">Effective VFX policy mask.</param>
        /// <param name="policyMask">Policy bit consumed by the caller.</param>
        /// <param name="pressureLevel">Homeostasis pressure level encoded as 0..3.</param>
        /// <param name="floorWeight">Emergency survival floor for the feature.</param>
        /// <returns>Continuous multiplier in the range floor..1.</returns>
        public static float ResolvePolicyQualityWeight(
            ulong killSwitchMask,
            ulong policyMask,
            byte pressureLevel,
            float floorWeight)
        {
            float pressure01 = pressureLevel == byte.MaxValue
                ? 1f
                : math.saturate(pressureLevel * 0.33333334f);
            return ResolvePolicyQualityWeight(killSwitchMask, policyMask, pressure01, floorWeight);
        }

        /// <summary>
        /// Converts a policy mask into a continuous quality multiplier using caller-owned pressure.
        /// </summary>
        /// <param name="killSwitchMask">Effective VFX policy mask.</param>
        /// <param name="policyMask">Policy bit consumed by the caller.</param>
        /// <param name="pressure01">Continuous pressure scalar.</param>
        /// <param name="floorWeight">Emergency survival floor for the feature.</param>
        /// <returns>Continuous multiplier in the range floor..1.</returns>
        public static float ResolvePolicyQualityWeight(
            ulong killSwitchMask,
            ulong policyMask,
            float pressure01,
            float floorWeight)
        {
            float mask01 = (killSwitchMask & policyMask) != 0UL ? 1f : 0f;
            float pressure = math.saturate(pressure01);
            float midPressure01 = math.smoothstep(0.18f, 0.45f, pressure);
            float emergencyPressure01 = math.smoothstep(0.48f, 0.90f, pressure);
            float compressedWeight = math.lerp(0.70f, 0.42f, midPressure01);
            compressedWeight = math.lerp(compressedWeight, math.saturate(floorWeight), emergencyPressure01);
            return math.lerp(1f, compressedWeight, mask01);
        }

        /// <summary>
        /// Keeps masked particle advection on a sparse cadence instead of disabling it.
        /// </summary>
        /// <param name="flowResampleFrames">Resolved flow resample cadence.</param>
        /// <param name="killSwitchMask">Effective VFX policy mask.</param>
        /// <param name="pressureLevel">Homeostasis pressure level encoded as 0..3.</param>
        /// <returns>Pressure-compressed non-zero cadence when advection is masked.</returns>
        public static int ResolvePolicyFlowResampleFrames(
            int flowResampleFrames,
            ulong killSwitchMask,
            byte pressureLevel)
        {
            if ((killSwitchMask & ParticleAdvectionMask) == 0UL)
                return math.max(0, flowResampleFrames);

            float pressure01 = pressureLevel == byte.MaxValue
                ? 1f
                : math.saturate(pressureLevel * 0.33333334f);
            float midPressure01 = math.smoothstep(0.18f, 0.45f, pressure01);
            float emergencyPressure01 = math.smoothstep(0.48f, 0.90f, pressure01);
            float cadence = math.lerp(
                math.max(1f, flowResampleFrames),
                MiddleQualityFlowResampleFrames,
                midPressure01);
            cadence = math.lerp(cadence, EmergencyFlowResampleFrames, emergencyPressure01);
            return math.clamp((int)(cadence + 0.5f), 1, EmergencyFlowResampleFrames);
        }

        /// <summary>
        /// Compresses fake depth/fog shadow taps under volumetric pressure instead of a hard middle-tier clamp.
        /// </summary>
        /// <param name="shadowTaps">Resolved fake depth/fog tap count.</param>
        /// <param name="killSwitchMask">Effective VFX policy mask.</param>
        /// <param name="pressureLevel">Homeostasis pressure level encoded as 0..3.</param>
        /// <returns>Pressure-compressed tap count.</returns>
        public static int ResolvePolicyShadowTaps(
            int shadowTaps,
            ulong killSwitchMask,
            byte pressureLevel)
        {
            float pressure01 = pressureLevel == byte.MaxValue
                ? 1f
                : math.saturate(pressureLevel * 0.33333334f);
            return ResolvePolicyShadowTaps(shadowTaps, killSwitchMask, pressure01);
        }

        /// <summary>
        /// Compresses fake depth/fog shadow taps under caller-owned pressure.
        /// </summary>
        /// <param name="shadowTaps">Resolved fake depth/fog tap count.</param>
        /// <param name="killSwitchMask">Effective VFX policy mask.</param>
        /// <param name="pressure01">Continuous pressure scalar.</param>
        /// <returns>Pressure-compressed tap count.</returns>
        public static int ResolvePolicyShadowTaps(
            int shadowTaps,
            ulong killSwitchMask,
            float pressure01)
        {
            int clampedTaps = math.clamp(shadowTaps, MinimumQualityShadowTaps, OverkillQualityShadowTaps);
            if ((killSwitchMask & VolumetricFogHighResMask) == 0UL)
                return clampedTaps;

            float pressure = math.max(math.saturate(pressure01), MaskedShadowPolicyPressureFloor);
            float midPressure01 = math.smoothstep(0.18f, 0.45f, pressure);
            float emergencyPressure01 = math.smoothstep(0.48f, 0.90f, pressure);
            float taps = math.lerp(
                clampedTaps,
                math.min(clampedTaps, MiddleQualityShadowTaps),
                midPressure01);
            taps = math.lerp(taps, MinimumQualityShadowTaps, emergencyPressure01);
            return math.clamp((int)(taps + 0.5f), MinimumQualityShadowTaps, clampedTaps);
        }

        /// <summary>
        /// Resolves the pool capacity for the requested fluid class.
        /// </summary>
        /// <param name="globalQualityWeight">Continuous visual quality weight, 0..1.</param>
        /// <param name="pressureLevel">Homeostasis pressure level.</param>
        /// <param name="fluidType">Fluid class emitted by the GPU particle owner.</param>
        /// <returns>Pool capacity for that class and pressure state.</returns>
        public static int ResolvePoolCapacity(
            float globalQualityWeight,
            byte pressureLevel,
            VFXEmissionProfile.FluidType fluidType)
        {
            VfxComputeParticleBudget budget = ResolveBudgetForPressure(globalQualityWeight, pressureLevel);
            return budget.ResolvePoolCapacity(fluidType);
        }

        private static VfxComputeParticleBudget ResolveContinuousBudget(float globalQualityWeight, float pressure01)
        {
            float q = math.saturate(globalQualityWeight);
            float minToMiddle = math.smoothstep(0f, 0.45f, q);
            float middleToMaximum = math.smoothstep(0.35f, 0.85f, q);
            float maximumToOverkill = math.smoothstep(0.72f, 1f, q);
            float middlePressure01 = math.smoothstep(0.18f, 0.45f, math.saturate(pressure01));
            float emergencyPressure01 = math.smoothstep(0.48f, 0.90f, math.saturate(pressure01));

            int marineSnowCount = ResolveContinuousBudgetCount(
                MinimumQualityMarineSnowCount,
                MiddleQualityMarineSnowCount,
                MaximumQualityMarineSnowCount,
                OverkillQualityMarineSnowCount,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill,
                middlePressure01,
                emergencyPressure01);
            int bubbleCount = ResolveContinuousBudgetCount(
                MinimumQualityBubbleCount,
                MiddleQualityBubbleCount,
                MaximumQualityBubbleCount,
                OverkillQualityBubbleCount,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill,
                middlePressure01,
                emergencyPressure01);
            int debrisCount = ResolveContinuousBudgetCount(
                MinimumQualityDebrisCount,
                MiddleQualityDebrisCount,
                MaximumQualityDebrisCount,
                OverkillQualityDebrisCount,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill,
                middlePressure01,
                emergencyPressure01);
            float stepDistanceMeters = ResolveContinuousBudgetFloat(
                MinimumQualityStepDistanceMeters,
                MiddleQualityStepDistanceMeters,
                MaximumQualityStepDistanceMeters,
                OverkillQualityStepDistanceMeters,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill);
            stepDistanceMeters = math.lerp(stepDistanceMeters, math.max(stepDistanceMeters, MiddleQualityStepDistanceMeters), middlePressure01);
            stepDistanceMeters = math.lerp(stepDistanceMeters, MinimumQualityStepDistanceMeters, emergencyPressure01);

            float shadowTapFloat = ResolveContinuousBudgetFloat(
                MinimumQualityShadowTaps,
                MiddleQualityShadowTaps,
                MaximumQualityShadowTaps,
                OverkillQualityShadowTaps,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill);
            shadowTapFloat = math.lerp(shadowTapFloat, math.min(shadowTapFloat, MiddleQualityShadowTaps), middlePressure01);
            shadowTapFloat = math.lerp(shadowTapFloat, MinimumQualityShadowTaps, emergencyPressure01);

            float flowFramesFloat = ResolveContinuousBudgetFloat(
                MinimumQualityFlowResampleFrames,
                MiddleQualityFlowResampleFrames,
                MaximumQualityFlowResampleFrames,
                OverkillQualityFlowResampleFrames,
                minToMiddle,
                middleToMaximum,
                maximumToOverkill);
            flowFramesFloat = math.lerp(flowFramesFloat, MinimumQualityFlowResampleFrames, emergencyPressure01);

            return new VfxComputeParticleBudget(
                marineSnowCount + bubbleCount + debrisCount,
                marineSnowCount,
                bubbleCount,
                debrisCount,
                math.max(0.05f, stepDistanceMeters),
                math.clamp((int)(shadowTapFloat + 0.5f), 0, OverkillQualityShadowTaps),
                math.clamp((int)(flowFramesFloat + 0.5f), 0, MiddleQualityFlowResampleFrames));
        }

        private static int ResolveContinuousBudgetCount(
            int minimum,
            int middle,
            int maximum,
            int overkill,
            float minToMiddle,
            float middleToMaximum,
            float maximumToOverkill,
            float middlePressure01,
            float emergencyPressure01)
        {
            float value = ResolveContinuousBudgetFloat(minimum, middle, maximum, overkill, minToMiddle, middleToMaximum, maximumToOverkill);
            value = math.lerp(value, math.min(value, middle), middlePressure01);
            value = math.lerp(value, minimum, emergencyPressure01);
            return math.max(0, (int)(value + 0.5f));
        }

        private static float ResolveContinuousBudgetFloat(
            float minimum,
            float middle,
            float maximum,
            float overkill,
            float minToMiddle,
            float middleToMaximum,
            float maximumToOverkill)
        {
            float value = math.lerp(minimum, middle, minToMiddle);
            value = math.lerp(value, maximum, middleToMaximum);
            return math.lerp(value, overkill, maximumToOverkill);
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
                return ResolveNonCriticalVfxSurvivalCount(fluidType, activeParticleCount, pressureLevel);
            }

            if (pressureLevel >= 3)
                return math.max(64, activeParticleCount * EmergencyMarineSnowMultiplierPermille / 1000);

            return activeParticleCount;
        }

        private static int ResolveNonCriticalVfxSurvivalCount(
            VFXEmissionProfile.FluidType fluidType,
            int activeParticleCount,
            byte pressureLevel)
        {
            int survivalFloor = fluidType == VFXEmissionProfile.FluidType.Bubble
                ? EmergencyBubbleSurvivalCount
                : EmergencyDebrisSurvivalCount;
            int floor = math.min(activeParticleCount, survivalFloor);
            float pressure01 = pressureLevel == byte.MaxValue
                ? 1f
                : math.saturate(pressureLevel * 0.33333334f);
            float emergency01 = math.smoothstep(0.48f, 0.90f, pressure01);
            float survivalScale = math.lerp(1f, EmergencyNonCriticalVfxMultiplierPermille / 1000f, emergency01);
            int scaled = math.max(floor, (int)(activeParticleCount * survivalScale + 0.5f));
            return math.clamp(scaled, floor, activeParticleCount);
        }

        /// <summary>
        /// Resolves the number of default-thread groups needed for a particle count.
        /// </summary>
        /// <param name="particleCount">Particle count.</param>
        /// <returns>Dispatch group count, or 0 when no portable dispatch can be submitted.</returns>
        public static int ResolveDispatchGroups(int particleCount)
        {
            return ResolveDispatchGroups(particleCount, DefaultThreadsPerGroup);
        }

        /// <summary>
        /// Resolves dispatch groups from a caller-owned kernel thread-group size.
        /// </summary>
        /// <param name="particleCount">Particle count.</param>
        /// <param name="threadGroupSize">Queried kernel thread-group size.</param>
        /// <returns>Dispatch group count, or 0 when no portable dispatch can be submitted.</returns>
        public static int ResolveDispatchGroups(int particleCount, int threadGroupSize)
        {
            if (particleCount <= 0 || threadGroupSize <= 0)
                return 0;

            long groups = ((long)particleCount + threadGroupSize - 1L) / threadGroupSize;
            return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }
    }

    /// <summary>
    /// Immutable compute-particle budget row.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = VfxComputeParticleBudgetCatalogLayout.VfxComputeParticleBudgetStrideBytes)]
    public struct VfxComputeParticleBudget
    {
        /// <summary>Minimum-quality budget row.</summary>
        public static readonly VfxComputeParticleBudget MinimumQuality = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.MinimumQualityParticleCount,
            VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
            VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount,
            VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount,
            VfxComputeParticleBudgetCatalog.MinimumQualityStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.MinimumQualityShadowTaps,
            VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames);

        /// <summary>Middle-quality budget row.</summary>
        public static readonly VfxComputeParticleBudget MiddleQuality = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.MiddleQualityParticleCount,
            VfxComputeParticleBudgetCatalog.MiddleQualityMarineSnowCount,
            VfxComputeParticleBudgetCatalog.MiddleQualityBubbleCount,
            VfxComputeParticleBudgetCatalog.MiddleQualityDebrisCount,
            VfxComputeParticleBudgetCatalog.MiddleQualityStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.MiddleQualityShadowTaps,
            VfxComputeParticleBudgetCatalog.MiddleQualityFlowResampleFrames);

        /// <summary>Maximum-quality budget row.</summary>
        public static readonly VfxComputeParticleBudget MaximumQuality = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.MaximumQualityParticleCount,
            VfxComputeParticleBudgetCatalog.MaximumQualityMarineSnowCount,
            VfxComputeParticleBudgetCatalog.MaximumQualityBubbleCount,
            VfxComputeParticleBudgetCatalog.MaximumQualityDebrisCount,
            VfxComputeParticleBudgetCatalog.MaximumQualityStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.MaximumQualityShadowTaps,
            VfxComputeParticleBudgetCatalog.MaximumQualityFlowResampleFrames);

        /// <summary>Visual-overkill budget row.</summary>
        public static readonly VfxComputeParticleBudget OverkillQuality = new VfxComputeParticleBudget(
            VfxComputeParticleBudgetCatalog.OverkillQualityParticleCount,
            VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount,
            VfxComputeParticleBudgetCatalog.OverkillQualityBubbleCount,
            VfxComputeParticleBudgetCatalog.OverkillQualityDebrisCount,
            VfxComputeParticleBudgetCatalog.OverkillQualityStepDistanceMeters,
            VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps,
            VfxComputeParticleBudgetCatalog.OverkillQualityFlowResampleFrames);

        /// <summary>Total particle count budget.</summary>
        [FieldOffset(0)]
        public readonly int ParticleCount;

        /// <summary>Marine-snow pool count.</summary>
        [FieldOffset(4)]
        public readonly int MarineSnowCount;

        /// <summary>Bubble pool count.</summary>
        [FieldOffset(8)]
        public readonly int BubbleCount;

        /// <summary>Debris pool count.</summary>
        [FieldOffset(12)]
        public readonly int DebrisCount;

        /// <summary>Collision/integration step distance in meters.</summary>
        [FieldOffset(16)]
        public readonly float StepDistanceMeters;

        /// <summary>Fake depth/fog occlusion tap count. Particle shadow casting remains forbidden.</summary>
        [FieldOffset(20)]
        public readonly int ShadowTaps;

        /// <summary>Flow resample cadence in frames. Zero disables flow resampling.</summary>
        [FieldOffset(24)]
        public readonly int FlowResampleFrames;

        [FieldOffset(28)]
        private readonly int _pad0;

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
            _pad0 = 0;
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
