using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    /// <summary>
    /// Cold-path native buffer maintenance for player-critical DSP buffers.
    /// </summary>
    internal static class PlayerCriticalBufferJobs
    {
        public static void Clear(NativeArray<float> buffer, int count)
        {
            if (!buffer.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, buffer.Length);
            if (safeCount <= 0)
                return;

            // COLD NATIVE CLEAR: audio configuration/reset path only; producer thread is stopped before this is called.
            unsafe
            {
                void* bufferPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                long byteCount = (long)safeCount * UnsafeUtility.SizeOf<float>();
                UnsafeUtility.MemClear(bufferPtr, byteCount);
            }
        }
    }
}
