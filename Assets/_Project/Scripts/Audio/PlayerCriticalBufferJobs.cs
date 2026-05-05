using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    /// <summary>
    /// Cold-path Burst jobs for player-critical DSP buffer maintenance.
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

            ClearFloatJob job = new ClearFloatJob
            {
                Buffer = buffer
            };
            // COLD SYNC JOB: audio configuration/reset path only; producer thread is stopped before this is called.
            job.Schedule(safeCount, 256).Complete();
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearFloatJob : IJobParallelFor
        {
            public NativeArray<float> Buffer;

            public void Execute(int index)
            {
                Buffer[index] = 0f;
            }
        }
    }
}
