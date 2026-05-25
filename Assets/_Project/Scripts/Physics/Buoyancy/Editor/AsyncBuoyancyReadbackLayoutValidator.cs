#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    public static class AsyncBuoyancyReadbackLayoutValidator
    {
        private const int RequiredAlignmentBytes = 16;

        [MenuItem("HECTON-8/Physics/Validate Async Buoyancy Readback Layout")]
        public static void ValidateFromMenu()
        {
            bool valid = Validate();
            if (valid)
                Debug.Log("SHINOBU_264 async buoyancy readback layout valid: ReadbackRequestDTO 16B offsets [0,8,12], wave params 64B, telemetry 64B.");
            else
                Debug.LogError("SHINOBU_264 async buoyancy readback layout invalid.");
        }

        public static bool Validate()
        {
            bool pointerAligned;
            unsafe
            {
                pointerAligned = ValidateTempBufferPointerAlignment();
            }

            return AsyncBuoyancyReadbackLayout.Validate() &&
                   UnsafeUtility.SizeOf<ReadbackRequestDTO>() == AsyncBuoyancyReadbackConstants.ReadbackRequestBytes &&
                   (UnsafeUtility.SizeOf<ReadbackRequestDTO>() % RequiredAlignmentBytes) == 0 &&
                   Marshal.OffsetOf<ReadbackRequestDTO>(nameof(ReadbackRequestDTO.LocalXZ)).ToInt32() == 0 &&
                   Marshal.OffsetOf<ReadbackRequestDTO>(nameof(ReadbackRequestDTO.ResultHeight)).ToInt32() == 8 &&
                   Marshal.OffsetOf<ReadbackRequestDTO>(nameof(ReadbackRequestDTO.EntityHash)).ToInt32() == 12 &&
                   UnsafeUtility.SizeOf<AsyncBuoyancyWaveParametersDTO>() == AsyncBuoyancyReadbackConstants.WaveParametersBytes &&
                   UnsafeUtility.SizeOf<ReadbackTelemetryEntry>() == AsyncBuoyancyReadbackConstants.ReadbackTelemetryBytes &&
                   pointerAligned;
        }

        private static unsafe bool ValidateTempBufferPointerAlignment()
        {
            NativeArray<ReadbackRequestDTO> sample = new NativeArray<ReadbackRequestDTO>(
                2,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                ReadbackRequestDTO* ptr = (ReadbackRequestDTO*)sample.GetUnsafePtr();
                long first = (long)ptr;
                long second = (long)(ptr + 1);
                return (first & (RequiredAlignmentBytes - 1L)) == 0L &&
                       (second - first) == AsyncBuoyancyReadbackConstants.ReadbackRequestBytes;
            }
            finally
            {
                sample.Dispose();
            }
        }
    }
}
#endif
