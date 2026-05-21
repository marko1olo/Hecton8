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
            AssertOffset<FoamComputeParamsDTO>(nameof(FoamComputeParamsDTO.AdvectionVectors), 0);
            AssertOffset<FoamComputeParamsDTO>(nameof(FoamComputeParamsDTO.DecayAndIntensity), 16);
            AssertSize<FoamWakeImpactDTO>(JacobianFoamContracts.WakeImpactStrideBytes);
            AssertOffset<FoamWakeImpactDTO>(nameof(FoamWakeImpactDTO.LocalPositionRadius), 0);
            AssertOffset<FoamWakeImpactDTO>(nameof(FoamWakeImpactDTO.IntensityAgeFlags), 16);
            AssertSize<FoamTuningDTO>(JacobianFoamContracts.TuningStrideBytes);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.PinchThreshold), 0);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.TextureWorldSizeMeters), 28);
            AssertOffset<FoamTuningDTO>(nameof(FoamTuningDTO.Version), 48);
            AssertSize<FoamRenderTelemetryEntry>(JacobianFoamContracts.TelemetryEntryStrideBytes);
            AssertOffset<FoamRenderTelemetryEntry>(nameof(FoamRenderTelemetryEntry.ScrollOffset), 32);
            AssertOffset<FoamRenderTelemetryEntry>(nameof(FoamRenderTelemetryEntry.DecayRate), 56);
            AssertSize<FoamAestheticProfileDTO>(JacobianFoamContracts.ProfileStrideBytes);
            AssertOffset<FoamAestheticProfileDTO>(nameof(FoamAestheticProfileDTO.NameHash), 0);
            AssertOffset<FoamAestheticProfileDTO>(nameof(FoamAestheticProfileDTO.Reserved0), 60);
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
