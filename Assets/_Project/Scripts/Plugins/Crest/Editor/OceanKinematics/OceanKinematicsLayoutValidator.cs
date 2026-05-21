#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.Physics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Physics.Editor
{
    /// <summary>
    /// Editor compile/import guard for ocean kinematics DTO byte layout.
    /// </summary>
    [InitializeOnLoad]
    public static class OceanKinematicsLayoutValidator
    {
        static OceanKinematicsLayoutValidator()
        {
            ValidateOrThrow();
        }

        private static void ValidateOrThrow()
        {
            int resultSize = UnsafeUtility.SizeOf<FluidSampleResultDTO>();
            int resultAlign = UnsafeUtility.AlignOf<FluidSampleResultDTO>();
            int waterOffset = (int)Marshal.OffsetOf<FluidSampleResultDTO>(nameof(FluidSampleResultDTO.WaterHeight));
            int velocityOffset = (int)Marshal.OffsetOf<FluidSampleResultDTO>(nameof(FluidSampleResultDTO.SurfaceVelocity));
            int waveSize = UnsafeUtility.SizeOf<GerstnerWaveDTO>();
            int waveStateHashOffset = (int)Marshal.OffsetOf<GerstnerWaveDTO>(nameof(GerstnerWaveDTO.StateHash));
            int waveFlagsOffset = (int)Marshal.OffsetOf<GerstnerWaveDTO>(nameof(GerstnerWaveDTO.Flags));
            if (resultSize != OceanKinematicsConstants.FluidSampleResultBytes ||
                resultAlign < 4 ||
                waterOffset != 0 ||
                velocityOffset != 4 ||
                waveSize != OceanKinematicsConstants.GerstnerWaveBytes ||
                waveStateHashOffset != 28 ||
                waveFlagsOffset != 32 ||
                !OceanKinematicsLayout.Validate())
            {
                throw new InvalidOperationException(
                    "Ocean kinematics DTO layout violation. FluidSampleResultDTO must be 16 bytes: WaterHeight offset 0, SurfaceVelocity offset 4.");
            }
        }
    }
}
#endif
