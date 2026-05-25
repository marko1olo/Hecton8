#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.VFX;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class JacobianFoamLayoutValidator
    {
        private const int FoamParamsAdvectionVectorsOffset = 0;
        private const int FoamParamsDecayAndIntensityOffset = 16;
        private const int FoamWakeImpactLocalPositionRadiusOffset = 0;
        private const int FoamWakeImpactIntensityAgeFlagsOffset = 16;
        private const int FoamTuningPinchThresholdOffset = 0;
        private const int FoamTuningTextureWorldSizeOffset = 28;
        private const int FoamTuningVersionOffset = 48;
        private const int FoamTelemetryScrollOffset = 32;
        private const int FoamTelemetryDecayRateOffset = 56;
        private const int FoamProfileNameHashOffset = 0;
        private const int FoamProfileReserved0Offset = 60;

        static JacobianFoamLayoutValidator()
        {
            Validate();
        }

        [MenuItem("HECTON-8/Rendering/Validate Jacobian Foam GPU Layouts")]
        public static void Validate()
        {
            if (!JacobianFoamContracts.ValidateRuntimeLayouts())
                throw new InvalidOperationException("SHINOBU_266 jacobian foam runtime layout mismatch.");

            AssertSize<FoamComputeParamsDTO>(JacobianFoamContracts.ParamsStrideBytes);
            AssertOffset<FoamComputeParamsDTO>(nameof(FoamComputeParamsDTO.AdvectionVectors), FoamParamsAdvectionVectorsOffset);
            AssertOffset<FoamComputeParamsDTO>(nameof(FoamComputeParamsDTO.DecayAndIntensity), FoamParamsDecayAndIntensityOffset);
            AssertSize<FoamWakeImpactDTO>(JacobianFoamContracts.WakeImpactStrideBytes);
            AssertOffset<FoamWakeImpactDTO>(nameof(FoamWakeImpactDTO.LocalPositionRadius), FoamWakeImpactLocalPositionRadiusOffset);
            AssertOffset<FoamWakeImpactDTO>(nameof(FoamWakeImpactDTO.IntensityAgeFlags), FoamWakeImpactIntensityAgeFlagsOffset);
            AssertSize<FoamTuningDTO>(JacobianFoamContracts.TuningStrideBytes);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.PinchThreshold), FoamTuningPinchThresholdOffset);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.TextureWorldSizeMeters), FoamTuningTextureWorldSizeOffset);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.Version), FoamTuningVersionOffset);
            AssertSize<FoamRenderTelemetryEntry>(JacobianFoamContracts.TelemetryEntryStrideBytes);
            AssertOffset<FoamRenderTelemetryEntry>(nameof(FoamRenderTelemetryEntry.ScrollOffset), FoamTelemetryScrollOffset);
            AssertOffset<FoamRenderTelemetryEntry>(nameof(FoamRenderTelemetryEntry.DecayRate), FoamTelemetryDecayRateOffset);
            AssertSize<FoamAestheticProfileDTO>(JacobianFoamContracts.ProfileStrideBytes);
            AssertOffset<FoamAestheticProfileDTO>(nameof(FoamAestheticProfileDTO.NameHash), FoamProfileNameHashOffset);
            AssertOffset<FoamAestheticProfileDTO>(nameof(FoamAestheticProfileDTO.Reserved0), FoamProfileReserved0Offset);
        }

        private static void AssertSize<T>(int expectedBytes) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException(typeof(T).Name + " size " + actualBytes + " != " + expectedBytes);
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset " + actualOffset + " != " + expectedOffset);
        }
    }
}
#endif
